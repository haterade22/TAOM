using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.BattleActionBar;

namespace TAOM.Tests.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// The #970 [Check] ids that tools/ingame_checks/970.toml reads, and the attach check's verdict at mission end (#1072):
/// it fails only when Battle mode was reached with the setting on, since the attach runs only on a Battle-mode tick.
/// </summary>
[TestClass]
public class BattleActionBarChecksTests
{
    [TestMethod]
    public void Ids_AreTheManifestIds()
    {
        Assert.AreEqual("taom.battle-action-bar.attach", BattleActionBarChecks.AttachId);
        Assert.AreEqual("taom.battle-action-bar.stance", BattleActionBarChecks.StanceId);
        Assert.IsTrue(OutcomeLog.IsValidId(BattleActionBarChecks.AttachId));
        Assert.IsTrue(OutcomeLog.IsValidId(BattleActionBarChecks.StanceId));
    }

    [DataTestMethod]
    // Not a field battle: the bar never attaches there.
    [DataRow(false, false, true, true, true, BattleActionBarChecks.AttachEnd.None)]
    // Attached: its own line was written.
    [DataRow(true, true, true, true, true, BattleActionBarChecks.AttachEnd.None)]
    // A Battle tick saw the setting on, then it was turned off.
    [DataRow(true, false, true, false, false, BattleActionBarChecks.AttachEnd.FailNeverAttached)]
    // Ended in Battle with the setting on, and no tick saw it: the view never ticked.
    [DataRow(true, false, false, true, true, BattleActionBarChecks.AttachEnd.FailNeverAttached)]
    // Fought with the setting off.
    [DataRow(true, false, false, true, false, BattleActionBarChecks.AttachEnd.SkipSettingOff)]
    // Ended in deployment: proves nothing, setting on or off.
    [DataRow(true, false, false, false, true, BattleActionBarChecks.AttachEnd.None)]
    [DataRow(true, false, false, false, false, BattleActionBarChecks.AttachEnd.None)]
    // expected is object: a public test method cannot take the internal enum as a parameter.
    public void ClassifyAtEnd_TruthTable(bool fieldBattle, bool attachedOnce, bool enabledBattleTickSeen,
        bool endedInBattle, bool enabledAtEnd, object expected) =>
        Assert.AreEqual(expected, BattleActionBarChecks.ClassifyAtEnd(fieldBattle, attachedOnce, enabledBattleTickSeen,
            endedInBattle, enabledAtEnd));
}
