# Harness retro, 2026-11-17

Run on Mike's word, on the HARNESS RETRO DUE banner. Three proposals were presented one at a time
and all three were approved on 2026-11-17. They are applied on branch `chore/harness-retro-2026-11-17`
(a sibling worktree) and wait for Mike's word to commit.

## Window and coverage

- **Window:** 2026-11-10 to 2026-11-17. `python tools/harness_retro.py`: 74 main sessions, 1,223
  subagent transcripts, 1,056,237 records, 0 parse failures, 245 compactions. This retro's own session
  (`daf35f90`) is inside the window and was not excluded.
- **Claude Code versions seen:** 2.1.288 (1 session), 2.1.319 (18), 2.1.322 (28), 2.1.323 (27),
  2.1.325 (11), 2.1.326 (8). The newest check named in `.claude/rules/harness-facts.md` is still
  v2.1.314, so its facts are unchecked on six newer versions.
- **Growth tables:** checked against the 2026-11-10 run, kept beside this one.
- **Extra scans, mine, read-only:** the miner reports Stop blocks in total, so I counted them by
  day and by message kind from the main transcripts (61, matching the miner). I also classified what
  each session answered after a block, counted double-backslash Bash commands, and listed Agent
  spawns that named a model. Subagent transcripts were not scanned for these four, so each count is
  a floor for main sessions only.
- **Reviewer accuracy** (`python tools/analyze_reviews.py --summary --no-plot`): 89 reviews, 282 real
  bugs, 22 false positives, 39 misses. Lens yield reads 479 findings from 28 of 698 RCA files.

## Signals

**Hooks.**

| Signal | Count | Sessions | Notes |
|---|---|---|---|
| `check-bash-length` denies | 61 (plus 3 under another label) | 13 | Last window 108 in 9 main sessions |
| `check-commit-subject-version` denies | 17 | 7 | Last window 6 in 3 |
| `check-moduledata-validation` denies | 5 | 5 | No fires last window |
| `check-doc-config-drift` denies | 4 | 3 | |
| `block-no-verify` 2, `config-protection` 1, `block-trunk-edit` 1, `check-claude-files-tracked` 1 | 5 | 5 | The trunk-edit gate landed 11-16 |
| `check-verification-evidence` Stop blocks | 61 | 30 | See the split below |
| Killed gates (timeout, failed open) | 10 kills across 6 gates | not counted | First seen on 11-10 for five gates (example session `ac263e4f`, the retro session of the week before) and on 11-16 for four, including `check-bash-length` (`ec3bdd7e`); `6063df9d` is the example for two more |
| No fires | `validate-push` | | A guard for a rare event; keep |

**Stop blocks by message kind** (main transcripts, counted by me):

| Day | Old shared-marker message | New "this session edited" | New "no ledger holds it" |
|---|---|---|---|
| 11-10 | 7 | 0 | 0 |
| 11-12 | 9 | 0 | 0 |
| 11-13 | 1 | 3 | 2 |
| 11-14 | 0 | 4 | 3 |
| 11-15 | 0 | 9 | 2 |
| 11-16 | 0 | 12 | 7 |
| 11-17 | 0 | 1 | 1 |
| Total | 17 | 29 | 15 |

The 15 "no ledger holds it" blocks came from five trunk-change moments, each one blocking every other
session's Stop at once (times UTC): 11-13 22:06 (`4771e1f8`, `1d4e6838`); 11-14 16:13 to 16:15 (`eac2bdb4`,
`b81c5699`, `9f7d6440`); 11-15 16:25 and 16:36 (`6b2a81d6`, `9054ac54`); 11-16 15:01 to 15:15
(`1b3f6674`, `8f59405d`, `9054ac54`, `6b2a81d6`, `eac2bdb4`, `ec2ae0bd`, `ec3bdd7e`); 11-17 19:15
(`9054ac54`, about the merge of the dwarf civilian fix into trunk). In all 15 the session's next
message said the named C# was not its own; 10 of the 15 name the shared main checkout.

**Banners against the skills they name.**

| Banner | Sessions printing | Invocations | Sessions that ran the command |
|---|---|---|---|
| GAME VERSION DRIFT | 2 | 1 | 1 |
| ARMORY ART DRIFT | 40 | 1 | 5 |
| UPSTREAM AHEAD | 49 | 1 | 1 |
| HARNESS RETRO DUE | 12 | 2 | 3 |

UPSTREAM AHEAD printed in 45, 49 and 49 sessions in the last three windows and was acted on once or never.

**Tool errors.** All 20 top signatures recur in three or more sessions. The largest: Edit "File has not
been read yet" 44 in 4 sessions (example `1b3f6674`); Bash Python traceback lines 21 in 10; empty Bash result 15
in 9; Edit "string to replace not found" 15 in 11; Read of a missing file 14 in 7; Write "no such tool
available" 14 in 5; `re.PatternError: unterminated character set` 12 in 7; `UnicodeEncodeError`
11 in 4. No runaway retry. After a compaction the plain error rate is 0.8% against 0.9% before.

**Double-backslash Bash commands** (main transcripts): 17,597 Bash commands, 199 hold two consecutive
backslashes (1.1%). Of the 110 with a heredoc, 22 errored (20%); of the 89 without, 3. The 3 regex errors
I could place (`4b4674fc` 11-16 15:37, `8e6ece76` 11-14 03:51, `ec2ae0bd` 11-13 03:50) all had a double
backslash in a heredoc, and so did two commands in this retro's own session.

**User corrections:** 6 in 4 sessions, none about the harness: a menu choice (`1ed02d37` 11-11), an
approval scope (`1ed02d37` 11-12), two requests to skip Codex or the in-game check (`81196940` 11-12),
one to skip Codex (`a8abe1c6` 11-12) and one about which TAOM checkout (`ec2ae0bd` 11-13).

**Usage.** Skills: `deep-review` 49, `verify` 48, `localize` 14, `issue` 12, `investigate` 11, `game-run` 7. No
invocation: `blender-asset`, `freeze`, `groom-hair`, `prep-tripo-prop`, `unfreeze`. Spawns by type:
`deep-reviewer` 445, `implementer` 57, `architect` 30, `Explore` 20, `taleworlds-researcher` 6; 539 of 561
used the definition's model. Spawns that named a model, 22: `Explore` on Sonnet 14 or Haiku 1, `architect` on
Opus 1 (its own model), `implementer` on Opus 6.

**Memory.** `MEMORY.md` 5,476 B and 43 lines against the cap of 4 KB and 40 lines (3,923 B and 32 lines
last window). Seven cards older than 21 days, unchanged and not judged: `career-system` (101 days),
`repo-reorg` (66), `starter-kit` (66), `engine-bump-1.5.2` (63), `ghidra-native-decompile` (52),
`nazgul-scream` (42), `arthedain-kingdom` (33).

**Cost at list prices** (a proxy; 4,759 main requests on `claude-fable-5-2` have no list price and count as $0).

| Measure | 2026-11-03 to 11-10 | 2026-11-10 to 11-17 | #1078 target |
|---|---|---|---|
| Total | $4,928.52 (rules review session left out) | $5,798.49 | |
| Spend share, `deep-reviewer` | 33.9% | 46.2% | |
| Spend share, main session | 29.9% | 30.0% | |
| Main session mean context | 316,704 tokens | 291,410 | under 300,000: met |
| Main spend at 500k tokens or over | 23.4% | 1.1% | |
| Main cache rebuilds after an idle hour | 51, mean 301,588 tokens | 86, mean 230,170 | |
| `deep-reviewer` tool calls per request | 1.95 | 1.39 | 1.6 or more: not met |
| Requests per lens, median | 24 | 37 | under 60: met |
| Cost per lens, median | $3.45 | $2.99 | |
| Review runs | 89 runs, 404 lenses | 134 runs, 661 lenses; 3 lenses per run at the median, 8 at p90, 126 at most | |
| Cost per review run | $14.39 median | $13.11 median, $39.57 p90, $388.18 at most | |

**Yield by lens** (credits, sole finder), against the 2026-11-10 table: tooling 133 and 78 (unchanged);
data flow 129 and 52 (126, 50); completeness 84 and 30 (unchanged); engine 70 and 23 (66, 20); XML 59 and 16
(58, 15); standards 50 and 16 and design 40 and 4, efficiency 17 and 5, convergence 19 and 15, Codex 46 and 27
(all unchanged). The four lenses kept for a second wave did not run in the window, and Codex had no usage.

**RCA files per week** (`git log --diff-filter=A`): 27 (10-27 to 11-03), 49 (11-03 to 11-10), 96 (11-10 to 11-17),
5,600 lines in the last week.

**Tool output size** (section 9): 295,771,954 characters (332,966,230 last window). Bash 52.1%, Read 40.8%,
Grep 5.7%.

| Shell family | Results | Chars | Share of all output | Share of shell |
|---|---|---|---|---|
| `grep` | 17,757 | 34,336,187 | 11.6% | 22.2% |
| `sed` | 9,116 | 31,544,386 | 10.7% | 20.4% |
| `git diff` | 2,367 | 16,580,686 | 5.6% | 10.7% |
| `cat` | 4,470 | 16,217,161 | 5.5% | 10.5% |
| `echo` | 2,221 | 7,895,073 | 2.7% | 5.1% |
| `ls` | 3,232 | 6,026,090 | 2.0% | 3.9% |
| `python` | 3,163 | 4,902,838 | 1.7% | 3.2% |

Growth against 2026-11-10: `grep` 9.1% to 11.6% (2.5 points). A scan of the main transcripts (about 15% of the
family's characters) found no session above 23% of it, and 13% of it came from results over 10,000 characters.
Not attributed for the subagent transcripts.

| File (top reads) | Reads | Whole | Share of Read |
|---|---|---|---|
| `.claude/skills/deep-review/lenses/5-data-flow.md` | 218 | 217 | 3.6% (was 1.6%, 2.1 points) |
| `tools/rebalance_armor.py` | 110 | 1 | 1.0% |
| `Main/Adapters/LongDefeatBattlefieldAdapter.cs` | 77 | 12 | 0.7% |
| `tools/rebalance_troops.py` | 70 | 2 | 0.7% |
| `tools/ingame_verdict.py` | 95 | 9 | 0.7% |

The lens file is 19,942 bytes against 5,000 to 10,000 for its siblings, and every reviewer reads it whole by
design. Its 4.4 million characters are about 0.02% of the `deep-reviewer` token total, so it is not proposed.

**Token growth** (share of cache creation plus output): `deep-reviewer` on Opus 36.2% to 44.6% (8.4 points, 445
spawns against 234); main on `claude-fable-5-2` 0.9% to 7.1% (6.2 points; 18 `/model` commands were typed, and
the main session's model is Mike's choice). **Model override:** `implementer` is defined `sonnet` and ran 3,800 requests on `claude-opus-5-5`; all 6
spawns that named Opus were in session `6b2a81d6` (five on 11-15, one on 11-16), a deliberate escalation.

**Files two sessions edited within 24 hours:** 14 files, 12 session pairs. `TaomSettings.cs` 7 sessions and 16
edits, `feature-map.md` 5, `special-resources.md` 4, `SubModule.cs` 3.

## Verdicts

| Change | Observed | Verdict |
|---|---|---|
| `check-bash-length.sh` (2026-10-21) | 108 denies in 9 main sessions, then 61 in 13; no quoting failure in the top signals | helped |
| `PYTHONIOENCODING=utf-8` in settings (10-21) | `UnicodeEncodeError` is back in the top 20 at 11 in 4 sessions (43 before the change); the cause is not shown to be the stdio encoding | helped, with a small recurrence |
| Safety gates registered at 30 s (10-21) | 10 timeout kills in the window against none last window, first seen on two days and including a trivial gate: machine stalls | helped; watch |
| `detect-docs-gaps.sh` rewrite and the upstream hook split (10-21) | No SessionStart hook was killed in the window | helped |
| `deep-reviewer` batches reads; effort by tier (#1078, 11-03) | Cost per lens $3.45 to $2.99, requests per lens 24 to 37; tool calls per request 1.95 to 1.39, below the 1.6 target. The two changes cannot be told apart | helped as a pair, one target slipping |
| `/autocompact 400k`, `MEMORY.md` split, always-on rule cuts | Main mean context 316,704 to 291,410, spend at 500k or over 23.4% to 1.1%; more but smaller cache rebuilds | helped, confounded between the three |
| `MEMORY.md` limit warning (`3ea14a730`) | The index grew from 3,923 B to 5,476 B in a week and the warning prints at every start; seven stale cards stay | no observed effect |
| Lenses 1, 3, 4, 6 only in a second wave (11-03) | Their credits did not move, as they did not run; the spend target is confounded with the rows above | no observed effect (second retro in a row; kept, it removes work and adds no text) |
| Seven hooks, three agents, six guides, Serena removed | No error signature names one | parity held; keep |
| `.githooks/pre-commit` for every committer (`434d1d795`) | Due for comparison on 11-17. In the main transcripts the git `pre-commit` printed on 10 results and refused none; `commit-msg` refused 9 commits in 8 sessions. The two Claude commit gates denied 9 times in 8 sessions at most. No moment shows a commit that only the git gates caught | too early; keep both |
| Model and effort per spawn (`062b30a89`, 11-05) | 22 of 561 spawns named a model; the one override is 6 deliberate escalations in one session | no observed effect (it permits a practice; the practice is rare) |
| Capped-result rule (`ce95aaab2`, 11-08) | No signal measures an agent stating that a result was capped | no observed effect (unmeasured) |
| Ranked writing tells (`5472faa21`, 11-08) | No correction in the window names AI-sounding prose | no observed effect |
| Provenance rule deleted (`ca6312a90`, 11-06) | `/verify` provenance step reports 0 misses; no error signature names it | parity held; keep |
| Stop reminder per session (`b610b35ea`, 11-13) | The old message is gone after 11-13 (17 before, 1 on 11-13). Of the new blocks 29 name the session's own edits and 15 are false (see the split above) | too early (4 days); P1 refines it |
| Work claims (`0400ad8d9`, 11-13), rule splits (`8ac4da01a`, `29f863aa4`, 11-13), trunk edit gate (`17e5dd35b`, 11-16) | Younger than seven days | too early |
| UPSTREAM AHEAD banner | 49 printing, 1 invocation; the third window with no effect | no observed effect, third retro: P3 |
| `validate-push`, `check-claude-files-tracked` | `validate-push` no fire; killed twice at its timeout | guards; keep |

## Proposals

| # | Proposal | Evidence | Form | Decision |
|---|---|---|---|---|
| P1 | Run the Stop reminder for C# no ledger holds (branch 1b of `check-verification-evidence.sh`) only in a linked worktree or a main checkout on a feature branch, and skip it in the shared main checkout on trunk (narrowed by the deep-review Tooling lens: the trunk gate tells sessions to branch there) | 15 of 15 such blocks were answered "not my files", from five trunk-change moments (sessions and times above); 10 name the main checkout; since 11-16 no Edit or Write lands there | hook fix | approved 2026-11-17 (Mike); applied on the branch, test rows in `tools/test_hooks.sh` 7o |
| P2 | Write an RCA file only for CRITICAL, HIGH, player-reached or repeat-cause findings (D3), and put plain C#, repo XML and XSLT in tier 2 (D4), in `/deep-review`, and the same sentence in `/ship` | RCA files per week 27, 49, 96; the review of the dwarf civilian fix in `daf35f90` (11-17) wrote a 50-line RCA for 7 findings with no CRITICAL or HIGH and had no tier row for repo XML plus a generator | skill text | approved 2026-11-17 (Mike); applied on the branch |
| P3 | Print the UPSTREAM AHEAD banner at most once in seven days, by a stamp file in `.claude/logs/`; RELEASE and NOTE lines still print every time | 49 sessions printed, 1 acted; 45 and 1, 49 and 0 before; the banner was Mike's own request of 2026-10-16 | hook fix | approved 2026-11-17 (Mike); applied on the branch, test rows in `tools/test_hooks.sh` 7l |

**Considered and not proposed:**

- **A gate on double backslashes in a Bash command.** It would refuse 110 heredoc commands to stop 22 that
  failed: 88 of 110 worked, because the tool's halving is what the author wanted. The rule is written in
  CLAUDE.md "MCP and shell" and each failure costs one retry.
- **The `grep` family growth.** Spread over many sessions with no session above 23% of the main-transcript part.
- **The Opus `implementer` requests.** One session, six deliberate spawns, the escalation rule in
  `model-effort-selection.md`.
- **Killed gates.** Two clusters of machine stalls, including a gate that only measures a string. Not a gate
  defect; the environment is Mike's to investigate (`environment-failures.md`).
- **The Data flow lens at 19,942 bytes.** Small against the review spend.
- **Deleting the Claude commit gates** that `434d1d795` planned for about 2026-11-17. The evidence does not
  show the git gates catching anything the Claude gates miss.

## Owed

- **Decisions from the 2026-11-03 list, still open:** the second-occurrence rule (G8-048), the lens 5 deletion
  item (G8-A03), and narrowing the `harness-facts.md` paths.
- **Harness facts on Claude Code 2.1.319 to 2.1.326:** not re-checked.
- **`.claude/skills/improve/workflows/review.js` line 262** still tells an improve run to write an RCA for any
  confirmed finding. P2 did not touch it; Mike decides.
- **`MEMORY.md`** is over its cap and seven cards are stale: judged by Mike, not here.
- **Git commit gates comparison:** repeat when a session shows a commit only the git gates refused.
- **The commit of this branch** and its merge need Mike's word.
