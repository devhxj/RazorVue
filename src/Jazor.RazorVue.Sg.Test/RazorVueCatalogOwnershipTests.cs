namespace Jazor.RazorVue.Sg.Test;

[TestClass]
public sealed class RazorVueCatalogOwnershipTests
{
    [TestMethod]
    public void BlazorClrMappings_AreOwnedByGeneratedClrModules()
    {
        var repositoryRoot = FindRepositoryRoot();
        var catalogDirectory = Path.Combine(repositoryRoot, "src", "Jazor.RazorVue", "RazorSdk", "Catalog");

        var catalogSources = Directory.Exists(catalogDirectory)
            ? Directory.EnumerateFiles(catalogDirectory, "*.cs", SearchOption.AllDirectories).ToArray()
            : [];
        Assert.HasCount(0, catalogSources, "RazorVue must not retain a CLR whitelist catalog.");

    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RazorVue.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
