# MCP Servers

> Extracted from CLAUDE.md 2026-08-05 (eager-context diet round 2). CLAUDE.md keeps the MCP
> Usage Guide (which tool for which task) + a 4-line taom-src lookup-order summary; this file
> holds the full server table, the research lookup-order detail, and configuration.

## Server table

| Server | Scope | Purpose | Config |
|--------|-------|---------|--------|
| **Serena** | Project | Symbolic code navigation (C# classes, methods, references) | `.mcp.json` |
| **GitHub** | Project | PRs, issues, actions, code search (HTTP — needs auth; falls back to `gh` CLI when unauthenticated) | `.mcp.json` |
| **filesystem** | Project | File operations across TAOM, Bannerlord Modules, LOTRAOM assets | `.mcp.json` |
| **git** | Project | Rich git operations (diff, blame, log, branch management) | `.mcp.json` |
| **ilspy** | Project | Decompile TaleWorlds DLLs — fallback when `E:\Decompiled_Bannerlord\` doesn't have what you need | `.mcp.json` |
| **taom-moduledata** | Project | Query TAOM ModuleData integrity (validate, item/troop/culture exists, find-references, list cultures/schemas) — wraps `tools/taom_query.py`. Needs the `mcp` SDK; restart Claude to load. See `docs/features/moduledata-validation.md`. | `.mcp.json` |
| **imagine** | Project | AI image generation (`https://mcp.imagine.art`, HTTP — needs auth; unauthenticated sessions can't use it) | `.mcp.json` |
| **sequential-thinking** | User | Extended reasoning for complex design decisions | `~/.claude/.mcp/user.json` |
| **context7** | User | Library documentation lookup | `~/.claude/.mcp/user.json` |

## TaleWorlds Research — Lookup Order

**Always use `taom-src` first.** It runs `ilspycmd` against the installed DLLs (version auto-detected from `Version.xml`) and caches under `~/.taom-src/<version>/`. The `E:\Decompiled_Bannerlord\` dump matches the pin and is fine for browsing namespaces/patterns; for authoritative signatures prefer `taom-src` against the installed DLLs (the dump can lag after an engine bump).

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does X work" questions (lifecycle, formation, mount/rider, campaign-mission seam, heartbeat, spawn pipeline). Saves raw decompile time when the process is already documented. |
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | One command — decompiles the installed (v1.4.7) DLL on cache miss, returns absolute path | **For signature verification** (Harmony patch, GameModel override, adapter, API call) — authoritative; run after you understand the process conceptually |
| 2. **Browse `E:\Decompiled_Bannerlord\`** | `Read` / `Grep` / `find` against the dump | Finding which DLL a class lives in, exploring a namespace tree |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_type` / `mcp__ilspy__list_types` | Fallback if `taom-src` fails (e.g., need a full DLL type listing) |

See `.claude/skills/taom-src/SKILL.md` for full usage. Composes with standard tools:
```bash
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
```

**Decompiled source layout:** `E:\Decompiled_Bannerlord\` category tree = the SHIPPING-CLIENT decompile (STRIPS editor-only code — "absent from the dump" != "doesn't exist"; editor-only types live in the `{_shipping_build,_editor_build}` dual-build). Folder map, builds, native-DLL inspection: [bannerlord-engine-and-toolchain.md](bannerlord-engine-and-toolchain.md).

**DLL path** (for ILSpy MCP fallback): `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\` (shipping). **Editor build = `…\bin\Win64_Shipping_wEditor\`** — same-named DLLs with editor-only types compiled in.

## Configuration

Project-level MCP servers (Serena, GitHub, filesystem, git, ilspy, taom-moduledata, imagine) are configured in `.mcp.json` at the project root and must be listed in `.claude/settings.local.json → enabledMcpjsonServers` to be trusted. (`taom-moduledata` is TAOM-authored — `tools/taom_mcp_server.py` — and requires the `mcp` Python SDK; a Claude restart is needed to pick up a newly-added server.) User-level servers (sequential-thinking, context7) are configured in `~/.claude/.mcp/user.json` and enabled globally.

## Plugin overlap (routing disambiguation)

Enabled plugins add their own skills alongside TAOM's and the MCP servers. Where they overlap, TAOM routing wins:

| Job | TAOM route | Overlapping plugin/server |
|-----|-----------|---------------------------|
| Pre-commit C# review | `/deep-review` (+ `/review-codex`) | `code-review` plugin (`/code-review` — kept for `/code-review ultra` cloud review) |
| GitHub issues/PRs | `gh` CLI (per CLAUDE.md MCP Usage Guide: GitHub MCP when authenticated) | `github` plugin, `github` MCP server |
| Redundant-code deletion | `/deslop` | `code-simplifier` plugin (`/simplify`) — disabled 2026-08-05 |
