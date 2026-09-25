# Deep review: plan 025, delete two unreachable scaffolds (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 025, IEditorSceneAdapter and the EditorCacheRebuild Caching/ scaffold deleted, with
         their two reserved config fields, 26 tests and one binding row
         (branch improve/025-delete-unreachable-scaffolds, 1091f3b6..032481cc)
Date: 2026-09-24

Scope:   C# (Main/Adapters, Main/Features/EditorCacheRebuild), tests, docs, CHANGELOG.
         No XML/XSLT, no scripts, no harness files.
Waves:   Wave 1: lenses 1 to 6. Lens 7 (XML) and tooling: NOT IN SCOPE.
         Codex adversarial (gpt-6-astra, ultra): complete ("END OF CODEX REVIEW" present).
         Review lead (this report): verification, fixes, Step 4, RCA.

STANDARDS:     PASS on all ten checks; 3 LOW (1 simplicity, 2 doc), all fixed
COMPATIBILITY: PASS, 49 verified, 0 incompatible, 0 unverified in code; 2 wrong engine
               claims in text (fixed), 1 unverified claim (dropped)
EFFICIENCY:    PASS, 0 H, 0 M, 2 L (1 applied, 1 follow-up)
COMPLETENESS:  INCOMPLETE: no GitHub issue (needs Mike); doc gaps fixed
DATA FLOW:     PASS, 16 flows, 0 gaps, 3 inconsistencies (all fixed)
DESIGN:        4 KEEP proposals (3 apply, 1 follow-up): 3 applied
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## DETAILS

Every finding below was re-read against the worktree before it was classified: the source at
`032481cc`, the base with `git show 1091f3b6:<path>`, history with `git log -S` and `git log`, and
engine claims against the v1.5.3 dump `E:\Decompiled_Bannerlord\_categories_v1.5.3` (Agent 2 and
Codex had already read the installed DLLs). Finding ids F1 to F7 are the RCA's,
`docs/reviews/rca-delete-unreachable-scaffolds-2026-09-24.md`.

### Agent 1: Standards

| Finding | Verdict | Action |
|---|---|---|
| F1 permanent absence test fails the simplicity criterion | CONFIRMED (RCA F6) | Applied as the Agent 6 P1 proposal: test deleted |
| F2 `editor-cache-rebuild.md:119` count 20, class now has 22 | CONFIRMED (F3): `grep -c` gives 22 | Per-bullet counts dropped |
| F3 "That alone" at `:181` points at the deletion | CONFIRMED (F1) | Paragraph rewritten |
| Follow-ups (fallback, citation, stale lines, issue, six fields, CHANGELOG merge) | See FOLLOW-UP and NEEDS MIKE | The `:65-80` citation verified exact; left alone |

### Agent 2: Engine compatibility

| Finding | Verdict | Action |
|---|---|---|
| F1 memoization premise wrong for v1.5.3; "2-3x" unmeasured | CONFIRMED (F1). Dump: `SandBoxNavigationCache.GetRealDistanceAndLandRatioBetweenSettlements` builds a local `NavigationPath` with multiplier `1f` (:87-88); `CheckBeingNeighbor` uses `2f` (:134); `NavigationCache.CheckBeingNeighbor` runs `CheckNeighbourAux` both ways (:398-399). Adapter: `NavigationCacheAdapter.cs:268-273` reads only the float and `landRatio` | Line 181 and the changelog bullet at `:207` rewritten; CHANGELOG recovery sentence reworded; 2-3x dropped |
| F2 `reflection-sites.md:81` wrong commit and wrong category | CONFIRMED (F2): `git log -S"TaleWorlds.Engine.PathReuseCache"` gives `41258657` | Line rewritten (`41258657`, Category D) |
| 47 rows verified by full name; removed row never engine | Verified by the lens; not re-run | None needed |
| Follow-ups (fallback, catalogue vs DataRow mismatch) | Pre-existing | FOLLOW-UP |

### Agent 3: Efficiency

| Finding | Verdict | Action |
|---|---|---|
| 1 performance claim at `:181` points at the wrong thing | CONFIRMED (F1), APPLY | Applied with the Agent 2 wording (stronger than the proposed move, which kept a wrong premise) |
| 2 heading "~30 min" vs the table's ~7 min | CONFIRMED, pre-existing | FOLLOW-UP (heading kept) |
| No runtime cost; no hot path | Agreed | None |

### Agent 4: Completeness

| Finding | Verdict | Action |
|---|---|---|
| GitHub issue missing | CONFIRMED | NEEDS MIKE (`/issue` is public) |
| 1 count at `:119` | CONFIRMED (F3) | Fixed |
| 2 `NavigationPath` dependency at `:113` | CONFIRMED (F4): `git grep -w NavigationPath -- Main` hits only `SupplyCaravanService.cs:854` | Removed |
| 3 "That alone" | CONFIRMED (F1) | Fixed |
| 4 `reflection-sites.md:81` | CONFIRMED (F2) | Fixed |
| 5 absence test (design) | CONFIRMED (F6) | Applied (plan allows deletion on review) |
| a FOR-MIKE decision 21 premise | CONFIRMED: `git grep -lw` finds `EnableDebugQualityCheck`, `EnableUiOverlay`, `Phase1SkipReversePathfind` only in `CacheRebuildConfig.cs`; `CheckpointEvery`, `IncrementalSpatialRadius`, `LogVerbosity` also only in `CacheRebuildConfigProvider.cs` (range checks). None has a runtime reader | NEEDS MIKE; FOR-MIKE.md is in `E:\repos\TAOM`, not edited |
| b-f | Pre-existing | FOLLOW-UP |

### Agent 5: Data flow

| Finding | Verdict | Action |
|---|---|---|
| 11 count at `:119` (and `:122` 8 vs 6) | CONFIRMED (F3): SmokeTestGateTests has 6 | Counts dropped |
| 12 `NavigationPath` dependency | CONFIRMED (F4) | Fixed |
| 13 recovery pointer at `reflection-sites.md:81` | CONFIRMED (F2) | Fixed |
| N1 "That" | CONFIRMED (F1) | Fixed |
| N2 absence test | CONFIRMED (F6) | Applied |
| N3 "never in the shipped JSON" | CONFIRMED (F5): `git show 6a80bac6:Main/_Module/ModuleData/configs/cache_rebuild_config.json` lines 6-7 carry both keys; `b5cb3018` removed them the same day; every tag containing `6a80bac6` also contains `b5cb3018` | CHANGELOG reworded |
| Follow-ups F1-F4 | Pre-existing | FOLLOW-UP |

### Agent 6: Design and elegance

| Proposal | Verdict | Action |
|---|---|---|
| P1 delete the absence test (PRESERVING, APPLY) | Re-verified | Applied |
| P2 reorder line 181 (PRESERVING, APPLY) | Re-verified; superseded by the Agent 2 rewrite | Applied as rewrite |
| P3 drop per-bullet test counts (PRESERVING, APPLY) | Re-verified | Applied to all eight bullets (two were wrong; none is computed) |
| P4 restrict the binding fallback to engine assemblies (CHANGING, FOLLOW-UP) | Code re-read (`ReflectionSiteBindingTests.cs:126-146`) | Not applied: pre-existing, behaviour-changing; FOLLOW-UP |
| CHANGELOG `:15` JSON claim | CONFIRMED (F5) | Fixed |

## ACTION ITEMS

1. Mike: file (or approve filing) a GitHub issue for plan 025 and cite `032481cc`.
2. Mike: decision 21 scope. None of the six other reserved `CacheRebuildConfig` fields has a
   runtime reader; FOR-MIKE.md says they "have readers and stay".
3. Orchestrator: one convergence `deep-reviewer` pass on the fix diff (Step 4.6); the review lead
   cannot spawn agents.
4. Follow-up: the binding gate's simple-name fallback (plan 008's area).

## IMPROVEMENTS (Step 4)

APPLIED:
- `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs:128-150`: absence
  test deleted (Agent 6 P1, Agents 1, 4, 5). Proof: `--filter FullyQualifiedName~EditorCacheRebuild`
  92 passed before; full suite after 10287 passed, 2 skipped, 0 failed (was 10288).
- `docs/features/editor-cache-rebuild.md:181` and `:207`: rewritten from the v1.5.3 code (Agent 6
  P2, Agent 3 item 1, Agent 2 F1). Doc only; `python tools/lint_docs.py` exit 0, 0 dashes in new
  prose.
- `docs/features/editor-cache-rebuild.md:119-126`: per-bullet test counts dropped; the config bullet
  names the retired-key test (Agent 6 P3). Doc only.

NOT APPLIED:
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:136-144` engine-only fallback (Agent 6 P4):
  pre-existing code and behaviour-CHANGING for the gate; needs Mike, natural home plan 008.
- `docs/features/editor-cache-rebuild.md:181` heading "~30 min" vs the ~7 min table (Agent 3
  item 2): pre-existing text.

FOLLOW-UP (no issue filed: filing is public and needs Mike):
- Binding gate fallback accepts TAOM types (Agents 1, 2, 4, 5, 6); lesson in
  `lessons/adapters-taleworlds-api.md`.
- Catalogue vs DataRow mismatch: `Mission._initialPlayerAgent` has no catalogue row; three
  `PlayerSwitcherBindingTests` members have no DataRow (Agents 2, 5); `taleworlds-api-snapshot/README.md:43`
  "32 auxiliary".
- `editor-cache-rebuild.md:215-216` says #118 is Open; `:23` claims a `ThreadLocal<NavigationPath>`
  pattern that no code uses (Agents 4, 5).
- Live `.ckpt.bin` checkpoint classes have no tests (`CheckpointSerializer`, `SettlementSnapshotStore`,
  `ProgressLogger`, `CacheElementKey`; Agent 4 b).
- Merge notes: improve/010 edits three deleted test files (modify/delete: keep the deletion);
  CHANGELOG top conflicts with sibling branches; use one `## 2026-09-24` heading.

VERDICT: READY FOR COMMIT (runtime unchanged; final full suite 10287 passed, 2 skipped, 0 failed;
convergence pass owed by the orchestrator; issue and decision 21 scope need Mike)

## CODEX REVIEW

Codex adversarial review, gpt-6-astra at ultra, raw output
`docs/reviews/raw/codex-adversarial-025-delete-unreachable-scaffolds-2026-09-24.md` (gitignored),
prompt `docs/reviews/codex-adversarial-025-delete-unreachable-scaffolds-2026-09-24.prompt.md`.
Complete: the final message ends "END OF CODEX REVIEW". Quality: vanilla excerpts from the installed
v1.5.3 DLLs, a config cross-reference table, all ten known suspects answered. No P1 or P2.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 (plan) | P3 (plan) | Yes | `plans/025-delete-unreachable-scaffolds.md:609` prescribes `TaleWorlds.Engine.PathReuseCache` in the status line; the done check at `:762` greps the file for that text. Branch wording avoids it. Recorded as RCA F7; plan not edited (executed, outside the change) |
| S1-S10 | Disputed or UNVERIFIED | Agree | Yes | S4, S6: the RED log (`scratch/025/red.log`, per Agents 1, 4, 5) and the binding run are covered by this lead's full run: 10287 passed, 0 failed, the only skips the two `WargAttack_*` tests, so no binding row went Inconclusive. S5: the build compiled in that run |

- **Confirmed bugs:** none in code. The one P3 is plan text.
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** F1 (its section 1 read line 181 as support and did not test the
  memoization premise, though its own excerpt shows the `2f` multiplier), F2 (recovery commit),
  F3 and F4 (stale doc facts), F5 (JSON history), F6 (simplicity of the absence test; it judged
  only whether the test can fail).

### Root cause table (Codex-confirmed bugs)

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Plan's prescribed status sentence contradicts its own done check | Other: plan self-contradiction | Plan checks written apart from the prose they check | Lesson in `lessons/misc.md` (plan prose is a draft) |

### AGENTS.md lessons (pending, consolidated later for all branches)

- "Bugs Codex typically misses": prose in a change's docs that restates an engine premise (here,
  Phase 1 paths reusable in Phase 2) is read as context, not tested, even when Codex's own excerpt
  contradicts it; and history claims ("never shipped", "recover from commit X") are not checked
  with `git log`.
- "What Codex does well": cross-checking a plan's done criteria against the prose it prescribes.

## Convergence

A convergence pass over the review-fix diff `032481cc..76f22e11` found no runtime or standards
defect (the only C# change removes one test) and two LOW text defects, both confirmed and fixed.

| # | Finding | Verdict | Fix |
|---|---|---|---|
| C1 | `lessons/misc.md:205` said the config-test count went "(20, now 22)"; the same commit removed one of those tests, so HEAD has 21 (`grep -c "\[TestMethod\]"` on `CacheRebuildConfigProviderTests.cs` gives 21, no `[DataRow]`) | CONFIRMED | Reworded to "(20, then 22 at `032481cc`)" |
| C2 | `CHANGELOG.md:25-27` credited the `41258657` provenance to both the feature doc and the binding catalogue; `grep -c 41258657` gives 0 in `editor-cache-rebuild.md` and 1 in `reflection-sites.md`, and the other two changes are in the feature doc only | CONFIRMED | Sentence split: the catalogue names `41258657`; the feature doc drops `NavigationPath` and the counts and explains the Phase 1 premise |

- **False positives:** none.
- **Left UNVERIFIED:** the FOR-MIKE.md "have readers and stay" wording lives in the main
  checkout's uncommitted copy, which this worktree pass does not read. The substance (no runtime
  reader of the six reserved fields) was confirmed by the convergence reviewer with `git grep -lw`.
- **Not changed:** the RCA's F3 row ("the change made it 22") and this report's Agent 1 row
  ("class now has 22") describe `032481cc`, the commit they reviewed, so they stay as written.
- **Tests:** doc-only fixes, nothing to test first. Full suite at the fix:
  `Failed: 0, Passed: 10287, Skipped: 2, Total: 10289` (the skips are the two `WargAttack_*`
  tests; the branch is based after `a39a9c86`, so no live-Armory failure is expected).
