using Bannerlord.UIExtenderEx;
using HarmonyLib;
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
        _uiExtender.Register(typeof(SubModule).Assembly);
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
        _harmony.PatchCategory("Patch30_MixedFormations");
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
        IoC.Resolve<IModLogger>().LogInfo(
            "[NativeSkinFixes] parked (disabled at the wiring level) — engine rendering is vanilla");
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
        campaignStarter.AddModel(new TaomCharacterStatsModel());
        campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
        campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));

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
        campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger));
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
        campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger));
        campaignStarter.AddBehavior(new PlayerAllianceProposalBehavior(diplomacyService, diplomacyLogger));
        campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
        campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
        campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));

        var wotrLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
        // WotR Momentum #327 — Evil-vs-Good progress tracking + victory; behavior is a
        // Reuse.Singleton (it carries the state store's persistence dict).
        campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());

        var siegeDefenseService = IoC.Resolve<ISiegeDefenseService>();
        var siegeDefenseLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger));
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
        campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives, IoC.Resolve<ITroopWeightService>()));
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
            IoC.Resolve<ITroopWeightService>());
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

        // CastleRecruitment (Patch42) — castle notable population + maintenance + volunteer fill +
        // player "Recruit troops" castle menu + issue/quest suppression for castle notables.
        // Registered unconditionally so the MCM master toggle takes effect at runtime.
        campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
            IoC.Resolve<ICastleRecruitmentService>(),
            IoC.Resolve<IModLogger>()));

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
            IoC.Resolve<IModLogger>()));

        // CultureConversion — conquered cross-culture fiefs gradually adopt the new owner's culture
        // (troops, militia, identity). Registered unconditionally so SyncData round-trips conversion
        // records and completed overrides re-apply on load even when the MCM toggle is off.
        campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
            IoC.Resolve<Features.CultureConversion.ICultureConversionService>(),
            IoC.Resolve<Features.CultureConversion.ICultureConversionStore>(),
            IoC.Resolve<IModLogger>()));

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

        _harmony.PatchCategory("Patch1_FirstTimeInit");
        _harmony.PatchCategory("Patch2_RefreshTableau");
        _harmony.PatchCategory("Patch3_SetRace");
        _harmony.PatchCategory("Patch4_CharacterSpawner");
        _harmony.PatchCategory("Patch5_FaceGen");
        _harmony.PatchCategory("Late_Transpiler");
        _harmony.PatchCategory("Late_ActionSetOverride");
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
        // Exit-phase probes (issue #331 — 30s-2min hang exiting tournaments): stamp the
        // mission end -> map resume window so the dominant phase gap names the time sink.
        Features.BattleLoadDiagnostics.Hooks.Mission_EndMission_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.Mission_EndMissionInternal_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.Mission_ClearUnreferencedResources_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MissionState_OnFinalize_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MapState_OnActivate_ExitPhase_Patch.Initialize(battleLoadSvc);
        Features.BattleLoadDiagnostics.Hooks.MapState_OnTick_ExitPhase_Patch.Initialize(battleLoadSvc);
        _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
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

        // Manual patches for PRIVATE engine methods (AccessTools-resolved targets; can't use
        // [HarmonyPatch] attribute binding + PatchCategory). Extracted verbatim to
        // ManualPatchApplicator (ADR-002); apply order unchanged, each fail-safes with a warning.
        ManualPatchApplicator.ApplyAll(_harmony);
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

        mission.AddMissionBehavior(new AdvancedCombatBehavior());
        mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
        mission.AddMissionBehavior(new AutonomousMovementPlayerController());
        mission.AddMissionBehavior(new WargMissionBehavior());
        mission.AddMissionBehavior(new SpiderMissionBehavior());
        mission.AddMissionBehavior(new Features.Elephant.ElephantMissionBehavior());
        mission.AddMissionBehavior(new Features.Mumakil.MumakilMissionBehavior());
        mission.AddMissionBehavior(new SiegeDismountMissionBehavior());
        mission.AddMissionBehavior(new MixedFormationsMissionBehavior());
        mission.AddMissionBehavior(new SmartCavalryAIMissionBehavior());
        mission.AddMissionBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());

        var colorStore = IoC.Resolve<IAgentColorStore>();
        if (colorStore != null)
            mission.AddMissionBehavior(new AgentColorStoreCleanupBehavior(colorStore));

        // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
        // every other mod in the load chain. Dumps MissionBehaviors + MissionLogics
        // on first OnMissionTick to taom_debug_*.log so user-uploaded crash logs
        // contain enough data to identify mod-conflict bugs (BehaviorType=Logic +
        // !MissionLogic null-cast offenders) and action-set anomalies.
        var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
        var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
        var diagLogger = IoC.Resolve<IModLogger>();
        if (diagSvc != null && raceMgr != null && diagLogger != null)
            mission.AddMissionBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));

        // BattleLoadDiagnostics phase-6: "battle playable" marker on first tick + closes
        // the loading window so the stall watchdog stands down and phase-5 stops logging.
        var battleLoadDiagSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
        if (battleLoadDiagSvc != null && battleLoadDiagSvc.IsEnabled)
            mission.AddMissionBehavior(new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(
                battleLoadDiagSvc, IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>()));

        // Dev-trigger behavior watches the CrashReport MCM toggle and throws a tagged
        // TaomDevTriggerException on the next OnMissionTick when the player flips
        // "Throw On Next Mission Tick". QA only — no-op in normal play.
        mission.AddMissionBehavior(new Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior());

        var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
        if (careerAbilityService != null && Campaign.Current != null)
        {
            mission.AddMissionBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
                IoC.Resolve<ICareerDataService>(),
                careerAbilityService,
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
                IoC.Resolve<Features.CareerSystem.UI.IAbilityHudController>(),
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
                IoC.Resolve<Features.CareerSystem.ICareerPassiveService>(),
                IoC.Resolve<IModLogger>()));
        }
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
    }
}
