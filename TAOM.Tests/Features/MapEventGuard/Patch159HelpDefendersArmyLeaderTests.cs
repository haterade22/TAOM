using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapEventGuard.Hooks;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// Patch159 (#1429, from Silas He's v1.5.6 fix package): "Help defenders" at a siege restarts the player encounter
/// with the siege camp's leader party as the defender. When that party is attached to another lord's army, the event
/// builds with the army leader on the other side, the defender side empties and the event throws; a later save is
/// broken. The encounter starts against the army leader instead, the path the engine takes when the camp leader leads
/// its own army.
/// </summary>
[TestClass]
public class Patch159HelpDefendersArmyLeaderTests
{
    private static readonly bool[] Both = { false, true };

    [TestMethod]
    public void ShouldUseArmyLeader_TheCrashCase_Swaps()
        => Assert.IsTrue(Patch159_HelpDefendersArmyLeader.ShouldUseArmyLeader(new Patch159_HelpDefendersArmyLeader.Facts(
            AttackerIsMainParty: true, DefenderIsMainParty: false, DefenderInMapEvent: false, DefenderLeadsOwnArmy: false,
            DefenderAttachedToArmyLeader: true, ArmyLeaderInSameCamp: true, ArmyLeaderIsMainParty: false, ArmyLeaderInMapEvent: false)));

    [TestMethod]
    public void ShouldUseArmyLeader_SwapsOnlyWhenEveryConditionHolds()
    {
        var swaps = 0;
        foreach (var attackerMain in Both)
        foreach (var defenderMain in Both)
        foreach (var defenderInEvent in Both)
        foreach (var leadsOwn in Both)
        foreach (var attached in Both)
        foreach (var sameCamp in Both)
        foreach (var leaderMain in Both)
        foreach (var leaderInEvent in Both)
        {
            var facts = new Patch159_HelpDefendersArmyLeader.Facts(attackerMain, defenderMain, defenderInEvent, leadsOwn,
                attached, sameCamp, leaderMain, leaderInEvent);
            var expected = attackerMain && !defenderMain && !defenderInEvent && !leadsOwn && attached && sameCamp
                           && !leaderMain && !leaderInEvent;
            Assert.AreEqual(expected, Patch159_HelpDefendersArmyLeader.ShouldUseArmyLeader(facts), facts.ToString());
            if (expected) swaps++;
        }
        Assert.AreEqual(1, swaps);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlayerEncounter_RestartPlayerEncounter_ResolvesWithFourParameters()
    {
        var target = AccessTools.DeclaredMethod(typeof(PlayerEncounter), nameof(PlayerEncounter.RestartPlayerEncounter),
            new[] { typeof(PartyBase), typeof(PartyBase), typeof(bool), typeof(bool) });
        Assert.IsNotNull(target, "PlayerEncounter.RestartPlayerEncounter(PartyBase, PartyBase, bool, bool) did not resolve; Patch159 targets it.");
        CollectionAssert.AreEqual(new[] { "defenderParty", "attackerParty", "forcePlayerOutFromSettlement", "isPlayerEncounterRestartedForRaid" },
            target.GetParameters().Select(p => p.Name).ToArray(), "Patch159's prefix binds defenderParty and attackerParty by name.");
    }
}
