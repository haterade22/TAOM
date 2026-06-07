# TAOM Knowledge Index

**Start here.** This file is the topical map across [docs/](.) — read it instead of grepping when you need to find the canonical doc for any TAOM system. CLAUDE.md describes the working rules and architecture stance; this file maps the persistent knowledge.

## Quickstart paths

| If you are... | Go to |
|---|---|
| A fresh Claude session orienting itself | [CLAUDE.md](../CLAUDE.md) → this file → relevant topical section below |
| Looking for one feature's canonical docs | The "By major system" section below, or directly [docs/features/<name>.md](features/) |
| Debugging a regression | [reviews/](reviews/) (search for `rca-<feature>-<date>.md`), then auto-memory `feedback_<symptom>.md` files |
| Authoring a new feature module | [ai-includes/architecture.md](ai-includes/architecture.md) + [ADR-002](adrs/002-thin-entry-points.md) + [ADR-007](adrs/007-adapter-pattern.md), then pick the nearest existing feature doc as a template |
| Authoring a new culture's armor + troops end-to-end | [ai-includes/new-culture-authoring.md](ai-includes/new-culture-authoring.md) |
| Adding or fixing lord skills + traits (any culture) | [ai-includes/lord-skills-authoring.md](ai-includes/lord-skills-authoring.md) |
| Researching a TaleWorlds API before editing | [ai-includes/taleworlds-research-guide.md](ai-includes/taleworlds-research-guide.md) + `pwsh tools/taom-src.ps1 path <Type>` |
| Adding text the player will read | [localization/TRANSLATOR_GUIDE.md](localization/TRANSLATOR_GUIDE.md) + [features/localization.md](features/localization.md) |
| Closing out a feature (build → review → ship) | CLAUDE.md "Completion Workflow" + [reviews/REVIEW-GUIDE.md](reviews/REVIEW-GUIDE.md) |
| Checking what was scored on each Codex review | [reviews/REVIEW-LOG.md](reviews/REVIEW-LOG.md) |

## By major system

### Character, race, body, & character creation
- [character-creation](features/character-creation.md) — race-restricted CC dropdown, action_set requirements, narrative-stage flow, vanilla-aligned bonus budget (skill/attribute/focus per stage)
- [character-creation-body-properties](features/character-creation-body-properties.md) — per-culture default body properties on CC screen (Patch29)
- [character-selection](features/character-selection.md) — transpiler-driven race fallback in CC
- [race-age-system](features/race-age-system.md) — race-appropriate lifespans (elven immortality, dwarf/hobbit aging) via TaomAgeModel + TaomPregnancyModel
- [hero-race](features/hero-race.md) — race assignment + persistence on Hero
- [offspring-race-inheritance](features/offspring-race-inheritance.md) — child race from parent races, race-aware hero creation defaults
- [initial-child-generation](features/initial-child-generation.md) — campaign-start child rolls
- [no-mount-cultures](features/no-mount-cultures.md) — suppress narrative horse crash on no-mount cultures (Patch20)
- [native-skin-fixes](features/native-skin-fixes.md) — managed wrapper for `TAOM.NativeSkinFixes.dll` (covers_head morph + hair/beard cloth sim)
- [gui-sprite-system](features/gui-sprite-system.md) — sprite atlas conventions, verification before reference, the **decompile-verified sprite-bake pipeline** (no `pack0.tpac`; per-category `AssetSources` PNG + `Assets/_tex.tpac` + manifest) + end-to-end **Adding / Verifying a sprite** workflow (a new sprite needs the generator AND a render check — **baked ≠ visible**)

### Combat, AI, & battle
- [advanced-combat](features/advanced-combat.md) — SpatialGrid, BoneCollision, CustomAttacks subsystems
- [smart-cavalry-ai](features/smart-cavalry-ai.md) — player-team cavalry state machine (Form → Charge → PassThrough → Reform)
- [mixed-formations](features/mixed-formations.md) — heterogeneous formation layout system
- [companion-tactics](features/companion-tactics.md) — companion-driven formation overrides; `CancelStanceOnMove` postfix
- [warg-combat](features/warg-combat.md) — BT elements, WargAttackService, WargMissionBehavior
- [spider](features/spider.md) — spider creature combat (PAUSED: native render AV; fix = wolf's public SpawnMonster + un-split mesh)
- [elephant](features/elephant.md) — war-elephant trample + mount-lock (1-for-1 ADOD port) + [howdah-crew-mechanism](features/elephant/howdah-crew-mechanism.md) (UsableMachine crew platform; not yet ported)
- [adod-beasts-architecture-and-taom-port](reference/adod-beasts-architecture-and-taom-port.md) — **the whole ADOD_Beasts mod end-to-end** (lifecycle + the WHY) + line-by-line TAOM port comparison across all 4 subsystems; the 1.2.12→1.4.5 drift catalogue. Read this before re-decompiling ADOD.
- [troop-weight-system](features/troop-weight-system.md) — TroopWeightSettings, PartyBase / TroopRoster patches (Patch17)
- [battle-balance](features/battle-balance.md) — TaomMilitaryPowerModel, TaomCombatSimulationModel
- [battle-scenes](features/battle-scenes.md) — battle scene system (Patch0, currently DISABLED)
- [worldmap-battle-scene-grid](reference/worldmap-battle-scene-grid.md) — how field-battle terrain is chosen; the `worldmap_battle_scene_grid` texture is **baked into `Main_map`**, not loaded by filename; re-author + bake workflow
- [custom-battles](features/custom-battles.md) — TAOM factions/commanders/troops in custom battles (Patch19)

### Career & progression
- [career-system](features/career-system.md) — 50 careers × 16 cultures, XML-driven defs, ability + passive systems, career screen UI
- [career-cc-selection](features/career-cc-selection.md) — CC career-selection stage + archetype-driven starting equipment
- [troop-progression](features/troop-progression.md) — tier-by-tier upgrade rules, MaxCharacterTier 10
- [troop-tree-revamp](features/troop-tree-revamp.md) — multi-culture troop roster authoring discipline
- [volunteer-recruitment](features/volunteer-recruitment.md) — per-settlement / clan / culture recruitment pools (TaomVolunteerModel)
- [equip-presets](features/equip-presets.md) — save/load equipment preset overlay on inventory
- [lord-skills](features/lord-skills.md) — lore-driven SkillSets for every TAOM lord (~880 NPCs, 17 cultures, 35 archetypes); authoring guide [lord-skills-authoring.md](ai-includes/lord-skills-authoring.md)

### Equipment & armor authoring
- [gondor-armor-revamp](features/gondor-armor-revamp.md) — Gondor armor authoring + roster swap (issue #99)
- [gondor-ithilien-ranger](features/gondor-ithilien-ranger.md) — Ithil Guard conditional + Ranger line
- [multi-culture-armor-revamp](features/multi-culture-armor-revamp.md) — Mordor/Isengard/Dol Guldur/Erebor/Rhun armor pass (issue #211)
- [weapon-xml-pipeline](features/weapon-xml-pipeline.md) — weapon XML generation + rebalancing
- [dale](features/dale.md) — Dale culture authoring (armor, troops, Lake-Town recruitment override) — proof-of-life for full-culture authoring
- [tournament-armor-assignment](features/tournament-armor-assignment.md) — per-participant culture armor in TaomTournamentModel
- See also: CLAUDE.md "Equipment & Armory" for canonical-folder table per item-ID prefix

### Sieges
- [siege](features/siege.md) — vanilla siege overrides
- [siege-defense](features/siege-defense.md) — watched-faction siege defense events with CampaignTime deadline
- [siege-trebuchets](features/siege-trebuchets.md) — TaomSiegeEventModel: defender Trebuchet option
- [siege-dismount](features/siege-dismount.md) — player dismount on siege entry; modifier-preserving horse storage

### Economy, settlements, resources
- [special-resources](features/special-resources.md) — 11 resources × 18 kingdoms, troop costs, save-compat (Patch26)
- [culture-marketplace](features/culture-marketplace.md) — daily LOTRLOME item injection by owner culture
- [settlement-guards](features/settlement-guards.md) — per-settlement guard pools, clan/culture fallback (Patch28)
- [settlement-nameplate-fade](features/settlement-nameplate-fade.md) — distance-based nameplate fade (Patch38)
- [revolt-tuning](features/revolt-tuning.md) — JSON-tunable revolt soft-nerf, TaomSettlementLoyaltyModel
- [startup-resources](features/startup-resources.md) — per-culture player startup gold/items
- [cultural-feats](features/cultural-feats.md) — 16 culture-feat GameModel overrides (Patch18)

### Faction, kingdom, & diplomacy
- [diplomacy](features/diplomacy.md) — TaomDiplomacyModel for LOTR faction relationships
- [kingdom-creation](features/kingdom-creation.md) — TAOM kingdom + clan + lord authoring
- [faction-map](features/faction-map.md) — campaign map faction rendering
- [minor-factions](features/minor-factions.md) — minor factions catalog + rules
- [alignment-aware-execution](features/alignment-aware-execution.md) — race/alignment-aware execution penalties
- [execution](features/execution.md) — TaomExecutionRelationModel + Patch14
- [banner-injection](features/banner-injection.md) — player banner persistence
- [banner-color-persistence](features/banner-color-persistence.md) — clan colors everywhere (Patch23 + Patch24)
- [named-companions](features/named-companions.md) — 18 lore companions as recruitable wanderers
- [war-of-the-ring](features/war-of-the-ring.md) — endgame WotR phase
- [diplomacy](features/diplomacy.md), [army-targeting](features/army-targeting.md) — see also TaomTargetScoreModel + Patch22 (border proximity floor)

### Sandbox, lifecycle, & UI
- [main-menu-customizer](features/main-menu-customizer.md) — hide Campaign, rename Sandbox → "Enter The Age Of Men"
- [encyclopedia](features/encyclopedia.md) — encyclopedia screen extensions, dispatch entry points
- [quick-actions](features/quick-actions.md) — inventory "Sell All" multi-action menu (Patch34)
- [fief-management](features/fief-management.md) — custom GameState for fief management
- [arena](features/arena.md) — TaomTournamentModel with culture armor + prize pools
- [messengers](features/messengers.md) — paid messenger dispatch + travel arrival inquiry
- [shader-precompilation](features/shader-precompilation.md) — pre-compile shaders menu option (Patch21)
- [time-acceleration](features/time-acceleration.md) — campaign time scale knobs
- [atmosphere-persistence](features/atmosphere-persistence.md) — forced-atmosphere scenes (Patch16)
- [weather-bounds-guard](features/weather-bounds-guard.md) — weather bounds clamp (Patch10)
- [localization](features/localization.md) — 12 languages × 3 modules, AI-translated via tools
- [localization-override](features/localization-override.md) — per-language curated overrides
- [army-targeting](features/army-targeting.md) — besieger commitment stickiness, priority lists, border floor

### Infrastructure & tooling
- [bannerlord-engine-and-toolchain](reference/bannerlord-engine-and-toolchain.md) — **the whole engine/toolchain**: shipping-vs-editor builds, managed-vs-native DLL split, verified tech stack (Mono, PhysX, Granite, DX11, DLSS), the managed↔native bridge, FBX→tpac pipeline, custom-creature workflow. `tools/decompile_bannerlord.ps1` (dual-build decompile) + `tools/pe_inspect.py` (see into native DLLs)
- **`reference/engine/` — phased engine study** (one process traced end-to-end from the decompile, what/how/why):
  - [agent-spawn-and-render-pipeline](reference/engine/agent-spawn-and-render-pipeline.md) (Phase 1 — `SpawnAgent`/`SpawnMonster`→`CreateAgent`→`BuildAgent`→native `PreloadForRendering`; pins the spider render AV to the mesh, not the spawn code)
  - [animation-binding-and-playback](reference/engine/animation-binding-and-playback.md) (Phase 2 — `Monster.FillAnimationSystemData`→`CreateAgent` binds the rig; `SetActionChannel`→native plays it; the "won't animate" diagnostic chain)
  - [monster-model](reference/engine/monster-model.md) (Phase 3 — the `monsters.xml` schema: bone-index map by name, IK/capsules/flags/usage, base-monster inheritance; what TAOM authors per creature)
  - [mission-and-missionbehavior-lifecycle](reference/engine/mission-and-missionbehavior-lifecycle.md) (Phase 4 — the in-battle runtime: `MissionBehavior`/`MissionLogic` virtuals, `AddMissionBehavior` dispatch, lifecycle order; confirms the `: MissionLogic` gotcha)
  - [object-system-mbobjectmanager](reference/engine/object-system-mbobjectmanager.md) (Phase 5 — `MBObjectManager` `RegisterType`/`LoadXML`/`GetObject<T>(stringId)`; the data backbone; null-on-missing = the underwear bug; load-order-tolerant cross-module merge)
  - [save-system](reference/engine/save-system.md) (Phase 6 — `SyncData` vs `SaveableTypeDefiner`+`[SaveableField]`; the base+localId collision gotcha; TAOM composite-string idiom)
  - [gamemodel-system](reference/engine/gamemodel-system.md) (Phase 7 — `GetModel<T>` last-added-wins; `AddModel` inheritance vs decorator; register-after-defaults + one-override-per-type; TAOM's ~40 overrides)
  - [scene-gameentity-scriptcomponent](reference/engine/scene-gameentity-scriptcomponent.md) (Phase 8 — `GameEntity`/`ScriptComponentBehavior`/prefab; engine-discovery by class name; `[EditableScriptComponentVariable]` = config-must-validate)
  - [campaignevents-and-campaignbehavior](reference/engine/campaignevents-and-campaignbehavior.md) (Phase 9 — `CampaignBehaviorBase` `RegisterEvents`/`SyncData`; `CampaignEvents`/`IMbEvent` subscription; no-RemoveListener + no-OnGameEnd gotchas)
  - [item-equipment-model](reference/engine/item-equipment-model.md) (Phase 10 — `ItemObject`/`ItemComponent`/`HorseComponent.Monster`; `EquipmentIndex` slots (Horse=ArmorItemEndSlot=10); `EquipmentElement`=Item+Modifier → the modifier-preserving-overload rule)
  - [gauntletui-viewmodel-screen](reference/engine/gauntletui-viewmodel-screen.md) (Phase 11 — `ViewModel`↔movie via `GauntletLayer.LoadMovie`; `GameState`/`GameStateScreen`/`IGameStateListener`; UIExtenderEx mixins; the UI gotcha cluster)
  - More phases added sequentially.
- [bannerlord-animation-clip-flags](reference/bannerlord-animation-clip-flags.md) — the `AnimFlags` clip-flag system + per-clip-type recipe + full per-flag reference (all ~60); flags are baked into the `_anm.tpac`, NOT `action_types.xml`; the spider's clips ship with zero flags (= broken locomotion)
- [editor-cache-rebuild](features/editor-cache-rebuild.md) — parallel + incremental + resumable settlement distance cache rebuild
- [scene-scripts](features/scene-scripts.md) — engine-discovered ScriptComponentBehavior subclasses (CS_Road, etc.)
- [crash-report](features/crash-report.md) — crash report enrichment
- [mission-diagnostic](features/mission-diagnostic.md) — first-tick MissionBehavior dump + action-set capture for mod-conflict diagnostics
- [bannerlord-together-compat](features/bannerlord-together-compat.md) — multiplayer mod compat surface

## Architecture Decision Records (canonical project rules)

See [adrs/README.md](adrs/README.md) for the full table. **Mandatory** ADRs that gate code review:

- [ADR-002 Thin Entry Points](adrs/002-thin-entry-points.md) — entry points <150 lines, delegate to services
- [ADR-007 Adapter Pattern](adrs/007-adapter-pattern.md) — services use `IHeroAdapter` etc., NEVER `Hero` directly
- [ADR-008 Testability Requirements](adrs/008-testability-requirements.md) — business logic 100% unit-testable

Other standards: [ADR-001 XML config](adrs/001-xml-config.md), [ADR-003 No `#region`](adrs/003-no-regions.md), [ADR-004 No `[Obsolete]`](adrs/004-no-obsolete.md), [ADR-005 No `#if DEBUG`](adrs/005-no-preprocessor-directives.md), [ADR-009 Self-documenting code](adrs/009-self-documenting-code.md), [ADR-010 Knowledge-base architecture](adrs/010-knowledge-base-architecture.md).

## Review & RCA archive

- [reviews/REVIEW-GUIDE.md](reviews/REVIEW-GUIDE.md) — Codex adversarial review process (prompts, dispatch contract, scoring)
- [reviews/REVIEW-LOG.md](reviews/REVIEW-LOG.md) — running scorecard of every Codex review (35+ reviews, 96 bugs found)
- [reviews/REVIEW-PLAN.md](reviews/REVIEW-PLAN.md) — multi-feature review planning
- Per-feature RCAs live as `reviews/rca-<feature>-<date>.md` — read these BEFORE re-implementing related behaviour. Filename convention is grep-friendly.
- Auto-detected reviews: `reviews/codex-adversarial-<feature>-<date>.md` (prompt + output pairs).
- Balance/data audits (machine-generated, distinct from RCAs): `reviews/<topic>-audit-<date>.md`. Current: [reviews/cc-bonus-audit-2026-05-30.md](reviews/cc-bonus-audit-2026-05-30.md) — per-culture character-creation skill/attribute/focus totals vs vanilla budget; regenerate with `tools/audit_cc_bonuses.py`.

## Migration history (v1.2 → v1.3 → v1.4.5)

- [migration/TRACKING.md](migration/TRACKING.md) — top-level migration audit trail
- [migration/v1.4.x-overview.md](migration/v1.4.x-overview.md) — current target migration plan
- [migration/api-diff-1.3.15-to-1.4.5.md](migration/api-diff-1.3.15-to-1.4.5.md) — API delta table
- [migration/XML-SCHEMA-CHANGES.md](migration/XML-SCHEMA-CHANGES.md) — XML schema changes between versions
- [migration/dr3-maintenance.md](migration/dr3-maintenance.md) — BUTR/MCM/ButterLib dependency pinning, smoke test, risk scenarios
- [migration/dual-dll-setup.md](migration/dual-dll-setup.md) — dual-DLL setup for cross-version testing
- [migration/dr3-mcm-internalization-plan.md](migration/dr3-mcm-internalization-plan.md), [migration/dr3-execution-handoff.md](migration/dr3-execution-handoff.md) — MCM/dependency internalization
- [migration/ROT-CORE-ANALYSIS.md](migration/ROT-CORE-ANALYSIS.md) — ToR_Core comparison

## AI / process guidance

- [ai-includes/architecture.md](ai-includes/architecture.md) — layer stack, IoC, lifetimes
- [ai-includes/patterns.md](ai-includes/patterns.md) — Hook / Strategy / GameModel patterns
- [ai-includes/agent-teams.md](ai-includes/agent-teams.md) — when to spawn parallel agents
- [ai-includes/tdd-enforcement.md](ai-includes/tdd-enforcement.md) — TDD workflow rules
- [ai-includes/testing-guide.md](ai-includes/testing-guide.md) — MSTest + NSubstitute patterns
- [ai-includes/research-workflow.md](ai-includes/research-workflow.md) — `taom-src` + decompilation workflow
- [ai-includes/taleworlds-research-guide.md](ai-includes/taleworlds-research-guide.md) — decompiled-source navigation
- [ai-includes/decompiled-code-analysis.md](ai-includes/decompiled-code-analysis.md) — reading decompiled output
- [ai-includes/iterative-problem-solving.md](ai-includes/iterative-problem-solving.md) — multi-pass refinement
- [ai-includes/multi-approach-validation.md](ai-includes/multi-approach-validation.md) — comparing alternative implementations
- [ai-includes/code-quality.md](ai-includes/code-quality.md) — non-negotiable C# quality rules
- [ai-includes/security.md](ai-includes/security.md) — input validation, boundary trust
- [ai-includes/new-culture-authoring.md](ai-includes/new-culture-authoring.md) — repeatable end-to-end culture authoring (Dale + Gondor)

## Cross-cutting concerns (where memory ↔ docs intersect)

These are recurring lessons the auto-memory system has captured. The memory files live outside the repo at `~/.claude/projects/<project-slug>/memory/` (auto-loaded on session start via `MEMORY.md`). When a memory entry says *"see CLAUDE.md X"* or *"see docs/features/Y"*, the doc here is the canonical truth; memory is the *lesson learned about how to use it*.

- **Equipment authoring discipline** — CLAUDE.md "Equipment & Armory" (canonical-folder table) + memory: `feedback_lotrlome_armor_cover_attributes`, `feedback_multi_folder_id_uniqueness`, `feedback_verify_troop_ids_against_canonical_xml`
- **Native C++ port discipline** — CLAUDE.md "Native C++ port discipline" + [features/native-skin-fixes](features/native-skin-fixes.md) + memory: `feedback_native_port_hot_path_audit`, `feedback_seh_filter_specificity`, `feedback_degraded_state_distinct_banner`
- **TaleWorlds API research** — [ai-includes/taleworlds-research-guide.md](ai-includes/taleworlds-research-guide.md) + memory: `feedback_taleworlds_vm_setter_decompile`, `feedback_codex_caught_api_misread`, `feedback_decompile_vanilla_setter_before_deserialize_mutate`, `feedback_imbevent_remove_one_unavailable`, `feedback_campaignbehavior_no_ongameend`, `feedback_movementorder_cctor_mission_current`
- **TAOM_Map live-vs-shadow trap** — CLAUDE.md "TAOM_Map settlements" + memory: `feedback_taom_map_live_vs_stale_shadow`, [reference/taom-map-settlement-naming.md](reference/taom-map-settlement-naming.md)
- **Adversarial-review discipline** — [reviews/REVIEW-GUIDE.md](reviews/REVIEW-GUIDE.md) + memory: `feedback_root_cause_mandatory`, `feedback_completion_workflow`, `feedback_dont_defer_high_review_findings`, `feedback_review_blindspots`
- **Validation at system boundaries** — memory: `feedback_editor_fields_are_config`, `feedback_clamp_nan_infinity_propagates`, `feedback_validate_before_lookup_with_fallback`

## Glossary / ID cheatsheet

- **Kingdom/culture ID mapping** — battania = Khand, empire = Dunland. Full table in memory `kingdom-culture-mapping.md`. *Do not* assume vanilla ID names map to vanilla LOTR factions.
- **Race system** — memory `races-system.md` (monsters.xml, skins.xml, action_sets.xml structure)
- **Banner ruling-clan convention** — `clan_<kingdom>_1` inherits kingdom banner_key; don't override in `spclans.xslt`. Memory: `banner-ruling-clan-convention.md`
- **Lord archetypes** — memory `lords-system.md` (914 lords × 12 archetypes × 13 cultures)

## Research notes

LLM-compiled wiki nodes derived from `docs/raw/`. See [research/README.md](research/README.md) for the structure and `/knowledge-compile` workflow.

- [karpathy-autoresearch](research/karpathy-autoresearch.md) — full review of Karpathy's autoresearch repo (10 files, 52 patterns extracted) + Tier-1/2/3 adoption map for TAOM. Source: `docs/raw/ai-research/karpathy-autoresearch/`.
- [reference/external-resources.md](reference/external-resources.md) — verified external resources to improve TAOM (official-vs-community Bannerlord docs hierarchy, BUTR deps, Harmony, LOTR/Tolkien lore + naming generators, comparable total-conversion mods, save-compat versioning). Cite this instead of re-searching the web.

## Conventions

- File links use markdown `[text](relative/path.md)` syntax. No Obsidian `[[wikilinks]]`. See [ADR-010](adrs/010-knowledge-base-architecture.md) for rationale.
- Auto-generated "Referenced by:" footers will appear at the bottom of feature docs once Phase 3 of the knowledge-base buildout ships. They are bot-edited only — do not hand-author them.
- Source materials (papers, web clippings, decompiled notes) go in `docs/raw/` (Phase 4, not yet present). Compiled wiki nodes derived from raw go in `docs/research/`.
- This file is hand-curated. When a new feature doc is added, also add it here, in the right topical section. The doc-health linter (Phase 2, not yet present) will flag orphan feature docs that aren't referenced from INDEX, CLAUDE.md, or another doc.

---

*Knowledge-base architecture: see [ADR-010](adrs/010-knowledge-base-architecture.md). Plan: `.claude/plans/karpathy-constantly-posts-tips-wondrous-willow.md` (local, not in repo).*

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/010-knowledge-base-architecture.md](adrs/010-knowledge-base-architecture.md)

<!-- backlinks-end -->
