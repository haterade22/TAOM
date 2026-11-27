# RCA: the push judge's redirection split (#1556, PR #1609, 2026-11-26)

`/deep-review` at tier 1 over a batch of four PRs (#1609 to #1612), with a Codex pre-review at `ultra`
and the Data flow, Engine compatibility and Tooling lenses. Codex found two HIGH regressions in PR #1609,
so a second wave ran on its files: Efficiency, Completeness and Design. Nothing reached trunk: every
finding below was fixed on the PR branch before the merge. Each was reproduced against the hooks of
`origin/bannerlord-1.5.x` before it was fixed.

## What the PR did

`segments()` and the quote-blind split in `.claude/hooks/_shellwords.py` cut a command at every `&` and
`|`, including the ones inside an fd redirection (`2>&1`, `&>log`, `>|log`), so the words after the
redirection fell off the line that holds `push` (#1556). The PR made such an `&` or `|` a non-separator,
in both splits, for every hook that reads them.

## Findings

| # | Sev | Finding | Found by | Why missed | Fix |
|---|---|---|---|---|---|
| F1 | HIGH | `git push --force origin &>/dev/null` on trunk was allowed: the judge kept the `&` of `&>` as a refspec, so the current branch (and `push.default=matching`) went unjudged | Codex, by comparing the judge at trunk and at the PR head | The new rows all named an explicit refspec after the redirection; none tested a push with no refspec | The judge's redirection regex is `&?>\|<`, so the `&` goes with the `>`; `ISSUE_1556` rows |
| F2 | HIGH | `echo \>\|git push --force origin bannerlord-1.5.x` (and `\>&`, `\<&`) was allowed: the rule looked at the character before the `&` or `\|` and could not see that the `<` or `>` was escaped | Codex; the Tooling lens on its own | The rule was written for redirections and tested only on them; nobody fed it a separator that follows a literal `>` | Trunk's split stays, and the redirection-aware split is read beside it, so the judge refuses whatever trunk refused |
| F3 | MED | The same split fed the confirm gates (`_shellwords.py commands`): `echo a\>&git reset --hard` no longer asked | Tooling lens | The PR scoped the change to the push judge, but changed a function every gate calls | `segments()` defaults to trunk's separator; the redirection reading is the push reader's only |
| F4 | HIGH | The first fix doubled every pass of the push reader, so a 2 MB PowerShell trunk force push overran the judge's 3 s budget and got an ask instead of a block (test_hooks.sh 7i) | Efficiency lens, by timing trunk and the fix | The fix was proved for verdicts, not for cost | The second split runs only where a `REDIRECTION_PAIR` occurs, and the lines are deduplicated before the costly passes; 7i shape F times the second split |

The union of the two splits is safe by construction: `Judge.run` refuses at the first refused line, and
the extra lines only add lines. A fuzz of 170,768 command, tool and branch cases found 0 commands that
trunk refused and the fix allows; the PR as first written had 4,908. The deduplicated reader gave the same
candidate lines as the first fix in 43,494 generated commands.

## Lessons

- A change to a function every gate shares is a change to every gate: list the callers before the first
  edit, and widen only the reader the issue names.
- A gate that may only refuse more is proved against the gate it replaces, not against the cases the
  change was written for: diff the verdicts of both versions over generated input.
- A fix proved for verdicts still owes its cost: a gate that overruns its budget changes its answer.

Recorded in `docs/reviews/lessons/build-tooling-workflow.md`.
