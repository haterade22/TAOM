using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementGuards;

namespace TAOM.Tests.Features.SettlementGuards;

/// <summary>
/// The decision behind the hideout guard-dialog suppression (regression of #258): vanilla's guard and
/// disguise start lines must not answer inside a hideout mission, where a Soldier-occupation boss
/// fallback would otherwise be turned into "Can't talk right now" and the boss fight never starts.
/// </summary>
[TestClass]
public class HideoutGuardDialogRuleTests
{
    [TestMethod]
    public void ShouldSuppress_HideoutAssaultMission_ReturnsTrue()
        => Assert.IsTrue(HideoutGuardDialogRule.ShouldSuppress(hasHideoutController: true, hasHideoutAmbushController: false));

    [TestMethod]
    public void ShouldSuppress_HideoutAmbushMission_ReturnsTrue()
        => Assert.IsTrue(HideoutGuardDialogRule.ShouldSuppress(hasHideoutController: false, hasHideoutAmbushController: true));

    [TestMethod]
    public void ShouldSuppress_NoMissionOrOtherMission_ReturnsFalse()
        => Assert.IsFalse(HideoutGuardDialogRule.ShouldSuppress(hasHideoutController: false, hasHideoutAmbushController: false));

    [TestMethod]
    public void ApplyToResult_GuardLineWouldShowInHideout_ForcesFalse()
        => Assert.IsFalse(HideoutGuardDialogRule.ApplyToResult(vanillaResult: true, hasHideoutController: true, hasHideoutAmbushController: false));

    [TestMethod]
    public void ApplyToResult_GuardLineWouldShowOutsideHideout_LeavesTrue()
        => Assert.IsTrue(HideoutGuardDialogRule.ApplyToResult(vanillaResult: true, hasHideoutController: false, hasHideoutAmbushController: false));

    [TestMethod]
    public void ApplyToResult_VanillaSaidFalse_StaysFalse()
    {
        Assert.IsFalse(HideoutGuardDialogRule.ApplyToResult(false, true, true));
        Assert.IsFalse(HideoutGuardDialogRule.ApplyToResult(false, false, false));
    }
}
