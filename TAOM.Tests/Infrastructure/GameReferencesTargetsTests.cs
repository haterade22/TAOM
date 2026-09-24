using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// GameReferences.targets owns where every project finds the Bannerlord assemblies: the install
/// (TaomGameRefs=Install, the default) or BUTR's reference assemblies (RefAsm, used by the hosted
/// CI job). These pin the two relationships that rot silently: the BUTR build must be the pinned
/// game version, and no project may spell the install path itself (it would bypass RefAsm and
/// break only on CI).
/// </summary>
[TestClass]
public class GameReferencesTargetsTests
{
    private static readonly string[] GameProjects =
    {
        Path.Combine("Main", "TAOM.csproj"),
        Path.Combine("Dependencies", "TAOM.Dependencies.csproj"),
        Path.Combine("TAOM.Tests", "TAOM.Tests.csproj"),
    };

    [TestMethod]
    public void BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild()
    {
        // Arrange
        var targetsPath = RepoPaths.RepoPath("GameReferences.targets");
        Assert.IsTrue(File.Exists(targetsPath), $"{targetsPath} is missing.");
        var pin = File.ReadAllText(RepoPaths.RepoPath(".claude", "pinned-game-version.txt")).Trim().TrimStart('v');

        // Act
        var versions = XDocument.Load(targetsPath).Descendants("BannerlordRefAsmVersion")
            .Select(e => e.Value.Trim()).ToList();

        // Assert
        Assert.AreEqual(1, versions.Count, "GameReferences.targets must set BannerlordRefAsmVersion exactly once.");
        StringAssert.StartsWith(versions[0], pin + ".",
            $"BannerlordRefAsmVersion {versions[0]} is not a build of the pinned game version v{pin}. " +
            "Bump both together (engine bump) and use the BUTR build published for that version.");
        var changeSet = versions[0].Split('-')[0].Split('.').Last();
        Assert.AreEqual(TaleWorlds.Library.ApplicationVersion.DefaultChangeSet.ToString(CultureInfo.InvariantCulture), changeSet,
            $"BannerlordRefAsmVersion {versions[0]} was built from another changeset of v{pin} than the installed game " +
            "(TaleWorlds.Library.ApplicationVersion.DefaultChangeSet). Use the BUTR build published for this Steam build.");
    }

    [TestMethod]
    public void GameProjects_EveryGameReference_ComesFromGameReferencesTargets()
    {
        // Arrange
        var problems = new List<string>();

        foreach (var project in GameProjects)
        {
            // Act
            var doc = XDocument.Load(RepoPaths.RepoPath(project));
            var imports = doc.Descendants("Import")
                .Count(e => (string?)e.Attribute("Project") == @"..\GameReferences.targets" && e.Attribute("Condition") == null);
            if (imports != 1)
                problems.Add($"{project}: expected one unconditional <Import Project=\"..\\GameReferences.targets\" />, found {imports}");

            foreach (var include in InstallPathReferences(doc))
                problems.Add($"{project}: <Reference Include=\"{include}\"> names the install path directly");
        }

        // Assert
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void InstallPathReferences_HintPathNamesTheInstall_IsFlagged()
    {
        // Arrange
        var project = XDocument.Parse(
            "<Project><ItemGroup>" +
            "<Reference Include=\"StoryMode\"><HintPath>$(GameFolder)\\Modules\\StoryMode\\bin\\Win64_Shipping_Client\\StoryMode.dll</HintPath></Reference>" +
            "<Reference Include=\"$(TaomGameBin)\\TaleWorlds.*.dll\"><HintPath>%(Identity)</HintPath></Reference>" +
            "</ItemGroup></Project>");

        // Act
        var flagged = InstallPathReferences(project).ToList();

        // Assert
        CollectionAssert.AreEqual(new[] { "StoryMode" }, flagged);
    }

    // The properties Directory.Build.props derives the install from. The whole element is searched,
    // because <HintPath>$(GameFolder)\...</HintPath> is the usual Bannerlord spelling.
    private static readonly string[] InstallPathProperties =
        { "$(GameFolder)", "$(GameBinariesFolder)", "$(BANNERLORD_GAME_DIR)", "$(BANNERLORD_OVERRIDE_DIR)" };

    private static IEnumerable<string> InstallPathReferences(XDocument project) =>
        project.Descendants("Reference")
            .Where(r => InstallPathProperties.Any(r.ToString().Contains))
            .Select(r => (string?)r.Attribute("Include") ?? "");
}
