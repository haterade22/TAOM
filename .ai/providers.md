# Provider setup

Keep project policy in shared files and provider credentials/settings outside
that policy. No API key, paid invocation, plugin, MCP server or particular model
is required by `reviewctl`. Python 3.10+ and Git are its only runtime requirements.
Use the model ID actually reported by your client. This setup does not prescribe
or claim availability of a model named Kimi3 or any other changing product name.

## Native entry points

| Client | How it receives the shared contract |
| --- | --- |
| Codex | Root `AGENTS.md` selects shared policy and the assigned role. `.agents/skills/` supplies five native workflow skills. Read the [Codex operating guide](../docs/ai-includes/codex-operating-guide.md); existing `.codex/config.toml` model, permission and MCP settings remain unchanged. |
| Claude Code | `CLAUDE.md` imports `@AGENTS.md`, with existing Claude workflows preserved below it. This is the documented [shared AGENTS.md bridge](https://code.claude.com/docs/en/memory#agentsmd). Review packets explicitly select a no-fix reviewer role. |
| Kimi Code | Read project `AGENTS.md`; the official [instruction-files documentation](https://www.kimi.com/code/docs/en/kimi-code-cli/customization/agents#instruction-files) lists project instruction files. Verify the installed client's loading behavior. Model and client names are recorded separately. |
| Any other CLI, API agent or chat UI | Explicitly provide the bootstrap prompt below and the same packet/files. Native auto-discovery is a convenience, not a prerequisite. |

These entry points were checked against official documentation on 2026-09-11.
Tool flags, loaders, model availability and configuration can change. Check
installed help and current official docs before automating a provider. Do not
copy another provider's hook/skill frontmatter or assume equivalent semantics.

An explicit user request to conduct paid dispatch is authorization within the
stated scope and budget. Do not ask for the same permission again, or infer it
from a request to build or update documentation. See [dispatch policy](policy.md#paid-dispatch-authority).

## Universal builder prompt

```text
Work in the TAOM checkout. Read AGENTS.md, .ai/policy.md,
.ai/roles/builder.md and .ai/scopes.md before acting. You are the builder for
the task below, regardless of your model provider. Follow applicable TAOM rules
and preserve unrelated changes. Implement only the user-authorized scope.
Task and acceptance criteria: [insert the actual request].
```

For a reviewer, supply the generated packet's `prompt.md`, `manifest.json`,
`review.template.json` and access to the source at that exact SHA. Do not send
other reviewers' reports during the first pass. Ask the client to confirm the
loaded role and read-only boundary before starting. The prompt is the same for
every provider; record actual model/provider/session in each returned report.

If the agent has no repository tools, provide the selected files, unchanged
consumers, relevant references and required external excerpts manually. Mark
anything you cannot provide UNVERIFIED. Uploading a diff alone is not equivalent
to access to the repository or installed engine. Redact secrets and respect
source/asset licensing and the team's data-sharing policy.

## Optional local tools and isolation

The portable baseline is file reads, Git, command execution and JSON reports.
MCP can expose filesystem, Git, ILSpy and ModuleData queries, but each client has
its own configuration and permission model. Existing `.mcp.json` and
`.codex/config.toml` are not a portable shared schema. Grant only necessary
read roots for the game dump and external modules; keep tokens local. The
ModuleData server at `tools/taom_mcp_server.py` is optional, not a review gate.

Before broad automation, manually prove one builder-to-two-reviewer cycle with
actual accounts. Then add optional provider adapters that accept a prompt path
and emit a report path, recording client/model versions, exit status, run ID,
cost, timeout and raw-output location. Such adapters and authenticated CI
ingestion are future work, not present features. Do not let a wrapper convert
an empty response, error, timeout or parser failure into a CLEAN result.
