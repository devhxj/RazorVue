using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using File = System.IO.File;

/// <summary>Uses pinned upstream prose without translating or rewriting it.</summary>
internal sealed class BindingUpstreamDocumentation
{
    private readonly Dictionary<string, string> _summaries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(string Member, string Text)>> _uses = new(StringComparer.Ordinal);
    private readonly HashSet<string> _documentedTypes = new(StringComparer.Ordinal);

    public BindingUpstreamDocumentation(string repository)
    {
        var upstream = Path.Combine(repository, "src", "ECMAScript.Vue.Generator", "upstream");
        ReadWebTypes("ECMAScript.ElementPlus", Path.Combine(upstream, "element-plus", "2.14.5", "web-types.json"));
        ReadWebTypes("ECMAScript.Vuetify", Path.Combine(upstream, "vuetify", "4.2.1", "web-types.json"));
        ReadOriginals(Path.Combine(repository, "src", "ECMAScript.Vue.Generator", "documentation", "originals.json"));
        foreach (var library in BindingDocumentationGenerator.Libraries)
        {
            var declarations = Directory.GetFiles(Path.Combine(repository, "src", library), "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.Contains("\\bin\\") && !path.Contains("\\obj\\"))
                .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)).GetRoot()
                    .DescendantNodes().OfType<MemberDeclarationSyntax>())
                .ToArray();
            foreach (var property in declarations.OfType<PropertyDeclarationSyntax>())
            {
                var summary = ReadSummary(property);
                var parent = property.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
                if (summary is null || parent is null) continue;
                var key = library + "|" + parent.Identifier.ValueText + "." + property.Identifier.ValueText;
                _summaries.TryAdd(key, summary);
            }
            // The same prop can be exposed as a config record and a Razor parameter.
            // Copy only between the matching component's props/config surfaces.
            foreach (var type in declarations.OfType<BaseTypeDeclarationSyntax>())
            {
                var typeName = type.Identifier.ValueText;
                if (ReadSummary(type) is not null) _documentedTypes.Add(library + "|" + typeName);
                var componentName = Regex.Replace(typeName, "(Props|Config)$", string.Empty);
                if (typeName == componentName) continue;
                foreach (var property in type.ChildNodes().OfType<PropertyDeclarationSyntax>())
                    if (_summaries.TryGetValue(library + "|" + componentName + "." + property.Identifier.ValueText, out var summary))
                        _summaries.TryAdd(library + "|" + typeName + "." + property.Identifier.ValueText, summary);
            }
            foreach (var (target, source) in new[] {
                ("ElInstallOptions", "ElConfigProvider"), ("ElLoadingOptions", "ElLoading"),
                ("ElTagTooltipProps", "ElTooltip"), ("ElColSizeProps", "ElCol"),
                ("ElMessageConfig", "ElMessage"), ("ElTableV2Column", "ElColumn") })
                foreach (var property in declarations.OfType<PropertyDeclarationSyntax>()
                    .Where(p => p.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == target))
                    if (_summaries.TryGetValue(library + "|" + source + "." + property.Identifier.ValueText, out var summary))
                        _summaries.TryAdd(library + "|" + target + "." + property.Identifier.ValueText, summary);
            foreach (var property in declarations.OfType<PropertyDeclarationSyntax>())
            {
                if (property.Identifier.ValueText.StartsWith("As", StringComparison.Ordinal) && property.ExpressionBody is not null) continue;
                var owner = property.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
                if (owner is null) continue;
                var memberName = owner + "." + property.Identifier.ValueText;
                var summary = _summaries.GetValueOrDefault(library + "|" + memberName);
                if (summary is null) continue;
                foreach (var type in property.Type.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    var key = library + "|" + type.Identifier.ValueText;
                    if (!_uses.TryGetValue(key, out var uses)) _uses[key] = uses = [];
                    if (!uses.Any(use => use.Member == memberName)) uses.Add((memberName, summary));
                }
            }
        }
    }

    public bool HasTypeDocumentation(string library, string name) => _documentedTypes.Contains(library + "|" + name);

    public string? Find(string library, MemberDeclarationSyntax member)
    {
        var parent = member.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        var name = member switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            PropertyDeclarationSyntax property => parent?.Identifier.ValueText + "." + property.Identifier.ValueText,
            DelegateDeclarationSyntax callback => callback.Identifier.ValueText,
            MethodDeclarationSyntax method => parent?.Identifier.ValueText + "." + method.Identifier.ValueText,
            _ => null
        };
        if (name is null) return null;
        if (_summaries.TryGetValue(library + "|" + name, out var result)) return result;
        if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax &&
            _uses.TryGetValue(library + "|" + name, out var uses))
        {
            var kind = member is DelegateDeclarationSyntax ? "回调签名" :
                member is EnumDeclarationSyntax ? "可选取值" : "参数类型";
            return "用于 " + string.Join("、", uses.Select(use => use.Member)) + " 的" + kind + "。\n" +
                string.Join("\n", uses.Select(use => use.Text).Distinct(StringComparer.Ordinal));
        }
        return null;
    }

    private void ReadOriginals(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var row in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            var entry = row.Deserialize<BindingDocumentationSnapshot.Entry>()!;
            var scope = entry.Scope;
            var member = entry.Member;
            if (entry.Library == "ECMAScript.ElementPlus")
            {
                // Attribute tables describe a component; named helper tables retain
                // their own scope, so e.g. CascaderProps.value is not Cascader.value.
                scope = Regex.Replace(scope, @"\s+(Attributes|Props|Exposes|Events|Slots)$", "", RegexOptions.IgnoreCase);
                if (scope == "Options") scope = Path.GetFileNameWithoutExtension(entry.Source) + "-options";
                if (scope is "Attributes" or "Props" or "Exposes" or "Events" or "Slots")
                    scope = Path.GetFileNameWithoutExtension(entry.Source);
                scope = "El" + Pascal(scope);
                if (entry.Scope.EndsWith("Events")) member = "On" + Pascal(member);
                else if (entry.Scope.EndsWith("Slots")) member = Pascal(member) + "Slot";
                else member = Pascal(member);
            }
            else if (entry.Library == "ECMAScript.Vuetify")
            {
                var parts = member.Split(':', 2);
                if (parts.Length == 2)
                    member = parts[0] switch { "events" => "On" + Pascal(parts[1]), "slots" => Pascal(parts[1]) + "Slot", _ => Pascal(parts[1]) };
            }
            _summaries.TryAdd(entry.Library + "|" + scope + (member.Length == 0 ? "" : "." + member), entry.Text);
        }
    }

    private static string Pascal(string value)
        => string.Concat(Regex.Split(value, @"[^A-Za-z0-9]+").Where(part => part.Length > 0)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private void ReadWebTypes(string library, string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var elementPlus = library == "ECMAScript.ElementPlus";
        foreach (var tag in document.RootElement.GetProperty("contributions").GetProperty("html")
            .GetProperty(elementPlus ? "vue-components" : "tags").EnumerateArray())
        {
            var sourceName = tag.GetProperty("name").GetString()!;
            var name = sourceName.Contains('-') ? string.Concat(sourceName.Split('-').Select(static part => char.ToUpperInvariant(part[0]) + part[1..])) : sourceName;
            if (tag.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(description.GetString()))
                _summaries[library + "|" + name] = description.GetString()!;
            if (!tag.TryGetProperty(elementPlus ? "props" : "attributes", out var attributes)) continue;
            foreach (var property in attributes.EnumerateArray())
            {
                if (!property.TryGetProperty("description", out description) || description.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(description.GetString())) continue;
                var propertyName = property.GetProperty("name").GetString()!;
                propertyName = string.Concat(propertyName.Split('-').Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
                _summaries[library + "|" + name + "." + propertyName] = description.GetString()!;
            }
        }
    }

    private static string? ReadSummary(MemberDeclarationSyntax member)
        => member.GetLeadingTrivia().Select(static trivia => trivia.GetStructure()).OfType<DocumentationCommentTriviaSyntax>()
            .SelectMany(static documentation => documentation.Content.OfType<XmlElementSyntax>())
            .Where(static element => element.StartTag.Name.LocalName.ValueText == "summary")
            .Select(static element => System.Net.WebUtility.HtmlDecode(Regex.Replace(string.Concat(element.Content), @"(?m)^\s*///\s?", string.Empty)).Trim())
            .FirstOrDefault();
}