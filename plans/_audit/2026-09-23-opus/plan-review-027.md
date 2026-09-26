# Cold review of plan 027 (PowerShell gate coverage)

Plan: `E:\repos\taom-improve\wt-027\plans\027-powershell-gate-coverage.md` (2035 lines), read in
full. Code read in the worktree at `96afb6fb` (HEAD confirmed `96afb6fb`, status only
`?? plans/027-...`, so Step 0 has not been applied yet). Template: `.claude/skills/improve/references/plan-template.md`.

## Verdict

**Not executable as written: one blocking defect (Step 7d).** Everything else checked out, much of
it by running the plan's own code without writing any file (in-memory execution, hooks piped
payloads on stdin).

## What was verified by execution

| Claim | Plan lines | Result |
|---|---|---|
| Reader + unit tests: 33 tests, OK | 548-756, 764-1047, 1049-1051 | **Confirmed**: loaded both code blocks in memory, `Ran 33 tests ... OK` |
| Step 4 RED: 26 7e verdict rows fail today | 1332-1334 | **Confirmed**: 26 fails, exactly the listed rows (the two `-F` file rows were skipped: they need harness temp files) |
| 7e commit-test (reach) rows: 12 fail today, all 28 pass after | 1335, 1261-1281 | Confirmed by reasoning plus reader output on each row |
| Step 5 GREEN: every 7e verdict row passes on reader output | 1489-1490 | **Confirmed** for all 56 checkable rows (commit gate run with the 5e `piped_text`/`commit_arg_lists` spliced in); the 3 new section 6 rows also pass |
| Step 6 RED: exactly 19 failures | 1574-1580 | **Confirmed**: 8 (6a) + 7 (6b) + 4 (6c), exactly the listed rows |
| Step 7 GREEN: all 7c rows pass | 1715-1718 | **FAILS as written**: 65 of 88 rows fail (every rc=2 row returns 0). With the one-character fix below, 88 of 88 pass and 100 push lines take ~0.4 s |
| Step 8 RED 2 / Step 9 GREEN | 1758-1760, 1805-1806 | **Confirmed**: all 13x2 MVR_CASES, 9 MVR_TOOL_CASES, both `is_interrupt` rows, the mention row, 100 KB and the PostToolUseFailure row behave as stated |
| Count arithmetic 651 / 685 / 691, 2126+33=2159, 31 registrations, 27 files | 1490, 1717, 1806, 433, 537, 1826 | Arithmetic consistent; 27 settings registrations, 9 events, 14 at 5 s confirmed; 26 tracked files under `.claude/hooks` today |

## Blocking

1. **Step 7d `CLEAN=${1//[\"\'(){}\`]/ }` breaks validate-push (plan line 1667).** The
   unescaped `}` inside the bracket expression closes `${...}` early. `bash -n` passes (so the
   Step 7 syntax check at line 1715 gives false confidence), but at runtime every `judge_command`
   call prints `}: command not found`, `CLEAN` is empty, no `git push` is ever found, and **every
   force push is allowed** (the only force-push guard fails open). Reproduced two ways: a
   one-line `bash -c` test (`bash -n OK`, then `}: command not found`, `[]`), and the spliced
   hook, where 65 of 88 7c rows fail. Fix: escape the brace,
   `CLEAN=${1//[\"\'(){\}\`]/ }` (verified: `a{b}c(d)e"f`g` becomes `a b c d e f g`, and all 88 7c
   rows then pass). A weak executor copying the block "exactly" gets ~65 failures at Step 7, with a
   plan that says expectations come from measurement; it will STOP or improvise in bash. Also add a
   runtime smoke to the Step 7 verify (for example one Bash payload `git push --force origin
   <trunk>` written to a scratch file must give rc 2), since `bash -n` cannot catch this class.

## Non-blocking

1. **Markdown-list indentation on code that must match exactly** (lines 141-153, 1393-1403): the
   `block-no-verify.sh` excerpt and its replacement are indented two extra spaces because they sit
   in list items. The file's lines are at column 0 (`import sys, json` at col 0, `print(` at col 4).
   A literal copy into an Edit `old_string` fails; the replacement pasted as shown is indented.
   Dedent both blocks, or say "strip the two-space list indent".
2. **Two-stage matcher excerpt is not verbatim for all five gates** (lines 172-182):
   `check-native-dll-crt.sh:54-56` also has `*"git -"*" commit-"*` in the first `case`, and in
   `check-claude-files-tracked.sh` the `cd` is at line 61 after a comment block, not right after
   the `esac`. Harmless (the plan does not edit these lines) but the drift STOP says excerpts must
   match.
3. **Off-by-one citation**: `check-commit-subject-version.sh:75-316` (line 188); the heredoc closes
   at line 317. `parse_commit` is at 164 as stated, but the `<<` stdin handling is at 178.
4. **DISK rule vs the harness**: `tools/test_hooks.sh` creates its sandbox with `mktemp -d` (C:
   temp), and `dotnet build`/`dotnet test` write TEMP and possibly NuGet data to C:. The plan puts
   scratch on E: but says nothing about TMP/TEMP for these runs (the repo already uses the
   `TEMP=E:/t TMP=E:/t dotnet test ...` shape, test_hooks.sh 7d). Consider prefixing the long runs
   with `TMP=/e/... TEMP=/e/... TMPDIR=/e/...`.
5. **`bash_to_posix` also rewrites line-start `Git`/`GIT` inside heredoc and multi-line quoted
   message bodies** (BASH_GIT, line 797, anchors on `\n`). A commit body line "Git reset --hard is
   gated" becomes `git reset --hard ...`, which block-dangerous-git's per-line split may now turn
   into an extra `ask`. Over-block only, untested; worth a Maintenance-notes probe line.
6. **Possible quadratic path in `ps_tokens`**: unquoted words grow by `word = (word or "") + c`
   (line 937). The 7e timing payloads use a quoted 100 KB word, so a huge unquoted PowerShell word
   is not timed. Consider a list buffer or one more timing payload.
7. **Blind-split comment strip** (lines 1624-1630) cuts at ` #` even inside quotes. Verified that
   `echo " #"; git push --force origin <trunk>` is still refused through the quoted split, but the
   blind split no longer backs it up; name this in the reviewer probes (it is partly there).
8. **Done criteria not fully machine-checkable** (lines 1964-1965, 1977-1979): the "or reconciled
   row by row" escape hatch and "`git show HEAD~4 --stat` .. `git show HEAD --stat` touch only
   in-scope files" are judgments. A single `git diff --name-only 96afb6fb..HEAD` compared against a
   listed file set would be a command with an expected output.
9. **Unverified by this review**: the 482-row baseline (line 374, orchestrator's run), the 2126
   unittest baseline and the `Passed 10767` dotnet count; running them would write to C: temp.

## Excerpt check (item 3)

All cited excerpts match `96afb6fb` except the entries in Non-blocking 2 and 3. Confirmed:
`settings.json` 62-126 (line 64 matcher, 116-125 the PowerShell group, 197 `Bash|PowerShell`);
the extraction blocks at `block-no-verify.sh:43-55`, `block-dangerous-git.sh:52-65`,
`block-broad-git-add.sh:62-74`, `validate-push.sh:32-47`, and the five python-only gates at the
cited lines; `block-no-verify.sh:61-66`; segment splits at 74 and 83; `commit_arg_lists` 128-139;
`check-claude-files-tracked.sh:84`; header line 3 in all eight; validate-push 54-59, 72-82,
83-121, 140-155, 173-183, 205-220; `mark-verification-run.sh` 20, 48-53, 54-87, 101-129;
`_pybin.sh:80` and its last line; `test_hooks.sh` (1377 lines) anchors 351, 444-446, 819-826,
900, 1132, 1138, 1143, 1221, 1244-1246, 1254, 1323, 1349; `SubModule.xml:6` is `v2.0.30`;
`build.yml` 178 and 205; docs anchors (`hooks-catalog.md` line 3, the blockquote at 43-44, the
eight `| PreToolUse (Bash) |` rows plus validate-push already `(Bash, PowerShell)` = 9;
`hook-authoring.md` 28 and 56; the harness-facts "30 hook events exist" row; `mcp-servers.md:46`).
`Main/SubModule.cs` and `Main/IoC.cs` are not cited or touched by this plan.

## Item 4 and 5 checks

- TDD order: RED/GREEN pairs at Steps 2/3, 4/5, 6/7, 8/9; no C# (ADR-002/007/008 named as not
  applicable). Issue-first: "create before implementation lands (orchestrator)". Binding
  decisions and rules named with one-line summaries. Single-owner files listed out of scope.
  STOP conditions are plan-specific (RED counts, settings re-edit, `git init -b`, unexpected live
  hook refusal). Planned-at `96afb6fb`; drift-check paths match Scope. Non-deploying commands carry
  `-p:DisableModuleCopy=true -p:ModuleId=`.
- Dashes: the only U+2013/U+2014 characters (lines 1407, 1868) are inside verbatim quotes of
  existing text used as Edit anchors, which is exempt. No secret values.
