using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The order in which presence tests decide a mission's kind (build-spec section 4): a controller type first, a campaign
/// location next, and only then combat. Mode alone proves nothing, because a town reports StartUp.
/// </summary>
[TestClass]
public class SeatMissionKindRulesTests
{
    private static SeatMissionKind Classify(bool hideout = false, bool tournament = false, bool arena = false,
        bool location = false, bool combat = false, bool teamBattle = false, bool battleMode = false) =>
        SeatMissionKindRules.Classify(new SeatMissionProbe(hideout, tournament, arena, location, combat, teamBattle, battleMode));

    [TestMethod]
    public void Classify_HideoutController_IsHideout_EvenWithCombat() =>
        Assert.AreEqual(SeatMissionKind.Hideout, Classify(hideout: true, combat: true, teamBattle: true, battleMode: true));

    [TestMethod]
    public void Classify_TournamentBehavior_IsTournament_EvenInsideALocation() =>
        Assert.AreEqual(SeatMissionKind.Tournament, Classify(tournament: true, location: true, combat: true, battleMode: true));

    [TestMethod]
    public void Classify_ArenaController_IsArena() =>
        Assert.AreEqual(SeatMissionKind.Arena, Classify(arena: true, location: true));

    [TestMethod]
    public void Classify_ControllerOrder_HideoutThenTournamentThenArena()
    {
        Assert.AreEqual(SeatMissionKind.Hideout, Classify(hideout: true, tournament: true, arena: true));
        Assert.AreEqual(SeatMissionKind.Tournament, Classify(tournament: true, arena: true));
    }

    [TestMethod]
    public void Classify_CampaignLocation_IsSettlement_EvenWhenAFightIsOn() =>
        Assert.AreEqual(SeatMissionKind.Settlement, Classify(location: true, combat: true, battleMode: true));

    [TestMethod]
    public void Classify_CombatWithATeamAi_IsBattle() =>
        Assert.AreEqual(SeatMissionKind.Battle, Classify(combat: true, teamBattle: true));

    [TestMethod]
    public void Classify_CombatInBattleOrDeploymentMode_IsBattle() =>
        Assert.AreEqual(SeatMissionKind.Battle, Classify(combat: true, battleMode: true));

    [TestMethod]
    public void Classify_CombatAloneWithStartUpMode_IsOther() =>
        Assert.AreEqual(SeatMissionKind.Other, Classify(combat: true));

    [TestMethod]
    public void Classify_BattleModeWithoutCombat_IsOther() =>
        Assert.AreEqual(SeatMissionKind.Other, Classify(teamBattle: true, battleMode: true));

    [TestMethod]
    public void Classify_NothingKnown_IsOther() =>
        Assert.AreEqual(SeatMissionKind.Other, Classify());
}
