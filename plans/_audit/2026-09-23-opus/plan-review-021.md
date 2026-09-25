# Cold review: plan 021 (architecture rule amendments)

Reviewer: cold read against `.claude/skills/improve/references/plan-template.md` "Quality bar", with
every "Current state" excerpt checked at `a39a9c86` (`git show a39a9c86:<path>`). The plan was not
edited.

**Verdict:** not executable by a weak model as written. There are 2 blocking defects; both are cheap
to fix. There are no excerpt mismatches.

## What I verified (evidence read this pass)

- **Rule-text excerpts, items 1 to 12 (plan lines 109-183):** every quoted line matches `a39a9c86`
  byte for byte, including the em dashes at `.ai/review-reference.md:328`, `harmony-patches.md:53`
  and `new-feature/SKILL.md:23`. `.ai/review-reference.md` is 613 lines.
- **ADR excerpts (plan lines 187-199):**
  - ADR-002 matches at 247, 249, 252, 253 (blank), 254 and 291. ADR-002 has 0 `hook` matches.
  - ADR-007 matches at 552, 554, 571, 582, 583 (blank), 584, 753 and 759.
  - ADR-008 matches at 11, 13 and 85. The FORBIDDEN example lists `CampaignTime.Now` and
    `Hero.MainHero`.
- **Code excerpts (plan lines 67-105):**
  - `RefugeService.cs` matches at 48-50, 787-791, 805, 808, 860-874 (15 lines), 876-895 (with the
    try/catch) and 1214.
  - `RefugeServiceTests.cs` matches at 26 and 72.
  - The `CampService.cs` seam header is at 659.
  - `RecruitmentResourceGateHook.cs` 14-15 is a single forward.
  - `ArmyTargetingIoC.cs:19` matches.
  - `CampState` and `RefugeData` are both `public sealed class`.
  - `CampaignTime.cs` line 11 is `public struct CampaignTime : IComparable<CampaignTime>`.
- **Counts at `a39a9c86`:** 217 patch files, 24 of them with `IOn*`, 19 distinct `interface IOn*`,
  0 `Substitute.For<IOn`.
- **Budgets:**
  - Entry docs total 22,189 B (7,114 + 7,081 + 7,994).
  - Unscoped rules total 12,420 B.
  - The new AGENTS row is 255 characters, and AGENTS.md grows by 63 B.
  - The unittest count is 47 (43 + 4).
  - `lint_docs.py --fail-on-drift` on a `git archive a39a9c86` export: exit 0 with 7 `size-warn`
    lines.
- **Proof script:**
  - I extracted it from the plan and ran it on `a39a9c86` copies. `rules` prints `38 problem(s)`,
    matching the planned RED.
  - Each of the 15 "gone" strings occurs exactly once in its file, so GREEN is reachable with the
    planned edits alone.
  - The planned greps (Steps 2-6 and Done criteria lines 764-765) give the stated counts after the
    edits.
- **Hooks:**
  - `config-protection.sh` blocks only `Directory.Build.props`, `settings*.json` and
    `docs/adrs/*.md`. No in-scope file is blocked.
  - `check-changelog-changed.sh` parses the ` -- ` pathspec as the plan says.
  - The main tree has no untracked files under `.claude/{skills,agents,rules,hooks}` today.
- **Other checks:** the three cited audit files and `DECISIONS.md` row 19 are in the commit. The
  plan has no em or en dash outside code spans or the check script, and no secret values.

## Blocking

1. **Step 8.2 (plan lines 684-686) sets an expected result that the planned edits must fail.**
   - `lint_docs.py` prints byte counts in its `size-warn` lines and line numbers in its per-line
     findings. Any edit to a size-warned file therefore adds a `>` line that names it.
   - I applied only the Step 6 edits to `harmony-patches.md` and `csharp-architecture.md` in the
     export, then ran `diff` on the two lint reports. The result:
     - `size-warn ... 26,239 B` became `26,476 B` for `csharp-architecture.md`;
     - `15,043 B` became `15,118 B` for `harmony-patches.md`;
     - a pre-existing `csharp-architecture.md:205` finding moved to `:206`.
   - Step 8.1 (line 681, "the same report-only `size-warn` lines") has the same flaw. The Commands
     table (line 233, "a finding ... the baseline did not have") uses a third, looser test.
   - **Effect:** a literal executor sees a failed verification with no STOP rule for it, so it
     either stops wrongly or improvises.
   - **Fix:** compare finding *kinds per file*, ignoring byte counts and line numbers. For example,
     strip the numbers with `sed -E 's/[0-9][0-9,]*//g'` on both reports before `diff`, or say
     "`size-warn` lines for `csharp-architecture.md` and `harmony-patches.md` change only their byte
     count".
2. **Commands are cwd-relative, but nothing pins the cwd (plan lines 15-16, 228, 288).**
   - `grep ... AGENTS.md`, `python tools/lint_docs.py`, `python <scratchpad>/plan021_check.py` (it
     uses `Path.cwd()` and `git diff HEAD`) and `bash tools/test_hooks.sh` are all relative. So are
     the Edit targets in Steps 2-7 (`In AGENTS.md ...`).
   - A subagent's Bash cwd resets on every call, and a session rooted at `E:\repos\TAOM` resolves
     these against the main tree, which holds another session's uncommitted work.
   - Run from the main tree, `plan021_check.py adr` fails at Step 1 and the executor reports "Step 0
     missing", which is the wrong diagnosis. `rules` shows a false RED of 38, and `dash` scans the
     other session's diff. Worst case, an Edit lands in `E:/repos/TAOM/AGENTS.md`.
   - Only the `git` commands use `-C E:/repos/wt-plan-021`.
   - **Fix:** state once that every Bash command starts with `cd E:/repos/wt-plan-021 && `, and
     every Edit or Read `file_path` is `E:/repos/wt-plan-021/<path>`. Or write every command with
     absolute paths.

## Non-blocking

- **Old rule text survives outside Scope.** `README.md:96`
  (`[HarmonyPatch / GameModel / CampaignBehavior] → IHookInterface → Service → IAdapter`) and
  `.serena/memories/project_overview.md:16` still state the hook layer as the architecture.
  - The STOP condition at lines 785-787 says "a grep turns up another tracked file", but no step
    runs that grep, so an executor may or may not find these.
  - Either add README.md to Scope, or name both files as known, accepted leftovers so the STOP does
    not fire mid-run.
- **Step 9.2 (lines 724-729) leaves the commit body to the executor with no template, and no check
  scans it for dashes.** The `dash` mode reads only `git diff HEAD`. Add a step that runs the dash
  test on `<scratchpad>/plan021-msg.txt`, or give the body text.
- **Two copies of SubModule.xml can disagree (lines 294-295 and 725).**
  - The plan says to re-read the version in `Main/_Module/SubModule.xml`, which is the worktree copy
    at `a39a9c86`.
  - `check-commit-subject-version.sh` runs `cd "${CLAUDE_PROJECT_DIR}"` and reads that tree's
    SubModule.xml, which may be the main tree.
  - Both read `v2.0.30` today. If the main tree cuts a release first, the hook denies the commit.
    The STOP at line 791 catches that, but the plan should name which file the hook reads.
- **Step 7.1 diagram indentation is ambiguous (lines 662-667).** The replacement block is nested
  5 spaces inside a list item, and the original lines 248-249 carry 5 and 3 leading spaces. The rule
  at lines 550-551 ("keep any leading spaces the block shows inside the line itself") does not say
  how much nesting to strip. The proof script checks only the first line, so misalignment passes
  silently. This is cosmetic.
- **The Step 3 line-number rule (lines 548-551) sits inside Step 3.** It also governs Steps 5-7,
  for example feature-builder lines 40 and 55 after the Step 5.2 insertion. Steps 6.3 and 7.1 give
  shifted numbers, but Step 5.2 does not. Move the rule to the executor block at the top.
- **The Step 1 RED expectation (lines 521-524) works as a drift tripwire only for the strings the
  script pins.** A drift inside an excerpted line outside those substrings passes. The drift check
  at line 22 covers it, and that check has no output today (`bannerlord-1.5.x` is `a39a9c86`).
- **Step 0.5 (lines 421-424) gives the ADR commit subject but no exact command.** The step is
  Mike's, so this is acceptable. Step 1.2 then asserts a `docs(adr): v2.0.30 - ...` subject, so the
  two must stay in sync.
- **The template's other checks all pass:**
  - TDD is not applicable (no C#), and the proof script's RED/GREEN is a sound analogue.
  - Issue-first is recorded (line 37).
  - ADR-002, 007, 008 and 011, output-style and simplicity-criterion are named with summaries.
  - The single-owner files are excluded (lines 223-224, 273).
  - `-p:ModuleId=` is on both dotnet commands (lines 239-240).
  - The planned-at SHA is filled, and the drift paths equal Scope minus `CHANGELOG.md`, plus the
    three ADRs.
  - The Done criteria are all commands.

## Excerpt mismatches

None. Every quoted line and line number in "Current state" matches `a39a9c86`.
