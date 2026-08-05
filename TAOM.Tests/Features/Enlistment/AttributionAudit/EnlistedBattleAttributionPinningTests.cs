using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.Enlistment.AttributionAudit;

/// <summary>
/// Pins the SpecialResources participation policy for ENLISTED service (#375): the player spends
/// months parked inside a commander's map events, so every earn decision must key on
/// participation (<c>MapEvent.PlayerSide == MapEvent.WinningSide</c>), never on
/// "party leader == player".
///
/// Engine semantics these pins rest on (v1.4.7 dump, MapEvent.cs:107-109 + 282):
/// <c>PlayerMapEvent =&gt; MobileParty.MainParty?.MapEvent</c>,
/// <c>PlayerSide =&gt; PartyBase.MainParty.Side</c>, <c>IsPlayerMapEvent =&gt; this == PlayerMapEvent</c>.
/// When ServiceBattleService restores the parked main party and JoinBattle succeeds
/// (EnlistedBattle state), MainParty is an involved NON-LEADER party of the commander's event —
/// PlayerSide is set to the commander's side even though the commander's party is the side's
/// LeaderParty. When the player stays parked (EnlistedAttached), MainParty is in no event and
/// PlayerSide is None.
/// </summary>
[TestClass]
public class EnlistedBattleAttributionPinningTests
{
    [TestMethod]
    public void IsPlayerVictory_EnlistedPlayerFoughtInsideCommandersEventAndSideWon_Earns()
    {
        // The enlisted-service case this audit exists for: the COMMANDER leads the winning side,
        // the player is a hidden non-leader participant. A leader-keyed gate would pay nothing for
        // the entire service term; the participation gate pays because PlayerSide is set whenever
        // MainParty is in the event, whoever commands it.
        Assert.IsTrue(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.Attacker));
    }

    [TestMethod]
    public void IsPlayerVictory_EnlistedPlayerFoughtOnDefendingSideAndItHeld_Earns()
    {
        // Same pin from the defender bench — the commander's party defends, the player joined.
        Assert.IsTrue(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Defender, winningSide: BattleSideEnum.Defender));
    }

    [TestMethod]
    public void IsPlayerVictory_EnlistedPlayerStayedParkedDuringCommandersVictory_DoesNotEarn()
    {
        // EnlistedAttached (parked, IsActive=false, in no map event): PartyBase.MainParty.Side is
        // None, so a commander victory the player sat out pays nothing — and, symmetrically, the
        // commander's win is never misattributed to the player.
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.None, winningSide: BattleSideEnum.Attacker));
    }

    [TestMethod]
    public void IsPlayerVictory_EnlistedPlayerFoughtAndCommandersSideLost_DoesNotEarn()
    {
        // Losing inside the commander's event must not pay: participation is necessary, not
        // sufficient — the side still has to win.
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Defender, winningSide: BattleSideEnum.Attacker));
    }

    [TestMethod]
    public void IsPlayerVictory_CommanderBattleUnresolvedWhenEventEnds_DoesNotEarn()
    {
        // A commander event that dissolves without a victory (retreat, sally-out merge) reports
        // WinningSide None; the policy must not read the enlisted player's set PlayerSide as a win.
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.None));
    }
}
