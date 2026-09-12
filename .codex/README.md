# Codex in TAOM

Start with the [Codex operating guide](../docs/ai-includes/codex-operating-guide.md)
and root [AGENTS.md](../AGENTS.md). Codex can build, review or adjudicate according
to the user's task; the client does not determine the role.

`config.toml` contains this client's existing model, sandbox and MCP settings.
This documentation update does not alter them. Their presence is not proof that
the current client loaded them or that a server works. Check the selected
executable, project trust, effective configuration and available tools before
relying on them. Do not broaden permissions to make a check pass.

Repository workflow skills live in [../.agents/skills/](../.agents/skills/), not
in this directory. They route to shared `.ai/` policy and existing TAOM references.
No named Codex subagent configurations, automatic dispatcher or enforced merge
gate is installed here. Do not confuse a role document with a runtime profile.

Machine-specific overrides and credentials belong in the appropriate local
client configuration. Existing `E:` paths describe the desktop; see
[development machines](../docs/reference/development-machines.md). Keep raw
session data and authentication files out of Git. For a review, prefer isolated
source access and a separate report destination as described in the guide.
