---
name: tooling-review
description: Review last 2 weeks of Claude Code and VS Code updates, cross-reference against project config to find improvements
---

# Tooling Update Review

Review recent Claude Code and VS Code releases, then cross-reference against the TAOM project's tooling configuration to surface actionable improvements.

## Step 1: Fetch Claude Code Updates

Gather Claude Code release notes from the last 14 days.

```bash
# List recent releases
gh release list --repo anthropics/claude-code --limit 10
```

- For each release tagged within the last 14 days, fetch details:
  ```bash
  gh release view <TAG> --repo anthropics/claude-code
  ```
- If `gh` returns no results or the repo is private, fall back to:
  1. `WebSearch` for "Claude Code release notes changelog site:docs.anthropic.com OR site:github.com/anthropics"
  2. `WebFetch` the top results
- Extract and note: new features, new hook events, new settings, new tools/permissions, deprecations, breaking changes.

## Step 2: Fetch VS Code Updates

Gather VS Code release notes from the last 14 days.

- Fetch the VS Code updates page:
  ```
  WebFetch: https://code.visualstudio.com/updates
  ```
- Also check GitHub releases for exact dates:
  ```bash
  gh release list --repo microsoft/vscode --limit 5
  ```
- For any release within 14 days, fetch its details via `gh release view` or the corresponding `https://code.visualstudio.com/updates/v<major>_<minor>` page.
- Extract and note: new editor features, new extension APIs, new settings, terminal changes, debugging improvements, MCP-related changes.

## Step 3: Snapshot Current Project Config

Read these files to understand what TAOM currently uses:

1. `.claude/settings.json` — Claude Code settings, permissions, plugins
2. `.claude/settings.local.json` — local overrides, MCP servers
3. `.vscode/settings.json` — VS Code editor settings
4. `.vscode/extensions.json` — recommended extensions
5. `.vscode/mcp.json` — MCP server configs
6. `CLAUDE.md` — hooks table, skills table, conventions
7. List `.claude/hooks/` — all hook scripts and their triggers
8. List `.claude/skills/` — all skill directories

Compile a concise summary of the current tooling state (settings in use, hooks registered, MCP servers configured, extensions recommended).

## Step 4: Cross-Reference Analysis

Launch **3 parallel agents** (Sonnet) with the update summaries from Steps 1-2 and the project snapshot from Step 3.

### Agent 1 — Claude Code Opportunities

Prompt the agent with the Claude Code release notes and the current `.claude/` configuration snapshot. Ask it to:

- Identify new hook events we're not using that could improve our workflow
- Find new settings or permission modes that apply to our setup
- Check if any existing hooks or skills could be simplified by new built-in features
- Flag deprecated features we still reference in `CLAUDE.md`, settings, or hook scripts
- Note new plugin capabilities relevant to our stack (C#, .NET, Harmony modding)

### Agent 2 — VS Code Opportunities

Prompt the agent with the VS Code release notes and the current `.vscode/` configuration snapshot. Ask it to:

- Identify new editor settings beneficial for C# / .NET Framework development
- Check for new debugging capabilities or terminal improvements
- Look for updates to our recommended extensions (C# Dev Kit, XML, PowerShell)
- Flag deprecated settings in our `.vscode/settings.json`
- Note MCP-related changes in VS Code that affect our server configuration

### Agent 3 — Integration Opportunities

Prompt the agent with both sets of release notes plus the full tooling snapshot. Ask it to:

- Find synergies between Claude Code and VS Code updates (e.g., new MCP features that both support)
- Identify workflow improvements that combine new features from both tools
- Check if `CLAUDE.md` documentation needs updates based on any changes
- Look for new capabilities that could replace or improve existing custom scripts or workarounds

## Step 5: Report

Compile agent findings into a single structured report:

### Report Format

```
## Claude Code Updates (last 14 days)
- [version]: [one-line summary of key changes]

## VS Code Updates (last 14 days)
- [version]: [one-line summary of key changes]

## Actionable Improvements

### HIGH — Direct workflow improvements
- [What to change] — [Which file(s)] — [Why it helps]

### MEDIUM — Worth exploring
- [What to change] — [Which file(s)] — [Why it helps]

### LOW — Minor tweaks
- [What to change] — [Which file(s)] — [Why it helps]

## Deprecation Warnings
- [Feature] — [Timeline] — [What to migrate to]

## No Action Needed
- [Feature reviewed but not applicable] — [Why]
```

## Important

- This is a **READ-ONLY** analysis. Do not modify any files. Only report findings.
- Focus on **project-specific** recommendations, not generic best practices.
- Skip features that clearly don't apply (e.g., Python-specific, web frontend, mobile).
- If no updates were released in the last 14 days for either tool, say so and skip that section.
