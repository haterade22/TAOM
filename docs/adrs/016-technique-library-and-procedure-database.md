# ADR-016: Technique Library and Procedure Database

**Status**: Proposed

**Date**: 2026-11-26

**Priority**: Standard

## Context

On 2026-11-26 the maintainer gave two orders. The first came after the neck and head armour of a horse
armour had taken several rejected rounds, until a model from an outside 3D tool showed the target:

> This is a perfect usecase for future research and other things like it. We struggled with a specific part, the
> horse helmet armor. Then we used tripo or another 3D tool to see what they did. At that point we should better our
> own workflow/processes with what the 3D tool did so that we do that by default moving forward. Like what did that
> tool do, that I didnt and why/how is it better. Let me add it to my own workflow/processes/toolbox. So in the
> future I do the same level of work and learn!
>
> Knowledge Management and improving our knowledge

The second followed the same morning:

> we need to develop a long term technique library and procedure database for the whole repo and keep it updated.

A **procedure** is a set of steps for a kind of task: a skill, a workflow doc, a command sequence. A
**technique** is a way of doing something well, with the reason it is better and, where one exists, the number
that shows it. A **lesson** is the record of a failure and its cause; a lesson can produce a technique or change
a procedure.

[ADR-011](./011-knowledge-delivery-tiers.md) decides where a fact lives: the narrowest trigger that fires
every time the fact is needed. It gives a home to the rule of a file area (a path rule), to a procedure (a
skill), to a trap (a sheet) and to a lesson (the archive). It gives none to a technique that belongs to no
single file area, and nothing in the repo records whether a technique acts on the next piece of work or is
only written down.

### What a survey of trunk found

Five read-only inventories ran on 2026-11-26 on trunk (commits `ce12141e2` and `831c502a9`). The counts below
were run again by hand on `831c502a9`; each names its command or its source.

| Finding | Evidence |
|---|---|
| Procedures have several kinds of home and no complete index | 28 skills (`ls -d .claude/skills/*/`), 22 docs in `docs/ai-includes/`, the recipes of `docs/modding/`, procedure sections of reference and feature docs, and 5 Codex skills in `.agents/skills/`. The survey listed 125 procedures in 114 files, and every file exists. `agent-operating-manual.md`, which tells a subagent to name the skill it needs, names 14 of the 28. Eight skills are named as a skill in none of seven index files (`python tools/knowledge.py procedures --orphans`). No check compares an index with the files it indexes |
| Most techniques exist only as lessons | 13 lesson files, 2,150 entries, 3,009,101 bytes (`grep -c '^### '`, `wc -c`). ADR-011 says each file's header names where its lessons are promoted: none of the 13 does. Four files (`data-content-cultures.md`, `native-cpp-port.md`, `state-lifecycle-save.md`, `testing-qa.md`) are named by no rule under `.claude/rules/`. No check asks whether a lesson became a rule line, a gate or a default |
| Knowledge that existed did not reach the worker | 97 lesson entries carry their own recurrence marker (the entry holds `**Recurred`, `**Recurrence` or `**Repeat`, or its heading holds "recurrence" or "repeat" in any case). One inventory read 29 cases in the lessons and RCAs where the knowledge existed or the fix was known, and classed each by hand: in 6 the spawn brief did not carry it; in 8 it was filed by subsystem or behind too narrow a trigger; in 3 it was written only where it was first used; in 4 it lived in another worktree, an unmerged branch or machine-local memory; in 4 the worker read it and did not apply it. In the other four a procedure had no step to promote it (1), nothing ran the gate (2), or it was never written (1) |
| The indexes that stayed true compare themselves with their source | The Armory catalogue (`generate_armory_catalogue.py --check`), the patch registry (`PatchCategoryNumberTests`) and the doc graph baseline each compare a list with its source and fail on a difference. Two hand-kept ones that were measured drifted: CLAUDE.md regrew from 28.4 KB to 48.9 KB between 2026-08-05 and 2026-09-22 (ADR-011), and the doc graph went from 64 orphans to 153 in ten weeks while its diagnostic ran nowhere (`tools/check_doc_graph_ratchet.py`) |
| The always-loaded tier is full | Entry docs 19,046 of 20,000 bytes, unscoped rules 15,184 of 16,000 (`python tools/lint_docs.py --context-budget-json`) |
| The work happens in many checkouts, and outside them | 12 checkouts of this repository on the build machine at the survey, 11 at 12:26 the same day (`git worktree list`). A path rule loads only in the checkout a session started in; in a sibling worktree a hook names it; outside the repository nothing fires (`.claude/rules/harness-loading.md`). Asset builds run in work folders outside the repository |

### The first case

The answer to the first order was one doc, uncommitted in the worktree of branch `feat/rhun-chariot-variants`: a teardown
of two reference models against the approved armour, and eleven technique rows. Each row has a column that says
how its default acts today: code, a gate, a printed report, or only a sentence. Two checkers read the first
draft and found that it called several things defaults that no build obeyed. In the checked table, eight of the
eleven rows act in code, one of them also through a gate and one through a printed report. The code of all eight
is called only by one armour's build, outside the repository; three are sentences only. The four other armours
still build their necks the old way. That column is the part of the first case that this decision keeps.

### How this decision was reached

Four designs were written independently, each from one stance: compute everything, start from delivery, the
smallest change, the life of one entry. Three judges then checked the designs' claims against the repository,
each through one lens: does it reach the worker, does it stay true, what does it cost. All four designs chose one
file per technique with a slug as its id and a machine-checked state for how it acts. They differed on procedures
(two computed the list; two kept a record for each procedure, one of them only where a procedure needs a trigger)
and on delivery, and two of the three judges chose a hook that prints the entry itself. The first slice was then
reviewed by two review lenses, two fact checkers and a mutation run, and by four more fact checkers after the
fixes. A last check before the merge, by three fact checkers and one adversary on the commit check, showed that
the check read the work tree and refused old findings: it now reads the git index and refuses only what a commit
adds. The same day another asset job took four more references apart in the record shape of the first case. Its
verifier's method and its list of what the format lacks went into the `/teardown` skill and into "Not decided
here".

## Decision

**A technique that no single area rule can reach gets one home, an entry in the knowledge store. Every entry
says how it acts today, and a machine proves each state from report up. The database of procedures is never
written down: it is computed from the files that hold the procedures.**

### The store

- `docs/knowledge/techniques/<id>.toml`, one file per technique, read with `tomllib`. The file stem is the id: a
  slug, never a number. Two branches that add entries touch different files, and two branches that pick one slug
  get a conflict that names a real duplicate.
- No index, view, catalogue or baseline of the store is committed. `tools/knowledge.py` prints every view.
- An entry holds the line a worker is given, the mechanism, its triggers, its evidence with a grade (measured,
  inferred, the maintainer's verdict), how it acts, what would make it act, and its sources. The key list is in
  [knowledge-library.md](../features/knowledge-library.md).
- An entry is retired, never deleted: lessons and commits cite ids.
- `docs/knowledge/teardowns/<date>-<part>.md` holds the measured record behind the entries of one teardown.
- **Scope.** An entry is written when no single area rule reaches every file the technique applies to: work
  outside the repository, work across areas, a teardown, an adopted practice, a standing correction of the
  maintainer. A technique for the files of one area stays one line in that area's path rule. An entry never
  repeats a line of a rule or a trap sheet.

### The ladder

| Level | Means | Proof |
|---|---|---|
| sentence | Only the entry says it | none; the entry says what would make it act |
| instance | Built once, in one place, no shared default | the entry says where; unchecked |
| report | A tool in the repository prints the number | `path::text` resolves; the entry still says what would make it act |
| gate | A check in the repository fails without it | `path::text` resolves |
| code | A shared function in the repository builds it | `path::text` resolves |

`path::text` names a file of the repository and a text on one line of it. When the file or the text goes, the
check fails, so an entry cannot keep claiming a default that was removed. Only gate and code count as acting. The
instance level exists because of the first case: a method that lives in one asset's build scripts is real, and it
is still not a default. `python tools/knowledge.py debt` prints the counts by level and every entry below a gate.
Nothing stores those counts or sets a limit on them.

### Delivery

No line is added to AGENTS.md, CLAUDE.md, orientation.md or an unscoped rule. Two things are added to what a
session or a task meets, and both are part of this decision: the description of one new skill in the skill list
of every Claude session, and one standing instruction in the guide that Codex reads before every task.

- **A hook prints the entry.** `.claude/hooks/point-knowledge.sh` runs after a Read, Edit or Write and prints, as
  `additionalContext`, the id, the level and the line of up to three entries whose globs match the file: in the
  main checkout, in a sibling worktree, inside a subagent and for a file outside the repository. Each entry comes
  once per session and once per subagent; further matching entries come with the next files. It reads an index
  that one bounded Python run builds per session and builds again when an entry file is newer, then matches in
  bash only. It rests on two sourced facts of `harness-loading.md`: the note arrives for a file outside the
  project, and a subagent's note lands in that subagent's own transcript. The sibling hook
  `point-worktree-rules.sh` shows the second fact in use: on 2026-11-26 at 11:35 the trunk checkout's ledger folder
  held 16 per-subagent ledgers of that hook (`ls .claude/logs/sessions | grep -c '\.arules$'`).
- **A reviewer gets a question.** For a `deep-reviewer` agent the hook prints an entry's `ask` where it has one.
  In one of the four sampled cases where the worker had read the fact and not applied it, a question in the
  review brief caught the repeat (`adapters-taleworlds-api.md`: "Writing the lesson down did not prevent the
  repeat"). For this class the survey's own judgement is that a gate or a review question fixes it, and more
  text does not.
- **A spawn brief gets a block.** `python tools/knowledge.py brief --paths <files> --task <phrases>` prints the
  entries for the `Knowledge:` slot of the spawn template, for design work before the first read. The entries
  that match both a path and a task phrase come first.
- **Any client can ask.** `for`, `find` and `show` run from any folder with the standard library.
- **Codex is told to ask.** `docs/ai-includes/codex-operating-guide.md`, which Codex reads before every task,
  tells it to run `for` on the files it will change before the first edit.
- **A skill holds the capture procedure.** `/teardown` takes a result that beat ours apart and writes the record
  and the entries. Its description joins the skill list that every Claude session loads, and the routing table
  of the `/blender-asset` skill points to it.
- **Globs** use the dialect of `tools/data_gates.py`: lower case, a leading `*`, matched against the whole path,
  `*` crosses folders. Bash matches it with `[[ $path == $glob ]]` and Python with `fnmatch`. A test holds the two
  to one verdict, and a bracket class and `?` are refused because the two read them differently.

The registration of the hook in `.claude/settings.json` is the maintainer's step. A gate blocks edits to that
file, and the hook is inert until he adds the two entries that the feature doc gives.

### Upkeep

- Every commit in a checkout that has the tool runs `python tools/knowledge.py check --staged` from
  `.githooks/pre-commit`. `core.hooksPath` is the main checkout's `.githooks` for every worktree, so the check
  starts when that copy holds it. The tool judges the store when the commit stages a file of the store, the tool,
  a file an entry names, or a deletion. It reads the git index only, names and text, so nothing that just the
  work tree holds refuses a commit: not a new entry, and not another session's unfinished edit. It then judges
  the last commit the same way and refuses only a finding that the last commit did not have.
- The same check runs on the whole work tree as a real-tree test that `/verify` and the PR checks run. A missing
  or empty store is a finding.
- A reference that no longer resolves fails. An in-repo trigger that matches no file fails. A teardown record
  that no entry cites fails, unless it holds a line `No technique: <reason>`.
- Whoever edits a file that an entry names as what makes it act gets a note at that edit, with the entry's line
  when the entry has a glob.
- There is no "verified on" date and no stamp. A date would turn entries red with no cause, and a stamp would
  make a blind re-stamp cheaper than a look. What the check cannot see is a sentence whose advice went wrong
  while everything it names still exists. Moving the entry to a gate or to code puts a check on the advice; until
  then `debt` lists it.

### Procedures

`python tools/knowledge.py procedures` computes one record per skill, Codex skill, workflow doc, recipe and `.ai`
instruction file, and one per file that an entry names with `see`. A record holds a trigger that is already
written: a skill's description, the task cell of the `doc-lookup.md` row that links a doc, or for a `see` target
the entry's line. It also holds the indexes that name it and the entries that point at it. `--orphans` lists the
records that no index names. `find` answers with entries, procedures and the rows of the task table together.
Nothing about a
procedure is typed twice. On 2026-11-26 the command printed 78 procedures. The path patterns find 71 of the 114
files the survey listed by reading; the other 43 are procedure sections inside feature and reference docs (30),
rule files (4), modding guides (3) and six other single files. An entry with `see` brings one of them into the
list when it matters.

### Capture

| Source | Step | Proof |
|---|---|---|
| A result that beat ours: an outside tool's reference, a part rejected twice, a review | The `/teardown` skill: the record, then one entry per default with the record as its source | `check` fails on an uncited record |
| A standing correction of the maintainer | An entry with grade `maintainer`, his words and their date | The entry; `check-feedback-memory.sh` already asks where a new feedback memory belongs |
| An adopted outside practice | An entry whose source is the `adopt-*.md` record | The source path must exist |
| A lesson | Not built and not decided here: each new lesson would name where it was promoted (an entry, a rule, a gate, or `none:` with a reason) | A lint check on new lesson entries. See "Not decided here" |

## Consequences

### Positive

- A technique that crosses areas or lives outside the repository has a home that every client and machine can
  read, and the store shows for each one whether it acts.
- The entry reaches the worker at the file, in a worktree and in a subagent, without a second read and without a
  line in the always-loaded files.
- The procedure list cannot go stale, because it is not stored.
- Entries added on different branches merge without conflict.
- A commit that removes or rewords what an entry names is refused at that commit.

### Negative

- A second on-demand channel now stands beside path rules. ADR-011 rejected nested CLAUDE.md files for that
  reason. This one is taken because a path rule cannot reach a worktree without a second read, and cannot reach a
  file outside the repository at all.
- One more hook runs on every Read, Edit and Write, and one more skill description is in the list that every
  session loads.
- A printed note does not make the worker follow it. On 2026-11-26, 20 of the 21 seed entries were below a gate.
- Work driven only through a shell command or the Blender MCP opens no file, so the hook says nothing there.
- The hook serves the store of the checkout a session started in. An entry on an unmerged branch is not served,
  and a session that started before the merge does not have the hook.
- Entries come in a fixed order, not by how well each fits the file.
- The commit check refuses only what a commit adds. A finding that reached trunk another way refuses no later
  commit, and it stays until someone reads the failure of the real-tree test.
- Evidence measured with scripts outside the repository cannot be made again from it.
- The computed procedure list misses procedures that are a section of a larger doc.

### Neutral

- Lessons stay append-only, and trap sheets and path rules keep their lines.
- `docs/INDEX.md`, `doc-lookup.md` and `feature-map.md` stay hand-kept maps. The tool reads the task table and
  does not replace it. Step 10 of ADR-011's migration (the three navigation indexes) stays open.

## Alternatives Considered

### Alternative 1: Generated path-rule sheets per area

- **Pros**: no new hook; the native loader and the worktree hook carry them.
- **Cons**: a committed generated file that every branch rewrites, so two branches that add entries to one area
  conflict; in a worktree the hook only names the sheet, and the worker must read it; nothing reaches outside the
  repository; the worktree hook reads at most 80 lines of front matter, so a long glob list is cut without a word.
- **Why rejected**: it brings back the shared appended file that this decision removes, and it misses the place
  where the first case happened.

### Alternative 2: One markdown table per area, the form of the first case

- **Pros**: readable; already written once.
- **Cons**: sequence ids and appended rows collide across branches; the "acts" column is typed and never checked.
- **Why rejected**: the first draft of that table called sentences defaults, and only its checkers saw it.

### Alternative 3: A hand-kept record for each procedure

- **Pros**: each procedure could carry its own triggers and owner.
- **Cons**: 114 files to describe by hand and keep true; the fields mostly repeat a skill's description or a row
  of the task table.
- **Why rejected**: nothing would compare 114 hand-written records with their files, and the two hand-kept
  indexes that were measured had drifted. A procedure that needs a path trigger gets an entry with `see`.

### Alternative 4: Widening `point-worktree-rules.sh`

- **Pros**: one hook, one matcher.
- **Cons**: that hook exits at once for a file inside the project and for a file in no checkout, so serving both
  is a rewrite of its hot path, and its own live proof is still owed.
- **Why rejected**: a separate small hook leaves a working one alone.

### Alternative 5: A token in each enforcing file, and hash stamps for freshness

- **Pros**: a two-way link between an entry and its enforcer; staleness visible without git history.
- **Cons**: a comment discipline in code files; a stored value that a one-line command refreshes without a look.
- **Why rejected**: the `path::text` reference catches a removed enforcer with no stored value, and the note at
  the edit reaches the editor when the diff is in hand.

### Alternative 6: A line in the always-loaded files

- **Pros**: every session would know the store exists.
- **Cons**: the tier is at 19,046 of 20,000 bytes; ADR-011's test of 2026-11-25 admits a line only when every
  task needs it, or a wrong act is irreversible or public and no gate stops it, or no narrower trigger reaches
  the work and the failure repeats.
- **Why rejected**: the hook and the check are narrower triggers. Whether a line is added later is the
  maintainer's decision under that test.

### Alternative 7: A vector store or semantic search

- **Pros**: finds a technique by meaning.
- **Cons**: a stored index that every branch rewrites, and a match that no test can pin.
- **Why rejected**: ADR-010 rules it out at this size, and a glob match is something a test can prove.

## Examples

### Good (Follows This ADR)

A session takes a better reference apart, writes the teardown record, and writes one entry for each default. An
entry whose method exists only in one asset's build scripts is marked `instance` and says what would make it act.
A later session moves that method into `tools/blender`, adds a test, changes the entry to `code` and `gate` with
both references, and deletes `next`.

The maintainer corrects a built asset. His words go into an entry with their date, and the next builder that
opens one of the builder files the entry names is shown the line.

### Bad (Violates This ADR)

An entry that says `code` and names a function in a work folder outside the repository. The check refuses it,
and it would have told the next builder that a default exists where none does.

An entry that repeats a line of `csharp-architecture.md` with a glob on `Main/**`. The rule already loads there.

An entry whose trigger is a whole workspace or an extension. In the review of the first slice one such glob put
twelve armour entries on every Python file of every asset job.

A technique saved only as a feedback memory, or only as a row of a doc on one feature branch.

A committed index of the store, kept by hand or generated.

## Migration Strategy

1. **This branch (#1613):** the tool and its tests; the hook and its tests; the pre-commit check; 21 seed
   entries with the teardown record of the first case; the `/teardown` skill and its row in the routing table of
   `/blender-asset`; the feature doc and the index rows. When it merges, the commit check starts in every
   checkout that has the tool. The hook changes nothing until it is registered.
2. **On acceptance:** the maintainer registers the hook in `.claude/settings.json`, and the amendments below go
   into ADR-011 and ADR-010. A live proof of the hook for each reader (main checkout, worktree, subagent,
   reviewer, a file outside the repository) is owed then, because a session reads its hooks from the checkout it
   started in.
3. **The first case's doc:** `docs/reference/technique-library.md` is uncommitted in the worktree of branch
   `feat/rhun-chariot-variants`. It is committed there first, on the maintainer's word. When that branch meets
   trunk, the doc gives its table and its teardown log to the store, its art check rows name the entry ids, and
   section 7b of the `/blender-asset` skill and the line in `/prep-tripo-prop`, both uncommitted in the same
   worktree, give way to the `/teardown` skill.
4. **The four teardowns of the other asset job** join the store as records with their entries when that job's
   branch meets trunk.
5. **Later, each on the maintainer's word:** the items under "Not decided here".

### Not decided here

- **A lesson names its promotion.** A lint check, built like the RCA FOLLOW-UP check, would ask each new lesson
  entry for a line that names an entry, a rule, a gate or `none:` with a reason. It makes ADR-011's promotion a
  recorded decision. It changes the lesson format for every session, and it needs a cutoff: the RCA FOLLOW-UP
  check also runs on a clean merge, so a check built like it would fail a long-lived branch on lessons written
  before the check existed.
- **A state for a default that acts against a technique.** A constant or a gate can refuse what the better result
  has. In the first case the 12 mm step between two bands stands against the closed lap. The four teardowns of the
  other asset job each named such a case: a layout gate that holds an exact mirror, a pinned relief clamp, a
  pinned strand squeeze, a constant that forbids contact between stones. Today the record and the entry's `next`
  name it, and the ladder shows such an entry as a sentence, or wrongly as a gate when a writer cites the gate
  that stands against it. A state `against`, with a `ref` or a `where`, would let `debt` print those entries
  first.
- **A trigger for work that opens no file**: a matcher on the Blender MCP tools, or on the script path of a shell
  command.
- **A measure of use.** The hook's notes start with `point-knowledge:`. `tools/harness_retro.py` already counts
  the sibling hook's notes in transcripts; the same count for this prefix would let the weekly retro compare the
  notes served with the lessons that recur. Nothing runs `debt` on a schedule yet.
- **A line in the always-loaded files.**
- **Retiring a sentence** that has stood for months with no enforcer.

### Amendments on acceptance

A gate blocks edits to an existing ADR, so the text waits here.

**ADR-011, the tiers table**, one row:

| Tier | Fires | Home | Holds |
|---|---|---|---|
| Technique | a Read, Edit or Write of a file that matches an entry's globs, in any checkout or outside the repository; a spawn brief; `tools/knowledge.py for` | `docs/knowledge/techniques/` | a way of doing a kind of work well that no single area rule reaches: its reason, its evidence, how it acts today |

**ADR-011, "Where new knowledge goes"**, a new item after item 5: "A way of doing a kind of work well, where no
single area rule reaches every file it applies to: an entry in `docs/knowledge/techniques/`. Its checkable part
goes to item 1, and the entry names what enforces it." The copy of this list in CLAUDE.md is an always-loaded
file and changes only on the maintainer's word.

**ADR-011, the lesson item of that list (item 7 today) and the Lessons ownership rule**: each gains "or a
knowledge entry" after "rule or skill".

**ADR-011, the Hooks ownership rule**: `point-knowledge.sh` is a second on-demand carrier beside the path rules,
for the reasons under "Negative" above.

**ADR-010**: `docs/knowledge/` is a layer of machine-read records beside the docs. Its exclusion of a vector
store stands.

## References

- Issue [#1613](https://github.com/haterade22/TAOM/issues/1613).
- [knowledge-library.md](../features/knowledge-library.md): the entry format, the commands and the steps.
- [The `/teardown` skill](../../.claude/skills/teardown/SKILL.md).
- [The teardown record of the first case](../knowledge/teardowns/2026-11-26-horse-armour-neck-and-head.md).
- `tools/knowledge.py`, `tools/tests/test_knowledge.py`, `.claude/hooks/point-knowledge.sh`,
  `tools/tests/test_point_knowledge_hook.py`.
- `.claude/rules/harness-loading.md` and `.claude/rules/harness-facts.md`: the sourced facts about what a hook
  note reaches.
- `.claude/rules/simplicity-criterion.md`, by which the alternatives were weighed.

## Related ADRs

- [ADR-011](./011-knowledge-delivery-tiers.md): Knowledge Delivery Tiers. This ADR adds the home that ADR-011
  lacks and uses its rule of the narrowest trigger.
- [ADR-010](./010-knowledge-base-architecture.md): Knowledge-Base Architecture. The store is a data layer beside
  the docs that ADR-010 organises.
