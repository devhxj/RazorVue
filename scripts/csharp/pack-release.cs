#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

var root = Directory.GetCurrentDirectory();
var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
var sdkVersion = props.Descendants("JazorSdkVersion").Single().Value;
var version = args.Length > 0 ? args[0] : props.Descendants("RazorVuePackageVersion").Single().Value;
var feed = Path.GetFullPath(args.Length > 1 ? args[1] : "artifacts/packages");
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
    ("ECMAScript.FilePond", "file-pond", "vue-filepond-LICENSE"),
    ("ECMAScript.Lucide", "lucide-vue-next", "LUCIDE-LICENSE"),
    ("ECMAScript.TDesign", "tdesign-vue-next", "LICENSE"),
    ("ECMAScript.VueDataUi", "vue-data-ui", "VUE-DATA-UI-LICENSE"),
    ("ECMAScript.VueDraggable", "vue-draggable", "vue-draggable-plus-LICENSE"),
    ("ECMAScript.Vuetify", "vuetify", "LICENSE.md"),
    ("ECMAScript.WangEditor", "wang-editor", "wangeditor-editor-LICENSE")
};

foreach (var (id, library, license) in componentPackages)
{
    await PackAsync(id);
    VerifyComponentPackage(id, library, license);
}

await PackAsync("Jazor.Vue");
VerifyRazorContractAssembly();
await PackAsync("Jazor.Admin");
await VerifyInteractionPackageConsumerAsync();

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

    if (library is "file-pond" or "vue-draggable" or "wang-editor"
        && package.GetEntry($"{resourceRoot}/inventory.json") is null)
        throw new InvalidOperationException($"{id} is missing its upstream inventory.");

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

void VerifyRazorContractAssembly()
{
    using var package = ZipFile.OpenRead(Path.Combine(feed, $"Jazor.Vue.{version}.nupkg"));
    var assembly = package.GetEntry("lib/net11.0/ECMAScript.VueContract.dll")
        ?? throw new InvalidOperationException("Jazor.Vue is missing ECMAScript.VueContract.dll.");
    using var image = new MemoryStream();
    using (var entry = assembly.Open())
        entry.CopyTo(image);
    image.Position = 0;
    using var pe = new PEReader(image);
    var metadata = pe.GetMetadataReader();
    var definition = metadata.GetAssemblyDefinition();
    if (metadata.GetString(definition.Name) != "ECMAScript.VueContract"
        || definition.Version != new Version(1, 0, 0, 0)
        || !definition.PublicKey.IsNil)
        throw new InvalidOperationException("The packaged VueContract assembly identity changed.");
}

Task PackAsync(string project)
    => RunDotnetAsync(
    [
        "pack", Path.Combine("src", project, project + ".csproj"), "-c", "Release", "-o", feed,
        "-p:MinVerVersionOverride=" + version, "-p:RazorVuePackageVersion=" + version,
        "-p:RestoreAdditionalProjectSources=" + feed
    ]);

async Task VerifyInteractionPackageConsumerAsync()
{
    var workspace = Directory.CreateTempSubdirectory("razorvue-bindings-");
    try
    {
        var project = Path.Combine(workspace.FullName, "InteractionBindings.csproj");
        var packageReferences = new[]
        {
            new XElement("PackageReference", new XAttribute("Include", "Jazor"), new XAttribute("Version", sdkVersion)),
            new XElement("PackageReference", new XAttribute("Include", "Jazor.Vue"), new XAttribute("Version", version),
                new XAttribute("PrivateAssets", "all")),
            new XElement("PackageReference", new XAttribute("Include", "ECMAScript.FilePond"), new XAttribute("Version", version)),
            new XElement("PackageReference", new XAttribute("Include", "ECMAScript.VueDraggable"), new XAttribute("Version", version)),
            new XElement("PackageReference", new XAttribute("Include", "ECMAScript.WangEditor"), new XAttribute("Version", version))
        };
        var output = Path.Combine(workspace.FullName, "jazor");
        new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk.Web"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net11.0"),
                new XElement("LangVersion", "preview"),
                new XElement("RazorLangVersion", "11.0"),
                new XElement("CETCompat", "false"),
                new XElement("JazorMode", "release"),
                new XElement("JazorDir", output)),
            new XElement("ItemGroup", packageReferences)).Save(project);
        await File.WriteAllTextAsync(Path.Combine(workspace.FullName, "Program.cs"),
            """System.Console.WriteLine(typeof(ECMAScript.VueContract.VueInjectAttribute).FullName);""");
        await File.WriteAllTextAsync(Path.Combine(workspace.FullName, "InteractionBindings.razor"), """
            @using ECMAScript
            @using static ECMAScript.Vue
            @using Microsoft.AspNetCore.Components
            @using Microsoft.AspNetCore.Components.Web
            @implements IVueComponent
            @attribute [ECMAScriptModule("./components/interaction-bindings")]

            <VueFilePond />
            <VueDraggableList TItem="string" ModelValue="@Items">
                <div>@context</div>
            </VueDraggableList>
            <WangEditorComponent ModelValue="initial" />
            <VueRouterLink To="@Home">Home</VueRouterLink>
            <VueRouterView />
            <input @onchange="ReadNativeEvent" />
            <span>@EventType</span>

            @code {
                private IVueRef<string[]> Items { get; } = Vue.Ref(new[] { "first" });
                private RouteLocationRaw Home => "/";
                private string EventType { get; set; } = "";
                private void ReadNativeEvent(ChangeEventArgs args) => EventType = args.NativeEvent.Type;
            }
            """);

        await RunDotnetAsync(
        [
            "build", project, "-c", "Release", "--configfile", Path.Combine(root, "NuGet.Config"),
            "-p:RestoreAdditionalProjectSources=" + feed,
            "-p:RestorePackagesPath=" + Path.Combine(workspace.FullName, "packages"),
            "-p:JazorPnpmStoreDirectory=" + Path.Combine(root, ".tmp", "nodeapi-store")
        ]);

        var module = Path.Combine(output, "components", "interaction-bindings.js");
        var contents = await File.ReadAllTextAsync(module);
        foreach (var specifier in new[] { "vue-filepond", "vue-draggable-plus", "@wangeditor/editor-for-vue", "vue-router" })
            if (!contents.Contains($"""from "{specifier}";""", StringComparison.Ordinal))
                throw new InvalidOperationException($"The packaged Razor consumer did not import {specifier}.");
        if (!File.Exists(Path.Combine(output, "dist", "bundle.js")))
            throw new InvalidOperationException("The packaged Razor consumer did not produce a Vite bundle.");

        Console.WriteLine("Verified packaged Razor component bindings, Router proxies, native events, and the Vite bundle.");
    }
    finally
    {
        workspace.Delete(recursive: true);
    }
}

async Task RunDotnetAsync(string[] arguments)
{
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false };
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start dotnet {arguments[0]}.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"dotnet {arguments[0]} {arguments[1]} exited with code {process.ExitCode}.");
}
