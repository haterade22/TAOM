# Refuter: harness and docs lanes (2026-11-12-fable)

Baseline `7087fa633`, worktree `wt-baseline`. Read-only. Findings checked: DX-01, DX-02, DX-03, DX-05,
DX-06, DX-09, DOC-01, DOC-02, DOC-03, DOC-04, DOC-08. Blocks are appended as each check finishes.

## DX-01: /ship's final /verify reuses the first run

**Verdict: NARROWED** (design conflict with an always-on rule the lane quoted only in part).

**Re-read:** `.claude/skills/ship/SKILL.md:19` (Phase 1 `/verify`), `:31` (Phase 4 `/verify`), `:23-28`
(Phases 2 and 3 implement fixes), `:39` ("Do not commit before Phases 1-3 are clean");
`docs/ai-includes/completion-workflow.md:74,79`; `.claude/skills/verify/SKILL.md` in full;
`.claude/rules/evidence-over-claims.md:25-41`; `.claude/hooks/mark-verification-run.sh` and
`.claude/hooks/check-verification-evidence.sh` in full; `.claude/hooks/block-no-verify.sh:7-23`.

**Ran:** `git grep -n "verification-ran\|verification-reminded"` (readers: only the two hooks, and
`tools/test_hooks.sh:1411,1436,2223-2315,2829,3140` as test fixtures); `git grep -ln "skills/ship"`
over tools, .claude, .githooks (no hit); `ls tools/evals` (9 cases; none routes or pins /ship or
/verify); grep of `tools/tests/test_ai_documentation.py` (reads `.agents/skills` only).

**What the hooks key on.** `mark-verification-run.sh:84-110` touches
`$CLAUDE_PROJECT_DIR/.claude/logs/.verification-ran` when a command segment starts with `dotnet build`,
`dotnet test` or a build.ps1 invocation. `check-verification-evidence.sh:56-78` reminds once per streak
when a dirty `*.cs` file (diff, cached diff or untracked, in the payload cwd's tree) has an mtime newer
than that marker. No commit gate reads the marker (`block-no-verify.sh:7-23` says the build gate was
removed on purpose). No test, linter or skill eval pins the ship or verify skill text.

**Corrections to the lane:**
- The lane quotes `evidence-over-claims.md:40-41` ("A verification you ran this turn ... re-running it
  adds none") but not `:25` ("run it now") and `:29` ("Not verification: an earlier run"). The reuse
  allowance is scoped to *this turn*. A /ship almost never fits in one turn: the deep-review lenses run
  as background subagents (harness-facts.md:55, background by default) and Codex runs in the background
  and resumes on a notification (`ship/SKILL.md:24-25`). So Phase 4 is usually in a later turn than
  Phase 1, and citing the Phase 1 report there is "an earlier run" under the always-on rule. The skill
  edit alone would contradict an always-on rule.
- "The verdict is the same by construction when the inputs are the same" is not true of every /verify
  step: Step 1b (NuGet advisory feed, `verify/SKILL.md:27-37`) depends on the network and time, Step 2d
  on the installed game DLLs, Step 6 on `gh` and new game logs. Step 6 is information only; 1b can turn
  a PASS into a FAIL between the two runs.
- Phase 2 and 3 implement fixes (`ship/SKILL.md:25,28`), so the duplicate exists only when no lens and no
  Codex finding led to an edit. How often that happens was not measured by the lane (UNVERIFIED).

**Stop hook and gates:** a reuse would not make the Stop hook complain when the tree is truly unchanged:
the marker from Phase 1 is still newer than every dirty `.cs`. One edge: a fix made and then reverted to
identical content keeps the hash equal but gives the file a newer mtime, so the Stop reminder fires once
(harmless, but the fix sketch's "unchanged" test and the hook would disagree). No commit gate keys on the
marker.

**Conditions for behaviour preservation:**
1. The maintainer amends `evidence-over-claims.md:29,40-41` to allow citing an earlier run whose recorded
   tree fingerprint still matches (or the skill restricts reuse to a Phase 1 run in the same turn, which
   makes the saving nearly nil). That rule file has 14 B of headroom under the unscoped cap, so any added
   words need DX-03's deletions first.
2. The fingerprint covers HEAD, `git diff HEAD --binary`, untracked file contents, and live TAOM_Map and
   Armory files newer than the Phase 1 time.
3. Step 1b (NuGet audit) re-runs anyway (its cost was not measured), or the reuse is limited to Steps 1, 2, 2b, 2c,
   2d and 2e; Step 6 re-runs (information only, cheap).
4. The same text change lands in `completion-workflow.md` step 5.

**Recalibrated:** impact LOW to MED (about 500 s, only on a /ship with no fix; frequency UNVERIFIED);
effort S for the text, plus a maintainer decision on the always-on rule; risk MED (it relaxes a
verify-before-claim rule that the RCA history in `ship/SKILL.md:9` exists to protect). Plan candidate:
no (decision), until the maintainer amends the rule.

## DX-02: /review-codex 3f runs the full suite per bug

**Verdict: NARROWED** (the texts differ in cost, they do not conflict; and the full suite does not run
afterwards in every path).

**Re-read:** `.claude/skills/review-codex/SKILL.md:232-239` (3f steps 1 to 5), `:264` ("Build and test after
EVERY batch of fixes", a second copy the lane missed), `:13-17` (argument forms: a `.md` path means
"verify that existing Codex review, implement fixes"); `.claude/rules/evidence-over-claims.md:35-38`;
`.claude/skills/ship/SKILL.md:28,31`; `.claude/skills/improve/workflows/review.js:247-259,275`.

**Ran:** `git grep -n "review-codex"` over `.claude/skills`, `.claude/agents`, `docs/ai-includes`, `.ai`
(callers: /ship Phases 2 and 3, /improve's review lead "as /review-codex Phase 3", /investigate and
/adopt-external as advice); grep of `review.js` for `dotnet test` and "full suite".

**Corrections to the lane:**
- "The two documents give an agent opposite instructions": not so. `evidence-over-claims.md:35-38` is "the
  cadence that keeps it cheap", a floor: a filtered run while iterating, the full suite at a work-item
  boundary. Running the full suite after each fix is stricter, not contrary; each fix changes the tree, so
  the earlier run is invalidated and `:40-41` ("re-running it adds none") does not apply. The finding is a
  cost finding, not a contradiction.
- The fix must also change `review-codex/SKILL.md:264`, which repeats the per-batch full run.
- "The full suite still runs once after the last fix (Phase 4)" holds only inside /ship
  (`ship/SKILL.md:31`). Standalone `/review-codex <path.md>` (`:16`) ends 3f, 3h, 3i with no full run: with
  the lane's edit, a standalone invocation would finish on a filtered run only. /improve's lead runs the full
  suite itself (`review.js:258,275`), so that path is safe.

**Conditions for behaviour preservation:**
1. The new 3f text itself ends with "after the last fix, the full suite (`/verify` Steps 2 and 2b)", so
   the standalone path keeps one full run; it must not rely on /ship Phase 4.
2. Line 264 changes in the same edit.
3. The filtered command comes from `graphify_taom.py tests`, which reads the code graph; a stale graph
   (68 of 69 sessions per the lane's own banner count) can miss a new test file, so the text should say
   to run `refresh --if-stale` first or accept that the final full run is the real gate.
4. Any `dotnet test` line written into the skill keeps `-p:DisableModuleCopy=true -p:ModuleId=`
   (`lint_docs.py:1292-1311` `check_unflagged_dotnet` scans `.claude/skills` and gates `--fail-on-drift`).

**Recalibrated:** impact LOW (N-1 full C# suites of about 129 s saved when Codex confirms N bugs; N is
usually small, not measured); effort S; risk LOW with condition 1, MED without it.

## DX-03: recover headroom under the two always-on caps

**Verdict: NARROWED.** The two CLAUDE.md deletions hold; the output-style "Also" deletion changes what an
always-on rule tells every session and is the maintainer's call. The CLAUDE.md saving is smaller than
stated.

**Ran:** `python -B tools/lint_docs.py --summary` (exit 0, `context_budget: 5`, `total_findings: 5`);
`python -B tools/lint_docs.py --context-budget-json` (entry docs CLAUDE.md 8,156 + AGENTS.md 7,677 +
orientation.md 9,757 = 25,590 B against `ENTRY_DOCS_MAX_BYTES` 25,600; unscoped rules total 16,370 B
against `UNSCOPED_RULES_MAX_BYTES` 16,384, output-style 3,816, model-effort-selection 2,258); `wc -c`
(raw CRLF sizes CLAUDE.md 8,270, AGENTS.md 7,772, orientation.md 9,838, output-style 3,881,
model-effort-selection 2,291); `file` (CLAUDE.md and output-style.md are CRLF); scratch
`verify-harness-docs/bytes.py` (LF-folded bytes of each deletion). The lane's cap numbers reproduce
exactly. `lint_docs.py:2086-2108`: `--drift-only` (the pre-commit and Claude commit hook form,
`.githooks/pre-commit:95`) runs `check_context_budget`, and `:1150-1153,1189-1192` make "size" and
"rules-size" gating kinds, so the next 11 B or 15 B addition does fail a commit.

**Deletion 1, `CLAUDE.md:98-103` "## Model & Effort Selection"** (312 B by the script, as the lane says).
- Surviving copy: `.claude/rules/model-effort-selection.md:3-5` ("give each subagent the cheapest model
  and effort that fits") and `:7-12` (the tier table with `fast-reader` Haiku/low, `implementer`
  Sonnet/medium, `architect` Opus/high). That rule has no `paths:` (`--context-budget-json`:
  `scoped: False`), so it loads wherever CLAUDE.md loads: every session, every custom and general-purpose
  spawn, after compaction (harness-facts.md:31,34). Explore and Plan get neither. Same contexts: safe.
- The Subagents bullet does list the tier agents (`CLAUDE.md:92-94`: "tiers `fast-reader`,
  `implementer`, `architect` (below)"), but it ends with "(below)", which points at the section to be
  deleted. The edit must change "(below)" to the rule's name or drop it, or it leaves a dangling pointer.
- Readers of the lines: `tools/run_skill_evals.py:35-39` extracts only the `## Skills` section (up to the
  next `## `), so deleting a later section does not change what it stages; `tools/tests/test_run_skill_evals.py:23`
  uses a `## Subagents` fixture of its own. `git grep "Model & Effort\|Tier agents\|model-effort-selection"`
  over tools, `.claude/hooks`, `.githooks`, `.claude/skills`, `TAOM.Tests`: no reader. The linter's
  `rule_drift` checks (`lint_docs.py:1242-1311`) read rule, skill and ai-includes text for memory
  citations, symbols and unflagged dotnet only: no effect.

**Deletion 2, `CLAUDE.md:93` ", so never pass `model`"** (23 B).
- Surviving copy: `model-effort-selection.md:30` ("Never pass `model` to `deep-reviewer`"), always-on, same
  contexts; also `deep-review/SKILL.md` (on invoke) and `harness-facts.md:52` (path-scoped). Safe. The fix
  must say whether only the clause goes or the whole parenthetical: "Opus 5.5 at max effort is set in its
  definition" is the reason, and the rule at `:30` gives no reason; keep the reason or accept losing it.

**Deletion 3, `output-style.md:55-59` "**Also:**"** (470 B; the lane's line numbers 55-57 and the link
at 58 are off: the paragraph is 55-59 and the link is `:61-62`).
- The items are in `docs/reference/writing-tells.md` (rows 11, 12, 13, 18, 25 and lines 101-102, re-read).
  But that doc is not a rule: no `paths:` rule matches it, and nothing imports it. It loads only when a
  session opens it (its own header, `writing-tells.md:3-5`, says to read it before a release note, long
  issue, feature doc or RCA). `output-style.md:35-36` says the tells "also apply to chat, status and code
  comments", and nothing makes a chat reply open writing-tells. So the deletion removes these eleven tells
  from the guidance every session and spawn carries, chat included. The lane's own risk line admits this
  ("the 'Also' items leave the always-on set").
- `writing-tells.md:3-4` says "The short form loads in each Claude session from output-style.md Part 2";
  after the deletion the short form is the ranked five only, and that sentence should say so.
- ADR-011:92-93 forbids copies of another tier's text in the first three tiers, which supports the
  deletion; ADR-011's own Bad example is a CLAUDE.md row restating a feature doc. But the rule file was
  written as a deliberate "short form" (writing-tells.md:3), so this is a decided summary, not an accident.
  Whether the "Also" list earns 470 B every session is for the maintainer (an adherence question no eval
  here measures).

**Conditions:**
1. Deletions 1 and 2: change "(below)" at `CLAUDE.md:94`; decide whether the reason clause at `:93` stays.
2. Deletion 3: the maintainer accepts that the eleven "Also" tells reach a session only when it opens
   writing-tells.md; update `writing-tells.md:3-4` to match.
3. Optional Skills-table step: the lane is right that `run_skill_evals.py` stages that table, so it needs a
   paid eval on the maintainer's word; out of an unattended plan.

**Recalibrated:** impact MED (it is the only way to unblock any growth of either always-on set; saving
about 335 B to 343 B under the entry cap, not "about 400 B", and 470 B under the rules cap); effort S;
risk LOW for deletions 1 and 2, MED (meaning change) for deletion 3. Plan candidate: yes for 1 and 2; 3
only on the maintainer's word.

## DX-05: parse the edited path in check-polearm-shield-parity.sh with a bash regex

**Verdict: NARROWED.** The saving is real; the lane's "same field, same unescape" is wrong: copying the
`check-verification-evidence.sh` unescape verbatim changes the parsed path on every Windows payload.

**Re-read:** `.claude/hooks/check-polearm-shield-parity.sh` in full (parse `:33-41`, uses of `FILE_PATH`
at `:43,52-60,70,111`); `check-verification-evidence.sh:46-54`; `tools/test_hooks.sh:3367-3402` (7k: the
FAIL, mute and no-python cases, all on one payload built with `json.dumps` of a sandbox path).

**Ran (read-only, scratch only):** `verify-harness-docs/dx05_payloads.py` wrote 11 payloads;
`verify-harness-docs/dx05_compare.sh` runs on each the current pipeline (`:39-40` verbatim), **B1** (the
regex and unescape of `check-verification-evidence.sh:46-51` with `cwd` replaced by `file_path`, which maps
`\` and `\/` to `/`), and **B2** (the same regex, `\` unescaped to one `\`, the current semantics).
`command -v jq`: not found, so the grep branch is the live one. Results:

| Payload | current | B1 | B2 |
|---|---|---|---|
| Windows path, doubled backslashes | `E:\repos\TAOM\...\troops.xml` | `E:/repos/TAOM/.../troops.xml` DIFFERS | same |
| path with spaces (Armory under Steam) | backslash form | slash form DIFFERS | same |
| path with an escaped quote `we\"ird` | `E:\repos\we\` (truncated at the quote) | full path, slash form DIFFERS | `E:\repos\we\"ird\x.xml` DIFFERS |
| `file_path` after `content` that mentions the key escaped | `E:\repos\TAOM\docs\INDEX.md` | slash form DIFFERS | same |
| empty payload | empty | empty | same |
| forward-slash path | same in all three | | |
| pretty-printed JSON | backslash form | slash form DIFFERS | same |
| path ending in `\` | `E:\repos\dir\` | `E:/repos/dir/` DIFFERS | same |
| JSON-escaped slash `\/` | `E:\/repos\/a.xml` | `E:/repos/a.xml` DIFFERS | same |
| `\u00e9` escape | left as `\u00e9` | left, slash form DIFFERS | same |
| no `file_path` key | empty | empty | same |

So B2 matches the current output on every payload except the escaped quote (which no Windows path can
hold; B2 is the more correct of the two there). B1 differs on every payload with a backslash.

**Does the difference matter downstream?** `FILE_PATH` feeds the `case` globs (`:52-55`, slash-blind), `grep
-q "<EquipmentRoster" "$FILE_PATH"` (`:56`, Git Bash opens both forms), `git -C "$(dirname
"${FILE_PATH//\//}")"` (`:70`, already converts to `/`) and the no-python message (`:111`), whose text
is hashed for the mute (`:85`). With B1 the verdicts stay the same; the message shows a slash path, and an
existing mute hash written by the old form no longer matches, so one already-delivered "did not run" line
repeats once after the change. With B2 nothing changes.

**Timing:** `verify-harness-docs/dx05_time.sh`, 20 parses each, three runs: current pipeline 134, 168,
164 ms per parse; B2 3.8, 5.4, 3.8 ms. The lane's direction holds (the pipeline is most of the hook's cost
on an irrelevant edit). One correction: `config-protection.sh` is PreToolUse, so it does not run in
parallel with the two PostToolUse hooks; the PostToolUse pair (`check-polearm-shield-parity.sh`,
`check-feedback-memory.sh`) runs in parallel, and the polearm hook sets that pair's wall time.

**Conditions:**
1. Use the B2 unescape (`\` to `\`), not a verbatim copy of `check-verification-evidence.sh:50-51`; or
   accept B1 knowing the message text and the mute hash change form.
2. Keep the `jq` branch (`:35-36`).
3. Add 7k cases for a Windows path with doubled backslashes and one with spaces (the current 7k payload is a
   sandbox path whose form depends on `$SANDBOX`), run the hook suite as the dispatch rules require.

**Recalibrated:** impact LOW (about 0.1 s per Edit or Write on this desktop; calls per session not
counted); effort S; risk LOW with condition 1.

## DX-06: cut session-start lines that repeat the harness's context or contradict each other

**Verdict: NARROWED.** Part 1 holds (plus a catalogue row); part 2 holds only in its conditional form;
part 3 (fold the two worktree checks) is not behaviour-preserving and the lane's premise for it is wrong.

**Re-read:** `.claude/hooks/session-start.sh` in full (376 lines); `tools/graphify_taom.py:445-472`
(`cmd_status`, the "refresh:" hint at `:466-468` printed whenever the state is not fresh, `--brief` drops only
the file list) and `:949-998` (`_refresh_detached` messages: "background refresh started (pid ...)" or
"background refresh not started: <reason>" for the opt-out, a held lock, Bannerlord running, a failed
process check or a failed spawn); `tools/test_hooks.sh:965-983` (5b2), `:3493-3650` (graph hang, native
archive, `ss_retro`, `ss_wt`, `ss_hp`), `:3836-3939` (7n Dead ends, MEMORY.md banner);
`docs/reference/hooks-catalog.md:21`; `harness-facts.md:31,34`.

**Ran:** `grep -n` of `tools/test_hooks.sh` for `Branch:`, `Recent commits`, `Uncommitted:`, `refresh:`,
`WORKTREES`, `lingering` (only `WORKTREES` is pinned, at `:3623-3631`); `git grep` of `tools/tests` for the
hint text and `--brief` (no pin of the hint); `git grep` of skills, rules, ai-includes, CLAUDE.md, AGENTS.md,
`.ai`, `docs/features`, `docs/reference` for `Branch:`, `Recent commits`, `WORKTREES`, `lingering under`,
`graphify code graph: STALE`, `TAOM Session Context` (no doc tells a session to act on the branch, commit
or worktree lines; `hooks-catalog.md:21` describes them); `git -C E:/repos/TAOM worktree list | wc -l` (41);
`ls -d E:/repos/TAOM/.claude/worktrees/*/` (0 directories today).

**Line by line:**

| Lines | What it prints | Duplicate? | Test rows | Doc that acts on it |
|---|---|---|---|---|
| `:61-63` | `Branch: <name>` | yes: the harness gitStatus has "Current branch" for the same directory, in the main session (Explore and Plan get neither, harness-facts.md:31) | none | none; `hooks-catalog.md:21` lists "branch" |
| `:360-363` | "Recent commits:" and `git log --oneline -5` | yes: gitStatus "Recent commits" is the same five lines | none | none; `hooks-catalog.md:21` lists "recent commits" |
| `:365-370` | Uncommitted counts | no (the lane keeps it) | none | `verify/SKILL.md:196` has its own line |
| `:203-209` | the STALE line plus "refresh: ... run it in the background" | the hint is redundant only when `:216-221` then prints "background refresh started" | none | `deep-review/SKILL.md:58`, `new-feature/SKILL.md:17` tell a session to run `refresh --if-stale` themselves, independent of the banner |
| `:255-276` | WORKTREES when `git worktree list` has more than 15 | no | `:3623-3631` (21 prints, 15 silent, failure UNCHECKED) | `ship/SKILL.md:35` runs `worktree_audit.py` on its own |
| `:278-289` | "Worktrees: N lingering under .claude/worktrees" when N > 0 directories exist | no, see below | none | none |

**Corrections to the lane:**
- "`ss_retro` at `:3562`, `ss_wt` at `:3618` pin several of these outputs, so the suite needs matching
  edits": `ss_retro` pins only the HARNESS RETRO lines (`:3568-3596`), which no part of the finding cuts;
  `ss_wt` pins only the WORKTREES line (part 3). Parts 1 and 2 need no test change; they need new rows if
  the maintainer wants the deletion pinned.
- Part 3: the second check is not contained in the first. It fires at one or more directories, the first only
  above 15 registered worktrees; and it counts directories with `find` (`:284`), which also sees a folder
  left behind after `git worktree prune` or a failed removal, which `git worktree list` no longer reports.
  Folding it into the WORKTREES line silences the lingering-directory note whenever there are 15 or fewer
  worktrees. Not behaviour-preserving.
- Part 2: when the refresh does not start (Bannerlord running, lock held, opt-out), the hint is the only
  instruction left, so "print without the hint" must apply only after a "started" answer. `head -1` on the
  status output (the lane's second option) would drop the hint in every case.
- `hooks-catalog.md:21` must lose "branch, recent commits" in the same change.

**Conditions:**
1. Part 1: delete `:61-63` and `:360-363`, edit `hooks-catalog.md:21`. Valid while the harness injects
   gitStatus (no `includeGitInstructions` key in `.claude/settings.json`; checked by `git grep`).
2. Part 2: print the STALE line first, run the detach, and print the hint only when its answer is not
   "background refresh started"; or add `--no-hint` and re-print the hint on a not-started answer.
3. Part 3: drop it, or keep both thresholds.

**Recalibrated:** impact LOW (two git starts and about 200 B per session start, one redundant
instruction on a stale-graph start); effort S; risk LOW for parts 1 and 2 under the conditions, MED for part
3 (silences a note).

## DX-09: correct /verify's stale counts and raise its discovery floors

**Verdict: CONFIRMED** for the numbers and the floors; the `-B` part is a separate, small behaviour change,
not a correction, and the lane's `:57` citation points at the wrong file.

**Re-read:** `.claude/skills/verify/SKILL.md:52` ("A total under 20,000 (today about 31,700)"), `:57`
(`python -m unittest discover -s tools/tests -t . 2>&1`, no `-B`), `:62-63` ("About four minutes"), `:65-66`
("fewer than 2,500 tests"); `.ai/verification.md:13` (the `python-tests` row, also without `-B`), `:55`
(the 2,500 floor), `:113`; run folder `baseline.md:10-11` (`Total: 39259`, `Ran 8578 tests in 363.306s`).

**Ran:** `git grep -n "2,500\|2500 tests\|20,000\|20000\|31,700\|31700\|four minutes"` over `.claude`, `.ai`,
`docs/ai-includes`, `tools`, AGENTS.md, CLAUDE.md: the floors appear only at `verify/SKILL.md:52,65` and
`.ai/verification.md:55` (the other hits are unrelated constants: `_shellwords.py:728`, face budgets, hex).
A second `git grep -E` over `docs/` (excluding reviews and the changelog archive) for the floor phrases: no
hit. `git check-ignore -v` on `.claude/hooks/__pycache__/x.pyc` and `tools/__pycache__/x.pyc`: both
ignored by `.gitignore:113`.

**Corrections to the lane:**
- "`.ai/verification.md:55` repeats the 2,500 floor. `:57` runs `python -m unittest` without `-B`": the
  command without `-B` is `verify/SKILL.md:57`; `.ai/verification.md:57` is prose. `.ai/verification.md:13`
  and `:113` also spell the command without `-B`.
- `-B` is required by the /improve dispatch rules, not by a repo rule. `__pycache__/` is gitignored, so the
  current form dirties no tree; `-B` stops the write (each run then compiles every module from source,
  cost not measured). It is a defensible change but not a stale-number fix; keep it a separate item or drop it.

**Conditions:**
1. New floors stay well below every legitimate total. Tests that need the install skip and still count in
   `Ran N` (`verify/SKILL.md:62-63`), so a laptop run should stay above 6,500; the laptop total itself is
   UNVERIFIED. 30,000 and 6,500 are 76% of today's totals.
2. Change both copies (`verify/SKILL.md:65` and `.ai/verification.md:55`) together; `.ai/verification.md`
   is read by Codex, so `python tools/reviewctl.py lint` (the `.ai` check named at `.ai/verification.md:62-66`)
   runs on the edit.
3. Drop "today about 31,700" and "About four minutes" rather than updating them (or give the command and
   date).

**Recalibrated:** impact LOW to MED (a discovery break losing up to 70% of the Python tests, 8,578 down to
2,500, or 49% of the C# tests, 39,259 down to 20,000, passes today); effort S; risk LOW.

## DOC-01: fix the wrong statements an author would act on

**Verdict: NARROWED.** Every quoted line reads as the lane says, but two of the lane's "sources of truth"
are themselves stale, one proposed repoint would rewrite an instruction that is already done, and the lane's
path and comment lists are incomplete.

**Re-read and ran:**

| Group | Quoted line | As the lane says? | Source of truth, re-checked |
|---|---|---|---|
| cultures.md wanderers | `docs/cultures.md:130-131` "12 Wanderer NPCs", `_0` through `_11` | yes | The lane's oracle, `recipe-add-a-culture.md:357-362` (measured 2026-09-05: "210 entries over 20 cultures, 17 at exactly 10"), is stale. Re-counted with grep over `Main/_Module/ModuleData/taom_wanderers.xml`: 321 `spc_wanderer_` ids over 30 cultures; 23 cultures at 10, 2 at 12, 4 at 13, 1 at 15. "Author 10" still holds; the numbers to cite are today's. |
| cultures.md equipment path | `docs/cultures.md:140` "Create `Main/_Module/ModuleData/taom_equipment_sets_{culture}.xml`" | yes | `git ls-files Main/_Module/ModuleData \| grep equipment_sets`: 28 files, 0 outside `equipmentsets/` (the recipe's "20 files" is also stale; `cultures.md:18-19` has 28 and the right folder). `docs/cultures.md:217` names the file without a folder (not wrong, but the same edit should add the folder). `new-culture-authoring.md:299` has the right path. |
| dr3 licence line | `docs/migration/dr3-maintenance.md:236` "via clean-room rewrite under MIT" | yes | `provenance-register.md:21` (do not claim clean-room unless the procedure was followed), `:75` (`UNKNOWN`, `behavioural-port`, `uncleared`), `:637-647` (the retraction). `module-dependencies.md:845-847` already flags the line. |
| INDEX regression row | `docs/INDEX.md:19` "auto-memory `feedback_<symptom>.md` files" | yes | `LESSONS-LEARNED.md:5`: the `feedback_*.md` lesson files "were retired in the 2026-06-24 memory reorg". Nuance: six `feedback_*.md` files still exist in this machine's memory folder, but they are machine preferences (e-drive, model choice), not regression symptoms, and machine-local in any case. |
| INDEX chapter count | `docs/INDEX.md:29` "its 38 chapters" | yes | `git ls-files docs/modding \| grep -vc README` prints 40; `git ls-files 'docs/modding/*.md' \| wc -l` prints 41, matching `docs/modding/README.md:334` (dated 2026-11-03). |
| moved tools | `runes.md:47,101,111,125`; `blender-build-prompt.md:98`; `equipment-rosters.md:634-647`; `troops-and-parties.md:576-577` | yes, all four tools exist only under `tools/oneoff/` (`git ls-files`) | The list is incomplete: `runes.md:138,179`, `equipment-rosters.md:43,484,686,688` and `troops-and-parties.md:570` name the same tools. |
| stale code comment | `MBMapScene_GetBattleSceneIndexMap_Patch.cs:18-19` "commented out in SubModule.cs" | yes | `Main/SubModule.cs:495` calls `TryPatchCategory("Patch0_BattleScenes")`, re-enabled 2026-06-01 per `:490-494`. A second stale comment the lane missed: `Main/Features/DevConsole/PatchReportFormatter.cs:14-16` gives `Patch0_BattleScenes` as the example of a category that is "deliberately never registered". |

**Corrections to the lane:**
- `blender-build-prompt.md:98` is not a path to repoint. It is a task instruction ("fix or remove the stale
  `tools/build_erebor_kitbash.py` reference in `README.md`"), and `git grep build_erebor_kitbash -- docs` finds
  no such reference left in any README: the instruction is done. Delete the clause; repointing it would
  tell a future session to fix a README that is already fixed.
- `docs/migration/` is a historical folder by the linter's own definition (`lint_docs.py:118-123`
  `STALE_VERSION_EXEMPT_PREFIXES`, `:671-681` `_is_historical_doc`). Repointing the 1.4.3 migration templates
  to `tools/oneoff/` is still useful (a reader would otherwise get "file not found"), but it is an edit to a
  point-in-time record, so a one-line "moved to tools/oneoff/ on <date>" note is the lighter fix.
- Cite current counts with their command, not the recipe's 2026-09-05 numbers (CLAUDE.md: a count names
  the command that prints it, or its date).

**Conditions:** prose and comment edits only; `PatchReportFormatter.cs` and the patch file are comment
lines (no behaviour); `Main/SubModule.cs` is not touched. `docs/cultures.md` is not on the skip list
(`Main/_Module/ModuleData/**` is, so no data file changes).

**Recalibrated:** impact MED (cultures.md is the checklist an author reaches from INDEX and
new-culture-authoring.md); effort S; risk LOW.

## DOC-04: the modding handbook still lists defects in other docs that were fixed since

**Verdict: NARROWED.** All six "fixed since" claims hold, and there is a seventh the lane missed; but the
fix sketch ("delete the six entries") contradicts the convention of `open-questions.md`, which marks rows
RESOLVED instead of deleting them.

**Re-read (each handbook line, then its target):**

| Handbook line | Says | Target now | Fixed? |
|---|---|---|---|
| `configs-balance.md:410` | race-age doc says human 85, per-race 6 to 30 | `race-age-system.md:65-83`: "Every race comes of age at 18", human 200 | yes |
| `configs-factions-and-world.md:444-446` | diplomacy.md says 5/11/10/33 | `diplomacy.md:255`: "38 Permanent, 29 Natural, 7 Neutral and 79 Hostile" (measured 2026-10-27); no 11 or 33 text | yes |
| `id-cheatsheet.md:466` | naming doc says 26 of 27 K are battania (`:88`) | `taom-map-settlement-naming.md:91`: "`Culture.battania` (all of them since `castle_K4` moved to Khand on 2026-10-30)" | yes |
| `module-map.md:573-575` | main-map-vista says no `AssetPackages/` | `main-map-vista.md:142-143`: "On 2026-11-03 it has an `AssetPackages/` directory (5 packs ...)" | yes |
| `open-questions.md:154` | localization-map says 11 files, cites `AllLanguageDirs_HaveExactlyElevenLanguageFiles` | `localization-map.md:13` says 23 files per language (2026-11-03) and `:18` cites `LanguageDataXmlTests`; `git grep AllLanguageDirs_HaveExactly -- TAOM.Tests` finds nothing | yes |
| `open-questions.md:81`, `equipment-rosters.md:405-407` | xml-data rule cites `feedback_equipmenttype_civilian_required.md` | `git grep "feedback_equipmenttype\|equipmenttype_civilian" -- .claude`: empty | yes |
| `id-cheatsheet.md:464` (missed by the lane) | xml-data rule's region-code line says `EN=Rohan`, `B=Dunland`, `K=Easterlings` (`xml-data.md:37`) | `.claude/rules/xml-data.md:26-27` now only links the cheatsheet; no such line remains | yes |

**Corrections to the lane:**
- `open-questions.md:1-11` defines the file as the list of questions the handbook could not answer, and
  `:81` shows its convention for an answered row: "RESOLVED 2026-09-12 (#569): ...". Three rows already
  carry RESOLVED. So for `open-questions.md:81,154` the consistent fix is a RESOLVED prefix with the date and
  the evidence, not a deletion; deletion fits the chapter "stale" bullets (`configs-balance.md:410` and the
  others), which are notes, not an archive.
- `recipe-add-a-culture.md:357-362` (the "cultures.md is stale in three places" bullet) is half fixed: its
  "Gaps table" claim was fixed 2026-11-03 (`docs/cultures.md:199`), and its own counts (20 files, 210
  wanderers over 20 cultures) are stale (DOC-01 re-count: 28 files, 321 wanderers over 30 cultures). The
  lane lists it as "still true". It needs an edit too, or it goes away with DOC-08.

**Conditions:** prose only, in `docs/modding/`; keep `open-questions.md`'s RESOLVED convention.

**Recalibrated:** impact LOW (wrong pointers in a handbook that names them as stale); effort S; risk LOW.

## DOC-08: three culture-creation procedures overlap

**Verdict: NARROWED.** The overlap is real, but there are four culture docs, not three; the lane's
inbound-link counts are wrong; and its fix (cut `docs/cultures.md` to "Current State" plus a pointer to
`new-culture-authoring.md`) would create a reading loop and drop content that only `docs/cultures.md` holds.

**Re-read:** `docs/cultures.md` headings (`grep -n '^#'`: Current State `:3`, sharing table `:21`, the
12-step checklist `:52-196`, tavern mercenaries `:83-105`, Gaps `:197`, Summary `:210`);
`docs/ai-includes/new-culture-authoring.md:8,442`; `docs/modding/recipe-add-a-culture.md:27-94,357-362`;
`docs/modding/cultures.md` (455 lines, a handbook chapter the lane did not name: `:231-` "Add a culture
from nothing", `:298`, `:419`, `:448`); `docs/features/tavern-mercenaries.md:60-68,161`.

**Ran:** `git grep -n "cultures\.md#"`: three anchor links (`docs/modding/README.md:60`,
`docs/modding/troubleshooting.md:45,170`), all to `docs/modding/cultures.md` (relative from
`docs/modding/`), none to `docs/cultures.md`. Then a grep for links that resolve to `docs/cultures.md`
(`](cultures.md` from `docs/`, `../cultures.md` from one level down, `docs/cultures.md`):
`new-culture-authoring.md:8,442`, `culture-playability-wiring.md:360`, `kingdom-creation.md:641`,
`id-cheatsheet.md:522` ("which cultures share rosters", the sharing table), `npcs-notables-and-townsfolk.md:276`
("the culture-authoring checklist"), and a text mention at `tavern-mercenaries.md:161`.

**Corrections to the lane:**
- "`git grep -c 'cultures.md'` shows it linked from INDEX.md (2), new-culture-authoring.md (3),
  orientation.md (1)": the INDEX hits are `no-mount-cultures.md` (`INDEX.md:46`) and `modding/cultures.md`
  (`:467`); the orientation hit is `lessons/data-content-cultures.md` (`orientation.md:38`). Neither
  links `docs/cultures.md`. INDEX.md does not link it at all.
- A fourth doc, `docs/modding/cultures.md`, carries its own "Add a culture from nothing" recipe (`:231-`).
- `new-culture-authoring.md:8` says: for a net-new culture, "read `docs/cultures.md` FIRST ... This guide
  picks up after the culture definition exists". The AI guide covers the troop and armour flow, not the
  culture object, so pointing `docs/cultures.md` at it creates a loop. The doc that covers the same
  ground as the checklist is `docs/modding/recipe-add-a-culture.md` (its "checklist, fatal rows first" and
  "Order of work" `:27-94`, with "10 each" wanderers at `:91` and the `equipmentsets/` path at `:92`).
- Content only `docs/cultures.md` holds that a cut must keep or move: the tavern-mercenary "three rules"
  (`:83-105`, which `tavern-mercenaries.md:161` names as living there; the feature doc holds the copies and
  leaf rules at `:60-68`, so the rules survive, but the pointer at `:161` changes), and the sharing table
  (`:21-51`, the target of `id-cheatsheet.md:522`).

**Conditions:**
1. Point the cut file at `recipe-add-a-culture.md` (and `docs/modding/cultures.md` for attributes), not at
   `new-culture-authoring.md`.
2. Repoint `new-culture-authoring.md:8,442` to the recipe in the same change, and fix the text pointers at
   `tavern-mercenaries.md:161`, `npcs-notables-and-townsfolk.md:276`.
3. Keep "Current State" and the sharing table; move or drop the tavern-mercenary section only after
   checking each line is in `tavern-mercenaries.md`.
4. No anchor link breaks (none targets `docs/cultures.md`); `lint_docs.py` dead-link check confirms.

**Recalibrated:** impact MED (removes the drifting copy that the AI guide sends readers to first); effort
S to M (more inbound pointers than the lane counted); risk LOW to MED (content loss if condition 3 is skipped).

## DOC-02: add the missing Realms row to feature-map.md and link rows that lack their doc

**Verdict: NARROWED.** The Realms gap is confirmed. The "41 link-less rows" count is overstated: about 13
feature rows lack any reference to their doc.

**Re-read:** `docs/reference/feature-map.md:1-5` (header: "Row form: feature, path, patches or models, a
link to `docs/features/<name>.md`, and one clause of purpose. Trim a row when you next touch it; no bulk
rewrite"), `:115` (Cardolan and Rhudaur, the only "realms" hit); `TAOM.Tests/Composition/PatchCategoryNumberTests.cs`
in full; `docs/features/realms.md:103-117` (Key Files).

**How the row is parsed** (`PatchCategoryNumberTests.cs:113-123,178-216`): every line that starts with
`"| "` is a row; the row name is the first cell (text up to the first `" |"` after column 2); the row's
feature is the first `(Main|Client)/Features/(\w+)/` match in the line, else the row name with spaces
removed. Every `Patch(\d+[a-z]?)(_[A-Za-z0-9_]+)?` in the line must name a category the code declares and
whose number the row's feature owns, unless listed in `RowCitesAnotherFeature` (`:56-60`). The scan needs
at least 50 mentions (`:28,208`). The test is `[TestCategory("RequiresGame")]` (`:23`), so it runs in the full
desktop suite and is skipped in a no-game run. `tools/lint_docs.py` and `tools/build_backlinks.py` do not
parse feature-map rows (`grep -n "feature-map\|FEATURE_MAP"`: no hit); the linter only checks its links.

**Realms facts for the row:** `git ls-files Main/Features/Realms Client/Features/Realms | wc -l`: 74 files.
`git grep "HarmonyPatch\|Harmony\b"` in both folders: no `[HarmonyPatchCategory]` and no PatchNN; four
manual `new Harmony(...)` patches (`Main/Features/Realms/Hooks/LiegeArmyDiscount.cs:32`,
`Main/Features/Realms/Hooks/PartyNames.cs:39`, `Client/Features/Realms/Hooks/ConversationNames.cs:34`,
`Client/Features/Realms/Encyclopedia/TitleEncyclopediaPage.cs:160`). Entry points per the feature doc:
`RealmsModule.cs`, `RealmsCampaignBehavior.cs`, `RealmsClient.cs`. Model it uses: `TaomClanFinanceModel`
(`realms.md:81`; `gamemodel-registry.md:39`).

**Row shape a plan can state:**
`| Realms | `Main/Features/Realms/` (manual Harmony patches, no PatchNN category: LiegeArmyDiscount, PartyNames) and `Client/Features/Realms/` (ConversationNames, the Titles encyclopedia page): <one clause of purpose>. See [realms.md](../features/realms.md) |`.
It must start with `"| "`, put `Main/Features/Realms/` before any other feature path (so `RowFeature` is
`Realms`), and contain no `Patch` followed by a digit.

**Ran:** scratch `verify-harness-docs/featuremap.py` and an inline recount: 267 lines start with `"| "`; 41
lack the substring `features/` (the lane's number), but that set includes the header and component rows
(Mod code, Core, Adapters and so on) and rows for data and tools. Of the 203 rows that name a
`Main/` or `Client/Features/` path, 15 have no markdown link to a `features/*.md` doc (many link as
`../../docs/features/x.md` or name the doc in text), 13 have no reference to a features doc at all, and 11 of
those have a same-named doc: CharacterCreation, AtmospherePersistence, AdvancedCombat, CulturalFeats,
Arena, MainMenuCustomizer, Siege, SpecialResources, SettlementGuards, NamedCompanions, RevoltTuning. Main
folders absent as `Features/<X>/`: only `Realms` of 202. Client folders absent as `Client/Features/<X>`:
`Realms` and `SettlementNameplateRelation` of 43.

**Corrections to the lane:** "41 table rows have no `features/` link" becomes "about 13 feature rows give no
reference to their doc; 11 have one to link". The lane's list says SiegeDefense; the row with no reference is
`Siege`.

**Conditions:**
1. The new row cites no PatchNN; run `PatchCategoryNumberTests` (desktop, with the game) after the edit.
2. Adding links to the 11 rows touches them, which the header's "trim when you next touch it" allows; it is
   not a bulk rewrite as long as only the link is added.
3. The optional folder-to-row gate is new code (simplicity criterion: small win, new check); it also fires on
   every new feature folder, so HeroDefence and FieldBallista would need rows on merge. Maintainer's call.

**Recalibrated:** impact LOW to MED; effort S (rows), M (gate); risk LOW.

## DOC-03: tell readers to grep the three navigation maps, and finish INDEX.md coverage

**Verdict: NARROWED.** No tool or test parses the header lines, so the header sentences are safe. The
"curated or complete" question is half answered already, and an ADR-011 step owns the larger question.

**Re-read:** `docs/INDEX.md:1-8` ("**Start here.** This file maps the canonical docs"); `docs/reference/doc-lookup.md:1-4`
("`docs/INDEX.md` is the fuller curated topical map"; "this file is the complete task table");
`docs/reference/feature-map.md:1-5`; `docs/adrs/011-knowledge-delivery-tiers.md:239` (Migration step 10:
"Assess consolidating the three navigation indexes; re-baseline"); `orientation.md:10-11` (the always-on
pointer to all three).

**Ran:** `wc -c -l`: INDEX.md 116,862 B / 520 lines, feature-map.md 184,787 B / 288, doc-lookup.md 50,513 B /
138 (the lane's sizes reproduce). `head -10 ... | grep -i "grep\|search"` on each: no search instruction in
any header (confirmed). `git grep "INDEX\.md\|doc-lookup\|feature-map"` over `tools/*.py`, `tools/*.sh`,
`tools/tests`, `TAOM.Tests`, `.claude/hooks`, `.githooks`: readers are `PatchCategoryNumberTests.cs:114`
(feature-map rows that start with `"| "`; header lines start with `#` or `>`, so a header sentence is
never a row), `tools/tests/test_ai_documentation.py:61-67` (INDEX.md must link the Codex operating guide
somewhere), `lint_docs.py:159` (doc-lookup exempt from the stale-version check by name),
`check_doc_graph_ratchet.py` (inbound link counts; more links cannot make it worse), and the backlinks
generator, which owns only the region between `backlinks-start` and `backlinks-end`. None reads a header
line. Coverage recount (top-level `docs/features/*.md`, name searched in INDEX.md): 61 of 284 absent; the
lane's 74 of 298 counts subfolder docs too (method differs, not a contradiction).
`git grep -i "three navigation\|navigation index"` over plans, retros, ADRs: only ADR-011:239, so step 10 has
no recorded outcome.

**Corrections to the lane:**
- doc-lookup.md:3 already calls INDEX.md "the fuller curated topical map", so "decide whether it is complete
  or curated" is answered in one of the three files; INDEX.md should say the same thing, which is a
  one-line consistency edit, not a decision.
- Whether to add the 61 to 74 missing lines, or to consolidate the indexes, is ADR-011 Migration step 10,
  still open: that part is the maintainer's, as the lane says.
- The token cost ("a session that follows start here reads up to 30k tokens") is size arithmetic; the Read
  tool caps a read at 2,000 lines and refuses large files without a range, so a whole read of feature-map.md
  is not the default path. Impact is UNVERIFIED, as the lane says.

**Conditions:** header sentences only (above the first table or list), no change to rows or the backlinks
region; INDEX.md keeps its Codex-guide link.

**Recalibrated:** impact LOW (unmeasured); effort S; risk LOW.

## Summary of verdicts

| Id | Verdict | Behaviour-preserving | One line |
|---|---|---|---|
| DX-01 | NARROWED | only with conditions | Reuse conflicts with `evidence-over-claims.md:25,29` ("run it now", "an earlier run" is not verification); /ship spans turns. Needs a rule amendment first. |
| DX-02 | NARROWED | only with conditions | A cost item, not a contradiction; standalone /review-codex would lose its only full run unless 3f says so; `:264` too. |
| DX-03 | NARROWED | only with conditions | CLAUDE.md deletions safe (fix "(below)"); about 335 B, not 400; the output-style "Also" deletion changes always-on guidance. |
| DX-05 | NARROWED | only with conditions | Verbatim copy of the cwd unescape turns `\` into `/` on every Windows payload; unescape to `\` keeps output identical. |
| DX-06 | NARROWED | only with conditions | Branch and log lines are duplicates (catalogue row to edit, no test pins); hint cut only after "started"; folding the worktree checks silences a note. |
| DX-09 | CONFIRMED | yes | Stale counts and weak floors confirmed; `-B` is a separate change; `:57` is in verify/SKILL.md. |
| DOC-01 | NARROWED | yes | Lines as quoted; the recipe's counts are stale too (321 wanderers, 30 cultures, 28 set files); one instruction is done, not stale; more paths and a second stale comment. |
| DOC-02 | NARROWED | yes | Realms row confirmed (no PatchNN to cite); about 13 rows lack a doc reference, not 41. |
| DOC-03 | NARROWED | yes | No parser of header lines; doc-lookup already calls INDEX curated; consolidation is ADR-011 step 10. |
| DOC-04 | NARROWED | yes | All six fixed, plus a seventh; open-questions.md marks RESOLVED, it does not delete. |
| DOC-08 | NARROWED | only with conditions | Four culture docs; lane's link counts wrong; pointing at new-culture-authoring.md would loop; point at the recipe. |

## What I did not cover

- I did not run `tools/test_hooks.sh`, any dotnet build or test, `run_skill_evals.py` (paid), or any tool that
  writes on a plain run. The hook parse comparison (DX-05) ran only my scratch copies of the two parse
  snippets on sample stdin; the hook itself was not executed.
- DX-01: how often a /ship ends with no fix (the only case the reuse saves) is not measured; the cost of
  /verify Step 1b alone is not measured.
- DX-02: the number of Codex-confirmed bugs per /review-codex run is not measured.
- DX-03: whether the "Also" list changes adherence is not measurable without an eval; I did not read the
  rest of `writing-tells.md` beyond the rows that hold the eleven items.
- DX-05: calls to Edit and Write per session are not counted; timings are from this desktop under load and
  differ from the lane's.
- DX-06: I did not run `session-start.sh` (it starts detached refreshes and archives). Whether the harness
  gitStatus is present in every client the maintainer uses (terminal, VS Code panel, desktop) is only
  inferred from this session and harness-facts.md.
- DX-09: the totals on the laptop (partial install) are UNVERIFIED.
- DOC-01: the wanderer count is from a grep of `spc_wanderer_<culture>_<n>` ids in the repo's
  `taom_wanderers.xml`; ids in another shape, or wanderers defined in the live TAOM_Map, are not counted.
- DOC-02: I did not run `PatchCategoryNumberTests` (it needs the game and dotnet). I did not check whether
  the four Realms manual patches are in `harmony-patch-registry.md` (no `Realms` hit there, by grep; a
  possible separate gap, not verified as required).
- DOC-04: only the seven handbook entries named here were re-checked, not the other rows of
  `open-questions.md`.
- DOC-08: I did not diff the 12 checklist steps of `docs/cultures.md` line by line against the recipe.
- Findings outside the assigned list (DX-04, DX-07, DX-08, DX-10, DX-11, DOC-05, DOC-06, DOC-07, DOC-09) were
  not checked.
