# Harness retro, 2026-11-25

Run on Mike's word as Phase 0 of the harness debloat (claim `harness-debloat`, branch
`chore/harness-debloat`, a sibling worktree). Three proposals were presented and all three were
approved on 2026-11-25. The fourth is the debloat plan itself, approved the same day.

## Window and coverage

- **Window:** 2026-11-17 to 2026-11-25. `python tools/harness_retro.py`: 59 main sessions, 1,029
  subagent transcripts, 842,889 records, 6 parse failures, 172 compactions. This retro's own session
  (`57e29aeb`) is inside the window and was not excluded.
- **Claude Code versions seen:** 2.1.322 (2 sessions), 2.1.325 (6), 2.1.326 (15), 2.1.327 (24),
  2.1.330 (16), 2.1.331 (5), 2.1.332 (7). The newest version a fact in `.claude/rules/harness-facts.md`
  names is 2.1.322, so its facts are unchecked on six newer versions.
- **Growth tables:** UNCHECKED by the miner. No earlier run was beside this one in the report folder,
  so the comparisons below are by hand against the 2026-11-17 record.
- **Extra scans, mine, read-only:** the miner counts rule loads per rule. I split them by checkout:
  for every transcript, which checkouts it touched a file in that the C# rules' globs match
  (Read, Edit, Write, MultiEdit), and whether `csharp-architecture.md` loaded. A second pass counted
  hand reads of rule files and first prompts that name a rule. A third found the commands behind the
  `numpy` and `PIL` import errors.
- **Reviewer accuracy** (`python tools/analyze_reviews.py --summary --no-plot`): 91 reviews, 318 real
  bugs, 24 false positives, 41 misses. Lens yield reads 538 findings from 32 of 734 RCA files.

## Signals

**Path rules do not reach work in a sibling worktree.** Transcripts that touched a file the C# rules'
globs match, by where the file was:

| Where | Kind | Rule loaded | Not loaded |
|---|---|---|---|
| Main checkout only, or both | main sessions | 18 | 0 |
| Main checkout only, or both | subagents | 14 | 1 |
| Sibling worktree only | main sessions | 2 | 12 |
| Sibling worktree only | subagents | 0 | 186 |

The 186 subagents: `deep-reviewer` 163, `implementer` 13, `architect` 6, `feature-builder` 3,
`taleworlds-researcher` 1. One of the 186 read `csharp-architecture.md` by hand; 55 had a first prompt
that names a rule file; 131 had neither (122 of them `deep-reviewer`). None of the 12 main sessions read
it by hand. In that window `agent-teams.md` and the trap index both told a session to carry the rules by
hand in a worktree; the counts show how often that happened. Of the 844 subagent transcripts with records in the
window, 793 ran with the main checkout as their working directory and 51 with another folder; none ran
in a worktree.

**Rule files loaded** (sessions, subagent transcripts), from the miner: the seven unscoped rules 54 and
773 each. Path rules: `external-skill-ports.md` 7 and 30, `harness-skills-agents.md` 7 and 30,
`csharp-patterns.md` 20 and 15, `csharp-architecture.md` 20 and 14, `data-scripts.md` 15 and 19,
`harmony-patches.md` 8 and 8, `traps/mission.md` 6 and 9, `harness-loading.md` 4 and 8,
`traps/creatures-assets.md` 6 and 6, `adapters.md` 8 and 3, `tests.md` 6 and 5, `traps/campaign.md` 7
and 4, `config-providers.md` 3 and 4, `gamemodels.md` 1 and 5, `harness-facts.md` 4 and 2, `xml-data.md`
3 and 2, `hook-authoring.md` 3 and 0, `gui-ui.md` 2 and 0, `troops.md` 1 and 1, `gui-vm-properties.md`
and `moduledata-validation.md` 1 and 0. No load in the window: `traps/data.md`, `xslt.md`,
`vanilla-data-comparison.md`, `native-cpp-ports.md`.

**Hooks.**

| Signal | Count | Sessions | Notes |
|---|---|---|---|
| `check-bash-length` denies | 70 (plus 1 under another label) | 12 | 61 in 13 last window, 108 in 9 before |
| `check-commit-subject-version` denies | 9 | 7 | 17 in 7 last window |
| `block-trunk-edit` denies | 6 (5 Edit, 1 Write) | 1 each | The gate landed 11-16 |
| `check-moduledata-validation` denies | 4 | 2 | A command that commits in two repositories |
| `config-protection` 3, `validate-push` 2, `check-doc-config-drift` 1, `freeze` 1 | 7 | 3, 1, 1, 1 | The `freeze` deny named a file in a sibling worktree |
| `check-verification-evidence` Stop blocks | 25 | 17 | 61 in 30 last window |
| `check-subagent-verification` SubagentStop blocks | 2 | 2 | |
| Killed at timeout, failed open | `block-trunk-edit.sh` 3 (2 Edit, 1 Write) | 2 and 1 | 2026-11-18 and 2026-11-25; registered at 10 s |
| Cancelled, not timed out | PostToolUse Edit 1, Write 1, PostToolUseFailure Bash 1 | 1 each | |
| Broken runs | 11 scripts, once each | 1 each | All on 2026-11-25, no stderr |
| No fires | `block-no-verify`, `check-claude-files-tracked` | | Guards for rare events; keep |

**Banners against the skills they name.**

| Banner | Sessions printing | Invocations | Sessions that ran the command |
|---|---|---|---|
| GAME VERSION DRIFT | 0 | 0 | 0 |
| ARMORY ART DRIFT | 33 | 3 | 4 |
| UPSTREAM AHEAD | 6 | 0 | 0 |
| HARNESS RETRO DUE | 9 | 3 | 3 |

`/game-run` was invoked 10 times. Its SKILL.md did not name the Armory drift check (it does since P3).

**Tool errors.** All 20 top signatures recur in three or more sessions. The largest: a Python
`FileNotFoundError` 41 in 4 sessions (example `4b4674fc`); Edit "File has not been read yet" 40 in 13
(`03777e99`; 44 in 4 last window); "No such tool available: TaskStop" 22 in 3 (`36836af8`);
`ModuleNotFoundError` for `numpy` 20 in 7 (`6d7bac86`) and for `PIL` 7 in 4; Edit "string to replace not
found" 12 in 10. Three runaway runs of four, all the harness refusing `SubagentHandback` outside auto
mode (`1051d54f` twice, `e557b364`). After a compaction the plain error rate is 1.2% against 0.8% before
(0.8% and 0.9% last window). The git `commit-msg` hook refused 3 commits in 3 sessions for subject
length (`82b2b072`, 11-22); `check-commit-subject-version.sh` has no length check.

**User corrections:** 5 in 4 sessions. Two were art direction on the Rivendell wall (`36836af8`, 11-25).
One told a session to go on because the blocked files were already cleared (`6b2a81d6`, 11-18). One
waived a public issue the session had planned and asked for no source credit (`b8435c17`, 11-23). One
was a message sent to the wrong session (`e557b364`, 11-19).

**Usage.** Skills: `deep-review` 42, `verify` 42, `investigate` 14, `game-run` 10, `issue` 9, `localize`
7. No invocation: `channel-sync`, `coop-upstream`, `engine-bump`, `freeze`, `groom-hair`,
`upstream-review`. Spawns by type: `deep-reviewer` 376, `implementer` 44, `Explore` 22, `web-fetch` 13,
`architect` 7, `taleworlds-researcher` 5; 432 of 480 used the definition's model. Spawns that named a
model: Sonnet 32, Opus 10, Haiku 6.

**Memory.** `MEMORY.md` 7,081 B and 41 lines against the cap of 4 KB and 40 lines (5,476 B and 43 lines
last window). Nine cards older than 21 days: `career-system` (109 days), `repo-reorg` (74), `starter-kit`
(74), `engine-bump-1.5.2` (71), `ghidra-native-decompile` (60), `nazgul-scream` (50), `arthedain-kingdom`
(41), `ingame-verdicts` (24), `yotthani-review-20261016` (22).

**Cost at list prices** (a proxy; 8,359 main requests on `claude-fable-5-2` have no list price and count
as $0).

| Measure | 2026-11-10 to 11-17 | 2026-11-17 to 11-25 | #1078 target |
|---|---|---|---|
| Total | $5,798.49 | $3,876.85 | |
| Spend share, `deep-reviewer` | 46.2% | 21.5% | |
| Spend share, `implementer` | not recorded | 41.3% | |
| Spend share, main session | 30.0% | 17.8% | |
| Main session mean context | 291,410 tokens | 350,102 | under 300,000: not met |
| Main spend at 500k tokens or over | 1.1% | 0.7% | |
| Main cache rebuilds after an idle hour | 86, mean 230,170 tokens | 68, mean 282,335 | |
| `deep-reviewer` tool calls per request | 1.39 | 1.18 | 1.6 or more: not met |
| Requests per lens, median | 37 | 28 | under 60: met |
| Cost per lens, median | $2.99 | $1.35 | |
| Review runs | 134 runs, 661 lenses | 96 runs, 407 lenses; 3 lenses per run at the median, 8 at p90, 15 at most | |
| Cost per review run | $13.11 median, $39.57 p90 | $5.34 median, $20.95 p90, $38.89 at most | |

Cache read is 98.1% of input over 62,199 requests, with 185 cache breaks. Mean context per request:
main 350,102 tokens, `implementer` 214,867, `deep-reviewer` 113,210. The always-loaded tier (entry docs,
unscoped rules, `MEMORY.md`) is about 42 KB, so it is a few percent of each request, nearly all of it
read from the cache.

**Yield by lens** (credits, sole finder), against the 2026-11-17 table: tooling 151 and 85 (133, 78);
data flow 142 and 56 (129, 52); completeness 89 and 34 (84, 30); engine 78 and 27 (70, 23); Codex 73
and 37 (46, 27); XML 60 and 16 (59, 16); standards 57 and 20 (50, 16); design 43 and 5 (40, 4);
convergence 22 and 18 (19, 15); efficiency 17 and 5 (unchanged).

**RCA files per week** (`git log --diff-filter=A`, counted the same way for both): 91 (11-10 to 11-17),
36 (11-17 to 11-25).

**Tool output size** (section 9): 197,443,196 characters (295,771,954 last window). Bash 54.1%, Read
39.9%, Grep 3.1%.

| Shell family | Results | Chars | Share of all output | Share of shell |
|---|---|---|---|---|
| `sed` | 5,950 | 22,757,156 | 11.5% | 21.1% |
| `grep` | 10,687 | 21,971,547 | 11.1% | 20.4% |
| `cat` | 3,453 | 12,042,794 | 6.1% | 11.2% |
| `python` | 5,847 | 8,878,770 | 4.5% | 8.2% |
| `git diff` | 1,106 | 6,520,122 | 3.3% | 6.0% |
| `ls` | 2,647 | 5,356,349 | 2.7% | 5.0% |
| `echo` | 1,007 | 3,383,352 | 1.7% | 3.1% |

Growth by hand against 2026-11-17: `python` 1.7% to 4.5% (2.8 points); `git diff` 5.6% to 3.3%; the
others moved less than one point. The `python` growth was not attributed to sessions.

| File (top reads) | Reads | Whole | Share of Read |
|---|---|---|---|
| `.claude/skills/deep-review/lenses/5-data-flow.md` | 97 | 97 | 2.5% (was 3.6%) |
| `docs/kitbash/rivendell-siege-set.md` | 99 | 11 | 1.7% |
| `tools/oneoff/blender_build_rivendell_wall.py` (two checkouts) | 93 | 16 | 2.2% |
| `tools/generate_armour_band_clones.py` | 31 | 7 | 0.9% |
| `.claude/skills/improve/references/plan-template.md` | 24 | 24 | 0.5% |

**Tokens by agent** (share of cache creation plus output): `implementer` on Sonnet 20.1%,
`deep-reviewer` on Opus 19.4% (44.6% last window), main on `claude-fable-5-2` 16.3% (7.1%),
`implementer` on Opus 14.2%, workflow subagents on Opus 10.1%, main on Opus 9.7%. **Model override:**
`implementer` is defined `sonnet` and ran 10,286 requests on `claude-opus-5-5` (3,800 last window). The
two costliest subagent transcripts, `a01eee3b` and `a84347ed`, are `implementer` runs of session
`b8435c17`.

**Files two sessions edited within 24 hours:** none of 5,070 (14 files and 12 session pairs last window).
The measure keys a file by its path, and sessions now edit in separate worktrees, so one file changed
in two worktrees does not count.

## Verdicts

| Change | Observed | Verdict |
|---|---|---|
| Trunk edit gate (`17e5dd35b`, 11-16) | 6 denies in 2 sessions; shared edits 14 files to none. 3 runs killed at the 10 s timeout, which lets the edit through. Side effect: work moved to sibling worktrees, where no path rule loads (the table above) | helped its target; hurt rule delivery: P1, P2 |
| Work claims (`0400ad8d9`, 11-13) | Shared edits to none; one wrong-session correction | helped, confounded with the trunk gate and with the path-keyed measure |
| Trap sheets and the harness rule split (`8ac4da01a`, `29f863aa4`, 11-13) | The sheets loaded in 6 or 7 main sessions each, `traps/data.md` in none; they cannot load in a worktree | confounded: P1 decides whether they reach the work |
| Stop reminder per session (`b610b35ea`, 11-13) and its narrowing (retro P1, `8a35108b0`, 11-17) | Stop blocks 61 in 30 sessions to 25 in 17; the message kinds were not split this time | helped |
| RCA only for CRITICAL, HIGH, player-reached or repeat causes (retro P2, `8a35108b0`) | RCA files per week 91 to 36, with 96 review runs against 134 | helped, partly confounded with less review work |
| UPSTREAM AHEAD at most once in seven days (retro P3, `8a35108b0`) | 49 sessions printing to 6; no invocation in either window | helped: the noise fell and nothing was lost |
| `deep-reviewer` at the session's effort (`74590c1d3`, 11-18) | Cost per lens $2.99 to $1.35, requests per lens 37 to 28, spend share 46.2% to 21.5%; tool calls per request 1.39 to 1.18, further below the 1.6 target | helped on cost, confounded with fewer and smaller runs; the batching target slipped again |
| Prompt audit for Opus 5.5 (`dbddae060`, 11-18) | No signal isolates it | no observed effect (unmeasured) |
| Memory audit tool (`20a1b9786`, 11-18) | `MEMORY.md` 5,476 B to 7,081 B; stale cards 7 to 9 | no observed effect |
| `MEMORY.md` limit warning (`3ea14a730`) | The index grew again and the warning prints at every start | no observed effect, second retro. The debloat's Phase 1 was to remove the cause; it left `MEMORY.md` as it is (owed in the adoption record), so the warning still prints |
| Manager session (`80a98c9b3`, `108f88b26`, `b0e90e25b`, 11-18) | `/manage` invoked 3 times | too early |
| `.githooks/pre-commit` and `commit-msg` for every committer (`434d1d795`) | `commit-msg` refused 3 commits in 3 sessions for subject length, which `check-commit-subject-version.sh` does not check | helped; this closes the comparison owed since 2026-11-03 |
| `check-bash-length.sh` (10-21) | 70 denies in 12 sessions; the written rule in CLAUDE.md does not stop the attempt, the gate does | helped; the rule text can shrink to name the gate |
| Lenses 1, 3, 4, 6 only in a second wave (11-03) | Standards rose 50 to 57 and design 40 to 43, so they ran; spend is confounded | no observed effect on cost, third retro; kept, it adds no text |
| `/issue` searches closed issues (`c6d55878a`, `502e8c336`), PR checks (`8b9cc1f74`), batch merge mode (`357dda88c`), ADR-009 ratchet (`7d6c226ac`, `673bf8dff`), hook fixes (`ea65759af`, `09f03e8c7`, `11f995db5`) | Younger than seven days | too early |
| `block-no-verify`, `check-claude-files-tracked`, `validate-push` | No fire, or 2 denies of a force push to trunk | guards; keep |

## Proposals

| # | Proposal | Evidence | Form | Decision |
|---|---|---|---|---|
| P1 | A hook that fires on Read, Edit or Write of a file in a sibling worktree of this repository, matches the file against each rule's `paths:` globs, and tells the agent once to read the matching rule files from that worktree. First prove that hook context arrives for a worktree path | 186 subagents and 12 main sessions touched C# only in a worktree with no rule loaded; in the main checkout the rule loaded in 32 of 33. A hook deny on a worktree path is proven (`freeze`, 11-18) | hook | approved 2026-11-25 (Mike) |
| P2 | Register `block-trunk-edit.sh` at 30 s, the value of the other safety gates | 3 kills at 10 s in 3 sessions (11-18, 11-25); the script runs three git calls of up to 3 s each | settings | approved 2026-11-25 (Mike) |
| P3 | `/game-run` runs the Armory drift check in its first phase and stops on drift; the ARMORY ART DRIFT banner prints once per drift state | 33 sessions printing and 3 invocations (40 and 1 last window); `/game-run` ran 10 times and does not name the check | skill step, hook fix | approved 2026-11-25 (Mike) |
| P4 | The harness debloat (plan approved 2026-11-25): cut the always-loaded tier by about a third, narrow path-rule globs, move branch-only skill text to reference files, lower the lint caps | The always-loaded tier loaded in 54 sessions and 773 subagent transcripts; `MEMORY.md` is 73% over its cap; `deep-review` and `engine-bump` keep steps past the 5,000 tokens that survive a compaction | text, lint caps | approved 2026-11-25 (Mike) |

Applied on the branch on 2026-11-25. P1: `.claude/hooks/point-worktree-rules.sh`, registered on PostToolUse
(Read, Edit, Write) and on SessionStart, with 43 rows in `tools/test_hooks.sh` 7r. Hook context for a file in
a worktree was already in the window's transcripts: 36 `additionalContext` notes for worktree edits and 14
for memory writes. A note for a subagent's tool call lands in that subagent's own transcript: from
2026-11-10 to 2026-11-25, 16 subagent transcripts hold 31 `record-session-edit` notes, so the channel
reaches the 186 subagents the table above counts. A live note from this hook is owed: a session reads the hooks of the main checkout, so
it can show only after the merge. P2: the one number in `.claude/settings.json`. P3: the stamp
`.claude/logs/.armory-drift-printed` in `session-start.sh` (13 rows in `tools/test_hooks.sh` 7n) and the
drift check in `/game-run` Phase 2.

For the next retro: P3 changes what the banner table measures. With one banner per drift state and
checkout, "sessions printing" falls by design, so the 33 to 3 ratio cannot be compared with a later one.
The review of P3 found that the first form of the `/game-run` check compared file times and so found no
drift in a fresh worktree; it now runs `tools/generate_armory_catalogue.py --check`
([RCA](rca-armory-drift-fresh-checkout-2026-11-25.md)).

What the evidence changes in P4. Nothing moves from the always-loaded tier into a path rule, because a
path rule does not reach worktree work until P1 lands. The path-rule diet comes after P1. The saving in
tokens is small (the tier is a few percent of a request and is read from the cache), so the case for
the cut is adherence and upkeep, not cost.

**Considered and not proposed:**

- **Installing `numpy` and `Pillow`.** Both import under `python` today. Of the 35 import
  errors my scan found, 17 came from commands that used the `py` launcher, which CLAUDE.md does not name.
- **More text about long Bash commands.** The gate already answers each attempt with the fix.
- **The Opus `implementer` requests.** Ten spawns named Opus; the two costliest transcripts are long runs
  of one session. The model of a spawn is the main session's choice.
- **The Data flow lens at 19,930 bytes.** 97 whole reads, 2.5% of Read output, down from 3.6%.
- **The `python` output growth.** Not attributed.

## Owed

- **A live note from `point-worktree-rules.sh`:** a session reads its hooks from the checkout it started
  in, so P1 can be shown live only after the merge. Until then its proof is `tools/test_hooks.sh` 7r.
- **The rest of P4:** listed under "Owed" in
  [adopt-instruction-debloat-2026-11-25.md](adopt-instruction-debloat-2026-11-25.md).
- **Decisions from the 2026-11-03 list, still open:** the second-occurrence rule (G8-048) and the lens 5
  deletion item (G8-A03). Narrowing the `harness-facts.md` paths went into the debloat's Phase 3 and was not done there: it is owed
  in the adoption record.
- **The split of rule loads by checkout** was a hand scan for C# files. `tools/harness_retro.py` now has it
  for every repository file ("Rule delivery by checkout", console line `rule reach:`), so the next retro
  can give P1 a verdict. It counts a folder named `<repo>-...` only when its `.git` file names this
  repository or the folder is gone (`taom-bot` is a repository of its own, and five `taom-*` folders are
  no checkout). A Read of a rule file counts as rule delivery only, not as work in that checkout. Run
  late on 2026-11-25, with that evening's sessions in the window, it reads: 292 subagent transcripts and
  13 main sessions worked only in a linked worktree; a path rule loaded in 0 and 4 of them; 26 subagents
  read a rule file.
- **Harness facts on Claude Code 2.1.325 to 2.1.332:** not re-checked.
- **"No such tool available: TaskStop"** (22 in 3 sessions) and **Edit "File has not been read yet"** (40
  in 13): not examined.
- **`.claude/skills/improve/workflows/review.js` line 262:** not re-checked since 2026-11-17.
- **The nine stale memory cards:** for Mike to judge with the `MEMORY.md` proposal, which the debloat left
  owed.
