# Knowledge store

One file per technique in `techniques/`, and the measured record behind a group of them in `teardowns/`. The full
description, the entry format and the steps to add an entry are in
[knowledge-library.md](../features/knowledge-library.md); the decision is
[ADR-016](../adrs/016-technique-library-and-procedure-database.md). The steps that take a better result apart and
write its record and entries are the `/teardown` skill ([SKILL.md](../../.claude/skills/teardown/SKILL.md)).

Ask the store, from any folder:

```
python tools/knowledge.py for <file> [<file> ...]    the entries for these files
python tools/knowledge.py find <words>               entries, procedures and task-table rows with these words
python tools/knowledge.py show <id>                  one entry
python tools/knowledge.py brief --paths <files> --task <phrases>    the block for a subagent's brief
python tools/knowledge.py debt                       what does not act yet
python tools/knowledge.py procedures [--orphans]     the procedures of the repository, computed
python tools/knowledge.py check                      after a change of the store, before the commit
```

Rules of the folder:

- The file stem is the id. It never changes and is never reused: retire an entry with `status = "retired"` and a
  reason, and keep the file.
- No index file and no generated file lives here. Several checkouts commit in parallel (`git worktree list`), and
  such a file collides at every merge. Every view is printed by the tool.
- An entry says how it acts today. Only `gate` and `code` count as acting. A `report` names a file of the
  repository and a text in it that `check` finds, and still says in `next` what would make it act.
- An entry never repeats a line of a path rule or a trap sheet.
- A trigger names the builder files where the work happens, never a whole work folder. The exception is a folder
  that holds only reference results, such as `*/tripo_in/*`.
