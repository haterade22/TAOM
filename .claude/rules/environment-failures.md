---
description: Environment failures are reported, not fixed. The agent works within infra; the user controls infra.
---

<!--
This rule has NO `paths:` field intentionally. Per Claude Code memory loader:
  - Rules WITHOUT `paths:` load at conversation start (always-on).
  - Rules WITH `paths:` (any glob, including `**/*`) load only when a matching
    file is opened — they are conditional, not unconditional.
This rule is meant to apply universally, so `paths:` is omitted.
-->


# Environment failures: report, don't fix

When something goes wrong that lives outside the project source — missing tools, broken paths, permissions, MCP server down, Bannerlord install drift, GitHub auth, network errors — **report and stop. Do not attempt to fix.**

## Why

Infra fixes are usually irreversible from the agent's view (installed wrong package, edited the wrong PATH entry, changed a global config). The user has context the agent does not — which Bannerlord install is current, which Steam library is preferred, which NuGet feed should host the package, why a particular MCP server is intentionally disabled.

Self-healing infra silently masks the real problem, then breaks differently next session.

## Examples — report, don't fix

| Symptom | What NOT to do | What TO do |
|---------|----------------|------------|
| `ilspycmd: command not found` | `dotnet tool install -g ilspycmd` | "ilspycmd is missing — needed for v1.4.5 signature verification. Install with `dotnet tool install -g ilspycmd`?" |
| `E:\Decompiled_Bannerlord\` empty | `git clone` something into it | "Decompiled folder is empty. Has the v1.4.5 dump moved? I need a path." |
| `dotnet build` fails: `MSBuild not found` | Edit `.csproj`, change SDK ref | "MSBuild path not picked up — likely a Visual Studio install issue. Investigate from your end." |
| Serena MCP returns no results | Restart MCP, reinstall | "Serena MCP is returning empty. The mcp-health hook may have marked it unhealthy. Check `.claude/logs/`." |
| `gh auth status` fails | `gh auth login` interactively | "gh CLI isn't authenticated. Run `gh auth login` when convenient." |
| Bannerlord DLL paths in `Directory.Build.props` don't resolve | Edit the props file | "Game install path may have moved. Current target: `E:\Steam\...`. Confirm or update?" |
| `BANNERLORD_GAME_DIR` env var unset | Set it for the session | "BANNERLORD_GAME_DIR isn't set. Export it from your shell config and try again." |

## Examples — these are NOT environment failures (fix normally)

- A C# build error in TAOM source — fix per `/build-fix`
- A test assertion fails — investigate per `/investigate`
- An XSLT transform produces wrong XML — fix the XSLT
- A Harmony patch doesn't take effect — debug per `/investigate`
- A skill or hook script throws — fix the script
- `git status` shows unexpected files — investigate (per `CLAUDE.md` guidance, never delete without checking)

The line: anything inside the TAOM repo's tracked files is in scope. Anything outside (tools on PATH, env vars, Steam install, MCP servers, OS config) is the user's domain.

## Tone when reporting

State the facts:
- What you tried
- What failed (exact error)
- What you suspect (one line — not a long diagnosis)
- The minimal next step the user can take

Don't suggest the user "fix their machine" or imply incompetence. Most env failures are just drift — paths move, tools update, auth tokens expire.
