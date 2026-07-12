# CHANGELOG — TAOM (Tales From the Age of Men)

> **Archive:** entries before 2026-07-01 live in [`docs/changelog-archive/CHANGELOG-2026-H1.md`](docs/changelog-archive/CHANGELOG-2026-H1.md) (rolled 2026-07-12; cadence: each Jan 1 / Jul 1 — keep the current half-year here, roll the rest).

## 2026-07-12

### fix(map): town_LN1 extra siege-ram slot — CTD joining any siege at Rivendell town

- Player crash bundle `4d003ae6` (TAOM v2.0.10, Bannerlord 1.4.6): `IndexOutOfRangeException` in vanilla
  `SettlementVisualManager.TickSiegeMachineCircles` every campaign-map frame while participating in a siege
  at town_LN1. Root cause: the live TAOM_Map `Main_map/scene.xscene` gave town_LN1 **two** `map_siege_ram`
  + two `map_siege_tower` slot entities = 4 attacker melee frames, while the engine hardcodes
  `SiegeEvent.SiegeEnginesContainer.DeployedMeleeSiegeEngines` to length 3 (1.4.6 + 1.4.7 verified) — the
  tower loop indexes `[ramFrames + towerIdx]` → `[3]` → IOORE → CTD. Three unguarded consumer paths share
  the frames (circle tick, engine-visual tick, `MapSiegeVM` deploy UI); all TAOM C# exonerated (zero
  patches on the crash stack).
- Fix (external TAOM_Map module, not in repo): removed the duplicate ram entity (in-editor) → town_LN1 is
  now 1 ram + 2 towers like the other 220 fortifications. Full-map audit: 221 fortifications, ram census
  221, tower census 442, **zero** engine-cap violations or shape deviations remaining.
- Cosmetic cleanup in the same pass: 16 fortifications carried wrong `map_defensive_engine_*` suffixes
  (15 with all four slots tagged `_3`, castle_ES1 `_0,_2,_3,_3` — suffixes only feed the slot sort order,
  counts were all exactly 4, no crash potential). 5 fixed in-editor; the remaining 11 (town_E2/E3/E4,
  town_RU1/RU3–RU8, castle_ES1) retagged `_0.._3` by scripted byte-surgical digit swap preserving the
  current effective sort order (file length unchanged). Defender tag census now uniform: 221 × each of
  `_0`–`_3`. Backups: `scene.xscene.bak-20260712-{siege-ram-fix,suffix-fix}`.
- Existing saves are safe (scene entities aren't serialized; no save can reference a 4th melee slot —
  the campaign array was always length 3). In-game smoke owed: join a siege at Rivendell town, ≥30 s on
  the map with the siege overlay active, confirm 1 ram + 2 tower circles and no CTD.

### docs(tests): test-mirror gap assessment — 3 of 4 flagged gaps are non-gaps (repo-reorg Track D)

- The reorg audit flagged 4 `Main/Features/` dirs without `TAOM.Tests/Features/` mirrors. Assessed:
  **ElephantLike** — covered by proxy (`ElephantAttackServiceTests` + `MumakilAttackServiceTests` exercise the
  shared `ElephantLikeAttackService` through both bindings; a mirror dir would duplicate). **BattleScenes** —
  3 thin Harmony hooks on a DISABLED feature (entry points: "test via game" per ADR-008). **CharacterSelection** —
  one shipped transpiler (`Late_Transpiler`), same entry-point category. **MissionDiagnostic** — the one real
  item: `MissionDiagnosticService` (173 lines) has no tests; the two snapshot methods read live engine state
  (boundary, not unit-testable) but `LogActionSetSeen`/`ResetForNewMission` dedup logic is testable — deferred
  as test debt (writing C# was out of the reorg's scope; pick up with the next MissionDiagnostic change).

### refactor(agents-md): rolling essay log — 25 per-review essays archived, catalog kept (repo-reorg Track D)

- AGENTS.md's "Lessons From Prior Reviews" held 25 verbatim per-review essays (~53 KB) PLUS the ~90-pattern
  distilled catalog (bugs-Codex-misses / false-positives / what-Codex-does-well) that already harvested their
  lessons. The essays moved verbatim to `docs/reviews/agents-md-review-lessons-archive.md` (complete history,
  newest first); AGENTS.md keeps the newest 5 essays + the FULL catalog (the operative reviewer calibration)
  + intentional-patterns + harness-review notes. **New rolling convention:** each review cycle adds its essay
  at the top, rotates the 6th-oldest to the archive, and harvests durable patterns into the catalog +
  `docs/reviews/lessons/`. AGENTS.md 149.7 KB → 110.8 KB (−26% of Codex's per-review context).

### chore(harness): Track D follow-ups — fresh context baseline, scan fix, hook recency window, description trims (repo-reorg)

- **`docs/context-budget-baseline.md` re-baselined** (April's was 8x stale): eager startup excl. MCP =
  ~26K tokens (CLAUDE.md 14.2K + always-load rules 8.9K + skill/agent descriptions + MEMORY.md).
  `scan.sh` fixed to split always-load vs paths-gated rules (pre-fix it counted all 22 rules eager,
  +18K phantom). The 59.4K MCP figure is CONDITIONAL — this session observed schemas DEFERRED behind
  ToolSearch (eager ~0); the baseline documents the levers (unauthenticated github/imagine, ilspy vs
  taom-src overlap) as user decisions if another session type proves eager.
- **`check-deep-review.sh`** mute-grep now recency-scoped (last 8h, awk timestamp filter, fail-open to
  the old whole-file grep) — months-old audit entries had permanently silenced the reminder; stale
  "Bannerlord 1.3" in the message fixed.
- **Skill descriptions**: `lint-cleanup-loop` 42→22 words, `taom-src` 31→23 (the ≤30 cap).

### refactor(claude-md): secondary slims + budget gate flipped to ENFORCE (repo-reorg Tracks C7+C8, decomposition complete)

- **Section slims:** Skills table -> routing-only one-liners (descriptions already load eagerly from SKILL.md
  frontmatter — the fat table double-charged); Native C++ port discipline -> new paths-scoped rule
  `.claude/rules/native-cpp-ports.md`; inline-hook-activation -> pointer to harness-facts; GitHub/KB
  templates + the 13-step completion sequence -> `docs/ai-includes/completion-workflow.md` (verbatim,
  CLAUDE.md keeps the mandates); decompile folder-layout table + wEditor warning -> merged into
  `docs/reference/bannerlord-engine-and-toolchain.md`; Localization prose -> 4 bullets + guide link;
  Doc Lookup / Skill Routing / Scoped Rules / Hooks / Equipment over-cap rows trimmed. Fixed live
  version-drift while in there (the taom-src paragraph still said "installed v1.4.6" / "v1.4.5 dump" —
  now version-agnostic via the pin).
- **Scoped Rules table:** `hook-authoring.md` + `native-cpp-ports.md` registered (follow-up commit).
- **Config-row consolidation:** 16 per-feature config rows (SettlementFood config, CaravanTrade config,
  CareerSystem sprites, ...) dropped after verifying each path is documented in its feature doc's
  Configuration section; one umbrella row remains.
- **Budget gate ON (calibrated):** `CLAUDE_MD_BUDGET_ENFORCE = True` — `--fail-on-drift` (the pre-commit
  hook) now hard-blocks budget violations. **Cap recalibrated 60 KB -> 100 KB hard / 95 KB warn:** the
  plan's 60 KB estimate predated recovering 17 missing Harmony categories and assumed fewer/shorter
  index rows; the decomposition's honest floor at one-line density with all 85 Key Paths + 65 Harmony +
  40 GameModel rows kept is ~91 KB. Gate proven end-to-end (pass at 91 KB -> forced-fail at a 50 KB cap ->
  pass restored). **Net: CLAUDE.md 174 KB -> 91 KB (-48%, ~15-20K tokens per session + per agent spawn),
  zero verified information loss, regrowth hard-gated.**

### refactor(claude-md): Key Paths verify-merge — 34 essay rows → one-liners + doc links (repo-reorg Track C5)

- The Key Paths table carried a 200–3,400-char compressed restatement of each feature's doc (52 KB of the
  file). A 28-agent verify-merge pass processed the 34 over-cap rows: each agent diffed its row's claims
  against the destination doc, **appended anything missing to the doc first** (e.g. QuickActions' thread-static
  vanilla-bypass + `TransactionCount` mechanics, NavalTravel's navmesh-stays-enabled rationale, Messengers'
  `MapCoord` ADR-007 row, SettlementFood's siege-gating rationale), then produced the ≤400-char thin row.
  Load-bearing flags stay in-row: PARKED/DISABLED + re-enable pointers (NavalTravel, NativeSkinFixes),
  the TAOM_Map LIVE-vs-stale-shadow warning, the vendored-DLL allowlist (`/improve` depends on both).
- **`docs/features/mcm.md` authored** (the doc-gap hook's standing flag): Patch41 on UIExtenderEx
  `WidgetFactoryManager.CreateAndRegister` flips MCM's 5 embedded prefabs to top-to-bottom layout;
  grounded in `f23434b0`/#252. INDEX.md gains mcm + the previously-unlinked save-load-diagnostics rows.
- **Harmony registry completeness:** a code sweep found 15 patch categories in `Main/**`
  `[HarmonyPatchCategory]` attributes that the old CLAUDE.md table never listed (Patch13 RaceAge,
  Patch25 LocalizationOverride, Patch30 MixedFormations, Patch33 EquipPresets, Patch35 CompanionTactics,
  Patch36 FiefManagement, Patch37 CrashReport, Patch39 BanditPartySize, Patch41 McmLayoutFix,
  Patch43 BattleLoadDiagnostics, Patch51 RecruitmentResourceGate, Patch55 BasicTableauRaceGuard,
  3× Patch61 reflection sub-categories, + `Late_*` ×2) — documented in the registry from the actual
  patch files; the CLAUDE.md/AGENTS.md thin tables gain their rows. (Reverse check: Patch28 + Patch31
  legitimately carry no category attribute — manual patches.)
- CLAUDE.md 140.6 KB → ~107 KB; budget findings 51 → 17.

### refactor(rules): harness-facts.md split — durable facts stay always-load, authoring lore goes scoped (repo-reorg Track C6)

- `harness-facts.md` (23.5 KB, always-load) held durable harness facts AND authoring-time conventions +
  incident write-ups. Split: hook-authoring conventions (sibling-mirroring table, git-invocation-forms +
  two-stage matcher, amend exemptions, + a new log-rotation convention) → new **paths-scoped rule
  `.claude/rules/hook-authoring.md`** (loads only when a `.claude/hooks/` file is open); the parallel-port
  build-watcher saga, CombatMechanics builder-brief seam findings, and worktree evidence/invocation detail →
  `docs/ai-includes/agent-teams.md` "Case studies" (verbatim). Distilled rules (worktree isolation
  when-to-apply, builder-briefs checklist, watcher prevention) stay always-load with pointers.
  harness-facts.md 23.5 KB → 14.6 KB (−9 KB eager per session); "Last verified" bumped to 2026-07-12.

### refactor(claude-md): GameModel Overrides rows capped at one line (repo-reorg Track C4)

- All 40 GameModel rows stay (the table is the routing map for "which model owns X"), but the 5 rows
  that had grown into 400–1,100-char essays (`TaomPartySizeModel`, `TaomPartyNavigationModel`,
  `TaomMarriageModel`, `TaomSettlementEconomyModel`, `TaomCombatMechanicsModel`) are now one-liners +
  feature-doc links — each evicted claim verified present in the linked doc before thinning (grep-checked:
  limit-deflation mechanism, PARKED re-enable steps, wraith marriage overrides, `GetTownGoldChange`
  scope + engine-bump note, CTB/cleave/knockdown mechanics). Budget findings 56 → 51.

### refactor(claude-md): Harmony table → docs/reference/harmony-patch-registry.md, thin routing residue (repo-reorg Track C3)

- The 25.6 KB Harmony Patch Categories table (48 categories; the fat rows were 400–3,000-char essays) moved
  **verbatim** into `docs/reference/harmony-patch-registry.md` (one `## PatchNN` section per category, with
  Target + full rationale/history/RCA links). CLAUDE.md keeps a 4-column thin routing table
  (Category | Feature | exact Target signature | Status) — stack-trace→owner routing stays eager;
  PARKED/DISABLED flags stay load-bearing in-row. CLAUDE.md 160.8 KB → 142.8 KB; budget findings 75 → 56.
- Corrections while in there: `Patch15_BannerLayerLimit` now shows **DISABLED (engine-native since
  v1.4.7)** — the old table predated the bump; `AGENTS.md`'s Harmony snapshot (stale: ended at Patch22,
  wrong Patch17 target) replaced with the current thin table + registry pointer (registry = single
  maintained source).
- `.claude/rules/harmony-patches.md` (paths-scoped: loads when editing hooks) gains the read-the-registry
  step, the **`Patch_MissionTime_SetMovementOrder` mandate** (any `MovementOrder`-signature postfix must
  join the deferred category — `MovementOrder.cctor` reads `Mission.Current`), apply-timing guidance, and
  its stale "v1.4.5"/"Patch0 through Patch6" lines fixed. `submodule-lifecycle-and-harmony.md` citation +
  INDEX.md row updated.

### refactor(claude-md): Rebalancing Tools table → tools/README.md union-merge (repo-reorg Track C2)

- The 13.4 KB / 42-row Rebalancing Tools table duplicated (and in 23 cases was the ONLY home of) per-tool
  documentation. **Union-merged into `tools/README.md`:** 23 missing tools added (armor authors #211,
  troop revamps #212 + polish #224 as a new "Troop revamps" section, `extract_perks`/`analyze_lord_balance`/
  `analyze_troop_balance`, starter-armor pair, `raise_party_template_maxes`, `validate_gondor_refs`,
  `rollback_erebor_iron_misfile`); overlapping rows enriched with the CLAUDE.md-only gotchas
  (**battania=khand** CULTURE_MAP, clean-tree-regen pre-flight + unsafe per-culture re-resolution,
  `detect_culture` elite-line routing + militia-L21-by-design, iron_hills canonical folder, elf-lord
  tier-cap math, engine-ignores-inline-skills). Verified: all 42 table tools now resolve in README.
- CLAUDE.md residue: a 3-line "Rebalancing & Data Tools" pointer (catalog + preferred validators +
  analyze-before-apply rule). CLAUDE.md 173.8 KB → 160.8 KB; budget findings 88 → 75.
  `author-armor` skill repointed to the README sections.

### feat(lint-docs): CLAUDE.md eager-load budget check — warn-only until the decomposition lands (repo-reorg Track C1)

- CLAUDE.md loads into every session + agent spawn; it hit 174 KB (~30K tokens, 8× its April baseline)
  because feature prose kept accreting into table rows. `check_claude_md_budget()` in `tools/lint_docs.py`
  now enforces: **≤60,000 B file** (55 KB warn), **≤400-char table rows**, **≤600-char prose lines**
  (fenced blocks exempt). New `budget` report section + `claude_budget:` summary line; wired into
  `--fail-on-drift` behind `CLAUDE_MD_BUDGET_ENFORCE` (False = warn-only during the Track C migration,
  flipped at C8). `check-doc-config-drift.sh`'s detail-extraction + deny message cover the new section.
- Current reading: 88 findings (1 size + 87 over-cap rows/lines) — the migration's progress meter.
- Preamble trimmed to the pin + doc pointers (the baseline-dump history it carried is in
  `docs/migration/v1.4.7-impact.md`); the `Target: Bannerlord 1.4.7` line stays verbatim-parseable
  for `check_version_consistency` (version_mismatch still 0).

### refactor(docs): split LESSONS-LEARNED.md into per-category files under docs/reviews/lessons/ (repo-reorg Track B)

- The master lessons record had reached 371 KB / 206 lessons in one file — the review skills' "read the
  relevant category first" step meant loading (or section-hunting) the whole thing. **All 206 lessons moved
  VERBATIM** (script-verified: per-category `###` counts sum to the source's 206) into 13 files at
  `docs/reviews/lessons/<category>.md` (6–54 KB each); `LESSONS-LEARNED.md` stays at its path as a thin
  index (3.4 KB — intro, house shape, linked ToC with per-category counts) so every historical
  "LESSONS-LEARNED 'Category'" prose citation still resolves.
- Each category file carries an append-here header with the house shape (`### rule` → Why missed →
  Prevent → Source). Read/append instruction sites updated: `/deep-review` (Phase 3e), `/review-codex`
  (Phase 3e), CLAUDE.md Doc Lookup row, harness MEMORY.md.

### chore(tools): segregate finished one-offs into tools/oneoff/ (repo-reorg Track B)

- 33 scripts referenced by **no living doc** (checked `tools/`-prefixed AND bare-name mentions across
  CLAUDE.md, AGENTS.md, `.claude/`, docs/ai-includes+features+migration+reference, INDEX, tools/README,
  .github; plus cross-import + subprocess-invocation scans over `tools/*.py` + `tools/tests/`) moved via
  `git mv` to `tools/oneoff/` — per-culture clan/lord authors, v1.4.x migration fixes, kitbash test
  builders, dao-rock scene one-offs. Zero dangling references confirmed post-move; lint unchanged (11).
- 14 candidates **kept** in `tools/` on evidence: bare-name README/rule/doc references (`audit_item_refs`,
  `repair_sav_strings.ps1`, the faction-map trio — also pending the unmerged `impl-005` edit to
  `process_faction_map.py`), plus living-but-undocumented `analyze_reviews.py` + `spider_render_triage.py`,
  which gained README rows ("Review analytics").
- **Convention (new):** one-off scripts land in `tools/oneoff/` when their job is done — documented in
  `tools/README.md` § One-offs + a CLAUDE.md Key Paths row.

### chore(changelog): roll 2026 H1 entries to docs/changelog-archive/ (repo-reorg Track B)

- Root `CHANGELOG.md` had grown to 1.49 MB / ~11.9K lines (112 date sections since 2026-01-24) —
  grep noise + a heavy read for any session that opens it. Entries 2026-01-24 → 2026-06-30
  (~10.7K lines) moved verbatim to `docs/changelog-archive/CHANGELOG-2026-H1.md`; root keeps
  July+ (~1.3K lines) with an archive pointer under the header.
- Hook compatibility verified: `check-changelog-updated.sh` (substring on diff names) and
  `check-changelog-changed.sh` (exact-match on root path) both key on the root file, which stays;
  `session-start.sh` prints only the first date section. **Roll cadence: each Jan 1 / Jul 1.**

### chore(hooks): size-capped rotation for the two unbounded .claude/logs writers (repo-reorg Track B)

- `session-stop.sh` rotates `session-log.md` at 1 MB → `.1` generation (was 2.8 MB, unbounded since
  March); `log-agent.sh` rotates `agent-audit.log` at 256 KB → `.1` (was 270 KB). One previous
  generation kept; both verified live (real oversized logs rolled on first trigger).
- Side benefit: `check-deep-review.sh` greps `agent-audit.log` for deep-review evidence — with months
  of unrotated history the reminder was permanently satisfied; rotation restores a recent window
  (session-scoped filtering remains an optional follow-up).
- `/context-save` Storage notes now tell the saver to prune >30-day snapshots whose work has landed
  (the 18 stale 2026-05-13 phase snapshots this session deleted were the motivating case).

### chore(reviews): retention policy — raw Codex transcripts move to gitignored docs/reviews/raw/ (repo-reorg Track B)

- **Problem.** `docs/reviews/` had grown to 43 MB / 265 git-tracked files, ~36 MB of it raw Codex stdout
  transcripts (2–4 MB each) accumulating ~100 files/month with no retention scheme — repo bloat + grep noise
  on every review-history search.
- **One-time sweep.** 73 raw outputs (`codex-adversarial-*` non-prompt + `codex-prereview/selfreview/result-*`)
  untracked (`git rm --cached`) and moved to `docs/reviews/raw/` (new, gitignored). Files stay on disk;
  history keeps the old blobs (only ~1.5% of the pack — rewrite pointless). Deleted `_issue_body_tmp.md`.
- **Kept committed** (the durable record): all 71 prompts (`*.prompt.md` + legacy `codex-prompt-*`), all
  `rca-*.md`, `LESSONS-LEARNED.md`, `REVIEW-LOG/GUIDE`, adopt/audit docs. 14 RCA/REVIEW-LOG links repointed
  to `raw/…` (resolve on-disk; dead in a fresh clone — accepted, the distillate is the record).
- **Future flow.** `/review-codex`, `/codex-verify`, `/deep-review` now dispatch `codex exec` output to
  `docs/reviews/raw/` (`mkdir -p` guard for fresh clones); prompts still commit. Retention section added to
  `REVIEW-GUIDE.md`.

### chore(repo): remove tracked root scratch + relocate legacy scripts/ (repo-reorg Track B)

- **Removed from tracking** (regenerable or one-off artifacts committed by accident): `SPOrderOfBattleVM.tmp.cs`
  (scratch decompile), `mordor-lords.html` (one-off lords viz), `out/0Harmony.decompiled.cs` (497 KB stale
  decompile — `/taom-src` regenerates on demand; `out/` was already gitignored), `report.json` (empty `[]`;
  regenerates via `tools/validate_moduledata.py --json report.json`, now gitignored as `/report.json`).
- **Moved** the 11 legacy Jan–Mar lords-migration one-offs `scripts/` → `tools/oneoff/lords-migration/`
  (one-off scripts now live under `tools/oneoff/`); repointed the 5 references in
  `docs/migration/{SESSION-S5a-S5b-PROMPT,TRACKING,v1.4.x-equipment-overhaul,v1.4.x-taom-impact}.md`.
- `lint_docs.py`: 0 new dead links (13 pre-existing, unrelated); drift checks clean.
- Part of the approved 2026-07-11 repo-reorg plan (Track B item 1 of 6).

## 2026-07-11

### balance(mordor): make Black Uruks rarer in recruitment and lord parties

- **Recruitment.** Dropped the `mordor_uruk_grunt` (Black Uruk Grunt) weight **3 → 1** in the Mordor
  town pool + culture fallback (`VolunteerRecruitmentService.Mordor.cs`), taking the recruitable Black
  Uruk from **20% → ~7.7%** of a town's volunteers. Castles were already 0% (unchanged). The
  "Morannon more plentiful than Black Uruks" invariant still holds (5 vs 1).
- **Lord parties.** Set every `mordor_uruk_*` stack to `min_value="0" max_value="8"` (from `50`) across
  the 16 Mordor lord templates — the generic `kingdom_hero_party_mordor_template` + the 15
  `kingdom_hero_party_mordor_empire_south_1…15_template` (71 stacks). The engine's seed-fill weights
  each troop by `min + (max−min)×ratio` with every other Mordor stack at `max=50`, so **`max_value` is
  the proportion lever, not `min_value`** — dropping uruk `max` 50→8 cuts the Black Uruk share of a
  freshly-spawned Mordor lord army from ~32% to ~7%; orcs/wargs/Morannon become the bulk.
- **Scope.** Mercenary / outlaw / patrol / rebel Mordor templates left as-is (per user). No troop ids
  changed → save-clean. Recruitment DataRow tests re-baselined to the new 13-total pool; full suite
  green (4198), `validate_moduledata.py` PASS.

### fix(shader-precompilation): 1.4.7 deployment-NRE — precompile stuck on 1.4.7 (#336)

- **Symptom.** On Bannerlord 1.4.7 the main-menu **Pre-compile Shaders** walk got stuck indefinitely;
  worked on 1.4.6. A user debugger caught `NullReferenceException` at
  `DeploymentMissionController.SetupTeams():173`, thrown every mission tick.
- **Root cause — a 1.4.7 engine regression, not TAOM code.** 1.4.7 added an **unconditional** deref of
  `Mission.InitialPlayerAgent` to `DeploymentMissionController.SetupTeams()`/`FinishDeployment()` (the new
  `AgentControllerType` hand-control). That field is set only when an agent builds with `Controller ==
  Player` (`Mission.cs:4024`); the precompile custom battle is **headless** (no human), so it stays null
  and the deref NREs. 1.4.6 had no such deref. Managed shader APIs are byte-identical across the bump.
  Scoped to precompile — every real battle has a player agent, so normal play is unaffected.
- **Fix.** `ShaderPrecompilePlayerAgentGuard` (`MissionLogic`, added only during a walk via
  `SubModule.OnMissionBehaviorInitialize` gated on `ShaderPrecompileRunner.IsWalkInProgress`): seeds
  `InitialPlayerAgent` on the first agent build (before the deref) + force-finishes the OoB deployment so
  the headless battle doesn't freeze waiting for a *Deploy* click. Reflection write of the private field is
  drift-guarded by `ReflectionSiteBindingTests`.
- **Robustness package** (bounds any future stall regardless of cause): per-item-kind decider caps (a scene
  pass bails at 8 min instead of the 90 min the character battle needs), a **churn backstop** (a count that
  changes every frame but never returns to 0 now aborts — the old frozen-count guard missed it),
  self-classifying abort logs (`AbsoluteTimeout`/`FrozenCount`/`ChurnTimeout`), and a **Ctrl+Shift+K**
  in-game cancel.
- **Verified.** In-game 1.4.7: `WALK COMPLETE — 13 items in 8m 6s`, 0 NRE, 0 hang; the seed fired on all
  12 deployment missions. 7 new/updated unit tests; full suite green. Deep-reviewed (5 agents: 0 functional
  defects). RCA `docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md`.
- **Known caveat.** The successful run completed the character battle in 20s on a warm shader cache, so the
  force-finish path for that item wasn't exercised (its `InitialPlayerAgent` was non-null and it settled
  before deployment mattered); a cold-cache run would additionally validate it. #336 stays OPEN for that.

### fix(animation): give every race the full civilian action-set family (elves shared one town idle)

- **Symptom.** Elf NPCs in every town all played the *same* idle animation. **Root cause:** a settlement
  NPC's idle-role animation comes from a GENERATED action-set name `as_<race>[_female]_<suffix>`
  (`villager`/`lord`/`beggar`/`guard`/carry-prop/`map`…, via `ActionSetCode.GenerateActionSetNameWithSuffix`);
  when `as_<race>_<suffix>` is absent the engine silently falls back to ONE default set. Elves shipped with
  ONLY `as_elf_facegen`/`_female_facegen` (Character-Creation), so all 82 civilian roles collapsed to one idle.
- **Wider audit (per user request — "check each race").** The gap wasn't elf-only: **every** non-human race
  was also missing the same 3 prop-carry sets (`villager_carry_bucket_on_lefthand`, `villager_carry_fish_buckets`,
  `worker_carry_wood_on_shoulder`).
- **Fix — data, in LOTRLOME_Armory `action_sets.xml` (live) + the tracked snapshot.** 194 thin `base_set`
  aliases in a `TAOM-CIVILIAN-COVERAGE` block: elf + sauron get the full 82 each, the other 10 non-human races
  their 3 missing carry sets. Human-skeleton races (elf/sauron/orc/uruk/…) alias to `as_human_<suffix>` (correct
  role animation, shared skeleton); dwarf (own skeleton) aliases to `as_dwarf_villager` (never a human clip on
  the dwarf rig, the 1.4.6 water-CTD class). No C# change — the existing
  `ActionSetCode_GenerateActionSetNameWithSuffix_Patch` already emits `as_elf_villager`; the fix makes it resolve.
- **Tooling (new).** `tools/audit_civilian_action_set_coverage.py` (read-only per-race coverage vs human, exits
  non-zero on a gap) + `tools/generate_race_civilian_action_sets.py` (idempotent alias generator, `.bak`, dry-run
  default). Re-run both after every engine bump / LOTRLOME update (added to the snapshot-README discipline).
- **Verified:** both files parse; coverage audit 43/43 + 39/39 for all 13 settlement races;
  `audit_action_set_parity.py` 0 humanoid gaps; generator byte-idempotent (re-apply → identical hash). Trolls
  excluded by design (never townsfolk). **In-game verification owed** (elf-CC RCA rule: XML animation fixes must
  be confirmed live) — visit towns with elf/orc/dwarf populations, confirm varied male + female idles.
- **Reviewed (3-agent adversarial pass).** Completeness agent decompiled every engine caller of
  `GetActionSetWithSuffix` (townsfolk/notable/hero-spawn/disguise/carry-item helpers) and confirmed the 43+39
  reference == the full set of generated civilian suffixes — no role falls back. Regression agent cleared
  facegen / active-patch / save / duplicate-id interactions and disproved the T-pose risk (civilian sets DO
  inherit `base_set`, unlike the facegen path). Tooling agent found only latent re-run hazards (own-skeleton
  race lacking its own villager → self-alias; no dangling-abort; fragile skeleton detection) — all fixed
  in-session: explicit `OWN_SKELETON_RACES`, dangling → refuse-to-write, empty-native + non-UTF-8 guards,
  multi-block dedup, self-reference skip.

### fix(caravan-trade): stop caravans leaving a town and immediately returning

- **Two root causes, both confirmed against the decompiled v1.4.7 engine.** (1) The shipped
  "anti-shuttle penalty" was **inert**: it keyed on `caravanParty.LastVisitedSettlement`, which is set
  only on settlement *enter* (`MobileParty.cs:602`) and never cleared, and the caravan re-decides its
  destination *while still parked* — so at decision time `LastVisitedSettlement == CurrentSettlement`,
  the town vanilla already excludes from candidates (`CaravansCampaignBehavior.cs:923`). The penalty
  never fired on a selectable town. (2) The home town was **exempt from the distance re-weight**, so it
  kept vanilla's full `1/days` near-field spike + growing `num5` gravity while every neighbor was
  compressed — a caravan homed at a hub (e.g. Minas Tirith) re-selected home the moment it parked at any
  neighbor, reading as "leaves and immediately returns."
- **Fix.** New per-caravan **visit memory** (`ICaravanVisitMemory` + thin `CaravanVisitMemoryBehavior`
  on `SettlementEntered`/`MobilePartyDestroyed`, no `SyncData`) records the last 4 towns each caravan
  entered and yields a recency penalty that deprioritizes just-visited towns — targeting genuinely
  *selectable* towns, unlike the old `LastVisitedSettlement` check. The penalty is a strictly-positive
  multiplicative floor (never a hard exclusion → no stranding in sparse regions), routed *into*
  `ReweightTradeScore` so the `IsActiveFor` player-scope gate governs it. The home town is now
  distance-compressed like any other (`homeDistanceReweight`, default on), which loses its proximity
  edge while preserving vanilla's upstream `num5` home-gravity — caravans still return home on the
  payout cadence. **Verified safe:** `DefaultClanFinanceModel.AddIncomeFromParty` pays the owner
  regardless of caravan location, so home-compression cannot starve caravan income.
- **Config.** Repurposed the (previously inert) `antiShuttlePenalty` knob as the recency-penalty
  strength (default `0.35 → 0.5`); added `homeDistanceReweight` (default `true`) as a JSON escape hatch
  to restore the old home exemption if playtest shows home visits are too rare. Both JSON-only.
- **Known residual:** the recency memory enlarges the loop to ~5 distinct towns rather than guaranteeing
  map-wide circulation; tunable via `antiShuttlePenalty`. In-game playtest owed (home-return frequency is
  the one thing unit tests can't settle).

Research: `CaravansCampaignBehavior` (`FindNextDestinationForCaravan`/`GetTradeScoreForTown`/`num5`), `MobileParty.LastVisitedSettlement`, `DefaultClanFinanceModel.AddIncomeFromParty`, `CampaignEvents.{SettlementEntered,MobilePartyDestroyed}` (installed v1.4.7).
Save-compat: no new SyncData — ephemeral memory, rebuilds as caravans move; master-off = exact vanilla.
Not-tested: the Harmony postfix + behavior invocation (requires a live campaign) — the pure memory + reweight services are unit-tested (80 CaravanTrade tests green).

### refactor(troop-weight): move the "elite tax" from the member count to the party-size limit — raw counts everywhere

- **Player-facing fix.** Troop counts were confusingly inflated: a party could show **325** on the party
  screen / **407** "Land Troop Capacity" while only **159** fought in battle and showed on the map. That
  gap was the TroopWeight feature weighting the member COUNT (`NumberOfAllMembers`) so heavy troops cost
  more party-size budget — the weighted number leaked into every count display.
- **The rework.** The weighting now lives on the party-size **limit** instead of the count. `TaomPartySizeModel`
  subtracts the weight surplus (`ceil(weighted) − raw`) from the limit via
  `ITroopWeightService.ApplyPartySizeWeightPenalty` (pure, unit-tested `ComputeSizePenalty`, clamped so the
  limit stays ≥ 1). Result: **every count reads raw everywhere** (map nameplate, party screen, land-capacity,
  tooltips, menus, battle all agree), while the recruit cap still fills at the troop weight. The displayed
  *limit* honestly shrinks as you stack elites (`150 / 240` instead of `150 / 300`) — no invisible recruit wall.
- **Removed (~26 files):** the two `NumberOfAllMembers`/`NumberOfRegularMembers` getter patches + hooks, the
  5 weighted-display hooks (phantom-wounded fix — now moot since nothing is weighted in display), the
  `WeightedCountCache`, and the temporary `[CountFlicker]` diagnostic (its job — proving the map "200↔20"
  flicker is the vanilla army-sum, not the weighting — is done). Shed-on-upgrade stays, adapted to the
  deflated-limit frame.
- **Blast-radius handling.** Unpatching a global getter changes every consumer of the weighted count:
  `SpecialResources` battle-reward scaling is **preserved** (switched to an explicit weighted-count call);
  `SettlementFood`'s garrison-leak correction self-neutralizes to zero (vanilla food now reads raw at
  source — net food unchanged); incidental side-effects on other engine consumers (e.g. elite parties
  moving slightly slower) are intentionally gone — the feature now affects only the size cap.
- New player-facing string `{=taom_troop_weight_size}Heavy troops` (party-size tooltip label) — renders via
  its inline default; **`/localize` owed** to propagate to the 11 AI-translated languages.
- Behavior changes flagged for review: shrinking displayed limit; slightly different intermediate
  party-fill ratio (recruit cap unchanged); elite-party incidental effects removed.
- **Deep-review (5 agents) fixes**, all from the cross-system data-flow trace: (1) the shed hook recovered
  a lossy `deflated + surplus` base that overshot when the penalty clamped — it now reads the exact
  pre-penalty base via `GetTrueBaseSizeLimit` (cached), so it no longer under-trims heavy post-upgrade
  parties; (2) SpecialResources battle-reward scaling re-gated on `EnableTroopWeight` (weighted on / raw
  off) — the removed getter patch used to gate it, so "off = vanilla" was briefly broken; (3)
  `TroopWeightXmlLoader` now rejects `weight="NaN"`/`"Infinity"` via `FiniteFloatValidator`; (4) stale
  "Patch17-weighted" comments in SettlementFood + TroopShedPlanning corrected. RCA:
  `docs/reviews/rca-troopweight-count-to-limit-2026-07-11.md`.

Research: `MobileParty.PartySizeRatio`/`PartyBase.PartySizeLimit`/`GetPartyMemberSizeLimit` (installed v1.4.7).
Not-tested: the GameModel invocation + shed Harmony postfix (require a live campaign) — the penalty clamp math + NaN-loader rejection are unit-tested; full suite green (4199).

### fix(troop-weight): reference-key the count caches + add a map-nameplate flicker diagnostic (superseded same day)

- **Confirmed defect fixed.** Both count-getter hooks (`PartyBaseNumberOfAllMembersHook`,
  `PartyBaseNumberOfRegularMembersHook`) cached their weighted result in a process-global
  `Dictionary<int,…>` keyed by `partyBase.GetHashCode()`. `object.GetHashCode()` isn't unique per
  instance, so two parties that collided AND shared a `MemberRoster.VersionNo` read each other's
  weighted count — the latent hazard flagged in `rca-troopweight-phantom-wounded-2026-06-07.md` §2
  and never back-ported from the display path. Replaced with a shared, reference-keyed
  `WeightedCountCache<PartyBase>` (`ConditionalWeakTable`): identity keying (no collisions),
  GC-eviction (no unbounded growth), internal synchronization (the old `Dictionary` was unlocked).
  RED→GREEN test (`WeightedCountCacheTests`) reproduces the cross-party contamination against the old
  hashcode key and proves the fix.
- **Flicker diagnostic (TEMPORARY).** Investigating the "bandit/AI-lord party count shows 200 then 20
  then back" report, an engine trace showed the campaign-map nameplate reads RAW
  `NumberOfHealthyMembers` (via `SandBoxUIHelper.GetPartyHealthyCount`) — untouched by the weighted
  getters — so the visible flicker is NOT the weighting. Added `SandBoxUIHelper_GetPartyHealthyCount_Patch`
  (Postfix, `Patch17_TroopWeight` category): on a large-ratio count swing it logs one `[CountFlicker]`
  line classifying the mechanism (army-sum toggle / cache poison / raw-roster change) so the next repro
  self-identifies. Sample-gated (per-party cap), try/catch'd, remove once root-caused.
- **Doc:** corrected `troop-weight-system.md` Performance section (the count cache is now a
  `ConditionalWeakTable`; the previously-documented "trims 25% at 2000 entries" never existed).

Research: `SandBox.ViewModelCollection.SandBoxUIHelper.GetPartyHealthyCount`, `PartyBase.NumberOf*` getters (installed v1.4.7).
Not-tested: the Harmony postfix invocation (requires a live campaign) — the pure detector/formatter are unit-tested.

## 2026-07-10

### chore: relocate repo to `E:\repos\TAOM`

- Moved the working copy from `C:\Users\mikew\source\repos\TAOM` to `E:\repos\TAOM`.
- Repointed the runtime configs that embedded the old absolute path: `.mcp.json` (serena
  `--project`, filesystem root, taom-moduledata server), `.codex/config.toml` (filesystem root).
- Future-proofed 7 hooks that hardcoded `cd "c:/Users/mikew/source/repos/TAOM"` — now
  `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"` (matching the newer hooks) so a future move needs no edits:
  `session-start`, `session-stop`, `pre-compact`, `post-compact`, `detect-docs-gaps`,
  `check-build-before-commit`, `log-agent`.
- Build stays relocation-clean (`Directory.Build.props` resolves the game via `BANNERLORD_GAME_DIR`).
  The Claude Code memory dir was moved alongside (slug `c--…` → `e--repos-TAOM`).

### feat(career-system): all 49 career ability icons + compact battle HUD (#101)

- **Icons:** every enabled career now has a 256x256 ability icon in a unified "named effect-icon"
  style — the ability's effect/emblem as a gritty painterly oil painting with the ability name
  hand-lettered across the bottom (Poisoned Blades = venom-slick crossed scimitars, Soul Drain =
  souls spiraling into a shadow hand, Warcry of Eorl = the sounding horn + white-horse banner, …).
  Art user-generated in Midjourney from per-ability prompts (faction palette + grounded-LOTR VFX
  policy: no wild-fantasy glow; overt effects only for the Dol Guldur sorcery set); downscaled
  Lanczos to 256 and baked into the `ui_taom_career_system` atlas (49/49 rects pixel-verified,
  manifest + atlas + `_tex.tpac` chain regenerated in order, install↔repo synced byte-identical).
- **Battle HUD** (`GUI/PreFabs/CareerSystem/AbilityHUD.xml`): panel 220x132 → 130x166, icon
  64 → 110, career-name line and black backdrop removed — the icon, "Press V" ready text, and the
  charge bar now float directly on the battle view. The VM's `AbilityName` property is now unbound
  (dead binding, candidate for later cleanup).
- **Rename:** `cave_troll_master` ability "Troll Frenzy" → "Gundabad Berserker"
  (`taom_career_strings.xml`, `taom_career_choices.xml`, the disabled template block — 16
  occurrences). The 12 `Languages/*/std_taom_career_strings_*.xml` files still carry the old
  translated name for those 8 string ids until the next `/localize` run.
- **Docs:** `career-system.md` (icon how-to rewritten: bake required, `sprite=` attr is dead,
  house style recorded) + `gui-sprite-system.md` (Sprites-Needed row closed; two empirical bake
  lessons: a repo→install deploy can silently clobber a fresh CLI bake — always
  `sync_sprite_bake.ps1` immediately; an editor pass can rebuild only the tpac without re-packing —
  mtime-check the manifest/atlas/tpac trio).

Not-tested: career-screen render of the new icons (battle HUD render verified in-game via
screenshot; the career screen resolves the identical sprite id).

### fix(dependencies): tournament-exit hang round 2 — PatchShield must never shield the Gauntlet UI layer (#331, the REAL fix)

- **Round-2 evidence (post-Patch60):** the ~107s stall MOVED with the relocated movie release into `EndMissionInternal` (2026-07-09 logs: `ReleaseMovie=104,482ms` / `108,866ms`; `RemoveLayer=0ms`), with the gen0 GC delta **+8,276 in all three measured hangs** across different towns and 4-745 agents — a deterministic fixed workload intrinsic to releasing the Tournament movie. Round-1's static arithmetic (widget counts, O(1) scans) was built on assumed counts and wrong.
- **Measured, not modeled:** new `ExitStallSampler` (`Main/Features/BattleLoadDiagnostics/`) — background thread that photographs the MAIN thread's managed stack at +15/+30/+60s into any exit stall (armed by the exit window's new `ExitWindowOpenedUtcTicks`; `Thread.Suspend` + the obsolete-as-warning `StackTrace(Thread,bool)` ctor, net472). First repro named the sink in one shot: `PatchShield.ShieldFinalizerVoid` atop a 16-deep `WidgetTemplate.OnRelease_Patch2` recursion; the second sample caught `MethodBase.GetMethodFromHandle` inside `WidgetFactory.IsCustomType_Patch2`.
- **Three-factor root cause, each harmless alone:** (1) the engine's tournament UI re-instantiates bracket templates per round, accumulating `WidgetTemplate._customTypeChildren` into a ~10^6-call release recursion (fixed per tournament — hence the invariant gen0 delta); (2) UIExtenderEx legitimately patches `WidgetFactory.IsCustomType` (prefix) and blank-transpiles `WidgetTemplate.OnRelease`; (3) TAOM.Dependencies' **PatchShield** stacks a `__originalMethod`-binding Harmony finalizer on EVERY patched method in the process — Harmony's wrapper then pays `GetMethodFromHandle` + try/catch per call (~50µs). ~10^6 × ~50µs ≈ 107s of frozen exit.
- **Fix:** `PatchShield.Install` now skips targets in `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension`/`TaleWorlds.MountAndBlade.GauntletUI` namespaces (`ExcludedTargetNamespacePrefixes`) — the UI layer is per-widget-recursion hot and shield value there is nil. **Measured result: tournament exit 105-109s → 9.5s** (`ReleaseMovie=8,822ms`, gen0 delta +3). The residual ~9s is UIExtenderEx's legitimate prefix wrapper at ~10^6 calls — normal loading-screen territory, not worth patching third-party internals (simplicity criterion). The third prefix came out of the round-2 deep review's compat agent: TAOM's own Patch38 nameplate-fade target (~3000 calls/sec on the campaign map) was silently paying the same shield tax every frame.
- Patch60 (round 1) stays: the leak it fixes is real and its relocation is cost-neutral; its new per-exit `ReleaseMovie=Nms` stamp is the permanent regression canary. Sampler thresholds raised to +15/+30/+60s (above the known-good residual) and kept as standing diagnostics.
- Suite 4177 green (+12 tests: sampler schedule, exit-window ticks lifecycle, toggle/closer regressions).
- **Round-2 reviews (deep-review 5 agents + Codex review 73, 0 P1 / 2 P2 / 4 P3 — all addressed):** `Poll` gained an `Interlocked` reentrancy guard (Timer ticks overlap when a capture blocks); the sampler got its own MCM kill switch ("Enable Exit Stall Sampler" — the only diagnostics component that suspends the main thread); capture errors now log AFTER Resume (nothing allocates inside the suspended window); the compat agent's empirical CLR pass killed a false code comment (the `StackTrace(Thread,bool)` ctor was never hidden — a named-argument typo had been misread as a missing ctor; now a direct call) and caught TAOM's own Patch38 nameplate target (~3000 calls/sec) still paying the shield tax → third exclusion prefix `TaleWorlds.MountAndBlade.GauntletUI`. RCA findings 5-14: `docs/reviews/rca-tournament-exit-hang-2026-07-06.md`.

Research: WidgetTemplate.CreateWidgets/OnRelease + WidgetFactory.IsCustomType (installed 1.4.6), Bannerlord.UIExtenderEx WidgetFactoryManager.Patch (vendored DLL decompile), PatchShield.Install/ShieldFinalizerVoid
Save-compat: none — UI teardown + diagnostics only.

## 2026-07-09

### balance(special-resources): raise all caps 400–600 → 10000, zero the starting amounts

- **Why:** the 2000-cost Mûmakil (`harad_mumakil_rider`, `recruit_cost="2000"`) was permanently
  unrecruitable — every resource capped at 400–600, and a creature is charged in the *recruiting
  player's* resolved resource (War Spoils for Mordor/Isengard/Gundabad/Dol Guldur players, War Drums
  for Harad/Aserai players), both far under 2000. Raising all 11 caps to 10000 makes the Mûmakil — and
  any future high-cost special creature/elite — affordable in every faction.
- **Also:** `starting_amount` set to 0 on all 11 resources — heroes now begin with an empty reserve and
  earn from scratch (was 20–40).
- **Data-only:** `special_resources_config.xml`. The `cap` flows `SpecialResourceConfigProvider` →
  `SpecialResource.Cap` → the `Math.Min(current + amount, Cap)` earning clamp and the `… / Cap` map-bar
  display; no C# change. Config is singleton-cached, so a **full game restart** (not a save reload) is
  needed to pick up the new values.
- **Files:** `Main/_Module/ModuleData/special_resources/special_resources_config.xml`,
  `docs/features/special-resources.md`.

Save-compat: none — the raised cap only relaxes the ceiling (existing balances ≤500 round-trip unchanged
and may now grow toward 10000); `starting_amount` affects only fresh hero seeding, so no saved balance is
retroactively zeroed.

## 2026-07-08

### chore(docs): enforce config-example + version-marker consistency (prevent doc drift)

- **Why:** the v1.4.7 deep-review found the banner-color feature doc still advertised the old
  `EnableLayerLimitTranspiler: true` default after the flip — a silent doc-vs-code drift (docs aren't
  compiled or tested). Rather than just fix the one doc, make the whole class a hard gate.
- **Two new `tools/lint_docs.py` checks:** (1) **config-example drift** — a `docs/features/*.md`
  `json` example whose values disagree with the shipped `Main/_Module/ModuleData/**/*.json` config it
  mirrors (compares shared keys only, so partial examples are fine; also flags a doc key the shipped
  config no longer has); (2) **version mismatch** — CLAUDE.md's "Target: Bannerlord X" line(s) or an
  API-snapshot header that disagrees with `.claude/pinned-game-version.txt`. Historical docs
  (migration/archive/rca-/codex-*) are exempt, reusing the existing stale-version exemption set.
- **Enforcement:** `.claude/hooks/check-doc-config-drift.sh` (PreToolUse Bash) runs
  `lint_docs.py --fail-on-drift` and **hard-blocks `git commit`** when a relevant file is staged and
  drift/mismatch is found. Fail-open per the TAOM hook rule (no python / linter crash / nothing
  relevant staged never blocks). **Wiring into `.claude/settings.json` is pending — the
  config-protection guardrail blocks settings edits without an explicit OK (the hook is dormant until
  registered).**
- **Drift found + fixed by the new checks:** `docs/features/war-of-the-ring.md` config example
  (`triggerDay` 1→2/14, testMode days) was out of sync with the shipped `war_of_the_ring.json`;
  `docs/ai-includes/agent-operating-manual.md` + `docs/features/bannerlord-together-compat.md` still
  named v1.4.5 as the *current* target. All fixed; `config_drift` + `version_mismatch` now 0.
- **Also made version-labels self-updating** so this class stops recurring: `tools/snapshot_api_surface.ps1`
  and the `taom-src` skill now derive the version from `Version.xml`/auto-detection instead of a
  hardcoded string.
- **Tests:** `tools/tests/test_lint_docs.py` — 14 unit tests (value mismatch, partial-example OK,
  extra/removed key, non-JSON skip, BOM config, historical exemption, version consistency, v-prefix).
- **Files:** `tools/lint_docs.py`, `.claude/hooks/check-doc-config-drift.sh`, `tools/tests/test_lint_docs.py`,
  `.claude/skills/lint-docs/SKILL.md`, CLAUDE.md hooks table, the doc fixes above. RCA:
  `docs/reviews/rca-v1.4.7-bump-2026-07-08.md`.

Save-compat: none — docs, tooling, and a commit-gate hook only.

### chore(engine): bump to Bannerlord v1.4.7 + impact analysis

- **Bump:** Steam auto-updated the installed shipping client v1.4.6 → **v1.4.7** (base game + War Sails). Handled via the
  `/engine-bump` offline pipeline: preserved the v1.4.6 decompile baseline (`_shipping_build_v1.4.6` + `_categories_v1.4.6`),
  regenerated the category tree + dual-build + `_manifest.json` to v1.4.7, MD5-diffed the blast radius (**10 assemblies
  changed**, none added/removed), bumped `.claude/pinned-game-version.txt`.
- **Compatibility:** `BindingVerification` gate **green (50/50)** — every Harmony target, GameModel override, and reflection
  site still resolves against v1.4.7. Creature/scene parity clean (`audit_mount_parity`, `audit_action_set_parity` 0 gaps,
  `audit_battle_scenes` all 256 indices). API snapshot regenerated + reproducible; the generator now version-stamps from
  `Version.xml` so its header no longer goes stale. Full impact matrix: `docs/migration/v1.4.7-impact.md`.
- **Patch15_BannerLayerLimit disabled** — v1.4.7 "made the banner layers unlimited in the banner reader"
  (`Banner.TryGetBannerDataFromCode` no longer has the `RemoveRange`/32-cap), so the transpiler is a no-op that logged
  `RemoveRange not found` every load. Flipped `EnableLayerLimitTranspiler` false in BOTH `BannerColorConfig.cs` and the
  shipped `banner_color_config.json` (JSON overrides the C# default) + added an early quiet-return guard so a disabled
  transpiler no longer logs the warning (the warning fired before the flag was consulted). Kept, not deleted. 3 tests flipped.
- **Patch49_ArmyGatheringNreGuard kept** — the v1.4.7 "null reference in AI behaviour" fix is a different site; the
  decompile confirms the guarded `Army.FindBestGatheringSettlementAndMoveTheLeader` derefs (`Army.cs:726` / `:659`) are
  still unguarded in v1.4.7, so the crash guard remains load-bearing (comment refreshed).
- **Unaffected (verified):** save-metadata stamp (Patch61 already upserts), attacking-a-raiding-party + village-no-militia
  crashes (different sites), `.sack` shader bloat (no TAOM workaround), cloth-sim crash (NativeSkinFixes parked).
- **Owed:** in-game control battles (vanilla → creatures charge/melee, Messenger conversation exit, SmartCavalry charge,
  >32-layer banner) — the only checks an offline session can't run.

Save-compat: none — decompile/docs/config-default changes only; no save-serialized state touched.

### chore(rendering): disable NativeSkinFixes by default (parked at the wiring level)

- **Change:** the three native MinHook detours (covers_head hand-morph freeze + hair/beard cloth physics) are
  now OFF by default. The install call in `SubModule.OnBeforeInitialModuleScreenSetAsRoot` is commented out, so
  the hooks never load and engine rendering is vanilla for everyone — regardless of any persisted MCM value.
- **Why the wiring-level park, not just a default flip:** the install gate reads `TaomSettings.Instance.EnableNativeSkinFixes`,
  and MCM persists a user's saved value over the compiled default. Flipping the default alone would leave the feature
  ON for any machine that already saved the toggle ON (the NavalTravel-park rationale). The compiled MCM default is
  also set to `false` and the hint rewritten to note the parked state.
- **Files:** `Main/SubModule.cs` (install branch commented out + `RE-ENABLE` breadcrumb), `Main/Features/TaomSettings.cs`
  (`EnableNativeSkinFixes` default `true`→`false`, hint), `TAOM.Tests/.../NativeSkinFixesInstallerTests.cs` (pinning
  test flipped to assert the `false` default), `docs/features/native-skin-fixes.md` (parked status).
- **Reversible:** the native DLL + C++ source stay in place; RE-ENABLE = uncomment the install branch + flip the default.

Save-compat: no save impact — the change only governs whether native hooks install at boot.

### feat(map): lore + role starting building levels for all 221 towns & castles

- **Problem:** TAOM's `settlements.xml` seeded each fief's building levels (fortifications/barracks/marketplace/…)
  as a semi-random scatter uncorrelated with prosperity or importance — the lowest-prosperity town outgunned the
  highest, and only Minas Tirith was hand-set. New campaigns therefore started arbitrary. Building levels are read
  once at new-campaign creation (`Town.Deserialize`, skipped for saved games), valid range 0–3, fortifications
  floors at 1; towns carry 12 `building_settlement_*`, castles 11 `building_castle_*` (grounded in installed
  vanilla `DefaultBuildingTypes`).
- **Change:** every one of the 221 towns/castles hand-curated to a lore + role standard — capitals & legendary
  fortresses maxed (Minas Tirith, Barad-dûr, Erebor, Orthanc, Dol Guldur); the Black Gate / Cirith Ungol /
  Cair Andros / Helm's Deep read as great fortresses regardless of prosperity; remote holds sparse but defensible.
  Consistent numbers via a pinned role-tier expander + culture flavor (orc garrisons brutal & civic-poor; dwarven
  wall-and-mason; elven refined; Umbar mercantile). fort3 rationed to capitals + legendary fortresses only. Applied
  to the LIVE `TAOM_Map/ModuleData/settlements.xml`: 221 fiefs, 1,363 building levels altered.
- **Tooling (new, modeled on the prosperity analyze/rebalance pair):** `tools/author_settlement_buildings.py`
  (source of truth: hand decisions + deterministic expander → per-culture JSONs + audit doc),
  `tools/dump_settlement_buildings.py` (read-only current-level dumper), `tools/apply_settlement_buildings.py`
  (two-level-regex safe applier: `.bak`, byte round-trip, exactly-once assertion, range/fort-floor/id-set
  validation, dry-run default, idempotent). Decisions recorded in `tools/data/settlement_building_levels/*.json`.
- **Review:** 7-bloc adversarial workflow over the audit doc; 3 low-severity consistency fixes incorporated
  (Barad Wath / Barad Nûrn fort3→2 to reserve fort3 for legendary Mordor fortresses; Ardûvar fort2→3 to match the
  Khand capital). Verified live: re-run reports 0 changes (idempotent), XML re-parses clean, lore overrides confirmed.
- **Docs:** `docs/features/settlement-building-levels.md`; per-fief audit artifact
  `docs/reviews/settlement-buildings-audit-2026-07-08.md`.

Save-compat: seeds NEW campaigns only; existing saves keep their own building data.

### balance(culture-conversion): ship the 1-day conversion hold as the default for everyone (#333)

- **Change:** `RequiredHoldDays` / `CultureConversionHoldDays` default 45 → **1** in all three places — the JSON
  (`culture_conversion_config.json`), the compiled config fallback (`CultureConversionConfig`), and the MCM
  compiled default (`TaomSettings`). The MCM default is the one that actually governs a new player (MCM-over-JSON:
  the settings provider reads `TaomSettings.Instance.CultureConversionHoldDays` and only falls back to the JSON
  when MCM is absent), so all three move together to keep the shipped default coherent.
- **Effect:** a cross-culture fief converts the day after its hold begins — culture flips + notables replace almost
  immediately on capture, and the foreign-occupier loyalty penalty drops right away (near-instant pacification). A
  deliberate fast-war-map choice; raise "Days To Convert" in MCM for slower, gradual assimilation.
- **Existing players:** MCM persists a player's saved value, so anyone who already launched the mod keeps their
  stored hold (typically 45) until they reset MCM or set "Days To Convert" to 1 themselves; only fresh installs
  pick up the new default automatically.
- 4 config-provider default-assertion tests updated (45 → 1); suite 4169/0 green.

## 2026-07-07

### fix(war-of-the-ring): chunk momentum SyncData so it never corrupts saves — the v2.0.9 "A problem occured while trying to load the saved game." bug

- **Root cause (confirmed both halves against the decompiled v1.4.6 engine + our code):** `WarOfTheRingMomentum` serialized its entire event log as ONE SyncData string (`_taom_wotr_momentum_v2` — [WarOfTheRingMomentumBehavior.cs:86](Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs), each of up to 100 events/type/side carrying its full localized `Description` via [MomentumStateStore.cs:78](Main/Features/WarOfTheRingMomentum/MomentumStateStore.cs)). In a developed campaign that JSON crosses ~32 KB around day ~50. The engine's `ArchiveSerializer.SerializeEntry` writes each save-archive entry's length as `(short)Data.Length` — a signed int16 truncation (`ArchiveSerializer.cs:27`) — but writes the data in full, so any string entry > 32,767 bytes gets a wrong length **on write** and desyncs on the next load (`ArgumentException: Source array was not long enough` inside `ArchiveDeserializer.LoadFrom`, or `OverflowException` for the 32,768–65,535 range). Every save past that point was unloadable. Independently root-caused by a user whose forensics matched our arithmetic exactly (a day-52 save: 72,915 B true, stored as `72915 mod 65536 = 7379`).
- **Fix (cap + split, zero gameplay change):** `MomentumSyncChunker` splits the serialized JSON across a count key + N chunk keys, each capped at 10,000 UTF-16 chars (≤ 30,000 UTF-8 bytes worst case — a proven margin under the 32,763-byte entry limit). No single synced string can reach the engine limit regardless of how the log grows; descriptions, the 100/type cap, and the momentum math are all unchanged. Keys renamed `_v2`→`_v3` so an old single-string save loads as absent → one-time momentum reset (kingdoms re-enroll + momentum re-accrues on the next daily tick); the campaign is untouched.
- **Recovery for already-bricked saves:** `tools/repair_sav_strings.py` (offline, stdlib) decompresses the save, parses the Strings archive (recovering the truncated entry length via the sequential-entry-id anchor), resets the oversized momentum string to empty, re-frames + recompresses to `<name>_fixed.sav`. Zero campaign-data loss — only the cosmetic war-meter history is cleared; the fixed save loads on the vanilla engine (no runtime patch). Verified on two real user saves (day-52 desync case + day-20 negative-length case): both repaired, re-parse clean, pass `inspect_sav.py --verify`. A no-install **PowerShell twin `tools/repair_sav_strings.ps1`** (recommended for players — ships with Windows 10/11, uses .NET `DeflateStream` = the same library the engine uses) produces byte-identical decompressed output. Player-facing Windows how-to: `docs/SAVE-REPAIR-GUIDE.md` + `.html`.
- **Tests:** `MomentumSyncChunkerTests` (7) incl. the end-to-end proof — a realistic max log exceeds the limit as one string but every chunk stays under it and round-trips losslessly (multibyte-UTF-8 covered). Suite 4169/0.
- Diagnosis validated the new SaveLoadDiagnostics `ArchiveDeserializer.LoadFrom` hook — it's the exact stamp that fires on this in the field. **Follow-up:** bump the module version on every distributed package (v2.0.9 spanned 34 commits, which blinded field triage).

Save-compat: `_v2`→`_v3` key rename → one-time momentum reset on first load; no campaign data affected.
Research: ArchiveSerializer.SerializeEntry / ArchiveDeserializer.LoadFrom / SaveEntry / EntryId / BinaryReader/Writer / GameData.Write / MetaData.Serialize (installed 1.4.6)

### fix(culture-conversion): same-culture ownership changes no longer restart the hold-timer (#333)

Play-test report (Grymmclúd/`castle_E6` captured as Rhûn, still dwarven): the conversion pipeline itself was
healthy — the fief had simply been queued toward `khuzait` **16 times across four days of play with zero
completions**, because `OnSettlementConquered` restarted the 45-day clock on EVERY ownership change. The
capture→grant double-fire (every conquest), kingdom re-grants, barters, and same-culture recaptures each reset
the timer, so a contested frontier fief could never accumulate the hold.

- `CultureConversionService.OnSettlementConquered`: if a timer is already pending toward the new owner's culture,
  it now CONTINUES (placed after the recruitment-pool/player-owned gates, so gate cancels still win). A different
  culture still restarts; a recapture by the effective culture still cancels (uninterrupted-hold by design —
  documented as a known limitation with the MCM "Days To Convert" pointer).
- Cancel + stale-timer-drop paths now log at DEBUG — both were silent, which slowed the diagnosis.
- Tests: +2 (same-target re-grant keeps the original start day and converts on the original schedule;
  capture→grant double-fire keeps the first timestamp). Suite 4162/0 green.

### feat(diagnostics): SaveLoadDiagnostics (Patch61) — name the real cause behind "A problem occured while trying to load the saved game."

- **Why:** multiple players report saves failing to load with the engine's generic load-failure dialog. The engine swallows the real exception — `LoadContext.Load` catches everything and prints only `ex.Message` (with TWParallel fill loops that's the useless "One or more errors occurred"), `LoadResult.CreateFailed` records the hardcoded string "Not implemented", and CrashReport never fires because nothing escapes. Field triage was additionally blind because the shipped "v2.0.9" label spans 34 commits.
- **Feature (`Main/Features/SaveLoadDiagnostics/`):** always-on `[SaveLoad]` lifecycle logging to `Logs/taom_debug_*.log` — 15 thin hooks in four categories (`Patch61_SaveLoadDiagnostics` + one isolated category per internal-type reflection hook, so one drifted binding can't kill its siblings) delegating to a lock-free, fault-throttled service. All Finalizers are void + `Priority.First` — SaveShield (TAOM.Dependencies) finalizes 4 overlapping methods and swallows, so Patch61 must observe the exception first (33-agent adversarial review HIGH; the review also added header-phase/`ArchiveDeserializer.LoadFrom`/deferred-callback coverage, `SaveId.GetStringId()` attribution, and 4 binding drift-guard tests). Load side: save-identity dump at `TryLoadSave` (module list + versions = which build wrote the save), interior Finalizers at the graph throw sites (`LoadContext.CreateLoadData` for objects, `ContainerLoadData.FillCreatedObject/Read/FillObject` for containers — where the big SyncData dicts live), unknown-SaveId detection for definer/build mismatch (`ObjectHeaderLoadData.CreateObject` + `ContainerHeaderLoadData.GetObjectTypeDefinition` — the engine silently null-fills these today), per-behavior SyncData attribution (`CampaignBehaviorDataStore.LoadBehaviorData` — the engine's raw `(T)value` cast has no per-behavior context). Save side: `FileDriver.Save` Finalizer fires ON the async writer thread at the #292 `GameData.Write` throw site, `SaveOutput.PrintStatus` catches the faulted-task `Game.OnSaveCompleted` signature, non-Success `SaveResult`s logged (antivirus/OneDrive write blocks surface here). Every Finalizer rethrows — engine behavior byte-identical.
- **Build stamp:** `MBSaveLoad.GetSaveMetaData` Postfix writes `TAOM_Build` (assembly + informational version) into every save's metadata — future saves self-identify their exact build.
- **Offline triage:** `tools/inspect_sav.py` dumps a .sav's version/character/module table without launching the game; `--verify` walks the deflate data region (OK / truncated / corrupt with offsets). Already caught two zero-header corrupt saves (interrupted-write signature) on the dev machine.
- Applied in `OnSubModuleLoad` (Patch58 precedent) — loads fire from the main menu, so the late batch would miss the first load. 20 service tests + 4 binding drift-guards; `HarmonyPatchBindingTests` binds all 15 targets against the installed 1.4.6 engine; suite 4162/0.
- **Field confirmation (same day):** a user-supplied failure trace — `TaleWorlds.Library.BinaryReader.ReadBytes` "Source array was not long enough" inside `ArchiveDeserializer.LoadFrom`, then "Not implemented" — lands exactly on the instrumented `archiveParse` site and matches the truncated/incomplete-write class (two zero-header saves found on the dev machine; one affected user reported disabling antivirus).

Save-compat: additive — one inert metadata key (`TAOM_Build`); no SyncData, no definer.
Research: SandBoxSaveHelper.{TryLoadSave,LoadGameAction} / MBSaveLoad.{LoadSaveGameData,GetSaveMetaData} / SaveManager.{Load,Save} / LoadContext.{Load,CreateLoadData} / ObjectHeaderLoadData / ContainerHeaderLoadData / ContainerLoadData / FileDriver.{Save,Load} / AsyncFileSaveDriver / SaveOutput.PrintStatus / CampaignBehaviorDataStore.LoadBehaviorData (installed 1.4.6)

### fix(castle-recruitment): guard castle notable spawn against missing culture templates — new-game infinite loading loop

- **Crash (tester machine, `taom_debug_2026-07-07_13-32-19.log`):** starting a new campaign NRE'd in `HeroCreator.CreateHero` ← `CreateNotable` ← `CastleNotableMaintainer.EnsureCastleNotables` ← `OnNewGameCreated`. The escaped exception stalls the engine's `GameLoadingState` — it re-runs campaign creation every tick, so the same NRE recurred **26,306+ times over ~16 minutes** as an infinite loading screen (CrashReport dedupe-suppressed the repeats). `OnGameLoaded` calls the same path, so save-loads were equally exposed.
- **Engine mechanism:** `GetRandomTemplateByOccupation` returns **null** when `settlement.Culture.NotableTemplates` has no template for the requested occupation; `CreateHero` derefs it. Identical pitfall already guarded in `CultureConversionAdapter.ReplaceNotable` (#325) — `CastleNotableMaintainer` lacked the guard.
- **Fix (`CastleNotableMaintainer`):** (1) per-occupation template pre-check (skip + warn once per culture:occupation pair, naming castle/culture/occupation so the offending data self-identifies in the log); (2) per-castle try/catch in `EnsureAllCastles` + `TickCastle` — a handler that runs inside `OnNewGameCreated`/`OnGameLoaded` must never throw (the failure mode is a loading-loop hang, not a CTD); (3) null-check on the `CreateNotable` return. Maintainer now takes `IModLogger`.
- **Data audit (dev machine):** all 143 castles / 19 castle cultures in the live TAOM_Map have GangLeader/Headman/Merchant/Artisan templates resolving with correct occupations — current data cannot produce the null; the tester's install had a stale module folder (LOTRLOME_Armory 2.0.7 physical copy in place of the current-version link). The guard makes any such divergence a logged skip instead of a bricked campaign.
- **Deep-review hardening (same day, 5 agents: 2 MED + 1 LOW, all resolved):** (1) null-ENTRY gate added — `NotableTemplates` can contain literal null entries (`ReadObjectReferenceFromXml` returns null on a malformed `<notable_templates>` ref) and the engine's occupation filter does NOT null-check, so the original pre-check could pass while `CreateNotable` still threw (caught by the try/catch, but bypassing the warn-once dedup → daily error spam); the culture is now skipped entirely with a one-time warning, and the same gate is propagated to the sibling call site `CultureConversionAdapter.ReplaceNotable` (#325); (2) the `CreateNotable == null` branches in both call sites are unreachable on v1.4.6 (the engine throws rather than returning null) — re-commented as explicit forward-guards so no future maintainer mistakes them for the real safety net; (3) no unit test for the maintainer guard — declined per ADR-008 boundary convention, decision recorded. RCA: `docs/reviews/rca-castle-recruitment-guard-2026-07-07.md` (pattern: a guard copied from a precedent inherits the precedent's unverified assumptions).
- **Verified:** suite 4153 passed / 0 failed (post-hardening).

Save-compat: none — spawn-guard only; no new state.
Research: HeroCreator.{CreateNotable,CreateHero} / DefaultHeroCreationModel.GetRandomTemplateByOccupation / CharacterObject.CreateFrom (installed 1.4.6)

### chore(harness): encode the tournament-exit review lessons as permanent gates (#331 close-out)

- **`.claude/rules/harmony-patches.md`** — new "Latches & Toggle Gates" section (MANDATORY): (1) enumerate a closer for every latch-opener path, (2) toggles gate I/O never state transitions, (3) verify "unconditional" at the OUTERMOST gate (grep all callers). Auto-loads for every `Main/**/Hooks/**` edit.
- **`/deep-review` Agent 5** — new rule 5d (latch closer coverage + toggle gating + outermost-gate verification) so the Data Flow agent checks this class on every future review; fix-loop guidance now requires caller-layer verification before marking a guard-semantics fix done.
- Documentation sweep for #331: `battle-load-diagnostics.md` test counts + hardened window semantics, RCA finding #4 (Codex caller-gate catch), LESSONS-LEARNED outermost-gate clause, AGENTS.md review-72 entry, REVIEW-LOG row 21. Issue #331 closed — fix chain: measured 108s → engine leak root-caused → Patch60 → deep-review 2 MED fixed → Codex 1 P2 fixed → suite 4136 green.

## 2026-07-06

### fix(arena): release the tournament UI at mission end — kills the 30s-2min tournament-exit hang (#331)

- **Root cause (engine defect, 1.4.6):** `MissionGauntletTournamentView.OnMissionScreenFinalize` nulls `_gauntletMovie`/`_gauntletLayer` **without** `ReleaseMovie`/`RemoveLayer` (the arena practice view releases both correctly at the same hook). The leaked 'Tournament' movie — the only mission UI holding live item-tableau/character-tableau widgets (prize item, per-round weapon icons, winner panel), with a prize render request typically in flight ~0.7s before exit — is then torn down inside `ScreenBase.HandleFinalize`'s layer loop under the exit loading screen, after the mission frame pump is dead, where it stalled **108 measured seconds** (+8,276 gen0 GCs; native scene clear itself = 4ms). Same teardown while the mission is alive costs milliseconds (every practice exit proves it).
- **Fix:** `Patch60_TournamentExitMovieRelease` (`Main/Features/Arena/Hooks/`) — capture-Prefix + release-Postfix on `OnMissionScreenFinalize` replicating the practice view's `ReleaseMovie` → `RemoveLayer` sequence at the identical lifecycle point (mission renderer still alive). Postfix-shaped because the original body must run first (drops focus, finalizes the VM — releasing in a Prefix would NRE in `TryLoseFocus` on the cleared UI context). Fail-safe: any capture/release failure degrades to today's vanilla leak + hang, never breaks the exit.
- **Evidence chain:** exit-phase diagnostics (below) localized the hang to the screen-layer finalize loop; 22 research/verification agents over 2 adversarial rounds decompiled the installed engine end-to-end — all TAOM code exonerated (zero bytes allocated in the window), every alternative mechanism (widget-count teardown, gamepad-nav scans, native scene clear, crowd-size scaling, autosave) refuted with arithmetic.
- **Verified:** suite 4133 passed / 0 failed; `HarmonyPatchBindingTests` binds the new target against the installed engine; 2 new drift-guard tests pin the private-field bindings (a 1.4.x rename silently reverts to the leak — these turn it red offline).

Not-tested: the release under a real tournament exit (in-game verification owed — the hang is only reproducible in-game).
Research: MissionGauntletTournamentView / MissionGauntletArenaPracticeFightView / GauntletLayer.ReleaseMovie / ScreenBase.{RemoveLayer,HasLayer,HandleFinalize} / GauntletMovie.Release / MissionScreen OnEndMission-vs-UnregisterView ordering (installed 1.4.6)
Save-compat: none — UI teardown only.

### feat(diagnostics): mission-EXIT phase stamps — localize the tournament-exit hang (#331)

- **Problem:** exiting any tournament hangs the loading screen 30 s–2 min (constant, incl. the first tournament of a session); practice fights and field battles exit normally. Static analysis ruled out every TAOM tournament hook and mission-end teardown (all O(small); engine prize pools cached per tournament instance) — the time sink can't be located without exit-side instrumentation, which didn't exist (`BattleLoadDiagnostics` covered entry only).
- **Change:** 9 new exit phases in the `[BattleLoad]` log contract — `ExitBegin` (mission/scene/agent counts + GC/heap stamp) → `ExitTeardownBegin/Done` (`Mission.EndMissionInternal`) → `ExitStateFinalizeBegin/Done` (`MissionState.OnFinalize`) → `ExitResourceClearBegin/Done` (`Mission.ClearUnreferencedResources` — the forced full GC + native GPU clear) → `MapResumed` (GC delta + `SaveHandler.IsSaving`) → `FirstMapTick` (closes the window). Six new thin hooks in the existing `Patch43_BattleLoadDiagnostics` category; exit-window gating keeps probes silent where targets also fire at load/every-frame (`MapState.OnTick` postfix is a two-read early-out). Same `EnableBattleLoadDiagnostics` master toggle.
- **Verified:** all 6 new patches bind against the installed 1.4.6 engine (`HarmonyPatchBindingTests` green); +10 service tests (window gating, seq restart, GC/isSaving tokens).
- **Deep-review hardening (same day):** the Data Flow agent confirmed two exit-window state-machine defects, both fixed — (1) window state transitions were gated behind `IsEnabled`, so an MCM toggle-off mid-window latched it forever (now unconditional; only logging is gated); (2) the window opened for ANY mission but every closer was campaign-only, so custom-battle/chained-mission exits leaked it into spurious `MapResumed` stamps (now campaign-gated at `ExitBegin` + unconditional stale-close at `Mission.Initialize`; residual quit-to-menu case documented as a known limitation). +3 regression tests. RCA: `docs/reviews/rca-tournament-exit-hang-2026-07-06.md`; lesson appended to LESSONS-LEARNED (State/Lifecycle).
- **Codex adversarial pass (review 72, gpt-5.5 xhigh): 0 P1 / 1 P2 / 0 P3.** The P2 caught the deep-review fix being incomplete at the CALLER layer: `PlayerEncounter_Start_Patch` + `Mission_Initialize_BattleLoad_Patch` early-out on `!IsEnabled` before invoking the now-unconditional window-closers, so the toggle-off latch remained reachable through those paths. Fixed — both hooks call the state-closing service method before their toggle gate (service self-gates its logging). Suite 4136 green. LESSONS-LEARNED sharpened: "unconditional" must be verified at the outermost gate.

Research: Mission.EndMission/EndMissionInternal/OnMissionStateFinalize/ClearUnreferencedResources, MissionState.OnFinalize/OnTick, MapState.OnActivate/OnTick, SaveHandler.IsSaving (installed 1.4.6 via taom-src)
Save-compat: none — diagnostics only, no persisted state.

### refactor(career): prune passive-effect vocabulary, harden config parse, retune career screen

- **`PassiveEffectType` pruned + renamed + regrouped.** 15 unused members deleted (none referenced by code, shipped XML, or saves — the enum is parsed from XML at load and never persisted). 10 members renamed to project vocabulary: `SpecialResourceGain` / `SpecialResourceUpkeepModifier` / `SpecialResourceUpgradeCostModifier` (match the SpecialResources feature), `MountHealth` / `MountChargeDamage` (TAOM mounts aren't only horses), `SmithingCostReduction`, `TroopSurvival`, `HeroHealing`, `RenownGain`, `ShrugOff`. Swept across all consumers: 8 C# files, 240 `type=` attributes in `taom_career_choices.xml`, 4 test files, 2 tools scripts. Members regrouped by domain with a consumers-note header. The engine parameter `isShruggedOff` (TaleWorlds signature in `TaomCombatMechanicsModel`) is intentionally untouched.
- **Unknown `type=` values now warn at load** (`CareerConfigProvider.ParseChoice`): an unrecognized value previously coerced silently to `Special` (inert pip); now a WARNING names the choice id + raw value. Case-insensitive parity with `ParseEnum` pinned by a new test (`LoadChoices_UnknownPassiveType_LogsWarningAndFallsBackToSpecial`).
- **Career screen prefab retune** (`GUI/PreFabs/CareerSystem/CareerScreen.xml`): VisualDefinitions renamed (`CareerHeaderSlide` / `CareerFooterSlide` / `CareerNodePanel`) and retimed, inert `EaseIn` markup dropped (decompile-verified — the prefab parser never reads it), pane split now 520/1400, node hover width 768. `CareerChoiceGroupObjectVM` click handlers rewritten to LINQ (behavior-identical; click-rate only).
- Deep review (5 agents) + Codex pre-review: **0 HIGH/P1/P2**; 2 P3 test-hygiene findings fixed in-session (old vocabulary in test method names; negative-parse exemplar → synthetic `NoSuchEffectType`). Stale enum names fixed in `docs/features/battle-balance.md` + `special-resources.md`. RCA: `docs/reviews/rca-career-enum-prefab-cleanup-2026-07-06.md`; systemic lesson appended to LESSONS-LEARNED (rename sweeps end with a substring pass over tests/docs). Full suite green (4121 passed).

Not-tested: career screen render at the new geometry (in-game open owed — prefab drift fails silently).
Save-compat: none — `PassiveEffectType` is never persisted; career saves store id strings only.

### docs: archive cleanup and provenance-note refresh

- Removed 14 superseded research/review artifacts from `docs/reviews/` + `docs/archive/` (early one-shot research prompts, comparison studies, and raw adversarial-review transcripts whose distilled findings live on in REVIEW-LOG and the RCAs); updated the archive index and every referring link.
- Refreshed provenance/design notes across feature docs, CLAUDE.md, AGENTS.md, README, historical CHANGELOG entries, and tool headers; trimmed the now-empty README Acknowledgments section.
- Docs linter: no new dead links (13 pre-existing items unchanged). Build + CombatMechanics tests green after the comment-only C# touches.

## 2026-07-05

### fix(HeroRace): make race persistence robust to skins.xml race-list reordering (#330)

`RacePersistenceService` stores each hero's race in the save as an **int** — a position index into `FaceGen.GetRaceNames()`, the merged skins.xml `<race>` list in module load order. Insert/remove/reorder a race between save and load (a LOTRLOME skins.xml change anywhere but append-at-end, a Bannerlord patch touching Native races, the player toggling another race mod) and every saved int silently re-points to a *different* race — the existing `IsValidRaceId` guard is an in-range check and cannot detect a shift. Only append-at-end has happened so far (`sauron`, 2026-07-02), which is why no save has visibly broken yet.

- **Race-name legend**: `CaptureHeroRaces` snapshots the ordered race-name list as one `;`-joined string (the engine's own `GetRaceIds` delimiter), synced under the new key `_taom_raceNameLegend` beside the existing `Dictionary<string,int>`. Restore translates `savedInt → legend[savedInt] → GetRaceIdFromName(name)` — reorder-proof; a genuinely removed race skips + warns and the hero keeps its XML race. Deliberately NOT a `Dictionary<string,string>` (failed to round-trip `IDataStore` at ~1000 entries — WotR Momentum, 2026-07-03).
- **New `IRaceManager.GetOrderedRaceNames()`** — exposes the init-time FaceGen array; `GetAllRaceNames()` rides `Dictionary.Values` ordering and is unsafe for index math.
- **Clear-on-load**: `SyncRaceData` resets map + legend when `dataStore.IsLoading` before syncing — an absent-key `SyncData` leaves ref values unchanged, so a same-process load of an older-format/pre-TAOM save previously inherited the prior campaign's races onto colliding StringIds (#130-R1 class, until now only handled for new campaigns).
- **Migration is automatic**: pre-#330 saves have no legend → legacy raw-int path byte-for-byte (incl. race-0 bypass + `IsValidRaceId` guard); the first save after the update writes the legend. Old TAOM builds ignore the new key.
- +14 tests (legend shift/removed/out-of-range/no-op, capture, clear-on-load, legacy path, shifted round-trip; `GetOrderedRaceNames` order + fallbacks). Full suite green (4120 passed). Deep-review (5 agents): 0 code findings; engine semantics (`IsLoading`, absent-key behavior, `SyncData<string>` + `Dictionary<string,int>` support) verified against installed 1.4.6 DLLs.

Save-compat: new `_taom_raceNameLegend` string key; absent on old saves → legacy path; additive only. See `docs/features/hero-race.md`.

### feat(diagnostics): log which sieges hit the vanilla gathering dead end

`Patch49_ArmyGatheringNreGuard` already swallows the vanilla siege-start NRE in `Army.FindBestGatheringSettlementAndMoveTheLeader` (`Army.cs:726` null `GatePosition`) so a besieger army that can't resolve a gathering fortification no longer CTDs — but the finalizer logged only a context-free `LogDebug` breadcrumb, so there was no way to see *which* sieges are broken. The guard now records the failure context before swallowing, turning the dead end into a reviewable to-fix list.

- New `ISiegeGatheringDiagnosticsService` (+ `SiegeGatheringDiagnosticsService`): classifies each failure (`KingdomNull` / `NoFortifications` / `AllFortificationsUnderSiege` / `NoReachableFortification` / `Unknown`), **dedups by `(kingdom, focus settlement)`** — first occurrence logs full detail at **WARNING**, repeats increment a counter and drop to DEBUG so WARNINGs never spam. Routed through the existing `IModLogger` → `Logs/taom_debug_*.log`; grep the `[SiegeDiag]` tag.
- Boundary DTO `SiegeGatheringFailureInfo.FromArmy(Army, Settlement)` is the sole sealed-type reader (ADR-002/007, mirrors `TownFoodSnapshot.FromTown`): army/leader/clan/kingdom + focus settlement id/name/culture/faction + a one-pass `Kingdom.Settlements` fortification census (total / under-siege) + leader & focus map positions + campaign time. Every access is null-guarded — it never throws a secondary exception.
- The finalizer widens to inject Harmony's `__instance` + `focusSettlement`; the whole diagnostic path sits inside the existing try/catch, so if it ever throws the NRE is still suppressed — **the crash guard is never weakened, behavior is otherwise unchanged**.
- +14 tests (`SiegeGatheringDiagnosticsServiceTests`): every `Classify` branch, dedup/level routing, and null/NaN-safe `Format`. Full suite green (4106 passed). `FromArmy` + the finalizer stay in-game-validated (ADR-008).

Note: the guard runs *after* the throw, so under an attached debugger the NRE still surfaces as a first-chance exception at `Army.cs:726` (`Source = "0Harmony"`) — expected, not a guard failure; press Continue and read the `[SiegeDiag]` log. See `docs/features/army-targeting.md` "Patch49".

## 2026-07-04

### feat(momentum): reskin the War of the Ring map bar with custom LOTR art

The on-map momentum bar dropped its generic native widgets (`Kingdom.Support.Fill` / `SPKingdom\progress_bar_frame` / `Kingdom.Support.Handle`) for three custom LOTR-themed sprites under `GUI/SpriteParts/ui_taom/WarOfTheRing/` (Imagine-generated source, cut + composited to transparent PNGs locally with PIL — background removal needed a paid Imagine plan, so the cutouts were done with a corner flood-key for the frame/fill and a soft distance-matte for the Ring's glow):

- **`wotr_frame.png`** (700×164) — an obsidian+gold casing, the **Eye of Sauron (Evil) at the left end** and the **White Tree of Gondor (Free) at the right**, with a recessed channel between them (matches the bar's `positive = Free = right` convention). Opaque background.
- **`wotr_fill.png`** (360×57) — a clean red\|green track (`WarOfTheRing.Bar.Fill` brush) sized to sit inside the channel. A smooth red→green gradient muddies to brown/grey at the midpoint, so a two-tone split was chosen instead.
- **`wotr_ring.png`** (150×151) — the One Ring (`WarOfTheRing.Bar.Handle` brush), the sliding handle; it travels toward the Eye when Evil leads, toward the Tree when the Free Peoples lead.

`MomentumMapIndicator.xml` was restructured so the frame is the opaque background and the `SliderWidget` (fill + Ring handle) draws on top, sized + centered to the channel — measured at **55.4% × 34.2%** of the frame, centered on both axes, so alignment needs no margins. Brushes added to `Main/_Module/GUI/Brushes/BalanceOfPower.xml`. All bindings (`@Momentum`, `@IsIndicatorVisible`, click→popup) are unchanged; this is cosmetic only, no C#.

Play-test follow-ups during iteration:
- **Frame border re-cut** — the first local cutout used a flat flood-fill tolerance, which bled into the obsidian frame's near-black outer rim (same colour as the background) and ate ragged gaps + left stray specks in the silhouette. Replaced with a cleanly pre-cut source (transparent background, anti-aliased edges), autocropped + resized to 700w; channel geometry unchanged (55.3%×34.3%, centered) so the slider still aligns. (Frame PNG changed → **re-bake required**.)
- **Popup mirrors the bar** — the detail popup put Free on the left, but the bar puts Evil (the Eye) on the left. Swapped the popup's `Good*`/`Evil*` bindings (banners, leaders, both ally rows) and the `Number1`/`Number2` breakdown/stats columns so **Evil is on the left, Free on the right** everywhere. Prefab-only; the VM keeps Free=`Good*` semantics.
- **Raid momentum cut** (`momentum.json` `raidMomentum` 200→100→50) — village raids were the single largest momentum source for both sides, and because Good factions rarely raid it structurally over-fed Evil. Cut to a quarter of the original — now by far the lowest per-event weight (siege 250 / army 200 / battle-max 300). JSON-only; retune freely (singleton-cached → restart to reload).
- **Enemies-killed now feeds the meter** (new `MomentumActionType.EnemiesKilled` + `momentum.json` `killMomentumPerHundred` = 10) — battle-won momentum is `casualties ÷ loser-strength`-normalized, so it stayed tiny (16–27) despite hundreds of thousands of kills, and the huge "Enemies Killed" total was display-only. Added a raw-attrition source: each side scores momentum for the enemies it kills, on the same battles as the kill stat, shown as an "Enemies Killed" breakdown row (`= kills × 10 ÷ 100` displayed, 504h decay). Save-safe (enum persisted by name; old saves restore an empty queue). Pure `MomentumEventService.AwardKillMomentum`, validated config, 5 new tests (150 momentum green).
- **Battle-won bumped** (`momentum.json` `maxBattleMomentum` 300→350) — a slight increase to the win reward, per request.
- **Army-gathering weight cut** (`momentum.json` `armyMomentum` 200→50) — gathering an army is a routine, repeatable move, not a war outcome; now weighted the same as a village raid.
- **Settlement captures worth more** (`momentum.json` `siegeMomentum` 250→400) — taking a fief is the war's real objective, so it's now the highest per-event weight (battle-cap 350 / army 50 / raid 50). JSON-only.
- **Relative Strength retired** (`momentum.json` `maxStrengthMomentum` 300→0) — Evil out-strengths the Free Peoples for most of a campaign, so the daily strength-differential award handed Evil free momentum every day regardless of what either side did. `MomentumEventService.AwardDailyStrengthMomentum` now early-returns when the cap is ≤ 0, and `RelativeStrength` is excluded from the popup breakdown so no dead "Relative Strength 0/0" row shows. Config-reversible (set the cap > 0). 145 momentum tests green.
- **Map-bar title moved below** — the taller custom frame overlapped the "War of the Ring" title (the `ButtonWidget` drew it on top via a stale `MarginTop`). Title + bar now stack in a vertical `ListPanel` with the title **below** the frame. (Note: the editor sprite-bake's post-run sync copies the install's `GUI/PreFabs/` back over the repo, which reverted this + the popup side-swap once — re-applied; deploy repo→install and don't sync prefabs install→repo.)
- **Popup banner flicker** (`MomentumPopupVM`) — leader/ally banners flashed in and vanished. Root cause: the popup live-recomputes on every `MomentumChanged`, and each `Rebuild` re-created the `BannerImageIdentifierVM`s; banner textures render asynchronously, so a fresh event replaced each VM before its texture finished. Fixed by building the banner/roster VMs **once** at open and refreshing only the numbers (total/color/breakdown/stats) on change — the enrolled factions don't change during the popup's brief life. Not a reskin regression (pre-existing in the live-recompute path).

New helper `tools/sync_sprite_bake.ps1` — copies ONLY the editor sprite-bake outputs (manifest + `AssetSources/GauntletUI/` + `Assets/GauntletUI/`, mirrored) from the game install back to the repo, and nothing else. Replaces the manual whole-folder copy that was silently reverting repo prefab/brush/JSON edits (the root cause of the "it keeps using the old png / my change didn't take" churn this session). Source files flow repo→install via `build.ps1` only.

**Not-tested:** the three sprites are loose PNGs that must be packed by the editor sprite-generation (`SpriteSheetGenerator.exe` + the `ui_taom_*_tex.tpac` texture-compile) before they render — a loose PNG is blank until baked. Sizes/alignment are first estimates; the bake + one in-game tuning pass are the remaining step (baked ≠ visible). Feature doc: `docs/features/war-of-the-ring-momentum.md` "UI & display".

### chore(logging): trim now-redundant per-tick diagnostics from four working features

A single 2.5-hour session produced a 21 MB / 169,676-line `taom_debug_*.log` — 97.5% of it per-tick tracing from features that are now confirmed working (and the log is bundled into crash reports via `IModLogger.LogFilePath`, so it bloated every crash ZIP). `FileLogger` has no level filter, so the noise was removed at the call sites; all WARNING/ERROR lines and the one-time "feature loaded" INFO markers are kept.

- **AlignmentDesertion** — the `AlignmentDesertionBehavior` desertion log (INFO, 60,432 lines this session, **0** of them player-relevant) now gates on `isPlayerOwned`, so only the player's own desertions record; AI-kingdom desertions no longer log.
- **CultureMarketplace** — the per-town daily summary (DEBUG, ~87.9k lines; 89% were `+0 injected` no-ops) reverts to its pre-2026-05-21 `>0` gate (logs only when a pass changed the roster); the per-tick "No pool for culture X" (~16.9k) and "no owner culture" lines gain once-per-culture / once-per-settlement `HashSet` guards.
- **Momentum** — deleted the per-battle `Player event recorded` DEBUG counter (~1.3k) and removed the now-dead injected `IModLogger` from `PlayerMomentumService` (enrollment/victory INFO is unaffected).
- **SpecialResources** — deleted the per-day `DAILY: net=…` DEBUG (~1.1k). The resolve/`CanAfford` DEBUG lines were left as-is (already once-per-key guarded, or bounded to the open party screen); every resource-reward line stays INFO.
- **Diplomacy** — dropped the two allowed-path `AllianceActionHook` DEBUG lines; the blocked-path INFO stays.

Net: ~170k → ~2k lines per session, with every meaningful marker preserved. One regression test pins the CultureMarketplace once-per-culture guard. Full suite 4087 pass.

### feat(caravan): AI caravans range further, trade across the war, carry fuller baskets

New `CaravanTrade` feature (`Patch59_CaravanTrade`) — four Harmony postfixes on vanilla `CaravansCampaignBehavior` private methods plus two `TaomCaravanModel` overrides, all delegating to a pure `ICaravanTradeService`. Fixes the "caravans shuttle between Minas Tirith and East/West Osgiliath and only buy one good" behavior. Mirrors the `ArmyTargeting` service+config+MCM pattern; **master-off = exact vanilla**, fully save-clean (no `SyncData`).

- **War gate** (`CanTradeWith`) — in TAOM's endless Free-vs-Evil war, vanilla only lets caravans visit non-enemy factions, which pens them into their own clustered towns. Lifts the war veto per `WarTradePolicy` (default `SameAlignmentAndNeutral`: a Free caravan reaches other Free + Neutral towns but not Evil ones). Only ever flips a war-caused `false→true`; honors the player's prohibited-kingdom list even during war.
- **Range re-weight** (`GetTradeScoreForTown`) — vanilla scores a town by `1/days`, so the closest always wins. Strips that spike and re-applies a gentler `1/(nearFieldFlatten+days)^decay` curve (clamped), with an anti-shuttle cut on the town just left. Near-equal towns tie on distance so the built-in profit estimate decides; longer profitable trips become competitive. Selection-only — profit/payout untouched.
- **Range envelope** (`CacheVeryFarDistances`) — scales the vanilla "very far" ceiling by `rangeMultiplier` so profitable distant towns aren't hard-rejected. Once per session.
- **Basket diversity** (`CalculateBudgetFactor` + `GetInitialTradeGold`) — vanilla's `budgetFactor = 0.1 + gold/5000` leaves a poor caravan buying one good; a floor + higher starting-gold let more categories clear the buy gate.

"Further = more money" is **emergent**: vanilla already prices undersupplied far towns up to 10× — the feature just lets caravans reach them, and the existing ClanFinance drip pays the owner more. No injected gold. Applies to player caravans too (MCM-toggleable). Config `caravan_trade/caravan_trade_config.json` (validated, MCM-over-JSON) + MCM "Caravan Trade" group. Research verified all bindings against installed v1.4.6.

Deep-review (5 agents): Standards PASS, Compat 24/24, Completeness COMPLETE. The data-flow agent caught one **HIGH** — the `SameAlignmentAndNeutral` default silently blocked all Neutral-faction trade because it delegated to `IAlignmentService.AreEnemyAlignments`, whose "Neutral is everyone's enemy" semantics are inverted for this purpose (the sibling `AlignmentRecruitment` feature had already documented this trap). Fixed in-session by resolving `GetKingdomSide` directly + Neutral-faction regression tests. `/review-codex` (gpt-5.5 xhigh) then returned **0 HIGH, 4 MED, 1 LOW**, all fixed/documented: the range lever now scales a read-each-time getter (`GetDistanceLimitVeryFarAsDaysForNavigationType`) instead of mutating the cache, so an MCM master-off reverts it live; a player-founded kingdom now sides by culture fallback (`GetKingdomSide`→`GetCultureSide`) so it can't trade across the Free/Evil line; and the range lever's global scope + the war gate's faction-level scope are documented honestly in the MCM hint. Codex correctly disproved a seeded player-detection hypothesis by decompiling the caravan-creation path; its double-distance MED was verified cache-backed (not a pathfind) and kept for terrain accuracy. RCA (both passes): `docs/reviews/rca-caravan-trade-2026-07-04.md`; feature doc: `docs/features/caravan-trade.md`. Tests: 58 CaravanTrade (service matrix + war-policy incl. Neutral & player-founded kingdom + config validation + binding drift-guards); full suite 4086 pass.

### chore(localization): full 11-language AI rerun + translator gap-file / parser fixes

Re-ran the AI translation pipeline (`translate_with_claude.py` → `rebuild_translation_files.py`) across all 11 AI-translated languages for every module — TAOM + TAOM_Map (settlements) + LOTRLOME_Armory. Filled the three shipped string files that both scripts had silently skipped, so they had been English-only in every non-PL language: `taom_wotr_strings` (23, momentum UI), `taom_lotr_issue_strings` (308), `taom_emissary_strings` (21). ~6,000 strings translated; $6.14 total.

- **Source-list gap fix** — added `lotr_issue` + `emissary` to `translate_with_claude.py`'s `english_source_files` and `wotr` / `lotr_issue` / `emissary` to `rebuild_translation_files.py`'s `taom_sources`; both now cover all 10 shipped `std_taom_*` files.
- **Parser hardening** — `_extract_translations` accepts the JSON response shapes the model actually emits (alternate value key, single-key wrapper, bare `{id: text}` map), not only the prescribed array. A shape drift had been wiping whole 40-string batches to 0/40 (323 strings failed the first pass; a retry after the fix rescued all but 7).
- **Residual** — 7 gender-conditional `{?GENDER}…{?}…{\?}` strings in `taom_module_strings.xml` (DE/CNs/FR/KO/TR) stay English; the placeholder-integrity guard rejected translations that changed the conditional-token count.

PL untouched (community hand-translated). TAOM_Map + LOTRLOME_Armory outputs write to the game install (external modules), not this repo. Validation: `LanguageDataXmlTests` 22/22.

### feat(momentum): endless war by default — victory is now opt-in (#327)

Added a victory on/off toggle (`victoryEnabled` JSON + MCM "Enable Victory"), **default OFF = endless war**. `MomentumVictoryService` returns None when off, so no side ever wins — the War of the Ring is tracked open-endedly. Reason: with the (intentionally) runaway momentum, an enabled threshold-victory fires almost immediately once the player has ~5 events, which ends the war anticlimactically (a play-tester hit "Long live Sauron!" unexpectedly). The victory machinery is fully wired + tested; enabling it is best paired with a future bounded-momentum rebalance. On load with victory off, a war that ended under a prior build is un-frozen (momentum/kingdoms/stats kept) so the meter resumes — an already-ended save becomes endless again.


### docs(momentum): refresh feature doc + index the feature (#327)

Brought `docs/features/war-of-the-ring-momentum.md` current with all play-test fixes (kingdom resolution, JSON-string persistence, culture-fallback + reconciling enrollment, Khand-neutral, ratio slider, colored Total), added a UI & display section + a play-test fix-history table + the runaway-momentum known-limitation, and indexed the feature in `docs/INDEX.md` + CLAUDE.md Key Paths (it was undocumented in both).


### fix(momentum): map bar now moves + colored balance total (#327)

Two play-test UI issues:

- **Map slider was pinned to one end and never moved.** It normalized the raw momentum lead against the victory threshold (500), but in a long war the lead accumulates many times past that (trimmed-at-cap events never subtract; the player gate can hold the war open), so it clamped forever. Replaced with a RELATIVE balance ratio `(free − evil)/(free + evil)` mapped to −100..+100 — the bar stays readable at any magnitude. Sign flipped so **positive = Free ahead = bar fills right toward the green end** (was positive = Evil), matching green-good intuition.
- **Popup total was an ever-growing negative number in near-invisible dark text.** Now shows the bounded balance magnitude (0–100), colored **green when the Free Peoples lead, red when Evil leads** (parchment when even) — direction by color, so the sign isn't needed; also fixes the readability.


### balance(alignment): Khand (battania) is now Neutral, not Evil

Changed `execution/alignment.json` `battania` from `evil` to `neutral`. Khand is a shared alignment key, so this applies to ALL alignment-aware systems, not just the War of the Ring meter: it no longer enrolls on the Evil side of the momentum war, no longer blocks/ is blocked by recruitment, its troops no longer desert over alignment, and it gets neutral execution-relation + diplomacy treatment. Updated the enrollment comment + the three alignment feature docs. New-campaign + live-save effective (config read at load; the momentum meter drops Khand on the next enrollment sweep).


## 2026-07-03

### fix(momentum): reload reset + blank banners + Relative-Strength 0 + narrow columns (#327)

In-game play-testing found three issues (two of them self-inflicted by the deep-review efficiency "fix"):

- **Blank Leaders/Allies banners + Relative-Strength stuck at 0/0:** the deep-review efficiency fix had swapped `Kingdom.All.FirstOrDefault(k => k.StringId==id)` for `MBObjectManager.GetObject<Kingdom>(id)` in `KingdomStrengthAdapter` + `MomentumPopupVM.ResolveKingdom`. `MBObjectManager` does NOT resolve campaign kingdoms → null → blank banners + every side strength 0 (so the daily strength award never fired). Reverted to the vanilla `Kingdom.All` idiom (the scan was never a hot path). Unit tests missed it because they mock the adapter — live-only regression.
- **State reset on save/reload:** the momentum store was synced as a `Dictionary<string,string>` (up to ~1000 entries in a deep campaign), which did not round-trip through the engine's `IDataStore` at scale — total stats and momentum reset every load. Now the store dictionary is JSON-encoded to a single string and that string is synced (key `_taom_wotr_momentum_v2`); a single string is unbounded and needs no container definition. Existing test-saves reset once (old dict-format key is ignored), then persist.
- **Popup number columns wrapped** (`12200` rendered as `200-`/`00`): the four value `TextWidget`s were pinned at `SuggestedWidth=50`, too narrow for 5-6 digit totals. Widened to 120.


## 2026-07-03

### feat(momentum): War of the Ring momentum — Evil vs Good progress tracking, victory, and map UI (#327)

Port of LOTRAOM 1.2.12's "Momentum" system onto TAOM 1.4.6, wired into the existing WotR phase machine
(`Main/Features/WarOfTheRingMomentum/` + `UI/`; ~20 services/VMs, 140 new tests, feature branch `feature/wotr-momentum`).

- **Scoring**: signed Free↔Evil momentum from battles won (scaled by casualties ÷ loser-side strength, cap 300),
  sieges (+250), raids (+200), armies gathered (+200), and a daily strength-differential award (cap 300); events
  decay after 21d/21d/21d/7d/12h. Player participation multiplies gains ×1.5 (MCM-tunable) and records toward the
  victory gate. Sides come from dynamic enrollment: every `alignment.json` Free/Evil kingdom sweeps in at FullWar
  (covers player-founded kingdoms; Neutral never enrolls; enrollment never declares wars — Diplomacy owns stances).
- **Victory**: at ±500 internal momentum (MCM 100–2000) or one side eliminated — gated on ≥5 player events (both
  sides, LOTRAOM parity) — the war ENDS: new `WarPhase.WarEnded` terminal state lifts all three peace-block layers
  (they key off `IsWarOfTheRingActive`/`ShouldBlockPeace`), cross-side at-war pairs peace out via
  `IAllianceAdapter.MakePeace` (ordering pinned by test), a localized inquiry announces the winner, meter freezes.
- **UI**: persistent on-map "War of the Ring" slider (MapView + GauntletLayer, appears at FullWar, MCM-hideable)
  opening a popup — faction banners (Gondor/Mordor), leaders/allies rows, per-type momentum breakdown with
  accumulating tooltips, total-stats table. TAOM's fork-residue MomentumView prefabs reused (already 1.4.x-migrated);
  edits: `StaticDiplomacyButton` (Diplomacy-mod dependency) deleted, dead `ListWidget` (removed in 1.4.6) replaced,
  labels localized, `KingdomIcon.xml` deleted. Zero new sprites/fonts.
- **Persistence**: primitive-dict SyncData `_taom_wotr_momentum` (Messengers pattern, no SaveableTypeDefiner);
  fixes LOTRAOM's unpersisted player victory gate. Phase + outcome persist in the Diplomacy behavior.
- **Deliberate deviations from LOTRAOM (bugs not ported)**: config event values are internal-scale units — the
  donor added them raw while comparing raw÷100 against the threshold, so its own tuning comments ("~2 sieges for
  victory") were off by 100× and the meter barely moved; raids now require an ENROLLED kingdom (donor sided raids
  by culture, so every looter raid fed Evil +200); alliance-stance reflection dropped (`StanceType.Alliance` does
  not exist on 1.4.6 — would throw at runtime); indicator VM event-subscription leak fixed.
- Config `momentum/momentum.json` (validated, defaults = donor's shipped XML values) + MCM "War of the Ring/Momentum"
  (enable, map meter, threshold, multiplier, player gate). Strings `taom_wotr_strings.xml` (localization pass pending).
- 1.4.6 drift handled: `Kingdom.CurrentTotalStrength`, `MapEventSide.TroopCasualties`, `ArmyGathered(Army, IMapPoint)`,
  `BannerImageIdentifierVM`, `GauntletLayer(string,int)`, banned `PartyBase.Owner` getter avoided via `MobileParty?.Owner`.

Not-tested: in-game meter/popup rendering + victory flow (control campaign pending; testMode phase2Day=3 fast-path).

### fix(momentum): Codex adversarial-review findings (#327)

Codex (1 HIGH, 2 MED, 2 LOW, all confirmed + fixed; RCA `docs/reviews/rca-wotr-momentum-2026-07-03.md` Codex-pass section):

- HIGH: a player-FOUNDED kingdom (id not in `alignment.json`) resolved Neutral and never enrolled — the player's own war contributions weren't counted and their kingdom never showed on the meter. Enrollment now falls back to the kingdom's CULTURE side (`GetKingdomCultureId` → `GetCultureSide`), reproducing LOTRAOM's culture-based siding for dynamically-created kingdoms.
- MED: battle momentum `casualties/loserStrength` is now clamped at 1.0 so a lopsided endgame battle can't blow past the documented `MaxBattleMomentum` cap and instant-win the war.
- MED: the enrollment sweep now prunes enrolled ids absent from the live kingdom set, so a kingdom destroyed while the feature was toggled off can't linger and block the elimination-victory count.
- LOW ×2: corrected `war-of-the-ring.md` (WarEnded phase + persisted phase/outcome) and an enrollment comment (Khand/`battania` is Evil, not Neutral).
- +6 regression tests; full suite 4,021 green.

### balance(startup-resources): retune per-culture lord gold + clan influence

New-game startup grants (`startup_resources_config.xml`; new campaigns only):

- Elves (rivendell/lothlorien/mirkwood): influence 1000 → **1250** per clan (gold stays 600k per lord).
- Erebor: gold 50k → **800k**, influence 150 → **1000**.
- Khuzait (Easterlings): gold 50k → **75k** (influence stays 1000).
- Gondor: gold 50k → **100k**, influence 500 → **1000**.
- Isengard/Dol Guldur: gold 200k → **75k**, influence 2000 → **500**; Gundabad: gold 200k → **75k**, influence 2000 → **1000**.
- Umbar: influence 500 → **1000** (gold stays 200k).
- `playerGold` and all other cultures unchanged.

### feat(culture-conversion): notables now convert with the settlement — foreign-culture notables replaced at conversion (#325)

Review confirmed the reported gap: a Mordor-captured Gondor town flipped `Settlement.Culture` (recruitment,
militia, loyalty) after the hold period, but its notables stayed Gondorian forever — nothing in TAOM or vanilla
ever changes a living notable's `Hero.Culture`, and vanilla turnover can't fix it (a notable dying at power ≥ 100
spawns a relative that COPIES the old culture; only rare low-power propertyless notables disappear for the weekly
deficit refill to backfill from the converted culture).

- `ApplyConversion` now replaces each still-alive, culture-mismatched notable in the town/castle + bound villages,
  AFTER the culture flip (replacement templates come from the NEW culture's `NotableTemplates`).
- Per notable, `CultureConversionAdapter.ReplaceNotable` runs the order-critical engine sequence: template
  pre-check (CreateNotable NREs on a missing occupation template — skip+warn instead) → spawn same-occupation
  replacement → transfer workshops/alleys/caravans (`ApplyByDeath`/`SetOwner`/`TransferCaravanOwnership` — before
  removal, or the engine destroys/reassigns them) → cancel any issue/quest (`CompleteIssueWithCancel`; relations
  deliberately NOT transferred) → zero power (suppresses the vanilla old-culture heir spawn at
  `NotableDisappearPowerLimit`) → `KillCharacterAction.ApplyByRemove`.
- Fail-safe throughout: any per-notable skip keeps the old notable + warns, never blocks the conversion or the
  daily tick. One-shot at conversion — the on-load re-apply never replaces. Restore-to-original replaces
  symmetrically.
- New `replaceNotablesOnConversion` JSON field + MCM "Replace Notables On Conversion" (default on).
- Data audit (one-off script): every conversion-eligible culture — all `taom_spcultures.xml` cultures + the 6
  vanilla-id cultures re-templated in `spcultures.xslt` — covers all 5 notable occupations; the pre-check fail-safe
  is currently unreachable for real cultures.
- Tests: +9 (8 service replacement decisions incl. flip-before-replace ordering + fail-continue + re-apply guard;
  1 config default). Full suite 3873 green. Engine signatures verified against installed 1.4.6 via `taom-src`.
- Reviews: `/deep-review` (5 agents — 0 code findings; 2 process findings fixed, RCA
  `docs/reviews/rca-culture-conversion-notables-2026-07-03.md`) + Codex adversarial (gpt-5.5 xhigh) VERDICT
  CLEAN, all 6 seeded Known Suspects disputed with decompile evidence (Review 70).

**Save-compat:** additive — no new SyncData; pre-feature converted settlements keep their old notables (documented
limitation; reconquest + re-conversion catches them up).

### balance(lords): north-orc Leadership raised to 130 average (#328)

The #322 cut left gundabad/dolguldur/goblin/mistymountainorcs lords at 74-84 avg Leadership — too weak on
morale/garrison scaling. +52 on the `north_orc_*` trio (227/127/112) + Bolgath (237): pooled resolved average
lands exactly 130.2 (per-culture 126-133). Mordor/Isengard/Dunland verified untouched; Steward stays ~100.
New campaigns only.

### docs(lords): lord-skills docs caught up to the balance arc (#322–#326)

`docs/ai-includes/lord-skills-authoring.md` (the `/lord-skills` source of truth) rewritten where stale:
regen-drift pre-flight + repoint-script contract in Quick reference, archetype catalog renumbered to current
values (74 archetypes; elf/dwarf command tiers), new "Per-culture balance variants (`archetype_alias`)" section
with the fork/alias/repoint rules, post-#326 power-threshold tiering, 7 new gotcha rows (generator drift,
per-culture regen unsafety, shared-set bleed, stale culture maps, child-lord rule, diff re-anchoring), file map +
verification checklist extended. Also: CLAUDE.md Rebalancing Tools table + tools/README gain
`apply_culture_skills_traits.py` / `repoint_evil_lord_skillsets.py` / `author_elf_lords.py` rows;
`docs/features/lord-perk-review.md` documents the khand/mirkwood grouping fix + the inline-sync mismatch cleanup;
4 lessons appended to LESSONS-LEARNED (generator drift, diff-presentation surgery, re-themed culture maps,
shared-set fork discipline).

### balance(lords): multi-culture Steward/Leadership/Tactics retune (#326)

Second balance pass on lord army stats, to resolved-lord average targets (children/rookie-template
lords excluded, per the established child treatment). Landed (resolved avg, target):

- **Elves** (mirkwood/lothlórien/rivendell): Leadership +72, Tactics +61 on all elf archetype +
  canonical sets — pooled resolved avg lands 299.8 Led / 300.2 Tac (per-culture 291–312 spread from
  set-mix composition; the three cultures share the elf sets, so per-culture exactness would need
  absurd per-canonical residuals).
- **Gondor** 200/200/190 (S/L/T, exact): +8/+26/+25 across 34 canonical sets + `elder_lord` in place;
  6 new `gondor_*` forks of the shared man sets (knight/lady/young_lady/young_lord/matriarch/lord).
- **Erebor** 280/300/310 (exact): +73/+127/+140 on the 5 dwarf sets + canonical E1_2 — dwarves are now
  the premier non-elf commanders.
- **Rohan** 180/180/190 (exact): +7/+12/+23 on rider/shieldmaiden/horse_breeder + 18 canonicals;
  4 `rohan_*` forks.
- **Dale** Tactics 175 (exact): +22 on `dale_lord` + matriarch/lord (dale-only after the gondor forks)
  + 4 `dale_*` forks (Tactics-only).
- **North orcs** (gundabad/dolguldur/goblin/mistymountainorcs — misty included per user call):
  Steward −53 on the north_orc trio + Bolgath — pooled resolved avg exactly 100 (per-culture 91–104).
- Shared base sets (`taom_knight/lady/young_*/matriarch/lord`) are UNCHANGED for
  shaghana/abanissa/rhun/harad/umbar/khand — verified byte-identical averages.
- Mechanics: 14 new fork archetypes + 3 `archetype_alias` maps in the generator (145 sets total,
  regen acceptance = exactly the planned cells); repoint script now syncs the FULL inline `<skills>`
  block from `taom_lord_skill_sets.xml` for every managed-culture lord (replaces the hand-maintained
  parity map; 187 template swaps, post-condition + idempotency PASS).
- **Save-compat:** hero skills bake at creation — NEW campaigns only.

## 2026-07-02

### content(lords): elf lord expansion — Lothlórien 10 adults (+2 new clans), Rivendell 20 adults (#324)

Party size per lord was fixed by #323, but army COUNT is capped per clan (tier<3: 1 party, t3-4: 2, t5+: 3 —
`DefaultClanTierModel`). Lothlórien had 3 adult lords in one clan; Nos Glorfindel (t6, 3 slots) had one. Now:

- **+7 Lothlórien lords, +2 clans**: `clan_lothlorien_2` **Wardens of the Naith** (t6 — Thandirion elf_lord owner,
  Baranthir, Aeglossen elf_archer, Nimlothiel elf_lady, + existing Caurmínas moved in from clan 1, fixing his
  L2-id-in-clan-1 mismatch) and `clan_lothlorien_3` **Nos Malgalad** (t5 — Malthorn elf_lord owner, Galuvir,
  Silivren elf_lady). Kingdom party slots 3 → 9; 10 adult lords, adult avg Steward 340.5.
- **+3 Rivendell lords** into Nos Glorfindel: Gildor Inglorion, Erestor (elf_lord counsellor), Lindir — clan now
  fills its 3 slots; kingdom at 20 adult lords, adult avg Steward 334.4.
- Authored by new one-off `tools/author_elf_lords.py` (--dry-run/--apply, well-formedness gate): NPCCharacter
  blocks (inline skills = live SkillSet values incl. the Steward boost, archetype traits, culture equipment
  templates a–e rotated, donor elf face keys per the existing shared-key convention) + Hero lore blurbs + Faction
  blocks (banner keys donated from existing elf clans). 4 canonical archetype pins added to the generator
  (regen-stable, byte-identical sets file). `validate_moduledata` PASS.
- Names avoid collisions — Haldir/Rúmil/Orophin already exist as Mirkwood lords, so Lothlórien's new lords use
  invented Sindarin names; Rivendell reuses canon Imladris figures.
- **Save-compat:** new heroes/clans appear on NEW campaigns only. Localization keys (`{=aom_*}`) ship with inline
  English defaults; 12-language propagation is a follow-up (`/localize`).

### fix(tools): Culture.battania is Khand, not Mirkwood — rebalance_lords mapping corrected

`rebalance_lords.CULTURE_MAP` still carried the pre-mirkwood-culture `battania → mirkwood` entry, so the 41 Variag
lords (taom_spcultures renames battania to Khand) received **elven** cultural modifiers on any `--apply` and were
folded into "mirkwood" in every balance report (71 = 30 elves + 41 Variags — the earlier session tables had this
pollution). Now `battania → khand` (no CULTURAL_MODS entry → baseline curve, same as mordor/goblin), and the real
Woodland Realm gained its missing `Culture.mirkwood → mirkwood` entry — without it those 30 lords fell through to
NO mods while Khand wore their elf bonuses. Reports now show khand (41) and mirkwood (30) separately.

### balance(lords): lord_R3_1 assigned a real elf SkillSet; child lords stay on rookie templates (#323 follow-up)

Of the 5 elf lords outside the TAOM SkillSet system, only one was a real gap: `lord_R3_1` (adult, age 30, owner of
the third Rivendell clan, placeholder name) had **no skill_template at all** — he now resolves to
`taom_elf_lord_skills` (Steward 355) via a canonical entry in the generator + a new `TEMPLATE_ASSIGN` mechanism in
`tools/repoint_evil_lord_skillsets.py` that inserts the missing attribute. The other 4 (`lord_M1_12`, `lord_L1_3`,
`lord_R1_11`, `lord_R2_11`) are **children aged 6-12** (two literally named "PlaceHolder Child") — vanilla
`spc_*_rookie` templates are the correct child-hero treatment (the generator deliberately skips age<14), so they
keep them. Also noted: Círdan's `taom_canonical_lord_R3_2_skills` is orphaned — no `lord_R3_2` NPC exists.
Adult-only elf Steward now: lothlórien 355 / rivendell 337 / mirkwood 333.

### balance(lords): elf lord Steward +100 — Rivendell / Lothlórien / Mirkwood party size (#323)

Follow-up to #322 after the mechanics correction: **Steward** is the direct party-size driver
(`StewardPartySizeBonus` = +0.25 party size per point, `DefaultSkillEffects.cs:281` → `DefaultPartySizeLimitModel.cs:266`,
v1.4.6); Leadership feeds party size only via perks (and morale/garrison). All lords of the three elf cultures get
**+100 Steward** (= +25 party size each): rivendell avg 211→300, lothlórien 194→269, mirkwood 225→322. Everyone else
byte-unchanged on both skills.

- All `taom_elf_*` sets are elf-exclusive (verified) → pure **in-place** boost: 7 elf archetypes + 9 elf canonical
  sets (Galadriel 415, Elrond 400, Thranduil 360, Celeborn 350, Glorfindel 342, Legolas 338…), plus the
  template-less `lord_R3_1` whose engine-authoritative inline block went 200→300. No forks, no repointing.
- `tools/repoint_evil_lord_skillsets.py` generalized into the balance-pass parity tool: per-template
  Leadership+Steward parity map + `INLINE_OVERRIDES` for template-less lords.
- **Finding:** the 41 `Culture.battania` lords are **Khand Variags** (evil — `taom_spcultures` renames battania to
  Variag; the generator's `khand` entry owns it), but `rebalance_lords.CULTURE_MAP` still says battania→mirkwood, so
  the analyzer folds them into "mirkwood" (71 = 30 elves + 41 Variags) and the rebalance curve would hand them elven
  modifiers. Correctly excluded here; stale mapping left for a follow-up pass.
- Not covered: 5 elf lords on vanilla `spc_*_rookie` templates (M1_12, L1_3, R1_11, R2_11 + 1) — the pre-existing
  93-lord vanilla-template gap.
- **Save-compat:** new campaigns only (hero skills bake at creation).

### balance(lords): evil-faction lord Leadership nerf — Gundabad / Misty Orcs / Goblins / Dol Guldur / Dunland (#322)

Those five cultures' lords hosted armies big enough to crush Rivendell and Lothlórien; Leadership (the army-size
driver) is cut to per-archetype targets while **Mordor + Isengard keep the base orc sets**. Average lord Leadership:
gundabad 168→81, mistymountainorcs 164→76, goblin 160→74, dolguldur 169→81, dunland 174→84 — vs Rivendell 212 /
Lothlórien 214 (unchanged, as are mordor/isengard/mirkwood — bleed-checked).

- **New variant archetypes** (only Leadership differs from the parent): `north_orc_chieftain` 175 /
  `north_orc_warrior` 75 / `north_orc_female` 60; `dunland_knight` 90 / `dunland_lady` 80 /
  `dunland_young_lord` 55 / `dunland_young_lady` 55 / `dunland_marauder` 80 (forked from `dunland_raider`,
  whose 2 mirkwood users keep 180). In-place cuts on dunland-exclusive sets: `dunland_warrior` 200→100,
  `dunland_brenin` 265→130; Gundabad's canonical Bolgath (`lord_G4_1`) 280→185 (ruler stays above his chieftains).
- **`archetype_alias`** — new per-culture hook in `tools/apply_culture_skills_traits.py` so gundabad/dolguldur/dunland
  resolve shared archetypes to their variants on any future generator run (no un-nerf on regen).
- **`tools/repoint_evil_lord_skillsets.py`** (new, one-off, `--dry-run`/`--apply`) did the actual swap for all five
  cultures — 510 `skill_template` swaps + 544 inline-`<skills>` Leadership doc-parity updates across
  `characters/lords.xml` + `lords.xslt` — instead of the generator's `process_file`, whose per-NPC re-resolution
  can't reproduce the live hand-tuned assignments (the 1f7a7a9a 149-lord drift; goblin/mistymountainorcs have no
  CULTURES entry at all). Post-condition + idempotency verified (0 swaps on re-run).
- Verified: per-culture averages match predictions exactly; `validate_moduledata` PASS; zero dangling SkillSet refs;
  lords.xslt well-formed with all 396 template ids present in vanilla SandBox `lords.xml`.
- **Save-compat:** hero skills bake at hero creation — new campaigns only; existing saves keep old stats.

### chore(lord-skills): sync SkillSet generator to the hand-tuned live XML (1f7a7a9a maintenance debt)

`tools/apply_culture_skills_traits.py` had drifted from `taom_lord_skill_sets.xml` since the legendary-lord
hierarchy commit hand-edited the XML (its own CHANGELOG note flagged this: "update its canonical entries first
if regenerating"). A blind `--apply` would have reverted 14 hand-tuned canonical-lord sets and **deleted**
`taom_sauron_skills` / `taom_witch_king_skills` / `taom_canonical_lord_M1_1_skills` (Sauron #321 would have lost
his stats). Synced: the 14 canonical `skills=` dicts now carry the live values, Thranduil (`lord_M1_1`) gained his
explicit dict, and `sauron` + `witch_king` are BASE_ARCHETYPES entries. Acceptance: regen output == committed XML
semantically (123 sets, zero value drift); the only file change is deterministic id-sorting of the 3 hand-appended
sets. Generator is once again safe to re-run.

### feat(sauron): grounded Dark Lord + dedicated `sauron` race — towering, immortal, NPC-only (#321)

Sauron (`lord_1_17`) now fights on foot: the `Horse` (`charger`) + `HorseHarness` slots were removed from both
`sauron_bat_equipment` and `sauron_civ_equipment` (`taom_equipment_sets_mordor.xml`); `default_group="Infantry"`
was already set in `lords.xslt`, so the mount was the only thing putting him in the saddle. He also moves off the
shared `elf` race onto his own **`sauron` race** so height and per-race combat tuning can target him alone:
a verbatim elf clone (same `sk_elf_basemesh_a1_*` meshes, `human_skeleton`, 10 maturity/gender skins) appended at
the END of LOTRLOME_Armory `skins.xml` + 5 Monster entries in `monsters.xml` (live install AND
`docs/reference/lotrlome-armory-snapshot/`, `.bak-sauron` backups) — race ints are skins.xml merge-order indices,
so append-at-end preserves every existing race id. Only deltas from elf: **adult `min_scale` 1.07/1.06 → 1.40**
(movie-towering; child/teen/tween/toddler skins untouched) and the race id. No `as_sauron_*` action_sets — battles
use `Monster.ActionSetCode` = `as_human_warrior`; settlement/map suffixed lookups (`as_sauron_lord`/`_map`) resolve
via the engine's native silent fallback on missing action-set ids (the elf-proven path — `as_elf_map` fires for
every elf lord party icon today). Facegen sets are CC-only: the race is **NPC-only**
(no `cultures.json` `races[]` lists it, so Patch9's allow-list dropdown can never offer it). Aging: `immortal: true`
in `race_age_config.json` (verbatim saruman — the other Maia). CombatMechanics parity: `["sauron"]` mirrors
`["elf"]` (CtbAttackBonus 20, RemoveNonOverheadPenalty) in BOTH the compiled defaults and
`combat_mechanics_config.json` (the JSON dict REPLACES compiled defaults), so the race split doesn't silently drop
his modifiers — pinned by `GetConfig_MissingFile_SauronDefaultsMirrorElf`.

Deep-review (6 agents) caught one HIGH before commit: the engine's pregnancy check runs on the FEMALE only
(`PregnancyCampaignBehavior.DailyTickHero` gates on `hero.IsFemale`), so Sauron's immortal entry alone never
gated conception with Morgha (`lord_1_18`, race-unset → human, fertile) — `TaomPregnancyModel` now also returns
0 when the SPOUSE's race is immortal, making the "no future children" promise real for immortal fathers
(Sauron today; any wraith/Saruman pairing later). RCA: `docs/reviews/rca-sauron-race-2026-07-02.md`.

Combat tuning (user decision, resolves the review's deferred weight question): the `sauron` race joins every
offensive CombatMechanics capability + charge-knockdown resistance, in BOTH config surfaces (compiled defaults
+ JSON): `knockdownResistanceMultiplier` **3.0** (above the dwarf ceiling 2.5 — the 1.40-scale Dark Lord keeps
elf Monster weight 80, so this row is what stops horse-bowling), `swingEnergyBonusFactor` **0.20** (strongest;
orc 0.15), `monsterCrushMonsterIds` + `sauron` (swings auto-crush any non-shield block, troll tier),
`orcShieldCrushRaces` + `sauron` (crushes shield blocks too, energy/skill-gated — AI-only by that mechanic's
design, and Sauron is NPC-only anyway), `cleaveMonsterIds` + `sauron` (hits keep 30% momentum and slice through).
Pinned by `GetConfig_MissingFile_SauronOffenseAndKnockdownDefaults` + updated list-count tests.

Save-compat: new campaigns only (heroes snapshot race + equipment at campaign start; `RacePersistenceService`
restores the captured race on legacy saves) — existing saves keep the mounted elf-race Sauron by design.
In-game verification owed: full restart + new campaign (Armory XML loads at process launch).

### chore(review-infra): mechanize the CombatMechanics RCA preventions — NaN-gate + parallel-builder rules now load, not just sit in the RCA

The rca-combat-mechanics RCA promised three preventive actions; promises don't fire on the next feature, rules do.
All are now mechanized: **(1)** `.claude/rules/csharp-architecture.md` gained "Engine-Float Decision Gates: NaN Must
FAIL the Gate" — the runtime sibling of the config-float rule (4th NaN-gate instance proved the scope was one
category too narrow each time; inverted early-exits like `x <= 0f` pass NaN, gates must be positive requirements,
`bool?` services return null on non-finite input) — plus config-rule point 7: dual-surface JSON+MCM values enforce
the same invariants at both surfaces. **(2)** `/deep-review` Agent 5 gained rule 4b (engine-float NaN-polarity audit
on every gate) and the toggle-coverage rule 2b gained the master-toggle fold check (enumerate EVERY override incl.
constant getters when a hint promises "off = vanilla" — the `GetHorseChargePenetration` miss). **(3)**
`harness-facts.md` gained "Parallel builder briefs: shared sub-problems get ONE prescribed solution" (pre-dispatch
checklist; the CombatMechanics findings all lived at builder seams) + CLAUDE.md briefing item 6 pointing at it.
LESSONS-LEARNED entries for both rule classes were appended earlier the same session; the RCA's codify section now
records each action as DONE with file refs. Review log: REVIEW-LOG entry 69; AGENTS.md lessons updated to 69 reviews.

### feat(combat): CombatMechanics — crush-through, creature cleave/unstoppable, weight-based charge knockdown, shield penetration, race modifiers (#320)

Clean-room adaptation of five mechanics from a reference damage model (reference repo
commit `d8ded52`, GPLv3 — no code copied; constants/formulas recorded as facts in
an internal spec), plus two TAOM-original systems.
New `TaomCombatMechanicsModel` occupies the engine's single `AgentApplyDamageModel` slot by DERIVING from the
CareerSystem `TaomAgentApplyDamageModel` (now `abstract` — career damage passives ride via inheritance;
registration swapped at the one `AddModel<AgentApplyDamageModel>` site in `SubModule.cs`). Nine thin overrides
delegate to four pure services (`CrushThroughService`, `ChargeKnockdownService`, `CreatureCombatService`,
`ShieldPenetrationService`) + a shared `RaceCombatModifiersResolver` (lazy race-name validation — the registry
is engine state — with validate-before-lookup so invalid race ids get Neutral, never the "human" fallback row).

Mechanics: **skill-based crush-through-block** (exponential skill-gap curve over a 30-point dead zone, capped
50% at Δ200, energy-gated at 25 with a momentum ramp, off-angle ×0.5; the vanilla 58f overhead path is
untouched); **monster auto-CTB** (troll/mûmakil/elephant/spider swings crush any non-shield block); **AI-orc
shield-CTB** (orc-family races crush even shield blocks, energy/skill-gated, never the player); **creature
cleave** (troll/mûmakil hits keep 30% momentum AND force SlicedThrough past vanilla's chain-terminating
Bounced/Stuck branches — both overrides verified necessary from the 1.4.6 momentum wiring); **creature
stagger immunity** (per-monster damage thresholds; shrug-off also suppresses knockback/knockdown/dismount by
engine design); **weight-driven charge knockdown** (TAOM-original: `Monster.Weight` ratio × charge speed ×
per-race resistance — Branch A auto-floors at ratio ≥8 [mûmakil 9999 vs man 80 ≈ 125], Branch B scales the
vanilla `DecideCombatEffect` penetration by weight ratio around neutral 6.0 = Native horse+rider/human so
horse-vs-man stays ≈ vanilla, and keeps the 0.7-dot KnockBack gate; horses can't floor 160-weight trolls);
**shield penetration** (config item-id/weapon-class lists — default javelins — grant
CanPenetrateShield/MultiplePenetration after base, preserving the vanilla Javelin+Impale grant; runtime-flag
shield-damage ÷0.3 correction for the native underestimation, config-gated pending a 1.4.6 control-battle
re-verify); **per-race combat modifiers** (one JSON table: dwarf ctbDefense +15 / knockdown-resist 2.5× /
stagger 1.5×, elf ctbAttack +20 + no off-angle penalty, orc "Brute" swing-energy +15%, uruk_hai +10% + 1.25×;
"tree-spirits dig in" is a future data row, not code).

Config `combat_mechanics/combat_mechanics_config.json` (validated: FiniteFloatValidator before every range,
ordering invariants, unknown weapon-class/race-name entries skipped+warned, `ObjectCreationHandling.Replace`
so JSON lists replace compiled defaults; app-restart reload scope) + MCM "Combat Mechanics" (GroupOrder 24,
master + 8 mechanic toggles + 2 sliders; master off = exactly pre-feature behavior). 107 new tests
(decision-matrix boundaries: dead zone 30/31, energy-gate 25, damage==threshold, roll==chance; one test per
config validation rule; NaN-gate regressions on engine inputs; `CombatMechanicsModelInvariantsTests`
reflection-pins the derivation + abstract parent + exact override set under the BindingVerification harness).
Full suite 3862 green; API snapshot refreshed (44 GameModels) and `-Check`-reproducing. Engine facts verified
against installed 1.4.6 via ilspycmd (`DecideCrushedThrough`/knockdown/momentum call-site flow,
`Monster.Weight`, `RelativeSpeedLimitForCharge` float.MaxValue default, WeaponFlags bit values). 6-agent deep
review (standards/compat/efficiency/completeness/data-flow/spec-conformance): all 8 findings fixed in-session
— per-hit Substring normalization replaced with construction-time variant expansion, engine-input NaN gates
rewritten to positive polarity (4th instance of the NaN-gate class — new LESSONS-LEARNED rule),
`GetHorseChargePenetration` now folds the mechanic toggle, MCM slider floor aligned to the JSON ordering
invariant, enum-name cache for the missile/shield paths. RCA: `docs/reviews/rca-combat-mechanics-2026-07-02.md`.
Owed in-game: control battles (mûmakil charge, troll cleave, dwarf line vs cavalry, javelin-vs-shield
correction A/B).

Research: SandboxAgentApplyDamageModel, MissionCombatMechanicsHelper, Mission.ChargeDamageCallback/CreateMeleeBlow, Monster, AttackInformation (installed 1.4.6)
Save-compat: No SyncData, no save-format impact — pure GameModel + config.

### refactor(special-resources): unify the three earning-notification blocks

`SpecialResourcesBehavior` carried three near-identical resolve→guard→display blocks (`NotifyEarning`,
`NotifyEarningDelta`, and an inline copy in `OnMapEventEnded`). One `NotifyEarning(..., float? before = null)`
helper now covers all earning toasts (null = running-total display; non-null = positive-delta-only). Deliberate
display-only wording change, verified in the diff: the victory toast reads "+N X from victory" (was "+N X earned
from victory"), matching the other delta toasts. Round-4 micro-cleanup O1; display text only, no service logic
touched. Branch: `refactor/round4-micro-cleanups`.

### chore(research-infra): decompile dump refreshed to v1.4.6 — category tree no longer lags installed

The `E:\Decompiled_Bannerlord\` category browse tree (Campaign/, MountAndBlade/, …) was still the v1.4.5
decompile (manifest 2026-05-30) while the installed engine and the `_shipping_build`/`_editor_build`
dual decompile (regenerated 2026-06-12) were v1.4.6 — every research task carried a "dump is one version
behind" caveat. Preserved the v1.4.5 category tree + manifest to `_categories_v1.4.5\` (joining the
existing `_shipping_build_v1.4.5\` baseline), regenerated via `tools/decompile_to_folder.ps1 -Force`
(59 DLLs, 60s), verified the new manifest reads v1.4.6 and spot-checked `GetTownGoldChange` against the
installed-DLL formula. CLAUDE.md version caveats updated (4 sites); `taom-src` on installed DLLs remains
authoritative for signatures after any future bump.

### feat(settlement-economy): tunable town market-gold regeneration — towns no longer stay broke (#317)

User reports: town markets drain to 0 gold and never recover, so players can't sell loot. A 10-agent
investigation (formulas verified on installed 1.4.6) found no TAOM bug — an equilibrium mismatch: the engine
regenerates town gold daily toward `10000 + Prosperity×12` at 25% of the deficit (`GetTownGoldChange`, sole
caller `ItemConsumptionBehavior.UpdateTownGold`), but TAOM's drains run ~2× vanilla (LOTRLOME loot computes to
~2.2× vanilla item values via the engine's `2.75^tier` formula — #318; +22% villager deliveries at 2.78 avg
bound villages/town), so wartime loot dumps + deliveries pin towns at ~0. Refuted: garrison wages (clan
expense, never `Town.Gold`); CultureMarketplace injection (moves no gold). Fix: `TaomSettlementEconomyModel :
DefaultSettlementEconomyModel` (SettlementFood donor pattern — thin model → pure `SettlementEconomyService`
with banker's-rounding parity → validated `SettlementEconomyConfigProvider`) overriding ONLY
`GetTownGoldChange`, knobs in `settlement_economy/settlement_economy_config.json`, **shipped base 25000**
(slope 12 / rate 0.25 stay vanilla — base-heavy buffing gives collapsed towns 2.5× faster recovery while
median towns gain ~29%; adversarial review confirmed no runaway loop, drains are goods-bounded). Castles never
reach the override (`DailyTickTownEvent` iterates `Town.AllTowns` only). MCM "Settlement Economy" master
toggle (off = base passthrough = vanilla); applies to existing saves (~90% convergence in 8 days). 29 tests.
Data companions: `tools/analyze_settlement_prosperity.py` (read-only report; found 89 castles flat @600 + 31
towns flat @3500 generator defaults) + `tools/rebalance_settlement_prosperity.py` (lift-only vanilla
quantile-map, dry-run validated: 141 raised / 0 lowered; `--apply` deferred to user — edits the live TAOM_Map
module). Follow-ups filed: #318 (LOTRLOME value rebaseline), #319 (CultureMarketplace filter defeats the
price-crash anti-farming guard; its stale "60-item cap" doc line corrected to 200). New engine-reference
section "Town gold — the market wallet" in `docs/reference/engine/settlement-economy-food-prosperity.md`;
feature doc `docs/features/settlement-economy.md`.

### fix(hero-race): uruk saves preview true-to-race on the Load Game screen (per-race allow-list)

User report: a new uruk (Mordor) campaign previewed as a bald human on the save list, though CC and in-game
rendered correctly. Root cause was TAOM's own `Patch55_BasicTableauRaceGuard` (2026-06-24, #299): it coerced
**every** custom race to human in the `BasicCharacterTableau` agentless native build because a **dwarf** head had
proven the morph-data AV (#295) — no other race was ever tested. An instrumented pass-through build showed the
native build renders **uruk fine** (all uruk skins ride `human_skeleton` with `sk_uruk_basemesh_a_*` meshes), so
the wholesale coercion was too broad for it. `BasicTableauRaceGuard` refactored from a hardcoded int set (`{0}`)
to a name-based `TableauSafeRaceNames` (uruk verified 2026-07-02) resolved per call via `IRaceManager` — ids are
skins.xml merge-order indices and shift with the module set; validate-before-lookup so an invalid id coerces
instead of riding the `GetRaceNameFromId` "human" fallback; any resolution throw fails safe to the human base
(worst case a human thumbnail, never a CTD). Cold-menu name resolution verified safe: `FaceGen.CreateInstance()`
runs from the engine's native `OnLoadCommonFinished` before the initial screen. Dwarf and all unverified races
stay coerced; the per-race verification recipe is documented in `docs/features/hero-race.md`. 9 guard tests
(safe-race pass-through, casing, dwarf/elf coercion, invalid-id + fallback-trap pins, throw fail-safe) + a
`Patch55` binding drift-guard pinning `BasicCharacterTableau._race` as `int` against the installed engine
(the `____race` field injection isn't covered by the generic `HarmonyPatchBindingTests` target resolution).
Reviews: 5-agent deep review 0 findings; Codex adversarial review CLEAN (0 P1/P2/P3 — all 6 Known Suspects
disputed with decompiled evidence; cross-session race-index drift classified vanilla-equivalent residual).
Review 67 in `docs/reviews/REVIEW-LOG.md`; issue #316; commit `4697ada5` + review-artifacts follow-up.

### balance(party-templates): stack maxes raised to 50 — bandit + kingdom hero parties (#315)

Map bandit parties averaged 20-25. Spawn size is `min + (max-min) × ratio` per template stack, the bandit ratio
averages ~0.2 early game, and `Patch39_BanditPartySize` caps its scaling at each stack's `max_value` — so the
template max is the binding lever. Per user direction (literal per-stack reading, chosen over a total-≈50
scaling with consequences stated): `max_value="50"` on every stack of the 8 bandit cultures' raider + boss
templates and all 221 `kingdom_hero_party_*` templates (2,607 stacks). The 1/1 hideout-boss hero stacks stay
1/1 (one boss is load-bearing for the boss conversation); `min_value` untouched; looters stay vanilla. Applied
via the new idempotent `tools/raise_party_template_maxes.py` (`--dry-run`/`--apply`, CRLF/BOM-preserving).
Expected: bandit parties ~30-75 early game, up to ~200 endgame. Accepted trade-offs: lord spawns can exceed the
party-size limit (engine adds the templated roster verbatim — no clamp; over-limit lords can't recruit and pay
big wages until attrition) and mercenary/outlaw templates lose their fixed min=max compositions. Value-only
change — save-compatible; full game restart required to load the new values; already-spawned parties keep their
size.

### fix(starting-equipment): non-Gondor characters naked after career until a full game restart (+ prevention)

The 2026-06-30 starter-armor change authored 12 new `LOTRLOME_items/<culture>/starter_armors.xml` files. On first
play every **non-Gondor** character was naked after selecting a career (Gondor fine). Not a data defect: Bannerlord
loads managed item XML in two one-shot phases — it **registers** each `<XmlName id="Items"
path="LOTRLOME_items/<culture>">` *directory* at process launch (`Module.cs:246→1032`) and **globs** it
(`DirectoryInfo.GetFiles("*.xml")`) at campaign start (`Campaign.cs:1471 LoadXML("Items")` →
`MBObjectManager.cs:894/900/901/903`), with no hot-reload. A file created **after** launch is invisible until a
full restart; Gondor's `starter_armors.xml` pre-existed the last launch, which is why only it was clothed. A full
restart loads all 12 files (user-confirmed) — no data change needed. Mechanism decompile-verified and
adversarially checked (workflow `naked-regression-prevention`).

Prevention (the reason `validate_moduledata` PASS + green build + green tests didn't catch it — none start a
campaign or instantiate `MBObjectManager`): documented the new-file/restart blind spot in
`.claude/rules/moduledata-validation.md` (auto-loads on ModuleData edits) and
`docs/features/starting-equipment-tuning.md`; added an RCA lesson to `docs/reviews/LESSONS-LEARNED.md`; and both
`tools/generate_starter_armor.py` and `tools/wire_career_starter_armor.py` now print a RESTART-REQUIRED +
verify-in-game reminder after `--apply`.

### fix(battle-balance): new-campaign CTD — throwing `PartyBase.Owner` getter banned assembly-wide (crash 0b462fd8)

Every v2.0.8.0 campaign crashed within its first in-game day: the engine's settlement daily tick feeds every
`settlement.Party` into `TaomPartyHealingModel.GetDailyHealingHpForHeroes` (added in the 2026-06-26 career
pip-bonus wiring, `9034e5dc`), which resolved the career-passive hero via `party?.Owner`. `PartyBase.get_Owner`
throws for a settlement party whose `OwnerClan` is null — `Settlement.Owner => OwnerClan.Leader`, unguarded —
and TAOM_Map's `retirement_retreat` (the lone `CustomSettlementComponent` settlement among 988) is exactly that.
A `?.` on the result cannot guard a getter that throws internally (`adapters.md`, the #281 family — this is the
third shipping instance of the class, and the #281 fix itself had planted `party.Owner?.Culture` inside the
"null-safe" `ResolvePartyCulture` chokepoint).

- New `CareerPassiveHero.ResolveId` (`Main/Features/CareerSystem/`): `(party?.MobileParty?.Owner ??
  party?.LeaderHero)?.StringId` — `MobileParty.Owner` (`=> _partyComponent?.PartyOwner`) is the safe owner
  accessor; owner-first order preserved so player-owned caravans/garrisons led by non-career companions still
  resolve to the player. All 6 career-passive call sites route through it (PartyHealing ×2, PartySize,
  PartyTroopUpgrade, BattleReward, Raid); `ResolvePartyCulture`'s owner limb swaps to
  `party.MobileParty?.Owner?.Culture`.
- **Prevention:** `PartyOwnerGetterBanTests` walks the raw IL of every method body in `TAOM.dll` (incl. generic
  definitions and compiler-generated types) and bans `PartyBase.get_Owner` outright. RED at 7 violations
  pre-fix — it found a 7th site (`TaomRaidModel`, `attackerSide?.LeaderParty?.Owner`) that text grep missed —
  GREEN post-fix.
- Intended behavior deltas (deep-review-verified negligible): settlement parties no longer resolve a
  career-passive hero (passives are player-hero-exclusive; `settlement.Party` rosters hold no combat members),
  and settlement-party culture feats fall to the `Settlement.Culture` field (vanilla `HasFeat`'s own final limb).
- 5-agent deep review: standards/compat/efficiency/completeness/data-flow all PASS; installed-1.4.6 verification
  of all 10 `PartyComponent.PartyOwner` overrides confirms the replacement chain cannot throw for any
  validly-constructed party. RCA: `docs/reviews/rca-party-owner-getter-nre-2026-07-02.md`.
## 2026-07-01

### chore(hooks): remove CLAUDE.md from config-protection's blocked list (user decision)

`config-protection.sh` no longer blocks Edit/Write to CLAUDE.md — explicit user decision (2026-07-02, solo
developer): the agent maintains CLAUDE.md as living documentation and the block forced a manual approval on
every routine doc correction (e.g. the #305 rename remainder). The hook itself stays: Directory.Build.props,
settings.json/settings.local.json, and ADRs remain protected — those gates guard against the agent weakening
build config, permissions, and architecture decisions rather than against collaborators. CLAUDE.md's Hooks
table updated to match.

### docs(claude-md): update the Elephant/Mûmakil + VolunteerRecruitment Key Paths rows for the 2026-07-01 refactors

USER-AUTHORIZED CLAUDE.md edit (config-protection deliberately bypassed by explicit instruction, hook untouched):
the War Elephant and Mûmakil rows now describe the shared `Main/Features/ElephantLike/` BT nodes bound via
`ElephantCombat.Profile`/`MumakilCombat.Profile` and the thin service bindings (#305) instead of the deleted
`BehaviorTreeElements/` folders and `ElephantAttackActions`; the VolunteerRecruitment row points at the
`RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs` partial split (#308). Closes the "known remainder"
from `rca-refactor-stack-2026-07-01.md`.

### refactor(hero-race): extract RaceTableauPositioning from CharacterTableauService (4x duplicated, untested)

The per-race tableau frame-offset block was duplicated FOUR times inside `CharacterTableauService`
(character + mount frames in both refresh paths) with zero tests, and its axis mapping is deliberately
unintuitive (config `Horizontal`→`origin.y`, `Vertical`→`origin.z`, `Zoom`→`origin.x` — camera-relative naming
from the donor CharacterAvatarPatch config). The offset math + the case-insensitive config-lookup builder now
live in pure `RaceTableauPositioning` with 8 tests pinning the axis mapping, null-item passthrough,
struct-copy non-mutation, and lookup semantics (case-insensitive, skip-empty, last-wins). Service behavior
byte-identical. Round-3 target R5 (the survey's other two untested-pure-logic claims —
PlainTextCrashReportRenderer + ShaderPrecompileRunner — were FALSE: both already have test files; caught by
inline vet after the survey's vet agents were rate-limited). Branch: `refactor/round2-cleanups`.

### fix(elephant)+review(round2): deliver the promised mission-end telemetry + round-2 RCA

Round-2 deep review (5 dimensions + adversarial verification): behavior preservation, efficiency, and wiring
parity all clean; one confirmed finding — R4's claimed "elephant gains late-attach telemetry" was only
half-delivered (mid-mission first-late log wired, mission-end summary missing). Fixed:
`ElephantMissionBehavior.OnRemoveBehavior` now emits the Spider/Mûmakil-parity summary and clears the error
dedup. RCA: `docs/reviews/rca-round2-cleanups-2026-07-01.md`; new LESSONS-LEARNED rule — a commit message's
claimed deltas are part of the diff, verify each before committing. Issues #309-#312 cover round 2.

### refactor(advanced-combat): extract shared CreatureTreeTracker from the three cloned creature MissionBehaviors

Spider/Elephant/Mûmakil MissionBehaviors each carried an identical ~40-line attach/prune block (shadow component
list, dedup TryAttach keyed on the Monster predicate, first-tick scan, late-spawn attach, dead-agent pruning) —
and the copies had already drifted: Spider/Mûmakil gained late-attach telemetry the elephant copy never did. The
bookkeeping now lives once in `Main/Features/AdvancedCombat/CreatureTreeTracker.cs`; the three behaviors keep
their own log tags, Armory-drift guards, and feature-specific work (howdah, summaries). Two deliberate log-only
deltas: the elephant gains the late-attach counter + first-late log (drift repair), and all three share the
tracker's build-failed message shape. The warg keeps its own wiring (different predicate mechanism + wording +
extra infra — forcing it in fails the simplicity bar). Boundary code, game-tested per ADR-008. Round-2 target R4.
Branch: `refactor/round2-cleanups`.

### test(behavior-trees): characterization tests for the inlined BT builder (zero coverage before)

The vendored-then-inlined `BehaviorTrees` builder (`Main/BehaviorTrees/BehaviorTreesCore.cs`) is load-bearing for
all four creature features but had no tests. 9 new tests pin the semantics the creature trees depend on: the
blackboard reflection-copy shares the tree's `BTBlackboardValue` INSTANCES with nodes and decorators at Add* time
(so trees must initialize blackboard values in their ctor — a post-build reassignment does not propagate), a node
whose blackboard interface the tree lacks fails the build with `MissingTreeBlackBoardException`, get-only
blackboard properties fail with `IncorrectPropertyException`, non-blackboard interfaces are ignored, `Up()` past
the root throws, and a trivial tree executes its task exactly once per `RunTree`. Round-2 target R3. Branch:
`refactor/round2-cleanups`.

### refactor(core-validation): consolidate copy-pasted SafeClamp helpers into TAOM.Core.Validation.SettingClamp

The byte-identical private `SafeClamp` (float, NaN-guarded) / `SafeClampInt` helpers copy-pasted across the
SmartCavalryAI, BanditManagement, CultureConversion, and CastleRecruitment settings providers now live once as
`SettingClamp.Clamp` overloads beside `FiniteFloatValidator`, with 15 new tests pinning the exact semantics —
including the asymmetry the consolidation surfaced: a NULL setting takes the default and flows through the range
clamp, while a NaN/Infinity setting returns the compiled default verbatim (early return). Providers keep their
per-knob ranges; only the mechanism is shared. Round-2 target R2. Branch: `refactor/round2-cleanups`.

### refactor(companion-tactics): delete four orphaned BattleActionBar action enums

`ShieldAction`, `PolearmAction`, `CavalryAction`, `RangedAction` (Main/Features/CompanionTactics/BattleActionBar/Models/)
had zero references outside their own definition files — repo-wide grep across C#, XML, and prefabs; the single
`RangedAction` hit was a substring in a test-method NAME, not a type usage. Deletion holding parity (3688 tests
green). Round-2 target R1 from the vetted duplication/dead-code survey. Branch: `refactor/round2-cleanups`.

### review(refactor-stack): 6-dimension deep review — code clean, 2 stale-doc findings fixed + prevention installed

`/deep-review` of the 4-branch refactor stack (#305-#308) via a 6-agent workflow (standards, installed-DLL API
compat, efficiency, completeness, data flow, behavior-preservation diff audit) + adversarial verification: **zero
code findings** — registration parity, Harmony wiring, ctor argument order, engine signatures, hot-path caching
all held. Two confirmed findings, both stale docs: `docs/features/elephant.md` + `mumakil.md` still pointed at the
deleted `BehaviorTreeElements/` folders and pre-unification type names — fixed. RCA:
`docs/reviews/rca-refactor-stack-2026-07-01.md`; durable prevention: LESSONS-LEARNED "structural refactor sweeps
must cover living docs" + a mandatory Documentation-sweep step 6 in the refactoring-specialist agent. Known
remainder: CLAUDE.md lines 372-373 carry the same stale names — edit blocked by config-protection (needs user
approval). GitHub issues #305/#306/#307/#308 filed for the four refactors.

### refactor(troop-progression): split VolunteerRecruitmentService per-culture pools into partial-class files

The 994-line service is now a 264-line core (maps, JSON loader, conditional→settlement→clan→culture cascade,
weighted pick, test helpers) plus 15 per-culture partial-class files under
`Main/Features/TroopProgression/RecruitmentPools/` — each culture's pools and their design-rationale comments
(Codex findings, user specs) live together, moved verbatim; the static ctor is unchanged. **Deliberate deviation
from plan T5's JSON migration:** the existing Gondor pattern is JSON-override-with-hand-written-fallback, so
extending it to 14 more cultures would have created a dual source of truth per culture and stranded the
rationale comments (JSON has none) while the 2,698-line test suite pins the hand-written maps — rejected per
`simplicity-criterion.md`; the split delivers the modularity goal with zero functional change. 3688 tests
green. Branch: `refactor/recruitment-pool-split` (plan T5, restructured).

### refactor(faction-map): extract PolygonWidget hit-test math to unit-tested AlphaHitMap + PolygonPointParser

`PolygonWidget` (1,140 lines, previously zero unit coverage) now delegates its pixel-accurate hit testing to
`AlphaHitMap` (downsampled max-alpha build + normalized opaque lookup, the off-by-one-prone index math; the
DS=4 constant was duplicated in builder and lookup and is now single-sourced) and its `Points` parsing to
`PolygonPointParser` — both TaleWorlds-free, 19 new tests, TDD (RED confirmed before implementing). One
deliberate fix: `PointsToString` formatted with the CURRENT culture while parsing was invariant, breaking
round-trips on comma-decimal locales; formatting is now invariant both ways. Plan-scope deviation: no
point-in-polygon code exists (hit-testing was always alpha-map only) and the hover tween is 4 lines — the
planned `AnimatedFloat` extraction was rejected per the simplicity criterion. Build + 3688 tests green.
Branch: `refactor/polygon-widget-math` (plan T4).

### refactor(submodule): extract the private-target manual-patch block to ManualPatchApplicator

The ~66-line run of AccessTools-resolved `_harmony.Patch(...)` calls for PRIVATE engine methods
(SettlementGuards ×2, BannerColor MobilePartyVisual/AgentVisuals/MapConversationTableau ×2, CompanionTactics
captain tooltip) moves verbatim from `OnGameInitializationFinished` to `Main/ManualPatchApplicator.ApplyAll`,
apply order + fail-safe warnings unchanged. `SettlementGuardsWiringTests` re-pinned to the new location plus a
new assert that SubModule still invokes `ApplyAll`. SubModule.cs is down to ~930 lines from 944 pre-T2. Build +
3669 tests green. Branch: `refactor/submodule-slim` (plan T3).

### refactor(submodule): extract OnGameStart registration block into ordered registration methods (ADR-002)

OnGameStart carried ~250 inline lines of behavior/model registration. The block now lives in seven private
static registration methods (`RegisterProgressionAndIdentity` → `RegisterCampaignLifeBehaviors`) invoked in the
original statement order from a slim coordinator that hoists the shared `careerPassives`/`culturalFeats`
resolves. Pure mechanical move — every AddBehavior/AddModel/RemoveBehaviors/SuppressAll call is verbatim and
order-preserved (script-verified token counts). Build + 3668 tests green. Branch: `refactor/submodule-slim`
(plan T2).

### refactor(elephant-like): unify Elephant + Mumakil duplicated attack code into a shared ElephantLike layer

The Mûmakil's attack service, BT task base, cooldown/engage decorators, blackboard interface, and action caches
were byte-identical clones of the war elephant's (only type names + config constants differed — verified by
name-substituted diff). Both features now bind a shared `Main/Features/ElephantLike/` layer: a pure
`ElephantLikeAttackService` base (ctor-bound tuning) behind per-creature marker interfaces (IoC registration +
`TaomAgentStatCalculateModel` injection unchanged), and shared BT nodes parameterized by an
`ElephantLikeCombatProfile` (scan ranges, blow magnitude, clip caches, lazy service resolver).
`IsElephantMonster`/`IsMumakilMonster` collapse to `IsCreatureMonster`. Zero behavior change — 3668 tests green
before and after, net −125 LOC. Branch: `refactor/elephant-mumakil-unify` (plan T1 of the 2026-07-01 refactor
target audit).

### diag(troop-weight): add temporary troop-count diagnostic for special-currency undercount report

A player reported 30 troops (10 special-currency) showing as 20 on the campaign-map nameplate + party-size
counter. Static analysis ruled out every TAOM mechanism as a cause of an *undercount*: Patch17 TroopWeight is
increase-only, a missing weight entry defaults to 1.0, the display hooks walk the full roster, and no roster-add
path bypasses `AddToCounts`. The symptom needs runtime roster state (wounded vs. troops living outside the main
party vs. a stale cached count) to resolve, so this ships an instrumented build to capture it.

- **`TroopCountDiagnosticsBehavior`** (`Main/Features/TroopWeight/Diagnostics/`) — on party-screen open, logs the
  main party's raw + weighted counts (per-slot bodies, wounded, resolved weight, special-currency flag,
  `EnableTroopWeight`) under a `[TroopCountDiag]` prefix, plus a scan of where the player's special-currency
  troops live across clan war parties + garrisons. Runs regardless of the Troop Weight setting; whole path is
  try/catch'd.
- Pure, unit-tested `TroopCountDiagnosticsFormatter` (6 tests) owns the line formatting incl. a slot-bodies vs.
  `TotalManCount` MISMATCH detector for the stale-count hypothesis.
- **Temporary** — registered in `TroopWeightIoC` + `SubModule`; both the behavior and its registrations are to be
  removed once the log pins the root cause and the real fix lands.

