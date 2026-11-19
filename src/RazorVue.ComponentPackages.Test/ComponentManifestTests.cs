using System.Text.Json;
using Jazor.Common;
using Jazor.Emit;

namespace RazorVue.ComponentPackages.Test;

[TestClass]
public sealed class ComponentManifestTests
{
    [TestMethod]
    public void Materialize_ExternalPackageProjectPreservesAuthoredLocksForMigration()
    {
        var tdesignManifest = FindLibraryManifest("ECMAScript.TDesign");
        var vueManifest = FindLibraryManifest("ECMAScript.Vue");
        var outputRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "external-package-lock", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        try
        {
            var libraries = new LibraryMaterializer().Materialize(
                [tdesignManifest, vueManifest],
                outputRoot,
                BuildMode.Production,
                requiredImports: ["tdesign-vue-next/es/button/index.mjs"]);

            // pnpm owns its own lock. Preserve the authored npm lock for explicit
            // migration instead of silently deleting another tool's input.
            File.WriteAllText(Path.Combine(outputRoot, "package-lock.json"), "{\"stale\":true}\n");
            LibraryPackageWriter.WritePackageProject(outputRoot, libraries);

            using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputRoot, "package.json")));
            Assert.AreEqual("1.20.7", package.RootElement.GetProperty("dependencies").GetProperty("tdesign-vue-next").GetString());
            Assert.AreEqual("npm", package.RootElement.GetProperty("jazor").GetProperty("packages").GetProperty("tdesign-vue-next").GetProperty("source").GetString());
            Assert.AreEqual("{\"stale\":true}\n", File.ReadAllText(Path.Combine(outputRoot, "package-lock.json")));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Materialize_TDesignButtonEntry_DoesNotMaterializeAlertOrGlobalStyles()
    {
        var tdesignManifest = FindLibraryManifest("ECMAScript.TDesign");
        var vueManifest = FindLibraryManifest("ECMAScript.Vue");
        var outputRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "tdesign", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new LibraryMaterializer().Materialize(
                [tdesignManifest, vueManifest],
                outputRoot,
                BuildMode.Production,
                requiredImports: ["tdesign-vue-next/es/button/index.mjs"]);

            Assert.IsTrue(result.ImportPaths.ContainsKey("tdesign-vue-next/es/button/index.mjs"));
            Assert.IsFalse(result.ImportPaths.ContainsKey("tdesign-vue-next/es/alert/index.mjs"));
            Assert.IsFalse(result.StylePaths.Any(path => path.Contains("alert", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(result.StylePaths.Any(path => path.Contains("root", StringComparison.OrdinalIgnoreCase)));
            Assert.IsEmpty(result.StylePaths);
            Assert.AreEqual("tdesign-vue-next/es/button/index.mjs", result.ImportPaths["tdesign-vue-next/es/button/index.mjs"]);
            Assert.IsFalse(result.MaterializedPaths.Any(path => path.Contains("tdesign-vue-next", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Materialize_ElementPlusButtonEntry_DoesNotMaterializeAlertOrRootAssets()
    {
        var elementPlusManifest = FindLibraryManifest("ECMAScript.ElementPlus");
        var vueManifest = FindLibraryManifest("ECMAScript.Vue");
        using var manifest = JsonDocument.Parse(File.ReadAllText(elementPlusManifest));

        var dayjsPackage = manifest.RootElement.GetProperty("packages").GetProperty("dayjs");
        Assert.AreEqual("npm", dayjsPackage.GetProperty("source").GetString());
        Assert.AreEqual("1.11.23", dayjsPackage.GetProperty("version").GetString());
        var dayjsImport = manifest.RootElement.GetProperty("imports").GetProperty("dayjs");
        Assert.AreEqual("dayjs", dayjsImport.GetProperty("path").GetString());

        var outputRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "element-plus", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new LibraryMaterializer().Materialize(
                [elementPlusManifest, vueManifest],
                outputRoot,
                BuildMode.Production,
                requiredImports: ["element-plus/es/components/button/index.mjs"]);

            Assert.IsTrue(result.ImportPaths.ContainsKey("element-plus/es/components/button/index.mjs"));
            Assert.IsFalse(result.ImportPaths.ContainsKey("element-plus/es/components/alert/index.mjs"));
            Assert.IsFalse(result.ImportPaths.ContainsKey("element-plus/es/index.mjs"));
            Assert.IsEmpty(result.StylePaths);
            Assert.AreEqual("element-plus/es/components/button/index.mjs", result.ImportPaths["element-plus/es/components/button/index.mjs"]);
            Assert.IsFalse(
                result.PackageReferences.ContainsKey("@popperjs/core"),
                "A non-Popper Element Plus entry must not pull the alias dependency into the root project.");
            Assert.IsFalse(result.MaterializedPaths.Any(path => path.Contains("element-plus", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Materialize_ElementPlusPopperEntries_DeclarePinnedAliasClosure()
    {
        var elementPlusManifest = FindLibraryManifest("ECMAScript.ElementPlus");
        var vueManifest = FindLibraryManifest("ECMAScript.Vue");
        using var manifest = JsonDocument.Parse(File.ReadAllText(elementPlusManifest));

        var popperPackage = manifest.RootElement.GetProperty("packages").GetProperty("@popperjs/core");
        Assert.AreEqual("npm", popperPackage.GetProperty("source").GetString());
        Assert.AreEqual("npm:@sxzz/popperjs-es@2.11.8", popperPackage.GetProperty("version").GetString());
        Assert.AreEqual(
            "sha512-wOwESXvvED3S8xBmcPWHs2dUuzrE4XiZeFu7e1hROIJkm02a49N120pmOXxY33sBb6hArItm5W5tcg1cBtV+HQ==",
            popperPackage.GetProperty("integrity").GetString());

        var popperEntries = new[]
        {
            "element-plus/es/components/select/index.mjs",
            "element-plus/es/components/tooltip/index.mjs",
            "element-plus/es/components/dropdown/index.mjs"
        };
        var declaredPopperEntries = manifest.RootElement.GetProperty("imports")
            .EnumerateObject()
            .Where(static entry => entry.Value.GetProperty("dependencies")
                .EnumerateArray()
                .Any(static dependency => dependency.GetString() == "@popperjs/core"))
            .Select(static entry => entry.Name)
            .ToArray();
        Assert.HasCount(23, declaredPopperEntries, "Every reachable Element Plus Popper entry must carry the alias closure.");
        foreach (var specifier in popperEntries)
        {
            CollectionAssert.Contains(declaredPopperEntries, specifier);
            var dependencies = manifest.RootElement.GetProperty("imports").GetProperty(specifier)
                .GetProperty("dependencies")
                .EnumerateArray()
                .Select(static value => value.GetString())
                .ToArray();
            CollectionAssert.Contains(dependencies, "@popperjs/core", $"'{specifier}' must carry the Popper alias closure.");
        }

        var outputRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "element-plus-popper", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new LibraryMaterializer().Materialize(
                [elementPlusManifest, vueManifest],
                outputRoot,
                BuildMode.Production,
                requiredImports: popperEntries);

            var popperReference = result.PackageReferences["@popperjs/core"];
            Assert.AreEqual("npm", popperReference.Source);
            Assert.AreEqual("npm:@sxzz/popperjs-es@2.11.8", popperReference.Version);

            LibraryPackageWriter.WritePackageProject(outputRoot, result);
            using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputRoot, "package.json")));
            Assert.AreEqual(
                "npm:@sxzz/popperjs-es@2.11.8",
                package.RootElement.GetProperty("dependencies").GetProperty("@popperjs/core").GetString());
        }
        finally
        {
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Materialize_LucideEntry_ResolvesPinnedVuePeer()
    {
        using var workspace = new LibraryWorkspace();
        var outputRoot = Path.Combine(workspace.Root, "out");

        var result = new LibraryMaterializer().Materialize(
            [
                FindLibraryManifest("ECMAScript.Lucide"),
                FindLibraryManifest("ECMAScript.Vue")
            ],
            outputRoot,
            BuildMode.Production,
            ["lucide-vue-next"]);

        CollectionAssert.AreEquivalent(
            new[] { "lucide-vue-next", "vue" },
            result.ImportPaths.Keys.ToArray());
        Assert.AreEqual("3.5.42", result.PackageReferences["vue"].Version);
        Assert.IsEmpty(result.MaterializedPaths);
    }

    [TestMethod]
    public void Materialize_ProductionVuetifyLabsEntry_ResolvesSelectedComponentClosureOnly()
    {
        using var workspace = new LibraryWorkspace();
        var outputRoot = Path.Combine(workspace.Root, "out");

        var result = new LibraryMaterializer().Materialize(
            [
                FindLibraryManifest("ECMAScript.Vuetify"),
                FindLibraryManifest("ECMAScript.Vue")
            ],
            outputRoot,
            BuildMode.Production,
            ["vuetify/components/VCalendar"]);

        CollectionAssert.AreEquivalent(
            new[] { "vue", "vuetify/components/VCalendar" },
            result.ImportPaths.Keys.ToArray());
        // VCalendar 等 9 个组件已从 labs 迁到 stable；声明值跟随上游可解析路径，
        // 因此键与目标同为 stable 入口（labs 路径在当前版本经 exports 无法解析）。
        Assert.AreEqual("vuetify/components/VCalendar", result.ImportPaths["vuetify/components/VCalendar"]);
        Assert.IsEmpty(result.MaterializedPaths);
        Assert.IsEmpty(result.StylePaths);
    }

    [TestMethod]
    public void Load_VueDataUiManifest_DeclaresUpstreamGlobalStylesheetEdge()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(FindLibraryManifest("ECMAScript.VueDataUi")));
        var root = manifest.RootElement;
        Assert.IsEmpty(root.GetProperty("styles").EnumerateArray().ToArray());

        var components = root.GetProperty("imports")
            .EnumerateObject()
            .Where(static entry => entry.Name.StartsWith("vue-data-ui/", StringComparison.Ordinal))
            .ToArray();
        Assert.HasCount(71, components);
        Assert.IsTrue(components.All(static component =>
            !component.Value.TryGetProperty("stylesheetImports", out _)));
    }

    [TestMethod]
    public void Materialize_ProductionVueDataUiDonut_CopiesSelectedChartClosureAndPdfImportOnly()
    {
        using var workspace = new LibraryWorkspace();
        var outputRoot = Path.Combine(workspace.Root, "out");

        var result = new LibraryMaterializer().Materialize(
            [
                FindLibraryManifest("ECMAScript.VueDataUi"),
                FindLibraryManifest("ECMAScript.Vue")
            ],
            outputRoot,
            BuildMode.Production,
            ["vue-data-ui/vue-ui-donut"]);

        CollectionAssert.AreEquivalent(
            new[] { "vue", "jspdf", "vue-data-ui/vue-ui-donut" },
            result.ImportPaths.Keys.ToArray());
        Assert.IsEmpty(result.StylePaths);
        Assert.IsEmpty(result.MaterializedPaths);
    }

    [TestMethod]
    public void Materialize_ProductionVueDataUiTable_PreservesPackageDependenciesForCompleteBrowserClosure()
    {
        using var workspace = new LibraryWorkspace();
        var outputRoot = Path.Combine(workspace.Root, "out");

        var materialization = new LibraryMaterializer().Materialize(
            [
                FindLibraryManifest("ECMAScript.VueDataUi"),
                FindLibraryManifest("ECMAScript.Vue")
            ],
            outputRoot,
            BuildMode.Production,
            ["vue-data-ui/vue-ui-table"]);

        CollectionAssert.AreEquivalent(
            new[] { "vue", "jspdf", "vue-data-ui/vue-ui-table" },
            materialization.ImportPaths.Keys.ToArray());
        Assert.IsEmpty(materialization.MaterializedPaths);
    }

    [TestMethod]
    public void Materialize_ProductionVueDataUiFlow_CopiesNewCatalogEntryAndPdfImportOnly()
    {
        using var workspace = new LibraryWorkspace();
        var outputRoot = Path.Combine(workspace.Root, "out");

        var result = new LibraryMaterializer().Materialize(
            [
                FindLibraryManifest("ECMAScript.VueDataUi"),
                FindLibraryManifest("ECMAScript.Vue")
            ],
            outputRoot,
            BuildMode.Production,
            ["vue-data-ui/vue-ui-flow"]);

        CollectionAssert.AreEquivalent(
            new[] { "vue", "jspdf", "vue-data-ui/vue-ui-flow" },
            result.ImportPaths.Keys.ToArray());

        Assert.AreEqual("vue-data-ui/vue-ui-flow", result.ImportPaths["vue-data-ui/vue-ui-flow"]);
        Assert.IsEmpty(result.MaterializedPaths);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Materialize_DeclaredElementPlusLocale_ReusesPinnedIdentityWithoutSelectingUnusedEntries(bool production)
    {
        using var workspace = new LibraryWorkspace();
        const string locale = "element-plus/es/locale/lang/zh-cn";
        var consumer = workspace.WriteConsumerManifest(locale);
        var binding = FindLibraryManifest("ECMAScript.ElementPlus");

        var result = new LibraryMaterializer().Materialize(
            [consumer, binding], workspace.Output, production ? BuildMode.Production : BuildMode.Development, [locale]);

        Assert.AreEqual(LibraryManifest.LoadMetadata(binding).Packages["element-plus"], result.PackageReferences[locale]);
        Assert.AreEqual(locale, result.ImportPaths[locale]);
        CollectionAssert.AreEqual(new[] { locale }, result.ImportPaths.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { consumer }, result.ManifestPaths.ToArray());
        LibraryPackageWriter.WritePackageProject(workspace.Output, result);
        using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Output, "package.json")));
        Assert.AreEqual("2.14.5", package.RootElement.GetProperty("dependencies").GetProperty("element-plus").GetString());
        Assert.AreEqual(
            result.PackageReferences[locale].Integrity,
            package.RootElement.GetProperty("jazor").GetProperty("packages").GetProperty("element-plus").GetProperty("integrity").GetString());
    }

    [TestMethod]
    public void Materialize_InteractionComponentClosures_ResolveTheirEntryGraphs()
    {
        // Selecting a component import also selects the files and peer dependencies it uses.
        var cases = new[]
        {
            (Manifest: "ECMAScript.VueDraggable", Id: "vue-draggable", Specifier: "vue-draggable-plus"),
            (Manifest: "ECMAScript.FilePond", Id: "file-pond", Specifier: "vue-filepond"),
            (Manifest: "ECMAScript.WangEditor", Id: "wang-editor", Specifier: "@wangeditor/editor-for-vue")
        };
        var vueManifestPath = FindLibraryManifest("ECMAScript.Vue");

        foreach (var (manifestName, libraryId, specifier) in cases)
        {
            var manifestPath = FindLibraryManifest(manifestName);
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var outputRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", libraryId, Guid.NewGuid().ToString("N"));
            try
            {
                var result = new LibraryMaterializer().Materialize(
                    [manifestPath, vueManifestPath],
                    outputRoot,
                    BuildMode.Production,
                    requiredImports: [specifier]);

                Assert.AreEqual(specifier, result.ImportPaths[specifier]);

                // 请求入口必须连同其 package 依赖链一起被选中（含闭包内的核心包）。
                var requested = manifest.RootElement.GetProperty("imports").GetProperty(specifier);
                foreach (var dependency in requested.GetProperty("dependencies").EnumerateArray()
                             .Select(static value => value.GetString()!)
                             .Where(static value => value != "vue"))
                    Assert.IsTrue(result.ImportPaths.ContainsKey(dependency), $"{libraryId}: dependency '{dependency}' was not selected.");

                if (requested.GetProperty("dependencies").EnumerateArray()
                    .Any(static value => value.GetString() == "vue"))
                    Assert.IsTrue(result.ImportPaths.ContainsKey("vue"), $"{libraryId}: the vue peer was not resolved.");

                // Styles are declared on the binding entry and emitted with the consuming module.
                Assert.AreEqual(0, result.StylePaths.Count, $"{libraryId}: external styles must not be copied from binding output.");
            }
            finally
            {
                if (Directory.Exists(outputRoot))
                    Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static string FindLibraryManifest(string projectName)
    {
        if (projectName == "ECMAScript.Vue")
            return Path.Combine(AppContext.BaseDirectory, "vue3.manifest.json");

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", projectName, "manifest.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate the {projectName} library manifest.");
    }

    private sealed class LibraryWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        public string Output => Path.Combine(Root, "out");

        public string WriteConsumerManifest(string import)
        {
            Directory.CreateDirectory(Root);
            var path = Path.Combine(Root, "consumer.manifest.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                libraryId = "consumer",
                version = "1.0.0",
                source = "npm",
                imports = new Dictionary<string, object> { [import] = new { type = "module", path = import } },
                requires = new { },
                styles = Array.Empty<object>(),
                files = Array.Empty<object>()
            }));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
