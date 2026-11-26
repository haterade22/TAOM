# Knowledge Library

## Overview

The knowledge store holds the techniques of this project: ways of doing a kind of work well, each with the reason
it is better and a plain statement of whether it acts on the next piece of work or is only written down. One tool,
`tools/knowledge.py`, checks the store and answers questions about it, and it computes the list of the repository's
procedures from the files that hold them. A hook puts the entries that match a file in front of whoever opens that
file; it is in the repository and is not registered yet. The `/teardown` skill is the procedure that fills the
store from a result that beat ours. The decision behind it is
[ADR-016](../adrs/016-technique-library-and-procedure-database.md), which is proposed and waits for the maintainer.

## Why This Exists

The maintainer's order of 2026-11-26: "we need to develop a long term technique library and procedure database for
the whole repo and keep it updated." It followed his order of the same morning to turn what an outside reference does
better into a default of our own process.

- **Before:** a technique that belonged to no single file area had no home. It stayed in a lesson, in one asset's
  build scripts or in machine-local memory. The method that produced the approved neck armour of one horse armour
  lived in that armour's own modules, and the four other armours kept building their necks the old way.
- **Before:** nothing recorded whether a technique acts. The first draft of the first technique doc, written the
  same morning, called several things defaults that no build obeyed, and only its two checkers found that.
- **Before:** the procedures had no complete index, and no check compared an index with the files.
- **Without this:** the next asset, tool or review repeats rounds that an earlier one already made.

The survey behind these points, with its counts, is in the ADR's Context.

## Architecture

### Design challenge

Several checkouts of the repository commit in parallel (11 on 2026-11-26 at 12:26, `git worktree list`), so a file
that every branch appends to collides at merge, and so does a sequence number. The files every session loads at
start are near their caps (`python tools/lint_docs.py --context-budget-json`). A path rule does not load in a
sibling worktree and never for a file outside the repository, where the asset builds run. A subagent cannot invoke
a skill. The two hand-kept indexes that were measured had drifted (ADR-016, Context).

### Solution approach

- **One file per technique**, `docs/knowledge/techniques/<id>.toml`. The file stem is the id. No committed index
  exists: every view is printed by the tool.
- **A ladder** says how each entry acts today, and the check proves each state from `report` up.
- **Procedures are computed**, never stored: the tool reads the skills and the workflow docs on each run.
- **Delivery is by trigger.** A PostToolUse hook prints matching entries beside a Read, Edit or Write. The same
  entries come as a block for a subagent's spawn brief, and any client can ask the tool from any folder.
- **No date and no stamp.** An entry is valid while what it names exists.

```
docs/knowledge/techniques/*.toml          the entries (written by hand)
docs/knowledge/teardowns/*.md             the measured record behind a group of entries
            |
     tools/knowledge.py  -- check --staged -->  .githooks/pre-commit (every commit)
            |      \     -- check ---------->  tools/tests/test_knowledge.py (/verify, PR checks)
            |       \
            |        ---- index ---->  <ledger>/<session>.kindex
            |                                   |
   for / find / show / brief / debt      .claude/hooks/point-knowledge.sh
   procedures (computed from skills,      (PostToolUse Read|Edit|Write: the entries
   workflow docs, the task table)          beside the tool result, three at a time)
```

## The entry format

An entry is a TOML file. A key that the tool does not know is a finding, because a mistyped key would switch its
field off without a word.

| Key | Required | Holds |
|---|---|---|
| `title` | yes | The technique as an instruction, one line of at most 100 characters |
| `line` | yes | What a worker is told, one line of at most 220 characters. The hook prints it |
| `why` | yes | The mechanism that makes it better |
| `area` | yes | A lessons file stem (`ls docs/reviews/lessons`) or one of `ai-harness`, `asset-craft`, `coop-server`, `release`, `review-process`, `scenes-map` |
| `status` | yes | `active` or `retired`. A retired entry keeps its file and says `retired = "<date>: <reason>"`; `superseded_by` may name the active entry that replaced it |
| `ask` | no | The question a reviewer is given in place of the line, one line of at most 220 characters |
| `see` | no | The file that owns the detail or the procedure, as `path` or `path#heading`, where `heading` is the heading's text. The file must be in the repository and the heading must exist. The hook prints it after the line, and the file joins the procedure catalogue |
| `next` | below a gate | What would make the entry act |
| `[when]` | a glob or a task phrase | `paths`: globs that must each match a file of the repository. `outside`: globs for files outside it, held to the same glob rules and matched against no repository file. `tasks`: phrases for `brief` and `find` |
| `[[evidence]]` | at least one | `grade` (`measured`, `inferred`, `maintainer`), `value`, `date` (a TOML date with no quotes), and for a measured value its `method` |
| `[[acts]]` | no | `state` and what proves it (the ladder below). `scope` may say how far it reaches |
| `[[source]]` | at least one | Exactly one of: `lesson` with its `heading`, a whole `### ` line of that file; `teardown` (a file stem in `docs/knowledge/teardowns/`); `adopt` (a `docs/reviews/adopt-*.md` record that exists); `issue` (its number); `maintainer` (a quoted date, `"2026-11-25"`) with his `words` |

The file stem is lower-case words joined by single hyphens, at most 60 characters. No string of an entry holds an
em or en dash.

A glob is lower case, starts with `*` and is matched against the whole path, so `*` crosses folders:
`*/tools/blender/*.py` matches that folder in every checkout. That much is the dialect of `tools/data_gates.py`.
This tool is stricter. A glob holds only `a-z 0-9 / _ . * -`. `**` is refused, because one `*` already crosses
folders. `?` and a bracket class are refused, because bash and Python read them differently. A glob with fewer
than three letters or digits outside its extension (`*.py`) is refused: it would fire on every file of that kind.
The check cannot see a glob that is only too wide. Name the builder files where the work happens, never a whole
work folder: in the review of this feature one such glob put twelve armour entries on every Python file of every
asset job. The one exception is a folder that holds only reference results (`*/tripo_in/*`): every file there is a
reference, so the entries about references name it whole.

`brief` matches an entry when every word of one of its task phrases is among the words given. Write a phrase that
names the work (`neck armour`, `strap joint`), never one common word: `gold` alone would fire on the game's
currency, and `strap` on an item's name. `brief` prints at most 12 entries: first the ones that match a path and a
task phrase, then the ones a task phrase alone brings, then the ones a path alone brings. It names the ids of the
entries it did not print.

**When to write an entry.** Write one when no single area rule reaches every file the technique applies to: work
outside the repository, work across areas, the result of a teardown or of an adopted practice, a standing correction
of the maintainer. A technique for the files of one area of the repository stays one line in that area's path rule
(ADR-011, routing item 3). An entry never repeats a line that a rule or a trap sheet already holds.

## The ladder

| Level | Means | What the check needs |
|---|---|---|
| `sentence` | Only the entry says it. The entry has no `[[acts]]` row | `next` |
| `instance` | It was built once, in one place, and is no shared default. Typical for a method that lives in one asset's build scripts outside the repository | `where`, and `next` |
| `report` | A tool in the repository prints the number | `ref`, and `next` |
| `gate` | A check in the repository fails without it | `ref` |
| `code` | A shared function in the repository builds it | `ref` |

`ref` is `path::text`: a file of the repository and a text on one line of it, for example
`tools/blender/blender_job.py::def job(path):`. When the file or the text is gone, `check` fails, and the fix is to
restore what the entry names or to lower the entry and say what would make it act. An entry's level is the highest
row it has. Only `gate` and `code` count as acting. `python tools/knowledge.py debt` prints the counts and every
entry below a gate with its `next`; nothing stores those counts, and nothing runs `debt` on a schedule yet. On
2026-11-26 it printed 3 sentences, 17 instances and 1 entry at `code`: 20 of the 21 seed entries do not act yet.

## How an entry reaches a worker

| Reader | What brings the entry |
|---|---|
| A session or a subagent that reads, edits or writes a matching file, in the main checkout, in a sibling worktree or outside the repository | `.claude/hooks/point-knowledge.sh` prints the id, the level and the line of at most three entries beside the tool result, once per entry for the session and for each subagent. Further matching entries come with the next files it touches |
| A reviewer (`deep-reviewer`) | The same hook prints an entry's `ask` in place of its line, where the entry has one |
| A subagent before its first read | The lead pastes `python tools/knowledge.py brief --paths <files> --task <phrases>` into the `Knowledge:` slot of the spawn template ([agent-teams.md](../ai-includes/agent-teams.md)) |
| Codex and other clients, where no Claude hook runs | `python tools/knowledge.py for <files>` before the first edit ([codex-operating-guide.md](../ai-includes/codex-operating-guide.md)). It also names an entry that acts through one of the files |
| Someone who edits a file that an `[[acts]]` row of an entry names (`report`, `gate` or `code`) | The hook names the entry at the edit, with its line when the entry has a glob |

The hook reads an index that one bounded Python run builds per session, then matches in bash only. It builds the
index again when an entry file, the techniques folder or the tool is newer than the index, so an entry that git or
another session changed is served. It reads the store of the checkout the session started in, so an entry on an
unmerged branch is not served. It never blocks. When it cannot build its index it says "unchecked, not absent"
once. When entries could not be read it says how many, once per reader. After a session start, a resume or a
compaction the session is told each entry again. Entries come in the order of the ladder, from sentence to code,
and then by id; the order does not know which entry fits the file best.

Measured on the build machine on 2026-11-26 with the store of 21 entries, by a script that piped one payload into
the hook per call and read the clock before and after. Four runs at about 12:20 and 12:26, when the load was not
measured and the index had 31 lines: 301 to 359 ms for the first call of a session, which builds the index; 63 to
79 ms for a call with no match (each the mean of ten calls); 108 to 157 ms for a call that prints a note. Three
runs at 13:36, with the machine at 82 percent CPU load and an index of 67 lines: 545 to 1,198 ms, 153 to 223 ms
and 187 to 361 ms. The cost is the start of processes, not the size of the index. A second script, one minute
later under the same load, timed only calls with no match, in three rounds of ten calls each: 108 to 162 ms with a
31-line copy of the index against 143 to 151 ms with all 67 lines, and 36 to 60 ms for an empty `bash` start. The
load changed from minute to minute, which is why these calls were faster than the ones at 13:36. Building the
index for a store of 300 entries took 168 to 171 ms once warm. Inside
the hook the build is bounded at 3 s, 4 s with the kill grace. The registration below is 10 s, because the search
for a Python that works can take up to 4 s before the build starts.

### Switching the hook on

The hook is in the repository and is registered nowhere. A gate blocks edits to `.claude/settings.json`, and a
registration is the maintainer's step. He adds this entry twice, beside the `point-worktree-rules.sh` entries:
once in the `PostToolUse` group whose matcher is `Read|Edit|Write`, and once in `SessionStart`.

```json
{
  "type": "command",
  "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/point-knowledge.sh",
  "timeout": 10
}
```

Until then the store is reached through `brief`, `for` and `find`, and through the check. A session reads its hooks
from the checkout it started in, so the proof of the hook in a running session is owed after the merge.

## How the store stays true

`python tools/knowledge.py check` exits 0 when clean, 1 on a finding, and 2 when it could not judge: git could not
list the files, or the check itself failed. It runs in two places:

- `.githooks/pre-commit` runs `python tools/knowledge.py check --staged` on every commit in a checkout that has the
  tool. `core.hooksPath` is one path for every worktree, the main checkout's `.githooks`, so the check starts for
  all of them when that copy holds the block. The tool judges the store only when the commit stages a file of the
  store, the tool, a file that an entry names, or a deletion. It reads the git index only, names and text. So
  nothing that just the work tree holds refuses a commit: not an entry file that only lies there, and not another
  session's unfinished edit of an entry or of a file an entry names. A break that is staged is seen though the
  disk copy was repaired. The tool then judges the last commit the same way and refuses only a finding that the
  last commit did not have. An older finding is printed when the command is run by hand, and the real-tree test
  below fails on it. Exit 1 refuses the commit, and its last line names the store and the ways out. On exit 2 the
  commit goes through and the output says that the store was not checked.
- A real-tree test in `tools/tests/test_knowledge.py` runs the check on the work tree, with a floor on the entry
  count. `/verify` Step 2b and the PR checks run it.

What it finds, each under a code that the output prints: an entry that does not parse; a missing or unknown key; a
value of the wrong type or outside its list; a bad id; an em or en dash; a line that is too long or broken; a
glob outside the rules; an in-repo trigger that matches no file; an active entry with no trigger; an `[[acts]]`
row with no valid state or no proof; a `ref` that is not `path::text`, or whose file or text is gone; an entry
below a gate with no `next`; no evidence row, or one with a bad grade, no value, no date or, when measured, no
method; no source row, one that names no kind or several, or one that does not resolve (a lesson heading that is
not a whole line of its file, a missing teardown or adoption record, an issue that is no number, the maintainer's
words with no date); a `see` whose file or heading is gone; a teardown record that no entry cites; a retired entry
with no reason, `retired` on an active entry, or a `superseded_by` that names no active entry; a missing or empty
store. When git could not list the files, the finding is `unchecked-tracked` and the exit code is 2. A retired
entry is history: the check reads its shape only, and what it names may be gone.

What it cannot find: a sentence whose advice has become wrong while everything it names still exists. Moving the
entry to a gate or to code puts a check on the advice. Until then `debt` lists it.

## The procedure catalogue

`python tools/knowledge.py procedures` computes one record for each skill (`.claude/skills/*/SKILL.md`), each Codex
skill (`.agents/skills/*/SKILL.md`), each doc of `docs/ai-includes/`, each `recipe-*.md` of `docs/modding/`, each
instruction file of `.ai/` and `.ai/roles/`, each doc under `docs/` whose name holds `workflow` or `-authoring` or
ends in `-process.md` (the folders of records and history are left out), and each file that an entry names with
`see`. A record holds the trigger its file already states: a skill's description, the task cell of the
[doc-lookup.md](../reference/doc-lookup.md) row that links a doc, or the line of the entry that names the file. It
also holds the indexes that name it and the entries that point at it. `--orphans` lists the records that none of
seven index files names.

On 2026-11-26 the command printed 78 procedures (29 skills, 5 Codex skills, 44 docs), 53 with a stated trigger, and
12 that no index names (8 skills and 4 docs). The same day a survey listed procedures by reading the docs and found
125 in 114 files. The path patterns here find 71 of those files. The other 43 are procedure sections inside 15
feature docs, 15 reference docs, 4 rule files, 3 modding guides and 6 single files (`AGENTS.md`, `CLAUDE.md`,
`tools/README.md`, the save repair guide, `cultures.md`, the translator guide). No name pattern can tell such a
file from reference text. An entry with `see` is the way to bring one of them in.

## Key Files

| File | Purpose |
|---|---|
| `docs/knowledge/techniques/*.toml` | The entries |
| `docs/knowledge/teardowns/*.md` | The measured record behind the entries of one teardown |
| `docs/knowledge/README.md` | The short form of this doc, beside the entries |
| `tools/knowledge.py` | `check`, `for`, `find`, `show`, `brief`, `debt`, `procedures`, `index` |
| `.claude/hooks/point-knowledge.sh` | The hook that prints matching entries (not registered yet) |
| `.claude/skills/teardown/SKILL.md` | The steps that take a better result apart and write its record and entries |
| `.githooks/pre-commit` | Runs `check --staged` |
| `docs/adrs/016-technique-library-and-procedure-database.md` | The decision, proposed |

## Tests

- `tools/tests/test_knowledge.py`: each finding on a fixture store with one value broken alone; the queries; the
  index; the procedure catalogue on a fixture tree; `check --staged` in real temporary git repositories (the index
  against the work tree, and the new findings against the last commit's); the real store with a floor, with paths
  and tasks that no entry may fire on, and paths where an entry must fire.
- `tools/tests/test_point_knowledge_hook.py`: the hook on payloads for a file in the main checkout, in another
  checkout and outside the repository, a subagent, a reviewer, a repeat, reads that run at the same time, a session
  start, a store that changed after the index was built, a store that cannot be read, and the agreement of the bash
  and Python glob matching.
- `tools/tests/test_githooks_pre_commit.py`, class `KnowledgeGate`: the commit runs `check --staged`; a finding
  refuses it; a check that cannot judge or crashes lets it through with a note; a checkout with no tool is left
  alone; a merge commit runs it.

Run the suite from Git Bash. From PowerShell or cmd, `bash` on PATH is the WSL launcher, and the hook tests fail
there, as four older hook test modules do.

## How to verify in game

Not applicable: this is tooling for the people and AI sessions that work on the repository, and it changes nothing
the game loads. `python tools/knowledge.py check` is its own verification. The proof of the hook in a running
session is owed after the merge and the registration.

## How to add a technique

1. Ask whether an area rule already reaches every file it applies to. If one does, add a line to that rule.
2. Search the store: `python tools/knowledge.py find <words>`. One technique is one entry.
3. Copy an entry of the same kind in `docs/knowledge/techniques/` and name the file with a slug of the technique.
4. Write `line` as the instruction a worker follows, and `why` as the mechanism. Put the number in `[[evidence]]`
   with how it was measured, or the maintainer's words with their date. A choice he made in a dialog counts as
   his: say that it was a dialog, and that the label is the session's wording. A reason that is your own reading
   has the grade `inferred`, never `maintainer`.
5. Say how it acts today. If nothing in the repository enforces it, leave `[[acts]]` out or mark an `instance`,
   and write `next`.
6. Give it a trigger: a glob for the builder files where the work happens, and task phrases that name the work.
7. Run `python tools/knowledge.py check`, then `python tools/knowledge.py for <a file it should match>` and
   `python tools/knowledge.py for <a file it should not match>`.

## How to record a teardown

A teardown is owed when a reference for a part came from an outside tool or source, when the maintainer rejected
the same result twice, or when a tool, a review or another session solved what our own rounds did not. The steps
are the `/teardown` skill ([SKILL.md](../../.claude/skills/teardown/SKILL.md)); a client without Claude skills
reads that file. It writes the record `docs/knowledge/teardowns/<date>-<part>.md` and one entry per default, each
with `teardown = "<the record's file stem>"` as its source.

`check` fails on a teardown record that no entry cites and that has no line `No technique: <reason>`.

## How to move an entry up the ladder

1. Read `python tools/knowledge.py debt` and take an entry whose `next` names work you are doing anyway.
2. Build what `next` says in the repository: the report line, the check, or the shared function.
3. Add an `[[acts]]` row with the new state and its `ref`, and remove `next` when the entry reaches a gate.
4. Run `check`. It refuses a `ref` that does not resolve.

## How to find the procedure for a task

`python tools/knowledge.py find <words>` prints the entries, the procedures and the rows of the task table that hold
any of the words as whole words, the ones with the most words first: at most 15 entries, 10 procedures and 10
rows. `python tools/knowledge.py procedures` prints the whole computed list.

## Limits

- Work that runs only through a shell command or the Blender MCP opens no file with Read, Edit or Write, so the
  hook prints nothing for it. Only the spawn brief carries the entries there. A step in the `/blender-asset` skill
  that prints them for the files of an asset job is owed.
- A note that arrives does not make the worker follow it. `debt` lists the entries below a gate.
- The hook serves the store of the checkout a session started in, and entries come in a fixed order.
- After one index build that fails or overruns its 3 s, the hook says "unchecked, not absent" once and then stays
  silent for that session, until a session start, a change of the store or the tool, or an edit under
  `docs/knowledge/`. A build that overran only because the machine was loaded is not tried again sooner.
- The note at the edit of a file that an `[[acts]]` row names carries the entry's line only when the entry has a
  glob. An entry with task phrases alone is named without its line.
- The commit check refuses only what a commit adds. A finding that reached trunk another way (a merge on GitHub,
  a commit made with `--no-verify`) refuses no later commit, and it stays until someone reads the failure of the
  real-tree test.
- For a merge, the last commit is the branch's own. A clean merge of a trunk that holds a finding is therefore
  refused in the branch until the finding is repaired, on trunk or in the merge. Git gives the hook of a clean
  merge no sure way to read the other parent.
- An amend is compared with the commit it replaces, so it does not refuse a finding which that commit brought in
  past the hook.
- The design file of an armour matches many entries (15 on 2026-11-26), because every armour technique applies
  there, and the hook prints three at each call. `brief --paths <file>` prints 12 and names the rest.
- Evidence measured with scripts outside the repository cannot be made again from the repository. Such an entry
  names its teardown record as its method.
- A new lesson is not yet asked where it was promoted. That check changes the lesson format for every session, and
  ADR-016 lists it under "Not decided here".
- The ladder has no state for a default that acts against a technique: a constant or a gate that refuses what the
  better result has. The `/teardown` skill names it in the record and in the entry's `next`. The four teardowns of
  another asset job on 2026-11-26 each named such a case, and a state for it is proposed in #1613.

## Changelog

- 2026-11-26: the store, the tool, the hook, the pre-commit check, 21 seed entries and the `/teardown` skill
  (#1613). ADR-016 proposed.

## GitHub Issue

- **Issue:** #1613: [Knowledge store: a technique library and a computed procedure database, kept true by checks](https://github.com/haterade22/TAOM/issues/1613)
- **Status:** Open
