using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapEventGuard;
using TAOM.Features.MapEventGuard.Hooks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Tests.Features.MapEventGuard;

[TestClass]
public class Patch173OrphanedBattleEncounterTests
{
    [TestMethod]
    public void Category_IsDeclaredByTheEncounterGuardModule()
    {
        var module = new EncounterGuardModule();

        Assert.IsTrue(module.PatchCategories.Any(c => c.Category == Patch173_OrphanedBattleEncounter.Category),
            "EncounterGuardModule must apply Patch173's category.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EncounterManager_StartPartyEncounter_ResolvesWithAttackerAndDefender()
    {
        var target = AccessTools.DeclaredMethod(typeof(EncounterManager), nameof(EncounterManager.StartPartyEncounter),
            new[] { typeof(PartyBase), typeof(PartyBase) });
        Assert.IsNotNull(target, "EncounterManager.StartPartyEncounter(PartyBase, PartyBase) did not resolve; Patch173 targets it.");
        CollectionAssert.AreEqual(new[] { "attackerParty", "defenderParty" },
            target.GetParameters().Select(p => p.Name).ToArray(), "Patch173's prefix binds attackerParty and defenderParty by name.");
    }

    [TestMethod]
    public void SettlementPrefix_SharesTheCategory()
    {
        var category = (HarmonyPatchCategory)typeof(Patch173_OrphanedBattleSettlementEncounter)
            .GetCustomAttributes(typeof(HarmonyPatchCategory), false).Single();

        Assert.AreEqual(Patch173_OrphanedBattleEncounter.Category, category.info.category);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EncounterManager_StartSettlementEncounter_ResolvesWithAttackerAndSettlement()
    {
        var target = AccessTools.DeclaredMethod(typeof(EncounterManager), nameof(EncounterManager.StartSettlementEncounter),
            new[] { typeof(MobileParty), typeof(Settlement) });
        Assert.IsNotNull(target, "EncounterManager.StartSettlementEncounter(MobileParty, Settlement) did not resolve; Patch173 targets it.");
        CollectionAssert.AreEqual(new[] { "attackerParty", "settlement" },
            target.GetParameters().Select(p => p.Name).ToArray(), "Patch173's settlement prefix binds attackerParty and settlement by name.");
    }

    [TestMethod]
    public void Prefixes_WithoutAService_LetVanillaRun()
    {
        Patch173_OrphanedBattleEncounter.Initialize(null, null);
        Patch173_OrphanedBattleSettlementEncounter.Initialize(null, null);

        Assert.IsTrue(Patch173_OrphanedBattleEncounter.Prefix(null!, null!));
        Patch173_OrphanedBattleSettlementEncounter.Prefix(null!, null!);
    }
}
