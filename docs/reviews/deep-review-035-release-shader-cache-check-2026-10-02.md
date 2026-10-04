# Deep review: plan 035, release packager shader cache and JIT reports (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 035, the release packager reports each shipped scene's shader cache (header,
         sack, sack format) with a WARNING for a sack out of step with the others, and each
         shipped TAOM DLL copy's JIT optimization state; report only, never refuses
         (branch perf/035-release-shader-cache-check, d50bf962..a2e46e29)
Date:    2026-10-02 (review lead run 2026-10-03)

Scope:   scripts (tools/package_release.py, tools/tests/test_package_release.py), harness
         (.claude/skills/release/SKILL.md), docs (release-process.md, module-map.md). No C#,
         no XML, no hook, validator or CI step.
Blast radius: NOT IN SCOPE (no C# type changed); git grep finds no hook or CI consumer of the
         packager's output.
Waves:   one wave: Standards (1), Completeness (4), Tooling correctness. Codex not run.

STANDARDS:     FAIL: 3 MEDIUM, 5 LOW (no CRITICAL: no escalation)
COMPATIBILITY: NOT IN SCOPE (no engine code)
EFFICIENCY:    NOT IN SCOPE (not launched)
COMPLETENESS:  INCOMPLETE: no GitHub issue at review time (DECISIONS D4, by design; filed since
               as #717); the #448 claim not delivered; the reader branches the real DLLs take
               untested
DATA FLOW:     NOT IN SCOPE (not launched)
DESIGN:        NOT IN SCOPE (not launched)
XML:           NOT IN SCOPE
TOOLING:       FAIL: 2 MEDIUM, 4 LOW, 3 FOLLOW-UP
```

## Details

The three lens reports overlap heavily; the table merges them into one row per defect. Every row
was re-checked by the review lead against the worktree before any change:

- **F1** reproduced with a probe that builds a `SceneObj/<Cyrillic name>` tree and runs the
  packager with `PYTHONIOENCODING=cp1252`: the commit under review exits 1 with
  `UnicodeEncodeError: 'charmap' codec can't encode characters in position 20-24`, the base
  (`git show d50bf962:tools/package_release.py`) exits 0.
- **F2** read from `stale_sack_formats`: the majority is taken over the shipped sacks only, so a
  uniformly lagging set has no odd one. A unit test now pins this. (Convergence round 1: #448's
  own sacks were module-level and never reach this function; see that section.)
- **F3** reproduced with the tooling lens's mutation scripts: 16 of 22 mutations of the reader
  left all 85 tests green.
- **F4** checked by counting `compressed_shader_cache.sack` files in the release channels:
  testing 52 scene sacks, patreon 53, public 53 (`TAOM_Map/SceneObj/Main_map` included in
  patreon), and 0 module-level `Shaders/D3D11` sacks in any channel. Vanilla v1.5.3 does ship
  header-only scenes (Native 2, SandBoxCore 8), so the remaining part of that comment holds.

| # | Sev | Finding (lenses) | Verdict | Action |
|---|-----|------------------|---------|--------|
| F1 | MEDIUM | A scene folder name a piped cp1252 stdout cannot encode crashes the report with a traceback and exit 1, before `--require-build` and the copy, breaking "never changes the exit code" (Std F1, Tooling 4, Compl L2) | CONFIRMED | Fixed: `main` reconfigures stdout and stderr with `errors="backslashreplace"`, and each report runs inside a guard that prints `<label> report failed, ignored: ...` and carries on. Tests `test_a_scene_name_the_console_cannot_encode_does_not_change_the_exit_code` and `test_a_failing_report_does_not_change_the_exit_code`, both red before the fix |
| F2 | MEDIUM | The stale-format WARNING cannot see #448's case: #448's three lagging sacks were module-level `<Module>/Shaders/D3D11` sacks, which the scene report never reads (corrected in convergence round 1; this row first blamed the majority rule). Separately, it compares the shipped scene sacks with each other, so a scene-sack set lagging as a whole raises none and a freshly re-saved sack can be flagged as the odd one, while the docs and the WARNING text said it covers #448 (Std F2, Tooling 2, Compl M1) | CONFIRMED (wording); the engine-baseline code change is NEEDS MIKE | Fixed the overclaim: the WARNING now reads `... not the majority 0x0783 of the shipped sacks (one of the two formats may be stale, see issue #448)`; the docstring, the constant's comment, `release-process.md` step 9, `module-map.md` and the skill say what the check cannot see. `test_a_set_uniformly_behind_the_engine_is_not_flagged` pins the limitation; the CLI test asserts the new wording (red before). Comparing against vanilla Native's own sacks departs from the amendment's "majority" rule, so it is Mike's call (N1) |
| F3 | MEDIUM | The reader's tests used one PE32+ fixture with 2-byte heap and coded indexes and one row per table; the installed `TAOM.dll` (HeapSizes 0x05: #Strings and #Blob 4 bytes, #GUID 2; MemberRefParent and CustomAttributeType 4 bytes through 20,114 MethodDef rows; corrected in convergence round 1), the PE32 channel copy (15,553 MethodDef rows, 2-byte HasConstant; added in convergence round 2) and the PE32 `TAOM.Dependencies.dll` take branches no test ran (Std F3, Tooling 1, Compl M3) | CONFIRMED | Fixed, tests only: `_managed_dll` gained `pe32`, `wide_heaps`, `filler`, `typerefs`, `memberrefs`, `params` and `native`, with widths written from ECMA-335 II.24.2.6 rather than from the reader. Twelve new reader tests, each reading a Debug and a Release form. System.Reflection.Metadata reads all 36 fixture files back with the intended verdicts. The lens's 22 mutations plus two of the new guards: 23 of 23 applicable now fail at least one test (16 survived before) |
| F4 | LOW | `package_release.py` ("TAOM does not ship sacks as a rule") and the test file's section comment state a policy the channels contradict: every channel ships 52 to 53 scene sacks (Std L1, Tooling 3, Compl L1) | CONFIRMED | Reworded both: whether a scene ships its sack is the maintainer's call; the test comment records the 2026-10-03 census |
| F5 | LOW | `SKILL.md` Phase 8 step 2 restated `release-process.md` step 9 almost line for line (Std L2, the H1 duplication check) | CONFIRMED | Trimmed to the operator's delta (the two sections never refuse, a missing sack ships, OFF is expected, what a WARNING means) and a link to step 9 |
| F6 | LOW | `release-process.md:97` ran to 139 characters in a paragraph wrapped near 100 (Std L4, Compl L5) | CONFIRMED | Rewrapped with the F2 edit |
| F7 | LOW | `jit_optimizer_disabled` had no production caller; it dropped the reason the tests should pin (Std L5, Tooling 6, Compl L3) | CONFIRMED | Deleted; the tests call `jit_optimization` and assert `(verdict, reason)` |
| F8 | LOW | Most failure reasons and the unreadable-sack CLI output were never asserted; `test_scenes_without_a_readable_format_are_not_counted` built a scene with no sack, not an unreadable one (Compl L3) | CONFIRMED | Added: the native reason (PE32 and PE32+), `no MZ header`, `cannot read`, truncated metadata, `test_an_unreadable_sack_is_listed_and_counted` (line and `1 unreadable` suffix), an unreadable sack in `stale_sack_formats`, and `test_file_names_match_whatever_their_case` |
| F9 | LOW | The commit body says the module map's packaging step describes "both reports"; `module-map.md` describes only the scene listing (Compl L4) | CONFIRMED | The commit is not rewritten; the follow-up commit body corrects the statement for the release note |
| T5 | LOW | An assembly with no DebuggableAttribute prints a bare `ON`, which reads the same as a misparse (Tooling 5) | Proposal, behaviour CHANGING (a report string) | NOT APPLIED: N3 |
| L3 | LOW | Module-level `Shaders/D3D11/compressed_shader_cache.sack` files are copied by `classify` and never reported (Std L3, Compl M2, Tooling FU1) | NEEDS MIKE (scope, the meaning of D8) | NOT APPLIED: N2 |

### Orchestrator probes, as verified by the lead

1. **No path refuses, changes the exit code or changes the output files**, now that F1 is fixed.
   The diff adds no `return`, `exit` or `raise` to `main`; the `--json` manifest and `_copy_list`
   are untouched. The guard catches `Exception`, so a report can no longer end the run with a
   traceback; the mutation that narrows the guard (`report_guard_catches_nothing`) fails a test,
   as does removing the stream reconfigure.
2. **The metadata reader against ECMA-335 II.22 and II.24.** The lenses checked the row widths,
   tag lists and heap bits by hand and differentially against System.Reflection.Metadata over
   421 and 1,613 real DLLs with 0 disagreements and 0 escaped exceptions. The lead did not
   repeat the 1,613-file run; the lead's evidence is the 36 new fixture files read back by
   System.Reflection.Metadata and the 23 killed mutations. A native DLL reads
   `(None, "no CLI header (a native DLL)")` and a truncated file `None` with a reason, both now
   tested.
3. **The sack format read** (the little-endian dword at bytes 4 to 7) is correct and tested,
   including a short file. **The majority rule** is what the amendment prescribed and is blind to
   a uniformly lagging set (F2).
4. **The executor's deviations** all trace to D8 (no refusal, no `--allow-missing-shader-cache`,
   report tests instead of refusal tests) or D6 (reasons and the "nothing to report" lines). The
   amendment's "and its log" has no target, since the packager writes stdout only (N5).
5. **Doc wording.** After the fixes, the edited docs match the code. Texts now false outside the
   change are listed under FOLLOW-UP; `shader-precompilation.md` lines 130 and 146 were named out
   of scope by the plan and stay so.

## Action items

1. Mike: whether the stale-format check should compare against vanilla's own sacks (N1), and
   whether D8's "I don't ship them" means module-level sacks (N2). The answers decide the
   follow-up doc sweep.
2. File the issue drafted for this work (DECISIONS D4) with labels that exist (N4). Filed since as
   #717.
3. The Step 4 convergence pass (one deep-reviewer on the fix diff) was not run: the review lead
   cannot spawn agents. The orchestrator may run it on the follow-up commit.

## Improvements (Step 4)

No Agent 3 or Agent 6 lens ran, so no APPLY or KEEP proposals exist beyond the tooling and
completeness rows above.

APPLIED:
- `tools/package_release.py` `jit_optimizer_disabled` deleted (F7); behaviour preserving, no
  production caller (`git grep` shows test callers only); proved by the reader tests, green
  before and after.
- `tools/tests/test_package_release.py` reader fixture widened (F3) and reason assertions added
  (F8); tests only.
- `.claude/skills/release/SKILL.md` trimmed to its delta (F5).

NOT APPLIED:
- Compare sacks with vanilla Native's sacks under `--source` instead of the shipped majority
  (Tooling 2, Compl M1 fix, Std F2 fix): behaviour CHANGING and departs from the amendment (N1).
- Report module-level sacks (Std L3, Compl M2): behaviour CHANGING, scope call (N2).
- `(False, "no DebuggableAttribute on the assembly")` and `ON (<reason>)` (Tooling 5): behaviour
  CHANGING (N3).
- An optional test that reads the installed `TAOM.dll` when a game directory is set (Compl M3):
  the widened fixtures, read back by System.Reflection.Metadata, cover the same branches without
  a machine dependency.

FOLLOW-UP (pre-existing or outside the diff; no issue filed: the run files none, DECISIONS D4):
- Texts now false (lens-reported; the lead verified only the channel census behind them):
  `docs/features/shader-precompilation.md` lines 5, 130, 140, 146 and 253;
  `Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt` lines 3 to 4 and 32;
  `Main/Features/ShaderPrecompilation/PrecompileSceneProvider.cs` lines 18 to 19;
  `docs/migration/v1.4.8-impact.md:64` (dated; an update note rather than a rewrite);
  `plans/_audit/2026-10-02-perf/REPORT.md:152` and `FOR-MIKE.md:5` (true of module-level sacks
  only); the premise of issue #448. Which of them to change depends on N2.
- `release-process.md:87` and `module-map.md:486` say to package without `--dry-run`, while the
  release skill says Mike packages in the editor (verified: both lines read so).
- `tools/README.md` has no row for `package_release.py` (verified: 0 matches).
- Lens-reported, not re-verified by the lead: the testing channel ships `TAOM_Map/SceneObj/Backups`
  (16 files, 116 MB); patreon ships retired Debug binaries `BehaviorTreeWrapper.dll` and
  `BehaviorTrees.dll`; patreon ships `temp_mission_scene` and two `wip_` scenes with sacks;
  `classify` compares `Backups` and `KNOWN_TOP_DIRS` case-sensitively; line citations into the
  packager in `module-armory.md`, `module-taom.md` and `provenance-register.md` were stale at the
  base and shift again.

## Verification

- Packager tests: before the fixes, with the new tests, `Ran 100 tests`, `FAILED (failures=2,
  errors=1)` (the three tests of F1 and F2); after, `Ran 102 tests`, `OK`.
- Full Python suite: base `Ran 2990 tests`, `FAILED (failures=3, skipped=8)`; after, `Ran 3007
  tests`, `FAILED (failures=3, skipped=8)`, the same three known base failures
  (`test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`,
  `test_default_is_on_the_e_drive`).
- dotnet (no C# changed, run for the record): `Failed! - Failed: 1, Passed: 12345, Skipped: 2,
  Total: 12348`. The failure is `EveryLanguage_DeclaresARowForEveryEnglishKey` on the
  `taom_tr_*` tournament keys, present at `d50bf962`; this branch touches no C# or localization
  file.
- `python -B tools/lint_docs.py --dash-base HEAD`: exit 0, 0 dashes in new prose, 0 dead links.
- Gate sweep: none. No hook, validator or CI step changed; the packager's refusal paths
  (`--require-build`, unknown entries, a non-empty destination) are untouched.

VERDICT: READY FOR COMMIT

## Codex review

Codex not run: no adversarial dispatch was made for this item, so there is no Phase 3d table to
fill.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| (none) | | | | Codex not run |

### AGENTS.md lessons (pending)

For the orchestrator's wrap-up, in `.ai/review-reference.md` "Look harder here":
- A report-only path in a CLI tool is still on the exit-code path: a print of an untrusted name
  under a cp1252 pipe ends the run with exit 1. Check every new print of a file or folder name.
- A binary-format reader tested on one hand-built fixture: ask which real files take the other
  branches (PE32 versus PE32+, 4-byte heap and coded indexes) and run a mutation or two.

## Needs Mike

- N1: compare scene sacks against vanilla Native's own sack format when `--source` is a dev
  install (falling back to the shipped majority for a channel folder), or keep the majority rule
  with the now-honest wording. N1 alone would not catch #448: its three lagging sacks were
  module-level `<Module>/Shaders/D3D11` sacks, which the report does not read. Catching a #448
  repeat needs N2 (reading module-level sacks) together with a baseline such as N1.
- N2: does D8's "I don't ship them" mean the module-level `Shaders/D3D11` sacks (absent from
  every channel, 166 MB that `classify` would copy from the dev install) rather than the scene
  sacks (52 to 53 per channel)? If so, report them, and decide whether `classify` should drop them.
- N3: print `ON (no DebuggableAttribute on the assembly)` instead of a bare `ON`.
- N4: the issue drafted for this work was not filed at review time (D4), and is filed since as
  #717; its draft labels `tooling` and `release` do not exist in the repo (lens-reported).
- N5: the amendment's "and its log" has no target; the packager writes stdout only.

## Convergence round 1

The convergence reviewer re-read `a2e46e29..6cc7fbfe` and raised two findings. Both were re-checked
against the worktree before any change, and both are fixed in the commit that adds this section
(`fix(release): v2.0.32 - convergence fixes for plan 035`).

| # | Sev | Finding | Verdict | Action |
|---|-----|---------|---------|--------|
| C1 | MEDIUM | The F3 fixture still did not take the installed `TAOM.dll`'s shape: its one `wide_heaps` switch set all three heap widths together (HeapSizes 0x00 or 0x07), and coded indexes widened only through TypeRef or MemberRef rows, while `TAOM.dll` reads HeapSizes 0x05 and owes its 4-byte MemberRefParent and CustomAttributeType to 20,114 MethodDef rows. The `_managed_dll` docstring and the F3 lesson described the shape inaccurately | CONFIRMED | Fixed, tests only. `shape.py` on the installed Win64 `TAOM.dll` printed HeapSizes 0x05, rows 0x01 1349, 0x06 20114, 0x0A 8068, and widths #Strings 4, #GUID 2, #Blob 4, ResolutionScope 2, TypeDefOrRef 2, MemberRefParent 4, CustomAttributeType 4. The reviewer's five mutants all survived at `6cc7fbfe` (`Ran 102 tests`, `OK (skipped=13)` each, the installed DLL misread as ON under M1 to M4). `_managed_dll` now takes `heap_sizes` (bits 0x01, 0x02, 0x04 set #Strings, #GUID and #Blob separately) and `methods` (MethodDef padding that widens HasCustomAttribute at 2,048, MemberRefParent and CustomAttributeType at 8,192, TypeDef's MethodList at 65,536). New tests: each heap bit alone, 0x07, the installed `TAOM.dll`'s measured header (`test_the_installed_taom_dll_shape`), and three MethodDef widenings. After the change every mutant is killed: M1 to M3 by `test_each_heap_size_bit_on_its_own` and `test_the_installed_taom_dll_shape`, M4 and M5 by `test_the_installed_taom_dll_shape`, `test_four_byte_member_ref_parent_and_attribute_type_through_method_def` and `test_a_four_byte_method_list_index`; controls C1 and C2 still killed. System.Reflection.Metadata read all 27 new fixture files (9 shapes, Debug, Release and none) with the intended verdicts and no error. The docstring, the F3 lesson in `lessons/testing-qa.md`, the RCA's F3 row and this record's F3 row now state the measured shape |
| C2 | LOW | The F2 rewording blamed the wrong mechanism for #448: #448's three lagging sacks were module-level `<Module>/Shaders/D3D11` sacks, which `scene_shader_caches` never reads, so the majority rule never saw #448's data; N1 alone would not catch #448 | CONFIRMED | Fixed, text plus one test. `package_release.py:375-379` skips any path whose first part is not `SceneObj` and needs `ShaderCache/D3D11` under the scene; `docs/features/shader-precompilation.md:130` counts zero TAOM_Map scene sacks on 2026-08-10 and `:140` places the three 0x0782 sacks under `TAOM/`, `TAOM_Map/` and `LOTRLOME_Armory/Shaders/D3D11/`. Reworded: the constant's comment and `_shader_cache_report`'s docstring in `package_release.py`, the uniform-lag test's comment, `release-process.md` step 9, the F2 lesson in `lessons/testing-qa.md`, the RCA (summary, F2 row, root-cause line, plan bullet), this record's F2 row and details line, the REVIEW-LOG entry, and N1 (a #448 repeat needs N2 plus a baseline). `test_issue_448s_module_level_sacks_are_not_read` replays #448's layout (header-only scenes, one module-level sack at 0x0782) and asserts no format is compared and no WARNING printed; it passes on the code and fails against a scratch mutant that reads module-level sacks (the N2 shape) |

No false positives. Gates: no hook, validator or CI step changed, so no differential sweep was
needed. Final suites: Python `Ran 3013 tests`, `FAILED (failures=3, skipped=8)`, the three known
base failures (`test_applying_every_spec_is_a_no_op`,
`test_the_committed_career_file_is_what_the_rule_derives`, `test_default_is_on_the_e_drive`;
before the fix `Ran 3007`, the same three). dotnet `Failed: 1, Passed: 12345, Skipped: 2,
Total: 12348`, the base's `EveryLanguage_DeclaresARowForEveryEnglishKey` on the `taom_tr_*`
tournament keys; no C# changed. `python tools/lint_docs.py --fail-on-drift` exit 0.

Texts still false (named for Mike, out of this plan's scope as before): the claims listed under
FOLLOW-UP (`shader-precompilation.md` lines 5, 130, 140, 146 and 253 and the others there) are
unchanged by this round.

## Convergence round 2

The convergence reviewer read `6cc7fbfe..893f9428` and raised one MEDIUM and two LOW findings; the
orchestrator verified and fixed all three in `docs(release): v2.0.32 - record plan 035's convergence rounds`.

- **R2-1 (MEDIUM):** the TAOM.dll the patreon and public channels ship is PE32 with HeapSizes 0x05, TypeRef
  1,193, MemberRef 6,243, MethodDef 15,553 and Param 14,333 rows, so HasConstant is 2 bytes while
  MemberRefParent, CustomAttributeType and HasCustomAttribute are 4. Mutant X1 (`coded_w(3, ...)` for
  HasConstant) passed all 108 tests and read both channels' Debug copy as ON. Fixed, tests only:
  `test_the_channel_taom_dll_shape` builds that header, and the `_managed_dll` docstring names it. The
  reviewer's harness, re-run on the fixed file: `Ran 109 tests`, `OK (skipped=13)` unmutated; X1 is killed
  by `test_the_channel_taom_dll_shape`, and M1 to M5, X2 to X5, X8, X10 to X12 and controls C1 and C2 are
  killed. X6, X7 and X9 (coded-index tag widths raised) survive and change no real DLL's read. The F3 rows
  here and in the RCA now say "installed" for the 20,114-row build and name the channel copy.
- **R2-2 (LOW):** `893f9428`'s body says five reader slips made the Debug `TAOM.dll` read ON. Four did
  (M1 to M4); M5 made every copy read unknown (`malformed metadata: IndexError`). Agents may not amend,
  so the records commit's body carries the correction; a history rewrite at merge can reword it.
- **R2-3 (LOW):** the `params` line of the `_managed_dll` docstring regained its sixth space.

Packager tests after the fix: `Ran 109 tests`, `OK`.
