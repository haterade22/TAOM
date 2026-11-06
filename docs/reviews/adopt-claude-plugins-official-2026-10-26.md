# Claude plugins marketplace review (2026-10-26)

Review of [anthropics/claude-plugins-official](https://github.com/anthropics/claude-plugins-official)
(Apache-2.0, Anthropic's managed Claude Code plugin directory) against TAOM's harness, through
`/adopt-external`. Mike asked what TAOM can incorporate, with the same process as the
[pstack review](adopt-pstack-2026-10-24.md): each change wired into a step that already runs.

## Coverage

- **Repository:** `5826794` (2026-10-23), 591 files, about 45 plugins, read from a shallow clone in
  the session scratchpad. Nothing was installed or run. Three read-only readers covered the review
  and security plugins (`code-review`, `pr-review-toolkit`, `code-simplifier`, `feature-dev`,
  `commit-commands`, `claude-security`, `security-guidance`); the harness plugins (`hookify`,
  `claude-md-management`, `claude-code-setup`, `skill-creator`, `plugin-dev`, `session-report`,
  `ralph-loop`, `receipts`, `project-artifact`, the two output styles); and the development
  plugins (`csharp-lsp`, `code-modernization`, `mcp-server-dev`, the external MCP entries) plus the
  marketplace's own CI.
- **Skipped by name:** LSPs for other languages, the math plugins, the chat bridges (Discord,
  Telegram, iMessage, fakechat), and the Firebase, Laravel, Terraform, GitLab, Asana and Linear
  connectors.
- **Starting point:** every plugin TAOM ever enabled from this marketplace is off in
  `.claude/settings.json` (seven entries, `false`).

## Security

- **Automated vet:** `python tools/audit_claude_config.py --root <clone> --external`: 591 files,
  3 CRITICAL, 65 HIGH, 108 MED, exit 1. Each load-bearing hit was read by hand: the CRITICALs are a
  masked example (`AKIA****`) and two test fixtures; `security-guidance/hooks/llm.py:1375` is a
  detection rule that tells the reviewer to flag permission bypasses; the Discord README's
  download-to-shell line is a user install instruction; the Telegram zero-width hit is emoji. The
  HIGH bulk is plugin-authoring docs describing wildcard tool grants and the security plugin's own
  tests.
- **Safe to learn from:** yes.
- **Safe to install:** no plugin is recommended.

| Plugin | Why not install |
|---|---|
| `security-guidance` | Sends diffs and files to Opus from a hook on every Stop and edit; builds a venv with an unpinned `pip install claude-agent-sdk` at session start; `ANTHROPIC_BASE_URL` redirects it |
| `hookify` | Runs `python3` four times per tool call; on this machine that is the Store alias, which hangs |
| `commit-commands` | `clean_gone` runs `git branch -D` and `git worktree remove --force` in a loop; the commit commands bypass the subject-version rule |
| `claude-security` | Web-vulnerability focus, many agents at xhigh per scan, minified 60 KB workflow, writes report folders into the repo |
| `skill-creator`, `session-report`, `ralph-loop` | Scripts write into `.claude/commands/` or read every transcript; ideas ported instead |
| `csharp-lsp` | Duplicates Serena, which already runs a C# language server (`.serena/project.yml`) |
| `code-modernization` | Telemetry on by default; ideas only |

## Adopted, and what fires each one

| # | Item (source) | TAOM change | Trigger |
|---|---|---|---|
| A1 | Unknown frontmatter keys and name mismatches (`skill-creator/.../quick_validate.py`) | `tools/test_hooks.sh` section 3b fails on a key outside the documented skill or agent fields, and on a `name` that differs from its folder or file | The hook suite, which `/improve` baselines and every hook change runs |
| A2 | "Distrust safety claims" and "check for missing controls" (`security-guidance/hooks/llm.py` 1420 to 1424) | `deep-review/lenses/5-data-flow.md` | `/deep-review`, required before C# and XML commits |
| A3 | Dangerous-sink rules (`security-guidance/hooks/patterns.py`, `llm.py`) | `lenses/tooling.md` (Python and bash), `lenses/1-standards.md` (C#: type-carrying deserializers, `XmlResolver`, `Process.Start`) | `/deep-review` |
| A4 | Canary: a deliberate failure must turn the run red (`code-modernization/commands/modernize-verify.md`) | `TAOM.Tests/Infrastructure/VerifyCanaryTests.cs`, failing only when `TAOM_VERIFY_CANARY=1`; `/verify` Step 2c and a test-count floor | `/verify`, before each claim of done |
| A5 | Token and cache accounting (`session-report/.../analyze-sessions.mjs`) | `tools/harness_retro.py` section 10 | `/harness-retro`, on its seven-day banner |

## Rejected after a check

- **MCP tool annotations** (`mcp-server-dev/.../tool-design.md`): Claude Code documents no use of
  `readOnlyHint` or the other hints (https://code.claude.com/docs/en/mcp, read 2026-10-26); the one
  changelog entry (2.1.307) covers retries for HTTP servers, and `taom-moduledata` is a stdio
  server. The MCP spec tells clients to treat hints as untrusted. No proven benefit, so the
  simplicity criterion rejects it.
- **A frontmatter parse check** (marketplace `validate-frontmatter.ts`): `tools/test_hooks.sh`
  section 3b already parses every skill, agent and rule frontmatter.
- **A pre-report verifier and a "predates the change" label** (`claude-security/agents/scan-verifier.md`):
  `/deep-review` already re-reads each finding before it counts and before a fix, and its
  FOLLOW-UP label is the same as "predates the change".

## Review of the port

A Tooling-lens review checked section 10 against the real transcripts and Claude Code's own
`cost-state` records. Cache read and creation matched to the token; the dedupe is correct. Fixed
before commit:

- **Output is a floor.** Claude Code 2.1.301 often writes a subagent request only as mid-stream
  snapshots, so no record carries the final output count (about 39% of subagent requests in the
  window). Section 10 now counts those requests and says the output figure is a floor.
- **Typed-while-busy turns.** A prompt typed while the model works is recorded only as a
  `queued_command` attachment; it now ends skill attribution like any human message.
- **A redundant uuid dedupe layer was deleted;** the requestId dedupe covers it.
- **The canary's failure message** names its cause, so a stray exported variable is not "fixed"
  by disabling the test.

Follow-ups, done 2026-10-26 on branch `taom-followups`: sections 5 and 6 of `harness_retro.py`
now treat a typed-while-busy prompt as a human turn through the same helper as section 10; the
token tables show output and cache-write columns, because cache reads are about 98% of the total;
section 3b runs a self-test on a planted tree, and decides a file's kind from its path under
`.claude`.

## Offers, decided 2026-10-26

Mike asked for the recommendation on each offer to be carried out. None was built.

| Offer | Decision | Reason |
|---|---|---|
| A controlled with/without run of a harness change (`skill-creator`) | Rejected for now | The [harness2 review](adopt-harness2-2026-10-21.md) deferred the same idea (D1) because it spends real usage; revisit when retro verdicts stay confounded |
| Held-out testing of a reworded skill description | Rejected | No skill has been found not firing; a procedure for a case that has not happened is YAGNI |
| A confidence score to rank review findings | Rejected | Findings are already ranked by severity, with UNVERIFIED kept below HIGH; the gain is small |
| Session isolation for Stop-hook streak markers (`ralph-loop/hooks/stop-hook.sh`) | Rejected | A guard for a collision nobody has seen (`simplicity-criterion.md`) |

## Declined

| Item | Reason |
|---|---|
| `code-review`, `pr-review-toolkit` | `/deep-review` and its lenses cover them; TAOM has no PR flow |
| `code-simplifier` | Disabled 2026-08-05; `refactoring-specialist` is the route; JS-specific standards |
| `feature-dev` | `/new-feature`, `architect`, `feature-builder` |
| `claude-md-management` | `lint_docs.py` context budget is stricter and measured |
| `claude-code-setup` | JS, Python and Go signals only |
| `plugin-dev` validators | `tools/test_hooks.sh` checks hooks more deeply; its scripts exit on the first finding under `set -e` |
| `receipts`, `project-artifact`, output styles | Out of domain or a context cost every session |
| `agent-sdk-dev`, marketplace CI | Out of domain |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/external-repo-adoption.md](../ai-includes/external-repo-adoption.md)

<!-- backlinks-end -->
