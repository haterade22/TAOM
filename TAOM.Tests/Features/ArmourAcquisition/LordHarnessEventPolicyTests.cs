using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmourAcquisition;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// "A Lord's Harness Unclaimed", the event route to lord kit: after a battle the player's side won against
/// at least one enemy lord, a rare roll finds his household's harness. The roll is a positive requirement,
/// so a NaN chance or roll never fires it (csharp-architecture.md, "Engine-Float Decision Gates").
/// </summary>
[TestClass]
public class LordHarnessEventPolicyTests
{
    [TestMethod]
    public void ShouldTrigger_WonAgainstALord_OffCooldown_RollUnderTheChance_IsTrue()
    {
        Assert.IsTrue(LordHarnessEventPolicy.ShouldTrigger(true, 1, today: 100, lastEventDay: null, cooldownDays: 90, chance: 0.1f, roll: 0.05));
    }

    [TestMethod]
    public void ShouldTrigger_BattleLost_IsFalse()
    {
        Assert.IsFalse(LordHarnessEventPolicy.ShouldTrigger(false, 1, 100, null, 90, 1f, 0.0));
    }

    [TestMethod]
    public void ShouldTrigger_NoEnemyLord_IsFalse()
    {
        Assert.IsFalse(LordHarnessEventPolicy.ShouldTrigger(true, 0, 100, null, 90, 1f, 0.0));
    }

    [TestMethod]
    public void ShouldTrigger_OnCooldown_IsFalseUntilItEnds()
    {
        Assert.IsFalse(LordHarnessEventPolicy.ShouldTrigger(true, 1, 189, 100, 90, 1f, 0.0));
        Assert.IsTrue(LordHarnessEventPolicy.ShouldTrigger(true, 1, 190, 100, 90, 1f, 0.0));
    }

    [TestMethod]
    public void ShouldTrigger_RollAtTheChance_IsFalse()
    {
        Assert.IsFalse(LordHarnessEventPolicy.ShouldTrigger(true, 1, 100, null, 90, 0.1f, 0.1));
    }

    [TestMethod]
    public void ShouldTrigger_NaNChanceOrRoll_IsFalse()
    {
        Assert.IsFalse(LordHarnessEventPolicy.ShouldTrigger(true, 1, 100, null, 90, float.NaN, 0.0));
        Assert.IsFalse(LordHarnessEventPolicy.ShouldTrigger(true, 1, 100, null, 90, 1f, double.NaN));
    }
}
