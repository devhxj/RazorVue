#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.Xml.Linq;

var root = Directory.GetCurrentDirectory();
var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
var sdkVersion = props.Descendants("JazorSdkVersion").Single().Value;
var version = args.Length > 0 ? args[0] : props.Descendants("RazorVuePackageVersion").Single().Value;
var feed = Path.GetFullPath("artifacts/packages");
Directory.CreateDirectory(feed);

// Both browser gates restore detached projects from this feed. Only RazorVue owns
// Jazor.Vue and Jazor.Admin; the SDK packages retain their upstream identity.
string[] sdkPackages =
[
    "Jazor", "ECMAScript.Vue", "ECMAScript.Style", "ECMAScript.VueRoute", "ECMAScript.VueDataUi",
    "ECMAScript.Lucide", "ECMAScript.TDesign", "ECMAScript.ElementPlus", "ECMAScript.Vuetify"
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

await PackAsync("Jazor.Vue");
await PackAsync("Jazor.Admin");

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
