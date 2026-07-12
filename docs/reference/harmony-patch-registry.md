# Harmony Patch Registry

Full per-category rationale, history, and RCA links for every TAOM Harmony patch — moved verbatim from CLAUDE.md's Harmony Patch Categories table (repo-reorg 2026-07-12) so the eager-loaded CLAUDE.md keeps only the thin routing table (category | feature | target | status). **Before editing any patch, read its section here** and the scoped rule [.claude/rules/harmony-patches.md](../../.claude/rules/harmony-patches.md) (loads automatically when a `Main/**/Hooks/**` file is opened). Most sections end with a `docs/features/<x>.md` pointer — that feature doc remains the deep-dive.

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

## Patch26_SpecialResources

**Target:** `PartyCharacterVM.InitializeUpgrades`, `PartyScreenLogic.UpgradeTroop`, `PartyScreenLogic.AddCommand`

Per-kingdom resource gating + transactional spending

## Patch27_CareerSystem

**Target:** `ViewModel.ExecuteCommand`, `AgentStatCalculateModel.UpdateAgentStats`

Career screen opening + ability V-key activation (3 archetypes: Infantry/Ranged/Cavalry, 50 careers, XML-tunable)

## Patch28_SettlementGuards

**Target:** `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (manual), `GuardsCampaignBehavior.GetSuitableSpear` (manual)

Per-settlement guard troop injection + per-culture spear mapping (manual patches)

## Patch29_CCBodyProperties

**Target:** `CharacterCreationContent.SetSelectedCulture`, `CharacterCreationCultureStageVM.OnCultureSelection`, `CharacterCreationNarrativeStageView.RefreshAgentVisuals`

Per-culture default BodyProperties on CC screen + culture-stage-VM body re-apply + career menu player body sync

## Patch31_SmartCavalryAI

**Target:** `Formation.SetMovementOrder` (Postfix, deferred — see `Patch_MissionTime_SetMovementOrder`)

Coordinated line-charge state machine on player cavalry (Forming→Charging→PassingThrough→Reforming + Rerouting branch); recursion-guarded. **Note:** the `Formation.SetMovementOrder` Postfix lives in the shared `Patch_MissionTime_SetMovementOrder` category (see below).

## Patch34_QuickActions

**Target:** `SPInventoryVM.ExecuteSellAllItems` (Prefix), `SPInventoryVM` ctor (Postfix capture), `SPInventoryVM.RefreshCallbacks` (Postfix search-apply), `SPInventoryVM.OnFinalize` (Postfix clear)

Inventory "Sell All" multi-action menu + active-VM capture + per-save search-toggle apply + thread-static bypass for vanilla re-entry

## Patch38_SettlementNameplateFade

**Target:** `SettlementNameplateWidget.DetermineTargetAlphaValue` (Postfix)

Distance-based settlement nameplate fade on the campaign map — Postfix multiplies vanilla target alpha by [0,1] fade factor derived from `DistanceToCamera`. MCM-tunable near/far band (default 80..200), master toggle. Hot path (~3000 calls/sec): service captured once via `Initialize` static-field; settings provider caches `TaomSettings.Instance` reference in its ctor.

## Patch40_HideoutDescription

**Target:** `HideoutCampaignBehavior.game_menu_hideout_place_on_init` (private, Postfix)

Themed LOTR hideout encounter descriptions — Postfix re-sets the `HIDEOUT_DESCRIPTION` GameText var for TAOM's 5 bandit cultures, replacing vanilla's hardcoded-culture default "(Undefined hideout type)". Delegates to `IHideoutDescriptionService` (string→string, ADR-007 clean); runs before menu render so the lazy `{HIDEOUT_DESCRIPTION}` substitution picks up the override. Only `hideout_place` shows the var; `hideout_after_wait` needs no patch.

## Patch42_CastleRecruitment

**Target:** `AiVisitSettlementBehavior.AiHourlyTick` (Transpiler), `AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays` (Transpiler), `RecruitmentCampaignBehavior.HourlyTickParty` (Postfix)

Castle troop recruitment — AI half (player menu + notable spawn/fill + issue suppression live in `CastleRecruitmentBehavior`, not Harmony). Two transpilers swap the single `!settlement.IsCastle` AI-scoring gate for a runtime toggle (`CastleAiToggle.IsCastleAndAiDisabled`, same Settlement→bool stack shape) so AI lords score + travel to castles like towns; a postfix invokes the private `CheckRecruiting` for an AI party in a non-besieged castle (bound once to an open delegate — no per-call alloc). Transpilers pin to the FIRST `get_IsCastle` + require a nearby anchor (`GetAvailableWageBudget` / `IsSettlementSuitableForVisitingCondition`), else fail-safe to vanilla.

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

## Patch53_PartyIconScale

**Target:** `MobilePartyVisual.AddCharacterToPartyIcon` (private, Transpiler)

Campaign-map party-icon figure/mount scale. Transpiler rewrites the two hardcoded `0.3f` scale literals in `MobilePartyVisual.AddCharacterToPartyIcon` (people = `ldc.r4 0.3`→`callvirt Scale`; mount = `ldc.r4 0.3`→`mul`) into a `call PartyIconScaleConfig.GetScale()` so both honour the MCM "Map Figure Scale" slider (default 0.15 = half vanilla 0.30, range 0.05–1.0; `FiniteFloatValidator`-guarded). Stack-neutral in-place swap (labels preserved); animation-math `/0.3f` (`div`) literals not matched; missing-site fail-safe (warn, keep vanilla, never throw). Static IL-call-target pattern mirrors `CastleAiToggle`. Coexists with the BannerColorPersistence Postfix on the same method. `Main/Features/PartyIconScale/`. See `docs/features/party-icon-scale.md`.

## Patch54_NavalTravelBoatVisual

**Target:** `MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents` (Postfix ×2, SandBox.View)

**PARKED 2026-06-26 — not applied (commented out in `SubModule.cs`, #120/#296).** Renders an at-sea party as a boat — the base game renders NO ship at sea without `NavalDLC.View` (it omits the leader figure + adds nothing). Two Postfixes share `UpdateBoat`: `OnTransitionEnded` drives add/remove on the embark/disembark (the at-sea change does NOT trigger an icon rebuild, so the rebuild hook alone never saw it), `AddMobileIconComponents` re-adds on rebuild. Adds the base-game `Native` `boat_sail_on` mesh (also `map_icon_ship`; no DLC) scaled `boatScale` to the party's `StrategicEntity`, tag-idempotent (`taom_naval_boat`). `Main/Features/NavalTravel/Hooks/`. NavalTravel feature #296.

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

## Patch_MissionTime_SetMovementOrder

**Target:** `Formation.SetMovementOrder(MovementOrder)` (Postfix ×2)

Shared deferred category for `Formation.SetMovementOrder(MovementOrder)` postfixes. Applied once from `OnMissionBehaviorInitialize` (one-shot static guard) because `MovementOrder.cctor` reads `Mission.Current.CurrentTime` — null in `OnSubModuleLoad`/`OnGameInitializationFinished`. Currently houses Patch31_SmartCavalryAI's charge handler and Patch35_CompanionTactics' `CancelStanceOnMove` postfix. **Any future patch with `MovementOrder` in its postfix signature must use this category.**

