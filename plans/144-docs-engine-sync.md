# Plan 144: Port the engine-research docs whose claims hold on v1.4.8, each claim re-checked

> **Executor instructions**: Follow this plan step by step, in the worktree named under "Git workflow". The
> work is split into three Parts (A corrections, B engine reference additions, C lessons, rules and
> CLAUDE.md); each Part is landable on its own and has its own done criteria, so a session may stop after
> any Part. Run every verification command and confirm the expected result before moving on. If anything
> in "STOP conditions" occurs, stop and report; do not improvise. Never research TaleWorlds behaviour
> yourself: every engine line this plan asks you to write was read on the installed v1.4.8 during planning
> and is quoted here. Never edit `plans/README.md` (it belongs to the integrator): when done, report status,
> commit hashes and measured counts in your hand-off.
>
> **Drift check (run first, inside Step A0)**: `git diff --stat 1b00d1acd..HEAD -- <the in-scope paths listed under Scope>`.
> Earlier WPs of this programme are expected to have changed several of them (Step A0 lists which and how);
> compare the anchors this plan quotes against the live files and STOP only on a real mismatch.

## Status

- **Priority**: P2
- **Effort**: L (three sessions: Part A about 2 h, Part B about 3 h, Part C about 1 h)
- **Risk**: LOW (documentation, four comment-only C# edits, one test assertion message, one new Python test)
- **Depends on**: `plans/141-*.md` merged (binding, from the work-package map). Wave 5 runs after waves 0 to 4,
  so the base also carries WP106, WP109, WP112, WP117, WP118, WP120 and WP127 output; Step A0 checks each
  file this plan relies on. Soft order: run Part C after WP143 has merged if you can (both edit `CLAUDE.md`
  and `.claude/rules/`); if WP143 is still open, keep every `CLAUDE.md` edit in the single commit C3.
- **Category**: docs
- **Wave / lane**: wave 5, lane I
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (nothing here is wired),
  D4 (shader precompilation stays ACTIVE, so the in-tick `FinishDeployment` note becomes an open fix item),
  D7 (no co-op text), D8 (subjects carry `v2.0.28`), D10 (an ordinary session runs this file), D11 (no
  ShaderCacheKeeper text), D16 (the crush roll's shared `MBRandom` draw stays; the combat doc says so), D18
  (the lessons `merge=union` line is WP142's; this plan only appends entries), D19 (git through the Bash tool
  only), D20 (Step A5 measures the laptop's refreshed Armory)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`,
  2026-11-04
- **Issue**: port of #618 and #624 (`4520af802`), #635 (`aed9dea8d`), #385 (`0934f65d2`), #733 (`fe6619615`),
  #407 (`2b6f6216c`), #739 (`b5e1d0b3d`), #644 (`369eb0306`), #792 (`63630e8a0`), #870 (`42e48bbe5`), #1079
  (`985b0d54e`). New work (the toolchain host sentence, the shader-precompilation fix item): the orchestrator
  creates the issue before implementation lands.

## Why this matters

- **The 1.4.x docs state engine facts that are false on v1.4.8.** Eight docs, a skill, a code comment and
  `CLAUDE.md:214` say `Agent.IsInBeingStruckAction` reads action type 52 as "being struck"; v1.4.8's test is
  the half-open `MBMath.IsBetween(type, 48, 52)`, so 52 is outside it. The lord docs and skill say a lord in
  both `lords.xslt` and `characters/lords.xml` uses only the XML copy; v1.4.8 merges the two attribute by
  attribute and unions their equipment sets into a random pick. The arena docs say vanilla dresses every
  tournament fighter in the host's kit; v1.4.8 does that only in practice fights. `RefugeService.cs:627` says the
  AI pin is not saved; v1.4.8 saves it (`[SaveableProperty(11)]`). `arena.md:24-28` blames the female dwarf crash
  on the underwear mesh name; the crash address decodes to a face part with no morph channels.
- **The player symptoms on 1.4.8 need this engine knowledge in the repo.** Maintainers have seen many endless
  loading screens: on v1.4.8 a throw in `Mission.AfterStart` reloads the mission every tick, and nothing in
  the 1.4.x docs says so. The save-time evidence (34 to 1,034 s Containers phases in E1/E2) needs the save-cost
  mechanics and the forced post-save collection written down with V01's corrections. The map-view GPU leak and
  the first-use shader compiles are live memory and stall leads.
- **Without a re-check the 1.5.x text would mislead.** Two 1.5.x claims are wrong on v1.4.8 and this plan
  corrects them before they land: `Mission.GetAgentTroopClass_Override` is subscribed by vanilla hideout and
  alley-fight controllers (the 1.5.x table says nobody subscribes it), and `Equipment.HairCoverType` falls back to
  `Type4` when the head and body slots are both empty (the 1.5.x helmet doc says only the head slot decides). The
  1.5.x "a missing action set returns the default set" claim is never carried: v1.4.8 `MBGlobals.GetActionSet`
  throws (`MBGlobals.cs:33-45`).

## Current state

### Wrong claims on `origin/bannerlord-1.4.5` that this plan fixes

| File:line (origin) | Claim | v1.4.8 fact (verified, see engine table) |
|---|---|---|
| `CLAUDE.md:214`, `.claude/skills/new-creature-mount/SKILL.md:36-38`, `Main/Features/WarRam/WarRamConfig.cs:94-99`, `docs/features/war-ram.md:174-177`, `docs/ai-includes/creature-mount-authoring.md:66`, `docs/community/bannerlordmodding-lt/guides/custom_creature_reference.md:166`, `.../custom_creature_troubleshooting.md:30`, `.../custom_creature_xml.md:342-345`, `docs/modding/items-mounts-and-harness.md:479-480`, `docs/modding/recipe-add-a-race-or-creature.md:51`, `docs/reviews/lessons/animation-skeleton.md:193-196` | `actt_mount_strike` (52) is inside the being-struck band | the band is 48 to 51 |
| `docs/ai-includes/lord-skills-authoring.md:48`, `:50`, `:499`; `docs/modding/skill-sets.md:176`; `.claude/skills/lord-skills/SKILL.md:27` | last-loaded lord file wins, the XSLT copy is dead | per-attribute merge, equipment sets union |
| `docs/features/arena.md:65`, `:67`, `:75`; `docs/features/tournament-armor-assignment.md:11`, `:13`, `:19`, `:35-37`; `docs/modding/npcs-notables-and-townsfolk.md:186` | vanilla gives every tournament fighter the host's kit | only in practice fights |
| `docs/reference/harmony-patch-registry.md:623` | `GetParticipantCharacters` has three callers (v1.4.7) | six direct callers |
| `Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs:34-45` | four call sites; "the clean-roster line is DEBUG" | six callers; the postfix logs only WARNING (its `Postfix` writes `LogWarning` only) |
| `Main/Features/Refuge/RefugeService.cs:45`, `:627`; `.../Components/RefugePartyComponent.cs:12-13`; `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs:1427`; `docs/features/refuge.md:76` | the AI pin is not persisted | saved, `[SaveableProperty(11)]` |
| `docs/features/arena.md:24-28` | female dwarf crash = unresolved underwear mesh name | face morph over a mesh with no morph channels (#385) |
| `docs/reference/bannerlord-engine-and-toolchain.md:186`, `:197`; `docs/INDEX.md:179` | managed code runs on Mono | three hosts ship; WP117 rewrites the `:186` row, this plan fixes `:197` and the INDEX line |
| `docs/reference/bannerlord-engine-and-toolchain.md:260` | `elephant_harad_armor_01_geo.tpac` carries `elephant_skeleton` | a 416-byte stub; `adod_elephant_geo.tpac` carries it (1.5.x desktop check) |
| `docs/features/kingdom-voices.md:255-259` | no TAOM culture can match a voice file | an empty accent matches any gendered vanilla voice |

### What earlier WPs put on the base (Step A0 checks each)

| Expected on the base | From | If absent |
|---|---|---|
| `docs/features/mission-start-guard.md` | WP109 | STOP before B1 (the lifecycle text and the C3 row point at it) |
| `Main/Composition/MainMenuStep.cs`, `TAOM.Tests/Composition/SubModuleMainMenuHookTests.cs` | WP112 | drop the MainMenuStep sentences in B1 and say so in the report |
| `docs/reference/engine/mission-frame-threads-and-native-costs.md` containing `On the 1.4.x line` | WP117 | skip the B4 agent-capacity sub-edit |
| `docs/reference/bannerlord-engine-and-toolchain.md` containing `ships three hosts` | WP117 | A7 applies WP117's row text itself (quoted in A7) |
| `docs/features/map-view-release.md`, `docs/features/nameplate-cull.md` | WP120 | skip the B4 map-view sub-edit; drop the two links in B2/B4 |
| `docs/features/butter-lib-distance-matrix.md` | WP118 | drop that link in B2, and in B5 the provenance paragraph on ButterLib and its row-cell clause |
| `.claude/rules/harmony-patches.md` containing `MBCallback(null, true)` | WP106 | STOP (the B3 combat paragraph relies on that thread row) |
| `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs` containing `randomRoll: MBRandom.RandomFloat` | WP127 | B3 names `TaomCombatMechanicsModel` instead |
| `docs/reference/bannerlord-skeleton-authoring.md` containing `VERBATIM` | not expected: WP141's plan skips `3c5e57e71` (Kit facts not re-measured on the v1.4.8 Kit) and origin has 0 hits | skip the B4 fact-4 sub-edit and keep the existing skeleton row in C3; drop the `Ported-from` lines of `4520af802` (B4) and `2a9e8bc49` (C3) and say so in the hand-off |
| `tools/tpac_clone_metamesh.py` | not expected: WP141 skips `tools/tpac_clone_metamesh.py` | B5 always takes the whole file |
| `CHANGELOG.md` gates removed | WP102 (D1) | STOP at the first commit if a hook demands `CHANGELOG.md` |

### Engine facts verified for this plan (v1.4.8, build 119303)

Read with `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>` (cache `C:\Users\mikew\.taom-src\v1.4.8`, "taom-src"),
the category dump `C:\Decompiled_Bannerlord\_categories_v1.4.8` ("dump"), the modules dump
`C:\Decompiled_Bannerlord\_modules_build` (dated 2026-09-06, the day of the v1.4.8 dump; "modules dump"), or the
installed game files (read only). Quote these; do not re-derive them.

| Fact | Evidence |
|---|---|
| `MBMath.IsBetween(int value, int minValue, int maxValue)` returns `value >= minValue && value < maxValue` | taom-src `MBMath.cs:295-302` |
| `IsInBeingStruckAction` = `MBMath.IsBetween((int)GetCurrentActionType(1), 48, 52)` or the same on channel 0 | taom-src `Agent.cs:1338-1348` |
| `ActionCodeType`: `Kick = 28`, `Rear = 47`, `StrikeLight = 48` .. `StrikeKnockBack = 51`, `MountStrike = 52`, `StrikeBegin = 48`, `StrikeEnd = 52` | taom-src `Agent.cs:292`, `:311-319` |
| `MBObjectManager.MergeElements`: attributes copied from the later element, children matched by schema unique key and merged, unmatched children appended | taom-src `MBObjectManager.cs:820-870` |
| `<Equipments>` is `AlwaysPreferMerge`; `EquipmentSet` is unique on `@id` + `@civilian` + `@stealth` | installed `XmlSchemas/NPCCharacters.xsd:369-372`, `:410-415` |
| `Hero.SetInitialValuesFromCharacter` picks one battle and one civilian set at random | taom-src `Hero.cs:2237`, `:2274`, `:2278` |
| `MobilePartyAi.DoNotMakeNewDecisions` is `[SaveableProperty(11)]`; `SetDoNotMakeNewDecisions` at `:1607` | taom-src `MobilePartyAi.cs:124-125` |
| `GetBehaviors` applies the initiative result despite `DoNotMakeNewDecisions` when the best target is in a map event at, or besieging, the party's `TargetSettlement` | taom-src `MobilePartyAi.cs:477`, `:486`, `:488` |
| `DefaultTournamentModel.GetParticipantArmor`: host-faction practice dummy only when the mission mode is not `Tournament`, else `participant.RandomBattleEquipment` | taom-src `DefaultTournamentModel.cs:86-93` |
| `TournamentBehavior.StartMatch` sets mission mode 7 (`Tournament`) before `_gameBehavior.StartMatch`; `MissionMode.Tournament` is the 8th member (value 7) | dump `TournamentBehavior.cs:159`, `:166-167`; `MissionMode.cs:12` |
| Armour copy: `TournamentFightMissionController.AddRandomClothes` calls `GetParticipantArmor` | dump `TournamentFightMissionController.cs:353`, `:358` |
| Six direct callers of `TournamentGame.GetParticipantCharacters` | dump `TournamentBehavior.cs:105` (`GetAllPossibleParticipants`), `:115` (`CreateParticipants`); `FightTournamentGame.cs:80` (`GetMenuText`), `:332` (`GetTournamentPrize`); `TournamentManager.cs:144` (`ResolveTournament`); `HeroKnownInformationCampaignBehavior.cs:51` (`OnPlayerJoinedTournament`) |
| `GetTournamentPrize` is reached from `UpdateTournamentPrize` (join menu on_init, `BettingFraudIssueBehavior`'s menu, game load, the off-screen prize award) and the `TournamentGame` constructor | dump `TournamentGame.cs:88`, `:103-109`; `TournamentCampaignBehavior.cs:59-66`, `:267-271`; `BettingFraudIssueBehavior.cs:884-888`; `TournamentManager.cs:180-184` |
| `ResolveTournament` simulates (hero XP), adds the leaderboard entry, gives the prize and raises `OnTournamentFinished` with the roster | dump `TournamentManager.cs:140-157` |
| The join menu's gossip line is `{=GuWWKgEm}` and counts `p.IsHero` | dump `FightTournamentGame.cs:78-88` |
| `ArmorComponent` parses `hair_cover_type` and `beard_cover_type` with `ignoreCase: true` | taom-src `ArmorComponent.cs:190-191` |
| `Equipment.HairCoverType`: the head slot's item, else `Type4` when the body slot is empty; `BeardCoverType` reads the head slot only | taom-src `Equipment.cs:83-99`, `:101` |
| Vanilla `skins.xml`: 245 of 255 hair meshes map `cover_type4` (32 to a `*_type4` mesh); the adult man has 41 named beards, 4 of which (`beards_c_a`, `_e`, `_j`, `_u`) swap on `cover_type1` | installed `Modules/Native/ModuleData/skins.xml`, parsed 2026-11-04 |
| `MissionState.FinishMissionLoading` sets `_missionInitializing = false` (`:335`) before `CurrentMission.AfterStart()` (`:345`); `TickLoading` runs `LoadMission` whenever `!_missionInitializing` | taom-src `MissionState.cs:333-352`, `:221-233`, `LoadMission` `:235-243` |
| `Mission.AfterStart`: submodules' `OnBeforeMissionBehaviorInitialize`, each behaviour's `OnBehaviorInitialize`, submodules' `OnMissionBehaviorInitialize`, `EarlyStart`, spawn-path selector, deployment plan, `AfterStart`, `AfterMissionStart`, weather model, then `CurrentState = Continuing` | taom-src `Mission.cs:3799-3835` (`OnBehaviorInitialize` loop `:3809-3812`, submodule hook `:3813-3816`); `MissionState.cs:263` (`OnAfterMissionCreated`) |
| `Mission.OnTick` clears `tickCompleted` (`:3740`), then a count-down loop over the live list (`:3741-3744`); `RemoveMissionBehavior` ends with `MissionBehaviors.Remove` | taom-src `Mission.cs:3740-3744`, `:4643-4660` |
| `DeploymentMissionController.FinishDeployment` ends with `RemoveMissionBehavior(this)`; `SetupTeams` calls it at its end | taom-src `DeploymentMissionController.cs:41`, `:80`, `:162`, `:183` |
| `Module.OnApplicationTick` calls `SetInitialModuleScreenAsRootScreen` while `ActiveState` is null; that method calls every module's `OnBeforeInitialModuleScreenSetAsRoot` with no `try` | taom-src `Module.cs:498-511`, `:750-758` |
| `Mission.GetDefendCollisionResults` is `[MBCallback(null, true)]`; it reaches `DecideCrushedThrough` | taom-src `Mission.cs:6454-6456`; dump `MissionCombatMechanicsHelper.cs:311` |
| `Game.OnSaveCompleted` calls `Common.MemoryCleanupGC()` | dump `Game.cs:216-224` |
| `SaveContext` walks objects and containers with `TWParallel.ForWithoutRenderThread`; string ids via `AddOrGetStringId` | dump `SaveContext.cs:137`, `:157`, `:372`, `:398`; `ContainerSaveData.cs:368` |
| `TextObject`: the private constructor gives id 0, the public one and the `[LoadInitializationCallback]` a fresh id; `GetHashCode` returns that id | dump `TextObject.cs:64-69`, `:71-76`, `:164-168`, `:264-267` |
| `MbEvent.AddNonSerializedListener` puts the new record at the head; `Invoke` walks from the head | taom-src `MbEvent.cs:24-30`, `:32-44` |
| Team: `FormationsIncludingEmpty` `:34` (8 slots, `:268`), `FormationsIncludingSpecialAndEmpty` `:36` (10 slots, `:267`), `TeamAI` `:38`, forwarders `:415-447`, `Tick` gates on `AllowAiTicking` `:573-579` | taom-src `Team.cs` |
| Enemy walks use the 10-slot list | taom-src `Formation.cs:1361-1371`; dump `TeamQuerySystem.cs:190`, `:234`, `:291` |
| `GeneralsAndCaptainsAssignmentLogic` fills generals and bodyguard formations; SandBox field battles add it | dump `GeneralsAndCaptainsAssignmentLogic.cs:51-61`, `:140-142`; `SandBoxMissions.cs:746`, `:855` |
| 24 single-player field behaviours registered per formation; split transfers go through `Agent.Formation`, whose setter calls `AddUnit` | taom-src `TeamAIGeneral.cs:57-85`; `Formation.cs:2112`, `:2158`, `:1988`/`:2048`, `:2490`/`:2505`; `Agent.cs:1098`, `:1142` |
| Team AI: tactic options by best Tactics skill (>= 20, >= 50); 5 s decision; `MaxBy` with 1.5 sticky bonus; switch only above 1.5 x current; forced `TacticCharge` | taom-src `MissionCombatantsLogic.cs:100`, `:152-177`, `:218-219`; `TeamAIComponent.cs:36`, `:131-141`, `:206`, `:276-290`, `:301`, `:316-321` |
| Formation AI: 0.5 s staggered tick, winner `GetAIWeight() * WeightFactor` with a 2.0 to 1.2 boost over the preserve window, `MBException` for an unregistered behaviour | taom-src `FormationAI.cs:63`, `:112-116`, `:130`, `:138`, `:179-182`, `:245`, `:286` |
| `TacticalPosition` public runtime ctor; `TacticDefensiveRing` makes one; `GetAgentTroopClass_Override` event and its use | taom-src `TacticalPosition.cs:105`; `TacticDefensiveRing.cs:117`, `:180`; `Mission.cs:1555`, `:2541-2543` |
| `GetAgentTroopClass_Override` IS subscribed by vanilla outside field battles | dump `HideoutMissionController.cs:441`, `HideoutAmbushMissionController.cs:155`, `AlleyFightMissionHandler.cs:34` (8 subscriptions in the two dumps); no hit in 1.4.x `Main/` |
| Morale: radius 4 m, at most 10 gainers and 10 losers; kill factor 0.75 melee, 0.5 ranged, 0.25 area or multi-penetration (x1.25 with Burning and MultiplePenetration); gain `battleImportance*3*k`, loss `*4*k*casualtiesFactor`; casualtiesFactor `1 + 2*removed share`; panic: friends lose `importance*factor*1.1`, enemies gain `importance*2`; per soldier `max / max(1, MoraleResistance)` | dump `AgentMoraleInteractionLogic.cs:12`, `:14-16`, `:53`, `:64`, `:81-84`, `:86-93`; `CustomBattleMoraleModel.cs:20-35`, `:51-57`, `:72-75`, `:87-98`; taom-src `SandboxBattleMoraleModel.cs:60-77` |
| `DestructableComponent`: public `HitPoint` setter; `OnCalculateDestructionStateIndex` hook consulted by `CalculateNextDestructionLevel`; `ReplaceEntityWithBrokenEntity` reads `_destructionStates[_currentStateIndex - 1]`; `CastleGate` uses the hook | taom-src `DestructableComponent.cs:87`, `:105-124`, `:399-412`, `:489-498`; `CastleGate.cs:208`, `:1054` |
| Siege AI takes `OuterGate`/`InnerGate` by tag from all active gates; a lane is open only when `InnerGate` is null or open; a hidden `CastleGate` neither ticks nor runs `OnTick`; `BehaviorAssaultWalls` dereferences `InnerGate` | taom-src `TeamAISiegeComponent.cs:54-57`, `:184`; `CastleGate.cs:609-622`; `BehaviorAssaultWalls.cs:122` |
| `Color.ConvertStringToColor` parses `#RRGGBBAA` | taom-src `Color.cs:180-191` |
| `EventManager.AnyWidgetsAt` skips a widget with `DoNotAcceptEvents` but still checks its children; `GauntletLayer.HitTest` uses it | dump `EventManager.cs:842`, `:857-877`; `GauntletLayer.cs:232` |
| Map conversation path (lines of the laptop modules dump) | `SandBox__SandBox.View.cs`: `MapConversationTableau.OnFinalize` `:12131`, `OnTick` `:12164` (unguarded read `:12198`), `OnConversationPlay` `:12585`, `RemovePreviousAgentsSoundEvent` `:12648`, `PlayConversationSoundEvent` `:12656-12663`, `MissionCampaignView` `:2264` calls `MapScreen.ClearGPUMemory` `:2275` (defined `:13931`), `MapScreen` `IMapStateHandler.OnMenuModeTick` `:14248`, `OnIdleTick` `:14745`; `SandBox__SandBox.GauntletUI.cs`: `MapConversationTextureProvider` `:3354` (`Clear` `:3383`, `Tick` `:3422`), `GauntletMapConversationView` `:8416` (`Tick` `:8530`, `TrySetFocus` `:8542`, frame/idle/menu ticks `:8698`, `:8704`, `:8710`), `"MapConversation"` layer z 205 `:8613`, `"EncyclopediaBar"` layer z 310 `:10924`, `RegisterActiveStateDisableRequest` `:10931`; `SandBox__SandBox.cs` lip-sync `:16804`, `:16831`; dump `GameStateManager.cs:193-207`, `TextureWidget.cs:163-176` |
| `DefaultVoiceOverModel` third tier builds `".+" + accentClass + "_.+"` | taom-src `DefaultVoiceOverModel.cs:90` |
| `ViewSubModule.BannerTexturedMaterialCache` is a `Dictionary<Tuple<Material, Banner>, Material>` | modules dump `Native__TaleWorlds.MountAndBlade.View.cs:4765` |
| Render members exist: `Skeleton.ForceUpdateBoneFrames` `:90`, `GetBoneEntitialFrame` `:207`; `MBAgentVisuals.SetAgentLodZeroOrMax` `:344`; `GameEntity.SetEnforcedMaximumLodLevel` `:332`; `Agent.VisualPosition` `:684` | dump |
| Native strings present in the installed v1.4.8 `TaleWorlds.Native.dll` (14,185,944 bytes): `animation_sampling_quality`, `human_shadow_mesh`, `horse_shadow_mesh`, `No morph data found for face mesh`, `Too many physics contacts between agents` (once each); `do_timer_based_skeleton_forced_updates` absent | byte search 2026-11-04 |
| Vanilla `map_icon_parts.tpac`: 232 items, 6,266 segments, packed (no gap); all 228 metameshes carry a binding segment; `mi_market_tent_b` has 3 segments; `mi_market_tent_a` lives in `meshes_shared_7.tpac` | installed `Modules/Native/AssetPackages`, parsed with `tpac_clone_metamesh.parse` 2026-11-04 |
| v1.4.8 `SandBoxCore` `items/weapons.xml`: `flail_skeleton` 2, `sling_skeleton` 3, `shackle_skeleton` 1 | installed, grep 2026-11-04 |
| `MBGlobals.GetActionSet` throws on an invalid set (never write "returns the default set") | dump `MBGlobals.cs:33-45` (programme brief) |

### Conventions that bind this plan

- **Evidence over claims, no fabrication**: every engine line you write is one of the facts above, quoted with its
  citation. A 1.5.x measurement you carry keeps its version tag and source and is labelled as not re-measured on
  v1.4.8. If a sentence you are about to carry is not covered here, leave it out and list it in the report.
- **Humanize output**: no em dash or en dash in any prose you write (commits, docs, comments); hyphens in paths
  and flags are fine. The linter and the done criterion judge a changed line whole, so if an edit changes a line that
  already holds a long dash in prose, replace that dash on that line too (comma or colon). The known cases are spelled
  out in A7, B1 and C1.
- **Quoted anchors**: the docs wrap at about 110 columns and indent list continuations. An anchor shown with `+ newline +`
  or described as wrapped spans lines in the file; match it line by line, with the file's indentation.
- **Edit scope**: touch only the anchors named in each step; never reflow surrounding text.
- **ADR-003, ADR-004, ADR-005**: no `#region`, `[Obsolete]` or `#if DEBUG` (the C# edits here are comments).
- **Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `.claude/settings.json`,
  `Directory.Build.props` and `docs/adrs/*.md` are not touched.
- **Lessons shape**: `### <rule title>`, then `- **Why missed:**`, `- **Prevent:**`, `- **Source:**`; append at the
  end of the category file. A source on the 1.5.x line is cited as "1.5.x `<path>` (not on this line)".

## Commands you will need

Run every command from the worktree root, through the Bash tool, prefixed with
`cd /c/Users/mikew/source/repos/taom-port/144 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 warning is the known Patch88 false positive unless WP104 silenced it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~<Class>` | 0 failed |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure against `AUX/tooltests0.txt` |
| One tool test | `python -m unittest tools.tests.test_prefab_asset_packages -v` | `OK`, 5 tests |
| Docs | `python tools/lint_docs.py --summary --dash-base "$(cat ../144-aux/base.txt)"` | `ai_dashes: 0`; every other count not above `AUX/lint0.txt` |

Both `-p:` flags are mandatory on every build and test (on 1.4.x `Main/TAOM.csproj:133-136` does not gate the
CopyModule target, so a build without `-p:ModuleId=` deploys into the game install). Never `./build.ps1`.
`AUX` means `/c/Users/mikew/source/repos/taom-port/144-aux` (outside the repo).

## Scope

**In scope** (the only files you modify):

- Part A: `Main/Features/WarRam/WarRamConfig.cs` (comment), `docs/features/war-ram.md`,
  `docs/ai-includes/creature-mount-authoring.md`, `docs/community/bannerlordmodding-lt/guides/custom_creature_reference.md`,
  `.../custom_creature_troubleshooting.md`, `.../custom_creature_xml.md`, `docs/modding/items-mounts-and-harness.md`,
  `docs/modding/recipe-add-a-race-or-creature.md`, `docs/reviews/lessons/animation-skeleton.md`,
  `.claude/skills/new-creature-mount/SKILL.md`, `.claude/skills/lord-skills/SKILL.md`, `docs/modding/skill-sets.md`,
  `docs/ai-includes/lord-skills-authoring.md`, `docs/reviews/lessons/xslt-moduledata.md`,
  `Main/Features/Refuge/Components/RefugePartyComponent.cs`, `Main/Features/Refuge/RefugeService.cs`,
  `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs`, `docs/features/refuge.md`, `docs/features/arena.md`,
  `docs/features/tournament-armor-assignment.md`, `docs/modding/npcs-notables-and-townsfolk.md`,
  `docs/reference/harmony-patch-registry.md`, `Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs` (comment),
  `docs/reviews/lessons/gamemodels-services.md`, `docs/reviews/lessons/harmony-il.md`,
  `docs/reviews/lessons/build-tooling-workflow.md`, new `docs/reference/helmet-hair-beard-cover.md`, new
  `docs/reference/images/helmet-cover/hair-options.png` and `beard-options.png`, `docs/modding/items-armor.md`,
  `docs/INDEX.md`, `docs/features/hero-race.md`, `docs/features/kingdom-voices.md`,
  `docs/reference/engine/gauntletui-viewmodel-screen.md`, `docs/reviews/lessons/misc.md`,
  `docs/reference/bannerlord-engine-and-toolchain.md`, new `docs/reference/race-face-and-hand-morphs.md`,
  `docs/reference/lotrlome-armory-snapshot/README.md`.
- Part B: `docs/reference/engine/mission-and-missionbehavior-lifecycle.md`,
  `docs/reference/engine/submodule-lifecycle-and-harmony.md`, `docs/features/shader-precompilation.md`,
  `docs/reference/engine/save-system.md`, `docs/reference/engine/campaign-tick-time-and-party-ai.md`,
  `docs/reference/engine/formations-and-team-ai.md`, `docs/features/combat-mechanics.md`,
  `docs/reference/engine/agent-spawn-and-render-pipeline.md`, `docs/reference/engine/animation-binding-and-playback.md`,
  `docs/reference/engine/gauntletui-viewmodel-screen.md`, `docs/features/map-view-release.md`,
  `docs/reference/engine/mission-frame-threads-and-native-costs.md`, `docs/reference/bannerlord-animation-clip-flags.md`,
  `docs/reference/bannerlord-skeleton-authoring.md`, `docs/ai-includes/creature-mount-authoring.md`,
  `docs/community/bannerlordmodding-lt/guides/custom_creature_troubleshooting.md`, new
  `docs/reference/tpac-static-prop-authoring.md`, new `tools/tests/test_prefab_asset_packages.py`,
  `tools/tpac_clone_metamesh.py`, `tools/README.md`, `docs/reference/provenance-register.md`,
  `docs/reference/bannerlord-engine-and-toolchain.md`, `docs/INDEX.md`.
- Part C: `docs/reviews/lessons/adapters-taleworlds-api.md`, `docs/reviews/lessons/localization-ui.md`,
  `docs/reviews/lessons/testing-qa.md`, `docs/reviews/lessons/harmony-il.md`, `docs/reviews/lessons/misc.md`,
  `docs/reviews/lessons/animation-skeleton.md`,
  `.claude/rules/adapters.md`, `docs/reference/engine/usable-machines.md`, `CLAUDE.md`.

**Out of scope**: every file not listed; `CHANGELOG.md` (D1); the single-owner files; any ModuleData, any
tool other than the two in B5; any 1.5.x-only feature doc (`culture-doctrine.md`, `race-abilities.md`,
`tournament-rewards.md`, `long-defeat.md`, `fords-of-war.md`); `docs/reviews/REVIEW-LOG.md` and the historical
review prompts (they record what was believed then); the game install.

## Git workflow

1. From the main checkout (Bash):
   `cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/144 -b port148/144-docs-engine-sync bannerlord-1.4.5 && mkdir -p /c/Users/mikew/source/repos/taom-port/144-aux`
2. Work only in the worktree. Every git command goes through the Bash tool (D19).
3. Per commit: stage explicit paths (`git add <paths>`, never `-A`), write the message to `AUX/msg-<step>.txt`,
   then run `git commit -F <msgfile>` in the NEXT, separate Bash call. Never `--amend --no-edit`, never let
   `cherry-pick` create a commit (`git cherry-pick -n` only).
4. Commit message template (wrap at 72, no em or en dashes, no Co-Authored-By line, no "Generated with"):

   ```text
   <type>(<scope>): v2.0.28 - <description>      (at most 72 characters)

   <what changed on the 1.4.8 line and why, for 1.4.x readers>

   Research: <the v1.4.8 facts from this plan's table that the text cites>
   Not-tested: <what needs an in-game check, or "docs only">
   Ported-from: <40-hex sha>                      (one line per 1.5.x commit)
   ```

   Type and scope follow the 1.5.x source where it is a docs commit; where the 1.5.x commit was a feature whose
   docs half alone is ported, the type is `docs` (the feature payload stays out, so `feat` would misdescribe it).
5. No push, no PR, no merge into `bannerlord-1.4.5`. The integrator merges `port148/144-docs-engine-sync`.

---

## Part A: correct what the 1.4.x docs state wrongly

### Step A0: baseline, drift and prerequisite checks

1. `cd /c/Users/mikew/source/repos/taom-port/144 && git rev-parse HEAD > ../144-aux/base.txt && git status --porcelain`
   (expect empty output).
2. Drift: `git diff --stat 1b00d1acd..HEAD -- <every in-scope path above>`. Expected changes from earlier WPs:
   `docs/reference/bannerlord-engine-and-toolchain.md` (WP117 row), `docs/reference/harmony-patch-registry.md`
   (many WPs add sections), `docs/reference/provenance-register.md` (WP115, WP117), `docs/INDEX.md`, the lessons
   files, `CLAUDE.md` (WP103, WP141, WP143), `.claude/rules/*` (WP106, WP143), the creature guides, the recipe and
   `animation-skeleton.md` (WP141), `RefugeService.cs` and `refuge.md` (WP127), `docs/features/combat-mechanics.md`
   (WP127), `docs/reference/engine/submodule-lifecycle-and-harmony.md` (WP104), `shader-precompilation.md`
   (WP113). Each step below matches by quoted text, not by line number, so these are fine. STOP only if a quoted
   anchor is missing and the step gives no fallback.
3. Prerequisites: run
   `for f in docs/features/mission-start-guard.md Main/Composition/MainMenuStep.cs docs/reference/engine/mission-frame-threads-and-native-costs.md docs/features/map-view-release.md docs/features/nameplate-cull.md docs/features/butter-lib-distance-matrix.md Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs tools/tpac_clone_metamesh.py tools/check_rdc_entries.py; do test -e "$f" && echo "present $f" || echo "ABSENT $f"; done; grep -c "ships three hosts" docs/reference/bannerlord-engine-and-toolchain.md; grep -c "MBCallback(null, true)" .claude/rules/harmony-patches.md; grep -c "VERBATIM" docs/reference/bannerlord-skeleton-authoring.md`
   and record the output in `AUX/prereq.txt`. Apply the "If absent" column of the table under Current state.
4. Baselines (record, do not fix): `dotnet restore TAOM.sln`, Build, Test (record total, passed, skipped in
   `AUX/test0.txt`; the expected count is the one the previous merged WP recorded in the main checkout's `plans/README.md`, read only),
   `python -m unittest discover -s tools/tests -t . > ../144-aux/tooltests0.txt 2>&1`,
   `python tools/lint_docs.py --summary --dash-base HEAD > ../144-aux/lint0.txt`, `wc -c CLAUDE.md > ../144-aux/claude0.txt`.

**Verify**: `AUX/base.txt`, `prereq.txt`, `test0.txt`, `tooltests0.txt`, `lint0.txt`, `claude0.txt` exist; Test shows 0 failed.

### Step A1: the being-struck band is 48 to 51 (from `4520af802`)

Method: hand edit (the 1.5.x commit mixes this fix with the war-ram redesign, which stays out). Apply each
replacement with the Edit tool. If an old text is not found (WP141 edited the guides), grep the file for
`IsInBeingStruckAction`, apply the same correction to the sentence that states the band, and note it in the report.
`CLAUDE.md:214` is done in C3.

1. `Main/Features/WarRam/WarRamConfig.cs`, replace the four comment lines that begin
   `    //   * act_horse_strike_front / _back are typed actt_mount_strike (ActionCodeType.MountStrike = 52).` with:
   ```csharp
       //   * act_horse_strike_front / _back play the horse's HIT REACTIONS (clips horse_hit_from_front /
       //     _back), so playing one makes the ram flinch as though hit while we emit damage. Their type,
       //     actt_mount_strike (ActionCodeType.MountStrike = 52), is harmless: Agent.IsInBeingStruckAction
       //     tests MBMath.IsBetween(type, 48, 52), which is half-open, so it reads 48 .. 51 as being struck
       //     and not 52 (v1.4.8 Agent.cs:1338-1348, MBMath.cs:295-302).
   ```
   and in the line `    // act_horse_kick is typed actt_kick (ActionCodeType.Kick = 28): outside the being-struck band and`
   replace `outside the being-struck band` with `outside the 48 .. 51 being-struck band`.
2. `docs/features/war-ram.md`, replace the bullet that begins ``- **`act_horse_strike_front`/`_back`** are typed`` (4 lines) with:
   ```markdown
   - **`act_horse_strike_front`/`_back`** play the horse's hit reactions (`horse_hit_from_front`/`_back`), so
     the ram would have flinched as though hit while we emitted damage. Their type, `actt_mount_strike`
     (`ActionCodeType.MountStrike = 52`), is not the problem (corrected 2026-09-18 on the 1.5.x line):
     `Agent.IsInBeingStruckAction` tests `MBMath.IsBetween(type, 48, 52)`, which is half-open, so it reads
     48 to 51 (`StrikeLight` .. `StrikeKnockBack`) as being struck and NOT `MountStrike` (52) (v1.4.8
     `Agent.cs:1338-1348`, `MBMath.cs:295-302`). The warg's `actt_mount_strike` attacks play for that reason.
   ```
3. `docs/ai-includes/creature-mount-authoring.md`, in the table row that begins ``| `act_horse_strike_front` / `_back` |``,
   replace its third cell (from `Sits inside` to `while you emit damage`) with:
   `The clips are the horse's hit reactions, `horse_hit_from_front` / `_back`: the creature flinches as though hit while you emit damage. The TYPE is harmless: `Agent.IsInBeingStruckAction` tests `MBMath.IsBetween(type, 48, 52)`, which is half-open, so it reads 48 to 51 (`StrikeLight` .. `StrikeKnockBack`) as being struck and NOT `MountStrike` (52) (v1.4.8 `Agent.cs:1338-1348`; the warg's `actt_mount_strike` attacks play)`
4. `.../guides/custom_creature_reference.md`, replace ``Anything in `StrikeBegin .. StrikeEnd` is read by `Agent.IsInBeingStruckAction` as **being struck**.`` with:
   ```markdown
   `Agent.IsInBeingStruckAction` reads types from `StrikeBegin` up to but not including `StrikeEnd` (48 to
   51) as **being struck**: `MBMath.IsBetween` is half-open, so `MountStrike` (52) is not included.
   ```
5. `.../guides/custom_creature_troubleshooting.md`, replace ``| Creature flinches while dealing damage | An attack was bound to an `actt_mount_strike` action |`` with
   ``| Creature flinches while dealing damage | An attack was bound to a hit-reaction clip, such as the horse's `act_horse_strike_*` (clips `horse_hit_from_*`) |``.
6. `.../guides/custom_creature_xml.md`, replace the paragraph that begins ``**`act_horse_strike_front` and `_back` are typed `actt_mount_strike`,**`` (4 lines) with:
   ```markdown
   **`act_horse_strike_front` and `_back` play hit reactions.** The underlying clips are literally named
   `horse_hit_from_front` and `horse_hit_from_back`. Bind an attack to those and the creature flinches
   as though it has been hit, at the exact moment it deals damage. Their type, `actt_mount_strike`, is
   harmless on its own: `Agent.IsInBeingStruckAction` reads types 48 to 51 as being struck, and
   `actt_mount_strike` is 52.
   ```
7. `docs/modding/items-mounts-and-harness.md`, replace the clause
   ``sit in the band the engine reads as BEING struck`` with ``play the horse's hit-reaction clips`` (one occurrence).
8. `docs/modding/recipe-add-a-race-or-creature.md`, replace ``(inside the `StrikeBegin..StrikeEnd` band the engine reads as *being struck*)``
   with `(the horse's hit-reaction clip, so the creature flinched while it dealt damage)`.
9. `docs/reviews/lessons/animation-skeleton.md`, replace the numbered item that begins
   ``  2. The replacement `act_horse_strike_front` is typed `actt_mount_strike` `` (4 lines) with:
   ```markdown
     2. The replacement `act_horse_strike_front` plays the horse's hit reaction (clips
        `horse_hit_from_front`/`_back`), so the creature flinches as though hit while you emit damage.
        Its type, `actt_mount_strike` (`ActionCodeType.MountStrike = 52`), is NOT the cause: corrected
        2026-09-18, `Agent.IsInBeingStruckAction` tests the half-open `MBMath.IsBetween(type, 48, 52)`,
        so it reads 48 to 51 as being struck (v1.4.8 `Agent.cs:1338-1348`).
   ```
10. `.claude/skills/new-creature-mount/SKILL.md`, replace
    ``act_horse_strike_front` is`` + newline + ``actt_mount_strike = 52`, inside the `48..52` band `Agent.IsInBeingStruckAction` reads as being`` + newline + `struck.`
    with: ``act_horse_strike_front` plays the`` / `horse's hit-reaction clip (its type, `actt_mount_strike = 52`, is outside the half-open `48..51` band` /
    `` `Agent.IsInBeingStruckAction` reads as being struck).`` (three lines, keep the file's wrapping width).

**Verify**: `git grep -n -E "48 ?\.\. ?52|StrikeBegin = 48 \.\. StrikeEnd = 52|reads as being-struck|sit in the band the engine reads" -- . ':!CHANGELOG.md' ':!docs/reviews/REVIEW-LOG.md' ':!docs/reviews/*.prompt.md' ':!plans'`
prints only the `CLAUDE.md` row (fixed in C3). (`:!plans` because WP101 committed the plan files, this one included, which quote the old text.) Build exit 0. Docs command: `ai_dashes: 0`.
Commit A1: stage the ten files. Subject `docs(creatures): v2.0.28 - being-struck band is 48 to 51, not 52`.
Body: the half-open test, the eight docs, the skill and the config comment that said otherwise, and that the
war ram's rejected strike clips were wrong because they are hit reactions. `Research:` the MBMath, Agent and
ActionCodeType rows. `Not-tested: comment and docs only`. `Ported-from: 4520af802d9c31f44263c7a9ce4a263e611d0c5d`.

### Step A2: a lord in both files is merged (from `369eb0306`, lesson #644 from `9804f67b9`'s lesson text)

Method: hand edit (the pick conflicts in `lord-skills-authoring.md`; the other two files would apply, but the
1.4.x wording differs, so all three are written here).

1. `.claude/skills/lord-skills/SKILL.md`, replace the line that begins `- **Last-loaded wins**` with:
   ```markdown
   - **An id in both files merges:** `characters/lords.xml` wins each attribute and `<skill id>` it redeclares, `lords.xslt` supplies the rest, and differing `<Equipments>` sets union into a random per-campaign pick (lesson #644 in `docs/reviews/lessons/xslt-moduledata.md`). Grep both files before editing a lord.
   ```
2. `docs/modding/skill-sets.md`, replace the step that begins `3. If the character exists in both files,` with:
   ```markdown
   3. If the character exists in both files, the engine merges the two definitions: `characters/lords.xml` loads second and wins every attribute it redeclares, so its `skill_template` (if it states one) is the one used, while the XSLT copy still supplies what the row omits ([lord skills](../features/lord-skills.md) line 20). Grep both files for the id first.
   ```
3. `docs/ai-includes/lord-skills-authoring.md`:
   - replace the paragraph that begins `**Load order matters.**` with:
     ```markdown
     **Load order matters.** SubModule.xml line ~60 loads `lords.xslt`-transformed vanilla NPCs FIRST. Line ~120 loads `characters/lords.xml` SECOND. An id in both files is **two definitions merged, not an override**: `MBObjectManager.MergeElements` (v1.4.8 `MBObjectManager.cs:820`) merges attribute by attribute and child by child (a `<skill id>` matches by id), so `characters/lords.xml` wins whatever it redeclares and the XSLT copy still supplies the rest ([lord skills](../features/lord-skills.md)). `<Equipments>` unions: a set with a different id is added, and the hero picks one at random per campaign ([lesson #644](../reviews/lessons/xslt-moduledata.md)). Before editing a lord, grep BOTH files for its id.
     ```
   - replace the line that begins `This is why the Gondor noblewomen rename fix` with:
     `This is why the Gondor noblewomen rename fix (Catella → Lindariel) lives in `lords.xml`, not in `lords.xslt`: the `name` that `lords.xml` redeclares is the one the player sees.`
   - replace the table row that begins ``| Same-ID NPCs in both `lords.xslt` `` with:
     ```markdown
     | Same-ID NPCs in both `lords.xslt` (vanilla transform) and `characters/lords.xml` (TAOM additions) merge: `lords.xml` loads second and wins each attribute and `<skill id>` it redeclares, the XSLT copy supplies the rest, and differing `<Equipments>` sets union into a random per-campaign pick (#644) | `Main/_Module/SubModule.xml` load order; `MBObjectManager.MergeElements` | Grep BOTH files for the id before editing. If a fix isn't taking effect, the other file may redeclare that attribute or carry a second equipment set; #648 tracks the ids still in both |
     ```
4. Append to `docs/reviews/lessons/xslt-moduledata.md`:
   ```markdown

   ### A lord in both `lords.xslt` and `characters/lords.xml` is two definitions merged, not an override (#644)
   The engine merges every file of one XML id (v1.4.8 `MBObjectManager.MergeElements`, `MBObjectManager.cs:820`): the
   later `characters/lords.xml` row wins each attribute it states, a child with the same unique key (a `<skill id>`) is
   merged, and any other child is appended. `<Equipments>` is `AlwaysPreferMerge` and `EquipmentSet` is unique on id,
   civilian and stealth (installed `XmlSchemas/NPCCharacters.xsd:369-372`, `:410-415`), so a set whose id differs is
   ADDED, and `Hero.SetInitialValuesFromCharacter` (`Hero.cs:2237`) then picks one battle and one civilian set at random
   per campaign (`:2274`, `:2278`). On the 1.5.x line three of the Nine wore the Nazgul kit or a generic Mordor kit by
   coin flip.
   - **Why missed:** either file alone looks complete, and no gate compares the two.
   - **Prevent:** before editing a lord, grep BOTH files for its id. Moving a lord from one file to the other means
     folding every value the row wins into the other file first. #648 tracks the ids still in both.
   - **Source:** 1.5.x `docs/reviews/rca-nazgul-race-2026-09-23.md` finding 1 (not on this line); plan 144.
   ```

**Verify**: `git grep -n -i "last-loaded wins\|dead code for that ID" -- docs .claude ':!docs/reviews/rca-*'` prints nothing (the historical RCA `rca-gondor-lord-review-2026-05-26.md` keeps its wording); Docs command
`dead_links` not above baseline, `ai_dashes: 0`. Commit A2 (four files). Subject
`docs(lords): v2.0.28 - lords in both files merge, not override`. `Research:` the MergeElements, XSD and Hero
rows. `Not-tested: docs only`. `Ported-from: 369eb030696718e961e7fc0fa1a4c438be4a5a1c` and
`Ported-from: 9804f67b9deba24813da1f2ad7ef2d5f9ad6fa14` (the lesson's origin).

### Step A3: the engine saves the refuge AI pin (`63630e8a0`, cherry-pick)

Simulation this session: `git merge-tree --merge-base=63630e8a0^ origin/bannerlord-1.4.5 63630e8a0` exits 0
(clean). WP127 later reshaped `RefugeService.cs` (plan 026); its plan states this pick still applies on top.

1. `git cherry-pick -n 63630e8a0`. If it conflicts, `git checkout --ours` nothing; instead resolve by hand: the
   four changes are comment, summary, assertion-message and doc text only, and the new texts are the `+` lines of
   `git show 63630e8a0` (no version tag in them). Keep every line of 1.4.x code around them.
2. `git diff --cached --stat` must list exactly `RefugePartyComponent.cs`, `RefugeService.cs`,
   `RefugeServiceTests.cs`, `docs/features/refuge.md`.

**Verify**: Build exit 0; Filtered `RefugeServiceTests` 0 failed; `git grep -n "not persisted" -- Main/Features/Refuge`
prints nothing. Commit A3 (the four files). Subject `fix(refuge): v2.0.28 - the engine saves the AI pin`. Body:
comments, the test message and the doc claimed the pin is lost on reload; v1.4.8 saves it, so the load pin is an
idempotent belt; no behaviour change. `Research: taom-src v1.4.8 MobilePartyAi.cs:124-125 ([SaveableProperty(11)])`.
`Not-tested: comment, message and doc only`. `Ported-from: 63630e8a09ce5b98b5ab7eccbfb0cf9984d532d9`.

### Step A4: tournament armour and the Patch69 caller list (`fe6619615`, `2b6f6216c`, `75b7b6661`'s comment)

Method: hand edit. `2b6f6216c` conflicts on the patch file in this session's simulation; `fe6619615` conflicts on
docs of 1.5.x-only features. Keep the 1.4.x override count: `TaomTournamentModel` has FIVE overrides on 1.4.x
(`GetTournamentStartChance`, `GetTournamentEndChance`, `GetRegularRewardItems`, `GetEliteRewardItems`,
`GetParticipantArmor`); never write "seven" or Tournament Rewards (a 1.5.x feature).

1. `docs/features/arena.md`:
   - Replace the bullet that begins `- **Vanilla behavior:** [DefaultTournamentModel](` with the `+` bullet of the same
     name in `git show fe6619615 -- docs/features/arena.md`, changing ``(taom-src `DefaultTournamentModel.cs:86-93`, unchanged in v1.5.4)``
     to ``(v1.4.8, taom-src `DefaultTournamentModel.cs:86-93`)`` and ``(`TournamentBehavior.StartMatch` sets it before the controller's `PrepareForMatch` copies the armour)``
     to ``(`TournamentBehavior.StartMatch` sets it, `TournamentBehavior.cs:166`, before the controller copies the armour in `AddRandomClothes`, `TournamentFightMissionController.cs:358`)``.
   - Replace the bullet that begins `- **Without this feature:**` and the paragraph that begins
     ``` `DefaultTournamentModel.GetParticipantArmor` takes a `CharacterObject` ``` with their `+` versions from that diff.
   - In the table row that begins ``| `GetParticipantArmor(CharacterObject)` |``, append to the last cell:
     ` Applies in every mission mode, where vanilla keeps a tournament fighter's own armour and gives practice fighters the host's kit.`
   - Under `## Changelog`, add as the first entry:
     `- 2026-11-04 (plan 144): corrected what vanilla's `GetParticipantArmor` does (v1.4.8 `DefaultTournamentModel.cs:86-93`). The doc said every tournament fighter wears the host culture's kit; vanilla does that only in arena practice fights, and a tournament fighter keeps his own armour (the `MissionMode.Tournament` check). TAOM's override applies in both, so in tournaments it swaps each fighter's own armour for his culture's practice kit. Found on the 1.5.x line on 2026-10-05; the vanilla code is the same on v1.4.8.`
2. `docs/features/tournament-armor-assignment.md`:
   - Replace the bullets `- **Vanilla behavior:**` and `- **Without this feature:**`, the first sentence of the
     Design Challenge paragraph (`The vanilla method completely discards participant identity` up to the first `—`
     clause end, i.e. through `which settlement is hosting.`), and the three diagram lines under
     ``└─ base.GetParticipantArmor(participant)`` with their `+` versions from `git show fe6619615 -- docs/features/tournament-armor-assignment.md`,
     changing ``(taom-src `DefaultTournamentModel.cs:86-93`, unchanged in v1.5.4)`` to ``(v1.4.8, taom-src `DefaultTournamentModel.cs:86-93`)``.
     Do NOT take the `seven`, the Dependencies or the `7 overrides` hunks.
   - In the Key Files row that begins ``| `Main/SubModule.cs:385` |``, replace `385` with the line that
     `grep -n "new TaomTournamentModel" Main/SubModule.cs` prints (one hit expected; STOP if 0 or 2+).
   - Under `## Changelog`, add as the first entry:
     `- 2026-11-04 (plan 144): corrected the vanilla behaviour this doc was built on. `GetParticipantArmor` gives every fighter the host faction's kit only in arena practice fights; in a tournament match each fighter keeps his own armour (v1.4.8 `DefaultTournamentModel.cs:86-93`). So TAOM's override fixes cross-race clipping in practice fights, and in tournament matches it swaps each fighter's own armour for his culture's practice kit. Registration line updated.`
3. `docs/modding/npcs-notables-and-townsfolk.md`: replace ``and vanilla does the same thing at `DefaultTournamentModel.cs:90`.``
   with ``and vanilla builds the same id from the host faction's culture for an arena practice fight (`DefaultTournamentModel.cs:86-93`; in a tournament match vanilla keeps the fighter's own armour).``
   Keep `TournamentService.cs:64-71` (true on 1.4.x).
4. `docs/reference/harmony-patch-registry.md`, in `## Patch69_TournamentRosterGuard`:
   - Replace the paragraph that begins `**Call frequency — verified, and it is why the healthy-path line is gone.**` with:
     ```markdown
     **Call frequency, and why the healthy-path line is gone.** On v1.4.8 (dump, re-read for plan 144) `GetParticipantCharacters` has six direct callers, none per-frame: `TournamentBehavior.CreateParticipants` (`TournamentBehavior.cs:115`, once per tournament) and `GetAllPossibleParticipants` (`:105`, from SandBox.View's `ArenaPreloadView`); `FightTournamentGame.GetMenuText` (`FightTournamentGame.cs:80`) and `GetTournamentPrize` (`:332`), reached from the join menu's **on_init** (vanilla's, `TournamentCampaignBehavior.cs:267-271`, and `BettingFraudIssueBehavior`'s, `:884-888`), the `TournamentGame` constructor (`TournamentGame.cs:88`), game load (`TournamentCampaignBehavior.cs:59-66`) and the off-screen prize award (`TournamentManager.cs:180-184`); `TournamentManager.ResolveTournament` (`:144`), the off-screen resolution; and `HeroKnownInformationCampaignBehavior.OnPlayerJoinedTournament` (`:51`). An earlier count of three (v1.4.7) missed the off-screen and known-information callers. The join menu's on_init reaches it twice per open, so the guard logged several times for a single player action: 78 lines in one 70-minute session. It was `LogDebug` (async) rather than INFO, which was the right call for that re-entrancy, but the line existed to prove the guard runs, it has done so across every logged session, and it was retired 2026-08-09 (`06cd51b4`). **The trade, recorded deliberately:** a session where the guard silently fails to run now looks identical to one where every roster was safe. Accepted because the guard's value is the substitution it performs, and that still announces itself.
     ```
   - Replace the paragraph that begins `**Known and accepted:**` with:
     ```markdown
     **Known and accepted:** the substitution happens wherever the roster is read, so its effects reach past the join menu. `GetMenuText`'s gossip line (`{=GuWWKgEm}`, "... {NOBLE_COUNT} lords with renowned fighting skills present ...") and the prize tiering both count `p.IsHero`, so substituting an unsafe hero shifts each by one. Off-screen, `ResolveTournament` simulates, awards and records the substituted roster (`TournamentManager.cs:140-157`), so an unsafe hero cannot win an off-screen tournament or get its leaderboard entry, prize or simulation experience. The `TournamentFinished` participant list carries the substitute on both the played and the off-screen path, so the event's listeners see it too, and a substituted hero is not marked known to the player when the player joins or watches. All of this is correct rather than a defect, since a substituted hero genuinely does not compete, but these are visible consequences of patching this method, recorded here rather than discovered later.
     ```
5. `Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs`: replace the two summary paragraphs that begin
   `/// **This runs on every call, not once per tournament.**` and `/// Two accepted consequences of patching a method the menu also calls:`
   (through `/// rather than discovered later.`) with:
   ```csharp
   /// It runs on every read of the roster, not once per tournament (no caller is per-frame), so every
   /// reader gets the substitute: the join menus' lord count, the prize tier, the off-screen
   /// <c>TournamentManager.ResolveTournament</c> and the <c>TournamentFinished</c> list. A clean roster
   /// is silent; a substitution logs WARNING. The callers and each consequence:
   /// <c>docs/reference/harmony-patch-registry.md</c>, "Patch69_TournamentRosterGuard".
   ```
6. `docs/reviews/lessons/build-tooling-workflow.md`: replace
   ``run "once per tournament". `FightTournamentGame.GetParticipantCharacters` has four call sites; two``
   with ``run "once per tournament". `FightTournamentGame.GetParticipantCharacters` had four known call sites (six in fact on v1.4.8: the off-screen `TournamentManager.ResolveTournament` and `HeroKnownInformationCampaignBehavior` were missed); two``.
7. Append to `docs/reviews/lessons/harmony-il.md` the lesson that begins
   `### A patch's class doc states its claim and points to the registry` from `git show 2b6f6216c -- docs/reviews/lessons/harmony-il.md`
   (the `+` lines), with `(four and three; there are six)` kept and the `- **Source:**` line replaced by
   `- **Source:** 1.5.x `docs/reviews/rca-patch69-comment-2026-10-05.md` findings 1 and 2 (not on this line); plan 144.`
8. Append to `docs/reviews/lessons/gamemodels-services.md` the lesson that begins
   `### Describe a vanilla model method from every branch, mode checks included` from `git show fe6619615 -- docs/reviews/lessons/gamemodels-services.md`,
   changing `and was found only by the v1.5.4 compatibility check` (wrapped across two lines in the source) to
   `and was found only by the 1.5.x line's v1.5.4 compatibility check (the vanilla code is the same on v1.4.8, `DefaultTournamentModel.cs:86-93`)`
   and the `- **Source:**` line to `- **Source:** the 1.5.x line's v1.5.4 compatibility check, 2026-10-05; plan 144.`

**Verify**: Build exit 0; Filtered `TournamentRosterGuard` 0 failed and more than 0 tests run (no test class name contains `Patch69`); `git grep -n "has three callers in v1.4.7\|has four call" -- docs Main` prints nothing;
`git grep -n -i "tournament rewards" -- Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs docs/features/arena.md docs/features/tournament-armor-assignment.md`
prints only lines that existed at `base.txt` (`git diff "$(cat ../144-aux/base.txt)" -- <those files> | grep -i "^+.*tournament rewards"` prints nothing).
Docs command clean. Commit A4 (eight files: the eight named in sub-steps 1 to 8). Subject `docs(arena): v2.0.28 - vanilla tournament armour, six roster callers`.
`Research:` the tournament rows. `Not-tested: comment and docs only; in-game check 7 confirms the described look`.
`Ported-from:` `fe6619615844aa86aa1ae307ea824a3278898f2a`, `2b6f6216c7e3d24120e019ace44df73213237403`, `75b7b6661d7f2e3193383b58c26ac33ccbcd41cf`.

### Step A5: helmet hair and beard cover reference (from `75b7b6661`)

1. `mkdir -p docs/reference/images/helmet-cover && for f in hair-options beard-options; do git show 75b7b6661:docs/reference/images/helmet-cover/$f.png > docs/reference/images/helmet-cover/$f.png; done && git show 75b7b6661:docs/reference/helmet-hair-beard-cover.md > docs/reference/helmet-hair-beard-cover.md`
2. Edit the new doc:
   - Replace the bullet ``- **Only the Head slot decides** (`Equipment.cs:101`). The same attribute on body armour or a cape does nothing to`` + `  hair.` with:
     ```markdown
     - **Only the Head slot decides** (v1.4.8 `Equipment.cs:83-101`), with one engine rule: with no head item and an
       empty Body slot the engine uses `Type4` for the hair (`:93-96`). The same attribute on body armour or a cape does
       nothing to hair.
     ```
   - In the Hair table, replace the `Type4` row's text ``Vanilla `skins.xml` maps it for hair (245 entries, several to `hair_*_type4` meshes)``
     with ``Vanilla v1.4.8 `skins.xml` maps it on 245 of 255 hair meshes, 32 of them to a `*_type4` mesh``.
   - In the Beard table, replace ``In vanilla `skins.xml`, `cover_type1` swaps 4 of the 41 human male beards and the other 37 show in full ([beard ledger](lotrlome-beard-cover-changes.md))``
     with ``In vanilla v1.4.8 `skins.xml`, `cover_type1` swaps 4 of the 41 adult male beards (`beards_c_a`, `_e`, `_j`, `_u`) and the other 37 show in full``.
   - Run this read-only check on the laptop's (WP101-refreshed) Armory:
     `python -I -c "import xml.etree.ElementTree as E,sys;r=E.parse(sys.argv[1]).getroot();print([(x.get('id'),s.get('name'),all(all(b.get('cover_type%d'%i) in (None,b.get('name')) for i in range(1,5)) for b in s.find('beard_meshes').findall('beard_mesh') if b.get('name'))) for x in r.findall('race') if x.get('id') in ('dwarf','saruman') for s in x.findall('skin') if s.find('beard_meshes') is not None])" "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/skins.xml"`.
     If every tuple ends `True`, change the Races sentence's `the Armory's dwarf and Saruman races map` to
     `the Armory's dwarf and Saruman races (checked <today> on the installed Armory) map`; if any is `False` or the
     race is absent, replace the whole Races paragraph with `**Races:** not checked on this line's Armory.`
   - Under `## Current Armory distribution`, from the installed Armory's `ModuleData` folder run
     `grep -rhoi 'hair_cover_type="[^"]*"' --include=head_armors.xml . | tr A-Z a-z | sort | uniq -c` and the same for
     `beard_cover_type`; replace the table's numbers, the date and `in the live Armory` with your counts, today's
     date and `in the installed Armory (`<its SubModule.xml Version>`)`. If no `head_armors.xml` is found, replace the
     section body with `Not measured on this line.`
   - Replace the whole paragraph under `## Changing one` with:
     ```markdown
     The helmets live in the unversioned `LOTRLOME_Armory`, so a reinstall or sync reverts an edit silently and no
     validator reads these two attributes. Back up each `head_armors.xml` outside the module, edit only the attribute
     (keep the file's CRLF or LF, no BOM), diff against the backup, run `python tools/validate_moduledata.py`, and record
     every id, old and new value in the commit body. Generators that write cover types (for example
     `generate_mordor_armor.py`, which emits `all` for both) put the old value back for any id they regenerate.
     ```
3. `docs/modding/items-armor.md`: in the row that begins ``| `hair_cover_type` | enum |``, append before the last
   cell's closing: ` What each type looks like: [helmet-hair-beard-cover](../reference/helmet-hair-beard-cover.md).`
   (the 1.5.x `+` line of `git show 75b7b6661 -- docs/modding/items-armor.md` shows the placement).
4. `docs/INDEX.md`: add the `+` line of `git show 75b7b6661 -- docs/INDEX.md` directly after the line that links
   `modding/items-armor.md`.

**Verify**: `grep -c "lotrlome-beard-cover-changes" docs/reference/helmet-hair-beard-cover.md` prints `0`;
`grep -c "Equipment.cs:83-101" docs/reference/helmet-hair-beard-cover.md` prints `1`; both PNGs exist and are
non-empty; Docs command `dead_links` not above baseline, `ai_dashes: 0`. Commit A5 (five files). Subject
`docs(armour): v2.0.28 - helmet hair and beard cover reference`. `Research:` the ArmorComponent, Equipment and
skins.xml rows. `Not-tested: docs only`. `Ported-from: 75b7b6661d7f2e3193383b58c26ac33ccbcd41cf`.

### Step A6: HeroRace #635 triage and the voice-over correction (from `aed9dea8d`, review fixes `0bb1f3cae`)

1. `docs/features/hero-race.md`: insert lines 223 to 250 of `git show aed9dea8d:docs/features/hero-race.md`
   (from `## Open: Map-Conversation CTD With a Voiced Elf Speaker` to the blank line before `## Changelog`) directly
   before the line `## Changelog`, and insert that file's line 252 (the `- 2026-09-22: #635 opened:` entry) as the first
   line under `## Changelog`. Check: `sed -n 223p` of the source prints the `## Open:` heading, `sed -n 252p` the entry.
2. `docs/features/kingdom-voices.md`: replace the 1.4.x lines from `**This is the one place culture is native**, and it is currently dead for TAOM.`
   through ``GameModel work per `.claude/rules/gamemodels.md`.`` with lines 255 to 281 of
   `git show aed9dea8d:docs/features/kingdom-voices.md`, then change
   ``Same code in v1.4.8 (`DefaultVoiceOverModel.cs:90` in the`` + newline + ``category dump) and v1.5.3 (`taom-src`).``
   to ``Same code in v1.4.8 (taom-src `DefaultVoiceOverModel.cs:90`).`` (rewrap to the file's width).
3. `docs/reference/engine/gauntletui-viewmodel-screen.md`: insert lines 97 to 147 of
   `git show 0bb1f3cae:docs/reference/engine/gauntletui-viewmodel-screen.md` (from `### Map conversation + encyclopedia`
   to the blank line before `## The native boundary`) directly before the 1.4.x line `## The native boundary`. Then, in
   the Evidence list, insert after the bullet that begins ``- `TaleWorlds.ScreenSystem/ScreenBase.cs`:7`` this bullet:
   ```markdown
   - Map conversation + encyclopedia (read 2026-09-22 on the v1.4.8 dump; lines re-anchored 2026-11-04 to the laptop's `C:\Decompiled_Bannerlord\_modules_build`): `SandBox__SandBox.View.cs` `MapConversationTableau.OnFinalize` :12131, `OnTick` :12164 (unguarded read :12198), `OnConversationPlay` :12585, `RemovePreviousAgentsSoundEvent` :12648, `PlayConversationSoundEvent` :12656-12663, `MapScreen` `IMapStateHandler.OnMenuModeTick` :14248 / `OnIdleTick` :14745; `SandBox__SandBox.GauntletUI.cs` `MapConversationTextureProvider` :3354 (`Clear` :3383 → `OnFinalize`, `Tick` :3422 → `OnTick`), `GauntletMapConversationView` :8416 (`Tick` :8530, `TrySetFocus` :8542, called from `OnFrameTick`/`OnIdleTick`/`OnMenuModeTick` :8698/:8704/:8710), `"MapConversation"` layer z 205 :8613, `"EncyclopediaBar"` layer z 310 :10924 with `RegisterActiveStateDisableRequest` :10931; category dump `Core/.../GameStateManager.cs` :193-207 (idle vs tick), `UI/.../TextureWidget.cs` :163-176 (provider ticks only after a visible render); `SandBox__SandBox.cs` `CampaignMissionComponent` lip-sync :16804/:16831.
   ```
4. Append to `docs/reviews/lessons/misc.md` lines 143 to 176 of `git show aed9dea8d:docs/reviews/lessons/misc.md`
   (the two #635 lessons), preceded by one blank line.

**Verify**: `grep -c "^## Open: Map-Conversation CTD" docs/features/hero-race.md` prints `1`;
`grep -c "currently dead for TAOM" docs/features/kingdom-voices.md` prints `0`;
`grep -c "_modules_build_v1.4.8\|v1.5.3 (\`taom-src" docs/reference/engine/gauntletui-viewmodel-screen.md` prints `0`;
Docs command clean. Commit A6 (four files). Subject `docs(herorace): v2.0.28 - #635 CTD triage, voice-over doc corrected`.
`Research:` the map conversation and voice-over rows. `Not-tested: docs only; #635 still needs a dump`.
`Ported-from:` `aed9dea8d8bf2486ec011730adf45fdeabf68991`, `0bb1f3caea18a0f65ea6bfb348b3b087e5579a30`.

### Step A7: toolchain doc: runtime hosts and the elephant skeleton (`8af881f2b`, new work)

1. If `grep -c "ships three hosts" docs/reference/bannerlord-engine-and-toolchain.md` printed `0` in A0, replace
   the table row that begins ``| **`mono-2.0-sgen.dll`** |`` with WP117's text:
   ```markdown
   | **`mono-2.0-sgen.dll`** | The native engine can host **Mono** (sgen GC) through its `WotsMainNative` entry; the import proves only that Mono is linked. v1.4.8 ships three hosts: `Bannerlord.exe` and the launcher are managed .NET Framework executables, `Launcher.Native.exe` starts the bundled .NET 6 CoreCLR, and `WotsMainNative` starts Mono. |
   ```
   (if `docs/reference/engine/mission-frame-threads-and-native-costs.md` exists, keep WP117's trailing link sentence instead).
2. Replace `§4 — the engine receives managed method pointers at startup, then calls back into Mono.` with
   `§4: the engine receives managed method pointers at startup, then calls back into managed code, whichever runtime hosts it (see the `mono-2.0-sgen.dll` row).`
3. Replace the bullet that begins ``- The **skeleton** lives inside a `*_geo.tpac` (e.g. LOTRLOME's `elephant_harad_armor_01_geo.tpac` carries`` (2 lines) with:
   ```markdown
   - The **skeleton** lives inside a `*_geo.tpac` (in the loose Armory tree, LOTRLOME's `adod_elephant_geo.tpac` carries
     `elephant_skeleton`; `elephant_harad_armor_01_geo.tpac`, named here until plan 144, is a 416-byte stub, per the
     1.5.x line's check of the desktop's loose Armory on 2026-09-29; a cooked install packs both into `pack*.tpac`);
     animation **clips** are skeleton-relative (bind by skeleton name).
   ```
4. `docs/INDEX.md`: in the line that links `reference/bannerlord-engine-and-toolchain.md`, replace
   `verified tech stack (Mono, PhysX, Granite, DX11, DLSS)` with `verified tech stack (three runtime hosts, PhysX, Granite, DX11, DLSS)`,
   and on the same line replace `.md) — **the whole engine/toolchain**` with `.md): **the whole engine/toolchain**`.

**Verify**: `grep -c "calls back into Mono\|runs on the \*\*Mono\*\* runtime" docs/reference/bannerlord-engine-and-toolchain.md` prints `0`;
`grep -c "adod_elephant_geo.tpac" docs/reference/bannerlord-engine-and-toolchain.md` prints `1`; Docs command clean.
Commit A7 (two files). Subject `docs(toolchain): v2.0.28 - three runtime hosts, elephant skeleton tpac`.
Body: the host sentence (V01 and V05 of the port audit: `Bannerlord.exe` carries a CLR header; a player log
recorded `CLR: 4.0.30319.42000`), the stub correction (desktop check owed, in-game check 2). `Research:` none on
the engine beyond WP117's; `Not-tested: docs only`. `Ported-from: 8af881f2bd639ee4a9044b665c19ea899a3d0f4e`.

### Step A8: the female dwarf crash decode and the face-morph reference (from `0934f65d2`)

1. `docs/features/arena.md`: after the sentence ``separately (#403); gate: `python tools/validate_mesh_refs.py --no-rgl-log`.`` add:
   ```markdown
   **Corrected 2026-11-04 (plan 144, from the 1.5.x line's decode of 2026-09-26):** the crash's faulting address
   (`0x24C`, 98 x 6) decodes to the static face morph over `sk_dwarf_bm_f1_head.eye`, a face part with no morph channels
   (#385); the underwear name was a second, real defect. What stopped the crashes was the 2026-08-11 stopgap
   (`dccbb9a3b`, on this line) that moved the adult woman onto the male base meshes (`sm_dwarf_basemesh_a1_*`); the
   laptop's installed Armory (2026-09-07) is in that state. The 1.5.x line restored her own meshes in the shared live
   Armory on 2026-09-26, which a 1.4.x release carries only if it pairs with a newer Armory (in-game check 4).
   [race-face-and-hand-morphs.md](../reference/race-face-and-hand-morphs.md).
   ```
2. `docs/reference/lotrlome-armory-snapshot/README.md`: at the end of the section that begins
   `### ⚠️ APPLIED EDIT — female dwarf \`underwear_bottom_mesh\`` (before the next `##` or `###` heading), add:
   ```markdown
   **Correction (2026-11-04, plan 144): the `+0x58232C` crash (v1.4.7) with faulting address `0x24C` was not the
   underwear.** #385's decode reads `0x24C` as 98 x 6, a row of the static face morph over a mesh with no morph buffer,
   and 98 is the vertex count of `sk_dwarf_bm_f1_head.eye`. The underwear fix above stays. The 2026-08-11 stopgap
   (`dccbb9a3b`) moved the adult woman onto `sm_dwarf_basemesh_a1_*`, which is what stopped the crashes
   ([race-face-and-hand-morphs.md](../race-face-and-hand-morphs.md)).
   ```
3. `git show 0934f65d2:docs/reference/race-face-and-hand-morphs.md > docs/reference/race-face-and-hand-morphs.md`, then:
   - In the first paragraph replace `work in Bannerlord v1.5.3,` with `work in Bannerlord (read on v1.5.3 and v1.4.7; see the next paragraph),`
     and insert after that paragraph (before `Evidence tags:`):
     ```markdown
     **On the 1.4.x line (plan 144):** the engine facts below were read from the v1.5.3 native client, with the female
     dwarf's crash offset from v1.4.7 (#385); none was re-derived on v1.4.8. The static face morph's error string
     ("No morph data found for face mesh. Can not do static morph.") is present in the installed v1.4.8
     `TaleWorlds.Native.dll` (byte search, 2026-11-04), so the routine exists; its v1.4.8 offset is not derived. The
     face-morph tools the 1.5.x line built (`add_face_morph_channels.py`, `fit_eye_morphs.py`, `transfer_hand_morphs.py`,
     `check_race_morph_channels.py`, `check_eye_follow.py`, `restore_adult_woman_dwarf.py`) are not on this line; the
     steps below still name them so an author knows what exists there.
     ```
   - Replace `` an access violation at `+0x57070C` on v1.5.3 (`+0x58232C` on v1.4.7).`` with
     `` an access violation at `+0x57070C` on v1.5.3 (`+0x58232C` on v1.4.7; v1.4.8 not derived).``
   - Replace the whole table under `## Tools` (keep the heading) with:
     `None of these is on this line (see the note at the top). Here, `python tools/native_crash_triage.py --rva 0x<offset>` names a native crash site, and `python tools/validate_mesh_refs.py --no-rgl-log` checks mesh names.`
   - In `## Sources`, replace `` "Hand pose morphs" and the face-morph entries;`` with ` (the hill troll entries are on the 1.5.x line);`
     and replace `(the lessons); the community guide's` + newline + ``[race page](../community/bannerlordmodding-lt/guides/custom_creature_race.md) "Face and hand morph channels".``
     with `(the lessons).`
4. `docs/INDEX.md`: after the line added in A5, add
   `- [reference/race-face-and-hand-morphs.md](reference/race-face-and-hand-morphs.md): race face and hand morph channels, the static face morph crash (divide the faulting address by 6 to name the mesh), read on v1.5.3 and v1.4.7`.

**Verify**: `grep -c "custom_creature_race.md" docs/reference/race-face-and-hand-morphs.md` prints `0`;
`grep -c "v1.4.8 not derived" docs/reference/race-face-and-hand-morphs.md` prints `1`; Docs command `dead_links`
not above baseline, `ai_dashes: 0`. Commit A8 (four files). Subject `docs(races): v2.0.28 - dwarf face crash decode, face-morph reference`.
`Research: native string search of v1.4.8 TaleWorlds.Native.dll; laptop Armory skins.xml woman meshes`.
`Not-tested: docs only`. `Ported-from: 0934f65d2f848fb06fcff51ceead0ed1764abc82`.

### Part A done criteria

- [ ] Build exit 0; Test 0 failed, total equal to `AUX/test0.txt`'s total (no test added or removed in Part A).
- [ ] The A1 verify grep prints only the `CLAUDE.md` row.
- [ ] `git grep -n "not persisted" -- Main/Features/Refuge` and `git grep -n -i "last-loaded wins" -- docs .claude ':!docs/reviews/rca-*'` print nothing.
- [ ] Docs command: `ai_dashes: 0`; `dead_links` and `orphan_features` not above `AUX/lint0.txt`.
- [ ] `git status --porcelain` is empty; `git log --oneline "$(cat ../144-aux/base.txt)"..HEAD` lists 8 commits, each subject with `v2.0.28`.
- [ ] `git diff --name-only "$(cat ../144-aux/base.txt)"..HEAD` lists only Part A in-scope files.

---

## Part B: carry the engine reference additions, re-cited and tagged

Every 1.5.x measurement carried here keeps its source and version and gets the words "not re-measured on
v1.4.8" unless the engine table above covers it.

### Step B1: mission start, behaviour removal and the main-menu hook (`b5e1d0b3d`, `4d79eca85`, `0bb1f3cae`, `ecaa67d26`'s lifecycle hunk deferred here by WP109)

Prerequisite: `docs/features/mission-start-guard.md` exists (else STOP).

1. `docs/reference/engine/mission-and-missionbehavior-lifecycle.md` (source file state: `0bb1f3cae`):
   - Insert lines 55 to 87 of `git show 0bb1f3cae:docs/reference/engine/mission-and-missionbehavior-lifecycle.md`
     (`### Removing a behaviour during \`OnMissionTick\` (v1.5.5)` through its **Rule** paragraph and the blank line)
     directly before the 1.4.x heading `### Lifecycle order (typical)`. In the inserted text apply:
     `(v1.5.5)` in the heading → `(v1.4.8)`; `(TAOM-verified 2026-10-15 from the v1.5.5 managed decompile;` →
     `(Read on the 1.5.x line 2026-10-15 from v1.5.5; every line below re-read on v1.4.8 with taom-src for plan 144;`;
     `` `Mission.cs:3750-3753` `` → `` `Mission.cs:3741-3744` ``; `` `Mission.cs:4707-4724` `` → `` `Mission.cs:4643-4660` ``;
     `re-checked on v1.5.5` → `re-checked on v1.4.8`; `(TAOM's parked` + newline + `` `ShaderPrecompilePlayerAgentGuard` does,`` →
     `` (TAOM's `ShaderPrecompilePlayerAgentGuard` does on its Order of Battle fallback, `ShaderPrecompilePlayerAgentGuard.cs:94`,``;
     `` (`:181-184`) `` → `` (`:162`, the call at `:183`) ``.
   - Replace the 1.4.x line `  → OnBehaviorInitialize → OnAfterMissionCreated → EarlyStart → AfterStart` with:
     ```text
       → EarlyStart → AfterStart   (TAOM's path: OnBehaviorInitialize and OnAfterMissionCreated never fire for it)
       [a behaviour handed to MissionState.OpenNew: OnAfterMissionCreated → OnCreated → OnBehaviorInitialize → EarlyStart
        → AfterStart; v1.4.8 Mission.cs:3809-3816 (OnBehaviorInitialize runs before the submodules add TAOM's
        behaviours), MissionState.cs:263]
     ```
   - Insert lines 127 to 149 of the same source (`### An exception in \`AfterStart\` reloads the mission forever`
     through `throw-safe stays the rule: the guard saves the battle, not the behaviour's setup.` and the blank line)
     directly before the 1.4.x heading ``## ⚠️ The `: MissionLogic` gotcha``. In it apply:
     `(yotthani, MithrilForge \`docs/engine/bugs.md\` B3, v1.5.3; v1.5.4: the managed sequence confirmed in` + newline +
     `the v1.5.4 decompile via \`taom-src\`, the swallowed exception and the log line not re-checked.)` →
     `(yotthani, MithrilForge \`docs/engine/bugs.md\` B3, v1.5.3. On v1.4.8 the managed sequence is the same, re-read with taom-src for plan 144: \`MissionState.cs:333-352\`, \`AfterStart\` at \`:345\`, \`TickLoading\` \`:221-233\`, \`LoadMission\` \`:235-243\`. On this line SaveShield's MISSION-INIT finalizer on \`FinishMissionLoading\` swallows the throw; the log line is not re-checked.)`;
     `Since 2026-10-08 TAOM's mission-start guard (Patch103,` → `Since plan 109 (the 1.5.x guard of 2026-10-08, ported to this line) TAOM's mission-start guard (Patch103,`.
2. `docs/reference/engine/submodule-lifecycle-and-harmony.md`:
   - In the table row that begins ``| **`OnBeforeInitialModuleScreenSetAsRoot()`**``, append to its last cell:
     ``` **A throw here repeats every tick (v1.4.8):** `Module.OnApplicationTick` calls `SetInitialModuleScreenAsRootScreen` on every tick while the global state manager's `ActiveState` is null (`Module.cs:498-511`), and that method calls every module's callback with no `try` (`:750-758`). A throw from any module's callback therefore leaves `ActiveState` null and runs every module's callback again on the next tick. TAOM's guard (#807, plan 112): the main-menu customization runs through `MainMenuStep.Run` (`Main/Composition/MainMenuStep.cs`), which never rethrows, logs one `[MainMenuStep]` WARNING per step name per process and runs the step again on the next call, so the step must be safe to repeat; `TAOM.Tests/Composition/SubModuleMainMenuHookTests.cs` pins it. Another module's throw is not contained by this.```
     If WP112's files are absent, append only the first three sentences. On the same row replace
     `(the native MinHook layer — managed Harmony can't touch native; **parked 2026-07-08** — the install call` with
     `(the native MinHook layer, since managed Harmony can't touch native; **parked 2026-07-08**: the install call`.
   - Insert after the bullet that begins `- **Deferred application**` this bullet:
     ```markdown
     - **A patch method never lives on a closed generic type, and is never a `DynamicMethod`.** Harmony 2.4.2 records
       each patch method as module plus metadata token and re-resolves it whenever another patch lands on the same
       target. The token carries no type arguments, so it resolves to the open generic type's method, and every later
       patch on that target throws `InvalidProgramException` in `PatchFunctions.UpdateWrapper`. In game it
       disabled three patches for the session. A test with only one patch per target cannot see it; test "our patch,
       then a foreign patch" and the reverse. (yotthani, MithrilForge `docs/engine/perf.md`, PerfProbe section of
       2026-10-04, in game on v1.5.3 with Harmony 2.4.2; this line ships the same Harmony 2.4.2, which the 1.4.8 player
       logs name as `0Harmony v2.4.2.0`.)
     ```
3. `docs/features/shader-precompilation.md`: at the end of the section `### Deployment fallback guard` (before the
   next `##`/`###` heading) add:
   ```markdown
   **Open fix item (plan 144, from the 1.5.x line's v1.5.5 check of 2026-10-15):** `ShaderPrecompilePlayerAgentGuard.OnMissionTick`
   calls `deployment.FinishDeployment()` (`ShaderPrecompilePlayerAgentGuard.cs:94`) from inside the mission's tick loop, and
   `FinishDeployment` ends with `RemoveMissionBehavior(this)` (v1.4.8 `DeploymentMissionController.cs:41-80`). The loop counts
   down by index over the live list (`Mission.cs:3741-3744`) and the removal shifts every later entry (`:4643-4660`), so if the
   controller sits below the guard's index the guard ticks twice that frame (the registration order was not checked). Move
   the call out of the tick: set a flag in `OnMissionTick` and finish deployment at the start of the next frame. The path is
   the Order of Battle fallback only and has not run in game since #560. Mechanism:
   [mission-and-missionbehavior-lifecycle.md](../reference/engine/mission-and-missionbehavior-lifecycle.md) "Removing a behaviour during `OnMissionTick`".
   ```

**Verify**: `grep -c "Mission.cs:3741-3744" docs/reference/engine/mission-and-missionbehavior-lifecycle.md` ≥ 1;
`grep -c "3750-3753\|4707-4724\|181-184" docs/reference/engine/mission-and-missionbehavior-lifecycle.md` prints `0`;
`grep -c "Module.cs:498-511" docs/reference/engine/submodule-lifecycle-and-harmony.md` prints `1`; Docs command
clean. Commit B1 (three files). Subject `docs(engine): v2.0.28 - AfterStart reload, removal in tick, menu hook`.
`Research:` the MissionState, Mission, DeploymentMissionController and Module rows.
`Not-tested: docs only; the shader-precompilation item is a fix for a later issue`.
`Ported-from:` `b5e1d0b3d679d539f2752b7fb8a11bfef6d06203`, `4d79eca85d7c383326577debd5f8fe3e856a9173`,
`0bb1f3caea18a0f65ea6bfb348b3b087e5579a30`, `ecaa67d26463f7652c8235ebff54a7fe78e030a7`.

### Step B2: what a save costs, campaign-map costs, listener order (`b5e1d0b3d`, `7eac30153`, `2ac85fbe7`, `0bb1f3cae`, fact from `ac1d8edbc`)

1. `docs/reference/engine/save-system.md`: insert lines 82 to 119 of `git show 2ac85fbe7:docs/reference/engine/save-system.md`
   (`## Performance: what a save costs [yotthani]` through the blank line before `## The native boundary`) directly
   before the 1.4.x heading `## The native boundary`. Then, right under the inserted heading, add:
   ```markdown
   **On the 1.4.x line (plan 144):** these are yotthani's measurements of a 145,000 to 152,000 object save on 1.5.3, not
   re-measured on v1.4.8. The 1.4.8 player saves in the port audit spent 34 to 1,034 s in the Containers phase alone
   (plan 114), so do not read these figures as the 1.4.x cost. The mechanisms below are the same on v1.4.8:
   `Game.OnSaveCompleted` calls `Common.MemoryCleanupGC()` (`Game.cs:216-224`), and `SaveContext` walks objects and
   containers on parallel workers (`SaveContext.cs:137`, `:157`, `:372`, `:398`), which is why string ids depend on worker
   order. Server GC is NOT advised on this line: v1.4.8 ships three runtime hosts, the players' host is unknown, and it
   costs 1.4 to 1.7 GB of private memory on machines already at 80 to 88 % memory load (plan 114, V01).
   ```
   And replace the bullet that begins ``- **A save loaded offline without its load callbacks gives every `TextObject` a hash of 0**``
   (through its last line) with:
   ```markdown
   - **A save loaded offline without its load callbacks gives every `TextObject` a hash of 0.** v1.4.8 `TextObject.GetHashCode`
     returns `_internalId` (`TextObject.cs:264-267`); the private constructor sets 0 (`:64-69`), and only the public
     constructor and the `[LoadInitializationCallback]` assign a fresh id (`:71-76`, `:164-168`). Offline, with no
     callback, the save's object dictionary therefore degenerates (`CollectObjects` 3.5 s instead of 0.2 s, yotthani). In
     game, distinct instances hash apart and only `GetEmpty()` instances share id 0 (V01 of the port audit corrects
     the claim that equal-content `TextObject`s collapse in a save).
   ```
2. `docs/reference/engine/campaign-tick-time-and-party-ai.md`:
   - Insert lines 99 to 136 of `git show 0bb1f3cae:docs/reference/engine/campaign-tick-time-and-party-ai.md`
     (`## Measured costs on TAOM's map [yotthani]` through the blank line before `## The native boundary`) directly
     before the 1.4.x heading `## The native boundary`, and add under its heading:
     `**On the 1.4.x line (plan 144):** measured on 1.5.3, not re-measured on v1.4.8. This line has the NameplateCull (plan 120) and the ButterLib distance matrix switch (plan 118) the bullets name.`
     If `docs/features/nameplate-cull.md` or `butter-lib-distance-matrix.md` is absent, delete the inserted links to it
     and keep the plain text.
   - At the end of the bullet list under `## TAOM relevance + gotchas`, add:
     ```markdown
     - **Listeners run newest first** (v1.4.8 taom-src `MbEvent.cs:24-30`, `:32-44`): `AddNonSerializedListener` puts each new
       record at the head of the list and `Invoke` walks from the head, so a listener added after the engine's own runs
       BEFORE it on the same event. (yotthani's CampaignProbe README observed the same on 1.5.5.)
     ```

**Verify**: `grep -c "^## Performance: what a save costs" docs/reference/engine/save-system.md` prints `1`;
`grep -c "NOT advised on this line" docs/reference/engine/save-system.md` prints `1`;
`grep -c "MbEvent.cs:24-30" docs/reference/engine/campaign-tick-time-and-party-ai.md` prints `1`; Docs command clean.
Commit B2 (two files). Subject `docs(engine): v2.0.28 - save cost, campaign map costs, listener order`.
`Research:` the Game, SaveContext, TextObject and MbEvent rows. `Not-tested: docs only`.
`Ported-from:` `b5e1d0b3d679d539f2752b7fb8a11bfef6d06203`, `7eac30153353ae401ef37c0955a29e92bb5d4b25`,
`2ac85fbe777a3cdc1513ba76402b8563fcea3770`, `0bb1f3caea18a0f65ea6bfb348b3b087e5579a30`, `ac1d8edbc82f01af77812972d14b980259108bf3`.

### Step B3: team AI, morale and the crush thread (`578770e03`, `5dfa8f4cb`'s table, `d690ff8d2`, `b5e1d0b3d`)

1. `docs/reference/engine/formations-and-team-ai.md`, Team paragraph: replace
   ``**`FormationsIncludingEmpty`** (`MBList<Formation>(8)` —`` + newline + ``always 8 slots by `FormationClass`, :34/:268); **`TeamAI`**`` with:
   ```markdown
   **`FormationsIncludingEmpty`** (`MBList<Formation>(8)`, always 8 slots by `FormationClass`, :34/:268) and
   **`FormationsIncludingSpecialAndEmpty`** (10: the same eight plus General (8) and Bodyguard (9), :36/:267; the list the
   engine's own enemy walks use, `Formation.CacheClosestEnemyFormation` (`Formation.cs:1361-1371`) and `TeamQuerySystem`
   (`:190`, `:234`, `:291`), and the one a TAOM scan of ENEMY formations must use too, since
   `GeneralsAndCaptainsAssignmentLogic` fills the general and bodyguard formations in an ordinary field battle
   (`SandBoxMissions.cs:746`, `:855`); own-team splits and snapshots stay on the eight); **`TeamAI`**
   ```
2. Same file: insert directly before the heading `### Agent ↔ Formation ↔ Detachment (Agent.cs)`:
   ```markdown
   ### The tactic layer on v1.4.8 (plan 144)

   Adapted from the 1.5.x line's v1.5.3 table (`5dfa8f4cb`, `578770e03`); every row re-read on the installed v1.4.8
   with taom-src or the dump.

   | Fact | Where (v1.4.8) |
   |---|---|
   | Field battles: every team gets `new TeamAIGeneral(mission, team)`; tactic options are registered per team by the side's best commander Tactics skill (`Charge`; >= 20 `FullScaleAttack` plus defender `DefensiveEngagement`/`DefensiveLine` or attacker `RangedHarrassmentOffensive`; >= 50 `FrontalCavalryCharge` plus defender `DefensiveRing`/`HoldChokePoint` or attacker `CoordinatedRetreat`), then `QuerySystem.Expire()` and `ResetTactic()` | `MissionCombatantsLogic.cs:100`, `:152-177`, `:218-219` |
   | The team decides every 5 s: `MaxBy(GetTacticWeight() * (current ? 1.5 : 1))`, and a switch happens only when the new weight beats 1.5 x the current tactic's; with no enemy formation left it forces `TacticCharge` | `TeamAIComponent.cs:206`, `:276-290`, `:301`, `:316-321` |
   | `_availableTactics` is a private readonly `List` with no getter; `Team.AddTacticOption`, `RemoveTacticOption(Type)`, `ClearTacticOptions` and `ResetTactic` are public forwarders; `Team.AddTeamAI` is public and replaces the component | `TeamAIComponent.cs:36`, `:131-141`; `Team.cs:415-447` |
   | A tactic's weight is `protected internal virtual float GetTacticWeight()`; the base exposes protected `_mainInfantry` and its siblings, `AssignTacticFormations1121`, `ManageFormationCounts(int, int, int, int)` and public static `SetDefaultBehaviorWeights` | `TacticComponent.cs:26`, `:475`, `:520`, `:581`, `:589` |
   | The defensive tactics return 0 unless `IsDefenseApplicable`; `TacticDefensiveRing` builds a runtime `TacticalPosition` (public constructor) for an insurmountable region; `TeamAIComponent.TacticalPositions` is a public mutable list | `TeamAIComponent.cs:50`, `:66`, `:215-247`; `TacticDefensiveRing.cs:117`, `:180`; `TacticalPosition.cs:105` |
   | `FormationAI` ticks every 0.5 s, staggered per team; the winning behaviour is `GetAIWeight() * WeightFactor`, the running one boosted by a factor that slides from 2.0 to 1.2 over its preserve window (10 s); orders apply only if `IsAIControlled`; `SetBehaviorWeight<T>` throws `MBException` for an unregistered T | `FormationAI.cs:63`, `:112-116`, `:130`, `:138`, `:179-182`, `:245`, `:286` |
   | `TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime` registers 24 single-player field behaviours per formation on its first unit (from `Formation.AddUnit`); a formation filled by a split gets them too, because `TransferUnitsAux` and `TransferUnitsWithPriorityFunction` set `Agent.Formation`, whose setter calls `AddUnit` | `TeamAIGeneral.cs:57-85`; `Formation.cs:2112`, `:2158`, `:1988`/`:2048`, `:2490`/`:2505`; `Agent.cs:1098`, `:1142` |
   | THREAD: `Team.Tick` runs inside `Mission.TickAgentsAndTeamsImp`, the agent-tick job (async in single player, see the thread table in `.claude/rules/harmony-patches.md`), before `tickCompleted = true`, and gates on `Mission.AllowAiTicking`, which `DeploymentMissionController` holds false through deployment | `Mission.cs:3601-3613`; `Team.cs:573-579`; `DeploymentMissionController.cs:31`, `:75` |
   | `MissionCombatantsLogic.GetAllCombatants()` and the static `SupportsAllyTeamOnPlayerSide(...)` are public | `MissionCombatantsLogic.cs:228`, `:261` |
   | `Mission.GetAgentTroopClass_Override` (a public event, consulted first) is subscribed by vanilla controllers outside field battles (hideouts, alley fights); TAOM does not subscribe it on this line. The 1.5.x line's "unsubscribed in vanilla" is wrong on v1.4.8 | `Mission.cs:1555`, `:2541-2543`; `HideoutMissionController.cs:441`, `HideoutAmbushMissionController.cs:155`, `AlleyFightMissionHandler.cs:34` |

   ### Morale: casualty shock (v1.4.8, plan 144)

   Re-derived on the 1.5.x line from v1.5.5 (`d690ff8d2`) and re-read on v1.4.8 (the dump; `SandboxBattleMoraleModel`
   through taom-src). Morale is per agent, never per formation.

   | Constant | Value | Source (v1.4.8) |
   |---|---|---|
   | Radius of the shock around a casualty or a panicking soldier | 4 m | `AgentMoraleInteractionLogic.cs:12`, passed at `:53` and `:64` |
   | Soldiers that lose morale, and soldiers that gain it | at most 10 each | `:14-16` |
   | Who loses | up to 10 random friends of the casualty within the radius, topped up from the casualty's own formation when fewer than 10 | `:81-84` |
   | Who gains | the killer if AI-controlled, human and an enemy, then random enemies of the casualty within the radius | `:86-93` |
   | Per-soldier change | the model's maximum divided by `max(1, MoraleResistance)` | `CustomBattleMoraleModel.cs:72-75` |
   | Weapon factor k for a kill | 0.75 melee, 0.5 ranged, 0.25 area-of-effect or multiple-penetration, then x1.25 with both Burning and MultiplePenetration | `CustomBattleMoraleModel.cs:20-31`; taom-src `SandboxBattleMoraleModel.cs:60-71` |
   | Gain for the killer's side | `battleImportance * 3 * k` | `CustomBattleMoraleModel.cs:34`; `SandboxBattleMoraleModel.cs:75` |
   | Loss for the casualty's side | `battleImportance * 4 * k * casualtiesFactor` | `CustomBattleMoraleModel.cs:35`; `SandboxBattleMoraleModel.cs:77` |
   | `casualtiesFactor` | `1 + 2 * (share of the side already removed)` | `CustomBattleMoraleModel.cs:87-98` |
   | A panic | friends lose `battleImportance * casualtiesFactor * 1.1`, enemies gain `battleImportance * 2` | `CustomBattleMoraleModel.cs:51-57` |

   **The break cascade** [yotthani, measured on v1.5.5 with TAOM 2.0.34, MithrilForge `docs/engine/battle-behaviour-measured.md`
   lines 94-95; not re-measured on v1.4.8]: a side starts to break at about 54 % of its strongest number (range 42 to
   65 %), then goes from 50 % to 10 % in 3 to 14 s in small battles, a cascade of single panics, not formations
   turning. The Dread Aura's morale constants are in [dread-aura.md](../../features/dread-aura.md).
   ```
3. `docs/features/combat-mechanics.md`: after the paragraph that ends ``nothing.`` following
   ``TAOM's own gate requires `HasMeleeWeapon` + `IsSwing`)`` (the orc javelin paragraph), add:
   ```markdown
   `GetDefendCollisionResults` is an engine callback marked `[MBCallback(null, true)]` (multi-thread callable; v1.4.8
   `Mission.cs:6454-6456`), so `DecideCrushedThrough` (`MissionCombatMechanicsHelper.cs:311`) may run off the main thread;
   yotthani's v1.5.3 decompile shows native calling it from inside the parallel melee sweep (not re-traced on v1.4.8). The
   crush path logs nothing per call and reads only state built at construction, apart from the crush roll's draw on the
   engine's shared generator (`randomRoll: MBRandom.RandomFloat` in `CombatMechanicsHooks`), which the port programme
   keeps (decision D16: concurrent draws cannot plausibly leave that generator stuck). Keep anything added to the path
   read-only (the thread table in `.claude/rules/harmony-patches.md`).
   ```
   If `CombatMechanicsHooks.cs` was absent in A0, write `TaomCombatMechanicsModel` instead of `CombatMechanicsHooks`.

**Verify**: `grep -c "^### The tactic layer on v1.4.8" docs/reference/engine/formations-and-team-ai.md` prints `1`;
`grep -c "^always 8 slots by" docs/reference/engine/formations-and-team-ai.md` prints `0`;
`grep -c "culture-doctrine" docs/reference/engine/formations-and-team-ai.md` prints `0`;
`grep -c "MBCallback(null, true)" docs/features/combat-mechanics.md` prints `1`; Docs command clean. Commit B3 (two
files). Subject `docs(battle): v2.0.28 - team AI tactic layer and morale on v1.4.8`. `Research:` the Team, team AI,
formation AI, morale and GetDefendCollisionResults rows. `Not-tested: docs only`. `Ported-from:`
`578770e03e369ffaba01923810ac94fb7b5c20a2`, `5dfa8f4cb0a9e86118ed41b0c6bdd3d9a977990b`,
`d690ff8d2133f2a2a4a2f79989ef7a4fd0195413`, `b5e1d0b3d679d539f2752b7fb8a11bfef6d06203`.

### Step B4: render, memory and animation leads (`b5e1d0b3d`, `7eac30153`, `2b8f5ff36`, `2ac85fbe7`, `8dd76ddb3`, `4520af802`, `0bb1f3cae`)

1. `docs/reference/engine/agent-spawn-and-render-pipeline.md`: insert lines 136 to 197 of
   `git show 0bb1f3cae:docs/reference/engine/agent-spawn-and-render-pipeline.md` (the state after the review restored the lab's hedge; `## After spawn: pose, LOD and shadows each frame (v1.5.3) [yotthani]`
   through the end of `## Agent-less figures [yotthani]`, the line before `## Evidence`) directly before the 1.4.x heading
   that begins `## Evidence (file:line, v1.4.5`. Under the first inserted heading add:
   `**On the 1.4.x line (plan 144):** native behaviour read on v1.5.3, not re-checked on v1.4.8. The managed members named here exist on v1.4.8 (`Skeleton.ForceUpdateBoneFrames`, `GetBoneEntitialFrame`, `MBAgentVisuals.SetAgentLodZeroOrMax`, `GameEntity.SetEnforcedMaximumLodLevel`, `Agent.VisualPosition`), and the strings `animation_sampling_quality`, `human_shadow_mesh` and `horse_shadow_mesh` are present in the v1.4.8 `TaleWorlds.Native.dll` (byte search, 2026-11-04); `do_timer_based_skeleton_forced_updates` is not, so treat that option name as unconfirmed here.`
   In the agent-less section keep ``Main/Features/HeroRace/CharacterSpawnerService.cs` (about 261-290)`` (true on this line).
2. `docs/reference/engine/animation-binding-and-playback.md`: insert after the code block that contains
   `Biped = { ragdoll stationary-check bones, foot-IK bones, ... }` (after its closing fence) the `+` paragraph that
   begins `**Step size and gait speed.**` from `git show 2b8f5ff36 -- docs/reference/engine/animation-binding-and-playback.md`,
   adding at its end ` Not re-measured on v1.4.8.`.
3. `docs/reference/engine/gauntletui-viewmodel-screen.md`: insert lines 71 to 96 of
   `git show 0bb1f3cae:docs/reference/engine/gauntletui-viewmodel-screen.md` (the three bullets
   `- **Every return to the map leaks GPU memory**`, `- **Generated UI prefabs are off with UIExtenderEx loaded`,
   `- **First-use shader compiles can block one frame`) directly after the 1.4.x bullet that begins
   `- **Overlay input wiring**`. In the first bullet replace ``should free them (read from`` + newline + two spaces + `code, not measured)`
   with ``should free them (v1.4.8 modules dump `SandBox__SandBox.View.cs:2275`, defined `:13931`; read from code, not measured)``
   and append to it ` On this line [map-view-release.md](../../features/map-view-release.md) (plan 120) releases them every Nth cover; `ViewSubModule.BannerTexturedMaterialCache` is the same `Dictionary<Tuple<Material, Banner>, Material>` on v1.4.8 (`Native__TaleWorlds.MountAndBlade.View.cs:4765`).`
   (drop the map-view-release link if that doc is absent). In the third bullet replace
   ``still open in`` + newline + two spaces + `` `plans/_audit/2026-10-02-perf/REPORT.md` lines 121-122, which this note leaves as it is.`` with
   `still open (1.5.x perf audit, not on this line).`
4. `docs/features/map-view-release.md` (skip if absent): after the bullet that begins
   `- **Measurement (yotthani, not reproduced by TAOM):**`, insert the `+` bullet that begins `- **More from the same source**`
   from `git show 2ac85fbe7 -- docs/features/map-view-release.md`, replacing `TAOM has not reproduced it on 1.5.6` with
   `TAOM has reproduced it on neither 1.5.6 nor v1.4.8`, and deleting the clause
   `, so it is not the same test as the second-scene-view study below`.
5. `docs/reference/engine/mission-frame-threads-and-native-costs.md` (skip if absent): at the end of the bullet list
   under `## 3. Agent capacity` (before `## 4.`), insert the two `+` bullets that begin
   `- **Soldiers held in reserve near the limit hung the battle start**` and `- **Log volume at a mass spawn**` from
   `git show 2ac85fbe7 -- docs/reference/engine/mission-frame-threads-and-native-costs.md`, and append to the second
   ` The message string is present in the v1.4.8 native DLL (byte search, 2026-11-04).`
6. `docs/features/shader-precompilation.md`: at the end of the section `## v1.4.8 and the shader cache` (before
   its next `###`/`##` heading) insert the `+` paragraph that begins `**Shaders built on first use after a graphics change**`
   from `git show 2ac85fbe7 -- docs/features/shader-precompilation.md` (replace `not re-measured by TAOM on 1.5.6` with
   `not re-measured by TAOM on 1.5.6 or v1.4.8`), then the section `## Unattended game runs: two leftovers after a kill (yotthani)`
   from `git show 8dd76ddb3 -- docs/features/shader-precompilation.md` (its `+` lines), placed before `## Configuration`,
   with `(the cold-cache protocol below)` replaced by `(a cold-cache walk)`.
7. `docs/reference/bannerlord-animation-clip-flags.md`: replace the 1.4.x row that begins
   ``| `anf_enforce_weapon_tip_with_rope_stretched` / `_relaxed` |`` with the `+` row of the same name in
   `git show 2b8f5ff36 -- docs/reference/bannerlord-animation-clip-flags.md`; insert that diff's four `+` paragraphs
   (`**Rope flags need a companion clip.**`, `**Which weapons.**`, `**Current exposure.**`, `**Not the same bits as \`anf_keep\` and \`anf_restart\`.**`)
   after the table that row belongs to (before the next `##` heading), taking the `**Rope flags need a companion clip.**` paragraph
   from `git show 0bb1f3cae:docs/reference/bannerlord-animation-clip-flags.md` instead (the review narrowed it to the rope path), and in them: replace
   ``TAOM re-checked the entry `0x701450` on v1.5.5, 2026-10-15.)`` with ``TAOM re-checked the entry `0x701450` on v1.5.5, 2026-10-15; not re-checked on v1.4.8, where that address is not valid.)``;
   replace the `**Which weapons.**` paragraph's first sentence with
   ``**Which weapons.** v1.4.8 `SandBoxCore` `items/weapons.xml` declares `skeleton_name="flail_skeleton"` 2 times, `sling_skeleton` 3 times and `shackle_skeleton` 1 time (installed, 2026-11-04).``;
   in `**Current exposure.**` append ` Not re-scanned for the Armory package a 1.4.x release ships.` Delete any sentence that links
   `bannerlord-animation-system-map.md` or `tpac-static-prop-authoring.md`.
8. `docs/ai-includes/creature-mount-authoring.md`: after the table row that begins `| 8 | **The skeleton's SkeletonUserData**`,
   insert row 9 (the line that begins `| 9 | **The FBX dialog's remembered settings.**`) from `git show 0bb1f3cae:docs/ai-includes/creature-mount-authoring.md`
   (not `2b8f5ff36`, whose registry path carries a stray form feed), replacing
   `key confirmed on TAOM's desktop 2026-10-15;` with `key confirmed on TAOM's desktop 2026-10-15 (this laptop had only HKCU\Software\TaleWorlds\RGL on 2026-11-04);`.
9. Only if A0 found `VERBATIM` in `docs/reference/bannerlord-skeleton-authoring.md` (WP141): insert the numbered fact that begins
   `4. **A master's bone tracks are stored in FBX NODE order, and the engine reads slot i as bone i of the` from
   `git show 4520af802 -- docs/reference/bannerlord-skeleton-authoring.md` after the doc's fact 3 (keep its wording;
   append ` Measured with the 1.5.x-era Kit; not re-measured with the v1.4.8 Kit. The transfer tool it names is on the 1.5.x line only.`),
   and in `docs/community/bannerlordmodding-lt/guides/custom_creature_troubleshooting.md` insert the `+` row that begins
   `| Some limbs play another limb's motion` from `git show 4520af802 -- docs/community/bannerlordmodding-lt/guides/custom_creature_troubleshooting.md`
   after the row fixed in A1, deleting its last sentence (the one naming `transfer_clip_to_engine_rig.py`).

**Verify**: `grep -c "^## After spawn: pose, LOD and shadows" docs/reference/engine/agent-spawn-and-render-pipeline.md` prints `1`;
`grep -c "On the 1.4.x line (plan 144)" docs/reference/engine/agent-spawn-and-render-pipeline.md` prints `1`;
`grep -c "plans/_audit/2026-10-02-perf" docs/reference/engine/gauntletui-viewmodel-screen.md` prints `0`;
`grep -rc "bannerlord-animation-system-map.md" docs/reference/bannerlord-animation-clip-flags.md` prints `0`;
`grep -c "flail_skeleton\"\` 2 times" docs/reference/bannerlord-animation-clip-flags.md` prints `1`; Docs command
`dead_links` not above baseline, `ai_dashes: 0`. Commit B4 (every file this step touched). Subject
`docs(engine): v2.0.28 - render, memory and animation leads, tagged`. `Research:` the render-member, native-string,
ClearGPUMemory and weapons.xml rows. `Not-tested: docs only; in-game check 5`. `Ported-from:` the seven SHAs in this step's title, each in full (without `4520af802` if sub-edit 9 was skipped).

### Step B5: static props in a tpac, and the camp-package gate (from `991f415f4`)

1. **RED**: `git show 991f415f4:tools/tests/test_prefab_asset_packages.py > tools/tests/test_prefab_asset_packages.py`.
   If `tools/tpac_clone_metamesh.py` is absent, run One tool test: it must FAIL with `ModuleNotFoundError: No module named 'tpac_clone_metamesh'`.
2. **GREEN**:
   - If `tools/tpac_clone_metamesh.py` is absent: `git show 991f415f4:tools/tpac_clone_metamesh.py > tools/tpac_clone_metamesh.py`
     (do NOT take `tools/tests/test_tpac_clone_metamesh.py`: it imports `xxhash` at module load, which this laptop's
     Python lacks, so it would add an import error to the tool-test baseline).
   - If it is present (WP141): apply only `991f415f4`'s hunk of that file:
     `git show 991f415f4 -- tools/tpac_clone_metamesh.py > ../144-aux/tcm.patch && git apply --check ../144-aux/tcm.patch && git apply ../144-aux/tcm.patch`;
     if `--check` fails because WP141 already carries the lazy imports (`grep -c "^import xxhash" tools/tpac_clone_metamesh.py` prints `0`), skip it.
   - One tool test: `OK`, `Ran 5 tests`.
3. `git show 991f415f4:docs/reference/tpac-static-prop-authoring.md > docs/reference/tpac-static-prop-authoring.md`, then:
   - In the opening paragraph replace `Review:` + newline + `[adopt-mithrilforge-2026-09-29.md](../reviews/adopt-mithrilforge-2026-09-29.md).`
     with `Review: the 1.5.x line's `docs/reviews/adopt-mithrilforge-2026-09-29.md` (not on this line).` (the review record stays out).
   - Replace lines 10 to 39 (from `## What TAOM has built this way, and why none of it renders` to the line before
     `## The recipe`) with:
     ```markdown
     ## What TAOM has built this way, on the 1.4.x line

     **TAOM:** the four packages in `Main/_Module/AssetPackages/` (`fieldcamp_camp_a`, `fieldcamp_palisade_ring`,
     `refuge_camp_a`, `refuge_palisade_ring`) are MithrilForge output, byte-identical on both lines. Each holds five items,
     named `<mesh>_d`, `<mesh>_n`, `<mesh>_s`, `<mesh>` (the Metamesh) and `<mesh>_mat`. `CampLayoutBuilder.PlaceCenteredPrefab`
     asks for them by name; on a miss the caller draws the vanilla siege-camp layout, and nothing is logged beyond the
     entity count.

     **Whether the 1.4.8 build loads them is UNVERIFIED.** The 1.5.x line found on 2026-09-29 that its releases carried only
     the editor's `pack0.tpac` and that its dev install read TAOM's loose `Assets/` tree instead of `AssetPackages/`
     ([armory-guide.md](armory-guide.md) "Two asset trees"), so its camps showed the fallback. This laptop's installed
     v2.0.28 TAOM ships all four beside `pack0.tpac` in `AssetPackages/` and also has a loose `Assets/` tree (2026-11-04).

     **How to tell in game:** `taom_debug_*.log` records `[FieldCamp] placed N ...` and `[Refuge] placed N ...`. A Field camp
     standing as the prefab places one entity; the fallback places a command tent plus a ring of tents.

     **Gate:** `tools/tests/test_prefab_asset_packages.py` keeps the source copies and the code in step: the prefab meshes
     the code passes to `PlaceCenteredPrefab` are exactly its table, each is a Metamesh in a package here, no item name or
     GUID repeats, and every package is structurally sound. It cannot see whether the game loads them.

     ```
   - In `## Package layout facts`: replace `measured on the four packages and the 1.5.3 install on 2026-09-29.` with
     `measured on the four packages (2026-09-29) and, for the vanilla figures, re-measured on the installed v1.4.8 (2026-11-04).`;
     keep `6,266 segments, no gap, no pad` and `all 228` (both re-measured on v1.4.8); replace
     ``existed ([animation system map](bannerlord-animation-system-map.md) section 6).`` with `existed (1.5.x line's animation system map, not on this line).`
   - In `## Names`, replace ``(`_7` on 1.4.6 per MithrilForge; `_9` on 1.5.3, read by TAOM)`` with
     ``(`_7` on 1.4.6 per MithrilForge and on v1.4.8 by a byte search of the installed packages; `_9` on 1.5.3)``.
4. `docs/reference/bannerlord-engine-and-toolchain.md`: after ``game. Deleting `AssetPackages` locally costs nothing; Publish Module regenerates it.`` add:
   `The corollary: a hand-written package dropped into `AssetPackages/` is not read on a dev install that has a loose `Assets/` tree, and Publish Module does not carry it into a release unless it is in the module's assets. Whether TAOM's four MithrilForge camp props render on the 1.4.8 build is an open in-game check ([tpac-static-prop-authoring.md](tpac-static-prop-authoring.md)).`
5. `docs/reference/provenance-register.md`: at the end of the section `### Yotthani commissioned modules (FieldCamp, Refuge, SupplyLines)` add:
   `The four `AssetPackages/*.tpac` above are MithrilForge's output (its item layout, read on the 1.5.x line 2026-09-29); the tool is irrelevant to their terms, which remain the commissioned clearance above. MithrilForge's facts are restated, attributed, in [tpac-static-prop-authoring.md](tpac-static-prop-authoring.md) (plan 144); `tools/tests/test_prefab_asset_packages.py` is TAOM's own and ports no MithrilForge code.`
   Then add this paragraph after it (the Yotthani and ButterLib note of `1325c36ea`, which WP118 leaves to this step; no RCA of that commit is on this line, so its path stays plain text):
   `On 2026-10-06, on the 1.5.x line, yotthani's `docs/engine/*` and `docs/perf-audit/*` at commit `2e6fe97` were read in a local clone. The ButterLib Distance Matrix stall (#740) was measured there on v1.5.3 and not re-measured on v1.4.8 (`docs/engine/perf.md`; [butter-lib-distance-matrix.md](../features/butter-lib-distance-matrix.md)). TAOM's fix is its own (it switches ButterLib's subsystem off by reflection, from ButterLib's MIT code), and yotthani's patch code in the `yotthani/bannerlord` repository was not read. No code was copied. The switch was reviewed in 1.5.x `docs/reviews/rca-butterlib-distance-matrix-2026-10-06.md` (not on this line).`
   If WP117's row `Yotthani MithrilForge and \`bannerlord\` repository (engine notes)` exists, append to its fifth cell
   `; plan 144 adds tpac-static-prop-authoring.md and the engine/ save-system, agent-spawn, lifecycle, submodule, gauntletui, campaign-tick and formations pages; the measurement cited in docs/features/butter-lib-distance-matrix.md`.
6. `tools/README.md`: add a row for `tests/test_prefab_asset_packages.py` (and for `tpac_clone_metamesh.py` if this step
   added the file) beside the other tpac tools, taking the wording of the matching `+` rows of `git show 991f415f4 -- tools/README.md`.
7. `docs/INDEX.md`: after the line added in A8 add
   `- [reference/tpac-static-prop-authoring.md](reference/tpac-static-prop-authoring.md): MithrilForge's recipe for a static prop in a tpac without the Kit, the package layout facts, and the camp-package gate`.

**Verify**: One tool test `OK` (5 tests); Tool tests: no new failure against `AUX/tooltests0.txt`;
`grep -c "2e6fe97" docs/reference/provenance-register.md` prints `1` (`0` only if Step A0 recorded `docs/features/butter-lib-distance-matrix.md` ABSENT);
`grep -c "why none of it renders\|E:\\\\LOTRAOM_Releases\|(../reviews/adopt-mithrilforge" docs/reference/tpac-static-prop-authoring.md` prints `0`;
Docs command clean. Commit B5 (the files this step touched). Subject `docs(mithrilforge): v2.0.28 - prop recipe, gate the camp tpacs`.
`Research: v1.4.8 map_icon_parts.tpac and meshes_shared_7 re-measured with tpac_clone_metamesh.parse; the four camp packages and their PlaceCenteredPrefab call sites`.
`Not-tested: whether the camps render on 1.4.8 (in-game check 1)`. `Ported-from: 991f415f41340823f63464c9d8692e5c7079bfc6`
(and `e1a940ac9c2fcaa6dd573b982409ad4e8cf17344` if this step added the tool file), and
`Ported-from: 1325c36ea8d14bcba9dc9d0a3f6f5087c63833a9` (its Yotthani provenance note only).

### Part B done criteria

- [ ] Build exit 0; Test 0 failed (docs and a Python test only, so the .NET total equals Part A's).
- [ ] One tool test `OK` with 5 tests; Tool tests no new failure.
- [ ] `grep -rn "returns the default set\|using default action set" docs` prints no line added since `base.txt`
      (`git diff "$(cat ../144-aux/base.txt)" -- docs | grep -i "^+.*default action set\|^+.*returns the default set"` prints nothing).
- [ ] Docs command: `ai_dashes: 0`; `dead_links`, `orphan_features` not above `AUX/lint0.txt`.
- [ ] `git status --porcelain` empty; Part B added 5 commits, each with `v2.0.28`.

---

## Part C: lessons, rules and the CLAUDE.md rows

### Step C1: lessons (`db8f7c10e`, `985b0d54e`, `b54179389`, `44058ce4b`, `42e48bbe5`, `b5e1d0b3d`, `4d79eca85`, `2b8f5ff36`, `2ac85fbe7`)

Append each entry at the end of its file, preceded by one blank line. Where this says "the `+` lesson", take the
lesson's `+` lines from `git show <sha> -- <file>` and replace its `- **Source:**` line with
`- **Source:** 1.5.x <the original source> (not on this line); plan 144.`

1. `docs/reviews/lessons/adapters-taleworlds-api.md`:
   - the `+` lesson `### Never drive a one-way engine state backward through a setter that allows it` (`db8f7c10e`),
     replacing its first sentence's `Fords tower regeneration raised` with `On the 1.5.x line, Fords of War tower regeneration raised`
     and adding after `(\`CalculateNextDestructionLevel\`)` the cite ` (v1.4.8 \`DestructableComponent.cs:399-412\`, the read at \`:498\`)`;
   - the `+` lesson `### The engine's gates ignore visibility, so read a scene's hidden gates before you trust a siege lane` (`985b0d54e`),
     replacing `Helm's Deep hides an inner gate at height 32.5.` with `On the 1.5.x line a Helm's Deep scene hid an inner gate at height 32.5.`,
     adding the v1.4.8 cites ` (taom-src \`TeamAISiegeComponent.cs:54-57\`, \`:184\`; \`CastleGate.cs:609-622\`; \`BehaviorAssaultWalls.cs:122\`)` after
     `so it never opens`, and appending the `+` Reach bullet from `b54179389` with `(a scan of the live \`TAOM_Map\` scene files, 2026-11-04)` replaced by
     `(the 1.5.x line's scan of the desktop's live \`TAOM_Map\`, 2026-11-04; not re-scanned on this laptop, in-game check 3)`;
   - this new lesson (`42e48bbe5`, adapted; K2 finding F10):
     ```markdown
     ### `SetDoNotMakeNewDecisions(true)` does not stop a party's own initiative everywhere (#870)
     v1.4.8 `MobilePartyAi.GetBehaviors` (from `:477`) applies the initiative result despite the flag when the best
     initiative target is in a map event at, or besieging, the party's `TargetSettlement` (`MobilePartyAi.cs:486`; it then needs a score above 1,
     `:488`). On the 1.5.x line a pinned Fire and Tribute host attacked a friend this way. On this line `SupplyCaravanService.PinAi`
     and `RefugeService` pin parties with the flag; a supply caravan whose destination is besieged can therefore take initiative
     despite its pin (UNVERIFIED in game; a refuge holds position, so its `TargetSettlement` is probably null).
     - **Why missed:** "makes no new decisions" was taken from the pin call without reading the branch that consumes the flag.
     - **Prevent:** before relying on `DoNotMakeNewDecisions` to silence a party, read `MobilePartyAi.GetBehaviors`; a rule about a
       pair of parties in a `MobilePartyAIModel` checks both directions.
     - **Source:** 1.5.x `docs/reviews/rca-fire-and-tribute-own-side-2026-10-27.md` finding 4 (not on this line); plan 144.
     ```
   - in the existing entry whose paragraph begins ``A process-wide cache keyed on `partyBase.GetHashCode()` ``, replace
     the sentence that begins ``Use `ConditionalWeakTable<TInstance, TBox>` `` (it holds the line's long dash) with the `+` text of
     `git show b5e1d0b3d -- docs/reviews/lessons/adapters-taleworlds-api.md` from ``Use `ConditionalWeakTable`` to its end (it adds the
     per-lookup lock cost), appending ` This line runs on the .NET Framework CLR (a 1.4.8 player log records \`CLR: 4.0.30319.42000\`).`
2. `docs/reviews/lessons/localization-ui.md`: the `+` lesson `### Gauntlet reads \`#RRGGBBAA\`, and \`DoNotAcceptEvents\` stops at a modal panel's root` (`44058ce4b`),
   with `(\`TaleWorlds.Library.Color.cs:172-183\`)` → `(v1.4.8 taom-src \`Color.cs:180-191\`)`, `Two prefabs wrote` → `On the 1.5.x line two prefabs wrote`,
   and ``EventManager.AnyWidgetsAt` then found`` → ``EventManager.AnyWidgetsAt` (v1.4.8 `EventManager.cs:857-877`, through `GauntletLayer.HitTest`, `GauntletLayer.cs:232`) then found``.
3. `docs/reviews/lessons/testing-qa.md`: the `+` lessons `### A fake that returns 0 hides its seam` (`44058ce4b`),
   `### A bench win on a save-path patch is not a game win` (`b5e1d0b3d`), and the two `+` lessons of `2ac85fbe7`
   (`### A single value from one session is not a finding` and `### Check that a test lever took effect before counting the run`).
4. `docs/reviews/lessons/harmony-il.md`: the `+` lesson `### A Harmony patch method never lives on a closed generic type or in a \`DynamicMethod\`` (`b5e1d0b3d`).
5. `docs/reviews/lessons/misc.md`: the `+` lesson `### A doc batch written beside the code is re-read against the reviewed code before it ships` (`4d79eca85`).
6. `docs/reviews/lessons/animation-skeleton.md`: the `+` bullet that begins `- **Guard the index first` (`2b8f5ff36`), inserted
   where that diff puts it (after the lesson's `Prevent` step it refers to); if that lesson is absent, skip it.

**Verify**: `grep -c "Color.cs:180-191" docs/reviews/lessons/localization-ui.md` prints `1`;
`grep -c "MobilePartyAi.cs:486" docs/reviews/lessons/adapters-taleworlds-api.md` prints `1`; every appended lesson has
`Why missed`, `Prevent` and `Source` lines; no appended line links `rca-long-defeat` or `rca-fords-of-war` files
(`git diff "$(cat ../144-aux/base.txt)" -- docs/reviews/lessons | grep -c "^+.*(\.\./rca-\(long-defeat\|fords\)"` prints `0`);
Docs command clean. Commit C1. Subject `docs(reviews): v2.0.28 - engine lessons re-cited for v1.4.8`.
`Not-tested: docs only`. `Ported-from:` the nine SHAs in this step's title, each in full.

### Step C2: a destructible's shown stage only moves forward (`c2c3a6f28`, `db8f7c10e`)

1. `docs/reference/engine/usable-machines.md`: at the end of the bullet list under `## TAOM relevance + gotchas`
   (before `## The native boundary`) insert the `+` bullet that begins ``- **`DestructableComponent` damage stages: the engine does not keep them forward (v1.5.6).**``
   from `git show db8f7c10e -- docs/reference/engine/usable-machines.md`, with `(v1.5.6)` → `(v1.4.8)`, adding the cites
   ``(`DestructableComponent.cs:87`, `:399-412`, `:489-498`; `CastleGate.cs:208`)`` after `` `CastleGate` uses`` + newline + two spaces + `it)`
   (at the end of that clause, before `returning`),
   and the last sentence `Found in Fords of War tower` + newline + two spaces + `regeneration ([RCA](../../reviews/rca-fords-of-war-2026-10-31.md), row R2).`
   → `Found on the 1.5.x line (Fords of War tower regeneration, 1.5.x RCA \`rca-fords-of-war-2026-10-31.md\` row R2); preventive here: no TAOM code on this line writes a destructible's \`HitPoint\`.`
2. `.claude/rules/adapters.md`: append at the end of the file:
   ```markdown

   ## A destructible's shown stage must only move forward (MANDATORY)

   Never raise a damaged `DestructableComponent`'s `HitPoint` (a heal, regeneration) unless the adapter has set
   `OnCalculateDestructionStateIndex` to keep the shown stage from moving backward: the engine does not, so a smaller heal
   steps the object back to an earlier model, and one that lifts `HitPoint` above the first line sets stage 0, where the
   engine reads `_destructionStates[-1]` after hiding the model and the object stays invisible and unhittable (v1.4.8
   `DestructableComponent.cs:399-412`, `:498`; [engine note](../../docs/reference/engine/usable-machines.md); found on the
   1.5.x line, 2026-10-31).
   ```

**Verify**: `grep -c "^## A destructible's shown stage must only move forward" .claude/rules/adapters.md` prints `1`;
`grep -c "reviews/rca-fords-of-war" docs/reference/engine/usable-machines.md` prints `0` (the 1.5.x RCA is named, not linked:
the replacement keeps it in backticks without a link); `git check-ignore -v .claude/rules/adapters.md` prints nothing;
Docs command clean. Commit C2 (two files). Subject `chore(rules): v2.0.28 - destructible damage stages move forward only`.
`Research:` the DestructableComponent row. `Not-tested: docs and rule only`. `Ported-from:`
`c2c3a6f285ba83814a45bd9aaf1c9c029c3fadd8`, `db8f7c10e7772612fbe20563b75b9e36c3d8e7b7`.

### Step C3: the CLAUDE.md trap rows (`4520af802`, `2a9e8bc49`, `b5e1d0b3d`, `ac1d8edbc`)

1. Record `wc -c CLAUDE.md` as BEFORE.
2. In the row that begins `| **Horse-skeleton reskins** |`, replace ``act_horse_strike_*` reads as being-struck.`` with
   ``act_horse_strike_*` are hit reactions.``.
3. Only if A0 found `VERBATIM` (WP141's skeleton doc), replace the whole row that begins
   `| **Animation must be authored on the ENGINE skeleton** |` with:
   ```markdown
   | **Animation must be authored on the ENGINE skeleton** | Kit facts: FBX bone locals stored VERBATIM, root turned 180, root motion zeroed at frame 0, bone tracks in FBX NODE order while the engine reads the skeleton's LIST order. So: engine frames, a hierarchy whose depth-first walk is that list, REST at frame 0, take = master name. `docs/reference/bannerlord-skeleton-authoring.md` |
   ```
   (if B4 sub-edit 9 was skipped, leave the row as it is).
4. Insert these two rows directly after the row that begins `| **Agent slots recycle; Agent.Tick is off-thread** |`
   (or, if WP143 reordered the table, as its last two rows):
   ```markdown
   | **An `AfterStart` throw reloads the mission forever** | v1.4.8 `FinishMissionLoading` clears `_missionInitializing` before `Mission.AfterStart` (`MissionState.cs:333-352`), so a throw makes every later tick reload the mission and SaveShield swallows it: an endless loading screen. Patch103 cuts the throwing call short. `docs/features/mission-start-guard.md` |
   | **Event listeners run newest first** | `MbEvent.AddNonSerializedListener` prepends (v1.4.8 `MbEvent.cs:24-30`), so a listener added after the engine's runs BEFORE it on the same event. `docs/reference/engine/campaign-tick-time-and-party-ai.md` |
   ```
5. Budget: `python -c "import sys;[print(len(l.rstrip(chr(10)))) for l in open('CLAUDE.md',encoding='utf-8') if l.startswith('| **An \`AfterStart\`') or l.startswith('| **Event listeners')]"`
   prints two numbers, each at most 400. Record AFTER = `wc -c CLAUDE.md`. If AFTER is above 46,000 while BEFORE was
   not, shorten the two new rows to these and re-measure:
   `| **An \`AfterStart\` throw reloads the mission forever** | v1.4.8 clears \`_missionInitializing\` before \`AfterStart\` (\`MissionState.cs:333-352\`), so each tick reloads the mission: an endless loading screen. Patch103 cuts the throwing call short. \`docs/features/mission-start-guard.md\` |`
   and `| **Event listeners run newest first** | \`MbEvent\` prepends (v1.4.8 \`MbEvent.cs:24-30\`): a listener added after the engine's runs before it. \`docs/reference/engine/campaign-tick-time-and-party-ai.md\` |`.
   If it is still above 46,000 and BEFORE was not, STOP and report both numbers (WP143 owns the trim; do not cut other rows).

**Verify**: `grep -c "reads as being-struck" CLAUDE.md` prints `0`; the A1 verify grep prints nothing; Docs command:
`claude_budget` not above `AUX/lint0.txt`, `ai_dashes: 0`. Commit C3 (`CLAUDE.md` only). Subject
`docs(harness): v2.0.28 - traps: AfterStart reload, listener order`. Body names the row changes made and BEFORE/AFTER
bytes. `Research:` the MissionState, MbEvent and IsInBeingStruckAction rows. `Not-tested: docs only`. `Ported-from:`
`4520af802d9c31f44263c7a9ce4a263e611d0c5d`, `b5e1d0b3d679d539f2752b7fb8a11bfef6d06203`,
`ac1d8edbc82f01af77812972d14b980259108bf3`, and `2a9e8bc497f911ae3f4635a7414b7fa072e4e7ee` only if sub-step 3 replaced the row.

### Step Z: final checks and report

Build, Test (0 failed; total equal to Part A's), Tool tests (no new failure), Docs command (`ai_dashes: 0`, nothing above
`AUX/lint0.txt`), `git status --porcelain` empty, `git log --oneline "$(cat ../144-aux/base.txt)"..HEAD` (16 commits if all
three Parts ran). Do not edit `plans/README.md`. In your hand-off, report status, every commit hash, the measured test
and lint counts, every skipped sub-edit, every fallback taken, BEFORE/AFTER `CLAUDE.md` bytes, and the A5 measurements,
so the integrator can record them.

### Part C done criteria

- [ ] C1 to C3 verify commands pass; `git grep -n "reads as being-struck" -- . ':!CHANGELOG.md' ':!docs/reviews/REVIEW-LOG.md' ':!docs/reviews/*.prompt.md' ':!plans'` prints nothing.
- [ ] `CLAUDE.md`: each new row at most 400 characters; total bytes not above 46,000 unless BEFORE already was.
- [ ] `git diff --name-only "$(cat ../144-aux/base.txt)"..HEAD` lists only in-scope files; no `CHANGELOG.md`.

---

## Disposition (every commit in the pack, plus the shared hunks other plans assigned here)

Simulation: `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`,
run this session. "Conflict" lists the main non-bookkeeping paths.

| Commit | Subject (1.5.x) | Group | Sim | Disposition |
|---|---|---|---|---|
| `578770e03` | docs: doctrine docs catch up; v1.5.3 named as current | docs-engine-formations-team-ai | conflict (formations doc, 1.5.x-only docs) | **Adapted** B3: Team paragraph and the 24-behaviour row. Skipped: culture-doctrine, battle-AI analysis, feature-map, the lesson edit (it amends a doctrine lesson absent here), v1.5.2 to v1.5.3 version text |
| `5dfa8f4cb` (shared, from WP117) | feat(combat): culture doctrines | mission-perf (WP117) | n/a | **Adapted** B3: the tactic-layer table, re-cited row by row; the "unsubscribed in vanilla" claim corrected |
| `2a9e8bc49` | feat(animation): ENGINE skeleton requirements | docs-kit-animation-authoring | conflict (CHANGELOG, CLAUDE.md) | **Adapted, conditional** C3: the row lands only if the base's skeleton doc carries `VERBATIM`. WP141's plan skips `3c5e57e71`, so the expected outcome is **skipped** (Kit facts not re-measured on the v1.4.8 Kit). CHANGELOG entries skipped (D1; 1.5.x content) |
| `4520af802` | feat(creatures): ram hits one foe, elephant hit capsules | engine-fact-isinbeingstruck-band | conflict (`tools/check_rdc_entries.py`, docs) | **Adapted** A1 (11 sites incl. a comment the 1.5.x commit fixed only in its redesign), C3, B4 sub-edit 9 (fact 4 and one troubleshooting row; conditional like `2a9e8bc49`, expected skipped). Skipped: SingleVictimPick, WarRam config and behaviour, elephant capsule data and docs, tools, clip-flags recipes and the handoff section (anchor absent; optional per pack), adoption record, RCAs |
| `aed9dea8d` | docs(herorace): #635 CTD triage | issue-635-herorace-ctd-triage | conflict (bookkeeping) | **Ported** A6 with the v1.4.8 citations re-anchored to the laptop dump; CHANGELOG and LESSONS-LEARNED counts skipped |
| `0934f65d2` | Add unit tests for eye follow and skin restoration | race-face-morph-docs | conflict (docs) | **Adapted** A8: race-face doc trimmed, #385 correction in arena.md and the snapshot README. Skipped: cultures.json `saruman`, eye tools, restore tool, skins snapshot, community guides, orientation.md (absent) |
| `991f415f4` | docs(mithrilforge): adopt prop recipe, gate the camp tpacs | adopt-mithrilforge | conflict (`tools/tpac_clone_metamesh.py`) | **Adapted** B5: doc (1.4.x section, v1.4.8 re-measures), gate test, tool or its lazy-import hunk, toolchain corollary, provenance. Skipped: clip-flags and UE-pipeline hunks (their anchors are 1.5.x sections), system-map (absent), field-camp and module-taom "render nowhere" claims (UNVERIFIED on 1.4.8; in-game check 1), adoption review and RCA |
| `e1a940ac9` (prereq) | feat(spider): Brown and Pale Spider mounts | issue-616 (WP141) | n/a | Only `tools/tpac_clone_metamesh.py` at its `991f415f4` state, and only if WP141 did not bring it (B5) |
| `8af881f2b` | docs: the mirror is Mike's to sync; elephant skeleton path | docs-armory-mirror-policy | conflict (absent files) | **Adapted** A7 (desktop check owed). Skipped: armory-audit skill, armory-ref-audit.md (absent), ranged-ladders step (no such step on 1.4.x) |
| `fe6619615` | docs(arena): vanilla tournament armour, v1.5.4 checks | docs-arena-tournament-armour | conflict (1.5.x-only docs) | **Adapted** A4 (override count stays five). Skipped: race-abilities.md, tournament-rewards.md, snapshot README v1.5.4 notes |
| `75b7b6661` | Refactor code structure and remove redundant sections | docs-helmet-hair-beard-cover | conflict (ledger, items-armor) | **Adapted** A5 (Equipment rule corrected) and A4 (its Patch69 comment, superseded by `2b6f6216c`'s wording). Skipped: beard-cover ledger, translation_cache (content) |
| `2b6f6216c` | docs(arena): Patch69 comment points to its registry entry | issue-407-patch69-comment | conflict (Patch69 file) | **Adapted** A4: comment without the Tournament Rewards clause, registry, two lessons. RCA skipped |
| `b5e1d0b3d` | docs(engine): fold in MithrilForge's newer engine findings | docs-engine-mithrilforge-findings | conflict (rules, docs) | **Adapted** B1, B2, B3, B4, C1, C3. Skipped: harmony-patches.md row (WP106 landed its v1.4.8 row), mission-frame-threads and mission-perf-heartbeat hunks (in WP117's `685c6a1e0` copies), clip-flags, system-map, scripted-melee, clip-inspector hunks (anchors absent), `CustomAttacksUtils.cs` comment (v1.5.x native addresses only), kitbash runes (content), `plans/_audit` file |
| `369eb0306` | docs(lords): lords in both files merge, not override | docs-lords-xml-merge | conflict (lord-skills-authoring) | **Adapted** A2 with the #644 lesson written for v1.4.8; the 1.5.x maintainer's lord-placement rule not carried |
| `9804f67b9` (lesson source) | feat(nazgul): the Nine become race nazghul | n/a | n/a | Only its #644 lesson's facts, re-cited (A2); the feature is content |
| `63630e8a0` | fix(refuge): the engine saves the AI pin | refuge-ai-pin-docs | clean | **Ported** A3 (`cherry-pick -n`) |
| `2b8f5ff36` | docs(animation): companion clips, agent-less figures, Kit | yotthani-adopt-2026-10-15-docs | conflict (absent docs) | **Adapted** B4 and C1. Skipped: armory-guide line (anchor absent), tpac-static-prop and system-map hunks, kitbash runes, Gondor audit, tools/README rows (tools absent) |
| `d690ff8d2` | docs(battle): measured battle AI and morale constants | yotthani-adopt-2026-10-15-docs | conflict (absent docs) | **Adapted** B3: morale table (re-cited), the switch bar (in the tactic table), the break cascade as a lead. Skipped: the measured tactic table and the Culture Doctrine paragraph (TAOM 2.0.34 battles on v1.5.5; doctrine is 1.5.x-only), four 1.5.x feature docs |
| `4d79eca85` | docs(reviews): adopt yotthani 2026-10-15, traps and lessons | yotthani-adopt-2026-10-15-docs | conflict | **Adapted** B1 and C1. Skipped: VanillaTuning coexistence (load-order doc and lesson), shader-compile-notice, external-repo-adoption, adoption records, provenance rows |
| `7eac30153` | docs(engine): 1.5.5 perf, native and coexistence findings | yotthani-adopt-2026-10-15-docs | conflict | **Adapted** B2 and B4. Skipped: VanillaTuning notes in six feature docs, the mission-frame-threads hunk (in WP117's copy), diplomacy figures, submodule-and-registration line, adoption record, RCA |
| `0bb1f3cae` | docs(engine): doc review fixes for the yotthani batch | yotthani-adopt-2026-10-15-docs | conflict | **Taken implicitly** where B1, B2, B4 and A6 extract from the `0bb1f3cae` file state (including the agent-less figures hedge, the rope-path clause and the row 9 registry path); hunks in absent or skipped files skipped, and so is its new `Restate an upstream fact` lesson in `misc.md` (its source record is on the 1.5.x line) |
| `8dd76ddb3` | docs(reviews): adopt yotthani 2026-10-16 | yotthani-adoption | conflict | **Adapted** B4 (the unattended-runs note). Skipped: fief-granting and settlement-economy sections (1.5.5 readings whose structure differs on v1.4.8: no `SiegeAssaultEventComponent`, `MapEvent` switches its own type at `MapEvent.cs:653`, `:888`; a v1.4.8 rewrite is new research), HoldChokePoint and teleport rows, RealmUI gauntlet bullets, map-view second-scene study, three harness lessons (WP142/WP143 scope), registry/provenance rows, records |
| `2ac85fbe7` | docs(yotthani): engine and campaign findings, Oct 29 | docs-yotthani-adoption-oct29 | conflict (`tools/lint_docs.py`, docs) | **Adapted** B2, B4, C1. Skipped: siege relief, probe facts and campaign band (1.5.5 readings, TAOM 2.0.34 measurements), the gauntlet typed-event and RealmUI bullets (v1.5.5/1.5.6 readings), formations retraction (its table is not taken), map-perf-profiler graphics runs, diplomacy, wanderer-allegiance, body-properties, creature and head-mesh docs, external-mod-couplings, race-face and tpac extensions, adoption record, upstream-watch.json |
| `ac1d8edbc` | docs(harness): two more trap lines in the orientation | docs-yotthani-adoption-oct29 | conflict (`tools/lint_docs.py`) | **Adapted** C3 (listener row only). Skipped: lint_docs budgets, ADR-011, orientation.md (1.5.x-only), the "Other mods" row |
| `db8f7c10e` | feat(fords-of-war): towers fall, unattended test runs | harness-destructible-stage-rule | conflict (Fords files) | **Adapted** C1, C2 (engine paragraph and lesson only). Everything else is Fords of War (OUT) |
| `c2c3a6f28` | chore(rules): destructible damage stages move forward only | harness-destructible-stage-rule | conflict | **Adapted** C2 (source line rewritten for 1.4.x) |
| `985b0d54e` | fix(long-defeat): waves end, and the unattended spike passes | feature-long-defeat | conflict (Long Defeat files) | **Adapted** C1: the gates lesson only; the "how a fight ends" lesson is Long Defeat-specific and skipped |
| `b54179389` | docs(reviews): which scenes hide an inner gate | feature-longdefeat | conflict | **Adapted** C1 (the Reach bullet, hedged; in-game check 3) |
| `44058ce4b` | docs(long-defeat): the review's lessons and last leftovers | feature-longdefeat | conflict | **Adapted** C1: colour and modal-root lesson, fake-returns-0 lesson. Skipped: the FordsOfWar/LongDefeat adapters lesson, FieldFacts.cs, SurvivalRunTests.cs |
| `42e48bbe5` (K2 F10) | fix(fire-and-tribute): own side leaves its raiding host be | (SKIP row) | conflict | **Adapted** C1: the `SetDoNotMakeNewDecisions` lesson with v1.4.8 lines |
| `ecaa67d26` (shared, from WP109) | feat(stability): mission start, freeze and map guards | mission-start-guard | n/a | **Adapted** B1: the lifecycle doc hunk WP109 deferred here |
| `09f3126a7` (hand-off from WP136) | fix(tools): exact mbproj ids, hook judges what commits record | n/a | n/a | Skipped here: the "native reads a row's type" modding docs need a v1.4.8 native check of `NATIVE_BY_TYPE`, and this plan does no new research. Deferred (Maintenance notes). The `audit_mbproj_registration.py` half is not here: it goes to WP136 |
| `1325c36ea` (hand-off from WP118) | perf(campaign): switch off ButterLib's Distance Matrix | issue-740-butterlib-distance-matrix (WP118) | n/a | **Adapted** B5: the Yotthani provenance note and row clause only, re-worded for this line (no "Fourth pass", since the earlier passes are absent here; "unlicensed" dropped, since WP117's row records the licence). Everything else is WP118's |
| `cd1fbdb6f` (named by WP117) | docs: backlinks footers regenerated on trunk | n/a | n/a | Skipped: generated footers, not ported text |
| `10e2efb49`, `8cbfa8333`, `a05500f61` | Long Defeat code and docs | feature-long-defeat | n/a | Skipped: SKIP-FEATURE (Long Defeat is a 1.5.x-only feature) |
| `758c8f6fc`, `3564d9782`, `8b7961dd7`, `0195d0fd7`, `eab59cbd4`, `744159870` | Long Defeat code and docs | feature-longdefeat | n/a | Skipped: SKIP-FEATURE |

New work (no 1.5.x commit): the toolchain `:197` host sentence and the INDEX line (A7); the B1 shader-precompilation
fix item (an issue for a later fix); the `GetAgentTroopClass_Override` and `Equipment.HairCoverType` corrections of
1.5.x text (B3, A5).

## Test plan

- **New test**: `tools/tests/test_prefab_asset_packages.py` (5 tests: packages exist, call-site table, every placed
  mesh packaged, no duplicate name or GUID, structural soundness). RED before `tpac_clone_metamesh.py` exists
  (`ModuleNotFoundError`), GREEN after. Planning pre-ran its logic on 1.4.x data (identical packages and constants on
  both lines): 4 call-site arguments, 4 metameshes, no duplicates, every package sound.
- **C# changes**: four comment-only edits (`WarRamConfig.cs`, `RefugePartyComponent.cs`, `RefugeService.cs`,
  `Patch69_TournamentRosterGuard.cs`) and one assertion message (`RefugeServiceTests.cs`). Build after each; filtered
  `RefugeServiceTests` and `TournamentRosterGuard`; full suite at the end of each Part. The .NET total does not change.
- **Docs**: the Docs command after each step (`ai_dashes: 0`, no new dead link or orphan); greps in each Verify.
- **Untestable here**: every in-game claim (see In-game checks owed); the native offsets the docs tag as v1.5.x.
- **Review**: the integrator runs `/deep-review` on the merged branch if wanted (C# here is comments and one message).

## Done criteria (whole plan)

- [ ] Part A, Part B and Part C done criteria all hold.
- [ ] `git log --format=%s "$(cat ../144-aux/base.txt)"..HEAD | grep -vc "v2.0.28 - "` prints `0`.
- [ ] `git diff --name-only "$(cat ../144-aux/base.txt)"..HEAD | grep -c "CHANGELOG.md\|Main/IoC.cs\|Main/SubModule.cs\|TAOM.csproj\|settings.json"` prints `0`.
- [ ] `git diff "$(cat ../144-aux/base.txt)"..HEAD | LC_ALL=C.UTF-8 grep -c "^+.*[—–]"` prints `0` (no em or en dash added anywhere; the `LC_ALL` is required, because without it this grep also matches arrows and box characters in the carried text).
- [ ] The hand-off carries status, commit hashes and measured counts; `git diff --name-only "$(cat ../144-aux/base.txt)"..HEAD | grep -c "plans/README.md"` prints `0`.

## STOP conditions

- A quoted anchor is missing and the step offers no fallback, or the live text around it contradicts this plan.
- `docs/features/mission-start-guard.md` absent (B1, C3), or `.claude/rules/harmony-patches.md` lacks `MBCallback(null, true)` (B3).
- A commit hook demands `CHANGELOG.md` (WP102 not merged) or rejects the `v2.0.28` subject.
- `git cherry-pick -n 63630e8a0` touches any file other than the four listed.
- `grep -n "new TaomTournamentModel" Main/SubModule.cs` prints 0 or 2+ lines.
- `TaomTournamentModel` has a number of overrides other than five (`grep -c "public override" Main/Features/Arena/Models/TaomTournamentModel.cs` prints other than `5`): the A4 text would be wrong.
- The B5 gate fails on 1.4.x data (it passed in planning), or the tool-test run shows a new failure.
- You find yourself writing an engine line not in the engine table, or carrying "returns the default set" for
  `MBGlobals.GetActionSet`.
- `CLAUDE.md` stays above 46,000 bytes after the short rows when it was not before C3.
- A build or test fails twice after a reasonable fix; another session is editing the worktree.

## In-game checks owed (for the maintainer)

1. **Camp prefabs (B5).** Start a Field camp on the 1.4.8 build; in `taom_debug_*.log` look for
   `[FieldCamp] placed 1 Field camp entities` (prefab) or a larger count (fallback). Same for a refuge (`[Refuge] placed N`).
   Record which in `tpac-static-prop-authoring.md`.
2. **Elephant skeleton package (A7).** On the desktop's loose Armory, confirm `adod_elephant_geo.tpac` carries
   `elephant_skeleton` and `elephant_harad_armor_01_geo.tpac` is a 416-byte stub (`ls -l`; a tpac dump for the skeleton).
3. **Hidden inner gates (C1).** One campaign siege at Helm's Deep (`taom_rohan_castle_helms_deep_forceatmo`) or
   `taom_gondor_castle_001_forceatmo`: does the middle (gate) lane ever open, or do attackers stand at the gate?
4. **Female dwarf meshes (A8).** For the Armory build a 1.4.x release pairs with, read `skins.xml`'s dwarf `woman`
   `face_meta_mesh`: `sm_dwarf_basemesh_a1_head` (stopgap) or `sk_dwarf_bm_f1_head` (restored). If restored, look at a
   female dwarf lord close up (Erebor keep) for the #385 crash and the eye gap.
5. **Map-view GPU memory (B4).** On the map, open and close the inventory 20 times; watch Task Manager's dedicated GPU
   memory with Release Map View Memory on and off.
6. **Being-struck band (A1).** None needed (engine code read); optional: a war ram head-butt shows no flinch.
7. **Tournament armour (A4).** In a tournament match fighters wear their culture's practice kit (TAOM's override); in an
   arena practice fight each fighter wears his own culture's kit too.

## Maintenance notes

- **WP143** trims `CLAUDE.md` and re-trims the always-on rules; if it merges after this branch, its integrator keeps
  C3's two rows (or moves them into the doc the trim chooses) and the `adapters.md` section from C2.
- **WP142** adds `merge=union` for `docs/reviews/lessons/*.md` (D18), and WP142 removes the counts (F1) from `LESSONS-LEARNED.md`, so this plan leaves that file alone; all lesson
  edits here are appends (one in-place line edit in `adapters-taleworlds-api.md` and one in `build-tooling-workflow.md`).
- **Deferred, with reasons recorded above**: a v1.4.8 rewrite of `8dd76ddb3`'s fief-granting and economy readings; the
  1.5.x clip-flags handoff sections; the later 1.5.x extensions of `race-face-and-hand-morphs.md` and
  `tpac-static-prop-authoring.md`; the 2ac85fbe7 gauntlet and siege-relief sections; deriving the v1.4.8 static face morph
  offset (WP137 brings `tools/native_decompile.py`); WP136's hand-off of `09f3126a7`'s "native reads a row's type" docs
  (wants the same native check; that commit's `audit_mbproj_registration.py` half goes to WP136, not here); the Kit-facts row and fact 4 if WP141 stays without `3c5e57e71` (a v1.4.8 Kit re-measure);
  WP121's `docs/modding/modules-overview.md` bin counts (the next re-measure; not read for this plan).
- **Open fix item**: `ShaderPrecompilePlayerAgentGuard`'s in-tick `FinishDeployment` (B1); the orchestrator files the issue.
- **Reviewer focus**: that every carried 1.5.x figure keeps its version tag; the A4 registry paragraph against the
  dump lines; the B3 table rows against taom-src; no link into a doc that is absent on this line.
