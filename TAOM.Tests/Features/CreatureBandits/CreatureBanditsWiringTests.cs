using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Composition;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CreatureBandits;
using TAOM.Features.CreatureBandits.Hooks;
using TAOM.Features.BanditManagement.Models;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.CultureDoctrine.Models;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CreatureBandits;

/// <summary>
/// Wiring regression guard for Creature Bandits (#692). Every piece fails silently when unwired: drop the module
/// line and creature troops spawn as husks on their mounts; drop the prisoner override and spiders are led off as
/// captives; drop the tree branch and a riderless spider stands still. Harmony binds prefix parameters by name, so
/// an engine rename would only fail at patch time; the binding tests catch it here.
/// </summary>
[TestClass]
public class CreatureBanditsWiringTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    // AccessTools.GetTypesFromAssembly is the enumeration Harmony's PatchCategory itself uses, so this sees exactly the
    // classes Harmony would apply (EconomyDiagnosticsPatchDiscoveryTests). Assembly.GetTypes would instead throw on the
    // hosted unit step, where a TAOM type built on a module-assembly base cannot load.
    private static IEnumerable<Type> PatchClasses(string category) =>
        AccessTools.GetTypesFromAssembly(typeof(CreatureBanditsModule).Assembly)
            .Where(t => t.GetCustomAttributes<HarmonyPatchCategory>().Any(c => c.info.category == category));

    private static IEnumerable<Type> AllCreaturePatchClasses() =>
        PatchClasses(CreatureBanditsConfig.PatchCategory).Concat(PatchClasses(CreatureBanditsConfig.CampaignPatchCategory));

    private static MethodBase TargetOf(Type patch)
    {
        var byName = AccessTools.Method(patch, "TargetMethod");
        if (byName != null)
            return (MethodBase)byName.Invoke(null, null);
        var info = HarmonyMethod.Merge(patch.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        return AccessTools.Method(info.declaringType, info.methodName);
    }

    [TestMethod]
    public void FeatureModules_ListTheCreatureBanditsModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<CreatureBanditsModule>().Count());

    [TestMethod]
    public void Module_AppliesPatch93AtProcessLoad_AndPatch94AtGameInit()
    {
        var decls = new CreatureBanditsModule().PatchCategories.ToDictionary(d => d.Category, d => d.Phase);
        Assert.AreEqual(2, decls.Count);
        Assert.AreEqual(ApplyPhase.ProcessLoad, decls[CreatureBanditsConfig.PatchCategory]);
        // The campaign-map patches target SandBox.View and campaign menu types, loaded by game init.
        Assert.AreEqual(ApplyPhase.GameInit, decls[CreatureBanditsConfig.CampaignPatchCategory]);
    }

    [TestMethod]
    public void Patch94_HasTheMapIconNoParleyAndNoJoinPatches()
    {
        CollectionAssert.AreEquivalent(
            new[] { nameof(Patch94_CreatureBroodMapIcon), nameof(Patch94_CreatureBroodNoParley), nameof(Patch94_CreatureBandNoJoin) },
            PatchClasses(CreatureBanditsConfig.CampaignPatchCategory).Select(t => t.Name).ToList());
    }

    [TestMethod]
    public void NoJoin_TargetsTheBanditJoinRoster_AndDropsBothClans()
    {
        // #694 review: with Partners in Crime, "serve under my command" recruits every bandit party that joins the
        // encounter (BanditInteractionsCampaignBehavior.OpenRosterScreenAfterBanditEncounter, v1.5.3 :420-460), and
        // CanTroopBeTakenPrisoner is never asked. The list the prefix trims is the one the caller then destroys.
        if (!_gameLoaded) Assert.Inconclusive("game assemblies not loaded");
        var target = TargetOf(typeof(Patch94_CreatureBandNoJoin));
        Assert.IsNotNull(target, "BanditInteractionsCampaignBehavior.GetMemberAndPrisonerRostersFromParties did not resolve");
        Assert.AreEqual("parties", target!.GetParameters()[0].Name);
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/Patch94_CreatureBroodCampaign.cs", stripComments: true),
            "parties.RemoveAll(p => CreatureBanditRules.IsCreatureBandClan(p?.ActualClan?.StringId))");
    }

    [TestMethod]
    public void BattleRewardModel_KeepsFreedPrisonersOutOfCreatureBands()
    {
        // #694 (Mike): a band stays trolls only, a brood spiders only. Vanilla hands a winning bandit party any freed
        // bandit prisoner (DefaultBattleRewardModel.GetLootPrisonerChances, v1.5.3 :253-275).
        var method = typeof(TaomBattleRewardModel).GetMethod(nameof(TaomBattleRewardModel.GetLootPrisonerChances));
        Assert.AreEqual(typeof(TaomBattleRewardModel), method?.DeclaringType,
            "TaomBattleRewardModel must override GetLootPrisonerChances, or freed looters join a troll band.");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs", stripComments: true),
            "CreatureBanditAgents.WithoutCreatureBandWinners(base.GetLootPrisonerChances(winnerParties, prisonerElement))");
    }

    [TestMethod]
    public void SpawnSwitches_DefaultOn_AndTheSettingsStartFromThem()
    {
        Assert.IsTrue(CreatureBanditsConfig.DefaultSpawnBroods);
        Assert.IsTrue(CreatureBanditsConfig.DefaultSpawnTrollBands, "#694: troll bands are on by default");
        var src = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);
        StringAssert.Contains(src, "CreatureBanditSpawnBroods { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditsConfig.DefaultSpawnBroods;");
        StringAssert.Contains(src, "CreatureBanditSpawnTrollBands { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditsConfig.DefaultSpawnTrollBands;");
    }

    [TestMethod]
    public void Module_AddsTheRoutedCountBackstopAndTheDiagnostics()
    {
        CollectionAssert.AreEquivalent(
            new[] { typeof(CreatureBanditMissionBehavior), typeof(TAOM.Features.CreatureBandits.Diagnostics.CreatureBanditDiagnosticsBehavior) },
            new CreatureBanditsModule().MissionBehaviors.Select(d => d.BehaviorType).ToList());
    }

    [TestMethod]
    public void SubModule_ResetsTheDiagnosticsOnUnload()
    {
        // The diagnostics hold a static logger and per-mission state (ResetForUnloadSweepTests' class of defect).
        StringAssert.Contains(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true),
            "CreatureBandits.Diagnostics.CreatureBanditDiag.ResetForUnload()");
    }

    [TestMethod]
    public void Module_AddsBothSpawnersAndTheCampaignDiagnostics()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(CreatureBroodSpawnBehavior), typeof(TrollBandSpawnBehavior),
                typeof(TAOM.Features.CreatureBandits.Diagnostics.CreatureBroodCampaignDiagBehavior),
            },
            new CreatureBanditsModule().CampaignBehaviors.Select(d => d.BehaviorType).ToList());
    }

    [TestMethod]
    public void TrollSpawner_ReadsTheMcmSwitchEachDay_AndCountsKingdoms()
    {
        // #694: the switch gates new bands only; the pure rule is tested in CreatureBanditRulesTests, this pins its wiring.
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/TrollBandSpawnBehavior.cs", stripComments: true);
        StringAssert.Contains(src, "TaomSettings.Instance?.CreatureBanditSpawnTrollBands ?? CreatureBanditsConfig.DefaultSpawnTrollBands");
        StringAssert.Contains(src, "CreatureBanditRules.KingdomsOwedATrollBand(");
        // A band counts for a kingdom id only: Clan.MapFaction is the clan itself when it has no kingdom.
        StringAssert.Contains(src, "as Kingdom)?.StringId");
        // Both spawners create and re-patrol their bands through the one helper, so the vanilla looter steps live once.
        StringAssert.Contains(src, "CreatureBandParties.Spawn(");
        StringAssert.Contains(src, "CreatureBandParties.ReturnStraysToPatrol(");
        var brood = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBroodSpawnBehavior.cs", stripComments: true);
        StringAssert.Contains(brood, "CreatureBandParties.Spawn(");
        StringAssert.Contains(brood, "CreatureBandParties.ReturnStraysToPatrol(");
        // #694 (Mike): vanilla's own out-of-sight rule (Codex O1: retries around the first point, path distance). The in-game
        // proof is the spawn line's fromPlayer above playerSight.
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBandParties.cs", stripComments: true),
            "DistanceHelper.FindClosestDistanceFromMobilePartyToPoint(player, retry, MobileParty.NavigationType.Default, out _)");
        // Only the console command places a band itself; the daily spawners keep the out-of-sight draw.
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBandParties.cs", stripComments: true),
            "CampaignVec2 point = at ?? OutOfPlayerSight(");
        StringAssert.Contains(src, "CreatureBandParties.Spawn(clan, settlements[MBRandom.RandomInt(settlements.Count)]);");
        StringAssert.Contains(brood, "CreatureBandParties.Spawn(clan, anchors[MBRandom.RandomInt(anchors.Length)]);");
    }

    [TestMethod]
    public void TrollBanditToughness_ReachesHitPointsAndEveryHit()
    {
        // Hit points: a troop's campaign battle health and its auto-resolve casualty roll read CharacterObject.MaxHitPoints(),
        // which is TaomCharacterStatsModel. Damage: both damage models' ApplyDamageReductions end in
        // CreatureBanditDamage.Reduce (campaign and Custom Battle).
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/TroopProgression/Models/TaomCharacterStatsModel.cs", stripComments: true),
            "CreatureBanditRules.TrollBanditHitPointsBonus(character?.StringId)");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/CreatureBanditDamage.cs", stripComments: true),
            "CreatureBanditRules.TrollBanditDamageTakenFactor(attackInformation.VictimAgent?.Character?.StringId)");
    }

    [TestMethod]
    public void NoParleyAndLooterCap_CoverBothClans()
    {
        // Trolls, like spiders, go straight to attack or leave, and vanilla must never spawn either clan map-wide.
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/Patch94_CreatureBroodCampaign.cs", stripComments: true),
            "CreatureBanditRules.IsCreatureBandClan(PlayerEncounter.EncounteredMobileParty?.ActualClan?.StringId)");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs", stripComments: true),
            "CreatureBanditRules.IsCreatureBandClan(clan?.StringId) ? 0");
    }

    [TestMethod]
    public void MissionBehavior_PutsCreaturesOnTheScoreboard()
    {
        // The battle observer reports humans only (BattleObserverMissionLogic.cs:35-76; Codex 2026-09-28 F1): without the
        // bridge a brood shows no troops, casualties or kills on the campaign and Custom Battle scoreboards.
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBanditMissionBehavior.cs", stripComments: true);
        StringAssert.Contains(src, "_scoreboard.OnBuilt(");
        StringAssert.Contains(src, "_scoreboard.Flush(");
        StringAssert.Contains(src, "_scoreboard.OnRemoved(");
        var bridge = RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/CreatureScoreboardBridge.cs", stripComments: true);
        StringAssert.Contains(bridge, "CreatureBanditRules.ScoreboardCasualty(");
        StringAssert.Contains(bridge, "CreatureBanditRules.ScoreboardBridgeCreditsKill(");
        // Codex second pass: a row needs no combatant (Custom Battle console origins have none), and a creature leaves
        // the scoreboard only if it was added, once.
        StringAssert.Contains(bridge, "CreatureBanditRules.IsScoreboardRow(");
        StringAssert.Contains(bridge, "_added.TryRemove(affected");
        Assert.IsFalse(bridge.Contains("BattleCombatant != null"), "the row rule must not demand a combatant");
        // Vanilla credits a kill only inside the victim's own row gate (BattleObserverMissionLogic.cs:54-75): a victim
        // with no row credits no one.
        StringAssert.Contains(bridge, "if (!victimHasRow");
    }

    [TestMethod]
    public void BroodSpawner_ReadsTheMcmSwitchEachDay()
    {
        // The switch gates new broods only; the pure rule is tested in CreatureBanditRulesTests, this pins its wiring.
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBroodSpawnBehavior.cs", stripComments: true);
        StringAssert.Contains(src, "TaomSettings.Instance?.CreatureBanditSpawnBroods ?? CreatureBanditsConfig.DefaultSpawnBroods");
        StringAssert.Contains(src, "BroodsToSpawnToday(existing, CreatureBanditsConfig.MaxBroods, SpawnEnabled)");
    }

    [TestMethod]
    public void Patch93_HasTheSevenPatchesInItsCategory()
    {
        // The morale route to panic is the morale models' CanPanicDueToMorale seam, not a patch (MoraleModels_NeverLetACreaturePanic).
        CollectionAssert.AreEquivalent(new[]
        {
            nameof(Patch93_CreatureBanditSpawn), nameof(Patch93_CreatureBanditNoPanic), nameof(Patch93_CreatureBanditNoRout),
            nameof(Patch93_CreatureBanditPrimaryWieldGuard), nameof(Patch93_CreatureBanditOffhandWieldGuard),
            nameof(Patch93_CreatureBanditMissileRangeGuard), nameof(Patch93_CreatureBanditWeaponState),
        }, PatchClasses(CreatureBanditsConfig.PatchCategory).Select(t => t.Name).ToList());
    }

    [TestMethod]
    public void MoraleModels_NeverLetACreaturePanic()
    {
        // CommonAIComponent.CanPanic asks BattleMoraleModel.CanPanicDueToMorale first (v1.5.3 CommonAIComponent.cs:174-177),
        // and TAOM registers a morale model for the campaign and for Custom Battle (SubModule.cs).
        foreach (var file in new[] { "TaomBattleMoraleModel.cs", "TaomCustomBattleMoraleModel.cs" })
            StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CultureDoctrine/Models/" + file, stripComments: true),
                "!CreatureBanditAgents.RefusesMoralePanic(agent)", file);
    }

    [TestMethod]
    public void Resistances_AreAppliedInTheCampaignAndInCustomBattle()
    {
        // Custom Battle installs CustomAgentApplyDamageModel and TAOM's campaign model is TaomCombatMechanicsModel: the
        // creature's damage-taken rule must sit in both. The Custom Battle twin is owned by CombatMechanics and added by
        // SubModule.RegisterCustomBattleModels (#788), so this module declares no model.
        var campaign = RepoPaths.ReadSource("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs", stripComments: true);
        StringAssert.Contains(campaign, "CreatureBanditDamage.Reduce(");

        var customBattle = RepoPaths.ReadSource("Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs", stripComments: true);
        StringAssert.Contains(customBattle, "CreatureBanditDamage.Reduce(");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/CreatureBanditDamage.cs", stripComments: true),
            "TaomAgentApplyDamageModel.BluntByVanillaRule(in attackInformation, in collisionData)",
            "a charge, kick or bash is Blunt, as vanilla computes it");

        Assert.AreEqual(0, new CreatureBanditsModule().GameModels.Count,
            "a module declaration is added after RegisterCustomBattleModels and would shadow the twin (the last model added wins)");
        StringAssert.Contains(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true),
            "basicStarter.AddModel<AgentApplyDamageModel>(new TaomCustomBattleDamageModel(");
    }

    [TestMethod]
    public void Fingerprint_DoesNotAskIsMount()
    {
        // Route A clears Mountable once the creature is built; a fingerprint keyed on IsMount would drop the creature
        // from every Patch93 guard at that moment (the June weapon-state crash class would return).
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs", stripComments: true);
        Assert.IsFalse(src.Contains("IsMount"), "CreatureBanditAgents must not read IsMount");
        StringAssert.Contains(src, "IsHuman");
    }

    [TestMethod]
    public void Spawner_ArmsTheWeaponStateScopeOnlyAroundItsOwnSpawnMonster()
    {
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/CreatureBanditSpawner.cs", stripComments: true);
        int arm = src.IndexOf("CreatureWeaponStateScope.Arm()", StringComparison.Ordinal);
        int spawn = src.IndexOf("SpawnMonster(", StringComparison.Ordinal);
        int disarm = src.IndexOf("CreatureWeaponStateScope.Disarm()", StringComparison.Ordinal);
        int finallyAt = src.IndexOf("finally", StringComparison.Ordinal);
        Assert.IsTrue(arm >= 0 && arm < spawn, "Arm() must precede SpawnMonster");
        Assert.IsTrue(finallyAt > spawn && disarm > finallyAt, "Disarm() must sit in a finally after SpawnMonster");
    }

    [TestMethod]
    public void Unmount_AndUnlist_LiveOnlyInTheRouteAStep()
    {
        // Clearing Mountable without unlisting leaves a non-mount in MountsWithoutRiders that OnAgentRemoved never
        // removes, and HumanAIComponent's mount search then walks a cleared agent (an NRE in the AI tick).
        foreach (var file in new[] { "Hooks/CreatureBanditSpawner.cs", "Hooks/CreatureBanditAgents.cs", "Hooks/Patch93_CreatureBandits.cs" })
        {
            var other = RepoPaths.ReadSource("Main/Features/CreatureBandits/" + file, stripComments: true);
            Assert.IsFalse(other.Contains("RemoveMountWithoutRider"), file);
            Assert.IsFalse(other.Contains("CombatantFlags"), file);
        }
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/CreatureRouteAUnmount.cs", stripComments: true);
        int unlist = src.IndexOf("RemoveMountWithoutRider(", StringComparison.Ordinal);
        int unmount = src.IndexOf("CombatantFlags(", StringComparison.Ordinal);
        Assert.IsTrue(unlist >= 0 && unmount > unlist, "unlist first, then clear Mountable");
        StringAssert.Contains(src, "AddMountWithoutRider(", "a failed flag change re-lists the still-Mountable creature");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]   // reads vanilla IL; the reference assemblies' bodies are `ldnull; throw`
    public void WeaponStateHook_TargetsTheOnlyPrivateCreateAgent_WithOneFlagsRead()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var target = (MethodInfo)TargetOf(typeof(Patch93_CreatureBanditWeaponState));
        Assert.IsNotNull(target, "Mission.CreateAgent(Monster, ...) not found");
        Assert.AreEqual(typeof(Agent), target.ReturnType);
        Assert.AreEqual(typeof(Monster), target.GetParameters()[0].ParameterType);
        Assert.AreEqual(1, typeof(Mission).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Count(m => m.Name == "CreateAgent"), "a second private CreateAgent overload appeared");

        var flagsGetter = AccessTools.PropertyGetter(typeof(Monster), nameof(Monster.Flags));
        int reads = PatchProcessor.GetOriginalInstructions(target).Count(i => i.Calls(flagsGetter));
        Assert.AreEqual(1, reads, "the transpiler hooks exactly one monster.Flags read, the one CreateAgentInternal gets");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RouteA_EngineMembers_Resolve()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.AreEqual(typeof(UIntPtr), AccessTools.Field(typeof(Agent), "_primaryWieldedItemIndexPointer")?.FieldType,
            "the weapon-state probe reads Agent._primaryWieldedItemIndexPointer");
        var remove = typeof(Mission).GetMethod(nameof(Mission.RemoveMountWithoutRider), new[] { typeof(Agent) });
        var add = typeof(Mission).GetMethod(nameof(Mission.AddMountWithoutRider), new[] { typeof(Agent) });
        Assert.IsTrue(remove is { IsPublic: true } && add is { IsPublic: true });
        Assert.IsNotNull(typeof(Monster).GetProperty(nameof(Monster.PelvisBoneIndex)));
    }

    [TestMethod]
    public void BattleRewardModel_RefusesCreatureTroopsAsPrisoners()
    {
        // MapEvent asks CanTroopBeTakenPrisoner before moving a defeated troop to the winner's prisoners
        // (v1.5.3 MapEvent.cs:1855); vanilla's answer is always true.
        var method = typeof(TaomBattleRewardModel).GetMethod(nameof(TaomBattleRewardModel.CanTroopBeTakenPrisoner),
            new[] { typeof(CharacterObject) });
        Assert.AreEqual(typeof(TaomBattleRewardModel), method?.DeclaringType,
            "TaomBattleRewardModel must override CanTroopBeTakenPrisoner, or creature troops become prisoners.");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs", stripComments: true),
            "!CreatureBanditAgents.RefusesPrisoner(troop?.StringId)");
    }

    [TestMethod]
    public void RefusesPrisoner_CreatureAndTrollBanditTroopsOnly()
    {
        // The rule lives in Hooks, outside the temporary Diagnostics folder, so stripping the diagnostics keeps it.
        Assert.IsTrue(CreatureBanditAgents.RefusesPrisoner("taom_spider_brood_forest"));
        Assert.IsTrue(CreatureBanditAgents.RefusesPrisoner("taom_spider_brood_pale"));
        // #694: a bandit troll is never led off, so it can never be recruited from prisoners.
        Assert.IsTrue(CreatureBanditAgents.RefusesPrisoner("taom_troll_bandit_cave"));
        Assert.IsTrue(CreatureBanditAgents.RefusesPrisoner("taom_troll_bandit_hill"));
        Assert.IsFalse(CreatureBanditAgents.RefusesPrisoner("cave_troll"));
        Assert.IsFalse(CreatureBanditAgents.RefusesPrisoner("looter"));
        Assert.IsFalse(CreatureBanditAgents.RefusesPrisoner(null));
    }

    [TestMethod]
    public void BanditDensityModel_OverridesTheLooterCap()
    {
        // The brood and troll clans are looter factions (can_have_settlement false), and vanilla spawns looters around
        // any town or village on the map (BanditSpawnCampaignBehavior.cs:514-525) up to this cap.
        var method = typeof(TaomBanditDensityModel).GetMethod(nameof(TaomBanditDensityModel.GetMaxSupportedNumberOfLootersForClan));
        Assert.AreEqual(typeof(TaomBanditDensityModel), method?.DeclaringType);
    }

    [TestMethod]
    public void CustomBattleStatModel_LocksCreatureBanditsAgainstRiding()
    {
        // The spikes run in Custom Battle, whose stat model had no creature lock (plans/_audit/2026-09-23-opus/
        // lane-2.findings.md:335), and AI soldiers look for loose mounts to ride (HumanAIComponent.cs:267-302).
        var method = typeof(TaomCustomBattleAgentStatCalculateModel).GetMethod(
            nameof(TaomCustomBattleAgentStatCalculateModel.CanAgentRideMount));
        Assert.AreEqual(typeof(TaomCustomBattleAgentStatCalculateModel), method?.DeclaringType);
    }

    [TestMethod]
    public void CreatureBanditTree_HasTheGateTheHuntAndBothAttacks()
    {
        // The creature bandit's own tree (#692), apart from the ridden spider's: gated on the fingerprint, then the
        // spider's engage and its pounce and swipe, then the hunt that moves a creature no rider steers.
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBanditBehaviorTree.cs", stripComments: true);
        StringAssert.Contains(src, "new IsCreatureBanditDecorator()");
        StringAssert.Contains(src, "new CreatureHoldTask()");
        StringAssert.Contains(src, "new SpiderEngageDecorator(");
        StringAssert.Contains(src, "new SpiderPounceTask(");
        StringAssert.Contains(src, "new SpiderSideAttackTask(");
        StringAssert.Contains(src, "new CreatureHuntTask(");
        StringAssert.Contains(src, "new OnSpiderDied()");
    }

    [TestMethod]
    public void CreatureBanditTree_HoldsUntilDeploymentIsOver()
    {
        // During deployment the engine turns AI ticking off and a scripted move teleports (Agent.cs:2462-2465): the
        // hunt would drop the creature onto the paused army. The fight is gated, and the creature holds instead.
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBanditBehaviorTree.cs", stripComments: true);
        int gate = src.IndexOf("new CreatureMayFightDecorator()", StringComparison.Ordinal);
        int engage = src.IndexOf("new SpiderEngageDecorator(", StringComparison.Ordinal);
        int hunt = src.IndexOf("new CreatureHuntTask(", StringComparison.Ordinal);
        int hold = src.IndexOf("new CreatureHoldTask()", StringComparison.Ordinal);
        Assert.IsTrue(gate >= 0 && gate < engage && engage < hunt && hunt < hold,
            "the fight gate wraps the engage and the hunt, and the hold follows as the fallback");

        var decorator = RepoPaths.ReadSource("Main/Features/CreatureBandits/BehaviorTreeElements/CreatureMayFightDecorator.cs", stripComments: true);
        StringAssert.Contains(decorator, "CreatureBanditRules.MayFight(mission.AllowAiTicking, mission.IsTeleportingAgents)");
    }

    [TestMethod]
    public void SpiderTree_NoLongerCarriesTheCreatureBranch()
    {
        var src = RepoPaths.ReadSource("Main/Features/Spider/SpiderBehaviorTree.cs", stripComments: true);
        Assert.IsFalse(src.Contains("IsCreatureBanditDecorator"), "the creature branch lives in CreatureBanditBehaviorTree");
        Assert.IsFalse(src.Contains("CreatureHuntTask"), "the hunt lives in CreatureBanditBehaviorTree");
    }

    [TestMethod]
    public void TreeTrackers_NeverBothClaimACreatureBandit()
    {
        // The tree framework only warns when a second tree is added to an agent, then overwrites its entry while the
        // first component keeps ticking: two trees would drive one spider. The spider tracker must skip creature
        // bandits, and the creature tracker claims only them.
        var spider = RepoPaths.ReadSource("Main/Features/Spider/SpiderMissionBehavior.cs", stripComments: true);
        StringAssert.Contains(spider, "!CreatureBanditAgents.Is(a)");
        var creature = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBanditMissionBehavior.cs", stripComments: true);
        StringAssert.Contains(creature, "new CreatureTreeTracker(CreatureBanditsConfig.TreeName");
        StringAssert.Contains(creature, "CreatureBanditAgents.Is");
        StringAssert.Contains(creature, "BTRegister.RegisterClass(CreatureBanditsConfig.TreeName");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryCreaturePatchParameter_BindsToTheInstalledTarget()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        foreach (var patch in AllCreaturePatchClasses())
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, $"{patch.Name}: target method not found");
            var targetParams = target.GetParameters().Select(p => p.Name).ToHashSet();

            foreach (var hook in patch.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name is "Prefix" or "Postfix"))
            {
                foreach (var p in hook.GetParameters())
                {
                    if (p.Name.StartsWith("___", StringComparison.Ordinal))
                        Assert.IsNotNull(AccessTools.Field(target.DeclaringType, p.Name.Substring(3)),
                            $"{patch.Name}: field {p.Name.Substring(3)} missing on {target.DeclaringType?.Name}");
                    else if (!p.Name.StartsWith("__", StringComparison.Ordinal))
                        Assert.IsTrue(targetParams.Contains(p.Name),
                            $"{patch.Name}: {target.DeclaringType?.Name}.{target.Name} has no parameter '{p.Name}'");
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HotCreatureTargets_AreOnPatchShieldsExclusionList()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // Each is asked for every agent, often on the TWParallel workers. Until plan 034 PatchShield's finalizer took
        // __originalMethod, whose per-call reflection lookup (rca-tournament-exit-hang-2026-07-06.md) cost about
        // 1,145 ns per call with 8 threads contending (measured by plan 034); the finalizer now takes only
        // __exception, and the exclusion stands as decided.
        foreach (var patch in new[]
                 {
                     typeof(Patch93_CreatureBanditPrimaryWieldGuard), typeof(Patch93_CreatureBanditOffhandWieldGuard),
                     typeof(Patch93_CreatureBanditMissileRangeGuard), typeof(Patch93_CreatureBanditNoRout),
                 })
        {
            var target = TargetOf(patch);
            Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
                $"{target.DeclaringType?.FullName}.{target.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
        }
    }

    // #742: the console is the one way to ask for a creature on the player's side, so it asks the rule before the team.
    [TestMethod]
    public void SpawnTroopsCommand_AsksTheCreatureSideRuleBeforeChoosingATeam()
    {
        var src = RepoPaths.ReadSource("Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs", stripComments: true);
        var rule = src.IndexOf("isPlayerSide = CreatureBanditRules.SpawnsOnPlayerSide(troopId, requestedPlayerSide)", StringComparison.Ordinal);
        var team = src.IndexOf("Mission.GetAgentTeam(origin, isPlayerSide)", StringComparison.Ordinal);
        Assert.IsTrue(rule >= 0, "spawn_troops must take its side from CreatureBanditRules.SpawnsOnPlayerSide");
        Assert.IsTrue(team > rule, "the side must be settled before the team is resolved");
    }
}
