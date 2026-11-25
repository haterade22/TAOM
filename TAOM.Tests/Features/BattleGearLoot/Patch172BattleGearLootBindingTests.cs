using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleGearLoot.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BattleGearLoot;

[TestClass]
public class Patch172BattleGearLootBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo Target()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var mapEvent = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEvent");
        Assert.IsNotNull(mapEvent);
        var matches = mapEvent.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(m => m.Name == "LootCasualtyCharacter").ToArray();
        Assert.AreEqual(1, matches.Length, "MapEvent.LootCasualtyCharacter must resolve to exactly one method (Patch172).");
        return matches[0];
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Target_HasTheParametersThePrefixBindsByName()
    {
        var method = Target();
        Assert.AreEqual(typeof(void), method.ReturnType);
        var parameters = method.GetParameters().ToDictionary(p => p.Name!, p => p.ParameterType.FullName);
        Assert.AreEqual("TaleWorlds.CampaignSystem.CharacterObject", parameters["casualtyCharacter"]);
        Assert.AreEqual("TaleWorlds.CampaignSystem.MapEvents.MapEventParty", parameters["winnerParty"]);
        Assert.AreEqual("TaleWorlds.CampaignSystem.Roster.ItemRoster", parameters["mainPartyLootFromCasualties"]);
        Assert.AreEqual("System.Int32", parameters["maxLootedItemsPerBodyForMainParty"]);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Prefix_ReturnsBool_AndNamesTheEngineParameters()
    {
        var prefix = typeof(Patch172_BattleGearLoot).GetMethod("Prefix");
        Assert.IsNotNull(prefix);
        Assert.AreEqual(typeof(bool), prefix.ReturnType);
        var names = prefix.GetParameters().Select(p => p.Name).ToArray();
        CollectionAssert.IsSubsetOf(new[] { "casualtyCharacter", "winnerParty", "maxLootedItemsPerBodyForMainParty", "mainPartyLootFromCasualties" }, names);
    }
}
