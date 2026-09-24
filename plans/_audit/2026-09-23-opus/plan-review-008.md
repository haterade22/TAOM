# Plan review 008: binding gate, no silent skips

Cold review of `plans/008-binding-gate-no-silent-skips.md` against
`.claude/skills/improve/references/plan-template.md` ("Quality bar"). Reviewer read only the plan,
the template and the repo. Line numbers are the plan's unless a file is named.

**Verdict:** one blocking issue (worktree path discipline), otherwise a strong plan. Every code
excerpt matches `b2e387db` byte for byte; the drift check is empty at `4b5662b2` (re-run this
review, exit 0, no output). Fix the blocking item and it is executable by a weak model.

## Evidence gathered this review

- `git show b2e387db:<path>` for every cited file: `GameAssemblies.cs` (132 lines; `:19-21`,
  `:43-48`, `:50-56`, `:111-122`, usings `:1-5` all match), `TAOM.Tests.csproj` (42 lines,
  tab-indented, `:34-42` match, `</ItemGroup>` at `:39`), `Directory.Build.props:37-38`,
  `HarmonyPatchBindingTests.cs:32-33,41-50`, `GameModelOverrideBindingTests.cs:48-57,75-80`,
  `notify-test-results.sh:45-60`, `.claude/settings.json:141-146`, `tools/test_hooks.sh` (825
  lines, 7b `:770-797`, `# ----` then `head2 "8.` at `:799-800`, `ok`/`bad`/`head2`/`$REPO`/`$SANDBOX`
  and the EXIT trap as described), `SKILL.md:27,32,74`, `README.md:46,66`,
  `agent-operating-manual.md:43`, `s6-runtime-punchlist.md:13`, `build.yml:3-8,259-278` (278 lines,
  so the new step is appended at end of file), `Main/TAOM.csproj:119-124`,
  `FileLoggerTests.cs:32-49`, `rca-lord-identity-2026-08-29.md:77-80`,
  `lessons/data-content-cultures.md:1588-1591`.
- Grep-count predictions hold: Harmony file has 2 `Assert.Inconclusive` (`:42`, `:47`), so 1 after
  Step 5; GameModel file has 5 (`:49,53,57,76,80`), so 3 after Step 5.
- `git grep -i runsetting b2e387db` is empty; `does not falsely pass` occurs only at `SKILL.md:27`;
  the only live gate-command copies are the four docs the plan edits (others are historical or
  mention the category name only).
- The two named baseline failures exist at `b2e387db`/HEAD (`ElkConfigTests.cs:93`,
  `AnimaliaMountWiringTests.cs:127`) and are absent from the main tree's uncommitted copies, so the
  plan's baseline describes committed code, which is what the worktree gets. Consistent.
- No test in a `BindingVerification` file reads `BANNERLORD_GAME_DIR` directly (the 8 files that do
  carry no `BindingVerification` tag), so Step 6 (c)/(d) predictions are sound.
- `Directory.Build.props`: `Nullable` enabled, no `TreatWarningsAsErrors`, `LangVersion 10.0`. No
  `Directory.Build.targets`, no `InitialTargets`/`<Error>` that would fire on a `--no-build` run.
- PyYAML 6.0.3 is installed (Step 10 verify works). `audit_claude_config.py --min HIGH` is a real
  flag. `printenv`, `cygpath`, `mktemp` exist in Git Bash; `BANNERLORD_GAME_DIR` is set.
- `tools/lint_docs.py` returns 1 only under `--fail-on-dead` / `--fail-on-drift`
  (`tools/lint_docs.py:1516-1527`); a bare run always returns 0.
- Commit gates (`check-changelog-changed.sh:44`, `check-commit-subject-version.sh:59`,
  `check-claude-files-tracked.sh:50`, `check-moduledata-validation.sh:55`) all
  `cd "${CLAUDE_PROJECT_DIR}"`, i.e. the main tree, not a manually created worktree.
- No em or en dash in plan prose: the six hits (lines 121, 177, 188, 192, 199, 263) are all inside
  verbatim code excerpts. No secret values.

## Blocking

1. **Worktree path discipline is under-specified for a subagent (lines 11-13, 407-410, 432, 640,
   838).** The plan says "`cd` there for every later step" but every command and every file path
   is repo-relative. A subagent's Bash cwd resets to `E:\repos\TAOM` on every call, and Edit/Write
   need absolute paths. A weak executor that `cd`s once, or that resolves `TAOM.Tests/...` against
   the session root, edits and tests the MAIN tree, and Step 12.5's `git add ... CHANGELOG.md` then
   stages another session's uncommitted `CHANGELOG.md` hunks (the main tree shows ` M CHANGELOG.md`
   now). That is the "never stage another session's hunks" harm. Compounding it, lines 423-425 say
   "if any commit gate denies, read its reason and satisfy it", while every commit gate inspects
   `CLAUDE_PROJECT_DIR` (the main tree), so a deny can name main-tree files the executor must not
   touch. **Fix:** state that every Bash call starts `cd /e/repos/wt-008-binding-gate && ...`, every
   Edit/Write path is prefixed `E:\repos\wt-008-binding-gate\`, add a pre-commit check
   `git -C E:/repos/wt-008-binding-gate rev-parse --abbrev-ref HEAD` prints `plan-008-binding-gate`,
   and add a STOP: "a commit gate's reason names a file outside the 14 in-scope paths, or any
   command shows `E:/repos/TAOM` as its toplevel".

## Non-blocking

1. **Test-count literals are unpadded (lines 362, 441-442, 577, 599, 645, 649, 872).** `dotnet test`
   pads counts (`Failed:     0, Passed:   368`); the plan's own 7c fixture (line 683) uses the
   padded form. An executor grepping `Failed: 0` literally sees no match. Say "counts are
   space-padded; match `Failed:\s+0`" or give a grep regex.
2. **Step 6 (c) output is huge (lines 652-656).** About 335 failures each print a message and stack
   trace, far above the tool's ~30 KB output cap, so the summary line and `exit=` may be cut. Redirect
   to a log (`> "$LOG" 2>&1; echo exit=$?`) and verify with `grep -E 'Failed!|Total:' "$LOG"` and
   `grep -c 'No bin folder with Bannerlord.exe under' "$LOG"`.
3. **`python tools/lint_docs.py` is a no-op gate (lines 364, 776, 834, 879).** It always exits 0
   without a `--fail-*` flag. Either use `--fail-on-drift` (recording its Step 0 baseline, since
   drift may pre-exist) or drop it from Done criteria so it does not read as proof.
4. **No full-suite baseline is recorded in the worktree (lines 344-351, 829-832, 873).** Step 0
   runs only the gate. The failure set of the full suite depends on the live `LOTRLOME_Armory`,
   which the plan itself says another session is editing; "exactly these two, or none" can shift
   between planning and execution. Add Step 0.6: run the full suite once, record the total and the
   failing names; Step 12.2 compares against that record.
5. **Step 11 cross-reference is wrong (line 822).** "Step 12's version read" does not exist; the
   version read is in "Git workflow" (lines 415-416).
6. **Stale doc left behind.** `docs/reference/hooks-catalog.md:42` describes the banner as
   `TEST RESULTS: PASSED/FAILED`; after Step 8 there is a third form, `PASSED WITH SKIPS`. Either add
   the line to scope or list it under "Deferred".
7. **Nullable warnings (lines 473, 534-537, 506-510).** With `<Nullable>enable</Nullable>`,
   `private string _root;`, a `string` property returning `?.Value`, and `null` arguments to
   `string` parameters raise CS8618/CS8603/CS8625 warnings. Not errors (no warnings-as-errors), and
   the existing file is written the same way, but `/deep-review` may flag them; consider `string?`.
8. **Issue-first is deferred past the commit (lines 30, 446-451).** The commit lands on the branch
   before an issue exists. Acceptable under the orchestrator model since nothing merges, but
   AGENTS.md says "before implementation"; the orchestrator should file it before dispatch.
9. **Minor factual slip (lines 78-79, 301-303).** `build.yml:8` also has `workflow_dispatch`, which
   can run the workflow on any ref, so "inert on `bannerlord-1.5.x`" holds only for push triggers.
10. **Step 9.1 replacement text (lines 755-762)** is hard-wrapped inside quotation marks; say
    "without the surrounding quotes" so the quotes do not land in the skill.

## Checklist

| Check | Result |
|---|---|
| Executable from plan + repo alone | Yes, except blocking 1 (path discipline) |
| Every step ends in a command with expected result | Yes; Step 1 is bookkeeping ("Verify: none"), acceptable |
| Current-state excerpts match `b2e387db` | Yes, all code excerpts exact (see mismatches below for two prose counts) |
| TDD order for C# | Yes: Step 2 RED, Step 3 partial GREEN leaving the metadata test red, Step 4 GREEN; hook Step 7 RED before Step 8 |
| Issue-first note | Present (line 30, Step 1); see non-blocking 8 |
| Binding ADRs named | ADR-008, ADR-003/004/005, ADR-002/007 (not engaged), `tests.md`, `hook-authoring.md` |
| Single-owner files | `Directory.Build.props`, `Main/TAOM.csproj`, `IoC.cs`, `SubModule.cs` out of scope with STOP |
| STOP conditions specific | Yes, tied to each Step 6 direction and the "decided skip" |
| Done criteria machine-checkable | Yes, except the no-op `lint_docs.py` line |
| Planned-at SHA and drift paths vs Scope | Consistent: all 11 pre-existing in-scope files listed; CHANGELOG exclusion explained; new files cannot drift |
| Non-deploying commands with `-p:ModuleId=` | Yes, every `dotnet` command (lines 359-362, 437, 641-665, 788, 828-829) |
| No em/en dash in prose, no secrets | Yes (dashes only inside verbatim excerpts) |

## Excerpt mismatches

No code excerpt differs from `b2e387db`. Two prose facts are slightly off:

- Line 66: "Used by about 73 test files through `EnsureLoaded()`": `git grep -l
  "GameAssemblies.EnsureLoaded" b2e387db -- TAOM.Tests` lists 72 files ("about" covers it).
- Line 79 / 301: "triggers on `bannerlord-1.4.5` pushes only": `build.yml:8` also declares
  `workflow_dispatch`.
