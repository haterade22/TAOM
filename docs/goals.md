# TAOM Goals

> **Owner: the maintainer.** The manager session ([ADR-015](adrs/015-self-managing-project.md),
> [project-manager.md](features/project-manager.md)) reads this file and never edits it. It
> selects work in the order of the table below.
>
> **Status:** ordered by the maintainer on 2026-11-18. The "Done when" cells are still empty.

## How to use this file

- **Order is priority.** Move a row up to make the manager work on it first.
- **One row, one GitHub milestone.** The milestone name is the row's "Milestone" cell. An issue
  with no milestone is not selected.
- **"Done when"** is what lets the manager propose, in the digest, that a milestone is done.
  Leave it empty and the manager works the milestone's issues but never calls the goal done.
- **Not now** holds goals the manager must not start. Move a row there to stop work on it.
- New player feedback and play notes go in the pinned issue "Manager inbox", not here.

## Goals, in priority order

Open issue counts are from the first sort on 2026-11-18
(`gh issue list --state open --milestone "<name>"` prints the current count).

| # | Goal | Milestone | Open issues | Done when |
|---|---|---|---|---|
| 1 | No crash, hang or broken save in normal play | Stability | 20 | |
| 2 | Performance | Performance | 24 | |
| 3 | Harness, tools, gates and engine bumps | Harness and tools | 42 | |
| 4 | Creatures and creature mounts | Creatures | 19 | |
| 5 | The War of the Ring campaign: war rule, momentum, pledges, hosts | War of the Ring | 16 | |
| 6 | Battle modes and battle AI: Staged Siege, Contested Ground, doctrines, combat rules | Battles | 20 | |
| 7 | Troops, equipment ladders, kits, economy and item values | Troops and balance | 29 | |
| 8 | Co-op campaigns work on the dedicated server | Co-op | 11 | |
| 9 | The Long Defeat, hero companions and Hero Defence | Long Defeat and heroes | 14 | |
| 10 | Campaign and screen quality of life | Quality of life | 26 | |
| 11 | Enlistment, field commission, careers and special resources | Enlistment and careers | 26 | |
| 12 | New realms and map content: Arthedain, Lindon, Ered Luin | Realms and map | 14 | |
| 13 | Localization | Localization | 6 | |

## Not now

| Goal | Milestone | Open issues | Why |
|---|---|---|---|
| Work that waits on an artist, the Modding Kit or another team | External | 12 | Nothing here can start until the other party delivers |

## Standing calls

Taste and balance rules the manager must hold to in every goal. The maintainer adds to this list.

- A troop keeps its weapon category; no swap across categories.
- A change to gameplay numbers or behaviour is proposed in the digest, never applied unasked.
