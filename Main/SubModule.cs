using System.Linq;
using Bannerlord.UIExtenderEx;
using Bannerlord.UIExtenderEx.Attributes;
using HarmonyLib;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TAOM.Features;
using TAOM.Features.BannerInjection;
using TAOM.Features.HeroRace;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionMap;
using TAOM.Features.InitialChildGeneration;
using TAOM.Adapters;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.Execution.Hooks;
using TAOM.Features.Execution.Models;
using TAOM.Features.PrisonerRecruitment.Models;
using TAOM.Features.RaceAge;
using TAOM.Features.RaceAge.Models;
using TAOM.Features.StartupResources;
using TAOM.Features.NamedCompanions;
using TAOM.Features.TroopProgression;
using TAOM.Features.TroopWeight;
using TAOM.Features.TroopWeight.Diagnostics;
using TAOM.Features.TroopWeight.Hooks;
using TAOM.Features.AtmospherePersistence.Hooks;
using TAOM.Features.TroopProgression.Models;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.NavalTravel;
using TAOM.Features.NavalTravel.Models;
using TAOM.Features.NazgulFamily;
using TAOM.Features.NazgulFamily.Models;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TAOM.Features.Warg;
using TAOM.Features.Spider;
using TAOM.Features.BattleBalance;
using TAOM.Features.BattleBalance.Models;
using TAOM.Features.Arena.Models;
using TAOM.Features.Encyclopedia;
using TAOM.Features.Encyclopedia.Models;
using TAOM.Features.MainMenuCustomizer;
using TAOM.Features.NativeSkinFixes;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.Siege;
using TAOM.Features.Siege.Models;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.ArmyTargeting.Models;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.LocalizationOverride;
using TAOM.Features.LocalizationOverride.Hooks;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Hooks;
using TAOM.Features.CareerSystem;
using TAOM.Features.BannerBearers.Models;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.CombatMechanics.Models;
using TAOM.Features.SettlementGuards;
using TAOM.Features.SettlementGuards.Hooks;
using TAOM.Features.RevoltTuning;
using TAOM.Features.SettlementEconomy;
using TAOM.Features.SettlementEconomy.Models;
using TAOM.Features.SettlementFood;
using TAOM.Features.SettlementFood.Models;
using TAOM.Features.BanditManagement;
using TAOM.Features.BanditManagement.Models;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CastleRecruitment.Hooks;
using TAOM.Features.SiegeDismount.Hooks;
using TAOM.Features.MixedFormations.Hooks;
using TAOM.Features.SmartCavalryAI.Hooks;
using TAOM.Features.FiefManagement;
using TAOM.Features.FiefManagement.Hooks;
using TAOM.Features.SettlementNameplateFade;
using TAOM.Features.SettlementNameplateFade.Hooks;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using BehaviorTreeWrapper;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM;

public class SubModule : MBSubModuleBase
{
    private Harmony _harmony;
    private UIExtender? _uiExtender;
    private ITimeAccelerationService? _timeAccelerationService;
    private static float _shaderTickAccumulator;
    private static ShaderPrecompileRunner _shaderRunner;
    private static bool _missionTimePatchesApplied;
    private static bool _gameInitPatchesApplied;
    private static bool _basicTableauGuardApplied;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        IoC.Configure();

        // Issue #371: report both modules' build stamps and flag a mismatched pair. TAOM resolves
        // HarmonyLib and UIExtenderEx THROUGH TAOM.Dependencies, so a stale pairing breaks patch
        // application and renders every character in bind pose — a failure that previously left no
        // evidence anywhere, because both assemblies carried frozen versions on every build.
        try
        {
            IoC.Resolve<IModLogger>().LogInfo(
                Core.Diagnostics.BuildStampReport.BuildReport(
                    typeof(SubModule).Assembly,
                    typeof(TAOM.Dependencies.Foundation.PatchShield).Assembly));
        }
        catch { /* a version report must never be the thing that stops the mod loading */ }

        // Save-definer collision preflight. The engine instantiates every SaveableTypeDefiner in
        // every loaded assembly and registers each into a dictionary keyed by save id; a duplicate
        // throws with a message naming neither mod.
        //
        // TIMING IS THE WHOLE POINT, and it must be here, not later. Verified against installed
        // v1.4.7 `TaleWorlds.MountAndBlade.Module`: `Initialize()` calls `LoadSubModules(...)`
        // (line 267) — which loads every module's assemblies and only then fans out
        // `OnSubModuleLoad()` (line 1095) — and afterwards, at line 285, calls
        // `SaveManager.InitializeGlobalDefinitionContext()`, which is where the duplicate-key
        // throw happens. `OnBeforeInitialModuleScreenSetAsRoot` runs from `OnApplicationTick`
        // (line 509) LONG after that, so a preflight there would never execute on the one boot
        // where a collision actually exists. Being in OnSubModuleLoad also makes it a natural
        // once-per-process run; that hook fires on every return to the main menu, this does not.
        // By this point every module's assembly is loaded, so the scan sees the full mod set.
        try
        {
            Features.CoopInterop.SaveDefinerCollisionGuard.Run(
                IoC.Resolve<Features.CoopInterop.ISaveDefinerCollisionDetector>(),
                IoC.Resolve<IModLogger>());
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogWarning($"[SaveDefiners] preflight wiring failed: {ex.GetType().Name}: {ex.Message}");
        }

        // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
        // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
        // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
        // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
        // is the IoC.Configure() call itself — if THAT throws, the entire feature is
        // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
        // a manual DI container; accept and document the residual.
        _harmony = new Harmony("com.taom.mod");
        if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
        {
            try
            {
                _harmony.PatchCategory("Patch37_CrashReport");
                IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
                {
                    IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
                }
            }
            catch (System.Exception ex)
            {
                IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        _uiExtender = UIExtender.Create("TAOM");
        RegisterUiExtensions(_uiExtender);
        _uiExtender.Enable();

        // Patch41_McmLayoutFix — flip MCM's embedded options-screen prefabs from VerticalBottomToTop
        // to VerticalTopToBottom (v1.4.0 layout regression). MCM's prefabs are embedded in
        // Bannerlord.MBOptionScreen and load via WidgetFactoryManager.CreateAndRegister, which bypasses
        // UIExtenderEx's [PrefabExtension] hook — so this is a Harmony Postfix, not a PrefabExtension.
        // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
        // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
        // must already be attached when MCM calls CreateAndRegister.
        _harmony.PatchCategory("Patch41_McmLayoutFix");

        _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();

        // Must be first — intercepts GetLocalizedText before any game texts are resolved.
        // Loads English string overrides from taom_module_strings.xml (removes hardcoded "The" articles).
        _harmony.PatchCategory("Patch25_LocalizationOverride");
        var pathService0 = IoC.Resolve<IPathService>();
        var logger0 = IoC.Resolve<IModLogger>();
        var xmlPath = System.IO.Path.Combine(pathService0.ModuleDataPath, "taom_module_strings.xml");
        try
        {
            var overrides = LocalizationOverrideLoader.ParseOverridesFromFile(xmlPath);
            foreach (var kvp in overrides)
                MBTextManager_GetLocalizedText_Patch.RegisterOverride(kvp.Key, kvp.Value);
            logger0.LogInfo($"[LocalizationOverride] Registered {overrides.Count} English string overrides");
        }
        catch (System.Exception ex)
        {
            logger0.LogError($"[LocalizationOverride] Failed to load overrides: {ex.Message}");
        }

        _harmony.PatchCategory("Patch18_CulturalFeats");
        _harmony.PatchCategory("Patch19_CustomBattles");

        // Patch58_SkipCampaignIntro — Prefix on SandBoxGameManager.OnLoadFinished that skips the vanilla
        // SandBox campaign intro video on a NEW game (mirrors the engine's own IsDevelopmentMode no-video
        // bypass), dropping straight into character creation; save-loads run vanilla untouched. Applied here
        // in OnSubModuleLoad (process-static one-shot) — NOT the late OnGameInitializationFinished batch —
        // because the target fires during the new-game load sequence (after campaign init but before
        // character creation), so the patch must already be attached before any new game can start. Any
        // binding failure inside the prefix falls back to the vanilla video. See docs/features/skip-campaign-intro.md.
        Features.SkipCampaignIntro.Hooks.Patch58_SkipCampaignIntro.Initialize(IoC.Resolve<IModLogger>());
        _harmony.PatchCategory("Patch58_SkipCampaignIntro");

        // Patch61_SaveLoadDiagnostics — always-on [SaveLoad] lifecycle logging for the "corrupted
        // save" investigation. The engine swallows the real exception behind the generic
        // "A problem occured while trying to load the saved game." dialog (LoadContext.Load catches
        // and prints only ex.Message), so interior Finalizers stamp the actual failing type/SaveId
        // to taom_debug, and save-side hooks catch bad WRITES (the #292 class) at write time.
        // All Finalizers are VOID (true rethrow, stack preserved, structurally can't swallow) at
        // Priority.First — SaveShield (TAOM.Dependencies) finalizes 4 overlapping methods at
        // default priority and SWALLOWS; ours must observe the exception first (review 2026-07-07
        // HIGH). Applied here in OnSubModuleLoad like Patch58: loads are triggered from the main
        // menu, before any game init — the late batch would miss the first load. Each
        // reflection-target hook (internal engine types) gets its OWN category: Harmony aborts a
        // category on the first failing class, so per-hook categories keep one drifted internal
        // type from killing its siblings. Diagnostics must never break startup: every category in
        // its own try/catch, fail = vanilla.
        try
        {
            var saveLoadDiagnostics = IoC.Resolve<Features.SaveLoadDiagnostics.ISaveLoadDiagnosticsService>();
            var saveLoadLogger = IoC.Resolve<IModLogger>();
            Features.SaveLoadDiagnostics.Hooks.SandBoxSaveHelper_TryLoadSave_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.MBSaveLoad_LoadSaveGameData_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.SaveManager_Load_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.LoadContext_CreateLoadData_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.ObjectHeaderLoadData_CreateObject_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.ContainerHeaderLoadData_GetObjectTypeDefinition_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.HeaderLoadData_Readers_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.LoadResult_InitializeCallbacks_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.CampaignBehaviorManager_LoadBehaviorData_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.SaveManager_Save_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.FileDriver_Save_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.SaveOutput_PrintStatus_Patch.Initialize(saveLoadDiagnostics);
            Features.SaveLoadDiagnostics.Hooks.ContainerLoadData_Fill_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
            Features.SaveLoadDiagnostics.Hooks.CampaignBehaviorDataStore_LoadBehaviorData_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
            Features.SaveLoadDiagnostics.Hooks.ArchiveDeserializer_LoadFrom_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
            _harmony.PatchCategory("Patch61_SaveLoadDiagnostics");
            foreach (var category in new[]
            {
                "Patch61_SaveLoadDiagnostics_ContainerFill",
                "Patch61_SaveLoadDiagnostics_BehaviorData",
                "Patch61_SaveLoadDiagnostics_ArchiveParse",
            })
            {
                try
                {
                    _harmony.PatchCategory(category);
                }
                catch (System.Exception ex)
                {
                    saveLoadLogger.LogWarning($"[SaveLoad] {category} not applied (engine drift?): {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogError($"[SaveLoad] init failed — save/load diagnostics inactive: {ex.GetType().Name}: {ex.Message}");
        }

        // Patch62 — containment guard (#339): a heap-corruption AccessViolationException inside
        // the Tournament movie's WidgetTemplate release walk CTD'd a player session (fired in
        // Patch60's early release AND again uncaught at the pop-time re-walk of the leaked
        // movie). The finalizer suppresses AV-only on GauntletMovie.Release, converting the
        // crash into one logged leaked movie; suppression on the first (Patch60) attempt also
        // removes the movie from the layer, so the fatal re-walk never happens. Applied here in
        // OnSubModuleLoad (Patch58/Patch61 precedent), NOT the late OnGameInitializationFinished
        // batch: GauntletMovie.Release runs for EVERY movie in the process — main menu, character
        // creation, load screen — all before any game init (the #299 apply-timing lesson).
        try
        {
            Features.Arena.Hooks.GauntletMovie_Release_AvGuard_Patch.Initialize(IoC.Resolve<IModLogger>());
            _harmony.PatchCategory("Patch62_MovieReleaseAvGuard");
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogWarning($"[Arena] Patch62 movie-release AV guard failed to apply: {ex.Message}");
        }

        // Patch0_BattleScenes: loads TAOM's sp_battle_scenes.xml (full 0-255 map_indices coverage) so the
        // TAOM_Map Main_map grid's extended indices (158-255) resolve to real battle terrains instead of
        // FailedAsserting against vanilla's 1-157 table. Re-enabled 2026-06-01 (TAOM_Map ships Main_map +
        // the extended XML exists; 3 patch targets verified against installed 1.4.5). In-game grid validation
        // pending the worldmap_battle_scene_grid re-author. See docs/reference/worldmap-battle-scene-grid.md.
        _harmony.PatchCategory("Patch0_BattleScenes");
        // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first

        var pathService = IoC.Resolve<IPathService>();
        var logger = IoC.Resolve<IModLogger>();
        FactionMapPaths.Initialize(pathService.ModuleRootPath, logger);

        var allianceHook = IoC.Resolve<IOnAllianceAction>();
        var peaceHook = IoC.Resolve<IOnPeaceAction>();
        DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
        AllianceCampaignBehavior_EndAlliance_Patch.Initialize(logger);
        AllianceCampaignBehavior_StartAlliance_Patch.Initialize(logger);
        AllianceCampaignBehavior_AddAllianceDecision_Patch.Initialize(logger);
        DeclareWarAction_ApplyInternal_Patch.Initialize(logger);
        MakePeaceAction_ApplyInternal_Patch.Initialize(logger);

        var executionHook = IoC.Resolve<IOnExecutionAction>();
        ExecutionIoC.InitializeHooks(executionHook);

        // Only the shed-on-upgrade hook survives the 2026-07-11 count->limit rework; the weight penalty
        // itself is applied by TaomPartySizeModel (registered in CreateGameModels), not a Harmony patch.
        TroopWeightIoC.InitializeHooks(IoC.Resolve<IOnPartyUpgraderUpgradeReadyTroops>());

        CustomBattlesIoC.InitializeHooks(
            IoC.Resolve<IOnGetCustomBattleCommanders>(),
            IoC.Resolve<IOnGetCustomBattleFactions>(),
            IoC.Resolve<IOnGetDefaultTroopOfFormation>(),
            IoC.Resolve<ISideCommanderFilter>(),
            logger);

        _harmony.PatchCategory("Patch21_ShaderPrecompilation");
        _shaderRunner = IoC.Resolve<ShaderPrecompileRunner>();
        ShaderPrecompilationIoC.InitializeHooks(logger, _shaderRunner);

        _harmony.PatchCategory("Patch22_ArmyTargeting");
        // Patch49: Finalizer guarding vanilla Army.FindBestGatheringSettlementAndMoveTheLeader,
        // which NREs (Army.cs:726 settlement.GatePosition / 659 Kingdom.Settlements, v1.4.6) when a
        // besieger army can't resolve a gathering fortification — a map-tick CTD on siege start.
        // No TAOM patch is on the stack; aggressive Patch22 targeting just makes it more reachable.
        // Crash report 2026-06-17. See the patch's doc-comment.
        _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
        // Patch59: CaravanTrade — four postfixes on CaravansCampaignBehavior private methods
        // (war gate, destination re-weight, range envelope, budget-factor floor) so AI/player caravans
        // range past the local town cluster instead of shuttling. Campaign-behavior target, so applied
        // in this campaign-phase block alongside the other AI patches.
        _harmony.PatchCategory("Patch59_CaravanTrade");
        // Patch68: EconomyDiagnostics — read-only town-gold telemetry. One prefix/postfix recorder on
        // SettlementComponent.ChangeGold (the pool's sole mutator, so no flow site can be missed)
        // plus four flow-tag pairs naming the caller. Answers "where does a town's daily mint go",
        // which no engine code logs. Campaign-behavior + action targets, so this block.
        // Guarded like Patch60/61/62/63: this is DIAGNOSTIC-ONLY, and an unguarded throw here would
        // abort the rest of this block — taking Patch30_MixedFormations and everything after it down
        // with it. A read-only instrument must never be able to disable gameplay patches.
        try
        {
            _harmony.PatchCategory("Patch68_EconomyDiagnostics");
        }
        catch (System.Exception ex)
        {
            logger.LogWarning($"[EconomyDiagnostics] Patch68 failed to apply: {ex.Message}");
        }
        _harmony.PatchCategory("Patch30_MixedFormations");
        // Patch63 — guarded reimplementation of BannerBearerLogic.SpawnBannerBearer (issue #360):
        // the engine's reinforcement bearer spawn reads the new agent's ExtraWeaponSlot native
        // entity with no check and AVs when the banner never made it into the slot (validating
        // spawn gate / native wield-time drop). Guarded like Patch60/61/62 — a containment guard
        // must never take startup down; unresolved bindings log + fall open to vanilla.
        try
        {
            Features.BannerBearers.Hooks.BannerBearerLogic_SpawnBannerBearer_Patch.Initialize(
                IoC.Resolve<Features.BannerBearers.IBannerBearerService>(), logger);
            _harmony.PatchCategory("Patch63_BannerBearerSpawnGuard");
        }
        catch (System.Exception ex)
        {
            logger.LogWarning($"[BannerBearers] Patch63 spawn guard failed to apply: {ex.Message}");
        }
        // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
        // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in
        // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
        // which is null during OnSubModuleLoad and would crash JIT prep with NRE.

        var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
        var bannerColorService = IoC.Resolve<IBannerColorService>();
        var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();

        Banner_TryGetBannerDataFromCode_Transpiler.Initialize(bannerColorConfig, logger);
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(bannerColorService);
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(logger);
        Clan_UpdateBannerColor_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        Banner_GetFirstIconColor_Patch.Initialize(bannerColorService);
        BannerEditorView_OnTick_Patch.Initialize(bannerColorService, logger);
        CampaignUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        SandBoxUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        PartyVM_RefreshCurrentCharacterInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        HeroViewModel_FillFrom_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        PartyCharacterVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        ClanPartyItemVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Initialize(bannerColorService);
        var agentColorStore = IoC.Resolve<IAgentColorStore>();
        Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
        Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
        AgentVisuals_Create_Patch.Initialize(bannerColorService);
        MapConversationTableau_SpawnOpponentLeader_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        MapConversationTableau_SpawnOpponentBodyguard_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter);
        OrderOfBattleHeroItemVM_RefreshInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);

        Mission_Initialize_Patch.Initialize(logger);

        // Patch42_CastleRecruitment — castle notable recruitment. Targets RecruitmentCampaignBehavior
        // + AiVisitSettlementBehavior (both in TaleWorlds.CampaignSystem, no View/Mission.cctor
        // dependency, safe in OnSubModuleLoad). The transpilers swap the AI IsCastle gate to a runtime
        // toggle; the postfix invokes the private CheckRecruiting for castles. All fail-safe.
        var castleRecruitmentSettings = IoC.Resolve<ICastleRecruitmentSettingsProvider>();
        CastleAiToggle.Initialize(castleRecruitmentSettings);
        Patch42_AiHourlyTick_Transpiler.Initialize(logger);
        Patch42_FillSettlements_Transpiler.Initialize(logger);
        Patch42_HourlyTickParty_Postfix.Initialize(castleRecruitmentSettings, logger);
        _harmony.PatchCategory("Patch42_CastleRecruitment");

        InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();
        IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();

        // Patch55_BasicTableauRaceGuard — MUST be applied HERE, not in OnGameInitializationFinished.
        // The Save/Load hero preview (BasicCharacterTableau) renders on the COLD main menu, before any
        // game-init callback fires. The sibling CharacterTableau patches live in Patch2_RefreshTableau,
        // applied in OnGameInitializationFinished (campaign init) — too late to guard the save-list CTD
        // (Codex C1, issue #299). By here, IoC.Configure() (OnSubModuleLoad) has already set the guard,
        // and the initial module screen has not been pushed yet, so the prefix attaches before the save
        // list can render. Process-static one-shot; fail-open (a missing guard is no worse than vanilla).
        if (!_basicTableauGuardApplied)
        {
            _basicTableauGuardApplied = true;
            try
            {
                _harmony.PatchCategory("Patch55_BasicTableauRaceGuard");
            }
            catch (System.Exception ex)
            {
                IoC.Resolve<IModLogger>().LogError($"[HeroRace] Patch55_BasicTableauRaceGuard apply failed: {ex.GetType().Name}: {ex.Message}");
            }
        }


        // BattleLoadDiagnostics collection: a battle/scene load that hung last session left
        // an inflight marker (phase-4 wrote it; phase-6/end never ran to clear it). If it
        // survived to this main menu, the previous load never finished — surface a notice so
        // the player knows to send the diagnostic log. See docs/features/battle-load-diagnostics.md.
        try
        {
            var stallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
            var stale = stallMarker?.TryConsumeStaleMarker();
            if (stale != null)
                Features.BattleLoadDiagnostics.StallReportNotifier.Notify(stale);
        }
        catch { /* never block the main menu over a diagnostic */ }

        // Session-wide memory telemetry (#386): periodic [MemSample] lines + low-commit-headroom
        // WARN so a native OOM CTD self-identifies from the log tail (#385 was only diagnosable by
        // parsing the 1.3 GB dump). Gating lives inside Poll on its OWN MCM toggle — the master
        // battle-load toggle must not silently kill session-wide crash forensics.
        //
        // Started HERE, not in OnGameInitializationFinished: that hook only runs once a game is
        // loading, so no [MemSample] line was EVER written at the main menu — and the A-vs-B menu
        // delta is what docs/investigations/native-commit-audit-2026-08.md calls the measurement
        // that most tightens attribution. taom.print_memory cannot fill that gap either, because its
        // RunAnywhere gate returns "Campaign was not started." before a game loads.
        //
        // Safe this early: MemorySampleReader is pure kernel32/psapi P/Invoke with no engine state,
        // the settings provider fails open when MCM has not registered yet, and IoC + FileLogger are
        // both live by here (the block above uses them). Start() is idempotent, which matters
        // because this hook fires on EVERY return to the main menu — pinned by
        // MemoryPressureSamplerTests.Start_CalledTwice_ReusesTheSameTimer.
        try { IoC.Resolve<Features.BattleLoadDiagnostics.MemoryPressureSampler>().Start(); }
        catch { /* never block the main menu over a diagnostic */ }

        // DevConsole discovery audit: ask the engine whether it actually registered TAOM's taom.*
        // console commands. CollectCommandLineFunctions is invoked from TaleWorlds.Native.dll, so its
        // timing relative to our assembly load is not knowable offline — but HasFunctionForCommand is
        // public, so we can just ask. Queries a vanilla control command too, which is what makes a
        // negative reading distinguishable between "too early" and "our command was dropped".
        // Goes quiet once the answer is conclusive. See docs/features/dev-console.md.
        try
        {
            Features.DevConsole.DevConsoleDiscoveryAudit.Run(IoC.Resolve<IModLogger>());
        }
        catch { /* never block the main menu over a diagnostic */ }

        // NativeSkinFixes — three native MinHook detours that fix engine bugs
        // TaleWorlds won't: covers_head morph freeze, hair cloth orphan, beard
        // cloth orphan. Loads TAOM.NativeSkinFixes.dll from Main/_Module/bin
        // and pattern-scans TaleWorlds.Native.dll for the hook targets at
        // install time. See docs/features/native-skin-fixes.md.
        //
        // PARKED 2026-07-08 (user decision) — DISABLED at the wiring level. The
        // install call below is commented out so the native hooks NEVER load,
        // regardless of any persisted MCM "Native Skin Fixes → Enable Native
        // Skin Fixes" value (MCM persists a saved value over the compiled
        // default, so flipping the default alone would not stop machines that
        // already saved it ON). Engine rendering is vanilla for everyone.
        // RE-ENABLE: uncomment the install branch below AND flip the MCM default
        // (TaomSettings.EnableNativeSkinFixes) back to true.
        // (No log line — a parked feature announcing itself every session is pure noise; the
        // commented-out install branch below is the record.)
        // bool nsfEnabled = false;
        // try { nsfEnabled = TaomSettings.Instance?.EnableNativeSkinFixes == true; }
        // catch { /* MCM not ready — fail closed */ }
        // if (nsfEnabled)
        //     NativeSkinFixesInstaller.Install(IoC.Resolve<IModLogger>());
        // else
        //     IoC.Resolve<IModLogger>().LogInfo(
        //         "[NativeSkinFixes] disabled (MCM 'Enable Native Skin Fixes' is off) — engine rendering is vanilla");

        // Pre-compile Shaders — RE-ENABLED 2026-06-17 (issue #287). Walks the all-characters battle
        // (character/equipment shaders) then each TAOM battle scene (terrain + forced-atmosphere
        // shaders — the d3dcompiler battle-load CTD class). Drives ShaderPrecompileRunner; progress
        // shows on the loading screen + a 1 Hz status toast. See docs/features/shader-precompilation.md.
        if (Module.CurrentModule.GetInitialStateOptionWithId("TaomPrecompileShaders") == null)
        {
            Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
                id:                  "TaomPrecompileShaders",
                name:                new TextObject("{=taom_precompile_shaders}Pre-compile Shaders"),
                orderIndex:          100,
                action:              () => InformationManager.ShowInquiry(new InquiryData(
                    "Shader Pre-compilation",
                    "Loads a battle with all TAOM troops, then walks each TAOM battle scene, to " +
                    "pre-compile every shader the game would otherwise compile mid-battle.\n\n" +
                    "THIS TAKES A LONG TIME (1-2 hours+). Leave it running — progress shows on the " +
                    "loading screen and as a status line. One-time process; it eliminates in-game " +
                    "stutter and the intermittent battle-load crash/hang.\n\n" +
                    "When you see 'Shader pre-compilation COMPLETE', you can play.",
                    true, true, "Start", "Cancel",
                    () =>
                    {
                        _shaderTickAccumulator = 0f;
                        _shaderRunner?.Begin();
                    },
                    () => InformationManager.HideInquiry())),
                isDisabledAndReason: () => (false, new TextObject("")),
                enabledHint:         new TextObject("{=taom_precompile_hint}Pre-compiles shaders to eliminate in-game stutter + the battle-load crash. Run once after installing TAOM."),
                // Hidden live when the MCM master toggle is off (no relaunch needed). Defaults to shown
                // if settings aren't resolvable yet. The "Include Scene Passes" toggle is read inside Begin().
                isHidden:            () => !(Features.TaomSettings.Instance?.EnableShaderPrecompilation ?? true)));
        }
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        // Session-level diagnostic snapshot: OS / CLR / mod list / mod-stack
        // assembly versions / campaign context. Runs once per session and is
        // idempotent so OnGameStart on save-load doesn't spam.
        try
        {
            IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>()?.LogSessionSnapshot();
        }
        catch { /* diagnostic is best-effort, never break OnGameStart */ }

        if (gameStarterObject is CampaignGameStarter campaignStarter)
        {
            // Registration order is preserved exactly from the pre-extraction inline block:
            // vanilla-behavior removals precede their TAOM replacements, and the LotrIssues suppression
            // must run inside this OnGameStart (after Sandbox registered its behaviors) — so the
            // groups below are invoked in the original statement order.
            // Phase 9b #173 — careerPassives resolved once for the whole CulturalFeats + CareerSystem
            // + TroopProgression model registration block. Replaces all CareerPassiveHelper static
            // calls with instance-injected ICareerPassiveService.
            var careerPassives = IoC.Resolve<TAOM.Features.CareerSystem.ICareerPassiveService>();
            // Hoisted: TaomVolunteerModel consumes ICulturalFeatsService for the village
            // volunteer-respawn-rate feats (Dunland/Gundabad/Dol Guldur/Mordor); the cultural-feat
            // model group reuses this same reference.
            var culturalFeats = IoC.Resolve<TAOM.Features.CulturalFeats.ICulturalFeatsService>();

            RegisterProgressionAndIdentity(campaignStarter, careerPassives, culturalFeats);
            RegisterRaceAgeAndFamily(campaignStarter);
            RegisterDiplomacyAndConflict(campaignStarter);
            RegisterCulturalFeatModels(campaignStarter, culturalFeats, careerPassives);
            RegisterBattleBalanceAndTargeting(campaignStarter);
            RegisterSpecialResourcesAndCareers(campaignStarter, careerPassives);
            RegisterCampaignLifeBehaviors(campaignStarter);
        }
    }

    /// <summary>
    /// #370 — registers TAOM's UIExtenderEx extensions, minus any marked
    /// <c>[CoopSuppressedUi]</c> when a co-op module is active.
    ///
    /// Mirrors UIExtenderEx's own <c>Register(Assembly)</c> type selection (any type carrying an
    /// attribute derived from <c>BaseUIExtenderAttribute</c>), then filters. Registration is a
    /// one-shot here in <c>OnSubModuleLoad</c>, so this is the only point at which a widget can be
    /// kept out of the prefab — a runtime check inside a mixin cannot un-inject one.
    ///
    /// Never throws: a failure here would cost every TAOM UI extension, so the fallback is the
    /// unfiltered assembly registration (solo behaviour).
    /// </summary>
    private static void RegisterUiExtensions(UIExtender extender)
    {
        var assembly = typeof(SubModule).Assembly;

        // SOLO TAKES THE ORIGINAL PATH, UNTOUCHED. Not an optimisation — a guarantee. Our own type
        // collection could in principle select a different set than UIExtenderEx's (its per-type
        // attribute read is tolerant where UIExtenderEx's is not), and a silently short list would
        // break TAOM's UI with no visible cause. Solo players are the overwhelming majority and must
        // not be exposed to that risk to serve a co-op path they never load, so the filtered path is
        // reached ONLY when a co-op module is present.
        bool coopActive;
        try
        {
            // Reading the flag here is SAFE, and that is verified rather than assumed.
            //
            // This is the one CoopPresence consumer that cannot self-correct: UI registration is a
            // one-shot, and a mixin cannot un-inject a widget that is already built. Everything else
            // (PatchShield, SaveShield, the diplomacy gates) reads live at gameplay time. So the
            // worry was that this decided from a probe taken before the launcher had published its
            // module list — which would silently take the solo branch and log nothing.
            //
            // Decompiled v1.4.7 settles it (two independent Codex passes, 2026-08-01):
            // Module.Initialize populates ModuleHelper's _loadedModules from the native module-code
            // string BEFORE calling LoadSubModules, which is what invokes OnSubModuleLoad. The list
            // is therefore complete here — even a SubModule constructor already sees it. The
            // "may not be populated this early" caution in CoopPresence.Refresh's docs is about the
            // pre-managed native string, not an OnSubModuleLoad race, and no extra Refresh() helps
            // with that. Removed the redundant re-probe accordingly.
            coopActive = CoopPresence.IsActive;
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogWarning(
                $"[CoopInterop] co-op probe failed ({ex.GetType().Name}: {ex.Message}) — " +
                "registering UI unfiltered (solo behaviour)");
            extender.Register(assembly);
            return;
        }

        if (!coopActive)
        {
            // Logged unconditionally: this line's ABSENCE is the only way to tell a genuine solo
            // session from a co-op session whose detection ran too late, and the boot matrix has to
            // be able to tell those apart.
            IoC.Resolve<IModLogger>().LogInfo(
                "[CoopInterop] no co-op module detected at UI registration — registering all TAOM UI");
            extender.Register(assembly);
            return;
        }

        try
        {
            var candidates = CoopUiRegistrationPolicy.CollectUiExtensionTypes(assembly);
            var registered = CoopUiRegistrationPolicy.Filter(candidates, coopActive: true);
            var dropped = CoopUiRegistrationPolicy.Suppressed(candidates);

            IoC.Resolve<IModLogger>().LogInfo(
                $"[CoopInterop] co-op active — registering {registered.Count} UI extension(s), " +
                $"suppressing {dropped.Count}" +
                (dropped.Count > 0 ? ": " + string.Join(", ", dropped.Select(t => t.Name)) : ""));

            extender.Register(registered);
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogWarning(
                $"[CoopInterop] UI extension filtering failed ({ex.GetType().Name}: {ex.Message}) — " +
                "registering the full assembly unfiltered");
            extender.Register(assembly);
        }
    }

    // Character identity, creation, and troop-progression registrations (ADR-002 extraction of the
    // former OnGameStart inline block — bodies are verbatim, order unchanged).
    private static void RegisterProgressionAndIdentity(
        CampaignGameStarter campaignStarter,
        ICareerPassiveService careerPassives,
        TAOM.Features.CulturalFeats.ICulturalFeatsService culturalFeats)
    {
        var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
        campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));

        var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
        var bannerExclusionService = IoC.Resolve<IBannerExclusionService>();
        campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService, bannerExclusionService));

        var ccContentService = IoC.Resolve<ICharacterCreationContentService>();
        var ccLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));

        // Re-applies the character-creation package when a multiplayer join swaps the controlled
        // hero out from under it. Inert in single-player. Field report 2026-08-03 §1 + §7.
        campaignStarter.AddBehavior(new TAOM.Features.PlayerPossession.PlayerPossessionBehavior(
            IoC.Resolve<TAOM.Features.PlayerPossession.IPlayerPossessionService>(),
            IoC.Resolve<TAOM.Features.PlayerPossession.IJoinReconciliationService>(),
            IoC.Resolve<ICareerMenuService>(),
            ccLogger));

        campaignStarter.RemoveBehaviors<InitialChildGenerationCampaignBehavior>();
        var childGenService = IoC.Resolve<IInitialChildGenerationService>();
        campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));

        var costService = IoC.Resolve<ITroopCostService>();
        // Phase 9b #180 / partial #148 — IWageModifierService extraction. Hoists garrison-wage
        // feat loop + Mordor/Gundabad/Umbar party-wage feats + Rohan mounted-wage scaling +
        // recruitment-cost feats out of the model body, satisfying gamemodels.md rule 4.
        var wageModifiers = IoC.Resolve<IWageModifierService>();
        var volunteerService = IoC.Resolve<IVolunteerTierService>();
        var recruitmentService = IoC.Resolve<IVolunteerRecruitmentService>();
        var volunteerContextAdapter = IoC.Resolve<IVolunteerContextAdapter>();
        var recruitmentAlignment = IoC.Resolve<TAOM.Features.AlignmentRecruitment.IRecruitmentAlignmentService>();
        campaignStarter.AddModel(new TaomCharacterStatsModel(careerPassives));
        campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
        campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));

        // Prisoner-recruitment morale waiver: no morale lost recruiting a prisoner of your own
        // faction or alignment side (Isengard taking on Mordor/Gundabad/Dunland troops). Vanilla
        // charges -1/-2 regardless. Registering here (OnGameStart) puts TAOM after SandBox's
        // DefaultPrisonerRecruitmentCalculationModel in the backwards model scan, so ours resolves.
        campaignStarter.AddModel(new TaomPrisonerRecruitmentCalculationModel(
            IoC.Resolve<TAOM.Features.PrisonerRecruitment.IPrisonerRecruitmentMoraleService>()));

        // NavalTravel — PARKED 2026-06-26: TAOM_Map's navmesh isn't set up to take advantage of naval
        // travel (no naval region navmesh → AI can't route at sea; #296/#120), so the feature is disabled
        // at the wiring level — registering nothing keeps vanilla DefaultPartyNavigationModel + vanilla
        // navmesh regardless of any persisted MCM toggle. All code/tests/fixes are preserved for re-enable.
        // RE-ENABLE: uncomment this model registration + the Patch54/Patch57 blocks in
        // OnGameInitializationFinished, and flip the `enabled` defaults back to true.
        // campaignStarter.AddModel(new TaomPartyNavigationModel(IoC.Resolve<INavalTravelService>(), IoC.Resolve<IModLogger>()));
    }

    // Race-appropriate aging/pregnancy/hero-creation + the Ringwraith family block.
    private static void RegisterRaceAgeAndFamily(CampaignGameStarter campaignStarter)
    {
        var raceAgeService = IoC.Resolve<IRaceAgeService>();
        var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
        var raceAgeLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger,
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>()));
        campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
        campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
        campaignStarter.AddModel(new TaomHeroCreationModel());

        // Ringwraiths (Witch-King + Nazgûl) take no spouse/parents/children: block their marriage
        // (so no spouse ⇒ no children) + a defensive clear-on-load for pre-feature saves.
        var nazgulRegistry = IoC.Resolve<INazgulRegistry>();
        campaignStarter.AddModel(new TaomMarriageModel(nazgulRegistry));
        campaignStarter.AddBehavior(new NazgulFamilyBehavior(nazgulRegistry, IoC.Resolve<IModLogger>()));
    }

    // Diplomacy / War of the Ring / siege defense / execution-relation registrations.
    private static void RegisterDiplomacyAndConflict(CampaignGameStarter campaignStarter)
    {
        var diplomacyService = IoC.Resolve<IDiplomacyService>();
        var wotrService = IoC.Resolve<IWarOfTheRingService>();
        var diplomacyLogger = IoC.Resolve<IModLogger>();
        // ShouldDeferToHost, not raw presence: gating shared-world decisions on "a co-op module is
        // installed" disabled TAOM's diplomacy for a solo player who merely had it enabled, and for
        // the co-op host itself. See ICoopSessionProvider.
        var coopSession = IoC.Resolve<TAOM.Features.CoopInterop.ICoopSessionProvider>();
        campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger, coopSession));
        campaignStarter.AddBehavior(new PlayerAllianceProposalBehavior(diplomacyService, diplomacyLogger));
        campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
        campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService, coopSession));
        campaignStarter.AddModel(new TaomDiplomacyModel(wotrService, coopSession));

        var wotrLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger,
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>()));
        // WotR Momentum #327 — Evil-vs-Good progress tracking + victory; behavior is a
        // Reuse.Singleton (it carries the state store's persistence dict).
        campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());

        var siegeDefenseService = IoC.Resolve<ISiegeDefenseService>();
        var siegeDefenseLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger,
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>()));
        campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));

        var executionRelationService = IoC.Resolve<IExecutionRelationService>();
        var playerContext = IoC.Resolve<IPlayerContextAdapter>();
        campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
    }

    // Cultural feat models — Phase 9b #144/#176: dispatch logic extracted to
    // ICulturalFeatsService. Each model is a thin boundary that converts
    // CultureObject → ICultureFeatAdapter and delegates (gamemodels.md rule 4).
    // `culturalFeats` is passed in (hoisted resolve, shared with TaomVolunteerModel).
    private static void RegisterCulturalFeatModels(
        CampaignGameStarter campaignStarter,
        TAOM.Features.CulturalFeats.ICulturalFeatsService culturalFeats,
        ICareerPassiveService careerPassives)
    {
        campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
        campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
        campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
        campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
        campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
        campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
        campaignStarter.AddModel(new TaomCaravanModel(culturalFeats, IoC.Resolve<TAOM.Features.CaravanTrade.ICaravanTradeService>()));
        campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
        campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
        campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
        campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives, IoC.Resolve<ITroopWeightService>(), IoC.Resolve<IModLogger>()));
        campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
        campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
        campaignStarter.AddModel(new TaomSettlementFoodModel(IoC.Resolve<ISettlementFoodService>(), IoC.Resolve<ISettlementFoodConfigProvider>()));
        campaignStarter.AddModel(new TaomSettlementEconomyModel(IoC.Resolve<ISettlementEconomyService>(), IoC.Resolve<ISettlementEconomyConfigProvider>()));
        campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
        campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
        campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
        campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
        campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));
        campaignStarter.AddModel(new TaomNotableSpawnModel(culturalFeats));
    }

    // Battle-balance / encyclopedia-visibility / army-targeting model registrations.
    private static void RegisterBattleBalanceAndTargeting(CampaignGameStarter campaignStarter)
    {
        var battleBalanceSettings = IoC.Resolve<IBattleBalanceSettingsProvider>();
        var battleBalanceConfig = IoC.Resolve<IBattleBalanceConfigProvider>();
        campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
        campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
        campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig, IoC.Resolve<ICareerPassiveService>()));

        campaignStarter.AddModel(new TaomInformationRestrictionModel(IoC.Resolve<IEncyclopediaSettingsProvider>()));

        var armyTargetingService = IoC.Resolve<IArmyTargetingService>();
        campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));
    }

    // Special-resource economy + the career system (behaviors, quests, and career GameModels).
    private static void RegisterSpecialResourcesAndCareers(
        CampaignGameStarter campaignStarter,
        ICareerPassiveService careerPassives)
    {
        var specialResourceService = IoC.Resolve<ISpecialResourceService>();
        var specialResourceStorage = IoC.Resolve<ISpecialResourceStorageService>();
        var specialResourceConfig = IoC.Resolve<ISpecialResourceConfigProvider>();
        var specialResourceLogger = IoC.Resolve<IModLogger>();
        var specialResourceBehavior = new SpecialResourcesBehavior(
            specialResourceService, specialResourceStorage, specialResourceConfig, specialResourceLogger,
            IoC.Resolve<ITroopWeightService>(),
            IoC.Resolve<TAOM.Features.CoopInterop.IDedicatedServerProvider>());
        campaignStarter.AddBehavior(specialResourceBehavior);
        PartyScreenLogic_AddCommand_Patch.SetBehavior(specialResourceBehavior);

        // TEMPORARY: troop-count diagnostic (special-currency undercount investigation). Dumps the
        // main party's raw + weighted counts to the log on party-screen open. Remove with the behavior.
        campaignStarter.AddBehavior(IoC.Resolve<TroopCountDiagnosticsBehavior>());

        var careerDataService = IoC.Resolve<ICareerDataService>();
        var careerRegistry = IoC.Resolve<ICareerRegistry>();
        var careerPassiveService = IoC.Resolve<ICareerPassiveService>();
        var careerLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));
        var careerCreationHandler = IoC.Resolve<ICareerCreationHandler>();
        var careerAbilityServiceForBehavior = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
        campaignStarter.AddBehavior(new CareerCampaignBehavior(
            careerDataService, careerRegistry, careerPassiveService, careerCreationHandler, careerAbilityServiceForBehavior, careerLogger));

        var careerAdapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
        // CareerSwitchDialogueBehavior used to take ICareerSwitchService too; that dependency
        // moved to GauntletCareerScreen.OnChooseSwitchTarget (Codex Review #32 cleanup).
        campaignStarter.AddBehavior(new CareerSwitchDialogueBehavior(
            careerDataService, careerRegistry, careerAdapterFactory, careerLogger));

        // Career-tied quest system (Phase 6) — offers/starts tier quests; CareerQuest : QuestBase
        // is registered for saving by the auto-discovered CareerQuestSaveableTypeDefiner.
        var careerQuestService = IoC.Resolve<Features.CareerSystem.ICareerQuestService>();
        campaignStarter.AddBehavior(new Features.CareerSystem.Quests.CareerQuestCampaignBehavior(
            careerDataService, careerQuestService, careerLogger));

        // Career system GameModels — reuse the hoisted careerPassives resolve.
        // Phase 9b #142 — agent-stat extraction: TaomAgentStatCalculateModel /
        // TaomAgentApplyDamageModel now delegate UpdateAgentStats + damage-amp/red +
        // shrug-off logic to ICareerAgentStatService (gamemodels.md rule 4).
        var careerAgentStat = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>();
        campaignStarter.AddModel(new TaomMapVisibilityModel(careerPassives));
        campaignStarter.AddModel(new TaomInventoryCapacityModel(careerPassives));
        var elephantAttackService = IoC.Resolve<Features.Elephant.IElephantAttackService>();
        var spiderAttackService = IoC.Resolve<ISpiderAttackService>();
        var mumakilAttackService = IoC.Resolve<Features.Mumakil.IMumakilAttackService>();
        campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerAgentStat, elephantAttackService, spiderAttackService, mumakilAttackService));
        // CombatMechanics (2026-07-02): TaomCombatMechanicsModel DERIVES from the (now abstract)
        // TaomAgentApplyDamageModel — one AgentApplyDamageModel slot, career passives via
        // inheritance + the combat feel pack on top (docs/features/combat-mechanics.md).
        campaignStarter.AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(
            careerAgentStat,
            IoC.Resolve<Features.CombatMechanics.ICrushThroughService>(),
            IoC.Resolve<Features.CombatMechanics.IChargeKnockdownService>(),
            IoC.Resolve<Features.CombatMechanics.ICreatureCombatService>(),
            IoC.Resolve<Features.CombatMechanics.IShieldPenetrationService>(),
            IoC.Resolve<Features.CombatMechanics.ICombatMechanicsConfigProvider>(),
            IoC.Resolve<Features.CombatMechanics.ICombatMechanicsSettingsProvider>()));
        campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));
        // BannerBearers: the engine's BannerBearerLogic already runs in every field battle,
        // sally-out and siege — this model supplies TAOM's policy (bearers per formation, the
        // race gate, an unarmed-bearer backstop). Resolved through MissionGameModels, which
        // takes the LAST registered model, so ours wins over SandBox's. Campaign-only: Custom
        // Battle builds CustomBattleBannerBearersModel off a BasicGameStarter and is unaffected.
        campaignStarter.AddModel<BattleBannerBearersModel>(new TaomBattleBannerBearersModel(
            IoC.Resolve<Features.BannerBearers.IBannerBearerService>()));
    }

    // Campaign-life behaviors: startup resources, companions, inventory/equipment QoL, fief +
    // formation tooling, messengers, marketplace, castle recruitment, alignment systems, culture
    // conversion, and the LOTR issue takeover (suppression stays inside OnGameStart, last in order).
    private static void RegisterCampaignLifeBehaviors(CampaignGameStarter campaignStarter)
    {
        var goldService = IoC.Resolve<IStartupGoldService>();
        var influenceService = IoC.Resolve<IStartupInfluenceService>();
        var startupLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));

        var namedCompanionService = IoC.Resolve<INamedCompanionService>();
        campaignStarter.AddBehavior(new NamedCompanionBehavior(namedCompanionService));

        // QuickActions: per-save inventory-search-box persistence (SyncData round-trips
        // even when EnableInventorySearch is OFF — disabled = inert, not absent).
        campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.QuickActions.Hooks.InventorySearchCampaignBehavior>());

        // EquipPresets: per-save preset persistence + orphan pruning. Unconditional registration
        // so the SyncData round-trip preserves presets even when EnableEquipmentPresets is OFF
        // (the MCM hint promises "existing presets are inert (preserved in save)").
        campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.EquipPresets.Hooks.EquipmentPresetCampaignBehavior>());

        // FiefManagement (Patch36) — register UNCONDITIONALLY so the menu is always present
        // and the EnableFiefManagement MCM toggle takes effect immediately at runtime.
        campaignStarter.AddBehavior(new FiefHubCampaignBehavior(
            IoC.Resolve<IFiefHubMenuPresenter>(),
            IoC.Resolve<IFiefManagementSettingsProvider>()));

        // CompanionTactics (Patch35) — FormationPresets persistence behavior. Registered
        // unconditionally so SyncData round-trips even when EnableFormationPresets is OFF.
        campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(
            IoC.Resolve<Features.CompanionTactics.FormationPresets.IFormationPresetService>(),
            IoC.Resolve<IModLogger>()));

        // Messengers — paid messenger dispatch + dialog hooks + per-save SyncData persistence.
        // Registered unconditionally so saves round-trip pending messengers even when
        // EnableMessengers is OFF (disabled = inert, not absent).
        campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>());

        // CultureMarketplace (#207) — daily injection of LOTRLOME items into town markets
        // keyed by owner culture. No SyncData (stock lives in vanilla Settlement.ItemRoster).
        campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
            IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
            IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
            IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
            IoC.Resolve<ITownRosterAdapter>(),
            IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
            IoC.Resolve<IModLogger>()));

        // CaravanTrade — per-caravan visit memory feeding the GetTradeScoreForTown recency penalty
        // (fixes caravans shuttling between the nearest two towns). Registered unconditionally so a
        // mid-session master-toggle-on works immediately; no SyncData (ephemeral, rebuilds as caravans move).
        campaignStarter.AddBehavior(IoC.Resolve<Features.CaravanTrade.CaravanVisitMemoryBehavior>());
        // Rolls the town-gold ledger onto a fresh day and clears it between campaigns (#317 follow-up).
        campaignStarter.AddBehavior(IoC.Resolve<Features.EconomyDiagnostics.EconomyDiagnosticsBehavior>());

        // CastleRecruitment (Patch42) — castle notable population + maintenance + volunteer fill +
        // player "Recruit troops" castle menu + issue/quest suppression for castle notables.
        // Registered unconditionally so the MCM master toggle takes effect at runtime.
        campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
            IoC.Resolve<ICastleRecruitmentService>(),
            IoC.Resolve<IModLogger>(),
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>()));

        // AlignmentDesertion — opposed-alignment troops (Free vs Evil) desert daily from mobile
        // parties and garrisons. Registered unconditionally so the MCM master toggle takes effect
        // at runtime; stateless (no SyncData). Reuses the Execution IAlignmentService.
        campaignStarter.AddBehavior(new Features.AlignmentDesertion.Hooks.AlignmentDesertionBehavior(
            IoC.Resolve<Features.AlignmentDesertion.IAlignmentDesertionService>(),
            IoC.Resolve<IModLogger>()));

        // EliteEmissary — buy a faction's elite troops for its special resource at key settlements.
        // Registered unconditionally so the MCM master toggle takes effect at runtime; stateless (no SyncData).
        campaignStarter.AddBehavior(new Features.EliteEmissary.Hooks.EliteEmissaryBehavior(
            IoC.Resolve<Features.EliteEmissary.IEliteEmissaryService>(),
            IoC.Resolve<Features.EliteEmissary.IEliteEmissarySettingsProvider>(),
            IoC.Resolve<Features.EliteEmissary.IEliteEmissaryConfigProvider>(),
            IoC.Resolve<ISettlementOwnerAdapter>(),
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>(),
            IoC.Resolve<IModLogger>()));

        // CultureConversion — conquered cross-culture fiefs gradually adopt the new owner's culture
        // (troops, militia, identity). Registered unconditionally so SyncData round-trips conversion
        // records and completed overrides re-apply on load even when the MCM toggle is off.
        campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
            IoC.Resolve<Features.CultureConversion.ICultureConversionService>(),
            IoC.Resolve<Features.CultureConversion.ICultureConversionStore>(),
            IoC.Resolve<IModLogger>(),
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>()));

        // Enlistment (#375) — serve as a soldier in a lord's party. Registered unconditionally so
        // SyncData round-trips the service record and load normalization can rescue an ownerless
        // hidden MainParty even when the feature is later toggled off. World mutations are
        // host-only inside the behavior.
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentMenuBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentBattleBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentMaintenanceBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentDialogBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentReleaseDialogBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentContentBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentQuartermasterBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentDutyBehavior>());
        campaignStarter.AddBehavior(IoC.Resolve<Features.Enlistment.Hooks.EnlistmentAssignmentDialogBehavior>());

        // FieldCommission (#376) — battlefield promotion of ranked troops into companions.
        // Registered unconditionally; merit accrual/offers gate internally on config, co-op
        // authority, and enlisted-state suppression via IEnlistmentStateQuery.
        campaignStarter.AddBehavior(new Features.FieldCommission.Hooks.FieldCommissionBehavior(
            IoC.Resolve<Features.FieldCommission.IFieldCommissionMeritService>(),
            IoC.Resolve<Features.FieldCommission.IFieldCommissionOfferFlowService>(),
            IoC.Resolve<Features.FieldCommission.IFieldCommissionConfigProvider>(),
            IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>(),
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>(),
            IoC.Resolve<Adapters.IHeroCommissionAdapter>()));

        // LotrIssues — suppress ALL 43 vanilla procedural issue behaviors (Sandbox registered them
        // before this OnGameStart) and register the single LOTR custom-issue dispatcher in their
        // place. New-campaign feature: a pre-suppression save keeps in-flight vanilla issues until
        // they resolve, since their behaviors are only absent for newly-started campaigns here.
        Features.LotrIssues.LotrIssueSuppression.SuppressAll(campaignStarter, IoC.Resolve<IModLogger>());
        campaignStarter.AddBehavior(new Features.LotrIssues.LotrIssuesCampaignBehavior(
            IoC.Resolve<Features.LotrIssues.ILotrIssueService>(),
            IoC.Resolve<IModLogger>()));
    }

    public override void OnGameInitializationFinished(Game game)
    {
        base.OnGameInitializationFinished(game);

        // Harmony patches are process-global (applied to methods, persist across games). Apply this
        // whole per-game-init patch block ONCE per process — re-applying on a 2nd game init duplicates
        // every prefix/postfix, restarts the BattleLoad watchdog, and CRASHES the non-idempotent
        // DeliverOffSpring transpiler (chained twice, it can't find its already-NOPped anchor). The
        // shader-precompile walk starts N custom games in one process and tripped exactly this on item 2;
        // a player loading a 2nd campaign/custom-battle in one session hits the same crash.
        // Mirrors _missionTimePatchesApplied in OnMissionBehaviorInitialize.
        if (_gameInitPatchesApplied) return;
        _gameInitPatchesApplied = true;

        // Diagnostics 2026-07-31 ("bendy man" / prone tableau): these categories own the entire
        // character-preview path. They were applied unguarded and in sequence, so the FIRST one to
        // throw silently prevented every later one from applying — a state that is indistinguishable,
        // from any log we ship, from all of them working correctly. Each is now isolated and reports
        // its own outcome.
        //
        // Patch67 (2026-08-06, issue #389) is listed here for the same error isolation but is
        // deliberately its OWN category: the black-silhouette investigation calls for disabling
        // Patch2/Patch3 to test whether TAOM's own patches cause the fault, and the instrument has to
        // keep reporting while that A/B runs. Remove the entry to silence it; do not fold it into
        // Patch2.
        foreach (var previewCategory in new[]
        {
            "Patch1_FirstTimeInit",
            "Patch2_RefreshTableau",
            "Patch3_SetRace",
            "Patch4_CharacterSpawner",
            "Patch5_FaceGen",
            "Late_Transpiler",
            "Late_ActionSetOverride",
            "Patch67_TableauResidencyDiag",
        })
        {
            try
            {
                _harmony.PatchCategory(previewCategory);
                Features.HeroRace.Diagnostics.TableauDiagnostics.LogAlways($"PatchCategory '{previewCategory}' applied OK.");
            }
            catch (System.Exception ex)
            {
                Features.HeroRace.Diagnostics.TableauDiagnostics.LogError(
                    $"PatchCategory '{previewCategory}' FAILED — the character preview will fall back to vanilla resolution: {ex}");
            }
        }


        // Repair ActionIndexCache's static indices if they were baked before action types loaded —
        // the bind-pose ("bendy man") fault. Self-gating: it checks MBAnimation (never
        // ActionIndexCache) and defers if action types are not ready, so calling it here cannot
        // cause the poisoning it exists to fix. CharacterSpawnerService retries on the first
        // tableau, which is late enough to always succeed.
        Features.HeroRace.ActionIndexCacheRepair.TryEnsureRepaired("OnGameInitializationFinished");
        _harmony.PatchCategory("Patch6_BannerEditor");
        _harmony.PatchCategory("Patch7_FactionMap");
        _harmony.PatchCategory("Patch9_RaceFilter");
        _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
        _harmony.PatchCategory("Patch8_SiegeCampGuard");
        _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
        _harmony.PatchCategory("Patch11_Diplomacy");
        _harmony.PatchCategory("Patch12_WarOfTheRing");

        _harmony.PatchCategory("Patch14_Execution");
        _harmony.PatchCategory("Patch15_BannerLayerLimit");
        _harmony.PatchCategory("Patch16_AtmospherePersistence");
        _harmony.PatchCategory("Patch17_TroopWeight");
        _harmony.PatchCategory("Patch23_BannerColorPersistence");
        _harmony.PatchCategory("Patch24_BannerDriftGuard");
        _harmony.PatchCategory("Patch39_BanditPartySize");
        _harmony.PatchCategory("Patch40_HideoutDescription");
        // Patch64 — retints game-menu hyperlinks by faction. GameMenuVM is constructed when the
        // map/menu state opens, well after initialization, so the standard batch is early enough.
        _harmony.PatchCategory("Patch64_MenuLinkColors");
        // Patch65 — guards vanilla's unguarded Settlement.All.First(culture) in
        // HeroSpawnCampaignBehavior.SpawnLordParty. TWO listeners reach that target, and the
        // tighter of the two is what constrains this placement: DailyTickClanEvent (the daily
        // clan tick) AND OnNewGameCreatedPartialFollowUpEvent -> TrySpawnHeroesAndParties(
        // isNewGame: true) -> SpawnLordParty. The standard batch is still correct — this method
        // runs as the last statement of Campaign.OnInitialize, and the new-game dispatch runs
        // later from PostInitializeFourthState — but do NOT re-batch this to a lazier hook on the
        // strength of "it only fires on the daily tick": the new-game path would then be
        // unguarded.
        _harmony.PatchCategory("Patch65_LandlessCultureSpawnGuard");
        // Patch66 — enlistment menu guard (SetNextMenu redirect + EnterMenuMode recovery) and,
        // as the battle layer lands, the four LordConversations condition suppressions. All
        // campaign-runtime targets; menus first open well after this batch. Fail-open prefixes
        // gated on IEnlistmentStateQuery — inert while not enlisted.
        _harmony.PatchCategory("Patch66_Enlistment");
        _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
        // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment
        // ("post-sever tick AV") was actually the CanAttack charge crash at set_attack_entity
        // (0x6BAB4E), which fired with AND without Patch47 and is fixed in data (LOTRLOME
        // monster Flags). Patch47's own job verified working: severed riders die clean on-foot
        // deaths (act_death_by_arrow class) instead of AVing in the native mounted-death path —
        // which 1.4.6 still does on melee deaths (Die-path AV reading float-bits-as-index from
        // a corrupted action record, debugger-proven 06-12). See docs/features/spider.md.
        Features.Spider.Hooks.Agent_Die_SpiderDismount_Patch.Initialize();
        _harmony.PatchCategory("Patch47_SpiderDeathDismount");

        // Patch48: the non-lethal sibling of Patch47. A CanDismount melee hit on a mounted Spider Rider AVs in
        // native HandleBlowAux (reading 0x3) — the same broken non-vanilla mounted-dismount path Patch47 routes
        // around on death. Strips CanDismount for spider riders so the native dismount never fires (the rider
        // stays on the locked mount; damage still applies). Debugger-proven 2026-06-15. See docs/features/spider.md.
        _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");

        // Patch50: Finalizer swallowing a vanilla NRE in Agent.CheckToDropFlaggedItem (Agent.cs:3595),
        // reached via the shared synthetic-bite path (CustomAttacksUtils.TakeDamage → RegisterBlow →
        // OnAgentHit → affectedAgent.CheckToDropFlaggedItem) when a warg bites another warg (mount
        // victim with a null wielded Item). Already caught by WargAttackService, but swallowing lets
        // OnAgentHit finish and stops the log spam. Crash report 2026-06-17. See the patch doc-comment.
        _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");

        // Patch63_BlowDiagnostics: toggle-gated (MCM "TAOM — Blow Diagnostics", OFF by default)
        // durable [BlowDiag] stamps on Agent.HandleBlowAux / Agent.Die / RangedSiegeWeapon.ShootProjectileAux.
        // Ships to capture the dwarf-siege native AV (wound + fire-pot impact) that leaves no managed
        // stack: the last durable line before the process dies names the fatal blow. Diagnostic siblings
        // of Patch47/48 — separate classes so the spider guards are untouched. See docs/features/blow-diagnostics.md.
        _harmony.PatchCategory("Patch63_BlowDiagnostics");

        // Patch56_SceneNotificationVisualGuard: Finalizer swallowing a managed NRE in
        // PopupSceneSpawnPoint.InitializeWithAgentVisuals, reached via GauntletSceneNotification.OpenScene
        // when the become-king (or sibling) cinematic builds a character whose human AgentVisuals yields
        // null — the engine derefs the human visual without a null guard (it guards only the mount). The
        // finalizer aborts the cinematic cleanly (HideSceneNotification) so cinematics that CAN render
        // still play. Fourth raw custom-race/visual render path (after Patch55). Crash reports
        // 2026-06-24/25 (become ruler of empire_w/gondor). See the patch doc-comment.
        _harmony.PatchCategory("Patch56_SceneNotificationVisualGuard");

        // Patch13_RaceAge — noise reduction (NOT a crash fix). NOPs the harmless
        // mother.Race == father.Race SilentAssert in DeliverOffSpring that fires on every
        // mixed-race birth (normal in TAOM). Stops the debugger break + debug-log spam.
        _harmony.PatchCategory("Patch13_RaceAge");

        var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();
        var specResLogger = IoC.Resolve<IModLogger>();
        PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
        PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
        PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
        RecruitmentVM_RecruitGate_Patch.Initialize(IoC.Resolve<IOnRecruitmentResourceGate>(), specResLogger);
        _harmony.PatchCategory("Patch26_SpecialResources");
        _harmony.PatchCategory("Patch51_RecruitmentResourceGate");
        _harmony.PatchCategory("Patch27_CareerSystem");
        _harmony.PatchCategory("Patch29_CCBodyProperties");
        _harmony.PatchCategory("Patch44_CCNameAutofill");
        _harmony.PatchCategory("Patch33_EquipPresets");
        _harmony.PatchCategory("Patch34_QuickActions");
        _harmony.PatchCategory("Patch35_CompanionTactics");
        _harmony.PatchCategory("Patch36_FiefManagement");
        SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
        _harmony.PatchCategory("Patch38_SettlementNameplateFade");

        // Patch53_PartyIconScale — transpiler that rewrites the two hardcoded 0.3f campaign-map scale
        // literals in MobilePartyVisual.AddCharacterToPartyIcon (leader figure + its mount) into a call
        // to PartyIconScaleConfig.GetScale(), so both honour the MCM "Map Figure Scale" slider
        // (default 0.15 = half vanilla). See docs/features/party-icon-scale.md.
        Features.PartyIconScale.Hooks.Patch53_PartyIconScale.Initialize(IoC.Resolve<IModLogger>());
        _harmony.PatchCategory("Patch53_PartyIconScale");

        // NavalTravel PARKED 2026-06-26 (#296/#120) — see the model-registration comment in OnGameStart.
        // Patch54 (boat visual) + Patch57 (at-sea native-AV crash guard) are only meaningful while a party
        // can be at sea, which only the (now-unregistered) TaomPartyNavigationModel enables — so neither is
        // applied while the feature is parked. RE-ENABLE: uncomment both blocks with the model registration.
        // Patch54_NavalTravelBoatVisual — render an at-sea party as a boat (base game renders no ship at sea).
        // Features.NavalTravel.Hooks.Patch54_NavalTravelBoatVisual.Initialize(IoC.Resolve<Features.NavalTravel.INavalTravelService>(), IoC.Resolve<IModLogger>());
        // _harmony.PatchCategory("Patch54_NavalTravelBoatVisual");
        //
        // Patch57_NavalAtSeaLandRescueGuard — prevent the native AV CTD on the hourly AI tick (the vanilla
        // AIMoveToNearestLandBehavior's native cross-region pathfind AVs on TAOM_Map's missing naval navmesh,
        // #120). Only fires for an at-sea party, which can't happen while the model is unregistered.
        // var navalRescueLogger = IoC.Resolve<IModLogger>();
        // Features.NavalTravel.Hooks.Patch57_NavalAtSeaLandRescueGuard.Initialize(IoC.Resolve<Features.NavalTravel.INavalTravelService>(), navalRescueLogger);
        // try { _harmony.PatchCategory("Patch57_NavalAtSeaLandRescueGuard"); }
        // catch (System.Exception ex) { navalRescueLogger.LogWarning($"[NavalTravel] Patch57 at-sea rescue guard failed to apply: {ex.Message}"); }

        // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an
        // intermittent battle-load hang leaves a log whose last line names the stuck phase
        // (and, for the equipment phase, the agent + the item whose bo_ collision mesh is
        // missing). The background stall watchdog auto-triggers a crash bundle on a freeze.
        var battleLoadSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
        var equipSnapshotAdapter = IoC.Resolve<IEquipmentSnapshotAdapter>();
        var battleLoadStallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
        Features.BattleLoadDiagnostics.Hooks.PlayerEncounter_Start_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MissionState_OpenNew_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.BattleSceneSelection_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.Mission_Initialize_BattleLoad_Patch.Initialize(battleLoadSvc, battleLoadStallMarker);
        Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.Initialize(battleLoadSvc, equipSnapshotAdapter);
        // AgentEquipOk only proves the equip call returned; Mission.BuildAgent keeps working on the
        // same agent for another ~14 native lines. This stamps its return, so an Ok with no
        // BuildDone localizes a CTD to that tail (2026-08-02 Dunland tournament).
        Features.BattleLoadDiagnostics.Hooks.Mission_BuildAgent_BattleLoad_Patch.Initialize(battleLoadSvc);
        // OpenNew->Initialize window probes (2026-07-16 Nan Angren player CTD): the OpenNew stamp
        // is a Prefix, so a crash in OpenNew's body, in LoadMission, or in the native resource
        // clear all produced an identical log tail. These name the segment that died.
        Features.BattleLoadDiagnostics.Hooks.MissionState_LoadMission_BattleLoad_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.Utilities_ClearOldResourcesAndObjects_BattleLoad_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.Mission_AfterStart_BattleLoad_Patch.Initialize(battleLoadSvc);
        // The MissionInitialize -> MissionAfterStartBegin gap measured 11.9 s of a 29 s load with no
        // instrumentation inside it. MissionState.cs:221-350 splits it into exactly three buckets:
        // the native InitializeMission call, the async IsLoadingFinished wait, and
        // FinishMissionLoading's pre-AfterStart work. FinishMissionLoading and TickLoading are both
        // PRIVATE — bound by string like the sibling LoadMission/BuildAgent patches. TickLoading is
        // a COUNTER hook only and never logs (720 lines in a 12 s wait at 60fps).
        Features.BattleLoadDiagnostics.Hooks.MissionState_FinishMissionLoading_BattleLoad_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MissionState_TickLoading_BattleLoad_Patch.Initialize(battleLoadSvc);
        // Exit-phase probes (issue #331 — 30s-2min hang exiting tournaments): stamp the
        // mission end -> map resume window so the dominant phase gap names the time sink.
        Features.BattleLoadDiagnostics.Hooks.Mission_EndMission_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.Mission_EndMissionInternal_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.Mission_ClearUnreferencedResources_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MissionState_OnFinalize_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MapState_OnActivate_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MapState_OnTick_ExitPhase_Patch.Initialize(battleLoadSvc);
        // Guarded like Patch60/61/62: this category binds several engine targets by string (two of
        // them private), so an engine bump can throw here. A DIAGNOSTICS category must never take
        // startup down with it — losing the stamps is survivable, losing the game is not.
        try { _harmony.PatchCategory("Patch43_BattleLoadDiagnostics"); }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogWarning($"[BattleLoad] Patch43 diagnostics failed to apply: {ex.Message}");
        }
        IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();

        // Exit-stall stack sampler (#331 round 2): OnGameInitializationFinished runs on the
        // game's main thread — the same thread the tournament-exit stall freezes — so this
        // is a valid capture point for the sampler's main-thread reference. (Verified against
        // 1.4.6/1.4.7: native tick → Module.OnApplicationTick → GameManager → Campaign init.
        // If a future engine bump moves game-init off the tick thread, the sampler would walk
        // the wrong thread — re-verify this call chain on engine bumps; accepted as-is per
        // Codex round-2 P3, no runtime invariant check.)
        var exitStallSampler = IoC.Resolve<Features.BattleLoadDiagnostics.ExitStallSampler>();
        exitStallSampler.SetMainThread(System.Threading.Thread.CurrentThread);
        exitStallSampler.Start();

        // Patch60 — release the tournament UI movie/layer at OnEndMission time. The engine's
        // MissionGauntletTournamentView leaks both (nulls without release, unlike the practice
        // view), deferring the Tournament-movie teardown into ScreenBase.HandleFinalize under
        // the exit loading screen, where an in-flight prize tableau render stalls it ~108s (#331).
        var tournamentExitLogger = IoC.Resolve<IModLogger>();
        Features.Arena.Hooks.Patch60_TournamentExitMovieRelease.Initialize(tournamentExitLogger);
        try { _harmony.PatchCategory("Patch60_TournamentExitMovieRelease"); }
        catch (System.Exception ex) { tournamentExitLogger.LogWarning($"[Arena] Patch60 tournament-exit movie release failed to apply: {ex.Message}"); }

        // Patch69 — winner-panel crash guard. Vanilla TournamentVM.OnTournamentEnd dereferences
        // hero.MapFaction.Color / character.Culture.Color without a null check; a clanless hero
        // with no home settlement and no party has a null MapFaction, so winning a tournament
        // NREs the panel (bundle d7d9f7d3, Erebor, 2026-08-06). The roster guard substitutes such
        // entrants at GetParticipantCharacters — never removes them, because vanilla's own
        // TournamentMatch.AddParticipant NREs on a short roster. The end guard is containment plus
        // the bracket dump that names any null site the roster guard does not cover.
        // Both categories fail independently: a diagnostic must never cost a working tournament.
        try { _harmony.PatchCategory("Patch69_TournamentRosterGuard"); }
        catch (System.Exception ex) { tournamentExitLogger.LogWarning($"[Arena] Patch69 tournament roster guard failed to apply: {ex.Message}"); }
        try { _harmony.PatchCategory("Patch69_TournamentEndGuard"); }
        catch (System.Exception ex) { tournamentExitLogger.LogWarning($"[Arena] Patch69 tournament end guard failed to apply: {ex.Message}"); }

        // Manual patches for PRIVATE engine methods (AccessTools-resolved targets; can't use
        // [HarmonyPatch] attribute binding + PatchCategory). Extracted verbatim to
        // ManualPatchApplicator (ADR-002); apply order unchanged, each fail-safes with a warning.
        ManualPatchApplicator.ApplyAll(_harmony);

        // Harmony census — LAST, so it sees every patch TAOM and every other mod has applied.
        // Only runs when a co-op module is active: it is the substitute for decompiling that mod
        // (which its licence forbids and TAOM does not do), and solo players should not pay the
        // registry walk. Reads HarmonyLib's own public registry — owner ids, patch kinds and
        // reflection metadata — never a method body.
        try
        {
            var coopIds = Dependencies.Foundation.CoopPresence.ActiveCoopModuleIds;
            if (coopIds.Count > 0)
            {
                Features.CoopInterop.Diagnostics.HarmonyCensusWriter.Write(
                    new Features.CoopInterop.Diagnostics.HarmonyCensusReportBuilder(),
                    coopIds,
                    IoC.Resolve<IModLogger>());
            }
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogWarning($"[HarmonyCensus] wiring failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);

        // Apply Formation.SetMovementOrder patches (Patch31_SmartCavalryAI + Patch35
        // CancelStanceOnMove) only once Mission.Current is non-null — MovementOrder's
        // type initializer constructs static fields whose ctor reads
        // Mission.Current.CurrentTime. Applying earlier crashes JIT prep with NRE.
        if (!_missionTimePatchesApplied)
        {
            _missionTimePatchesApplied = true;
            _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
        }

        // [BattleLoad] TAOM-behavior bracket. Mission.AfterStart calls this for EVERY submodule,
        // so the Begin->Done pair fences OUR behaviors off from other mods' — a crash between
        // MissionAfterStartBegin and TaomBehaviorsBegin is not ours, which is what lets a player
        // report exonerate TAOM instead of merely accusing it.
        var battleLoadDiagSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
        try { battleLoadDiagSvc?.LogTaomBehaviorsBegin(); } catch { /* diagnostic only */ }
        int taomBehaviorCount = 0;

        // Stamps the name BEFORE handing the behavior to the engine: the ctors here are trivial,
        // but AddMissionBehavior does real engine registration, so a fault there leaves the
        // culprit's name as the last line rather than forcing source archaeology on the order.
        void AddTaomBehavior(MissionBehavior behavior)
        {
            try { battleLoadDiagSvc?.LogTaomBehaviorAdded(behavior.GetType().Name); } catch { /* diagnostic only */ }
            mission.AddMissionBehavior(behavior);
            taomBehaviorCount++;
        }

        // 1.4.7 headless-battle deployment-NRE guard: added ONLY while a shader-precompile walk is in
        // flight (never a normal battle — IsWalkInProgress is false then). Seeds Mission.InitialPlayerAgent
        // on the first agent build so the engine's new DeploymentMissionController.SetupTeams deref doesn't
        // NRE the player-less precompile battle. Must be added HERE (the engine's mission-init hook, with
        // the mission handed in directly) — an AddMissionBehavior from the game manager's OnLoadFinished
        // no-ops because Mission.Current is not yet the battle mission at that point.
        if (Features.ShaderPrecompilation.ShaderPrecompileRunner.IsWalkInProgress)
            AddTaomBehavior(new Features.ShaderPrecompilation.ShaderPrecompilePlayerAgentGuard(IoC.Resolve<IModLogger>()));

        AddTaomBehavior(new AdvancedCombatBehavior());
        AddTaomBehavior(new BehaviorTreeMissionLogic());
        AddTaomBehavior(new AutonomousMovementPlayerController());
        AddTaomBehavior(new WargMissionBehavior());
        AddTaomBehavior(new SpiderMissionBehavior());
        AddTaomBehavior(new Features.Elephant.ElephantMissionBehavior());
        AddTaomBehavior(new Features.Mumakil.MumakilMissionBehavior());
        AddTaomBehavior(new SiegeDismountMissionBehavior());
        // Registered unconditionally; gates internally on its MCM toggle (off by default).
        AddTaomBehavior(new Features.SiegePropDiagnostics.Hooks.SiegePropDiagnosticsMissionBehavior());
        AddTaomBehavior(new MixedFormationsMissionBehavior());
        AddTaomBehavior(new SmartCavalryAIMissionBehavior());
        // Registered unconditionally per TAOM convention; self-filters in AfterStart on
        // Campaign.Current + enlisted battle state (the donor's mission.Mode gate at init
        // time never fired — Mode is still StartUp there).
        AddTaomBehavior(new Features.Enlistment.Hooks.EnlistmentMeritMissionBehavior(
            IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>(),
            IoC.Resolve<Features.Enlistment.Content.IBattleMeritAccumulator>(),
            IoC.Resolve<Features.Enlistment.Content.IEnlistmentContentStore>(),
            IoC.Resolve<Features.Enlistment.Content.IEnlistmentContentConfigProvider>().GetConfig().MeritScoring));
        // Registered unconditionally; self-filters internally on Campaign.Current and co-op authority.
        AddTaomBehavior(new Features.FieldCommission.Hooks.FieldCommissionMissionLogic());
        // Added unconditionally per TAOM convention; gates internally on
        // Mission.Mode == Deployment (the bearer-freeze guard) and on a live BannerBearerLogic.
        AddTaomBehavior(new Features.BannerBearers.Hooks.BannerBearerAssignmentMissionLogic());
        AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());

        var colorStore = IoC.Resolve<IAgentColorStore>();
        if (colorStore != null)
            AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));

        // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
        // every other mod in the load chain. Dumps MissionBehaviors + MissionLogics
        // on first OnMissionTick to taom_debug_*.log so user-uploaded crash logs
        // contain enough data to identify mod-conflict bugs (BehaviorType=Logic +
        // !MissionLogic null-cast offenders) and action-set anomalies.
        var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
        var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
        var diagLogger = IoC.Resolve<IModLogger>();
        if (diagSvc != null && raceMgr != null && diagLogger != null)
            AddTaomBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));

        // BattleLoadDiagnostics phase-6: "battle playable" marker on first tick + closes
        // the loading window so the stall watchdog stands down and phase-5 stops logging.
        //
        // Registered UNCONDITIONALLY (TAOM convention, and latch rule 3 in
        // .claude/rules/harmony-patches.md — verify "unconditional" at the OUTERMOST gate). This
        // behavior is the loading window's ONLY closer; the opener runs in Mission.Initialize's
        // prefix. The 2026-07-06 RCA deferred this one as "the same synchronous call chain", and
        // the three-bucket measurement disproves that premise: the two evaluations are separated by
        // a tick boundary AND a measured ~11.9 s native load (MissionState.cs:221-350). A toggle
        // flipped inside that window latched the loading window open until the next
        // Mission.Initialize, and the stall watchdog then fired at 300 s and wrote a spurious
        // bundle. This changeset makes toggling MCM mid-session an EXPECTED operator action during
        // the commit-attribution matrix, so the window is no longer theoretical.
        //
        // Safe to register while disabled: BattleLoadPhaseBehavior already self-gates its logging
        // (LogBattlePlayable returns early when disabled) while Close()/ClearInflight() are
        // unconditional state transitions. Steady-state cost is one `if (_playableLogged) return;`
        // per mission tick.
        if (battleLoadDiagSvc != null)
            AddTaomBehavior(new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(
                battleLoadDiagSvc, IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>()));

        // Dev-trigger behavior watches the CrashReport MCM toggle and throws a tagged
        // TaomDevTriggerException on the next OnMissionTick when the player flips
        // "Throw On Next Mission Tick". QA only — no-op in normal play.
        AddTaomBehavior(new Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior());

        var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
        if (careerAbilityService != null && Campaign.Current != null)
        {
            AddTaomBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
                IoC.Resolve<ICareerDataService>(),
                careerAbilityService,
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
                IoC.Resolve<Features.CareerSystem.ICareerPassiveService>(),
                IoC.Resolve<Features.CareerSystem.ICareerConfigProvider>(),
                IoC.Resolve<IModLogger>()));
        }

        try { battleLoadDiagSvc?.LogTaomBehaviorsDone(taomBehaviorCount); } catch { /* diagnostic only */ }
    }

    protected override void OnApplicationTick(float dt)
    {
        _timeAccelerationService?.OnTick();

        // Shader pre-compilation walk: tick the runner every frame (responsive state transitions),
        // and surface its status as a 1 Hz toast when a loading screen isn't already showing it.
        var runner = _shaderRunner;
        if (runner != null && runner.IsActive)
        {
            runner.Tick();
            _shaderTickAccumulator += dt;
            if (_shaderTickAccumulator >= 1f)
            {
                _shaderTickAccumulator = 0f;
                if (!LoadingWindow.IsLoadingWindowActive && !string.IsNullOrEmpty(runner.StatusLine))
                    InformationManager.DisplayMessage(new InformationMessage(runner.StatusLine));
            }
        }
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        // Detach the AppDomain.UnhandledException subscription BEFORE IoC disposal so
        // the hook doesn't hold a stale reference to a disposed CrashReportService
        // across game-restart-in-same-process. Deep-review INC 3 (2026-05-25).
        try { IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>()?.Unsubscribe(); }
        catch { /* IoC may already be torn down — best-effort */ }

        // Reverse NativeSkinFixes hooks so DLL unload during reload-in-same-process
        // doesn't leave dangling MinHook trampolines. Best-effort — swallows.
        try { NativeSkinFixesInstaller.Uninstall(); }
        catch { /* shutdown — never block */ }

        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();

        // Codex review #46 (2026-05-25) HIGH-01: clear the static service cache in
        // the patch helper so the next module load resolves a fresh service graph from
        // the new IoC container. Without this, Finalizers fire against a disposed
        // FileLogger after reload and silently drop every log line.
        TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
        TAOM.Features.EconomyDiagnostics.Hooks.SettlementComponent_ChangeGold_Patch.ResetForUnload();
        TAOM.Features.Arena.Hooks.Patch69_TournamentRosterGuard.ResetForUnload();
        TAOM.Features.Arena.Hooks.Patch69_TournamentEndGuard.ResetForUnload();
    }
}
