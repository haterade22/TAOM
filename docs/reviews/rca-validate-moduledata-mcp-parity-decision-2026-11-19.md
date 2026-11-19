# RCA: the ModuleData commit hook's exit 2 note (plan 059, #623 decision A, deep review, 2026-11-19)

## Top line

Commit `09f03e8c7` makes `.claude/hooks/check-moduledata-validation.sh` answer a validator exit 2 (no
game install, no ModuleData, a usage error such as a renamed `--code` flag) with an UNCHECKED note and
the stderr tail, still an allow, where it used to answer a silent `{}`. The tier 2 review (Lens 5
Data flow, Tooling correctness) and the lead's differential sweep confirmed the decision is wired for
every exit 2 the hook can reach, that career kit drift still blocks, and that no case the old hook
refused is now allowed. One MEDIUM and four LOW findings (one of them the lead's) were confirmed in
the changed code and fixed in the review's commit. Nothing reached a player: the branch is not merged. Codex did not run.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | MED | The new `RC -eq 2` branch exits before the shared `RC -ne 1` tail, which printed the career kit "could not judge, UNCHECKED for career kit drift" note. With a validator exit 2 and a kit tool that crashed or timed out, the base hook named the kit and the new one did not (sweep rows `v2-* / k-crash`: base `allow-note[K]`, head `allow-note[2]`). | Other: a new early exit skips a shared output after it | The branch was modelled on the rc=3 branch, which has had the same gap since it landed, and the new test ran with the kit stub at exit 0, so the bypassed tail was never on the test's path. | The branch appends the kit sentence when `KIT_UNCHECKED=1`; test 7m "a validator exit 2 with a kit tool that cannot judge names both". Lesson in `lessons/build-tooling-workflow.md`. |
| F2 | LOW | The note builder read stderr as strict UTF-8. One byte that is not UTF-8 raised in the Python snippet, the snippet printed nothing, and the hook fell back to `{}`: the "checked, clean" answer decision A exists to replace (sweep rows `v2-badbytes`: `allow-silent` on head). No real caller was found that writes such a byte. | Missing null guard: the failure path of the note itself | The `{}` fallback was copied from the rc=3 branch as the generic fail-open shape; nobody asked whether `{}` undoes this branch's purpose. | Stderr is decoded with `errors="replace"`, and the fallback is now a fixed UNCHECKED note; test 7m "a validator exit 2 with stderr that is not UTF-8 still says UNCHECKED". Lesson in `lessons/build-tooling-workflow.md`. |
| F3 | LOW | On the no-install exit 2 (`validate_moduledata.py:985`), the cause line `WARNING: Bannerlord Modules folder not found` is stderr line 6 of 38, and the 10-line tail held only SKIPPED notices, so the note could not say which of its three causes fired. | Other: the note's evidence did not hold its cause | The test stub wrote one usage line, which the tail always holds; the real no-install stderr was not read. | The note now puts up to three cause lines (`ERROR:`, `not found`, `: error:`) from above the tail ahead of the tail; test 7m "a validator exit 2 from a missing install names the cause". |
| F4 | LOW | The new branch's comment listed "a missing baseline tag" as an exit 2 cause. That exit (`validate_moduledata.py:801`) is reached only through `--regenerate-shipped-troop-ids`, which the hook never passes. | Other: comment wider than the code | The comment listed every `return 2` in the validator without reading which ones the hook's arguments reach. | Comment corrected. No lesson: one-off. |
| F5 | LOW | `docs/reference/hooks-catalog.md:30` said only "a validator crash is allowed with a note". | Other: doc drift | The commit updated the feature doc and not the catalog row. | Row now names exit 2 too. No lesson: one-off. |

The new exit 2 test grepped the raw answer for text and never parsed it as JSON; it now reads the
answer as JSON (with a quote, a backslash and non-ASCII in the stub's stderr), and a validator exit 2
with kit drift is pinned as a deny. Both were test gaps, not defects.

## Root-cause pattern

F1 and F2 share one cause: the rc=2 branch was copied from the rc=3 branch, and the copy brought two
properties of the original that nobody re-derived for the new branch (an early exit in front of the
kit note, and a `{}` fallback). Both gaps remain in the rc=3 branch, which this commit did not change.
FOLLOW-UP (no issue: the review lead cannot file issues; the orchestrator files one at wrap-up): give
the rc=3 branch and the rc=1 "no findings" note the same kit sentence and the same fallback.

## Why each agent missed these

- **The executor (commit `09f03e8c7`):** wrote the branch from its rc=3 sibling and a test with one
  stub; it did not cross the new exit code with the kit stub's states or read the real no-install
  stderr.
- **Tooling correctness:** found F1, F2 and F5, and both test gaps.
- **Agent 5, Data flow:** found F1 to F3 and F5. Neither lens flagged F4; both named `:801` as
  unreachable but did not read the comment that cited it.
- **Agents 1, 3, 4 and 6:** skipped by tier. Agents 2 and 7 not in scope (no C#, no ModuleData).
