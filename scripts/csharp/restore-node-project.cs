#!/usr/bin/env dotnet run
#:property AssemblyName=Jazor.NodeApiVerification
#:property CETCompat=false
#:property PublishAot=false
#:package Jazor@2.1.0-preview.5

using Jazor.NodeHost;

// Repository tool fixtures use the same pnpm API and libnode as production
// Emit. CI must not replace this entrypoint with npm/pnpm CLI installation.
if (args.Length == 0)
    throw new ArgumentException("Usage: dotnet run --file scripts/csharp/restore-node-project.cs -- <project-directory> [...]");

var repository = new DirectoryInfo(Directory.GetCurrentDirectory());
while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "RazorVue.slnx")))
    repository = repository.Parent;
var repoRoot = repository?.FullName ?? throw new InvalidOperationException("Cannot locate RazorVue.slnx.");
var store = Environment.GetEnvironmentVariable("JazorPnpmStoreDirectory") ?? Path.Combine(repoRoot, ".tmp", "nodeapi-store");

foreach (var project in args)
    await NodeToolchain.RestoreAsync(Path.GetFullPath(project), store, CancellationToken.None, Console.Out);
