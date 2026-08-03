using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.SpecialResources;

[TestClass]
public class SpecialResourceEarnPolicyTests
{
    // --- The regression this policy exists for ---

    [TestMethod]
    public void IsPlayerVictory_PlayerFightsInsideAnAiLedArmyAndThatSideWins_Earns()
    {
        // THE single-player bug the multiplayer report surfaced without noticing. The old gate asked
        // whether the player IS the winning side's LeaderParty.LeaderHero; join any lord's army and
        // you are not, so every victory paid zero. Participation is the correct question.
        Assert.IsTrue(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.Attacker));
    }

    [TestMethod]
    public void IsPlayerVictory_PlayerLedTheWinningSide_StillEarns()
    {
        // The case that already worked must keep working — the fix widens the gate, never narrows it.
        Assert.IsTrue(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Defender, winningSide: BattleSideEnum.Defender));
    }

    [TestMethod]
    public void IsPlayerVictory_PlayerOnTheLosingSide_DoesNotEarn()
    {
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Defender, winningSide: BattleSideEnum.Attacker));
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.Defender));
    }

    [TestMethod]
    public void IsPlayerVictory_BattleUnresolved_DoesNotEarn()
    {
        // Under co-op a client routinely observes state=None because the server is authoritative and
        // never re-broadcasts BattleState. Treating that as a win would pay out for lost battles.
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.None));
    }

    [TestMethod]
    public void IsPlayerVictory_PlayerOnNoSide_DoesNotEarn()
    {
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.None, winningSide: BattleSideEnum.Attacker));
    }

    [TestMethod]
    public void IsPlayerVictory_NeitherSideResolved_DoesNotEarn()
    {
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.None, winningSide: BattleSideEnum.None));
    }

    // --- Dedicated-server attribution ---

    [TestMethod]
    public void MayCreditMainHero_DedicatedServer_IsRefused()
    {
        // MainHero there is the idle world-gen hero; crediting it banks income nobody can spend.
        Assert.IsFalse(SpecialResourceEarnPolicy.MayCreditMainHero(isDedicatedServer: true));
    }

    [TestMethod]
    public void MayCreditMainHero_NormalGameProcess_IsAllowed()
    {
        // Single-player AND a client-hosted session's host both land here — the host is a real
        // player despite also being the server, and must keep earning.
        Assert.IsTrue(SpecialResourceEarnPolicy.MayCreditMainHero(isDedicatedServer: false));
    }
}
