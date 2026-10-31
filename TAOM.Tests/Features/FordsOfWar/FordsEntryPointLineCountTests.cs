using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>ADR-002: entry points stay under 150 lines. The Fords of War hooks, the two mission views and the module.</summary>
[TestClass]
public class FordsEntryPointLineCountTests
{
    [TestMethod]
    public void EveryHooksFile_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FordsOfWar", "Hooks"), "*.cs");
        Assert.IsTrue(files.Length >= 3, "FordsOfWarMissionLogic, FordsTowerHitPatch and FordsMissionLauncher");
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, "ADR-002: " + Path.GetFileName(file) + " is 150 lines or more");
    }

    [DataTestMethod]
    [DataRow("Main/Features/FordsOfWar/Hooks/FordsOfWarMissionLogic.cs")]
    [DataRow("Main/Features/FordsOfWar/Hooks/FordsTowerHitPatch.cs")]
    [DataRow("Main/Features/FordsOfWar/FordsOfWarModule.cs")]
    [DataRow("Client/Features/FordsOfWar/FordsCommandView.cs")]
    [DataRow("Client/Features/FordsOfWar/FordsCommanderCameraView.cs")]
    public void TheEntryPoint_StaysUnder150Lines(string relativePath)
    {
        var lines = File.ReadAllLines(RepoPaths.RepoPath(relativePath.Split('/'))).Length;

        Assert.IsTrue(lines < 150, "ADR-002: " + relativePath + " is 150 lines or more (" + lines + ")");
    }
}
