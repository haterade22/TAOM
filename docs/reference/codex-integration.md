# Claude-to-Codex integration

For Codex onboarding, building, research and review, use the
[Codex operating guide](../ai-includes/codex-operating-guide.md). The
[shared workflow](../../.ai/README.md) applies to all providers: Codex can be the
builder, and Claude or Kimi can be reviewers. This page documents the existing
Claude dispatch adapters, not a mandatory role for Codex.

## Existing Claude adapters

| Claude skill | Purpose and source |
| --- | --- |
| `/codex-verify [feature]` | Verification through `codex exec`; [skill contract](../../.claude/skills/codex-verify/SKILL.md) |
| `/review-codex [feature]` | Adversarial review and follow-up; [skill contract](../../.claude/skills/review-codex/SKILL.md) |
| `/deep-review [feature] --codex` | Optional Codex pre-review before Claude reviewers; [skill contract](../../.claude/skills/deep-review/SKILL.md) |

These skills contain Claude-specific Bash/background invocation, authentication
checks and follow-up instructions. Their tool names, notification behavior and
slash commands are not available automatically in Codex or other clients. Read
the actual skill before using it; a Markdown table does not establish runtime
availability. Plugin commands require a separately available plugin.

The Claude skills run `codex exec -c project_doc_max_bytes=65536 - < prompt > out` in the
background. A `/codex-verify` pass typically takes 5 to 20 minutes and `/review-codex` 10 to 45.
Pre-flight with the client's own status command (`codex login status`).

## Authority and isolation

Run paid dispatch when the user explicitly requests it, within the agreed scope
and budget. Do not repeat an already satisfied approval question. A legacy
completion checklist alone does not authorize paid calls, parallel agents,
fixes, issues, commits or publication. See [shared policy](../../.ai/policy.md).

A fresh session provides conversational separation, not source isolation. Use
separate clean source checkouts and read-only reviewer access. Confirm the actual
client and model provider; two CLI brands can use the same provider's models.
Record evidence and review fixes at a new SHA under the shared packet workflow.
Its blind first pass excludes known suspects and builder explanations from the
legacy prompt style. Do not present a legacy prose review as a validated packet.

## Local preflight

On PowerShell, use `Get-Command codex`, `codex --version`, `codex --help` and
`codex exec --help` to select the installed executable and supported options.
Do not assume the older npm shim is the active binary. Check authentication
using the installed client's documented status command before an authorized
dispatch; never log in, expose tokens, or rewrite configuration on the user's
behalf without authority.

The project [Codex README](../../.codex/README.md) describes the existing settings
and their limits. [Verification](../../.ai/verification.md) supplies non-deploying
checks. Paid-provider wrappers, authenticated report ingestion and remote merge
enforcement are not installed by the shared documentation layer.

