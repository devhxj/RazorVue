#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

var root = Directory.GetCurrentDirectory();
var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
var sdkVersion = props.Descendants("JazorSdkVersion").Single().Value;
var version = args.Length > 0 ? args[0] : props.Descendants("RazorVuePackageVersion").Single().Value;
var feed = Path.GetFullPath("artifacts/packages");
Directory.CreateDirectory(feed);

// Detached consumers restore the published SDK and the RazorVue package candidates
// from the same feed. Only the candidates are uploaded by this repository.
string[] sdkPackages =
[
    "Jazor", "ECMAScript.Vue", "ECMAScript.Style", "ECMAScript.VueRoute"
];
using var client = new HttpClient();
foreach (var id in sdkPackages)
{
    var filename = $"{id}.{sdkVersion}.nupkg";
    var target = Path.Combine(feed, filename);
    if (File.Exists(target))
        continue;
    var key = id.ToLowerInvariant();
    var url = $"https://api.nuget.org/v3-flatcontainer/{key}/{sdkVersion}/{key}.{sdkVersion}.nupkg";
    await File.WriteAllBytesAsync(target, await client.GetByteArrayAsync(url));
}

var componentPackages = new (string Id, string Library, string License)[]
{
    ("ECMAScript.ElementPlus", "element-plus", "LICENSE"),
    ("ECMAScript.Lucide", "lucide-vue-next", "LUCIDE-LICENSE"),
    ("ECMAScript.TDesign", "tdesign-vue-next", "LICENSE"),
    ("ECMAScript.VueDataUi", "vue-data-ui", "VUE-DATA-UI-LICENSE"),
    ("ECMAScript.Vuetify", "vuetify", "LICENSE.md")
};

foreach (var (id, library, license) in componentPackages)
{
    await PackAsync(id);
    VerifyComponentPackage(id, library, license);
}

await PackAsync("Jazor.Vue");
await PackAsync("Jazor.Admin");

void VerifyComponentPackage(string id, string library, string license)
{
    using var package = ZipFile.OpenRead(Path.Combine(feed, $"{id}.{version}.nupkg"));
    var resourceRoot = $"jazor/{library}";
    string[] requiredEntries =
    [
        "README.md", $"lib/net11.0/{id}.dll", $"lib/net11.0/{id}.xml",
        $"buildTransitive/{id}.targets", $"{resourceRoot}/manifest.json", $"{resourceRoot}/licenses/{license}"
    ];
    foreach (var entry in requiredEntries)
        if (package.GetEntry(entry) is null)
            throw new InvalidOperationException($"{id} is missing {entry}.");

    if (package.Entries.Any(entry => entry.FullName.StartsWith($"{resourceRoot}/dist/", StringComparison.Ordinal)))
        throw new InvalidOperationException($"{id} must use the upstream npm asset graph.");

    using var manifest = package.GetEntry($"{id}.nuspec")!.Open();
    var metadata = XDocument.Load(manifest).Root!.Elements().Single(element => element.Name.LocalName == "metadata");
    var ns = metadata.Name.Namespace;
    if (metadata.Element(ns + "repository")?.Attribute("url")?.Value != "https://github.com/devhxj/RazorVue")
        throw new InvalidOperationException($"{id} must identify the RazorVue source repository.");

    var dependencies = metadata.Descendants(ns + "dependency")
        .ToDictionary(dependency => dependency.Attribute("id")!.Value);
    string[] expected = id == "ECMAScript.ElementPlus"
        ? ["Jazor", "ECMAScript.Vue", "ECMAScript.VueRoute"]
        : ["Jazor", "ECMAScript.Vue"];
    foreach (var dependency in expected)
        if (dependencies.GetValueOrDefault(dependency)?.Attribute("version")?.Value != $"[{sdkVersion}]")
            throw new InvalidOperationException($"{id} must pin {dependency} to SDK {sdkVersion}.");

    if (dependencies["Jazor"].Attribute("exclude")?.Value != "Build,Analyzers")
        throw new InvalidOperationException($"{id} must leave SDK tooling selection to the consumer.");

    Console.WriteLine($"Verified {id} {version} against SDK {sdkVersion}.");
}

async Task PackAsync(string project)
{
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false };
    foreach (var argument in new[]
    {
        "pack", Path.Combine("src", project, project + ".csproj"), "-c", "Release", "-o", feed,
        "-p:MinVerVersionOverride=" + version, "-p:RazorVuePackageVersion=" + version,
        "-p:RestoreAdditionalProjectSources=" + feed
    })
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start pack for {project}.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"Could not pack {project}. Exit code {process.ExitCode}.");
}
