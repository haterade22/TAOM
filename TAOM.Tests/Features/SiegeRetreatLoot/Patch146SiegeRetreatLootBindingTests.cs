using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SiegeRetreatLoot.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SiegeRetreatLoot;

/// <summary>
/// Drift-guards for Patch146 (#1121). The adapter calls the private <c>MapEvent.LootDefeatedPartyCasualties</c> by name
/// and the patch binds the target's parameter by NAME, so a rename or a changed signature on an engine bump would
/// otherwise turn the retreat loot into a silent no-op. The decisions are covered by <c>SiegeRetreatLootServiceTests</c>.
/// </summary>
[TestClass]
public class Patch146SiegeRetreatLootBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Engine(string fullName)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CasualtyLootStep_ResolvesPrivateInstance_WithWinnerAndDefeatedPartyLists()
    {
        var mapEvent = Engine("TaleWorlds.CampaignSystem.MapEvents.MapEvent");
        var method = mapEvent.GetMethod("LootDefeatedPartyCasualties", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "LootDefeatedPartyCasualties is gone: SiegeRetreatLootAdapter cannot run the retreat loot (Patch146).");
        Assert.AreEqual(typeof(void), method.ReturnType);
        CollectionAssert.AreEqual(new[] { "winnerParties", "defeatedParties" }, method.GetParameters().Select(p => p.Name).ToArray());
        foreach (var parameter in method.GetParameters())
        {
            Assert.AreEqual("TaleWorlds.Library.MBReadOnlyList`1", parameter.ParameterType.GetGenericTypeDefinition().FullName);
            Assert.AreEqual("TaleWorlds.CampaignSystem.MapEvents.MapEventParty",
                parameter.ParameterType.GetGenericArguments()[0].FullName);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ContinueBattle_ResolvesPublicInstance_WithNamedBattleResultParameter()
    {
        var component = Engine("TaleWorlds.CampaignSystem.MapEvents.SiegeAssaultEventComponent");
        var method = component.GetMethod("OnPlayerEncounterContinueBattle", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, "OnPlayerEncounterContinueBattle is gone: Patch146 has no target.");
        Assert.AreEqual(typeof(void), method.ReturnType);
        CollectionAssert.AreEqual(new[] { "campaignBattleResult", "nextEncounterState", "stateHandled" },
            method.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual("TaleWorlds.CampaignSystem.Encounters.CampaignBattleResult", method.GetParameters()[0].ParameterType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PrefixAndPostfix_BindTheTargetParameterByNameAndShareState()
    {
        var prefix = typeof(Patch146_SiegeRetreatLoot).GetMethod("Prefix");
        var postfix = typeof(Patch146_SiegeRetreatLoot).GetMethod("Postfix");
        Assert.IsNotNull(prefix);
        Assert.IsNotNull(postfix);
        CollectionAssert.AreEqual(new[] { "campaignBattleResult", "__state" }, prefix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(prefix.GetParameters()[1].IsOut, "The prefix must set __state on every path (out parameter).");
        CollectionAssert.AreEqual(new[] { "__state" }, postfix.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void StateAndLootMembersTheAdapterReads_StayPublic()
    {
        var mapEvent = Engine("TaleWorlds.CampaignSystem.MapEvents.MapEvent");
        foreach (var name in new[] { "BattleState", "WinningSide", "DefeatedSide", "PlayerSide", "Component" })
        {
            Assert.IsNotNull(mapEvent.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod(),
                "MapEvent." + name + " lost its public getter.");
        }

        var component = Engine("TaleWorlds.CampaignSystem.MapEvents.MapEventComponent");
        var canLoot = component.GetMethod("CanLootDefeatedPartyCasualties", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(canLoot, "MapEventComponent.CanLootDefeatedPartyCasualties is gone.");
        Assert.AreEqual(typeof(bool), canLoot.ReturnType);
        Assert.AreEqual(0, canLoot.GetParameters().Length);
    }
}
