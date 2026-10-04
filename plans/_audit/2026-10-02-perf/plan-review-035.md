# Plan review 035, round 1 (cold reviewer)

Plan: `plans/035-release-shader-cache-check.md` (1035 lines). Code read from the run worktree at HEAD
`e9cd8b39`, whose parent chain includes `dffdf879`; `git diff --stat dffdf879..HEAD` over the plan's
five drift-check paths prints nothing, so every in-scope file is identical to the planned-at commit.
No earlier `plan-review-035*.md` exists; this is the first round.

## What I executed (evidence)

I lifted every code block out of the plan by line number into scratch copies of
`tools/package_release.py` and `tools/tests/test_package_release.py` (script
`apply_plan.py` in my scratch folder), applying Steps 2 and 3 literally, anchor by anchor. Each anchor
matched exactly once. Runs used `python -B -m unittest tools.tests.test_package_release` with the
worktree's git dir, so the `--require-build` CLI tests ran rather than skipped:

| Stage | Result |
|---|---|
| base (unchanged copies) | `Ran 57 tests`, `OK` |
| RED (Step 2 only) | `Ran 77 tests`, `FAILED (failures=7, errors=13)`; the 13 errors are 7 `AttributeError ... 'jit_optimizer_disabled'` and 6 `... 'scenes_missing_shader_cache'`; the 7 failures are the 4 `TestSceneShaderCacheCli` and 3 `TestJitReportCli` tests, exactly as plan lines 558 to 562 predict |
| GREEN (Steps 2 and 3) | `Ran 77 tests`, `OK` |
| `package_release.py --help` (GREEN) | exit 0, lists `--allow-missing-shader-cache SCENE` |

Step 5 smoke with the GREEN copy, read only, `--dry-run`, `--dest` absent before and after:

- Dev install, `--modules TAOM TAOM_Map TAOM.Dependencies`: exit 2; stderr lists
  `TAOM_Map/SceneObj/Main_map` and `TAOM_Map/SceneObj/taom_rohan_edoras_town_forceatmo`; six
  `JIT optimization OFF` lines. Matches plan lines 918 to 921.
- Patreon channel, `--modules TAOM_Map TAOM`: exit 0, no scene listed, four `TAOM/bin/.../TAOM.dll:
  JIT optimization OFF` lines (including `Win64_Shipping_wEditor`). Matches plan line 927.

The reader also returned True for every installed `TAOM.dll`/`TAOM.Dependencies.dll` copy, False for
third-party Release DLLs (DryIoc, Newtonsoft.Json, 0Harmony, MCMv5 and others) and None for the native
`MinHook.x64.dll` and `TAOM.NativeSkinFixes.dll`. I checked the reader's row widths for tables 0x00 to
0x0C, the coded-index tag lists (HasCustomAttribute, ResolutionScope, TypeDefOrRef, MemberRefParent,
HasConstant, CustomAttributeType), the stream-header alignment and the blob decoding against
ECMA-335 II.22 to II.24; I found no error.

Shader-cache facts re-measured: the dev install `TAOM_Map/SceneObj` has 47 entries, 44 with both files,
2 header-only, plus `Backups`; `testing` `Main_map` holds only the 45,832-byte header; `patreon`
`Main_map` holds the 1,835,980-byte sack; `Native/SceneObj/__default_new_editor_scene_/ShaderCache/D3D11/`
holds only the header. `$BANNERLORD_GAME_DIR` is set on this machine.

Not run (read-only role): dotnet. The Status baseline totals match `plans/_audit/2026-10-02-perf/baseline.md:7`
but I did not re-run them (UNVERIFIED by me). The full `tools/tests` discover total was not run.

## Blocking

1. **Step 4 Verify and a Done criterion can never pass: `git grep -n "proves the DLLs only"` is not
   empty after the edits.** Plan lines 905 to 908 and line 976 require the whole-repo grep to print
   nothing, but at `dffdf879` the phrase also lives in two historical review files the plan
   (rightly) does not touch:
   `docs/reviews/deep-review-017-build-identity-dirty-flag-2026-09-24.md:78` ("The gate proves the
   DLLs only; ...") and `docs/reviews/rca-build-identity-dirty-flag-2026-09-24.md:31`. After Step 4
   the grep still prints those two lines, the executor's verification fails twice, and the STOP rule
   fires. Fix: path-limit the grep to the three edited docs, for example
   `git grep -n "proves the DLLs only" -- .claude/skills/release/SKILL.md docs/reference/release-process.md docs/modding/module-map.md`
   prints nothing, in both Step 4 and Done criteria.

## Non-blocking

1. **The `allow-missing-shader-cache` grep (lines 977 to 979) is unbounded.** If the orchestrator
   commits `plans/035-*.md` or `plans/_audit/...` files onto the executor's base, the grep also lists
   them, and "lists" the five files becomes a judgment. Path-limit it to the five Scope paths, or say
   "lists at least these five, and no other file outside `plans/`".
2. **The last STOP condition (lines 1002 to 1004) names the process doc, which never said it.**
   `docs/reference/release-process.md:87` (step 9) still says "then package without `--dry-run` (the
   skill's Phase 8)"; only the skill (`.claude/skills/release/SKILL.md:173-176`) says Mike packages in
   the editor. A literal executor could read "the skill or process doc no longer says" as already
   true for the process doc and STOP. Name the one anchor to check: SKILL.md Phase 8 step 3,
   "Mike packages through the Modding Kit editor". The step 9 contradiction itself is worth a
   Maintenance note (out of scope here).
3. **Done criterion lines 982 to 985 cannot check the "0x2" half.** Step 5 prints only OFF lines, so it
   verifies the Debug value 0x107 but nothing about a Release build's 0x2 (the comment in 3d at line
   611 asserts both). The criterion already allows UNVERIFIED; better to say so explicitly for 0x2,
   or point at a check that can show ON (the reader returning False on a third-party Release DLL such
   as `TAOM/bin/Win64_Shipping_Client/DryIoc.dll` shows the ON branch, not the literal 0x2).
4. **Excerpt line ranges are slightly off** (see mismatches); the text itself matches.
5. **Line 74 carries a local absolute path** (`E:\LOTRAOM_Releases\<channel>\Modules\`). It is a
   verbatim excerpt of `.claude/skills/release/SKILL.md:175`, already in the repo, and Step 5.2 needs
   it, so it is acceptable; noted only against the "no local absolute paths" bar.
6. **Step 1 item 2 and the Commands table** say `python -B -m unittest tools.tests.test_package_release`
   without `timeout`; the run takes about 2 s, so this is cosmetic.

## Excerpt mismatches (against `dffdf879`)

- Line 125: labelled `tools/package_release.py:179-185`, but the excerpt (lines 128 to 135) runs to
  `client = f"bin/Win64_Shipping_Client/{dll_name}"`, which is line 186.
- Line 141: "`:398-400`: `_report` ends, then `def main` at line 400". `_report` ends at line 397;
  398 and 399 are blank. `def main` at 400 is correct.

Everything else checked matches: docstring 33 to 35, imports 39 to 47 (no `struct`), 97 to 102, 106 to
120, `stamps_dirty_trees` ending at 162 and `require_build` at 165, `classify` 241 to 242, 409 to 411,
438 to 462, unknown entries 440 to 449 / 488 to 490 / 492 to 496; the test file (618 lines, `import
shutil` at 28, last docstring bullet at 24, last class line at 614); SKILL.md 144 to 150 and 173 to 176;
release-process.md 90 to 91; module-map.md step 5 at 485 to 489; `.github/workflows/build.yml:180-216`;
`docs/features/shader-precompilation.md:5`, `:130`, `:146`; `docs/modding/modules-overview.md:195-199`;
`Directory.Build.props:27-38` (the GameFolder resolution comment and `BANNERLORD_GAME_DIR`).

## Checklist

- Executable from plan plus repo: yes once blocking item 1 is fixed. Every code block is complete and
  anchored on quoted text; `<scratch>`, `<game>` and `<releases>` resolve from the dispatch rules,
  `$BANNERLORD_GAME_DIR` and the quoted skill excerpt.
- Every step ends in a command with an exact expected result: yes (Steps 1 to 6).
- TDD order: Step 2 RED with exact totals before Step 3 GREEN; verified above.
- Issue line: "filed by the orchestrator before execution", matching Orchestrator steps.
- ADRs: correctly states ADR-002/007/008 do not bind (no C#); simplicity criterion named.
- Protected and single-owner files: none touched; `Directory.Build.props` and the csproj are listed
  out of scope.
- STOP conditions: specific to this plan (RED count, reader offsets, Step 5 verdicts, `classify` and
  `SCENE_BACKUPS`); see non-blocking 2.
- Done criteria machine-checkable: yes, apart from blocking 1 and non-blocking 1 and 3.
- Planned-at `dffdf879` and drift-check paths equal the five Scope paths.
- Non-deploying dotnet commands carry `-p:DisableModuleCopy=true -p:ModuleId=`; no `./build.ps1`.
- No worktree path or branch name; no CHANGELOG step (CHANGELOG listed out of scope).
- Commit subject `feat(release): v2.0.32 - packager refuses a scene missing its sack` is 66
  characters; `Main/_Module/SubModule.xml:6` is `v2.0.32`.
- No em or en dash anywhere in the plan (scanned all 1035 lines for U+2014 and U+2013); no secret
  values.
