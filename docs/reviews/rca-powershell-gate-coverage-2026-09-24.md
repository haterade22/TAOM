# RCA: plan 027, PowerShell gate coverage (deep review and Codex, 2026-09-24)

**Summary.** Plan 027 (`96afb6fb..05dbc0d4`, branch `improve/027-powershell-gate-coverage`) put
the nine PreToolUse git gates behind one `Bash|PowerShell` registration and a shared reader,
`.claude/hooks/_shellwords.py`, that hands a PowerShell command to each gate as Bash text. The
review found that `validate-push.sh`, the repo's only force-push guard, now **allowed eight shapes
(ten payloads across both tools and branches) it refused at `96afb6fb`**, while its own comments and the hooks catalog promised that reading
PowerShell "never makes this gate refuse less than before". Every one was reproduced against the
old hook (`git show 96afb6fb:`) and the new one. Two changes caused them: a new `#` comment strip
that ran before anything knew where the quotes were, and a new option-value skip that ran after the
quotes had been flattened away. Beside those, the reader missed PowerShell statement forms that run
git (an assignment, the `.` operator, `${name}`), the commit gate invented message text for piped
producers it could not evaluate, and a quadratic loop and three Python starts put two gates close to
their 5 s kill (lesson 3). All were fixed on the branch with a failing row first; a base-versus-fixed sweep of
5,896 combined shapes found no other refused-then-allowed case.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `validate-push.sh` drops a push after a quoted ` #`: `git -c "user.name=a #b" push --force origin <trunk>` after a heredoc line holding one `"`, `-o 'ci #1'` after `Don't stop` or `$'it\'s'`, and PowerShell typographic quotes (`‘a #'`), each rc 2 at base and rc 0 at `05dbc0d4` | Logic error (regression) | The plan relaxed one shape (a trunk named in a trailing comment) with a per-segment `s/[[:space:]]#.*//` over the quote-blind split, which by construction cannot know whether the `#` is quoted; the "never refuse less" claim was argued from the named examples, never tested against the old hook | Comment dropped only where no quote (ASCII or typographic) precedes it; reader reads typographic quotes; four 7c rows plus two JSON-escaped typographic rows; lesson 1 |
| 2 | HIGH | `git -C "E:\R&D" push --force origin ("<trunk>")` and the `$(...)` twin from PowerShell: rc 2 at base, rc 0 at `05dbc0d4` | Logic error (regression) | The quoted split moved from the raw text (tool escape) to the reader's text, where `( )` are statement breaks, so the refspec left the segment; the raw text was kept only for the quote-blind split, where the quoted `&` cuts git from push | The raw command's own quoted split (the pre-027 split) is judged again, via `_shellwords.py push`; 7c rows; lesson 1 |
| 3 | HIGH | `git push --force -o "" origin HEAD:<trunk>` (Codex P1): rc 2 at base, rc 0 at `05dbc0d4` | Logic error (regression) | The new `-o` value skip ran on quote-flattened tokens, so a lost empty value made the skip take the remote and the refspec became the remote | Positionals judged twice, with and without the value skip; a quote-aware variant keeps argument boundaries (`-o "ci skip"` too); 7c rows; lesson 1 |
| 4 | HIGH | `-fo ci.skip origin`, `--push-opt ci.skip`, `--recurse-submodules check`, `-vfu` on or to a trunk pass (also at base) | Logic error (incomplete fix) | The plan fixed the named spellings of the value-taking options, not git's grammar for them (clusters, prefixes, one option missed) | Short clusters read as git reads them, long-option prefixes, `--recurse-submodules`; 7c rows |
| 5 | HIGH | PowerShell `$r = git commit -m "no label"`, `$null = git reset --hard`, `$out = git add -A` pass the commit and confirm gates | Missing vanilla gate (grammar) | The reader's command position started only after a separator or `&`; the plan's parser table listed quoting and blocks, not assignments | Assignment operators end the statement head; unit tests and 7e rows; lesson 2 |
| 6 | MED | `${env:USERPROFILE}` split into a script block, so `git -C ${env:USERPROFILE}\repo commit ...` hid the commit; `. git commit` hid it too | Missing vanilla gate (grammar) | Same enumeration gap | `${...}` one word; `.` a call operator; unit and 7e rows |
| 7 | MED | `piped_text` read a command name (`pbpaste`, `Get-Clipboard`), a variable (`$MSG`) or a printf argument as the commit message and denied on invented text (Codex P2, lenses T13 and F3); Bash cases allowed at base | Logic error (regression, over-block) | The reader erases the difference between a PowerShell string literal and a command word, and the producer model ignored printf's format | Reader writes a PowerShell value statement as `echo <value>`; only echo, Write-Output and printf `%s`/`%s\n` with a `$`-free word are read; section 6 and 7e rows; lesson 2 |
| 8 | MED | `validate-push.sh` took 4.3 to 4.8 s of its 5 s registration on a 1 MB PowerShell payload (base 2.9 to 3.5 s) (three Python starts, the raw text doubled into a bash split); a killed gate fails open | Other: timing | The plan's timing rows used 100 KB; the "Timing headroom" note blamed pre-existing code | One `_shellwords.py push` run returns every split: 1 MB PowerShell median 746 ms (05dbc0d4: 4,466 ms), a plain push 345 ms (783 ms) |
| 9 | LOW | `block-broad-git-add.sh` quote strip quadratic in one segment (100 KB message: 2.5 s) | Other: timing | The fork-free loop was timed on many short segments, not one long one | `sed` above 4 KB, the loop below: 686 ms (05dbc0d4: 2,906 ms) |
| 10 | LOW | `_shellwords.py` `_escape` searched for `}` without a bound (quadratic on many `` `u{ ``) | Other: timing | Adversarial input not considered | Bound at 6 hex digits, PowerShell's own limit; unit test |
| 11 | LOW | Reader diverged from PowerShell's parser: `'HEAD'--hard` is two words, a pipe ends `--%`, a no-break space separates | Missing vanilla gate (grammar) | Parser probes covered the plan's table only | Fixed with unit tests |
| 12 | LOW | Bash `GIT_TRACE=0 GIT commit` and `'git' reset --hard` not renamed | Convention inconsistency | Rename anchored only after a separator | Assignment prefixes and quoted `git` renamed; unit and section 6 and 7e rows |
| 13 | LOW | A quoted path alone (`'.\build.ps1'`) marked verification (Codex P2) | Logic error | The mark hook strips quotes from the first word, and the reader lost that PowerShell prints a quoted value | Covered by the `echo <value>` rewrite; 7d rows. `$v = { dotnet test }` still marks: known limitation, needs Mike |
| 14 | LOW | 7e's registration inventory came from the settings under test (Codex P3); the reader fallback had no test; 7e did not check that a gate reads through `taom_hook_command` | Other: test oracle | Self-derived oracle; the fallback proved only by hand | Fixed list of nine; fallback rows; grep row per gate |
| 15 | LOW | Docs: hook-authoring and the catalog claimed 7e rows "under both tool names" that do not exist, the catalog still said to prefer jq and to prefilter on `*git*`, `mcp-servers.md` said the hooks match Bash only, `harness-facts.md` kept its old verified date | Other: stale docs | Written from the plan, not from the tests | Corrected |

## Root-cause pattern

Findings 1 to 4 share one shape: **a gate relaxation argued from examples instead of proven against
the old gate.** Plan 027 made `validate-push.sh` refuse less in two places on purpose (a trunk
named in a comment; an option's value taken for the remote), and each relaxation consumed text
that another stage of the gate had already reinterpreted: the comment strip ran on text whose
quotes it could not see, and the value skip ran on tokens whose boundaries were gone. The plan and
its two cold reviews checked the named examples and wrote "never refuses less than before" as a
property. No test compared the old hook and the new one on the same inputs. The sweep that would
have caught the three regressions (1 to 3) took one script and twelve minutes (5,896 shapes, 0 refused-then-allowed
after the fixes, 190 newly refused).

Findings 5 to 7 and 11 share a second shape: **a translator written from a table of forms instead
of the target grammar.** The reader handled every PowerShell form the plan's parser table listed,
and none it did not (assignments, the `.` operator, braced variables, typographic quotes, value
statements). Each was one `ParseInput` call away.

## Why each agent missed these

- **The implementer and the plan's two cold reviews** caught the per-line `#` strip and an
  unescaped `}` but accepted "never refuses less" from examples; the second review's own standard
  ("refused before, allowed after" is blocking) was never turned into a differential test.
- **Codex** found finding 3 (the only regression it reported) and findings 7, 13 and 14 by static
  trace. It did not model quote parity across heredoc lines or PowerShell's typographic quotes, and
  it did not enumerate PowerShell statement forms, so findings 1, 2, 5 and 6 are lens-only.
- **Standards (Agent 1)** is not a behaviour lens; it caught the doc overclaims (15) and pointed the
  correctness lens at the reader's `{` handling.
- **Efficiency (Agent 3)** found 8 to 10 but judged only parity of verdicts, not the verdicts.
- **Completeness (Agent 4)** found the missing option (`--recurse-submodules`), the untested
  fallback and the missing pins, from git's usage and the test inventory, without executing shapes.
- **Data flow (Agent 5)** and **Tooling** found every regression by running the old hook beside
  the new one; neither ran a sweep, so each found a different subset (Agent 5 findings 2 and the
  double-quote heredoc; Tooling the apostrophe heredoc, typographic quotes and assignments).
- **Design (Agent 6)** found `${}` and the cluster grammar while simplifying, not while hunting.

## Feedback memories to codify

The two patterns above are systemic and go to `docs/reviews/lessons/build-tooling-workflow.md`
(three entries appended with this RCA). No memory file: the lessons file is the canonical record.
