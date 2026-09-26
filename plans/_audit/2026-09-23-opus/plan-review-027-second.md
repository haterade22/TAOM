# Second cold review of plan 027 (PowerShell gate coverage)

Plan: `E:\repos\taom-improve\wt-027\plans\027-powershell-gate-coverage.md` (2139 lines), read in
full with the template's "Quality bar". Worktree at `96afb6fb`, status only
`?? plans/027-powershell-gate-coverage.md` (Step 0 not applied). The first review
(`plan-review-027.md`) is not repeated here; its `{\}` fix is in the plan and holds (below).

## Method

Everything under `E:\repos\taom-improve\scratch\plans\027\review2\`:

- `apply.py` copies `.claude/hooks` (only that folder) to `review2/new/` and applies Steps 3, 5,
  7 and 9 **literally**, lifting every code block from the plan by line number and anchoring each
  replacement on the plan's quoted excerpt (every anchor matched exactly once). `bash -n` is clean
  on all hooks; `python -m unittest tools.tests.test_shellwords` gives `Ran 33 tests ... OK`.
- `run_cases.py` feeds each payload to the current hook (worktree) and the planned hook (scratch)
  and prints both verdicts (`!!` = refused before, allowed after). Case files `cases_a.py` (107
  rows) and `cases_b.py` (10 rows), output in `out_a.txt`.
- `vp_rows.sh` runs validate-push's existing `VP_CASES` and `VP_TOOL_CASES` rows, the plan's 6a,
  6b and 6c rows, and nine regression rows of mine, against any hooks folder (97 rows).
- `mvr_cases.py` (mark-verification-run), `timing.py` and `timing2.py` (large payloads).
- `new2/` is the same planned hooks plus one candidate fix to validate-push (see Blocking 1 and 2).

Confirmed by execution: the Step 6 RED set is exactly the plan's 19 rows (the current hooks fail
exactly those and nothing else of the 97); all 97 rows except my nine pass on the planned hooks;
the Step 7 runtime smoke prints `bash rc=2`, `ps rc=2`, `ok rc=0`, `0` on the planned hooks; the
Step 0 edit, applied in memory, yields exactly the expected list and 26 registrations; section 2's
frontmatter scan counts 3 (2 + 1), so `all 29 registrations` holds; section 4 runs 25 hooks, so
+25 holds; 7e holds 58 verdict rows (141 total checks out); `grep -c 'FAIL'` counts one line per
failure (the summary prints `Failures:`, which does not match).

## Blocking

### 1. validate-push, Bash: the blind-split comment strip lets a real trunk force push through

Step 7c strips ` #...` from each **line** before the line is split at `; & |`. A `#` inside a
quoted value (`"fix #1"`, `"a #b"`) therefore deletes every later command on that line from the
blind split. The quoted split normally still catches the push, but not when an unbalanced
apostrophe earlier in the text (a heredoc line such as `Don't`, or bash `$'it\'s'`) glues the rest
into one quoted segment: there the trunk token keeps its trailing `;` (`bannerlord-1.5.x;`), which
`is_protected` does not match. Before plan 027 the unstripped blind split refused all of these;
after it, all pass (rc 2 before, rc 0 after, each run on both hook sets):

| Tool | Command (`\n` = newline) |
|---|---|
| Bash | `echo $'it\'s'; echo "a #b"; git push --force origin bannerlord-1.5.x; echo done` |
| Bash | `cat <<EOF\nit's\nEOF\necho "a #b"; git push --force origin bannerlord-1.5.x; echo done` |
| Bash | `git commit -F - <<'EOF'\nDon't stop\nEOF\ngit log --grep "fix #1"; git push --force origin bannerlord-1.5.x; git log -1` |
| Bash | `cat <<EOF\nit's\nEOF\ngit commit -m "x\n#1"; git push --force origin bannerlord-1.5.x; echo` (a quoted line that starts with `#` is dropped whole) |

The third shape is ordinary agent output (a heredoc commit, a `--grep` with an issue number, a
push). The plan's Maintenance notes say the review "confirmed" the quoted split backs the strip
up; that holds only without glue. The Risk line ("for Bash text the reader changes only a git
named by a path or in capitals") also misses this Bash change.

**Candidate fix, verified in `new2/`:** strip per blind **segment**, after the `[;&|]` split,
not per line:

```bash
SPLIT=${COMMAND//[;&|]/$'\n'}
BLIND=""
while IFS= read -r L; do
  [[ "$L" == '#'* ]] && L=""
  [[ "$L" == *'#'* ]] && L=${L%%[[:space:]]#*}
  BLIND+="$L"$'\n'
done <<< "$SPLIT"
COMMAND=$BLIND
```

A `#` inside a quote then cuts only its own simple command. With this change all 97 rows pass,
including the plan's `# bannerlord-1.5.x later` rows (the comment fix survives) and the four rows
above. Add those four as `VP_CASES` / `VP_TOOL_CASES` rows expecting 2 (Step 6 RED count then
changes; they pass today, so they are not RED rows).

### 2. validate-push, PowerShell: shapes refused before now pass

`validate-push.sh` was already registered for PowerShell, so these are regressions, not new gaps.
The plan's reader turns PowerShell `( )` into statement breaks and keeps a comma list in one word;
validate-push now judges only the reader's text, never the raw command. PowerShell itself was
checked at review (pwsh 7.6.6): `& ("python") ...` runs python, and `("paren")` as an argument
reaches the native command as `paren`.

| PowerShell command | before | after |
|---|---|---|
| `Start-Process git -ArgumentList 'push','--force','origin','bannerlord-1.5.x' -Wait` | rc 2 | rc 0 |
| `Start-Process git -ArgumentList 'push', '--force', 'origin', 'bannerlord-1.5.x'` | rc 2 | rc 0 |
| `[Diagnostics.Process]::Start('git', 'push --force origin bannerlord-1.5.x')` | rc 2 | rc 0 |
| `& ("git") push --force origin bannerlord-1.5.x` | rc 2 | rc 0 |
| `& (Get-Command git) push --force origin bannerlord-1.5.x` | rc 2 | rc 0 |
| `git push --force origin ("bannerlord-1.5.x")` | rc 2 | rc 0 (cwd on a feature branch) |
| `git push --force origin $("bannerlord-1.5.x")` | rc 2 | rc 0 (cwd on a feature branch) |

**Candidate fix, verified in `new2/`:** also feed the raw command (the pre-027 read, with the
`\`-newline and backtick-newline joins) into the same per-segment blind split as a third input.
This restores "never refuse less than before" for PowerShell, keeps every plan row green (97 of 97
in `new2/`), and costs one more Python start. A cheaper variant is a reader mode that prints the
raw command too. Either way the plan's Step 7 text, the 7c comment block and the hooks-catalog row
need the third split named, and the Risk section should stop implying the reader bounds
validate-push's behaviour.

### 3. The new 7e `ps-lines` timing row for `block-broad-git-add.sh` fails under load

`block-broad-git-add.sh` forks `sed` once per git segment (`rest=$(printf ... | sed -E ...)`,
line 106). The planned `ps-lines` payload has 100 git segments. Measured at review, three runs
each: planned hook, PowerShell `ps-lines` 6693, 6042, 5940 ms; planned hook, Bash twin 5583,
5884, 5374 ms; **current** hook, Bash twin 5313, 5512, 7413 ms, against the row's 4000 ms limit
(80% of 5 s). A bare loop of 100 `$(printf | sed)` took 10571 ms at the same time, so the
machine was loaded (parallel agents), but that is the executor's normal environment. The cost is
pre-existing, not the reader's, yet the plan adds the first row that measures it. The executor
then hits "A timing row exceeds 80% of its registration twice in a row" and stops at Step 5. It is
also a real fail-open: a killed ask gate allows, now for PowerShell too. Either replace that `sed`
with a bash-native quote strip (the file is already in scope for Step 5) and time it, or give
this gate a payload with fewer git segments and record the per-segment cost as a known limit.

## Non-blocking

1. **mark-verification-run, PowerShell `.\build.ps1` no longer marks.** The reader quotes any
   word holding `\`, so `.\build.ps1 -RunTests` becomes `'.\build.ps1' -RunTests`, and the
   `case "$first"` at `mark-verification-run.sh:119` no longer matches. Measured before/after
   marking: `.\build.ps1 -RunTests` 1/0, `& .\build.ps1 -RunTests` 1/0, `E:\repos\TAOM\build.ps1`
   1/0, `Set-Location E:\repos\TAOM; .\build.ps1` 1/0 (`./build.ps1` and `pwsh .\build.ps1` still
   mark). Safe direction (one extra Stop reminder), but `.\build.ps1` is the everyday PowerShell
   spelling. Fix: drop quotes from `first` before the `case` (`first=${first//\'/}`), and add a
   `MVR_TOOL_CASES` row `PowerShell|1|.\build.ps1 -RunTests`.
2. **PowerShell shapes still outside the reader, none claimed by the plan:** `& ("git") commit ...`,
   `Start-Process git -ArgumentList ...`, `pwsh -c "git commit ..."` and `Invoke-Expression 'git
   reset --hard'` pass the commit and confirm gates (they were unregistered for PowerShell before,
   so no regression). Worth one line in the hooks-catalog "Not read" list next to `$(...)` inside a
   double-quoted string.
3. **Pre-existing Bash gap not in the deferred list:** `git  commit -m "docs: no label"` (two
   spaces) and a tab between `git` and `commit` pass `check-commit-subject-version.sh` before and
   after (the two-stage matcher wants one space). The PowerShell twin is now denied because the
   reader rejoins words with one space. Add to the Maintenance "Deferred" list.
4. **`--repo` skip is right**, checked against git: `git push --repo origin --force b2` fails with
   "'b2' does not appear to be a git repository" (the positional wins as the repository), so the
   new rc 0 for `git push --repo origin --force <trunk>` is not a hole. The reviewer probe list
   could say so.
5. **Step 10a anchors are wrapped prose.** The quoted old and new texts for `hooks-catalog.md`
   line 3 and the validate-push row are wrapped across plan lines with a two-space list indent;
   a literal Edit `old_string` copied from the plan fails. Say "the quoted text is one line in the
   file" or give them as column-0 code blocks, as Steps 5 and 7 already do.
6. **Step 7c comment-block text** should be revised with whichever fix lands for Blocking 1 and 2
   (it currently says the # is cut per line and that the quoted split backs it up).
7. **A command that is only a comment** (`# git push --force origin <trunk>` in Bash) is still
   refused: `segments` prints an empty line, `[ -n "$Q" ]` fails, and QSEGS falls back to the
   unstripped text. Harmless over-block; note it next to the comment fix so nobody "fixes" the
   fallback.

## Checklist items asked for

- **TDD order:** RED then GREEN at 2/3, 4/5, 6/7, 8/9; the RED counts 41, 19, 2 are stated; 19
  confirmed here by execution. No C#.
- **STOP conditions:** plan specific (settings re-edit, RED counts, timing twice, `git init -b`,
  unexpected live refusal, payload shape). Blocking 3 will trigger the timing STOP under load.
- **Done criteria:** all commands with expected output; the scope `diff ... && echo SCOPE-OK`
  replaces the old judgment item.
- **Step 0:** consistent: line 64 is `"matcher": "Bash",`, lines 116 to 125 are exactly the
  PowerShell group, the result parses, prints the expected list and totals 26; `git diff --stat`
  `1 insertion(+), 11 deletions(-)` is arithmetic-consistent. The executor never edits
  `settings.json` (Scope, Step 0, Step 5 only stage it; STOP on any other change).
- **Commit list vs scope:** 2 + 11 + 1 + 1 + 4 new paths = the 19 files of the Scope list and of
  the Step 11 item 11 list.
- **Timing rows:** 7e has 27 (three payloads per gate) plus the existing 4b, 7c 100-line and 7d
  100 KB rows. Extra measurement here: a 1 MB unquoted PowerShell word took validate-push 3659 ms
  and check-commit-subject-version 7113 ms (limits 4 s and 8 s); not a planned row, just close.
- **Excerpts:** every anchor `apply.py` used matched exactly once; spot-checked line numbers
  (extraction blocks 36/51/47/42/49, `cd` lines 61/70/66/62/68, header line 3 in all eight,
  `check-claude-files-tracked.sh:84`, `_pybin.sh:80`, validate-push 32-47, 54-59, 83-121, 140-155,
  205-220, mark-verification-run 20, 48-53, 54-87) all match `96afb6fb`.

## Verdict

Not ready for a weak executor: Blocking 1 and 2 ship a validate-push that allows force pushes it
refuses today (Bash and PowerShell), and Blocking 3 is likely to stop Step 5 on a loaded machine.
The rest of the plan (reader, other gates, harness arithmetic, docs, commits) held up under
execution.
