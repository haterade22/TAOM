# RCA: the #1020 quality gates (2026-10-26)

`/deep-review` of the five gates ported from mokevnin/dev-quality-gates
([record](adopt-dev-quality-gates-2026-10-26.md)), at tier 2: Data flow and Tooling in wave 1, Codex
`xhigh` beside them (on Mike's word), then Standards, Completeness and Design in a second wave
because wave 1 found a HIGH. Every finding below was checked against the files, a probe or the
installed tool before it was acted on.

**Top line:** one HIGH (the ShellCheck config file breaks on a fresh Windows checkout), found by
Data flow and Codex independently. Most of the rest share one cause: each gate was proven on the
state its author's worktree was in, and never on the states a later run meets.

## Findings

| # | Sev | Finding | Found by | Why missed | Fix |
|---|---|---|---|---|---|
| 1 | HIGH | `.shellcheckrc` was LF only because a tool wrote it; `core.autocrlf=true` and no eol attribute meant a checkout writes CRLF, and ShellCheck rejects a CR in its rc file (SC1017), so the gate fails everywhere after merge | Data flow, Codex P1 | Verified on the file as written, never as Git checks it out; the third instance of this class | The rc file is gone: the test runs `--norc` and allows SC2206 only on a `set -f;` line (Design P1, Mike's choice) |
| 2 | MED | Shell discovery read the index only (`git ls-files`): a new script is not linted before it is staged, a deleted one makes ShellCheck exit 2, and Git's quoted output breaks non-ASCII names | Data flow, Codex, Standards | Tested on today's 34 tracked, existing, ASCII-named scripts | `-z --cached --others --exclude-standard`, existing files only; tests for each case |
| 3 | MED | Ruff linted a hand-kept folder list, so a `.py` in a new folder escaped; Ruff reports a pass when it finds no file | Data flow, Tooling, Codex | The list matched today's tree | Ruff over `.` (it honours `.gitignore`) and a test that every git-listed `.py` is in Ruff's file list |
| 4 | MED | NuGet skips the audit on a no-op restore, so an unchanged checkout never hears of a new advisory | Data flow, Codex | The restore was proven once, on a full restore | `/verify` Step 1b: `dotnet restore TAOM.sln --force-evaluate`; a probe showed the plain second restore is a no-op ("All projects are up-to-date") and the forced one is not |
| 5 | MED | The C# scanner's string blanking misread interpolated strings (a real `[Obsolete]` erased after `$"{M("http://")}"`), and its attribute regex missed multi-line and nested lists and flagged an indexer | Codex | Source text was parsed by regex | `[Obsolete]` found by reflection over the four compiled assemblies; directives matched per line with no blanking |
| 6 | MED | The Gitleaks step: from a subfolder the ignore file is not found; outside a repo Gitleaks prints "0 commits scanned, no leaks found" and exits 0; exit 1 meant both a finding and a crash | Tooling, Data flow | Run once, from the root | The command resolves the repo root, uses `--exit-code 2`, and the step reads the commit count first |
| 7 | MED | The skill-key allowlist got a third hand-kept copy | Data flow, Tooling | Copied from `test_hooks.sh` 3b | The Python lint reads the `harness-facts.md` row |
| 8 | MED | No provenance-register row for this adoption or for #996 and #1007, though an adoption is not complete without one | Completeness | The rule's paths never match an adoption's files, and the adoption procedure did not mention the register | Rows for all six sources; a line in `external-repo-adoption.md` step 5 |
| 9 | MED | No setup record for the three tools on another machine | Completeness | Installed on the desktop and written down only in the review | `development-machines.md` "Lint gates" section and two env-var rows |
| 10 | LOW | Weak assertions: a Ruff crash passed the syntax test; nothing proved the rc file disabled only SC2206 | Tooling | The tests checked "nonzero" and one code | Exact return code and rule code; the planted-script test lists every expected (line, code) |
| 11 | LOW | Vacuity floors: none on hook paths; skills and agents floored at today's exact count | Data flow, Tooling | | At least 20 hook paths; at least 1 skill and 1 agent |
| 12 | LOW | Hook commands with `;`, `&&`, `|` or a quote after the path read the separator as part of the file name | Codex P3 | Today's commands have none | Path parsing stops at separators; a test per separator |
| 13 | LOW | Tool output decoded as cp1252; `python3` shebangs; an unused loop variable kept with a suppression; a hook split broken out of the rule's one-line shape | Tooling, Standards | | UTF-8 decoding; `python`; `for _ in`; a brace group |
| 14 | LOW | Rule text named only `#if DEBUG` while the gate follows ADR-005's full ban, and nothing named the gate | Data flow | | AGENTS.md row widened and names `BannedConstructsGateTests`, within the context budget |
| 15 | LOW | Ruff's target was 3.11 with no reason | Tooling, Design | | 3.14, Mike's choice (keep tools current); the comment notes Blender 5.2 and the Ghidra venv run 3.13 |
| 16 | LOW | Review routing sent `ruff.toml` to a lane with no checks; the Completeness lens's own grep could not see the new suppression forms or untracked files | Completeness | | `ruff.toml` in the `tooling` lane (`.gitleaksignore` stays out: no check reads it); the lens grep extended, with an unanchored form for untracked files |

**Not applied:** `.gitleaksignore` and the gate configs under `config-protection.sh` (Mike declined: a
speculative guard); a version check in the ShellCheck lookup (no second version exists here);
"before a release" in `/security-scan`'s "When to use" has no caller in `/release` or `/ship`
(no issue: that line predates #1020; the new history step runs with every `/security-scan` run
and adds no trigger of its own).

## Convergence round

After the fixes, the Data flow lens and Codex at `high` re-read the change. Both closed every
first-round finding they checked, and both found the same new defect:

| # | Sev | Finding | Found by | Fix |
|---|---|---|---|---|
| C1 | MED | The Ruff gate's new "every tracked `.py` is linted" check read the index, so a tracked file deleted but not yet staged failed the gate: the shared root above, reintroduced by its own fix | Data flow, Codex | Listed files filtered to those on disk; a planted deleted-file test (RED, then GREEN) |
| C2 | LOW | Docs said Gitleaks is found through PATH; the command reads only `TAOM_GITLEAKS` or its default | Data flow, Codex | `development-machines.md` and the record describe each tool's real lookup |
| C3 | LOW | The lens grep's untracked-file pass reused a pattern anchored on diff `+` and `-`, so it matched nothing | Data flow | An unanchored pattern, shown to find this branch's untracked suppression text |
| C4 | LOW | `tests.md`'s new path loaded C#-only naming rules into Python test edits; a Python test not named `test_*` never runs | Data flow | One line scoping the C# sections and naming the `test_` prefix |
| C5 | LOW | `.gitleaksignore` was routed to the Python tests, which never read it; the record's count of fixed and suppressed ShellCheck findings was off by one | Data flow | Routing entry removed; the record corrected (four fixed, eight suppressed under five directives) |

C1 is the one that matters: the fix for finding 3 compared the index with the disk the same way
finding 2's first version did. The tests.md line from this RCA names "a deleted one" for exactly
this reason, and the convergence pass caught it before commit. The stop rule ends the review here:
these are MEDIUM and lower, proven by their tests.

## The shared root

Findings 1, 2, 3, 4, 6 and 11 are one mistake made six times. Each gate passed on its author's
worktree: a config written LF, every script already tracked, every Python file in a known folder,
a full restore, a run from the root, today's exact counts. None was run on the states the gate meets
later: a fresh checkout, a script not yet staged, a new folder, an unchanged checkout, a subfolder,
an empty input. A gate that is only proven on its author's state is advice with a green tick.

Finding 1 is also the third instance of one class: a file is LF only because a tool wrote it, and a
checkout under `core.autocrlf=true` turns it CRLF for a consumer that rejects CR (the hooks,
2026-08-31; the Howdah snapshot, 2026-09-19, #627). Both earlier lessons said to pin the file in
`.gitattributes`; this time no consumer was named, so the rule never fired. The fix chosen here
removes the file instead.

## Why each reviewer caught or missed what it did

- **Data flow** traced each gate's inputs to their sources, which is how it found 1 to 4 and 6 to 8.
- **Codex** probed the scanners with adversarial inputs (finding 5) that no Claude lens built.
- **Tooling** ran the tools in the failure states (non-repo folder, broken config) and found 6 and 10.
- **Completeness** found 8, 9 and 16 by reading the rules that govern adoption, not the code.
- **Standards and Design** found no new defect; Design found the simpler shape for finding 1.

## Prevention

- A lesson in [build-tooling-workflow.md](lessons/build-tooling-workflow.md): prove a gate on the
  states it will meet, with the list above, and prefer passing a tool's options on the command line
  over a tracked config file that a CR-intolerant tool reads.
- `.claude/rules/tests.md` now also loads for `tools/tests/**` and carries one line of that rule.
- `external-repo-adoption.md` step 5 now requires the provenance row.
