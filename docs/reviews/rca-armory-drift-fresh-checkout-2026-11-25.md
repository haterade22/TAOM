# RCA: the Armory drift check in a fresh checkout (harness retro P3, 2026-11-25)

`/deep-review` at tier 2 (two hooks, the hook test suite, settings, one skill and instruction text; no C#, no
XML, no live data). First wave: the Data flow and Tooling lenses. Codex at tier 2 needs the maintainer's yes
once per session; none was given, so that pass is owed before merge. The Data flow lens rated one finding
HIGH, so this file exists. Every finding below was read in the files or run before it was fixed. F1 repeats
a cause that the lesson written the same day already names: a trigger tied to the place where the work used
to happen.

## Findings

| # | Sev | Finding | Found by | Why missed | Fix |
|---|---|---|---|---|---|
| F1 | HIGH | The drift check that P3 added to `/game-run` was `find -newer catalogue.tsv`. `git worktree add` writes the catalogue with the time of the checkout, so in a new worktree no pack is newer and the check says "no drift". Run on the install: the main checkout (catalogue on disk since 2026-11-13 22:41) printed `creature/chariot/chariot_rig_geo.tpac`; the worktree made on 2026-11-25 18:58 printed nothing for the same committed catalogue. `session-start.sh` had the same blind spot for a session that starts in a worktree | Data flow lens; reproduced by the author in both checkouts | The author's rows set every time with `touch` in a sandbox that was no git checkout. No row ran the check where `/game-run` runs since the trunk edit gate: a fresh sibling worktree | `/game-run` Phase 2 runs `python tools/generate_armory_catalogue.py --check`, which rebuilds the catalogue from the live Armory and compares content; it reads no file time and answered with drift in the fresh worktree in 1.7 s. `session-start.sh` stays a file-time hint: in a linked worktree (its `.git` is a file) with no newer pack it says UNCHECKED and names the exact check, and it leaves the stamp alone. A first fix that took the reference time from the catalogue's commit was removed again (S1 below). Three rows in `tools/test_hooks.sh` 7n, in a sandbox whose `.git` is a hand-written file |
| F2 | MED | Three in-game check preconditions, the `/armory-audit` trigger list and one `doc-lookup.md` row told the reader to act "if the banner shows". With one banner per drift state, a later session sees none | Data flow lens | The change searched for the banner's text in hooks and tests, not for instructions that depend on seeing it | Each names `generate_armory_catalogue.py --check` |
| F3 | MED | The first form of the `/game-run` check resolved the game folder with `${BANNERLORD_GAME_DIR:-default}`, which takes no fallback when the variable is set and wrong; `session-start.sh` tries the variable and then always the default (the fix of 2026-08-10) | Data flow lens | A one-line command written beside a hook that had already paid for this lesson | Closed with F1: the generator resolves the install itself |
| F4 | LOW | The retro's banner table counts sessions that print the banner; with P3 that number falls by design | Data flow lens | The proposal was judged by the number it then changes the meaning of | Said in the retro record, for the next retro |
| F5 | LOW | The stamp path is relative, so "once per drift state" is once per drift state and checkout; two docs did not say so | Data flow lens | Written from the intent | `armory-ref-audit.md`, the `session-start.sh` row of `hooks-catalog.md` and the skills say "per checkout" |
| F6 | LOW | The `hooks-catalog.md` row of the new hook said "no interpreter" and "deletes the session's state"; the script starts Python for a path with a `\u` escape and leaves a subagent's file | Data flow lens | The row was a summary of the header, shortened once too often | Row corrected against the script |
| F7 | LOW | `_session_ledger.sh` lists every file type of the ledger folder and lacked the two new suffixes | Data flow lens | The new hook's header was written, the shared one was not read again | Listed there |
| F8 | LOW | The hook reads only the block form of `paths:`; an inline `paths: [..]` rule would go quiet in every worktree, and the 7r guard would not have seen it | Data flow lens | The guard pinned the characters of the globs, not the shape of the list | The guard fails on an inline form |
| F9 | LOW | The adoption record gave byte counts measured before the last edits, said "four rows" for five, and still listed the hook as owed | Data flow lens | The record was written before the work it describes was final | Corrected when the sizes are final |
| F10 | LOW | ADR-011's 2026-11-13 amendment describes the Skills table as keeping its doc-routing rows; those are now one pointer to `doc-lookup.md` | Data flow lens | An ADR is protected from edits, so the change left it for the amendment | In the ADR-011 amendment of this branch |
| F11 | LOW | Two moved lines lost a clause: CLAUDE.md no longer said that an ask reason goes to the user (its other home is a path rule), and AGENTS.md no longer said which label means which | Data flow lens | The ledger check asked whether a home exists, not whether that home always reaches the same reader | Both clauses restored |

## Tooling lens: no HIGH, eight findings

The lens ran after the fix of F1 and read the corrected files. Each finding was checked in the code or on
the machine before it was acted on.

| # | Sev | Finding | Fix |
|---|---|---|---|
| T1 | MED | `point-worktree-rules.sh` left at once for every file under the project folder, so a worktree under the project's own `.claude/worktrees/` (EnterWorktree, `isolation: "worktree"`) got no note | The prefilter lets a file under `.claude/worktrees/<name>/` through; a root equal to the project is left again after the walk. Rows in `test_hooks.sh` 7r, from hand-written `.git` files |
| T2 | MED | The first fix of F1 in `session-start.sh` asked git with no regard for the start budget, and then forced a 0.5 s scan: about 10.5 s in the worst case against a 10 s registration, and a killed hook prints nothing | Bounded at first (git only with 3 s left, 0.7 s a call), then gone with the git calls (S1) |
| T3 | LOW | `git diff` can take `index.lock` from another session in the same checkout | `--no-optional-locks` at first, then gone with the git calls (S1) |
| T4 | MED | The retro miner counted every folder named `<repo>-...` as a sibling worktree. On this machine `taom-bot` is a repository of its own and five such folders are no checkout | A folder that is there must have a `.git` file that names this repository; one that is gone counts by its name. Run from a worktree, the miner finds the main checkout from its own `.git` file. Seven tests |
| T5 | LOW | A relative gitdir was resolved with `pwd`, which answers with a mount name in Git Bash (`/tmp` for `E:/Temp`), so the two spellings of one folder compared unequal; an unopenable gitdir was reported as "no gitdir line" | `pwd -W`, with `pwd` as the fallback; a cause of its own in the note |
| T6 | LOW | Two process starts on every Read: `$(cat)` and `$(dirname)` | `dirname` became a parameter expansion. The stdin read stays `$(cat)`: measured here, `read -d ''` takes 50 ms against 60 ms on a 200 B payload and 473 ms against 60 ms on a 200 KB one, because bash reads a pipe one byte at a time |
| T7 | LOW | The `/game-run` text said "exit 1: run the audit", but exit 1 is also unclassified rows and a traceback, and a missing install is exit 2 | The skill reads the output line: `OK:`, `DRIFT:`, `UNCLASSIFIED:`, `ERROR:` or a traceback, each with its action |
| T8 | LOW | Rows missing for a relative gitdir, a submodule, another repository's worktree, a Git Bash drive path and a rule two folders down; the hook read rules two levels deep where the loader reads every depth; one Armory row passed on silence alone | The rows exist; the hook reads every depth; the row also fails on an UNCHECKED note |

## Second Data flow pass, on the fixes: no HIGH

The fix-loop rule sends the lens that found a HIGH back over its fix. It also read the two scripts that were
new since its first pass.

| # | Sev | Finding | Fix |
|---|---|---|---|
| S1 | MED | The first fix of F1 in `session-start.sh` took the earlier of the catalogue's time on disk and the author time of its last commit, for a catalogue that matches HEAD. An audit that rewrites the catalogue with the same content (an editor re-saved a pack and changed no mesh) then still matches HEAD, so the old author time stood and the banner could never be cleared | The commit-time logic is removed. The banner is the file-time hint it is on trunk, plus the UNCHECKED note in a linked worktree. That takes 33 lines and two rows with a fake git and a slow `stat` out again, and closes T2, T3 and three LOW findings of this pass (a git error counted as an answer, a merge in progress, a shallow clone) by removal |
| S2 | MED | The new skill size gate, like the path rule gate it copies, compared a file with its copy at HEAD when no commit was in view. CI runs the linter on a checked-out merge commit, where the tree equals HEAD, so neither growth gate could fire there: both held only in the local commit hooks | `check_context_budget` takes a base for that case, and the run passes `--dash-base`, which `pr-checks.yml` already sets to `HEAD^1`. Three tests |
| S3 | MED | The retro table counted a Read of a rule file as work in that checkout. A worktree agent that read the main checkout's rule by hand moved to the "both" row, so the row the hook is judged by would lose its careful agents before the hook and keep them after it | A Read of a rule file marks only the "read a rule file" column. Two tests |
| S4 | LOW | "Compares it with the committed one": `--check` compares with the catalogue file of the checkout, which is the right behaviour | Wording in the skill and the feature doc |
| S5 | LOW | A renamed skill folder counts as a new one, so a rename of a skill over the cap is refused | Said in the linter's feature doc; it refuses, it never lets growth through |

## Second wave: the Completeness lens, no HIGH

The code met the retro's proposals; the catalogs and the lists of owed work were behind it. One MEDIUM: the
two "Owed" lists left out what the approved plan still owes (its later phases lived only in a machine-local
plan file and a memory card). Eight LOW: the tools catalog, the rules catalog, the hooks catalog's helper
paragraph, the linter's report heading, the docs index and the baseline's guard list did not name the new
hook, gate or table; the adoption record named a cut that a later fix had restored; the live proof of the
hook was owed only in passing. All nine are corrected in the documents named.

## Second wave: the Standards lens, no HIGH

Two MEDIUM. The retro miner finds the hook's note by its opening words, and no test ran the hook's real
output through the miner: a reworded note would have emptied the miner's column with every test green.
`tools/test_hooks.sh` 7r now does that for both notes, and the hook's "unchecked" note, which names no rule,
no longer counts as rules named. A lint report had been written into the worktree as a file named `-` by a
reviewer's `--report -`; it is removed. `lint_docs.py --report -` writes a file of that name where a sibling
tool takes `-` for stdout: not changed here (no issue: found at night with the maintainer away).

Six LOW, all applied: the growth message names the base it compared with; a `SKILL.md` one folder further
down is no skill, in the tree and in a commit; the new row of `harness-loading.md` keeps the fact and the
command and leaves the counts in the retro; two code comments lost their history to this file; one double
negative; the miner counts the three tools the hook is registered on.

## Second wave: the Efficiency lens, one HIGH on the hot path

The new hook runs after every Read, Edit and Write of every session and subagent, so its cost is paid about
15,000 times a week for Read alone.

| # | Sev | Finding | Fix |
|---|---|---|---|
| E1 | HIGH | The hook looked for a SessionStart payload first, with a regex over the whole payload. A PostToolUse payload carries the tool's response, which for a Read is the file's text, and a regex that cannot match scans every byte of it. Measured here for a file in the session's own checkout: 76 ms on a 200 KB payload and 310 ms on a 2 MB one, against 47 ms and 63 ms for a bare `$(cat)` | The file path is read first, and SessionStart is looked for only in a payload that has none. After: 46 ms and 92 ms. The same rows of `test_hooks.sh` 7r hold the behaviour |
| E2 | MED | In a worktree the hook translated every one of the rules' globs to a regex for every call, although most start with a literal folder the path does not start with | A glob with a slash is skipped when the path does not start with its literal head. The reviewer measured 90.7 ms to 64.8 ms a call and no difference in output over 173 paths |
| E3 | LOW | `session-start.sh` started `rm` in every session that found no drift | The delete runs only when the stamp is there |

Not applied, each on a measurement: a cache of the translated globs (15 to 20 ms a call in worktrees only,
against three ways for a cache to go stale and drop a rule without a word); a cheaper own-checkout test on
the raw payload (2 ms at most); a stamp that holds its own time to save one `find` per session start while a
drift lasts (about 15 ms, against a second stamp field).

**Why E1 was missed.** The author timed the hook with a 200-byte payload, the size of a hook's input for an
Edit of one line, and never asked what a PostToolUse payload for a Read holds. The reviewer who had proposed
a cheaper stdin read made the same assumption, and the measurement that refuted that proposal used a large
payload only for the read itself.

## Second wave: the Design lens, six proposals applied

All six keep behaviour, and the rows and tests that existed prove each one. In the hook, the root is
normalised once and the "root equals project" test sits right after the walk, where that case arises. In the
miner, the five flags of a transcript became a set of named marks, and the reach helpers reuse `rule_name`
and `edit_key`. In the linter, the base a file is judged against is chosen in one place. In the suite, 7r
uses the path helper it defines. In `session-start.sh`, the Armory branches read drift, cannot tell, no
drift.

Rejected by the lens, with its reasons: a shared helper for the two growth gates of the linter (seven
parameters for fourteen lines); two hooks in place of one script with an event branch (five lines, at no
cost to the hot path); bash pattern matching in place of the glob translator (in `[[ == ]]` a `*` crosses
`/`); shared code between the bash hook and the Python miner.

## Convergence pass and mutation run: one missing row

One reviewer checked that the Efficiency and Design changes keep behaviour. It ran the hook against eight
payloads for E1, and for E2 it compared 156 globs with 679 paths: the skip fired 84,210 times, 1,502 pairs
matched, and no skipped pair would have matched. It read the rest. Its one finding, LOW: the `--dash-base`
help of the linter said the option is the growth base only without `--staged` or `--only-paths`, and it is
the growth base in every mode. The help says so now.

A scratch script then changed the final hook in 21 ways, one at a time, and ran `test_hooks.sh` 7r against
each. Two changes left 7r green. Removing the fast exit for the session's own checkout changes nothing a
row can see, because the root test after the walk ends the run the same way. "Any payload with no file path
resets the session" was a missing row: nothing proved that a tool payload with no file path leaves the
session's notes alone. 7r has that row now, and the same change turns it red, so 20 of the 21 fail a row.

## Root-cause pattern

F1, F3 and F5 are one fault: a property of the checkout (a file's time on disk, a path relative to the
session's folder, an environment value) used as if it were a property of the commit. The harness retro of the
same day found the same fault in path rules, which load only in the checkout a session started in, and its
lesson listed three triggers of that kind. A file time was not on the list, and the author of the list then
wrote one.

## Why each check missed F1

- **The author's rows** passed 10 of 10: they set the catalogue's and the pack's times by hand, in a folder
  that was no git checkout.
- **The full hook suite** ran those rows, so it passed too.
- **The author's own run of the hook** against the install came after the fix, not before.

S1 is the same fault from the other side: the fix replaced one property of the checkout (the file time) with
a property of the commit (the author time) where the question was about neither. The question is whether the
catalogue's content matches the live Armory, and only a comparison of content answers it.

## Stated limit

The session-start banner compares file times. In a linked worktree it cannot see a pack older than the
checkout, and says UNCHECKED there; in a fresh clone it cannot see one either, and says nothing.
`generate_armory_catalogue.py --check` has no such gap, and it is the check `/game-run` and the in-game
preconditions name. No issue: the banner is a hint since P3, and the exact check is the gate.

## Lesson

Extended, not new: "A gate that moves where the work happens moves it away from every trigger tied to the
old place" in [build-tooling-workflow.md](lessons/build-tooling-workflow.md) now lists a file's time on disk.

## Codex review after the merge (2026-11-26)

Mike asked for it after the merge; no Codex pass ran before. `codex exec` at `xhigh` on the scripts and
tests of `0e92a28f9`, prompt `codex-adversarial-harness-worktree-rules-2026-11-26.prompt.md`. Eleven
findings, none P1. Each was read against the code. Two of its seven suspects were disputed by Codex
itself: the glob matcher agrees with every glob form in the rule files, and the growth gates in
`tools/lint_docs.py` hold for a rename, a new file, a missing base, `--staged` and a merge commit.

| # | Codex | Finding | Verdict | What was done |
|---|---|---|---|---|
| F5 | P2 | `harness_retro.py` took a relative `gitdir:` line as it stood, so a worktree made with `worktree.useRelativePaths` was no sibling in the rule delivery table | Confirmed by a failing test | A relative gitdir is read from the checkout root, as the hook does |
| F6 | P2 | The miner counted a plain folder under `.claude/worktrees/` as a worktree; the hook treats it as the main checkout | Confirmed by a failing test | A folder that is there with no `.git` entry is main checkout work |
| F11 | P3 | The quiet rows of the Armory banner tests passed for a hook that died: `exit 17` in the quiet branch left 13 rows green | Confirmed in the code | Every row first requires exit 0 |
| F4 | P2 | A second changed pack prints nothing while the first newer pack stays the same | Confirmed, and the comment and the feature doc claimed more ("again for another pack") | Wording corrected. The behaviour stays: the drift is told, the audit is owed, and `/game-run` runs the exact check. A stamp on the whole set needs a full scan at every session start |
| F1 | P2 | A path with `..`, or through a junction into a sibling, is taken for the session's own checkout | Confirmed in the code, not applied | The tools send absolute paths with no `..`, and no junction between checkouts exists. No issue: a guard for a case nobody has shown |
| F2 | P2 | With `CLAUDE_PROJECT_DIR` set to a subfolder of the checkout the hook stays silent | Confirmed in the code, not applied | The project folder is the one that holds `.claude/`. UNVERIFIED whether a session can start with another value |
| F3 | P2 | No Python and a path with a `\u` escape: no note and no "unchecked" | Confirmed in the code, not applied | Both conditions at once; `session-start.sh` already reports a missing Python |
| F7 | P3 | Six Reads that end together give six copies of the note | Confirmed by Codex's run, not applied | The cost is repeated text once per rule set and session. A lock on a hook that runs after every Read costs more than that |
| F8 | P3 | Session ids `a.b` and `ab` share a ledger | Not applied | Session ids are UUIDs |
| F9 | P3 | A file more than 40 folders below its checkout gets no note | Not applied | The deepest tracked path has 8 segments |
| F10 | P3 | A 40 MB payload takes 5.3 s against a 5 s timeout | Not applied | UNVERIFIED that a payload of that size can arrive; a timeout is a missed note, and the hook never blocks |

Not checked by Codex, in its own words: live delivery by Claude Code, UNC paths, and the full hook suite
(it does not run git commands that write). The live delivery was proven by hand on 2026-11-26: a Read of
`Main/IoC.cs` in a sibling worktree returned the note with both C# rules.

Why the two miner defects were missed: the hook's rows for a relative gitdir and for a plain nested
folder came from the Tooling lens (T8), and nobody carried them to the second consumer of the same
question, the miner. The standing lesson in `build-tooling-workflow.md` covers it: "When two tools are said
to share a rule, the shared symbol is not the claim: test the pair where the logic around it differs".
