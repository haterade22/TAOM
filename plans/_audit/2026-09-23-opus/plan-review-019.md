# Cold review: plan 019 (nullable ratchet, Siege first)

Reviewer: cold read, no prior context. Plan: `plans/019-nullable-ratchet.md` (941 lines).
Template: `.claude/skills/improve/references/plan-template.md`, "Quality bar".
Evidence read this turn: `git show b2e387db:<path>` for every cited repo file, the v1.5.3
taom-src cache (`~/.taom-src/v1.5.3/`) for every engine fact, `.claude/hooks/*.sh` for the
commit gates, `dotnet --version` (10.0.401). No build or test was run (a review; baselines not
re-measured).

**Verdict:** a strong plan, close to executable by a weak model. One blocking defect (commit
commands without the worktree prefix); the rest are small.

## Blocking

1. **Commit commands at lines 516, 751 and 815 lack the `cd /e/repos/wt-plan-019 && ` prefix.**
   Line 516 `git commit -m "chore(build): ..."`, line 751 `git commit -m "fix(siege): ..."`,
   line 815 `git commit -m "docs(nullable): ..."` are each given as a separate "Then:" command.
   The header (lines 12-16) says the shell resets to `E:/repos/TAOM` between calls and every call
   needs the prefix, and line 397 shows the prefixed form, but a weak executor copying the literal
   command runs `git commit` in the main tree. Today nothing is staged there (`git diff --cached`
   empty), so it would fail; if another session has staged hunks at that moment, it commits
   their work (AGENTS.md "Preserve others' work"). Fix: write each as
   `cd /e/repos/wt-plan-019 && git commit -m ... -m ...`.

## Non-blocking

1. **Subject hook reads the main tree's version, not the worktree's** (lines 398-404).
   `.claude/hooks/check-commit-subject-version.sh:59` does `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"`
   and reads `Main/_Module/SubModule.xml` there. The plan tells the executor to read the version
   from the worktree. If the main tree's version changes (a release bump) while the worktree's
   does not, every commit is denied and the executor STOPs with no explanation. Add one line:
   "the subject hook checks against `E:/repos/TAOM/Main/_Module/SubModule.xml`; if the two
   versions differ, STOP and report".
2. **Test-summary strings do not match VSTest's padded format** (lines 633, 664, 717, 468).
   `dotnet test` prints `Failed:     1, Passed:     1, ...` (padded). The plan writes
   `Failed: 1, Passed: 1`. A literal-minded executor may think the output differs. Say "the
   numbers, ignoring spacing", or grep with `-E "Failed: +1, Passed: +1"`.
3. **Step 0.6 expectation is a judgment** (line 468: "about `Failed: 2, Passed: 10235 ...`").
   The hard check is the named failures (fine); state that the counts may differ and only the
   failing test names matter.
4. **Step 1 fallback trigger is fuzzy** (line 503: "If `total` is about 2,028"). A result such
   as `total=3` (for example, from package content files compiled into Main whose paths are
   outside `Main/`) is neither 0 nor about 2,028; Step 1's text gives no instruction for it
   (the STOP list at line 876 does cover it). Say: "If `total` > 0, check whether the paths are
   under `Main\` or `Dependencies\`; if yes, apply the fallback; otherwise STOP."
5. **Full-suite runtime vs the 600000 ms tool timeout is unverified** (lines 21, 467, 742).
   10,239 tests plus a build may exceed 10 minutes; a timed-out call leaves a partial log. Add:
   "if the call times out, re-run with `run_in_background` and read the log when it exits" or
   state the measured planning duration.
6. **Step 8 leaves `docs/features/siege.md` partly stale** (lines 760-769). The component
   diagram (`siege.md:23-36`) and item 3 (`:20`, "sets `__result` to `settlement.GatePosition`")
   still describe the flow without the new no-settlement branch, and the Key Files table
   (`:41-44`) does not list the new test. Add a diagram line and a Key Files row.
7. **Issue-first is recorded but not gated** (line 41). No step confirms the issue exists before
   Commit B, and no commit body carries an issue reference. The template allows orchestrator
   ownership; consider "if the orchestrator gave you an issue number, add `(#N)` to the Commit B
   body".
8. **Lint step reads as a judgment** (lines 807-809). Plain `python tools/lint_docs.py` exits 0
   regardless of findings (no `--fail-on-dead`), and "no line reports a dead link in these three
   files" asks the executor to classify pre-existing findings. Use `--fail-on-dead` against a
   BASE baseline, or diff the lint output against a Step 0 run.
9. **Working-tree cleanliness after builds is assumed** (lines 750, 824). `.gitignore` re-includes
   `Dependencies/_Module/bin/Win64_Shipping_Client/` for some tracked DLLs; if a
   `DisableModuleCopy` build still rewrites a tracked file there, "nothing unstaged or untracked"
   fails. Planning says the fresh-worktree build was clean; not re-verified here (UNVERIFIED).
10. **Maintenance note "never `required` (C# 10)"** (line 899) is true for `Main` only;
    `Dependencies/TAOM.Dependencies.csproj:12` sets `<LangVersion>preview</LangVersion>`. Harmless
    here (no Dependencies code edits), but the procedure is written for any folder including
    `Dependencies/Foundation` (line 908).

## Excerpt check against `b2e387db`

Matched exactly (verified):

- `Directory.Build.props:5-6` (LangVersion 10.0, Nullable enable).
- `Main/TAOM.csproj:9` NoWarn line; `:113` the `Nullable` 1.3.1 PackageReference.
- `Dependencies/TAOM.Dependencies.csproj:9` NoWarn line with `8374;8174`.
- `TAOM.Tests/TAOM.Tests.csproj`: no NoWarn, no Parallelize, no runsettings in the repo.
- Root `.editorconfig`: 22 lines, content identical; it is the only editorconfig/globalconfig/ruleset.
- `BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs`: 66 lines; lines 14-65 match (52-57 elided
  with `...`); line 50 `settlement.GatePosition`; lines 43-44 and 46 as cited for the insert point.
- `Main/SubModule.cs:1545` `_harmony.PatchCategory("Patch8_SiegeCampGuard");` (via `git show`).
- `ActiveSiegeDefenseEvent.cs` and `KingdomSiegeMessages.cs`, whole files.
- `SiegeDefenseService.cs:84-94` (`Resolve`), `:87`, `:118-125`, `:137-139`, `:146`,
  `:230-240`, `:264-271`, `:335`.
- `SiegeDefenseConfigProvider.cs:34` DeserializeObject line.
- `SiegeDefenseServiceTests.cs:373` text; the only test dereference of a `KingdomSiegeMessages`
  property (lines 362-363, 374, 384 pass values, no dereference).
- `KingdomSiegeMessages` / `ActiveSiegeDefenseEvent` used only in `Main/Features/Siege/` and
  `SiegeDefenseServiceTests.cs`.
- Engine (v1.5.3 cache): `BesiegerCamp.cs:26`, `:167`, `:305` and the unchecked
  `siegeCamp1GlobalFrames.Length`/index; `SiegeEvent.cs:798`; `Debug.cs:81` `DebugManager`,
  `:163-174` `Print` forwarding with the `0xFFFFFFFF00000000` mask; `IDebugManager.Print`
  signature; `MatrixFrame.cs:12` `Identity`.
- `docs/features/siege.md:7-9`, `:20`, `:52`; `docs/ai-includes/code-quality.md:428` closing
  fence before `### LINQ Best Practices` (`:430`).
- `docs/reviews/rca-banner-bearers-2026-07-16.md:24`; `rca-field-commission-reset-equipments-2026-08-20.md:16`.
- 252 `rca-*.md` at `b2e387db`; `TroopWeightXmlLoader.cs:70` / `:92`.
- Test exemplars exist: `FiefGrantingBehaviorCaptureGateTests.cs` (GetUninitializedObject at
  `:37`), `Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs` (direct `Prefix` at `:33`).
- Drift check at HEAD `4b5662b2`: empty; main tree has no uncommitted edit to those paths.
- `<Version value="v2.0.30"`; the three commit subjects are 68, 69 and 63 characters.

Mismatches:

1. Plan lines 335-338 attribute to `docs/ai-includes/code-quality.md:392-428` the rules "Use
   `= null!` only on a field that the constructor genuinely cannot assign and that is set before
   first use; never `!` on an engine getter". That section is only a Bad/Good code block; it says
   `!` "hides bug" and shows `T?` returns and `is null` checks. Neither the `= null!` rule nor
   "engine getter" appears anywhere in the file. The rules are stated in the plan itself, so the
   executor is not misled, but the citation overstates the source.
2. Plan line 764 calls `siege.md:52` a "two-sentence paragraph"; it has three sentences. The
   paragraph is also identified by its opening words, so the replacement target is unambiguous.

Not verified (would need a build or test run): the 2,028 / 2,256 counts, the `2 Warning(s)`
baseline (both cited analyzer files exist), 10,239 tests, and that the Roslyn glob
`[{Main,Dependencies}/**.cs]` matches (the plan carries a fallback for it).

## Checklist

| Item | Result |
|---|---|
| TDD order | Pass: RED test Step 3 (with a precise expected failure), GREEN guard Step 4 |
| Issue-first note | Pass (Status line 41, orchestrator); see non-blocking 7 |
| Binding ADRs named | Pass: ADR-002, ADR-007, ADR-008, simplicity criterion, code-quality (citation mismatch 1) |
| Single-owner files | Pass: csproj line 9 each, explicitly authorized; IoC.cs, SubModule.cs out of scope with STOP |
| STOP conditions specific | Pass: tied to warning counts, the RED failure reason, the probe, `!`/pragma bans |
| Done criteria machine-checkable | Pass: greps, counts, `test ! -e`, diff stat against recorded BASE |
| Planned-at SHA and drift paths vs Scope | Pass: `b2e387db`; every in-scope path is in the drift list except CHANGELOG (explained) and the fallback-only new files |
| Non-deploying commands with `-p:ModuleId=` | Pass: every build and test command |
| Every step ends in a command with an expected result | Mostly: see non-blocking 2, 3, 4, 8 |
| No em or en dash in prose | Pass: the only two are inside a code block (line 192) and a code span (line 341) |
| No secret values | Pass |
