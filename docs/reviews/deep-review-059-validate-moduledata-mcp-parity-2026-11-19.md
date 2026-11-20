# Deep review: plan 059, one pass composition for the validator CLI and the MCP tool (#623)

```
DEEP REVIEW REPORT
===================
Feature: validate_moduledata: one pass composition for the CLI and the MCP tool (#623), plan 059
Date: 2026-11-19

Scope:   scripts and docs. tools/validate_moduledata.py (run_all_passes, PassRun, _loaded_tpacs),
         tools/validate_mesh_refs.py (DEFAULT_TPAC_MODULES, _line_index, the Languages/ skip, the
         Tier C fallback), tools/taom_query.py, tools/taom_mcp_server.py, tools/test_hooks.sh, four
         tools/tests files, four docs and the armory-audit skill line.
         Diff af0f5f330..9d147c068 on improve/059-validate-moduledata-mcp-parity.
Blast radius: NOT IN SCOPE for graphify (Python only). Callers traced with git grep instead:
         run_all_passes (main, ModuleDataQuery.validate), DEFAULT_TPAC_MODULES (validate_mesh_refs
         main, audit_deleted_mesh_impact.py:805, audit_gender_variation_flags.py:159, _loaded_tpacs),
         classify body_tpac_paths (validate_moduledata.py:372, audit_armory_refs.py:357,
         validate_mesh_refs.py:1251). Every classify caller passes the same packs it built the
         present-set from.
Waves:   one wave: Agent 5 (Data flow), Tooling correctness. Tier 2 tooling.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: SKIPPED BY TIER
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS, 0 gaps, 1 inconsistency (LOW, outside the changed hunks: FOLLOW-UP)
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE
TOOLING:       PASS, 4 findings (all LOW): 2 confirmed and fixed, 1 NEEDS MIKE, 1 false positive
```

## Lead's own checks (the orchestrator's probes)

Each was run or read by the lead in the worktree this session.

| Probe | Result | Evidence |
|---|---|---|
| `run_all_passes` reaches every pass old `main()` ran, stderr texts verbatim | Holds | Removed and added hunks compared side by side. Old (base `af0f5f330` copies in scratch) and new CLI run against the live install: the `--json` issue files are byte-identical (`cmp`, 1823 issues each), report stdout byte-identical, both exit 0. The stderr diff is only the order of the `SandBoxCore` no-pack line (now printed during the run, before the notices). |
| MCP result carries a WARNING when the install is absent | Holds | Probe with `game_modules` pointing at a folder that does not exist: 11 `PARTIAL_RUN` issues (the no-install notice and 10 SKIPPED notices); a `codes=["UNKNOWN_CULTURE"]` call still returns them. With the install: 1823 issues, the same count as the CLI, 0 `PARTIAL_RUN`. Both calls wrote 0 characters to stdout (the stdio JSON-RPC stream). |
| Tier C fallback scans only unparsed packs and finds the same refs | Holds for every real caller | Differential sweep, 60 classify cases: 55 identical. The 5 that differ are all stricter in the new code: a body found only in a parsed pack's raw bytes (not its TOC), and a caller passing `body_tpac_paths` outside the present-set, which no caller does (`git grep`). |
| `_line_index` gives the same line numbers | Holds | 300 random texts with comments, CRLF and all three ref loops: 0 differences between old `_lineno` and new `_line_index`. |
| The `Languages/` skip | Holds | Matches only the root folder: nested `items/Languages/` and `LanguagesExtra/` are still read. No mesh, body or prefab attribute in the live Armory or repo `Languages/` folders (grep, those two folders only). |
| One `DEFAULT_TPAC_MODULES` list | Holds; now gated | `_ART_MODULES` survives only in an RCA and the plan. A new test reads `SubModule.xml` (finding T3). The `TAOM_Map` precondition is in the plan's Maintenance notes, accepted. |
| Moved `__main__` runs every test | Holds | See the suite totals below: the full `tools/tests` discover run includes the module. |

## Details

### Agent 1, Standards

SKIPPED BY TIER (deep-review tier 2 rule).

### Agent 2, Engine compatibility

SKIPPED BY TIER.

### Agent 3, Efficiency

SKIPPED BY TIER.

### Agent 4, Completeness

SKIPPED BY TIER.

### Agent 5, Data flow

11 flows traced, 0 gaps. Lead verdicts on its items:

- **L5-I1, INCONSISTENT (LOW): `audit_armory_refs.py:355` still checks bodies against
  `[module, "Native", "SandBoxCore", "SandBox"]`, so the audit and the validator now use different
  module lists.** CONFIRMED (read at :355 and :357). A body that ships only in TAOM or `TAOM_Map` packs
  passes the validator and reads MISSING in `/armory-audit`: stricter, never a silent pass, and 0 such
  refs on the live install. The file is outside the changed hunks and the plan's Maintenance notes put
  it out of scope. FOLLOW-UP.
- **L5-FU1: the no-install `PARTIAL_RUN` text reaches MCP callers with CLI-only advice ("This run will
  exit 2", "pass --game-modules").** Accepted by the plan (:622). Not a defect: rewriting it changes the
  CLI's stderr bytes, which this review proved identical. NOT APPLIED.
- **L5-FU2: `body_tpac_paths=unparsed` at `validate_moduledata.py:372` and `body_tpac_paths=tpacs` at
  `audit_armory_refs.py:357` are now redundant.** FALSE POSITIVE. `classify` still reads
  `body_tpac_paths` in its no-TOC branch, which runs whenever `present.physicsshapes` is empty
  (`validate_mesh_refs.py:849-852` and the `else` at :862), for example when every pack failed to
  parse. Dropping the argument changes which packs that branch scans.

### Agent 6, Design and elegance

SKIPPED BY TIER.

### Agent 7, XML and ModuleData integrity

NOT IN SCOPE (no XML changed).

### Tooling correctness

- **T1 (LOW): the new `test_hooks.sh` case pins a silent `{}` for every validator exit 2, and the hook
  deletes the stderr file on exit 2, so an argparse usage error from a future flag rename would turn
  the gate off quietly.** CONFIRMED as described (the hook behaviour is pre-existing; the test only pins
  it). Changing the answer to an `UNCHECKED` `additionalContext` changes a gate's behaviour. NEEDS MIKE.
- **T2 (LOW): the MCP tool docstring said "a pass that could not run comes back as a PARTIAL_RUN
  WARNING".** CONFIRMED. `armour_class_table_issues` (`validate_moduledata.py:155-158`) and
  `validate_mesh_refs.module_tpacs` (:455-459) still announce their skips on stderr only, which an MCP
  client never reads. The live install shows the second one on every run (`SandBoxCore` ships no
  `.tpac`). FIXED (docstring half): the tool docstring, `ModuleDataQuery.validate`'s docstring and the
  feature doc line now name what comes back and what does not. The other half (return those skips as
  notices) changes the MCP output and is NOT APPLIED.
- **T3 (LOW): `DEFAULT_TPAC_MODULES` is kept by hand; its comment points at `SubModule.xml`, and the
  only test pinned today's list.** CONFIRMED, and a repeat of the `lessons/build-tooling-workflow.md`
  entry "A gate's realm list comes from the generators, never from a hand-kept list". FIXED with a
  test that reads every `DependedModule` from `Main/_Module/SubModule.xml` and fails unless each is in
  the list or in a named set of modules that ship no `.tpac` (`TAOM.Dependencies`, `CustomBattle`:
  neither has `AssetPackages/` nor `Assets/` packs on the desktop install, counted this session).
- **T4: CLI notices now print after the whole run, so a crash (exit 3) loses the no-install line.**
  FALSE POSITIVE as a defect: a documented, intended order change (moduledata-validation.md
  changelog), and the traceback still reaches the hook's `additionalContext`.
- **UNVERIFIED (carried over): whether an MCP client's tool-call timeout allows a full unfiltered run
  (about 20 s on the desktop, CLI timing above).** The commit's Not-tested trailer already says so.

## Gate sweep

The diff changes two gates the commit hook runs (`validate_moduledata.py`, `validate_mesh_refs.py`);
no hook script changed, so the "Bash and PowerShell tool name" corpus does not apply (these gates read
data, not command text). Old gate: the base copies of both files run from the scratch folder, with
their path constants pointed at the worktree. Corpus: the live install, plus a synthetic corpus seeded
from each mechanism of the fix (Tier C fallback scope, `Languages/` skip, line index, module list).

What the old gate refused and the new one allows:

1. A mesh or body ref inside a root `Languages/` folder is no longer extracted, so it is never checked.
   On disk: 0 such refs in the live Armory and repo `Languages/` folders.
2. A body or mesh present only in TAOM or `TAOM_Map` packs (and, for the `validate_mesh_refs` CLI,
   `SandBox`) now resolves. That is the intended fix: the engine loads those modules. On the live
   install the validator's findings are byte-identical, old against new.

The new gate is stricter in one place: with a pack that failed to parse, a body seen only in a parsed
pack's raw bytes (not its TOC) is now MISSING.

## Suite totals

Base totals (branch head `9d147c068`, before any lead edit) and final totals:

- C#: base `Failed!  - Failed:     3, Passed: 43028, Skipped:     9, Total: 43040`. The 3 failures are
  localization tests (`EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems`,
  `EveryKey_IsSeededInAllTwelveLanguageFiles`); the branch changes no C# and no language file, so they
  are not this branch's. The review's commit changes no C#, so that run is also the final one.
- Python `tools/tests`: base `Ran 10560 tests in 619.640s`, `OK (skipped=13)`; final
  `Ran 10561 tests in 584.882s`, `OK (skipped=13)` (the one new test is T3's).
- Hook suite, from the worktree with `CLAUDE_PROJECT_DIR` set to it, on the branch head:
  `1446 passed, 0 failed`; section 7m (the ModuleData hook, with the three new exit-code cases) ran.
  The base's own count was not measured, so "three more than the base" is UNVERIFIED. The review's
  commit changes no hook and no `test_hooks.sh`.

```
─────────────────────────
ACTION ITEMS
─────────────────────────
1. NEEDS MIKE: T1, whether the ModuleData hook answers UNCHECKED on validator exit 2.
2. FOLLOW-UP: L5-I1, audit_armory_refs.py reads DEFAULT_TPAC_MODULES.
3. NEEDS MIKE: T2's second half, whether the two stderr-only skips become PARTIAL_RUN notices.

─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:     tools/taom_mcp_server.py:74-80, tools/taom_query.py:123-126,
             docs/features/moduledata-validation.md:837: the PARTIAL_RUN claim narrowed to what
             comes back (T2). Docstrings and docs only; the full tools/tests run is the
             characterisation, green before and after.
             tools/tests/test_validate_mesh_refs.py TpacModuleFallbackTests
             test_every_module_taom_depends_on_is_scanned_for_art (T3): red against the base list
             ("Items in the first set but not the second: 'sandbox'"), green on the branch.
NOT APPLIED: tools/test_hooks.sh exit-2 case and .claude/hooks/check-moduledata-validation.sh (T1):
             behaviour-changing in a gate, pre-existing hook behaviour; NEEDS MIKE.
             validate_moduledata.py:155-158, validate_mesh_refs.py:455-459 skips as notices (T2,
             second half): behaviour-changing; NEEDS MIKE.
             validate_moduledata.py:839-849 no-install notice text (L5-FU1): plan :622 accepts it,
             and a rewrite changes the CLI's stderr bytes.
FOLLOW-UP:   tools/audit_armory_refs.py:355 module list (L5-I1): no issue filed, an /improve lead
             files none; the orchestrator decides at wrap-up.

VERDICT: READY FOR COMMIT
```

## CODEX REVIEW

Codex not run: no paid dispatch was authorised for this item.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| (none) | | | | Codex not run |

- **Confirmed bugs:** none from Codex.
- **False positives:** none.
- **Design questions:** none from Codex (the lens questions are in ACTION ITEMS).
- **Things Codex missed:** not applicable.

### Codex lessons (pending)

None: no Codex run, so no new false-positive pattern or missed category.
