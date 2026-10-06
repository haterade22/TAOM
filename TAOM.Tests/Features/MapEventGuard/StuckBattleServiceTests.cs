using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.MapEventGuard;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// The stuck AI battle verdict (#748, docs/features/map-event-guard.md "Stuck AI battles"). v1.5.4
/// <c>MapEvent.Update</c> runs a round only while both sides have healthy troops and sets a winner only inside a
/// round, so a side that reaches 0 healthy between rounds never ends; a destroyed party left attached makes
/// <c>CanPartyJoinBattle</c> refuse every joiner. These tests pin which battles the guard touches and how.
/// </summary>
[TestClass]
public class StuckBattleServiceTests
{
    private IEnlistmentStateQuery _enlistment = null!;
    private StuckBattleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _sut = new StuckBattleService(
            _enlistment, Substitute.For<IStuckBattleAdapter>(), Substitute.For<ICoopSessionProvider>(),
            Substitute.For<IModLogger>());
    }

    internal static StuckBattleSnapshot Snap(
        int attackerHealthy = 8000,
        int defenderHealthy = 0,
        double ageHours = 24,
        int destroyedParties = 0,
        bool isVillageHostileAction = false,
        bool involvesPlayer = false,
        bool hasPendingOutcome = false,
        IReadOnlyList<string>? partyIds = null) =>
        new StuckBattleSnapshot(
            ageHours: ageHours,
            attackerHealthy: attackerHealthy,
            defenderHealthy: defenderHealthy,
            destroyedParties: destroyedParties,
            isVillageHostileAction: isVillageHostileAction,
            involvesPlayer: involvesPlayer,
            hasPendingOutcome: hasPendingOutcome,
            partyIds: partyIds ?? new[] { "lord_party_1", "lord_party_2" });

    [TestMethod]
    public void Decide_EmptyDefenderSide_AwardsTheAttackers() =>
        Assert.AreEqual(StuckBattleVerdict.AwardAttacker, _sut.Decide(Snap(attackerHealthy: 8000, defenderHealthy: 0)));

    [TestMethod]
    public void Decide_EmptyAttackerSide_AwardsTheDefenders() =>
        Assert.AreEqual(StuckBattleVerdict.AwardDefender, _sut.Decide(Snap(attackerHealthy: 0, defenderHealthy: 300)));

    // The engine's rule outside a round (MapEvent.CheckIfOneSideHasLost): with both sides empty the defenders hold.
    [TestMethod]
    public void Decide_BothSidesEmpty_TheDefendersHold() =>
        Assert.AreEqual(StuckBattleVerdict.AwardDefender, _sut.Decide(Snap(attackerHealthy: 0, defenderHealthy: 0)));

    [TestMethod]
    public void Decide_BothSidesHealthy_LeavesItToTheEngine() =>
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(attackerHealthy: 500, defenderHealthy: 400, ageHours: 500)));

    [TestMethod]
    public void Decide_NegativeHealthyCount_CountsAsEmpty() =>
        Assert.AreEqual(StuckBattleVerdict.AwardAttacker, _sut.Decide(Snap(attackerHealthy: 10, defenderHealthy: -1)));

    [TestMethod]
    public void Decide_DestroyedPartyAttached_DetachesFirst() =>
        Assert.AreEqual(StuckBattleVerdict.DetachDestroyed,
            _sut.Decide(Snap(attackerHealthy: 500, defenderHealthy: 400, destroyedParties: 1)));

    [TestMethod]
    public void Decide_DestroyedPartyAndAnEmptySide_DetachesFirst() =>
        Assert.AreEqual(StuckBattleVerdict.DetachDestroyed, _sut.Decide(Snap(defenderHealthy: 0, destroyedParties: 2)));

    [TestMethod]
    public void Decide_YoungerThanTheGraceWindow_LeavesItAlone() =>
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(ageHours: StuckBattleService.GraceHours - 0.1)));

    [TestMethod]
    public void Decide_ExactlyAtTheGraceWindow_Resolves() =>
        Assert.AreEqual(StuckBattleVerdict.AwardAttacker, _sut.Decide(Snap(ageHours: StuckBattleService.GraceHours)));

    [TestMethod]
    public void Decide_YoungBattleWithADestroyedParty_LeavesItAlone() =>
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(ageHours: 1, destroyedParties: 1)));

    [TestMethod]
    public void Decide_NaNAge_FailsTheGraceGate() =>
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(ageHours: double.NaN)));

    [TestMethod]
    public void Decide_InfiniteAge_FailsTheGraceGate() =>
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(ageHours: double.PositiveInfinity)));

    [TestMethod]
    public void Decide_PlayerBattle_LeavesItAlone()
    {
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(involvesPlayer: true)));
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(involvesPlayer: true, destroyedParties: 1)));
    }

    [TestMethod]
    public void Decide_EnlistedCommandersBattle_LeavesItAlone()
    {
        _enlistment.IsEnlisted.Returns(true);
        _enlistment.IsCommanderParty("commander_party").Returns(true);

        Assert.AreEqual(StuckBattleVerdict.None,
            _sut.Decide(Snap(destroyedParties: 1, partyIds: new[] { "enemy_party", "commander_party" })));
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(partyIds: new[] { "commander_party" })));
    }

    [TestMethod]
    public void Decide_EnlistedCommanderElsewhere_StillResolves()
    {
        _enlistment.IsEnlisted.Returns(true);
        _enlistment.IsCommanderParty("commander_party").Returns(true);

        Assert.AreEqual(StuckBattleVerdict.AwardAttacker, _sut.Decide(Snap(partyIds: new[] { "enemy_party" })));
    }

    [TestMethod]
    public void Decide_NotEnlisted_SkipsTheCommanderLookup()
    {
        _enlistment.IsEnlisted.Returns(false);

        Assert.AreEqual(StuckBattleVerdict.AwardAttacker, _sut.Decide(Snap()));
        _enlistment.DidNotReceive().IsCommanderParty(Arg.Any<string>());
    }

    // Each IsCommanderParty call looks the commander up again; a battle the sweep would leave alone asks nothing.
    [TestMethod]
    public void Decide_EnlistedButBattleLeftAlone_SkipsTheCommanderLookup()
    {
        _enlistment.IsEnlisted.Returns(true);

        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(ageHours: 1)));
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(attackerHealthy: 500, defenderHealthy: 400)));
        _enlistment.DidNotReceive().IsCommanderParty(Arg.Any<string>());
    }

    [TestMethod]
    public void Decide_OutcomeAlreadyPending_LeavesItToFinish() =>
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(hasPendingOutcome: true, destroyedParties: 1)));

    [TestMethod]
    public void Decide_RaidWithAnEmptySide_LeavesItToTheRaid() =>
        Assert.AreEqual(StuckBattleVerdict.None, _sut.Decide(Snap(attackerHealthy: 40, defenderHealthy: 0, isVillageHostileAction: true)));

    [TestMethod]
    public void Decide_RaidWithADestroyedParty_StillDetaches() =>
        Assert.AreEqual(StuckBattleVerdict.DetachDestroyed,
            _sut.Decide(Snap(attackerHealthy: 40, defenderHealthy: 0, isVillageHostileAction: true, destroyedParties: 1)));

    [TestMethod]
    public void Decide_NullPartyIds_AreTreatedAsNoCommander()
    {
        _enlistment.IsEnlisted.Returns(true);

        Assert.AreEqual(StuckBattleVerdict.AwardAttacker,
            _sut.Decide(new StuckBattleSnapshot(24, 10, 0, 0, false, false, false, null!)));
    }
}
