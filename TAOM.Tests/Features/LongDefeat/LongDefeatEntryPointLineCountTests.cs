using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>ADR-002: entry points stay under 150 lines. The Long Defeat hooks, counting every file of a partial class.</summary>
[TestClass]
public class LongDefeatEntryPointLineCountTests
{
    [TestMethod]
    public void EveryHooksClass_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "LongDefeat", "Hooks"), "*.cs");
        Assert.IsTrue(files.Length >= 3, "LongDefeatMorale, LongDefeatMissionLauncher and LongDefeatMissionLogic");
        foreach (var group in files.GroupBy(f => Path.GetFileName(f).Split('.')[0]))
        {
            var lines = group.Sum(f => File.ReadAllLines(f).Length);
            Assert.IsTrue(lines < 150, "ADR-002: " + group.Key + " is " + lines + " lines over " + group.Count() + " file(s)");
        }
    }

    [DataTestMethod]
    [DataRow("Main/Features/LongDefeat/Hooks/LongDefeatMissionLogic.cs")]
    [DataRow("Main/Features/LongDefeat/LongDefeatModule.cs")]
    [DataRow("Client/Features/LongDefeat/LongDefeatHudView.cs")]
    public void TheEntryPoint_StaysUnder150Lines(string relativePath)
    {
        var lines = File.ReadAllLines(RepoPaths.RepoPath(relativePath.Split('/'))).Length;

        Assert.IsTrue(lines < 150, "ADR-002: " + relativePath + " is 150 lines or more (" + lines + ")");
    }
}
