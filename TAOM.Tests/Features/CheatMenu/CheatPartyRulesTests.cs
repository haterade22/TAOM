using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentDesertion.Hooks;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The cheat menu's "Unlimited party" switch (CheatPartyRules): the size bonus for the main party only, the switch through the
/// service (refused in co-op), off in every new campaign, and where each rule it skips reads it. The skips sit in engine code
/// (a daily tick and a recruit guard on sealed party types), so their place is pinned in the IL, the way
/// AlignmentDesertionBehaviorTests pins its gate, and in the party-size model's source, the way AiPartySizeOrderingTests does.
/// </summary>
[TestClass]
public class CheatPartyRulesTests
{
    // ---- the rules ----

    [DataTestMethod]
    [DataRow(true, true, CheatPartyRules.UnlimitedSizeBonus)]
    [DataRow(true, false, 0f)]
    [DataRow(false, true, 0f)]
    [DataRow(false, false, 0f)]
    public void SizeBonusFor_OnlyTheMainPartyWhileOn(bool on, bool isMainParty, float expected)
    {
        var rules = new CheatPartyRules { UnlimitedParty = on };

        Assert.AreEqual(expected, rules.SizeBonusFor(isMainParty));
    }

    [TestMethod]
    public void UnlimitedSizeBonus_IsFarAboveAnyPartyAndInsideTheBonusHelpersCeiling()
    {
        Assert.IsTrue(CheatPartyRules.UnlimitedSizeBonus >= 5_000f);
        Assert.IsTrue(CheatPartyRules.UnlimitedSizeBonus <= TAOM.Features.AiPartySize.AiPartySizeService.MaxFlatBonus,
            "AddResultFrameBonus ignores a bonus above MaxFlatBonus");
    }

    // ---- the switch ----

    private static (CheatMenuService Service, CheatPartyRules Rules, ICoopSessionProvider Coop) Service()
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var rules = new CheatPartyRules();
        var service = new CheatMenuService(Substitute.For<ICheatWorldAdapter>(), Substitute.For<ISpecialResourceService>(), coop,
            Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>(), rules);
        return (service, rules, coop);
    }

    [TestMethod]
    public void SetUnlimitedParty_TurnsItOnAndOff()
    {
        var (service, rules, _) = Service();

        Assert.IsTrue(service.SetUnlimitedParty(true).Ok);
        Assert.IsTrue(rules.UnlimitedParty);
        Assert.IsTrue(service.UnlimitedParty);

        Assert.IsTrue(service.SetUnlimitedParty(false).Ok);
        Assert.IsFalse(rules.UnlimitedParty);
    }

    [TestMethod]
    public void SetUnlimitedParty_InCoop_IsRefusedAndChangesNothing()
    {
        var (service, rules, coop) = Service();
        coop.IsSessionActive.Returns(true);

        Assert.AreEqual(CheatOutcomeKind.CoopRefused, service.SetUnlimitedParty(true).Kind);
        Assert.IsFalse(rules.UnlimitedParty);
    }

    [TestMethod]
    public void ANewCampaign_StartsWithTheSwitchOff()
    {
        var rules = new CheatPartyRules { UnlimitedParty = true };
        var (service, _, _) = Service();

        _ = new CheatMenuCampaignBehavior(service, Substitute.For<ICheatMenuSettingsProvider>(), Substitute.For<ICheatWorldAdapter>(),
            Substitute.For<ICheatMenuMapViewAdapter>(), rules);

        Assert.IsFalse(rules.UnlimitedParty, "the rules are a singleton: the behavior built for a new campaign must clear them");
    }

    [TestMethod]
    public void CheatMenuModule_OwnsSaveData()
    {
        Assert.IsTrue(new CheatMenuModule().OwnsSaveData, "its behavior saves the switch");
    }

    [TestMethod]
    public void SaveKey_IsTaomPrefixed_AndSyncDataUsesIt()
    {
        StringAssert.StartsWith(CheatMenuCampaignBehavior.UnlimitedPartyKey, "_taom_");
        var src = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "CheatMenu", "Hooks", "CheatMenuCampaignBehavior.cs"));
        StringAssert.Contains(src, "dataStore.SyncData(UnlimitedPartyKey, ref unlimited)");
    }

    // ---- where each skipped rule reads the switch ----

    private static int CallIndex(MethodInfo method, string name) =>
        IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()!).ToList().FindIndex(m => m.Name == name);

    private static MethodInfo Method<T>(string name) =>
        typeof(T).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
        ?? throw new AssertFailedException(typeof(T).Name + "." + name + " is gone");

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AlignmentDesertion_DailyTick_AsksTheSwitchBeforeTheMainPartyTest_AndBeforeTheOwnerCheck()
    {
        var tick = Method<AlignmentDesertionBehavior>("OnDailyTickParty");
        int unlimited = CallIndex(tick, "get_KeepsMainPartyTroops");
        int owner = CallIndex(tick, "TryResolveOwner");

        Assert.IsTrue(unlimited >= 0, "OnDailyTickParty reads the Unlimited party switch");
        Assert.IsTrue(owner >= 0, "OnDailyTickParty resolves the owner");
        Assert.IsTrue(unlimited < owner, "the main party with the switch on leaves before any desertion work");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void RecruitGuard_AsksTheSwitchBeforeItsDesertionChecks()
    {
        var wouldDesert = Method<DesertionRecruitGuard>("WouldDesertFromParty");
        Assert.IsTrue(CallIndex(wouldDesert, "get_KeepsMainPartyTroops") >= 0, "WouldDesertFromParty reads the switch");
        Assert.IsTrue(CallIndex(wouldDesert, "get_KeepsMainPartyTroops") < CallIndex(wouldDesert, "WouldDesert"),
            "the main party with the switch on is never told a troop would desert");

        var cap = Method<DesertionRecruitGuard>("CapHeroIndex");
        Assert.IsTrue(CallIndex(cap, "get_KeepsMainPartyTroops") >= 0, "CapHeroIndex reads the switch");
        Assert.IsTrue(CallIndex(cap, "get_KeepsMainPartyTroops") < CallIndex(cap, "FirstDeserterSlot"),
            "the player's own volunteer slots are not capped while the switch is on");

        // Garrisons and other parties keep the rule: the garrison cap never reads the switch.
        Assert.AreEqual(-1, CallIndex(Method<DesertionRecruitGuard>("CapGarrisonSlots"), "get_KeepsMainPartyTroops"));
    }

    [TestMethod]
    public void PartySizeModel_AddsTheUnlimitedBonusLast_AfterTheTroopWeightPenalty()
    {
        var src = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "CulturalFeats", "Models", "TaomPartySizeModel.cs"));
        int penalty = src.LastIndexOf("_troopWeight.ApplyPartySizeWeightPenalty(", System.StringComparison.Ordinal);
        int bonus = src.IndexOf("_cheatParty.SizeBonusFor(party == PartyBase.MainParty)", System.StringComparison.Ordinal);
        int end = src.IndexOf("return result;", bonus < 0 ? 0 : bonus, System.StringComparison.Ordinal);

        Assert.IsTrue(penalty >= 0 && bonus >= 0, "the model applies the TroopWeight penalty and the cheat bonus");
        Assert.IsTrue(bonus > penalty, "the bonus comes after the TroopWeight penalty, so nothing later shrinks it");
        Assert.IsTrue(end > bonus, "the bonus is part of GetPartyMemberSizeLimit's result");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(src, @"IoC\.Resolve<CheatMenu\.ICheatPartyRules>\(\)").Count);
    }
}
