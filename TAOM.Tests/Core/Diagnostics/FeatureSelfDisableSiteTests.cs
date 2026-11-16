using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.HeroRace;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Core.Diagnostics;

/// <summary>
/// Issue #1223: three feature latches outside a patch class turned a feature off with a log line only. Each now
/// reports to the #1185 ledger once, on its failure branch, so the player sees one chat line naming it.
/// </summary>
[TestClass]
public class FeatureSelfDisableSiteTests
{
    private SelfDisableLedger _savedLedger = null!;

    [TestInitialize]
    public void Setup()
    {
        _savedLedger = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
    }

    [TestCleanup]
    public void Cleanup() => SelfDisables.Ledger = _savedLedger;

    private static string[] TakenIds() => SelfDisables.Ledger.Take().Select(e => e.Id).ToArray();

    // The first call runs CustomAttacksUtils' static constructor, which binds the engine and prints through Debug.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CustomAttacks_AnUnboundBlowCall_ReportsOnce()
    {
        Assert.IsTrue(CustomAttacksUtils.Unbound(bound: false, "Mission.RegisterBlow method not found"));
        Assert.IsTrue(CustomAttacksUtils.Unbound(bound: false, "again"));

        CollectionAssert.AreEqual(new[] { CustomAttacksUtils.SelfDisableId }, TakenIds());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CustomAttacks_ABoundBlowCall_ReportsNothing()
    {
        Assert.IsFalse(CustomAttacksUtils.Unbound(bound: true, null));

        Assert.IsFalse(SelfDisables.Ledger.HasPending);
    }

    [TestMethod]
    public void ActionIndexRepair_TheLastFailedPass_Reports()
    {
        ActionIndexCacheRepair.ReportIfSpent(ok: false, attempts: 2, "menu");
        Assert.IsFalse(SelfDisables.Ledger.HasPending, "a pass that can still be retried");

        ActionIndexCacheRepair.ReportIfSpent(ok: true, attempts: 3, "menu");
        Assert.IsFalse(SelfDisables.Ledger.HasPending, "a pass that worked");

        ActionIndexCacheRepair.ReportIfSpent(ok: false, attempts: 3, "menu");
        CollectionAssert.AreEqual(new[] { ActionIndexCacheRepair.SelfDisableId }, TakenIds());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void PlayerSwitcher_AFailedHandlerRegistration_Reports()
    {
        CollectionAssert.AreEqual(new[] { PlayerSwitchRegistrationBehavior.SelfDisableId }, FailRegistration(enabled: true));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void PlayerSwitcher_AFailedRegistrationWhileOff_ReportsNothing()
    {
        CollectionAssert.AreEqual(new string[0], FailRegistration(enabled: false));
    }

    private static string[] FailRegistration(bool enabled)
    {
        var policy = Substitute.For<IPlayerSwitchPolicyProvider>();
        policy.Current.Returns(enabled ? PlayerSwitchPolicy.Default : PlayerSwitchPolicy.Disabled);
        var session = new PlayerSwitchSessionStore();
        var sut = new PlayerSwitchRegistrationBehavior(
            Substitute.For<IHeroSwitchService>(), Substitute.For<ISwitchPlanner>(), session, session,
            policy, Substitute.For<TAOM.Features.CareerSystem.ICareerDataService>(),
            Substitute.For<IPlayerPartyAdapter>(),
            Substitute.For<IInquiryAdapter>(), Substitute.For<IPlayerIdentityAdapter>(), Substitute.For<IModLogger>());

        // A null manager makes the registration throw.
        sut.OnCharacterCreationInitialized(null!);
        return TakenIds();
    }
}
