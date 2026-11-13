# Deep review: plan 051, tools dead scripts and test speed (2026-11-13)

```
DEEP REVIEW REPORT
===================
Feature: plan 051, "Tools: delete three dead scripts, correct the catalog's environment section,
         and cut repeated parsing from two tool tests" (Refs #1132)
Branch:  improve/051-tools-dead-scripts-and-test-speed, range 664f19c5b..ee81570a3
Date:    2026-11-13

Scope:   Python tools (translate_with_claude.py, check_generator_item_refs.py,
         validate_all_troop_refs.py docstring), two tool test modules, three deleted scripts,
         tools/README.md and six docs. No C#, XML, hook, harness or .ai file.
Blast radius: no C# type changed, so graphify affected is NOT IN SCOPE. Python callers checked by
         repo-wide grep: skip_ids_for has two production callers (discover_entries, hoisted; the
         --sync-ids loop, unchanged with siblings=None); the three deleted stems have no live
         reference under tools/, .claude/, TAOM.Tests or Main (12 dated doc lines remain).
Waves:   one wave: Agent 5 (Data flow) and the Tooling correctness lens. Codex not run.

STANDARDS:     NOT IN SCOPE (no C#)
COMPATIBILITY: NOT IN SCOPE (no engine-facing code)
EFFICIENCY:    NOT RUN (the orchestrator launched two lenses)
COMPLETENESS:  NOT RUN (the orchestrator launched two lenses)
DATA FLOW:     PASS: 15 flows traced, 0 gaps, 2 inconsistencies (both LOW, docs), 1 LOW test note
DESIGN:        NOT RUN (the orchestrator launched two lenses)
XML:           NOT IN SCOPE
TOOLING:       PASS: 0 HIGH, 0 MEDIUM, 4 LOW
```

## Details

### Agent 5, Data flow

The three orchestrator questions all answered no defect, with scratch proofs against the base code
(`eq_check.py`: 276 pairs, 0 mismatches; `memo_probe.py`: one build serves both ratchet tests and
each assertion still fails under a mutant). Items it raised in the changed code:

- Trace 6 (LOW): the new equality test compared two paths that both run `sibling_string_ids`; pin
  the base values. Same as Tooling F1.
- Trace 10 (LOW): the `tools/README.md` game-dir prose around the two counts. Same as Tooling F3,
  plus three more points (the inheritors are inside the 50, 54 importers never name the variable,
  six importers use only helpers).
- Trace 13 (LOW nit): `gondor-armor-revamp.md:110` "the run ends `PASS: ...`"; two scope lines follow.

### Tooling correctness

- F1 (LOW): same as trace 6, with the base oracle values.
- F2 (LOW): no test can fail on the hoisted wiring in `discover_entries`.
- F3 (LOW): "Every one falls back" is false for `weapon_xml/config.py`.
- F4 (LOW nit): redundant local `from unittest import mock` in `run_main`.

Its deleted-gate check: the Gondor predecessor accepted an id only from `LOTRLOME_items/gondor/*.xml`;
the successor accepts one anywhere in the Armory. On today's data no ref differs. Whether the engine
resolves item ids regardless of folder is UNVERIFIED.

### Verification by the review lead

I re-read each named line in the worktree before acting:

| Finding | Check I ran | Verdict |
|---|---|---|
| Trace 6 / F1 | `translate_with_claude.py:375-376` (`siblings=None` calls `sibling_string_ids`); mutant run `mutants.py` M1 | CONFIRMED |
| F2 | `discover_entries` at `:264-267`; no test calls it with a sibling tree; mutant M2 | CONFIRMED |
| F3 | `weapon_xml/config.py:109-111` returns `[]` with the variable unset; my own scan of the 50 files finds no other file without a Steam literal, a `_gamedir` import or an import of a resolving tool | CONFIRMED |
| Trace 10 extras | the three inheritors name the variable at `analyze_armor_balance.py:609`, `apply_settlement_buildings.py:32`, `derive_armor_tiers.py:322`; 54 importers not in the 50 (`comm`); six importers use only `game_or_kit_running` or `ensure_exists` | CONFIRMED |
| Trace 13 | `validate_all_troop_refs.py:96-103` prints two scope lines after PASS | CONFIRMED |
| F4 | `test_doc_graph_ratchet.py:17` and `:108` | CONFIRMED |

## Action items

All five confirmed items are LOW and fixed in this review's commit. No HIGH or MEDIUM finding.

## Improvements (Step 4)

```
APPLIED:
- tools/tests/test_translate_batching.py SiblingIdsHoistTests: EXPECTED skip sets computed with the
  664f19c5b code (oracle.py: module target {0CsbbwtN, k_mod, k_xslt}, xslt target
  {0CsbbwtN, k_mod, k_shared}, malformed module target as the module target); the two equality
  tests assert both forms against it (Tooling F1, trace 6). PRESERVING. Proof: mutants.py
  unmutated (5, 0, 0); M1, a collector that drops every id, (5, 3, 0).
- tools/tests/test_translate_batching.py: two new tests through discover_entries on a synthetic
  tree (a key whose row lives only in another file of the language is skipped; a sibling that does
  not parse raises SystemExit) (Tooling F2). PRESERVING. Proof: both pass on the base module
  (oracle.py: discover BASE ['k_new'], SystemExit) and fail under M2, a caller handing
  skip_ids_for empty siblings (5, 5, 0).
- tools/README.md "Game install" and "Resolve it through": quantifiers re-scoped to the lists the
  two commands print, weapon_xml/config.py named as the exception, six helper-only importers named
  (Tooling F3, trace 10). The touched line's existing em dash became a colon (lint_docs flagged it
  as a new line). Proof: the scans above; lint_docs --fail-on-drift exit 0.
- docs/features/gondor-armor-revamp.md:110: "prints ... (two scope lines follow it)" (trace 13).
- tools/tests/test_doc_graph_ratchet.py:108: redundant local import deleted (Tooling F4). Proof:
  the module's tests OK, ruff clean.
NOT APPLIED: none.
FOLLOW-UP (all pre-existing code; no issue: an /improve agent files none, the orchestrator routes them):
- tools/README.md extract_wanderers.py row: no warning that it writes ModuleData/ instead of
  equipmentsets/.
- docs/modding/file-catalogue.md SubModule.xml citations 18 lines low past line 234.
- Line citations into tools/README.md and translate_with_claude.py from skill-sets.md,
  module-taom.md, localization-map.md and AccentStrippedTranslationTests.cs.
- troops-and-parties.md and equipment-rosters.md templates still name validate_gondor_refs.py.
- tools/package_quad_toolkit.py leaves a partial ZIP at --out on a missing file (CHANGING fix).
- analyze_war_theaters.py and audit_mbproj_registration.py read the variable with os.environ.get.

VERDICT: READY FOR COMMIT
```

The Step 4 convergence pass (one `deep-reviewer` on the applied diff) was not run by me: a review
lead cannot spawn agents. The orchestrator's convergence rounds cover it.

## Gate sweep

Two validators changed: `check_generator_item_refs.py` lost its `generate_batch2_wanderers.py` spec,
and `validate_gondor_refs.py` was deleted. Neither is a shell-command gate, so the Bash and
PowerShell tool-name corpus does not apply; I ran the old against the new on today's data instead.

- `sweep.py`: the base registry has 7 specs, HEAD 6; the only difference is the batch2 spec. Against
  the live registry (6502 items), the base batch2 tables hold 137 ids and the old gate refused 0.
- `gondor_sweep.py`: the old Gondor gate reads 313 refs and 352 ids in `gondor/`, and refuses 0; 0
  refs it refuses does the successor accept; the successor's prefix filter scans all 313.
- Seeded from the mechanism: an `sk_gd_*` or `sk_dg_*` id defined only outside
  `LOTRLOME_items/gondor/` is refused by the old gate and allowed by the successor. That is the
  intended widening to the engine's own scope; whether the engine is folder-blind is UNVERIFIED.

## Tests

- Before my edits, at ee81570a3: `Ran 8581 tests`, `FAILED (failures=7, errors=2, skipped=13)`,
  the nine known heraldry and kingdom generator failures (#1135); C#: `Passed! - Failed: 0,
  Passed: 39250, Skipped: 9, Total: 39259`.
- After my edits: `Ran 8583 tests`, `FAILED (failures=7, errors=2, skipped=13)`; the failure and
  error names match the run before by `diff` (the two added tests pass). C# was not re-run: no C#
  file changed after the run above.
- Touched modules after the edits: `python -B -m unittest tools.tests.test_doc_graph_ratchet
  tools.tests.test_translate_batching tools.tests.test_check_generator_item_refs`: `Ran 65 tests`, `OK`.
- `ruff check --no-cache` on the three changed Python files: `All checks passed!`
- `python -B tools/lint_docs.py --fail-on-drift`: exit 0.
- The hook suite was not run: no hook or `tools/test_hooks.sh` changed.

## CODEX REVIEW

Codex not run (out of usage, on the maintainer's standing word; the orchestrator's convergence
rounds stand in for it). Phase 3d assessment:

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| (none) | not run | n/a | n/a | No Codex findings to assess |

- **Confirmed bugs:** none from Codex. The five LOW items above came from the lenses.
- **False positives:** none.
- **Design questions:** none.
- **Things Codex missed:** not applicable.

### Codex lessons (pending)

None. No Codex run, so no new false-positive pattern and no missed category to record.

## Records

- RCA: `docs/reviews/rca-tools-dead-scripts-and-test-speed-2026-11-13.md`
- Lessons: `docs/reviews/lessons/testing-qa.md` (refactor parity oracles),
  `docs/reviews/lessons/build-tooling-workflow.md` (quantifiers beside a computed count)
