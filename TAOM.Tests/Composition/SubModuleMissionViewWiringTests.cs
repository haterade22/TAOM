using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// Issue #970. A MissionView added in OnMissionBehaviorInitialize is never registered with the MissionScreen
/// (MissionScreen.cs:4054-4065), so it never ticks. TAOM's two views are [DefaultView] types in TAOM.Client and
/// SubModule adds neither.
/// </summary>
[TestClass]
public class SubModuleMissionViewWiringTests
{
    [TestMethod]
    public void AddTaomMissionBehaviors_AddsNoMissionView()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        Assert.IsFalse(src.Contains("IAutonomousMovementControllerFactory"), "the controller is a [DefaultView]; no factory seam");
        Assert.IsFalse(src.Contains("IBattleActionBarViewFactory"), "the action bar is a [DefaultView]; no factory seam");

        var offenders = RepoPaths.CSharpFiles("Main")
            .Select(f => new { f, text = RepoPaths.StripComments(RepoPaths.Text(f)) })
            .Where(x => x.text.Contains("new AutonomousMovementPlayerController(")
                        || x.text.Contains("new BattleActionBarMissionView("))
            .Select(x => Path.GetFileName(x.f)).ToList();
        CollectionAssert.AreEqual(new string[0], offenders, "no file under Main may construct a client MissionView");
    }
}
