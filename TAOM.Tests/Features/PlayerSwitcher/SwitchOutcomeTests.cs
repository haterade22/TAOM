using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// The one definition of "the player became that hero", read by the handover's gold record, the
/// treasury restore and StartupResources' skip, which must agree exactly.
/// </summary>
[TestClass]
public class SwitchOutcomeTests
{
    [DataTestMethod]
    [DataRow(SwitchOutcome.NotAttempted, false)]
    [DataRow(SwitchOutcome.Blocked, false)]
    [DataRow(SwitchOutcome.Switched, true)]
    [DataRow(SwitchOutcome.Failed, false)]
    [DataRow(SwitchOutcome.SwitchedWithErrors, true)]
    public void TookEffect_IsTrueExactlyWhenThePlayerIsNowTheHero(SwitchOutcome outcome, bool expected)
    {
        Assert.AreEqual(expected, outcome.TookEffect());
    }
}
