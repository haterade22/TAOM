using System.Collections.Generic;
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

    private const string MainHeroId = "main_hero";

    private static (CheatMenuService Service, CheatPartyRules Rules, ICoopSessionProvider Coop) Service() =>
        ServiceWithCache(Substitute.For<IPartySizeCacheInvalidator>());

    private static (CheatMenuService Service, CheatPartyRules Rules, ICoopSessionProvider Coop) ServiceWithCache(
        IPartySizeCacheInvalidator cache)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var world = Substitute.For<ICheatWorldAdapter>();
        world.MainHeroIds().Returns((MainHeroId, "kingdom", "culture"));
        var rules = new CheatPartyRules();
        var service = new CheatMenuService(world, Substitute.For<ISpecialResourceService>(), coop,
            Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>(), rules, cache);
        return (service, rules, coop);
    }

    private static bool OnlyTheMainHero(ICollection<string> ids) => ids.Count == 1 && ids.Contains(MainHeroId);

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

    // PartyBase.PartySizeLimit is cached until the roster's VersionNo changes: a flip that does not bump it leaves the old limit
    // in force (the party screen's "transfer all" cap, the Over Limit question) until some roster change.
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SetUnlimitedParty_RefreshesTheMainPartysCachedSizeLimit(bool on)
    {
        var cache = Substitute.For<IPartySizeCacheInvalidator>();
        var (service, rules, _) = ServiceWithCache(cache);
        rules.UnlimitedParty = !on;

        Assert.IsTrue(service.SetUnlimitedParty(on).Ok);

        cache.Received(1).InvalidateLedBy(Arg.Is<ICollection<string>>(ids => OnlyTheMainHero(ids)));
    }

    [TestMethod]
    public void SetUnlimitedParty_InCoop_RefreshesNothing()
    {
        var cache = Substitute.For<IPartySizeCacheInvalidator>();
        var (service, _, coop) = ServiceWithCache(cache);
        coop.IsSessionActive.Returns(true);

        service.SetUnlimitedParty(true);

        cache.DidNotReceiveWithAnyArgs().InvalidateLedBy(default!);
        cache.DidNotReceiveWithAnyArgs().InvalidateAll();
    }

    [TestMethod]
    public void SessionLaunch_SwitchLoadedOn_InSinglePlayer_StaysOnAndRefreshesTheCache()
    {
        var cache = Substitute.For<IPartySizeCacheInvalidator>();
        var (service, rules, _) = ServiceWithCache(cache);
        rules.UnlimitedParty = true;

        service.ReconcileAtSessionLaunch();

        Assert.IsTrue(rules.UnlimitedParty);
        cache.Received(1).InvalidateLedBy(Arg.Is<ICollection<string>>(ids => OnlyTheMainHero(ids)));
    }

    // Only the setter checks co-op: a single-player save with the switch on, loaded into a session, would keep acting.
    [TestMethod]
    public void SessionLaunch_SwitchLoadedOn_InCoop_TurnsItOffAndRefreshesTheCache()
    {
        var cache = Substitute.For<IPartySizeCacheInvalidator>();
        var (service, rules, coop) = ServiceWithCache(cache);
        coop.IsSessionActive.Returns(true);
        rules.UnlimitedParty = true;

        service.ReconcileAtSessionLaunch();

        Assert.IsFalse(rules.UnlimitedParty);
        cache.Received(1).InvalidateLedBy(Arg.Is<ICollection<string>>(ids => OnlyTheMainHero(ids)));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SessionLaunch_SwitchOff_DoesNothing(bool coopSession)
    {
        var cache = Substitute.For<IPartySizeCacheInvalidator>();
        var (service, rules, coop) = ServiceWithCache(cache);
        coop.IsSessionActive.Returns(coopSession);

        service.ReconcileAtSessionLaunch();

        Assert.IsFalse(rules.UnlimitedParty);
        cache.DidNotReceiveWithAnyArgs().InvalidateLedBy(default!);
    }

    [TestMethod]
    public void Behavior_ReconcilesTheSwitchAtSessionLaunch_AfterTheSaveIsRead()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuCampaignBehavior.cs", stripComments: true);
        var launch = System.Text.RegularExpressions.Regex.Match(src, @"void OnSessionLaunched\(CampaignGameStarter starter\)\s*\{(?<body>[^}]*)\}");

        Assert.IsTrue(launch.Success, "OnSessionLaunched has a block body");
        StringAssert.Contains(launch.Groups["body"].Value, "_service.ReconcileAtSessionLaunch();");
    }

    // ChangePlayerCharacterAction moves MainParty to another hero with no roster change, so the old party, now AI-led,
    // would keep its cached +10,000 limit until its roster changes.
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PlayerCharacterChanged_RefreshesEveryLimit_OnlyWhileTheSwitchIsOn(bool on)
    {
        var cache = Substitute.For<IPartySizeCacheInvalidator>();
        var (service, rules, _) = ServiceWithCache(cache);
        rules.UnlimitedParty = on;

        service.OnPlayerCharacterChanged();

        cache.Received(on ? 1 : 0).InvalidateAll();
    }

    [TestMethod]
    public void Behavior_RefreshesTheLimits_WhenThePlayerCharacterChanges()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuCampaignBehavior.cs", stripComments: true);
        StringAssert.Contains(src,
            "CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, (_, _, _, _) => _service.OnPlayerCharacterChanged())");
    }

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
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
    public void PartySizeModel_AddsTheUnlimitedBonus_BeforeTheTroopWeightPenalty()
    {
        var src = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "CulturalFeats", "Models", "TaomPartySizeModel.cs"));
        int penalty = src.LastIndexOf("_troopWeight.ApplyPartySizeWeightPenalty(", System.StringComparison.Ordinal);
        // The switch first, then MobileParty.IsMainParty: PartyBase.MainParty dereferences Campaign.Current.MainParty, which
        // vanilla nulls for a moment in OnPlayerCharacterChanged, and with the switch off nothing past the flag is read.
        int bonus = src.IndexOf("_cheatParty.SizeBonusFor(_cheatParty.UnlimitedParty && party.MobileParty?.IsMainParty == true)",
            System.StringComparison.Ordinal);
        int end = src.IndexOf("return result;", bonus < 0 ? 0 : bonus, System.StringComparison.Ordinal);

        Assert.IsTrue(penalty >= 0 && bonus >= 0, "the model applies the TroopWeight penalty and the cheat bonus");
        // Before the penalty, like every flat bonus: the penalty saves the limit it reads as the "true base", which the
        // party-screen header shows, so a bonus added after it would make the header disagree with the tooltip.
        Assert.IsTrue(bonus < penalty, "the bonus comes before the TroopWeight penalty");
        Assert.IsTrue(end > bonus, "the bonus is part of GetPartyMemberSizeLimit's result");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(src, @"IoC\.Resolve<CheatMenu\.ICheatPartyRules>\(\)").Count);
    }

    [TestMethod]
    public void PartySizeModel_NamesTheUnlimitedBonusInTheTooltip()
    {
        var src = RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs", stripComments: true);

        Assert.IsFalse(src.Contains("party == PartyBase.MainParty"), "PartyBase.MainParty can be null mid player change");
        StringAssert.Matches(src, new System.Text.RegularExpressions.Regex(
            @"UnlimitedPartyBonusText\s*=\s*new(\s+TextObject)?\(""\{=taom_cheat_unlimited_party_bonus\}Unlimited party \(cheat\)""\)"));
        StringAssert.Matches(src, new System.Text.RegularExpressions.Regex(
            @"SizeBonusFor\([^;]*\),\s*UnlimitedPartyBonusText\);"));
    }
}
