using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// #969: the DeployTAOMDependenciesStubs target in Dependencies/TAOM.Dependencies.csproj must put each
/// alias stub at Modules\&lt;ID&gt;\SubModule.xml. The launcher reads only that depth (v1.5.7
/// ModuleHelper.GetPhysicalModules), and the old target kept the _Module segment that
/// %(RecursiveDir) carries, so no stub was ever visible. These tests read the target and apply its
/// destination rewrite to every repo stub the way MSBuild fills %(RecursiveDir) for the Include.
/// The release side (tools/package_release.py) is pinned by tools/tests/test_package_release.py.
/// </summary>
[TestClass]
public class AliasStubDeployTests
{
    private const string GameFolder = @"C:\Game";

    private static XElement DeployTarget()
    {
        var csproj = XDocument.Load(RepoPaths.RepoPath("Dependencies", "TAOM.Dependencies.csproj"));
        var target = csproj.Descendants("Target")
            .SingleOrDefault(t => (string?)t.Attribute("Name") == "DeployTAOMDependenciesStubs");
        Assert.IsNotNull(target, "Dependencies/TAOM.Dependencies.csproj has no DeployTAOMDependenciesStubs target.");
        return target!;
    }

    private static string ItemInclude(XElement target, string item)
    {
        var element = target.Descendants(item).SingleOrDefault();
        Assert.IsNotNull(element, $"DeployTAOMDependenciesStubs defines no {item} item.");
        return (string)element!.Attribute("Include")!;
    }

    [TestMethod]
    public void Include_TakesOnlyTheOneManifestPerStubFolder()
    {
        // Act
        var include = ItemInclude(DeployTarget(), "_TaomStubFiles");

        // Assert: exactly Stubs\<ID>\_Module\SubModule.xml, so stray backups and deeper files never deploy.
        Assert.AreEqual(@"$(MSBuildThisFileDirectory)..\Stubs\*\_Module\SubModule.xml", include);
    }

    [TestMethod]
    public void Destination_EveryRepoStub_LandsWhereTheLauncherReadsIt()
    {
        // Arrange
        var target = DeployTarget();
        var transform = ItemInclude(target, "_TaomStubDestFiles");
        var copy = target.Descendants("Copy").Single();
        Assert.AreEqual("@(_TaomStubFiles)", (string?)copy.Attribute("SourceFiles"));
        Assert.AreEqual("@(_TaomStubDestFiles)", (string?)copy.Attribute("DestinationFiles"));

        StringAssert.StartsWith(transform, @"@(_TaomStubFiles->'$(GameFolder)\Modules\%(RecursiveDir)%(Filename)%(Extension)'");
        var replaces = Regex.Matches(transform, @"->Replace\('([^']*)', '([^']*)'\)")
            .Cast<Match>().Select(m => (From: m.Groups[1].Value, To: m.Groups[2].Value)).ToList();
        Assert.AreNotEqual(0, replaces.Count, "The destination transform drops no _Module segment.");

        var stubRoot = RepoPaths.RepoPath("Stubs");
        var manifests = Directory.GetFiles(stubRoot, "SubModule.xml", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(Path.GetDirectoryName(p)) == "_Module").ToList();
        Assert.AreEqual(4, manifests.Count, "Expected the four BUTR alias stubs under Stubs/.");

        foreach (var manifest in manifests)
        {
            // %(RecursiveDir) is the part of the path the wildcard matched: <ID>\_Module\.
            var id = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(manifest)))!;
            var destination = $@"{GameFolder}\Modules\{id}\_Module\SubModule.xml";

            // Act
            foreach (var (from, to) in replaces)
                destination = destination.Replace(from, to);

            // Assert
            Assert.AreEqual($@"{GameFolder}\Modules\{id}\SubModule.xml", destination, id);
        }
    }
}
