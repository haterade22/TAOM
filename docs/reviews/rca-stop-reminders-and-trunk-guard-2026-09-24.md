# RCA: plan 011 review, Stop reminders and trunk guard (#654, 2026-09-25)

## Top-line

The deep review of plan 011 (`43e6780e` against `bec0389d`) ran five lenses (Standards, Data flow,
Tooling, Efficiency, Completeness) and Design, then a Codex adversarial pass (gpt-6-astra, ultra).
The review lead confirmed 15 distinct defects: 1 HIGH, 5 MED and 9 LOW. Twelve are fixed on the
branch, test first. One needs a `settings.json` change, which is the orchestrator's (F4). The
other two are rollout or design notes (F10, F14).

The HIGH is a repeat. `validate-push.sh` took the last word of a line as the push's refspec.
Any tail after the branch name therefore became the "target", and the force push went through:
`2>&1`, `| tail -5`, `&& echo done`, `; git status`, a second refspec, or a trailing comment.
The ADR-011 batch 1 RCA recorded the trailing-comment form on 2026-09-23 as a follow-up
(`docs/reviews/rca-adr011-batch1-2026-09-23.md`, "Follow-ups not taken"). Plan 011 then rewrote
that same function for D38 and re-registered it for PowerShell without closing the bypass. Its
tests, catalog row and CHANGELOG all claimed the trunks were guarded. Every lens except Efficiency
found it, and so did Codex. Only a tail-free force push is actually refused, which is also all
that live check B would have tested.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH | A trunk force push with any same-line tail (`2>&1`, `\| tail`, `&&`, `;`), a second refspec, `--all` with force or `--mirror` passed `validate-push.sh` (rc 0) | Logic error (parser reads a line, not a command) | The plan called `;` and `&` "harmless tokens" (plan 011:261-262), and the executor copied that premise. 7c only tested pushes that end their line. The 2026-09-23 follow-up was never turned into a test row | Each line split at `; & \|`; redirections skipped; every refspec judged; `--all` with force and `--mirror` refused. 11 new rc=2 rows and 6 rc=0 rows in 7c. Lesson in `lessons/build-tooling-workflow.md` |
| F2 | MED | Every Stop hook exited at the top when `stop_hook_active` was true, so a streak that Claude ended in the continuation (a build, a CHANGELOG entry, a tag) kept its marker and muted the next streak | Stale state / lifecycle | The plan prescribed the early return (plan 011:769-774) as the loop guard and kept the marker logic unchanged (:782-783). Nobody traced the marker through the continuation. 7a tested trigger, loop silence and mute, but never clear or re-arm | The guard moved to just before the block in all four hooks, so cleanup always runs. 7a runs trigger, mute, clear, re-arm and continuation-clear for every discovered Stop hook. Lesson in `lessons/state-lifecycle-save.md` |
| F3 | MED | `mark-verification-run.sh` read `\` as the escape character under PowerShell. ``Write-Output "x`"; dotnet test"`` marked a verification that never ran, and `Set-Location "E:\x\"; dotnet test` did not mark | Convention inconsistency (one shell's grammar applied to another) | The PowerShell registration made a Bash parser reachable. 7d replayed the Bash corpus under a PowerShell tool name, which proves only that `tool_name` is ignored | The split moved into the payload's Python, with the escape taken from `tool_name`. PowerShell-only and Bash-only 7d rows. Lesson in `lessons/build-tooling-workflow.md` |
| F4 | MED | A command that exits non-zero raises PostToolUseFailure, where the marker writer is not registered. A failed build or test never marks, despite the hook's "pass OR fail" contract | Missing vanilla gate (harness event semantics) | The contract was written against PostToolUse without checking the failure path. Verified in the 2.1.241 bundle: `m.isError&&!de` throws, and only `RZn` (PostToolUseFailure, whose payload has `tool_input`) runs | Settings change for the orchestrator: register the hook on PostToolUseFailure for `Bash\|PowerShell`. Hook comment and catalog row now state the gap |
| F5 | MED | The new bash split was quadratic (`${s:i:1}` copies the string): 5.4 s for 100 KB, past the 5 s registration, while its comment claimed "linear" | Other: unmeasured complexity claim | The executor measured 20 KB only and extrapolated | Python split. 7d times a 100 KB command under `timeout 5` (1.5 s now) |
| F6 | MED | 7a never proved a reminder clears, re-arms, or that the verification writer and reader agree on a path. The Stop steps discarded exit status and stderr on silent paths | Other: test oracle | Tests checked output shape only. The writer and reader were tested apart (7d, 7a) | `stop_expect` requires rc 0 and empty stderr on every step. The verification streak ends through the real writer called as PowerShell. A logged deep-reviewer run must clear its marker. The helper's CR, LF and TAB are covered |
| F7 | LOW | A command on a non-final line (a bare `./build.ps1`) never marked, because Python's `print` ends lines with CRLF on Windows | Platform default | `validate-push.sh` strips CR but blamed PowerShell for it; no test had a middle-line command | CR dropped in the Python split, output written as bytes. Middle-line rows in 7c and 7d. The comment is corrected |
| F8 | LOW | D41's stricter env-prefix strip stopped marking `env DOTNET_NOLOGO=1 dotnet test`, which marked at base | Logic error (regression) | The D41 fix was tested on assignments only | `env` is dropped with the assignments. 7d row |
| F9 | LOW | The D38 line loop refuses a commit whose message quotes a trunk force push | Logic error, kept by design | A gate cannot tell quoted or heredoc text from text that `bash -c` or `bash <<EOF` runs | Kept fail-safe. Pinned in 7c, and the catalog row names the `git commit -F <file>` workaround |
| F10 | LOW | Markers written by the silent hooks survive the upgrade and mute the first visible reminder of a running streak. The main tree holds a `.verification-reminded` from 11:51, newer than its `.verification-ran` (10:55) | Stale state / lifecycle (rollout) | The plan kept marker names and contents | Merge step for the orchestrator: delete `.claude/logs/.*-reminded` in the main tree once. It self-heals when each streak ends |
| F11 | LOW | `harness-facts.md` Visibility contradicted itself ("stdout ... goes to the debug log", then "Stop ... reaches Claude only through JSON on stdout") and denied the documented Stop `additionalContext`. `_stop_reminder.sh` and a `test_hooks.sh` comment repeated the error | Other: doc drift | Written from the plan's summary, not re-read against the plan's own quote of the docs | Row reworded ("Plain stdout"; `additionalContext` documented, unproven). Helper and section 4 comment corrected. ADR-011:52 is protected: Mike's call |
| F12 | LOW | Two headers still said "soft reminder, not a hard block" above a `decision: block`, and one said the reminder was "re-injected every turn" | Convention inconsistency | Only `check-deep-review.sh`'s header was rewritten | Both headers carry the deep-review wording |
| F13 | LOW | The CHANGELOG entry and commit did not cite #654. `finish-branch` said `validate-push.sh` "warns on master/main pushes", an invisible warning, and named only one trunk | Other: traceability | Sibling entries were not compared | `(#654)` added; skill line names both trunks and what the hook does |
| F14 | LOW | Three facts lived only in the plan: `additionalContext`, HEAD resolving against the main tree, and a Stop block replacing a headless run's final message | Other: knowledge routing | Plans are not a knowledge base (`plans/README.md:3`) | Harness-facts and the hooks catalog now carry them, with the headless one marked unverified |
| F15 | LOW | 7c ran every case twice although the hook never reads `tool_name` (about 4 s per run) | Other: test cost | Copied from 7d's shape | PowerShell pass trimmed to one case in 7c. 7d keeps both passes, because the hook now reads `tool_name` |

## Root-cause pattern

F1, F3, F7 and F9 share one cause: **a hook parsed a shell command with a model of the grammar
that nobody wrote down or tested.** `validate-push.sh` assumed one command per line.
`mark-verification-run.sh` assumed Bash escaping for PowerShell text. Both assumed LF line ends.
In each case the tests fed the hook the executor's own idea of a command. None fed it the shapes
Claude actually sends: a routine `2>&1 | tail`, a `Set-Location "...\";` prefix, a CRLF-joined
multi-line command. F2 and F10 are the second pattern, **a marker's lifecycle traced on the happy
path only**: nobody asked what the marker holds after a continuation or after an upgrade.

## Why each agent missed these

- **The executor** implemented the plan's premises verbatim (F1's "harmless tokens", F2's early
  return) and wrote tests from the plan's case list, which held neither a tail nor a continuation
  clear.
- **Standards (Agent 1)** found F1, F9, F3's fail-safe half, F11, F12 and F13. It did not trace the
  marker through a continuation (F2), because its checks are per-file conventions.
- **Data flow (Agent 5)** found F1, F4, F7, F9 and F11's contract copies. It traced the loop guard
  as CONNECTED (trace 1) because it checked silence, not what the silence skips.
- **Tooling** found F1, F5, F6's clear and re-arm gap, and F8. It read the continuation path as
  correct in its 16-step walk: that walk never cleared a condition inside a `stop_hook_active` Stop.
- **Efficiency (Agent 3)** found F5 and F15. A parser's correctness is outside its lens.
- **Completeness (Agent 4)** found F1, F6, F7's missing test and F14.
- **Design (Agent 6)** proposed the per-command split and every-refspec loop that fix F1, and
  checked F3's backslash case but judged it harmless (one extra reminder). It did not consider
  the reverse direction, a backtick-escaped quote that marks falsely.
- **Codex** was the only reviewer to find F2 and F3's false-mark direction. It missed F4, F5, F7
  and F8, which needed the CLI bundle, a timing run or a real PowerShell payload.

## Feedback memories to codify

None beyond the lessons below. The standing rule "Prove a gate live" (`hook-authoring.md`)
already covers F1's class. What failed is that the 2026-09-23 follow-up was left as prose instead of
a failing test row. The lesson below makes that step explicit.
