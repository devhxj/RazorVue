using System.Diagnostics;
using System.Xml.Linq;

namespace Jazor.RazorVue.Sg.Test;

[TestClass]
public sealed class CompilationTimingTargetTests
{
    [TestMethod]
    public async Task VueTarget_ImportsTimingInMsBuildEvaluationWithPackageReferences()
    {
        var repository = FindRepository();
        var root = RazorSgTestHost.CreateTestArtifactDirectory("timing-target");
        Directory.CreateDirectory(root);
        try
        {
            // Match the packed sibling layout. Import conditions run during evaluation,
            // where item-list expressions are forbidden even though ItemGroup permits them.
            File.Copy(Path.Combine(repository, "src/Jazor.Vue/buildTransitive/Jazor.Vue.targets"),
                Path.Combine(root, "Jazor.Vue.targets"));
            File.Copy(Path.Combine(repository, "src/Jazor/build/Jazor.CompilationTiming.targets"),
                Path.Combine(root, "Jazor.CompilationTiming.targets"));
            var project = Path.Combine(root, "probe.proj");
            new XDocument(new XElement("Project",
                new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Jazor.Vue"))),
                new XElement("Import", new XAttribute("Project", "Jazor.Vue.targets")),
                new XElement("Target", new XAttribute("Name", "Probe"),
                    new XElement("Error", new XAttribute("Condition", "'$(_JazorCompilationTimingTargetsImported)' != 'true'"),
                        new XAttribute("Text", "Timing target was not imported.")))))
                .Save(project);
            using var child = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = root
                }
            };
            foreach (var argument in new[] { "msbuild", project, "/t:Probe", "/nr:false", "/v:minimal" })
                child.StartInfo.ArgumentList.Add(argument);
            RazorSgTestHost.ConfigureProcessEnvironment(child.StartInfo);
            child.Start();
            var stdout = child.StandardOutput.ReadToEndAsync();
            var stderr = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync();
            Assert.AreEqual(0, child.ExitCode, await stdout + await stderr);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RazorVue.slnx")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Jazor repository root was not found.");
    }
}
