using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// Issue #970. A MissionView added in OnMissionBehaviorInitialize is never registered with the MissionScreen (v1.4.8
/// MissionScreen.cs:3991-4002 runs before Mission.cs:3815), so it never ticks. TAOM's two views are [DefaultView]
/// types, and no file under Main constructs either.
/// </summary>
[TestClass]
public class SubModuleMissionViewWiringTests
{
    private static readonly Regex Construct = new Regex(
        @"\bnew\s+(?:[\w.]+\.)?(?:AutonomousMovementPlayerController|BattleActionBarMissionView)\s*\(");

    [TestMethod]
    public void NoFileUnderMain_ConstructsATaomMissionView()
    {
        var offenders = RepoPaths.CSharpFiles("Main")
            .Where(f => Construct.IsMatch(RepoPaths.StripComments(File.ReadAllText(f))))
            .Select(Path.GetFileName)
            .ToList();
        CollectionAssert.AreEqual(new string[0], offenders, "no file under Main may construct a TAOM MissionView");
    }
}
