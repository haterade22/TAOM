# Harness retro, 2026-11-10

Run on Mike's word, after the Bannerlord v1.5.7 bump (#1112). Two proposals wait for his decision;
nothing in the harness was edited for this retro. The harness changes that the bump's own review
made (two skill steps) are in [rca-engine-bump-v1-5-7-2026-11-10.md](rca-engine-bump-v1-5-7-2026-11-10.md).

## Window and coverage

- **Window:** 2026-11-03 to 2026-11-10. `python tools/harness_retro.py`: 55 main sessions, 1,183
  subagent transcripts, 754,882 records, 0 parse failures, 210 compactions. Claude Code versions
  seen: 2.1.288 (2 sessions), 2.1.314 (13), 2.1.318 (14), 2.1.319 (34). The newest check named in
  `.claude/rules/harness-facts.md` is v2.1.314, so its facts are unchecked on the two versions most
  sessions now run.
- **Second run for the cost rows,** as #1078 asks: `--exclude-session
  1196e135-e79e-4e10-8741-b5074435d142` (the rules review's own session). 54 main sessions, 1,066
  subagent transcripts, $4,928.52 at list prices.
- **Growth tables: UNCHECKED.** No earlier report folder was left beside this run, and the two
  earlier records hold no section 9 table. The tables below are the first durable baseline.
- **Reviewer accuracy** (`python tools/analyze_reviews.py --summary --no-plot`): 87 reviews, 273 real
  bugs, 22 false positives, 39 misses. Lens yield reads 471 findings from 27 of 601 RCAs.

## Signals

**Hooks.**

| Signal | Count | Sessions | Dates | Example session |
|---|---|---|---|---|
| `check-verification-evidence` Stop blocks | 26 (miner), 32 distinct moments by hand | 14 to 16 | 11-03 to 11-10 | `4517a99e`, `04bbab90`, `1f15d340` |
| of which: the session's next message says the changed C# belongs to another session or tree | 16 | 10 | | `4517a99e` 2026-11-10T01:04Z and T02:04Z; `04bbab90` 2026-11-06T21:39Z and T22:04Z; `32bb976d` 2026-11-10T02:37Z |
| of which: its build or test was still running, or ran in a tree the hook does not read | 8 | 5 | | `1f15d340` 2026-11-08T22:43Z; `3777ca82` 2026-11-10T17:44Z |
| of which: not classified (no text after the block, or another answer) | 8 | 4 | | |
| blocks followed by a build or test that failed | 2 | 2 | | `3777ca82` (the v1.5.7 compile break, already known to that session), `9b602bcb` |
| `check-bash-length` denies | 108 (miner), 126 by hand | 9 main sessions, 94 transcripts | 11-03 to 11-09 | `04bbab90` |
| by agent type (denies, transcripts) | implementer 45, 38; feature-builder 37, 24; architect 22, 17; deep-reviewer 10, 7; main 9, 6 | | | |
| in one transcript | once in 69, twice in 19, three or four times in 6 | | | |
| `check-commit-subject-version` denies | 6 | 3 | to 11-10 | `1196e135` |
| `block-no-verify` denies | 5 | 1 | 11-03 to 11-04 | `1196e135` (the rules review, testing that hook) |
| Every PreToolUse Bash gate failed, non-blocking, no stderr | 11 scripts, 3 or 4 each | 1 session and its subagent | 2026-11-09T01:01:32Z to :35Z only | `1f15d340` |
| Cancelled hooks | 1 (PostToolUse Bash) | 1 | 11-10 | `ac263e4f` |
| No fires | `validate-push`, `check-moduledata-validation` | | | |

**Banners against the skills they name.**

| Banner | Sessions that printed it | Invocations of the skill | Sessions that ran its command |
|---|---|---|---|
| GAME VERSION DRIFT | 2 | 2 | 5 |
| ARMORY ART DRIFT | 43 | 4 | 8 |
| UPSTREAM AHEAD | 49 | 0 | 0 |
| HARNESS RETRO DUE | 11 | 1 | 5 |

**Tool errors** (all 20 signatures recur in three or more sessions). The largest: Read of a missing
file 54 in 7 sessions; Read over the size or token limit 49 in 15 session rows; Edit "string to
replace not found" 26 in 7; Grep on a missing path 22 in 12 session rows. The Bash quoting failure (151 in the
2026-10-21 window) and the `UnicodeEncodeError` (43) are no longer among the 20. No runaway retry.
User corrections: 3, in one session, none about the harness.

**Usage.** Skills: `verify` 40, `deep-review` 31, `issue` 19, `localize` 14, `investigate` 10. No
invocation: `freeze`, `groom-hair`, `improve`, `prep-tripo-prop`, `upstream-review`. Subagent
spawns: `deep-reviewer` 234, `implementer` 30, `Explore` 12, `architect` 7; 283 of 294 used the
definition's model, and no model override was seen.

**Memory.** `MEMORY.md` 3,923 B and 32 lines (cap 4 KB and 40). Seven feature cards are older than
21 days: `career-system` (94 days), `repo-reorg` (59), `starter-kit` (59), `engine-bump-1.5.2` (56),
`ghidra-native-decompile` (45), `nazgul-scream` (35), `arthedain-kingdom` (26).

**Cost at list prices** (a proxy for relative size; rules review session left out).

| Measure | To 2026-11-03 | 2026-11-03 to 11-10 | #1078 target |
|---|---|---|---|
| Spend share, `deep-reviewer` | 51.4% | 33.9% | |
| Spend share, main session | 28.8% | 29.9% | |
| Main session mean context | 437,869 tokens | 316,704 | under 300,000: not met |
| Main spend at 500k tokens or over | 54.2% | 23.4% | |
| Main cache rebuilds after an idle hour | 135 in 13 days, mean 472,656 tokens | 51 in 7 days, mean 301,588 | |
| `deep-reviewer` tool calls per request | 1.16 | 1.95 | 1.6 or more: met |
| Requests per lens, median | 83 | 24 | under 60: met |
| Cost per lens, median | $6.14 | $3.45 | |
| Review runs | | 89 runs, 404 lenses; 4 lenses per run at the median, 9 at p90, 20 at most; $14.39 per run at the median | |

**Yield by lens** (credits, sole finder), against the 2026-11-03 table: tooling 133 and 78 (was 92
and 49); data flow 126 and 50 (116, 47); completeness 84 and 30 (unchanged); engine 66 and 20 (59,
18); XML 58 and 15 (52, 14); standards 50 and 16 (unchanged); Codex 46 and 27 (13, 4); design 40 and
4 (unchanged); convergence 19 and 15 (unchanged); efficiency 17 and 5 (unchanged). The four lenses
the tier rule keeps for a second wave gained nothing, as expected: they did not run.

**Tool output size** (section 9): 332,966,230 characters of tool results. Read 50.5%, Bash 43.0%,
Grep 5.1%.

| Shell family | Results | Share of all output | Share of shell |
|---|---|---|---|
| `sed` | 7,025 | 9.3% | 21.6% |
| `grep` | 11,448 | 9.1% | 21.0% |
| `cat` | 3,083 | 4.2% | 9.7% |
| `git diff` | 1,591 | 3.9% | 8.9% |
| `echo` | 1,729 | 2.8% | 6.5% |
| `ls` | 2,242 | 1.8% | 4.1% |
| `python` | 3,474 | 1.8% | 4.1% |
| `git status` | 846 | 1.1% | 2.5% |

Top files read: `docs/features/marshals-word/build-spec.md` (183 reads, 14 whole, 2.5% of Read
output), `docs/features/seat-of-seeing/build-spec.md` (166, 7, 1.7%),
`.claude/skills/deep-review/lenses/5-data-flow.md` (133, 130 whole, 1.6%),
`docs/features/long-defeat/phase2-plan.md` (120, 15, 1.5%). Growth: UNCHECKED, see above.

## Verdicts

| Change | Observed | Verdict |
|---|---|---|
| `check-bash-length.sh` (2026-10-21, P3) | The quoting failure it replaced (151 in the last full window) is out of the top 20. The gate denied 108 times; 69 of 94 transcripts met it once | helped |
| `deep-reviewer` told how to run a script with no Write tool (10-21, P4) | No refused Write in the top signals (66 before). 10 length denies in 7 of its transcripts, against 45 in 38 for `implementer`, whose definition has no such line | helped |
| `PYTHONIOENCODING=utf-8` in settings (10-21, P5) | `UnicodeEncodeError` (43 before) is out of the top 20 | helped |
| Safety gates registered at 30 s (10-21, P2) | About 400 killed gate runs before. One cancelled hook now, and one three-second moment in which every gate failed open | helped |
| `detect-docs-gaps.sh` rewrite (10-21, P1); upstream check in its own hook (`407570ece`) | No timeout kill of either in the window (39 and 12 before) | helped |
| `deep-reviewer` batches its reads; effort by tier (#1078, `2c737d97c`) | Tool calls per request 1.16 to 1.95; requests per lens 83 to 24; cost per lens $6.14 to $3.45, over 404 lenses. The two changes cannot be told apart | helped, as a pair |
| `/autocompact 400k`, the `MEMORY.md` split and the always-on rule cuts (`e653257a8`, `625e8fc53`) | Main mean context 437,869 to 316,704; spend at 500k or over 54.2% to 23.4%. Three changes, one signal | helped, confounded between the three |
| `MEMORY.md` limit warning (`3ea14a730`) | 13,807 B and 97 lines to 3,923 B and 32 lines, under the cap all week | helped |
| Lenses 1, 3, 4 and 6 only in a second wave (2026-11-03) | Their credits did not move. Codex gained 33 credits, 23 of them alone. What the four would have found is not measurable | no observed effect |
| Seven hooks, three agents, six guides and Serena removed (`9cfcadd07`, `452796db4`, `4fbfb7adc`) | No error signature names one of them | parity held; keep |
| `.githooks/pre-commit` for every committer (`434d1d795`) | The comparison with the Claude gates is due about 2026-11-17. The moment on 2026-11-09 shows the Claude layer can fail open | too early |
| UPSTREAM AHEAD banner | Printed in 49 sessions, acted on in none. The window before: 45 and 1 | no observed effect, second retro: proposal 2 |
| Model and effort chosen per spawn (`062b30a89`, 11-05); capped results (`ce95aaab2`, 11-08); ranked writing tells (`5472faa21`, 11-08); the provenance rule deleted (`ca6312a90`, 11-06) | Younger than seven days | too early |
| `validate-push`, `check-moduledata-validation` | No fire | guards; keep |

## Proposals

Both wait for Mike. No harness file was edited.

| # | Proposal | Evidence | Form | Decision |
|---|---|---|---|---|
| P1 | Make `check-verification-evidence` judge the session, not the tree. Today it reads every dirty C# file of the checkout the session started in, against one build marker that all sessions share. Record the C# files a session's own Edit and Write calls changed, and its own build or test runs, and remind only on those | 32 blocks in 16 sessions in seven days. In 16 the session answered that the file was another session's, and in 8 that its run was in progress or in another tree. Two blocks were followed by a build or test that failed. The "already reminded" marker is shared too (read from the hook's source), so the session that owns the edit can miss its reminder. Sessions and times are in the Signals table | hook fix | pending |
| P2 | Print UPSTREAM AHEAD at most once in seven days (a stamp file), not at every session start. `/upstream-review` stays on request | 49 sessions printed it and none acted, after 45 and 1 in the window before. The banner was Mike's own request on 2026-10-16, so the choice is his | hook fix | pending |

Considered and not proposed:

- **The 108 length denies.** Each costs one wasted command, most transcripts meet the gate once, and
  the definition line that helped `deep-reviewer` would be more text for three other agents. The cost
  is below the bar.
- **ARMORY ART DRIFT in 43 sessions.** The banner is right: the committed catalogue is older than the
  newest art drop, and the audit's verdict stays BROKEN on the FBX LOD report. The cure is that
  decision and a `regen`, not a quieter banner.
- **Three acts of this retro's own session** (`ac263e4f`, 2026-11-10): a tag cut before the tool
  suite ended, a script that wrote into another session's working copies, and a process filter that
  stopped another session's test run. No gate can see a script's writes or a process filter, and the
  tag was the maintainer's word. The RCA above and three lessons record them; `/release` Phase 5 has
  the step that would have caught the red test.

## Owed

- **Due now, held on 2026-11-03 until #1078's comparison:** D3 (which findings get an RCA file), D4
  (the tier table's hole), the second-occurrence rule, the lens 5 deletion item and the narrower
  `harness-facts.md` paths. The comparison is the cost table above. Mike decides.
- **Harness facts on Claude Code 2.1.318 and 2.1.319:** not re-checked.
- **Seven stale memory cards:** not judged here.
- **About 2026-11-17:** the commit gates' second stage, as the last record says.
