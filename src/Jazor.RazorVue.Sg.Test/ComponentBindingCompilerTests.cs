using Jazor.Common;
using Jazor.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jazor.RazorVue.Sg.Test;

[TestClass]
public sealed class ComponentBindingCompilerTests
{
    private static (INamedTypeSymbol, SemanticModel) CompileAndGetSymbol(
        string source, string className, params MetadataReference[] references)
    {
        var tree = CSharpSyntaxTree.ParseText(source, TestMetadataReferences.PreviewParseOptions);
        var compilation = CSharpCompilation.Create("ComponentBindingConsumer", [tree],
            TestMetadataReferences.Net11.Concat(references)
                .Append(MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Components.ComponentBase).Assembly.Location))
                .DistinctBy(static reference => reference.Display),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
        var model = compilation.GetSemanticModel(tree);
        var declaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(type => type.Identifier.ValueText == className);
        return ((INamedTypeSymbol)model.GetDeclaredSymbol(declaration)!, model);
    }

    private static void AssertScriptEqual(string expected, string? actual)
        => Assert.AreEqual(expected.ReplaceLineEndings("\n"), actual?.ReplaceLineEndings("\n"));

    [TestMethod]
    public async Task Convert_ClassUsingComponentLibraryUnionAssignments_GeneratesErasedValues()
    {
        var code = """
            using ECMAScript;
            using ECMAScript.ElementPlus;
            using ECMAScript.TDesign;
            using static ECMAScript.Vue;

            namespace Demo
            {
                [ECMAScriptModule("components/union-contract.mjs")]
                public static class ComponentUnionModule
                {
                    public static TMenuValue CreateMenuValue(Number value) => value;

                    public static TMenuWidthValue CreateMenuWidth(TMenuWidthValueOption3Item[] values) => values;

                    public static ElUploadBeforeUploadResult CreateUploadFile(FileRef value) => value;

                    public static ElUploadBeforeUploadResult CreateUploadBlob(Blob value) => value;

                    public static ElUploadBeforeUploadResult CreateUploadPromise(IPromise<VueValue?> value) => new(value);

                }
            }
            """;

        var (_, semanticModel) = CompileAndGetSymbol(
            code,
            "ComponentUnionModule",
            MetadataReference.CreateFromFile(typeof(ECMAScript.ECMAScriptModuleAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Vue).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.ElementPlus.ElementPlus).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.TDesign.TDesign).Assembly.Location));
        var moduleSymbol = semanticModel.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(static declaration => declaration.Identifier.Text == "ComponentUnionModule")
            .Select(declaration => semanticModel.GetDeclaredSymbol(declaration))
            .OfType<INamedTypeSymbol>()
            .Single();

        var converter = new AstConverter(moduleSymbol, semanticModel);
        var module = await converter.Convert();
        var script = module?.ToKnRECMAScript();

        AssertScriptEqual(
@"export function CreateMenuValue(value) {
  return value;
}
export function CreateMenuWidth(values) {
  return values;
}
export function CreateUploadFile(value) {
  return value;
}
export function CreateUploadBlob(value) {
  return value;
}
export function CreateUploadPromise(value) {
  return value;
}
", script);
    }

    [TestMethod]
    [DataRow("AsFile")]
    [DataRow("AsBlob")]
    public async Task Convert_TaggedUnionProjectionWithAssignableBranches_ReportsLostTag(string projection)
    {
        var code = $$"""
            using ECMAScript;
            using ECMAScript.ElementPlus;
            [ECMAScriptModule("components/tagged-projection.mjs")]
            public static class TaggedProjectionModule
            {
                public static {{(projection == "AsFile" ? "FileRef" : "Blob")}}? Read(ElUploadBeforeUploadResult value)
                    => value.{{projection}};
            }
            """;
        var (symbol, model) = CompileAndGetSymbol(code, "TaggedProjectionModule",
            MetadataReference.CreateFromFile(typeof(ECMAScript.ECMAScriptModuleAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Vue).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.ElementPlus.ElementPlus).Assembly.Location));
        var converter = new AstConverter(symbol, model);
        var error = await Assert.ThrowsAsync<OperationTransformationException>(converter.Convert);
        StringAssert.Contains(error.Message, "exact branch tag", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Convert_ClassUsingEcmaScriptVueVuetifyProxy_GeneratesVuetifyImportsFromNameAttributes()
    {
        var code = """
            using ECMAScript;
            using ECMAScript.Vuetify;
            using static ECMAScript.Vue;

            namespace Demo
            {
                [ECMAScriptModule("app/main.mjs")]
                public static class AppModule
                {
                    public static VueComponentPublicInstance Boot(IVueComponent component)
                    {
                        var app = Vue.CreateApp(component);
                        app.Use(Vuetify.CreateVuetify(new VuetifyOptions
                        {
                            Components = new VuetifyComponentRegistry
                            {
                                VBtn = VuetifyComponents.VBtn
                            },
                            Directives = new VuetifyDirectiveRegistry
                            {
                                Ripple = VuetifyDirectives.Ripple
                            },
                            Theme = new VuetifyThemeOptions
                            {
                                DefaultTheme = "jazor"
                            }
                        }));
                        app.Component("v-btn", VuetifyComponents.VBtn);
                        app.Directive("ripple", VuetifyDirectives.Ripple);
                        return app.Mount("#app");
                    }
                }
            }
            """;

        var (_, semanticModel) = CompileAndGetSymbol(
            code,
            "AppModule",
            MetadataReference.CreateFromFile(typeof(ECMAScript.ECMAScriptModuleAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Contract.IUIComponent).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Vue).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Vuetify.Vuetify).Assembly.Location));
        var appModule = semanticModel.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(static x => x.Identifier.Text == "AppModule")
            .Select(x => semanticModel.GetDeclaredSymbol(x))
            .OfType<INamedTypeSymbol>()
            .Single();

        var converter = new AstConverter(appModule, semanticModel);
        var module = await converter.Convert();
        var script = module?.ToKnRECMAScript();

        Assert.AreEqual(
@"import ""vuetify/lib/components/VBtn/VBtn.css"";
import { createApp } from ""vue"";
import { createVuetify } from ""vuetify"";
import { VBtn } from ""vuetify/components/VBtn"";
import { Ripple } from ""vuetify/directives/ripple"";
export function Boot(component) {
  let app = createApp(component);
  app.use(createVuetify({
    components: { VBtn: VBtn },
    directives: { Ripple: Ripple },
    theme: { defaultTheme: ""jazor"" }
  }));
  app.component(""v-btn"", VBtn);
  app.directive(""ripple"", Ripple);
  return app.mount(""#app"");
}
".ReplaceLineEndings("\n"), script?.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public async Task Convert_ClassUsingVueBaseRegistriesWithStringKeys_GeneratesRegistryObjectLiterals()
    {
        var code = """
            using ECMAScript;
            using ECMAScript.Vuetify;
            using static ECMAScript.Vue;

            namespace Demo
            {
                [ECMAScriptModule("app/main.mjs")]
                public static class AppModule
                {
                    public static IVueComponent Create()
                        => Vue.DefineComponent(new VueComponentOptions
                        {
                            Name = "ParentView",
                            Components = new VueComponentRegistry
                            {
                                ["ChildView"] = Vue.DefineComponent(new VueComponentOptions
                                {
                                    Name = "ChildView"
                                })
                            },
                            Directives = new VueDirectiveRegistry
                            {
                                ["Ripple"] = VuetifyDirectives.Ripple
                            }
                        });
                }
            }
            """;

        var (_, semanticModel) = CompileAndGetSymbol(
            code,
            "AppModule",
            MetadataReference.CreateFromFile(typeof(ECMAScript.ECMAScriptModuleAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Contract.IUIComponent).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Vue).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ECMAScript.Vuetify.Vuetify).Assembly.Location));
        var appModule = semanticModel.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(static x => x.Identifier.Text == "AppModule")
            .Select(x => semanticModel.GetDeclaredSymbol(x))
            .OfType<INamedTypeSymbol>()
            .Single();

        var converter = new AstConverter(appModule, semanticModel);
        var module = await converter.Convert();
        var script = module?.ToKnRECMAScript();

        Assert.AreEqual(
@"import { defineComponent } from ""vue"";
import { Ripple } from ""vuetify/directives/ripple"";
export function Create() {
  return defineComponent({
    name: ""ParentView"",
    components: { ChildView: defineComponent({ name: ""ChildView"" }) },
    directives: { Ripple: Ripple }
  });
}
".ReplaceLineEndings("\n"), script?.ReplaceLineEndings("\n"));
    }

}
