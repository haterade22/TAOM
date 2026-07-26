# Harmony Patch Registry

Full per-category rationale, history, and RCA links for every TAOM Harmony patch — moved verbatim from CLAUDE.md's Harmony Patch Categories table (repo-reorg 2026-07-12) so the eager-loaded CLAUDE.md keeps only the thin routing table (category | feature | target | status). **Before editing any patch, read its section here** and the scoped rule [.claude/rules/harmony-patches.md](../../.claude/rules/harmony-patches.md) (loads automatically when a `Main/**/Hooks/**` file is opened). Most sections end with a `docs/features/<x>.md` pointer — that feature doc remains the deep-dive.

> **Ground truth is the code:** `grep -r 'HarmonyPatchCategory' Main/` enumerates every attribute-registered category — this registry is maintained by hand and can lag. Two caveats when reconciling: **manual patches carry no category attribute by design** (Patch28_SettlementGuards' two manual patches, Patch23_BannerColorPersistence's manual entries), and the Patch31_SmartCavalryAI / Patch35_CompanionTactics `Formation.SetMovementOrder` postfixes register under the shared `Patch_MissionTime_SetMovementOrder` category attribute, not their feature-numbered ones.

## Patch0_BattleScenes

**Target:** `Campaign.InitializeScenes`

Battle scenes (DISABLED)

## Patch1_FirstTimeInit

**Target:** Various

First-time initialization

## Patch2_RefreshTableau

**Target:** Various

Banner tableau refresh

## Patch3_SetRace

**Target:** Various

Race assignment

## Patch4_CharacterSpawner

**Target:** Various

Character spawning

## Patch5_FaceGen

**Target:** Various

Face generation

## Patch6_BannerEditor

**Target:** Various

Banner editor

## Patch7_FactionMap

**Target:** Various

Faction map

## Patch8_SiegeCampGuard

**Target:** Various

Siege camp guard

## Patch9_RaceFilter

**Target:** `FaceGenVM.Refresh`

Culture-restricted race dropdown on CC

## Patch10_WeatherBoundsGuard

**Target:** `DefaultMapWeatherModel`

Weather bounds clamping

## Patch11_Diplomacy

**Target:** Various

Diplomacy system

## Patch12_WarOfTheRing

**Target:** Various

War of the Ring

## Patch13_RaceAge

**Target:** `HeroCreator.DeliverOffSpring` (Transpiler)

Noise reduction, NOT a crash fix. Vanilla `DeliverOffSpring` carries a `Debug.SilentAssert(mother.Race == father.Race)`; in TAOM mixed-race couples are normal, so the assert fires on every cross-race birth — breaking an attached debugger via `Debugger.Break()` and spamming "Silent Assert Failed!" debug-log lines (ButterLib's wrapper only logs; harmless for players). The transpiler NOPs the entire assert sequence (args + call), matching the `SilentAssert` call by operand method name because `CallerXxx` default-parameter attributes can defeat `MethodInfo.Equals`. If either IL anchor is missing (already-NOPped by a prior application, or engine IL drift) it logs a warning and degrades to a no-op instead of throwing out of `PatchCategory` — this transpiler is explicitly non-idempotent, which is why the `OnGameInitializationFinished` patch batch is one-shot guarded (`_gameInitPatchesApplied` in `SubModule.cs`). Behavior is otherwise unchanged — the birth proceeds identically. Owning feature: RaceAge — see `docs/features/race-age-system.md`.

## Patch14_Execution

**Target:** Various

Execution system

## Patch15_BannerLayerLimit

**Target:** Various

Banner layer limit. **DISABLED at the v1.4.7 bump (2026-07-08)** — the engine made banner layers natively unlimited (`Banner.TryGetBannerDataFromCode` no longer caps at 32), which is exactly what the transpiler forced. See `docs/migration/v1.4.7-impact.md`.

## Patch16_AtmospherePersistence

**Target:** `Mission.Initialize`

Forced-atmosphere scenes

## Patch17_TroopWeight

**Target:** `PartyUpgraderCampaignBehavior.UpgradeReadyTroops` (Postfix)

Troop weight system — heavy troops cost more party-size budget. **Reworked 2026-07-11 (count→limit):** the "elite tax" is now a party-size-LIMIT *deflation* applied in `TaomPartySizeModel` (`ITroopWeightService.ApplyPartySizeWeightPenalty` subtracts `ceil(weighted)−raw` from the limit, clamped ≥1; pure `TroopWeightService.ComputeSizePenalty`), NOT a count-getter patch — so **every troop count reads RAW everywhere** (map nameplate, party screen, land-capacity, tooltips, menus, battle all agree) while the recruit cap still fills at the troop weight (the displayed limit honestly shrinks with elite stacks). The two `NumberOfAllMembers`/`NumberOfRegularMembers` getter patches, the 5 weighted-display hooks (phantom-wounded fix — now moot, nothing is weighted), the `WeightedCountCache`, and the `[CountFlicker]` diagnostic were all **REMOVED** (~26 files). This category now holds only the **shed-on-upgrade** postfix (adapted to the deflated-limit frame). Ripples handled: `SpecialResources` battle-reward scaling preserved via an explicit weighted-count call; `SettlementFood`'s garrison-leak correction self-neutralizes (vanilla food now reads raw at source). Incidental side-effects of the old global getter weighting (e.g. elite parties moving slightly slower) are intentionally gone — the feature now affects only the size cap. Reworked because the map "200↔20" flicker (proved to be the vanilla army-sum, not the weighting) + "party shows 325 but 159 fight" reports were all the weighting making UI counts disagree with reality. See `docs/features/troop-weight-system.md`.

## Patch18_CulturalFeats

**Target:** `Campaign.InitializeDefaultCampaignObjects`

Custom culture feat registration

## Patch19_CustomBattles

**Target:** `CustomBattleData`, `CustomBattleHelper`, `BannerlordMissions`

Custom battle TAOM factions/commanders/troops

## Patch20_NarrativeHorseGuard

**Target:** `CharacterCreationCampaignBehavior`, `CharacterCreationNarrativeStageView`

Suppress CC narrative horse crashes for no-mount cultures

## Patch21_ShaderPrecompilation

**Target:** `LoadingWindowViewModel`

Loading screen shader progress text

## Patch22_ArmyTargeting

**Target:** `AiMilitaryBehavior`

Border proximity floor for priority-list targets

## Patch23_BannerColorPersistence

**Target:** `CampaignUIHelper`, `SandBoxUIHelper`, `SPInventoryVM`, `PartyVM`, `HeroViewModel`, `PartyCharacterVM`, `ClanPartyItemVM`, `Mission`, `CampaignSceneNotificationHelper`, `Banner`, `BannerEditorView`, `Agent.EquipItemsFromSpawnEquipment`, `AgentVisuals.Create` (manual), `MapConversationTableau` (manual ×2), `OrderOfBattleHeroItemVM`

UI color persistence + 3D battle + conversation — player clan colors everywhere

## Patch24_BannerDriftGuard

**Target:** `Clan.UpdateBannerColorsAccordingToKingdom`, `Clan.UpdateBannerColor`

Block vanilla banner color drift during War of the Ring

## Patch25_LocalizationOverride

**Target:** `MBTextManager.GetLocalizedText` (Prefix, bound via `TargetMethod()`/`AccessTools.Method`)

English string overrides for vanilla `{=ID}` tokens. Vanilla `GetLocalizedText` short-circuits for English — it returns the inline text from the `{=ID}text` value and never consults `LocalizedTextManager` — so `module_strings.xml` entries that reuse vanilla `{=ID}` tokens are silently ignored. The Prefix parses the leading `{=ID}` (skipping non-token strings and the `{=!}`/`{=*}` sentinels) and, when an override is registered in the patch's static dictionary (`RegisterOverride`), returns that text instead — making the ~120 "The" fixes in `taom_module_strings.xml` actually take effect. Unregistered ids fall through to vanilla. See `docs/features/localization-override.md`.

## Patch26_SpecialResources

**Target:** `PartyCharacterVM.InitializeUpgrades`, `PartyScreenLogic.UpgradeTroop`, `PartyScreenLogic.AddCommand`

Per-kingdom resource gating + transactional spending

## Patch27_CareerSystem

**Target:** `ViewModel.ExecuteCommand`, `AgentStatCalculateModel.UpdateAgentStats`

Career screen opening + ability V-key activation (3 archetypes: Infantry/Ranged/Cavalry, 50 careers, XML-tunable)

## Patch28_SettlementGuards

**Target:** `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (manual), `GuardsCampaignBehavior.GetSuitableSpear` (manual), `GuardsCampaignBehavior.InitializeGarrisonCharacters` (manual, Postfix)

Per-settlement guard troop injection + per-culture spear mapping (manual patches). The `InitializeGarrisonCharacters` Postfix (#346, 2026-07-14) scrubs excluded-race troops (cave troll) out of the private `_garrisonTroops` guard candidate list — vanilla draws guards from it weighted by troop LEVEL, so the L51 troll dominated the pick in any settlement without a configured pool. Field read via cached `AccessTools.Field`, fail-open warn-once; garrison roster/siege defense untouched. See `docs/features/settlement-guards.md` "Guard-Duty Race Exclusions".

## Patch29_CCBodyProperties

**Target:** `CharacterCreationContent.SetSelectedCulture`, `CharacterCreationCultureStageVM.OnCultureSelection`, `CharacterCreationNarrativeStageView.RefreshAgentVisuals`

Per-culture default BodyProperties on CC screen + culture-stage-VM body re-apply + career menu player body sync

## Patch30_MixedFormations

**Target:** `Formation.GetOrderPositionOfUnit` (Prefix)

Mixed ranged/melee formation layout — the Prefix replaces a unit's vanilla order position with the plane position computed by `IFormationLayoutService.ComputeUnitPlanePosition(formation, agentIndex, agentIsRanged)`, grounded via `Scene.GetGroundHeightAtPosition` and validated through `Mission.IsFormationUnitPositionAvailable` before overriding `__result` (Codex review #35 HIGH: vanilla's Hold path routes through that availability check to keep units off non-navigable terrain — an unavailable candidate falls through to vanilla so the engine's own `unit.GetWorldPosition()` fallback applies). Any null/missing-value/exception path returns `true` (vanilla). HOT PATH — fires per-unit per-formation-position-recalculation (up to ~40,000×/frame worst case in 200-unit formations), so the service singleton is cached in a static field per the harmony-patches hot-path rule. See `docs/features/mixed-formations.md`.

**Fall-through cases (do not remove).** A blanket `return false` on a positioning Prefix is a silent monopoly on that engine decision — any other feature relying on the vanilla path breaks against it with no error. Patch30 therefore falls through for:
- **Non-field-battle missions** (`Mission.IsFieldBattle != true`) — siege, sally-out, hideout, naval, settlement.
- **Banner bearers** (`unit?.Banner != null`) — `BannerBearerLogic` places them via `SwitchUnitLocations` into the engine's dedicated `RelativeFormationPosition[6]` banner slots; overriding their order position scatters the standards through the ranks. Added 2026-07-16 (Codex review 74 MED) when BannerBearers first gave formations bearers for Patch30 to misplace. `Agent.Banner` is `Equipment?.GetBanner()` — one `_weaponSlots[4]` read, no loop or allocation, so the check is cheap enough for this hot path, and it sits *before* the IoC resolve. See `docs/features/banner-bearers.md` + `docs/reviews/rca-banner-bearers-2026-07-16.md`.

## Patch31_SmartCavalryAI

**Target:** `Formation.SetMovementOrder` (Postfix, deferred — see `Patch_MissionTime_SetMovementOrder`)

Coordinated line-charge state machine on player cavalry (Forming→Charging→PassingThrough→Reforming + Rerouting branch); recursion-guarded. **Note:** the `Formation.SetMovementOrder` Postfix lives in the shared `Patch_MissionTime_SetMovementOrder` category (see below).

**Open-field-only (do not remove).** This is an open-field line charge and must never manipulate formations outside a field battle. `CavalryChargeService.HandleChargeOrder`/`.Tick` bail unless `IBattlefieldQueryAdapter.IsFieldBattle` (engine `Mission.IsFieldBattle` — true ONLY for `MissionTeamAIType == FieldBattle`), so no native `Formation.SetPositioning`/`SetMovementOrder` is issued in a siege / sally-out / hideout / naval / settlement mission. That native re-entry, while the engine is still finalizing siege deployment, is the suspected path behind the Grymmclúd siege CTD (#349).

**The service gate alone is NOT sufficient — gate the tick too.** `SmartCavalryAIMissionBehavior.OnMissionTick` also calls `ApplyCollisionAvoidance`, which writes `agent.SetMovementDirection` per mounted unit per frame **bypassing `ICavalryChargeService` entirely**; with `AvoidFriendlies` (default `true`) the feature kept manipulating cavalry every frame in a siege even with the service gated. The same gate therefore sits at the top of `OnMissionTick` (which also skips the per-formation adapter build). Deep-review 2026-07-16 HIGH — gating one layer did not gate the feature; see the lesson "Gating a feature OFF requires path enumeration, not layer gating" in `docs/reviews/lessons/gamemodels-services.md`.

**Live read — never cache.** `MissionTeamAIType` is assigned in `MissionCombatantsLogic.EarlyStart`, and the engine runs *every* `OnBehaviorInitialize` before *any* `EarlyStart` — so caching `IsFieldBattle` at init would read `NoTeamAI` 100% of the time and silently disable the feature in every battle.

**Caveat:** `OpenSiegeMissionNoDeployment` hardcodes `(MissionTeamAITypeEnum)1` = `FieldBattle` (`SandBoxMissions.cs:1582`, identical v1.4.6/v1.4.7), so relief-force / no-deployment siege assaults still run the feature — accepted (genuine maneuvering battles). See `docs/features/smart-cavalry-ai.md` + `docs/reviews/rca-siege-guards-2026-07-16.md`.

## Patch33_EquipPresets

**Target:** `SPInventoryVM.RefreshValues` (Postfix) + `GauntletInventoryScreen.OnInitialize` (Postfix) / `.OnFinalize` (Prefix)

Equipment-preset save/load overlay on the inventory screen. The `RefreshValues` Postfix captures the live VM into `IInventoryScreenAdapter.SetActive` so the preset menu can read active-hero/equipment-mode state and trigger a refresh after Load — hooked at `RefreshValues` (not the ctor) because the VM is constructed before `_currentCharacter` is finalized. Hot-ish path (fires every transaction tick while inventory is open): adapter + logger are lazily static-cached. The `OnInitialize` Postfix — settings-gated on `IEquipPresetsSettingsProvider.IsEnabled` — creates a new `GauntletLayer` at z-order **1000** (way above vanilla `InventoryScreen`'s 15) hosting `PresetsOverlay.xml` + a `PresetsOverlayVM` datasource, with `InputRestrictions` set so button clicks register but no focus steal; the `OnFinalize` Prefix removes the layer and clears the captured VM (a leaked layer on re-entry is logged defensively, not disposed). See `docs/features/equip-presets.md`.

## Patch34_QuickActions

**Target:** `SPInventoryVM.ExecuteSellAllItems` (Prefix), `SPInventoryVM` ctor (Postfix capture), `SPInventoryVM.RefreshCallbacks` (Postfix search-apply), `SPInventoryVM.OnFinalize` (Postfix clear)

Inventory "Sell All" multi-action menu + active-VM capture + per-save search-toggle apply + thread-static bypass for vanilla re-entry

## Patch35_CompanionTactics

**Target:** `PartyCharacterVM.RefreshValues` (Postfix), `OrderOfBattleHeroItemVM.RefreshValues` (Postfix), `OrderOfBattleVM..ctor` (parameterless, Postfix) + `.OnFinalize` (Prefix), `MissionGauntletOrderOfBattleUIHandler.OnMissionScreenTick` (Postfix) + `.OnMissionScreenFinalize` (Postfix), `Mission.OnTick(float,float,bool,bool)` (Postfix); plus `OrderOfBattleHeroItemVM.GetCaptainTooltip` (private — **manual** Postfix wired in `SubModule.cs` via `AccessTools.Method`, attribute binding can't resolve it) and the `CancelStanceOnMove` Postfix on `Formation.SetMovementOrder(MovementOrder)` which registers under the shared `Patch_MissionTime_SetMovementOrder` category (see that section)

CompanionTactics UI hooks, two halves. **Roles:** the `PartyCharacterVM.RefreshValues` Postfix appends the companion's role prefix to character names (thin — delegates to `IRoleTooltipDecorator`); the `OrderOfBattleHeroItemVM.RefreshValues` Postfix does the same on the OOB screen at `LowerThanNormal` priority so it runs AFTER Patch23's tooltip rewrite; the manual `GetCaptainTooltip` Postfix decorates the captain tooltip. **FormationPresets:** the `OrderOfBattleVM` ctor Postfix captures the new VM into a tracker (empty `MethodType.Constructor` form chosen so BUTR.Harmony.Analyzer resolves it statically), the `OnFinalize` Prefix clears it; the OOB UI-handler tick Postfix delegates to `IOOBOverlayService` which drives the false→true→false attach/detach cycle for the Save/Load/AutoAssign buttons overlay, and the handler-finalize Postfix detaches the layer; the `Mission.OnTick` Postfix is a near-no-op hot path (~60Hz, zero-alloc toggle check only — the donor mod's hotkey reads moved into `OOBButtonsVM` button commands). See `docs/features/companion-tactics.md`.

## Patch36_FiefManagement

**Target:** `MapScreen.OnFrameTick` (Postfix) + `GameStateScreenManager.CreateScreen` (Prefix)

Fief-management hub. The `OnFrameTick` Postfix polls `IMapScreenInputAdapter.IsF6Pressed` and opens the `fief_hub` game menu (`GameMenu.ActivateGameMenu`) when the player owns fiefs (a no-fief press shows an information message instead) — gated on the MCM `EnableFiefManagement` toggle and vanilla `MapScreen`'s FULL modal-suppression set (`MapState` active, not in menu / battle simulation / army management / marriage offer / heir selection / map cheats / map incident / overlay context menu / encyclopedia — Codex review #38b caught that an `IsInMenu`-only guard let F6 fire during modals that read the swapped `MainParty.CurrentSettlement`). The `CreateScreen` Prefix intercepts `FiefManagementGameState` and returns TAOM's `GauntletFiefManagementScreen` (with `IRemoteFiefSettlementSwapper`), skipping vanilla screen creation; all other states pass through. See `docs/features/fief-management.md`.

## Patch37_CrashReport

**Target:** 9 Finalizers on engine lifecycle methods — `Managed.ApplicationTick`, `ScriptComponentBehavior.OnTick`, `Module.OnApplicationTick`, `MissionView.OnMissionScreenTick`, `ScreenManager.Tick`, `ScreenManager.Update()` (no-arg inner overload, explicitly disambiguated), `Mission.Tick`, `MissionBehavior.OnMissionTick`, `MBSubModuleBase.OnSubModuleLoad` — plus a dev-trigger Postfix on `Module.OnApplicationTick` (`CrashReportApplicationTickTrigger` in `DevTriggers/`, priority 900 so its MCM-toggled throw lands INTO the Finalizer) and reflection-attached Finalizers on every `*CallbacksGenerated` method via `Native2ManagedPatcher` at runtime (hundreds of methods)

TAOM's crash-capture pipeline. Every Finalizer (all at `[HarmonyPriority(800)]` — matching BetterExceptionWindow's published tier so co-installed ordering is deterministic; the service's `TrySuspend` on BUTR's handler makes co-existence rare) routes the exception through `CrashReportPatchHelper.HandleAndSwallow`, which captures the report and swallows (returns null) so the game continues. Everything shares this one category so `_harmony.UnpatchCategory("Patch37_CrashReport")` detaches the lot in one call. MUST register FIRST in `SubModule.OnSubModuleLoad` (immediately after `IoC.Configure()`) to maximise coverage of other mods' `OnSubModuleLoad` throws — see the chicken-and-egg caveat in `docs/features/crash-report.md`.

## Patch38_SettlementNameplateFade

**Target:** `SettlementNameplateWidget.DetermineTargetAlphaValue` (Postfix)

Distance-based settlement nameplate fade on the campaign map — Postfix multiplies vanilla target alpha by [0,1] fade factor derived from `DistanceToCamera`. MCM-tunable near/far band (default 80..200), master toggle. Hot path (~3000 calls/sec): service captured once via `Initialize` static-field; settings provider caches `TaomSettings.Instance` reference in its ctor.

## Patch39_BanditPartySize

**Target:** `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` (Postfix)

PlayerProgress-scaled bandit party sizes (the roster half of BanditManagement — the density levers live in `TaomBanditDensityModel`, no patch). Vanilla builds the initial roster by rolling `num = MinValue + (MaxValue − MinValue) × ratio` per `PartyTemplateStack`, with the (private) ratio function returning 0.4–1.2 for bandits and vanilla asserting `ratio ≤ 1.0` — so the ratio itself can't be postfixed. Instead, after the roster is built, this Postfix walks each template stack and scales its troop count UP by `IBanditScalingService.GetPartySizeMultiplier(Campaign.Current.PlayerProgress)`, capping at the stack's `MaxValue` — respecting the upper bound the party templates already encode while letting endgame bandit parties reliably hit full templated strength instead of the random vanilla draw. Non-bandit parties (player, lords, caravans, villagers, patrols) and multipliers ≤ 1 pass through untouched; service-disabled = vanilla. See `docs/features/bandit-management.md`.

## Patch40_HideoutDescription

**Target:** `HideoutCampaignBehavior.game_menu_hideout_place_on_init` (private, Postfix)

Themed LOTR hideout encounter descriptions — Postfix re-sets the `HIDEOUT_DESCRIPTION` GameText var for TAOM's 5 bandit cultures, replacing vanilla's hardcoded-culture default "(Undefined hideout type)". Delegates to `IHideoutDescriptionService` (string→string, ADR-007 clean); runs before menu render so the lazy `{HIDEOUT_DESCRIPTION}` substitution picks up the override. Only `hideout_place` shows the var; `hideout_after_wait` needs no patch.

## Patch41_McmLayoutFix

**Target:** `WidgetFactoryManager.CreateAndRegister(string, XmlDocument)` (UIExtenderEx assembly, Postfix)

Repairs MCM's inverted options-screen layout (the v1.4.0 `VerticalBottomToTop` regression). MCM registers its embedded options-screen prefabs through this method; the registered `Func<WidgetPrefab>` closes over the SAME `XmlDocument` reference and parses it lazily at first screen-open, so mutating the document in this Postfix — `McmLayoutRewriter.FlipMcmLayout`, which flips the inverted layouts — repairs the screen before that parse. A Harmony Postfix instead of UIExtenderEx's `[PrefabExtension]` because MCM's embedded prefabs load via UIExtenderEx's `LoadFromDocument` reverse-patch, which never runs the `ProcessMovie` step that applies PrefabExtension patches — a PrefabExtension on these movies is a silent no-op. Timing: registered from `SubModule.OnSubModuleLoad`, which completes for all modules before MCM's `ResourceInjector.Inject()` runs at `OnBeforeInitialModuleScreenSetAsRoot`, so the Postfix is attached before MCM calls `CreateAndRegister`. Cosmetic fix — any exception is logged and swallowed so MCM screen registration never breaks. No dedicated feature doc — root-cause analysis lives in `McmLayoutRewriter` + the patch file (`Main/Features/Mcm/Hooks/Patch41_McmLayoutFix.cs`).

Feature doc: `docs/features/mcm.md` (authored 2026-07-12).

## Patch42_CastleRecruitment

**Target:** `AiVisitSettlementBehavior.AiHourlyTick` (Transpiler), `AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays` (Transpiler), `RecruitmentCampaignBehavior.HourlyTickParty` (Postfix)

Castle troop recruitment — AI half (player menu + notable spawn/fill + issue suppression live in `CastleRecruitmentBehavior`, not Harmony). Two transpilers swap the single `!settlement.IsCastle` AI-scoring gate for a runtime toggle (`CastleAiToggle.IsCastleAndAiDisabled`, same Settlement→bool stack shape) so AI lords score + travel to castles like towns; a postfix invokes the private `CheckRecruiting` for an AI party in a non-besieged castle (bound once to an open delegate — no per-call alloc). Transpilers pin to the FIRST `get_IsCastle` + require a nearby anchor (`GetAvailableWageBudget` / `IsSettlementSuitableForVisitingCondition`), else fail-safe to vanilla.

## Patch43_BattleLoadDiagnostics

**Target:** 14 hooks — battle-load phase stamps: `PlayerEncounter.Start` (Postfix), `MissionState.OpenNew` (Prefix+Postfix), `DefaultSceneModel.GetBattleSceneForMapPatch` (Postfix), `MissionState.LoadMission` (private, Prefix), `Utilities.ClearOldResourcesAndObjects` (Prefix+Postfix), `Mission.Initialize` (public, Prefix), `Mission.AfterStart` (Prefix+Postfix), `Agent.EquipItemsFromSpawnEquipment` (Prefix+Postfix); mission-EXIT phase stamps: `Mission.EndMission` (Postfix), `Mission.EndMissionInternal` (Prefix+Postfix), `Mission.ClearUnreferencedResources` (Prefix+Postfix), `MissionState.OnFinalize` (Prefix+Postfix), `MapState.OnActivate` (Postfix), `MapState.OnTick` (Postfix)

Always-on `[BattleLoad]` diagnostics: phase-stamps the entire attack → battle-playable lifecycle (and the mission-exit phase) to `Logs/taom_debug_*.log`, paired with a background-thread stall watchdog — when a battle hangs on the loading screen (no crash, no stack trace — main thread blocked, so the CrashReport pipeline never fires), the last line before the freeze names the stuck phase. `MissionState.OpenNew` is the single funnel for every mission (scene name + attacker/defender/sizes/player side); the `Agent.EquipItemsFromSpawnEquipment` pair is the money hook — the Prefix logs the agent's full `SpawnEquipment` loadout BEFORE the engine equips, so an `AgentEquipBegin` without a matching `AgentEquipOk` names the agent + item whose `bo_` collision mesh is missing (a confirmed hang cause, #352; companion tool `docs/features/mesh-ref-validation.md`). The exit-phase stamps (EndMission → ClearUnreferencedResources → OnFinalize → MapState reactivation) fed the #331 tournament-exit RCA (see Patch60). Phases coexist with the pre-existing Patch16 / Patch23 hooks on the shared targets. See `docs/features/battle-load-diagnostics.md`.

**2026-07-16 — the OpenNew→Initialize segment stamps.** The `OpenNew` hook is a *Prefix*, so for a long time everything from `OpenNew`'s body through the tick boundary to `Mission.Initialize` was one dark window: a Nan Angren player CTD (v2.0.12) left a log ending at `MissionOpenNew` that could not distinguish a fault in `OpenNew`, in `LoadMission`, or in the native resource clear. Four targets split it: an `OpenNew` **Postfix** (`MissionOpenNewDone` — its absence means the fault was inside `OpenNew`); the private `MissionState.LoadMission` **Prefix** (`LoadMissionBegin`, the next tick); `Utilities.ClearOldResourcesAndObjects` **Prefix+Postfix**, the one *native* call in the window and the shape that access-violates (one caller in the shipping build, so no noise); and `Mission.AfterStart` **Prefix+Postfix**, which brackets *every* submodule's `OnMissionBehaviorInitialize` — the gap from `MissionAfterStartBegin` to `TaomBehaviorsBegin` is other mods' work, which is what lets a report **exonerate** TAOM rather than only accuse it. TAOM's own 11 behaviors are stamped by name (`TaomBehaviorAdded`) from the `AddTaomBehavior` helper in `SubModule.OnMissionBehaviorInitialize`, not by a patch.

Applied in `OnGameInitializationFinished`, now **try/catch-guarded** like Patch60/61/62: the category binds a private method by string, and a diagnostics category must never take startup down on engine drift. Note `Mission.Initialize` is **public** (`Mission.cs:1798`) — this entry described it as private until 2026-07-16; the binding is by string either way, so the error was cosmetic.

## Patch44_CCNameAutofill

**Target:** `CharacterCreationReviewStageVM..ctor` (Postfix)

Pre-fills the CC Review-stage "Enter your name" field with a culture-appropriate first name when blank — Postfix on `CharacterCreationReviewStageVM`'s 6-arg constructor calls the VM's own public `ExecuteRandomizeName()` (draws from `SelectedCulture` + `Hero.MainHero.IsFemale`) only when `Name` is empty, so a typed name is never clobbered and the field stays editable. Runs at the Review stage because gender is finalized there. Companion to the family-name fix in `FactionMap.CultureSettingService` (assigns `Hero.MainHero.Culture` before `SetSelectedCulture` so the clan name comes from the selected culture's `<clan_names>`, not the stale default — that part is a service edit, not a patch).

## Patch46_TournamentDwarfDismount

**Target:** `TournamentFightMissionController.PrepareForMatch` (Postfix)

Dwarf tournament dismount — Postfix on the public `TournamentFightMissionController.PrepareForMatch` (SandBox.dll). The horse comes from the culture tournament *weapon template* (`CultureObject.TournamentTeamTemplatesFor*Participant` / `tournament_template_empire_*`) cloned into `participant.MatchEquipment`, NOT from `GetParticipantArmor` (which only fills armor slots 5–9 via `AddRandomClothes`). The postfix iterates `____match` (the private `_match` field; **four** underscores = Harmony's `___` prefix + `_match`) teams/participants and, for any participant whose race `ITournamentService.ShouldDismountInTournament` returns true (currently dwarves — custom skeleton clips inside the mount), clears `EquipmentIndex.Horse` + `HorseHarness` (`AddEquipmentToSlotWithoutAgent(slot, EquipmentElement.Invalid)`). Single chokepoint covers both the visual spawn (`SpawnAgentWithRandomItems`) and AI `Simulate`. Keyed on race (not culture) — catches a dwarf in any town + the player. Decision uses validate-before-lookup via `IRaceManager` (`IsValidRaceId`→`GetRaceNameFromId`→case-insensitive `dwarf`); resolves race through the same `IRaceManager` as `EyeHeightAdjustmentHook`, plus the `IsValidRaceId` guard that hook lacks. Lazy-resolve like Patch40.

## Patch47_SpiderDeathDismount

**Target:** `Agent.Die` (Prefix)

Spider rider-death AV mitigation. A rider dying seated on the non-vanilla spider mount AVs inside native `Agent.Die` (1.4.6 melee-death: Die-path reads float-bits-as-index from a corrupted action record, debugger-proven). Prefix hard-dismounts via the engine's private `SetMountAgent(null)` (cached `AccessTools`) so the rider dies the proven on-foot death; a dying spider frees its rider first. Spider-only; body try/catch'd.

## Patch48_SpiderHitDismountGuard

**Target:** `Agent.HandleBlowAux` (private, Prefix)

Non-lethal sibling of Patch47 (debugger-proven + in-game-confirmed 2026-06-15). A finite real-melee `CanDismount` hit on a SURVIVING mounted Spider Rider AVs inside native `Agent.HandleBlowAux` reading `0x3` (`MeleeHitCallback -> Mission.RegisterBlow -> Agent.RegisterBlow -> HandleBlow -> HandleBlowAux`). Same broken non-vanilla mounted-dismount path; Patch47 only covers death. Prefix strips `BlowFlags.CanDismount` when the victim's mount is the spider Monster -> native dismount never fires, rider stays on the locked mount, damage still applies. Delegates `IsSpiderMonster` to `ISpiderAttackService`. Spider-only (elephant mahout latent).

## Patch49_ArmyGatheringNreGuard

**Target:** `Army.FindBestGatheringSettlementAndMoveTheLeader` (private, Finalizer)

Map-tick CTD guard (crash report 2026-06-17). Vanilla `Army.FindBestGatheringSettlementAndMoveTheLeader` NREs at `settlement.GatePosition` (Army.cs:726, v1.4.6) when a besieger army can't resolve a gathering fortification, or at `Kingdom.Settlements` (line 659) when the army leader's clan is kingdomless — fired from `Army.OnSiegeStarted` during an AI siege start. No TAOM patch is on the stack; `Patch22_ArmyTargeting`'s aggressive cross-map siege steering just makes it more reachable. Finalizer swallows ONLY `NullReferenceException` → the army skips relocating its gathering leader this tick (vanilla already null-guards `AiBehaviorObject` downstream at Army.cs:480-490/564) and re-plans next tick. **Diagnostics (2026-07-05):** before swallowing, the finalizer records the failure to `ISiegeGatheringDiagnosticsService` (boundary DTO `SiegeGatheringFailureInfo.FromArmy` → classify `KingdomNull`/`NoFortifications`/`AllFortificationsUnderSiege`/`NoReachableFortification` → dedup by `(kingdom, focus settlement)`, first hit = WARNING with army/kingdom/focus + fortification census, repeats = DEBUG) so dead-end sieges are a reviewable `[SiegeDiag]` list in `Logs/taom_debug_*.log`; the diagnostic path is inside the try/catch so the crash guard is never weakened. A managed-NRE Finalizer still surfaces as a first-chance exception under a debugger — expected. Lives in `Main/Features/ArmyTargeting/{Hooks,Diagnostics}/`.

## Patch50_DropFlaggedItemGuard

**Target:** `Agent.CheckToDropFlaggedItem` (public, Finalizer)

Warg-on-warg bite NRE guard (crash report 2026-06-17; caught, non-fatal log spam). The shared synthetic-bite path (`CustomAttacksUtils.TakeDamage` → `Mission.RegisterBlow` → `Agent.HandleBlow` → `Mission.OnAgentHit`) calls `affectedAgent.CheckToDropFlaggedItem()` (Mission.cs:5609) on the victim; when the victim is a non-vanilla mount (a warg biting another warg) it passes the `CanWieldWeapon` guard but `Equipment[wieldedIndex].Item` is null → `.ItemFlags` NRE (Agent.cs:3595). Finalizer swallows ONLY `NullReferenceException`; damage is applied upstream in `HandleBlow` so the bite still lands, and the only skipped effect (a flagged-item drop) doesn't apply to a mount. Covers warg + spider. Lives in `Main/Features/AdvancedCombat/Hooks/`.

## Patch51_RecruitmentResourceGate

**Target:** `RecruitmentVM.RefreshPartyProperties` (Postfix)

SpecialResources — gates the recruit-volunteers Done button on the player's special-resource balance. Vanilla `RefreshPartyProperties` sets `IsDoneEnabled` from gold; this Postfix ANDs in a resource check so a troop the player can't afford in resources (e.g. an elephant/spider costing `war_drums` / `war_spoils`) blocks the recruit. It groups the recruit cart (one entry per unit) into troopId→count, evaluates via `IOnRecruitmentResourceGate.EvaluateCart(heroId, kingdomId, cultureId, entries)`, and on a block sets `IsDoneEnabled = false` + swaps the `DoneHint` to "Requires {AMOUNT} {RESOURCE}". Only ever forces the flag FALSE — the gold gate is preserved, and an already-disabled button is left alone. This is the block half only; the actual deduction happens on `OnUnitRecruitedEvent` in `SpecialResourcesBehavior`. Hook + logger injected once via `Initialize` (no per-call IoC resolve). See `docs/features/special-resources.md`.

## Patch53_PartyIconScale

**Target:** `MobilePartyVisual.AddCharacterToPartyIcon` (private, Transpiler)

Campaign-map party-icon figure/mount scale. Transpiler rewrites the two hardcoded `0.3f` scale literals in `MobilePartyVisual.AddCharacterToPartyIcon` (people = `ldc.r4 0.3`→`callvirt Scale`; mount = `ldc.r4 0.3`→`mul`) into a `call PartyIconScaleConfig.GetScale()` so both honour the MCM "Map Figure Scale" slider (default 0.15 = half vanilla 0.30, range 0.05–1.0; `FiniteFloatValidator`-guarded). Stack-neutral in-place swap (labels preserved); animation-math `/0.3f` (`div`) literals not matched; missing-site fail-safe (warn, keep vanilla, never throw). Static IL-call-target pattern mirrors `CastleAiToggle`. Coexists with the BannerColorPersistence Postfix on the same method. `Main/Features/PartyIconScale/`. See `docs/features/party-icon-scale.md`.

## Patch54_NavalTravelBoatVisual

**Target:** `MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents` (Postfix ×2, SandBox.View)

**PARKED 2026-06-26 — not applied (commented out in `SubModule.cs`, #120/#296).** Renders an at-sea party as a boat — the base game renders NO ship at sea without `NavalDLC.View` (it omits the leader figure + adds nothing). Two Postfixes share `UpdateBoat`: `OnTransitionEnded` drives add/remove on the embark/disembark (the at-sea change does NOT trigger an icon rebuild, so the rebuild hook alone never saw it), `AddMobileIconComponents` re-adds on rebuild. Adds the base-game `Native` `boat_sail_on` mesh (also `map_icon_ship`; no DLC) scaled `boatScale` to the party's `StrategicEntity`, tag-idempotent (`taom_naval_boat`). `Main/Features/NavalTravel/Hooks/`. NavalTravel feature #296.

## Patch55_BasicTableauRaceGuard

**Target:** `BasicCharacterTableau.RefreshCharacterTableau` (private, Prefix)

Main-menu Save/Load hero-preview CTD guard for custom races. `BasicCharacterTableau` (instantiated only by `SaveLoadHeroTableauTextureProvider`) builds the preview body via the agentless native `MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals(entity, SkinGenerationParams{_race}, …)` on the hardcoded human skeleton; for a TAOM custom-race save the native static-morph build access-violates — the custom-race head meshes lack the per-face-component morph data vanilla heads carry (same class as the Erebor-arena crash, issue #295). A native AV can't be try/caught, so the fix is preventive: the Prefix coerces the private `_race` field (injected as `ref int ____race` — Harmony's `___` prefix + `_race`) to a render-safe race via `IBasicTableauRaceGuard.ResolveSafeRace` (ADR-002/007), an EMPIRICAL per-race allow-list keyed by race NAME (ids shift with skins.xml merge order): races proven to render pass through true-to-race (uruk verified 2026-07-02), dwarf is proven unsafe (#295), every unverified race coerces to the human base — the preview shows a human head with correct equipment until the race is render-verified or its morph data is authored asset-side. Scope is narrow by construction: only the Save/Load preview uses `BasicCharacterTableau`; the in-game tableaus use the AgentVisuals path and are untouched. CRITICAL timing (Codex C1, issue #299): its OWN category applied from `SubModule.OnBeforeInitialModuleScreenSetAsRoot` (process-static one-shot) — NOT the sibling `Patch2_RefreshTableau` batch in `OnGameInitializationFinished`, which is too late because the save list renders on the COLD main menu. Guard injected once via `Initialize` from HeroRaceIoC. Owning feature: HeroRace — see `docs/features/hero-race.md`.

## Patch56_SceneNotificationVisualGuard

**Target:** `GauntletSceneNotification.OpenScene` (private, Finalizer) + `.OnTick` (Postfix, deferred close) + `PopupSceneSpawnPoint.InitializeWithAgentVisuals` (public, diagnostic Prefix)

Become-king (and sibling) cinematic CTD guard (crash reports 2026-06-24/25 — become ruler of a kingdom). Becoming ruler raises the engine's `BecomeKingSceneNotificationItem` (`scn_become_king_notification`, from `DefaultCutscenesCampaignBehavior.OnKingdomDecisionConcluded`), which renders ~20 culture characters through the raw scene-notification path `GauntletSceneNotification.OpenScene` → `PopupSceneSpawnPoint.InitializeWithAgentVisuals`. That engine method derefs the human `AgentVisuals` with NO null guard (`PopupSceneSpawnPoint.cs:91/92` + the unconditional else `:108/109` — `_humanAgentVisuals.GetEquipment().Clone(false)`); the mount IS guarded (foot characters), so the asymmetry is the engine bug. One character's null/unbuildable visual NREs (managed `System.NullReferenceException`, HResult 0x80004003) → CTD. **Finalizer** on the private `OpenScene` swallows ONLY `NullReferenceException` (returning to `OnTick` lets `:135` `_isPendingSceneLoad=false` run → no re-crash loop), so a cinematic that CAN render still plays and one that would crash aborts. **Deferred close** (deep-review MED): the finalizer does NOT call `HideSceneNotification()` synchronously — that would release input/focus only for `OnTick:127-129` to re-lock them one line after the swallowed `OpenScene` returns (campaign-map soft-lock). Instead it raises a `CloseRequested` flag consumed by a sibling **Postfix on `OnTick`** that runs `MBInformationManager.HideSceneNotification()` AFTER the OnTick body, so the input/focus release wins. Generic by design — also covers KingdomCreated/JoinKingdom/Marriage/death notifications. Fourth raw custom-race/visual render path (after Patch55). Registered in `SubModule.OnGameInitializationFinished` (campaign-only cinematic). Companion **diagnostic Prefix** on `PopupSceneSpawnPoint.InitializeWithAgentVisuals` replicates the engine's own first derefs (`GetCopyAgentVisualsData()` then `GetEquipment()`) and logs which fails (pure logging) so the next occurrence self-identifies the culprit. `Main/Features/HeroRace/Hooks/`.

## Patch57_NavalAtSeaLandRescueGuard

**Target:** `AIMoveToNearestLandBehavior.AiHourlyTick` (internal, Prefix)

**PARKED 2026-06-26 — not applied (commented out in `SubModule.cs`); unnecessary while the model is unregistered (nothing reaches sea), needed again on re-enable.** Native-AV CTD guard for NavalTravel (#296; crash report 2026-06-25). Enabling naval travel lets a party reach `IsCurrentlyAtSea`, which activates the vanilla `AIMoveToNearestLandBehavior.AiHourlyTick` (inert in vanilla TAOM — nothing ever reaches sea). It calls the native cross-region land-pathfind `MapScene.GetNearestFaceCenterForPositionWithPath` (`maxDist=MapDiagonal/2`, `excludedFaceIds=GetInvalidTerrainTypesForNavigationType(All)={7,13,14,21,22}`), which dereferences the naval region-map navmesh **TAOM_Map never builds** (#120) → `0xC0000005` reading `0x4` on the hourly AI tick, for ANY at-sea party (AI, and the player once sailing works). A native AV is a corrupted-state exception a managed Finalizer can't reliably catch (unlike Patch49/50's managed-NRE finalizers), so the fix is the **prevent-the-call Prefix** pattern of Patch47/48: skip the behavior while the feature is enabled. Player disembark is unaffected (it routes through `CanPlayerNavigateToPosition`, not this behavior); non-at-sea parties already early-return, so the only behavioral change is preventing the crash. Targets the internal vanilla type by name (`AccessTools.TypeByName`, drift-safe: a bind failure logs + no-ops rather than failing module load); decision = pure `INavalTravelService.ShouldSuppressAtSeaLandRescue` (= `IsEnabled`). `Main/Features/NavalTravel/Hooks/`.

## Patch58_SkipCampaignIntro

**Target:** `SandBoxGameManager.OnLoadFinished` (public override, Prefix)

Skip the vanilla SandBox campaign intro video (`Modules/SandBox/Videos/CampaignIntro/campaign_intro.ivf`) on a NEW game → straight into character creation. Prefix mirrors the engine's own `IsDevelopmentMode` no-video branch: for `!__instance.LoadingSavedGame` it invokes the private `SandBoxGameManager.LaunchSandboxCharacterCreation()` (the dev-mode bypass) + sets the method's trailing `MBGameManager.IsLoaded=true` (both bindings `AccessTools`-cached at type-load, exposed `internal` for drift-guard tests), then returns false so the original never builds/pushes the `VideoPlaybackState`. Save-game loads return true (the engine's separate save-load branch runs untouched); any binding-null or thrown exception returns true (fail-safe → vanilla intro plays, never breaks new-game start). **Hardcoded always-skip, no MCM toggle.** Applied in `SubModule.OnSubModuleLoad` (process-static one-shot) — NOT the late `OnGameInitializationFinished` batch — because the target fires during the new-game load sequence (after campaign init, before character creation), so the patch must be attached before any new game can start; `SandBox.dll` is a `LoadBeforeThis` dependency so the type is patchable that early. Bindings verified against installed 1.4.6; 2 drift-guard tests + `HarmonyPatchBindingTests`. `Main/Features/SkipCampaignIntro/`. See `docs/features/skip-campaign-intro.md`.

## Patch59_CaravanTrade

**Target:** `CaravansCampaignBehavior.CanTradeWith` + `.GetTradeScoreForTown` + `.GetDistanceLimitVeryFarAsDaysForNavigationType` + `.CalculateBudgetFactor` (all private, Postfix ×4)

CaravanTrade — four postfixes on `CaravansCampaignBehavior` private methods that lift the local-cluster shuttle so caravans range further, trade across the war, and carry fuller baskets. `CanTradeWith` (war-gate: flip a war-caused `false→true` per `WarTradePolicy`, sides via `IAlignmentService.GetKingdomSide`+culture-fallback, honors the player prohibited-kingdom list during war); `GetTradeScoreForTown` (strip vanilla's `1/days` distance spike, re-apply a softer clamped curve, cut the just-left town's score — selection-only, profit/payout untouched); `GetDistanceLimitVeryFarAsDaysForNavigationType` (scale-on-read range-ceiling widen — reads the MCM master toggle live so master-off reverts instantly; engine-global); `CalculateBudgetFactor` (floor the per-caravan budget factor so poor caravans buy more than one good). All delegate to pure `ICaravanTradeService`; every hook try/catch-degrades to vanilla; master-off = exact vanilla. See `docs/features/caravan-trade.md`.

## Patch60_TournamentExitMovieRelease

**Target:** `MissionGauntletTournamentView.OnMissionScreenFinalize` (public override, SandBox.GauntletUI.dll, Prefix+Postfix)

Arena — tournament-exit hang fix, round 1 of 2 (#331; **necessary-not-sufficient** — the 104-109s stall moved WITH the relocated release; the round-2 real fix is the PatchShield GauntletUI exclusion in TAOM.Dependencies + the `ExitStallSampler` that named it, see the RCA round-2 section; measured post-fix exit 9.5s, the per-exit `ReleaseMovie=Nms` stamp is the regression canary). Engine defect: `MissionGauntletTournamentView.OnMissionScreenFinalize` nulls `_gauntletMovie`/`_gauntletLayer` WITHOUT ReleaseMovie/RemoveLayer (practice view releases correctly), deferring the 'Tournament' movie teardown — the only mission UI with live item/character tableau widgets (prize, round weapon icons, winner panel), usually with a prize render in flight at exit — into `ScreenBase.HandleFinalize`'s layer loop under the exit loading screen (frame pump dead → ~108s stall, +8,276 gen0 GCs; native scene clear = 4ms). Capture-Prefix (fields via `AccessTools.Field`, original body nulls them) + release-Postfix (AFTER the body drops focus + finalizes the VM — Prefix-release would NRE in `TryLoseFocus`) replicating the practice view's `ReleaseMovie`→`RemoveLayer` at `OnEndMission` time while the mission renderer still services tableau work. Fail-safe → vanilla leak. 2 drift-guard tests pin the private fields. RCA: exit-phase diagnostics (Patch43) + 22-agent adversarial decompile, all TAOM code exonerated. See `docs/features/arena.md`.

## Patch61_SaveLoadDiagnostics

**Target:** save/load pipeline Finalizers/Postfixes (see the feature doc)

Always-on `[SaveLoad]` lifecycle logging to `Logs/taom_debug_*.log` for the "corrupted save" investigation — the engine swallows the real load exception behind the generic "A problem occured while trying to load the saved game." dialog (`LoadContext.Load` catches + prints only `ex.Message`). 15 thin hooks in 4 categories (`Patch61_SaveLoadDiagnostics` + one isolated per-internal-type reflection category). Interior Finalizers (all **void** + `Priority.First` — SaveShield in TAOM.Dependencies swallows at 4 overlapping methods, so ours must observe first) stamp the exact failing type/`SaveId.GetStringId()`/chunk at the graph throw sites (`LoadContext.CreateLoadData`, `ContainerLoadData.*`, header readers, `ArchiveDeserializer.LoadFrom`); unknown-SaveId detection (`ObjectHeaderLoadData.CreateObject` / `ContainerHeaderLoadData.GetObjectTypeDefinition` — engine silently null-fills); per-behavior SyncData attribution (`CampaignBehaviorDataStore.{Load,Save}BehaviorData`); save-WRITE faults on the async thread (`FileDriver.Save` / `SaveOutput.PrintStatus` — the #292 class + AV/OneDrive write blocks); `TAOM_Build` metadata stamp (`MBSaveLoad.GetSaveMetaData`) so every save self-identifies its build. Applied in `OnSubModuleLoad` (Patch58 precedent — loads fire from the main menu). Offline companions `tools/inspect_sav.py` + `tools/repair_sav_strings.py`. This stack root-caused the v2.0.9 momentum >32 KB corruption (RCA `docs/reviews/rca-momentum-save-corruption-2026-07-07.md`). `Main/Features/SaveLoadDiagnostics/`. See `docs/features/save-load-diagnostics.md`.

The three internal-engine-type hooks below are ISOLATED sub-categories of this feature (Harmony aborts a whole category on the first failing class, so a reflection-bind failure on one internal type must not kill the other 12 hooks). `SubModule.OnSubModuleLoad` applies each in its own try/catch right after the main category.

## Patch61_SaveLoadDiagnostics_ArchiveParse

**Target:** `ArchiveDeserializer.LoadFrom(byte[])` (internal engine type, bound via `AccessTools.TypeByName` + `TargetMethod()`; void Finalizer, `Priority.First`)

Attributes raw archive-chunk parse faults (review 2026-07-07 MED). Every raw archive parse — header chunk, strings chunk, per-object chunk, per-container chunk — passes through `ArchiveDeserializer.LoadFrom(byte[])`; a bit-flipped or truncated chunk that survived the deflate faults here first. The Finalizer stamps `kind=archiveParse chunkBytes=N` to `ISaveLoadDiagnosticsService.LogFault(GraphFault, …)` and rethrows (void Finalizer): the byte length distinguishes "tiny/empty chunk" (truncation) from "full-size chunk with garbage" (bit corruption). Bind failures log a `[SaveLoad]` engine-drift warning and self-disable instead of failing module load. See `docs/features/save-load-diagnostics.md`.

## Patch61_SaveLoadDiagnostics_BehaviorData

**Target:** `CampaignBehaviorDataStore.LoadBehaviorData` + `.SaveBehaviorData` (internal engine type, bound via `AccessTools.TypeByName` + `TargetMethods()`; void Finalizer, `Priority.First`)

Per-behavior SyncData attribution, BOTH directions. Load: the engine reads every behavior's data via a raw `(T)value` cast (`BehaviorSaveData.SyncData`) with NO per-behavior try/catch — an `InvalidCastException` (a SyncData type changed between the writing and loading builds) aborts campaign start with zero context about WHICH behavior. Save: the collection pass (`SaveBehaviorData`, fired from `OnBeforeSave` BEFORE `SaveManager.Save`) has the same shape — a duplicate-record or serializer fault names no behavior. The Finalizer stamps direction + the behavior's full type name (`BehaviorSyncFault`) then rethrows. Per-method bind failures log which direction is uninstrumented. See `docs/features/save-load-diagnostics.md`.

## Patch61_SaveLoadDiagnostics_ContainerFill

**Target:** `ContainerLoadData.InitializeReaders` / `.FillCreatedObject` / `.Read` / `.FillObject` (internal engine type, bound via `AccessTools.TypeByName` + `TargetMethods()`; void Finalizer, `Priority.First`)

The money hook for CONTAINER data — dictionaries and lists, which is where TAOM's growing SyncData surfaces live (`_taom_heroRaceMap`, `_taom_specialResources`, the career dicts). `LoadContext.Load` fills containers inline under TWParallel (NOT via `CreateLoadData`), so the object-side hook never sees these faults. `InitializeReaders` is included because it runs per container BEFORE the fill methods and its raw dictionary-indexer entry lookups are the FIRST throw site for a truncated/corrupted container chunk (review 2026-07-07 HIGH). The Finalizer stamps `kind=container step=<method> saveId/type/elements` from the once-cached `ContainerHeaderLoadData` property, then rethrows. Own category so drift here can't kill any other hook. See `docs/features/save-load-diagnostics.md`.

## Patch62_MovieReleaseAvGuard

**Target:** `GauntletMovie.Release` (public, TaleWorlds.GauntletUI.Data, Finalizer)

Arena — heap-corruption CTD containment (#339; v2.0.12 player report, signature `4698b4d4`). A tournament exit AV'd inside `WidgetFactory.IsCustomType` → `Dictionary.FindEntry` during the recursive `WidgetTemplate.OnRelease` walk of the Tournament movie — first in Patch60's early release (caught by its fail-safe), then again UNCAUGHT when `GauntletLayer.ClearContext` re-walked the same corrupt tree releasing the leaked movie at `ScreenManager.PopScreen`. The corruption pre-dates mission end (a corrupt template-tree string; prize tableau render in flight at exit, the #331 round-1 fingerprint) and is engine/native territory — this guard is containment, not root cause. Finalizer swallows ONLY `AccessViolationException` (catchable process-wide: the launcher config sets `legacyCorruptedStateExceptionsPolicy`), logs a WARNING with the movie name, and propagates everything else. Suppressing on the first (Patch60 → `GauntletLayer.ReleaseMovie`) attempt also removes the movie from `_movieIdentifiers`, so the fatal pop-time re-walk never sees it. Cost of a suppression: one bounded leaked movie (skipped `PrefabChange`/`BrushChange` unhook + `IsReleased` flag; `GauntletLayer.OnFinalize`'s "not released" `Debug.FailedAssert` is a no-op in shipping; the leaked `PrefabChange`/`BrushChange` subscriptions are provably inert in a shipping client — the producer side, `ResourceDepot.CheckForChanges`, only runs under `_uiDebugMode`, deep-review 2026-07-13). **Applied in `OnSubModuleLoad`** (Patch58/Patch61 precedent), NOT the late batch: `GauntletMovie.Release` runs for every movie in the process — main menu, character creation, load screen — all before game init (the #299 apply-timing lesson; deep-review Flow-6 finding, fixed in-session). **Engine-bump re-check:** `GeneratedGauntletMovie.Release()` (the AutoGenerated-prefab movie class) is NOT covered by this guard — verified benign on 1.4.7 because its release has no `WidgetTemplate.OnRelease` walk and no `WidgetFactory.OnUnload`; re-verify that on engine bumps. Cold path — once per movie lifetime. Lives in `Main/Features/Arena/Hooks/`.

## Patch63_BannerBearerSpawnGuard

**Target:** `BannerBearerLogic.SpawnBannerBearer` (public, TaleWorlds.MountAndBlade, Prefix skip-original)

BannerBearers — reinforcement-bearer AV guard (#360; siege of Glad Thaw CTD, signature `67b75cb4`, TAOM v2.0.13). The engine method spawns a reinforcement troop with the formation banner, then **unconditionally** reads the agent's ExtraWeaponSlot native weapon entity through an unvalidated P/Invoke (`Agent.cs:2708`) — a `0xC0000005` when the slot record is absent. The reinforcement path (unlike deployment, which never runs this code) installs the banner via `Mission.SpawnTroop`'s validating gate and wields initial weapons natively; either can leave slot 4 empty (likely trigger: 2H replacement sidearm forcing a native drop of the `DropOnWeaponChange` banner — fixed in data, pinned by `BannerBearerReplacementWeaponDataTests`). The prefix reimplements the engine's five statements (caller discards the return value — declining bookkeeping is always safe) adding: a **toggle-folded** race/formation-group eligibility gate (`IsReinforcementBearerAllowed`; disabled ⇒ allowed ⇒ vanilla parity — the deep-review Flow-4 finding; the engine's reinforcement path consults no per-agent policy, so this also closes the troll-as-bearer gap), a **managed slot-4 check** before the native read (anomaly ⇒ mechanism-naming WARN instead of CTD — the standing Iron-Law confirmation channel), and an **AV-only catch** on the clean-path read (Patch62 precedent). Private members (`GetFormationControllerFromFormation`, nested `FormationBannerController` + `BannerItem`/`Formation`/`OnBannerEntityPickedUp`, `AddBannerEntity`) reached via `BannerBearerLogicReflection` — cached once, fail-open to the original engine method on drift, pinned by `BannerBearersBindingTests`. Applied in `OnSubModuleLoad` inside a try/catch (a guard must never take startup down). Cold path — runs only when a reinforcement wave finds `GetMissingBannerCount > 0`. Known accepted divergence: the deployment gate's agent-level base checks (`IsHuman`, `is CharacterObject`) cannot run pre-spawn; the slot-4 guard is the backstop. RCA: `docs/reviews/rca-banner-bearers-reinforcement-av-2026-07-25.md`. Lives in `Main/Features/BannerBearers/Hooks/`.

## Patch_MissionTime_SetMovementOrder

**Target:** `Formation.SetMovementOrder(MovementOrder)` (Postfix ×2)

Shared deferred category for `Formation.SetMovementOrder(MovementOrder)` postfixes. Applied once from `OnMissionBehaviorInitialize` (one-shot static guard) because `MovementOrder.cctor` reads `Mission.Current.CurrentTime` — null in `OnSubModuleLoad`/`OnGameInitializationFinished`. Currently houses Patch31_SmartCavalryAI's charge handler and Patch35_CompanionTactics' `CancelStanceOnMove` postfix. **Any future patch with `MovementOrder` in its postfix signature must use this category.**

## Late_ActionSetOverride

**Target:** `ActionSetCode.GenerateActionSetNameWithSuffix` (Prefix)

Race-aware action-set name generation for custom races. The Prefix replaces the vanilla name build: a null `Monster` returns `as_human[_female]<suffix>`; otherwise the result is `as_<monsterId>[_female]<suffix>`, where `monsterId` prefers `Monster.BaseMonster` when present and falls back to the full `StringId` (matching vanilla's preference order) — so TAOM's custom-race monsters resolve to their own `as_<race>*` action sets (the LOTRLOME_Armory `as_<race>_facegen` requirement in `docs/features/character-creation.md` depends on this resolution). Returns `false` (skip original) on success; any exception falls through to vanilla. Applied in the late `OnGameInitializationFinished` batch alongside `Late_Transpiler` (one-shot guarded). Historical note in-file (Phase 9b #151): Harmony 2 attribute patches require `public static class` — this was TAOM's one non-static outlier, since fixed. Owning feature: HeroRace — see `docs/features/hero-race.md`.

## Late_Transpiler

**Target:** `BodyGeneratorView.RefreshCharacterEntityAux` (Transpiler)

CharacterSelection face-generator action-set injection. The transpiler finds the `Newobj` for `AgentVisualsData`'s parameterless ctor and inserts, right after it, a call to the patch's own `GetActionSet(BodyGeneratorView)` + `AgentVisualsData.ActionSet(...)` — so the body-generator preview is built with a race-appropriate `_facegen` action set (`BodyGen.Race` → `FaceGen.GetBaseMonsterFromRace`, null → human fallback, then `MBGlobals.GetActionSetWithSuffix(monster, isFemale, "_facegen")`) instead of the human default. Degrades gracefully (Phase 9b #160): if any of the three lookups (ctor / `ActionSet` method / `Newobj` IL match) fails, it logs the specific gap and returns the original instructions unchanged — previously such a mismatch threw out of `PatchCategory` during `OnGameInitializationFinished` and bricked startup. Applied in the late `OnGameInitializationFinished` batch (one-shot guarded). See `docs/features/character-selection.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/arena.md](../features/arena.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
