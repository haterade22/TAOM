using System.Collections.Generic;
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
                .Count(e => (string?)e.Attribute("Project") == @"..\GameReferences.targets");
            if (imports != 1)
                problems.Add($"{project}: expected one <Import Project=\"..\\GameReferences.targets\" />, found {imports}");

            foreach (var reference in doc.Descendants("Reference"))
            {
                var spelled = ((string?)reference.Attribute("Include") ?? "") + ((string?)reference.Attribute("Exclude") ?? "");
                if (spelled.Contains("$(GameFolder)"))
                    problems.Add($"{project}: <Reference Include=\"{(string?)reference.Attribute("Include")}\"> names $(GameFolder) directly");
            }
        }

        // Assert
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }
}
