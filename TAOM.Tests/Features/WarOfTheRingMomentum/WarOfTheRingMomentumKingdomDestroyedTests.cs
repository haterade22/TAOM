using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Features.WarOfTheRingMomentum.UI;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// #1113: a kingdom's destruction decides the war from the enrolled lists, and a pledge changes a kingdom's
/// side between two daily sweeps. These tests run the real enrollment and victory services over one state,
/// as the handler does, with a realm that took a side after the last sweep. Only the engine boundary is a fake.
/// </summary>
[TestClass]
public class WarOfTheRingMomentumKingdomDestroyedTests
{
    private IMomentumSettingsProvider _settings = null!;
    private IWarOfTheRingService _wotr = null!;
    private IAllianceAdapter _alliance = null!;
    private IAlignmentService _alignment = null!;
    private MomentumWarState _state = null!;
    private MomentumEnrollmentService _enrollment = null!;
    private MomentumVictoryService _victory = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMomentumSettingsProvider>();
        _settings.MomentumEnabled.Returns(true);
        _settings.VictoryEnabled.Returns(true);
        _settings.VictoryThreshold.Returns(500);
        _settings.RequireParticipationForVictory.Returns(false);
        _wotr = Substitute.For<IWarOfTheRingService>();
        _wotr.CurrentPhase.Returns(WarPhase.FullWar);
        _alliance = Substitute.For<IAllianceAdapter>();
        _alliance.AreAtWar(default!, default!).ReturnsForAnyArgs(true);
        // An unmocked GetKingdomCultureId answers an empty culture, so these kingdoms resolve on their id alone.
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide("empire_w", "").Returns(FactionSide.Free);
        _alignment.ResolveSide("vlandia", "").Returns(FactionSide.Free);
        _alignment.ResolveSide("empire_s", "").Returns(FactionSide.Evil);
        _alignment.ResolveSide("cardolan", "").Returns(FactionSide.Neutral);

        _state = new MomentumWarState();
        _enrollment = new MomentumEnrollmentService(_wotr, _alliance, _alignment, Substitute.For<IModLogger>());
        _victory = new MomentumVictoryService(
            _settings, Substitute.For<IPlayerMomentumService>(), _wotr, _alliance, Substitute.For<IModLogger>());
        Live("empire_w", "vlandia", "empire_s", "cardolan");
        _enrollment.SweepEnrollment(_state);
    }

    // The engine marks a kingdom eliminated before it raises KingdomDestroyed, so a fallen kingdom is not listed.
    private void Live(params string[] ids) => _alliance.GetAllKingdomIds().Returns(new List<string>(ids));

    // Cardolan pledges to the Free Peoples after the day's sweep: its side changes, the enrolled lists do not.
    private void CardolanPledgesFree() => _alignment.ResolveSide("cardolan", "").Returns(FactionSide.Free);

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void OnKingdomDestroyed_TheLastEnrolledKingdomOfASideFalls_ARealmPledgedSinceTheLastSweepKeepsTheWarOpen()
    {
        Live("empire_w", "empire_s", "cardolan");
        _enrollment.RemoveKingdom(_state, "vlandia");
        CardolanPledgesFree();
        Live("empire_s", "cardolan");
        var store = Substitute.For<IMomentumStateStore>();
        store.State.Returns(_state);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var sut = new WarOfTheRingMomentumBehavior(
            store, Substitute.For<IMomentumEventService>(), _enrollment, _victory,
            Substitute.For<IWarEventSnapshotAdapter>(), _settings, _wotr, Substitute.For<IMomentumUIService>(),
            Substitute.For<IModLogger>(), coop);
        var handler = typeof(WarOfTheRingMomentumBehavior).GetMethod(
            "OnKingdomDestroyed", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(handler, "OnKingdomDestroyed moved");
        var gondor = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
        gondor.StringId = "empire_w";

        handler!.Invoke(sut, new object[] { gondor });

        Assert.IsFalse(_state.Free.ContainsKingdom("empire_w"));
        Assert.IsTrue(_state.Free.ContainsKingdom("cardolan"), "the realm that pledged today is enrolled before the count");
        Assert.IsFalse(_state.HasWarEnded, "a Free realm lives, so the Shadow has not won by elimination");
        _wotr.DidNotReceive().EndWar(Arg.Any<WarOutcome>());
    }

    // The same two calls the handler makes, in its order. The victory is by the threshold, so the war ends,
    // and the realm that pledged today must be in the victory's peace.
    [TestMethod]
    public void RemoveKingdomThenVictory_TheThresholdIsReached_TheVictorysPeaceIncludesARealmPledgedSinceTheLastSweep()
    {
        _state.Free.EditMomentum(500 * MomentumWarState.MomentumScale);
        CardolanPledgesFree();
        Live("empire_w", "empire_s", "cardolan");

        Assert.IsTrue(_enrollment.RemoveKingdom(_state, "vlandia"));
        var outcome = _victory.CheckAndApplyVictory(_state);

        Assert.AreEqual(WarOutcome.FreeVictory, outcome);
        _alliance.Received(1).MakePeace("cardolan", "empire_s");
        _alliance.Received(1).MakePeace("empire_w", "empire_s");
    }
}
