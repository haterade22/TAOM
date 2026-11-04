# RCA: the Gauntlet stretch gate failed on the main checkout (2026-11-03)

**Top line:** `NoPrefab_GainsAStretchChildInsideACoverChildrenParent` failed in `/verify` on the main
checkout, on a tree with no prefab change. Git tracks the folder as `Main/_Module/GUI/PreFabs`. The
main checkout holds it on disk as `Prefabs`, and the eight `taom-*` worktrees on this machine hold
`PreFabs`. The gate takes its path from the disk and looked it up in `Known` with an ordinal
comparer, so every baseline entry missed on the main checkout. The gate came in with `e6dbea3e0`
(2026-10-31). Found during the #1078 verification.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | All five `Known` entries miss on a checkout that spells the folder `Prefabs`, so the gate reports five new offenders and five stale entries. No prefab is at fault | Gate correct only on its author's state | The state list in `.claude/rules/tests.md` names line endings, untracked and deleted files, a non-ASCII path, a subfolder, a missing tool and an empty input. It does not name path case | `Known` and `seen` compare with `OrdinalIgnoreCase`; path case joins the state list in `tests.md` and the lesson |
| 2 | LOW | A malformed prefab skips this gate at the `catch (XmlException)`, before `seen.Add`. If that file is in `Known`, the gate says "matches no prefab file; remove it", and that advice would drop a live baseline | Misleading gate message | Pre-existing; the catch came with the gate. `PrefabAttributeNameBindingTests.cs:351` reports the same file as "not well-formed" in the same suite, so the suite still fails | None here, see the follow-up below |

## Root cause

Git keeps one spelling for a path, and a case-insensitive filesystem keeps its own. A checkout made
long ago can differ from one made today, and `core.ignorecase` is true here, so `git status` shows
nothing. A test that builds a repo-relative key from `Directory.EnumerateFiles` reads the disk's
spelling, so an ordinal lookup against a literal path depends on which checkout runs it.
`tools/package_release.py` already folds case for this same folder (its comment names `GUI/PreFabs`
and `GUI/Prefabs`).

This is the same class as the lesson "Prove a gate on the states it will meet, not the one it was
written in" (`docs/reviews/lessons/build-tooling-workflow.md`, #1020), a repeat with a state that
lesson did not list.

## Why each lens missed these

Only lens 5 (Data flow, `effort: high`) ran on this fix, at tier 2. It found finding 2 and confirmed
the fix: it replayed the gate in Python over 83 prefab files, with no mismatch under folded compare
and all five mismatches under exact compare. It walked 8,803 tracked paths under `Main`, `Client`
and `TAOM.Tests`, and this folder was the only case mismatch. Lenses 1, 3, 4 and 6 were skipped by
tier. Codex did not run, because this session has no paid-dispatch word for this change.

I did not check which reviews ran on `e6dbea3e0`, so I make no claim about why they missed finding 1.

## Follow-up

`FOLLOW-UP`: finding 2 stays as it is. no issue: the sibling test fails the suite on the same file with
the right message, so only the wording of a second failure line is wrong, and a fix adds code for a
case that needs a malformed prefab already in `Known`.

## Feedback memories to codify

None. The rule goes in `tests.md` and the lesson.
