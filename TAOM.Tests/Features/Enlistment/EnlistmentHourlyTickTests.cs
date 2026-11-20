using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #832: the behavior only delegates the tick (the order lives in the reconciler, see
/// EnlistmentReconcilerTests, where ReconcileHourly_NeverReleasesForACrownWar pins that the load
/// normalizer's and the captivity path's ReconcileHourly never reach the tick-only steps).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EnlistmentHourlyTickTests
{
    private IEnlistmentReconciler _reconciler = null!;
    private IEnlistmentLoadNormalizer _normalizer = null!;
    private IPlayerPartyAdapter _playerParty = null!;
    private ICoopSessionProvider _coop = null!;
    private EnlistmentBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _reconciler = Substitute.For<IEnlistmentReconciler>();
        _normalizer = Substitute.For<IEnlistmentLoadNormalizer>();
        _playerParty = Substitute.For<IPlayerPartyAdapter>();
        _playerParty.GetMainHeroId().Returns("heir_hero");
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _sut = new EnlistmentBehavior(
            Substitute.For<IEnlistmentStore>(), Substitute.For<IEnlistmentStateMachine>(), _reconciler,
            _normalizer, _playerParty, _coop,
            Substitute.For<IServiceMaintenanceService>(), Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void OnHourlyTick_WithoutAuthority_TouchesNothing()
    {
        _coop.IsAuthority.Returns(false);

        _sut.OnHourlyTick();

        _reconciler.DidNotReceiveWithAnyArgs().OnHourlyTick(default);
        _reconciler.DidNotReceiveWithAnyArgs().ReconcileHourly(default);
        _normalizer.DidNotReceiveWithAnyArgs().DischargeHeirMismatchOutsideBattle(default!);
    }

    [TestMethod]
    public void OnHourlyTick_HeirMismatchDischargedAfterItsBattle_SkipsTheReconcile()
    {
        // #1030: the load held the heir-mismatch discharge while the soldier was in his battle.
        _normalizer.DischargeHeirMismatchOutsideBattle("heir_hero").Returns(true);

        _sut.OnHourlyTick();

        _reconciler.DidNotReceiveWithAnyArgs().OnHourlyTick(default);
    }
}
