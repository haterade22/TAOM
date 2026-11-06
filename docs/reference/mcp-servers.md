# MCP Servers

> Extracted from CLAUDE.md 2026-08-05 (eager-context diet round 2). CLAUDE.md keeps the MCP
> Usage Guide (which tool for which task) + a 4-line taom-src lookup-order summary; this file
> holds the full server table, the research lookup-order detail, and configuration.

## Server table

| Server | Scope | Purpose | Config |
|--------|-------|---------|--------|
| **Serena** | Project | Symbolic code navigation (C# classes, methods, references) | `.mcp.json` |
| **ilspy** | Project | Decompile TaleWorlds DLLs — fallback when `E:\Decompiled_Bannerlord\` doesn't have what you need | `.mcp.json` |
| **taom-moduledata** | Project | Query TAOM ModuleData integrity (validate, item/troop/culture exists, find-references, list cultures/schemas) — wraps `tools/taom_query.py`. Needs the `mcp` SDK; restart Claude to load. See `docs/features/moduledata-validation.md`. | `.mcp.json` |
| **imagine** | Project | AI image generation (`https://mcp.imagine.art`, HTTP — needs auth; unauthenticated sessions can't use it) | `.mcp.json` |
| **sequential-thinking** | User | Extended reasoning for complex design decisions | `~/.claude/.mcp/user.json` |
| **context7** | User | Library documentation lookup | `~/.claude/.mcp/user.json` |

**Removed by WP116 (from 1.5.x `fdf9f38cb`): `github`, `git`, `filesystem`.** No TAOM skill, agent or rule called them; each duplicated a tool with gates of its own (`gh`, git through Bash, Read/Glob/Grep). `github@claude-plugins-official` declares the same `github` server, so it is disabled in `.claude/settings.json` too. Record: [adopt-ecc-2026-09-29.md](../reviews/adopt-ecc-2026-09-29.md), Step 4.

## Denied write tools (2026-08-31)

Nine MCP write tools are listed under `permissions.deny` in `.claude/settings.local.json`. They name servers that are now gone, so the entries are inert and the maintainer may delete them:

`mcp__git__git_add` · `git_commit` · `git_reset` · `git_checkout` · `git_create_branch` ·
`mcp__filesystem__write_file` · `edit_file` · `move_file` · `create_directory`

Sixteen Serena editing tools are denied in `.claude/settings.json` (`permissions.deny`, maintainer decision 2026-11-04): `mcp__serena__` followed by `create_text_file`, `replace_content`, `replace_in_files`, `delete_lines`, `replace_lines`, `insert_at_line`, `replace_symbol_body`, `insert_after_symbol`, `insert_before_symbol`, `rename_symbol`, `safe_delete_symbol`, `execute_shell_command`, `jet_brains_move`, `jet_brains_safe_delete`, `jet_brains_rename`, `jet_brains_inline_symbol`. Serena's read tools and its four memory tools stay allowed. The list is the can-edit set the 1.5.x line derived for Serena 7a296833 (7d13adc36); this line runs Serena unpinned, so re-derive it when Serena adds an editing tool.

**Why:** every git safety hook in this repo is registered against `matcher: "Bash|PowerShell"`, and
`config-protection.sh` against `matcher: "Edit|Write"`. Nothing matches `mcp__*`. So the MCP
write tools went straight past the force-push block, the CHANGELOG-staged gate, the
`.claude/`-tracked-files gate, the ModuleData ref gate and the settings/ADR protection, all
at once and silently. That was not a theoretical hole: CLAUDE.md's own MCP Usage Guide routed
git work to those tools.

Stage and commit through
Bash, and write files through Edit/Write, which is where the gates live.

Note also that `mcp__ilspy__decompile_type` and `mcp__git__git_blame` **do not exist** and never
did; they were documented across nine sites until 2026-08-31. The real names are
`decompile_assembly(assembly_path, type_name=...)` and, for blame, `git blame` via Bash.

## TaleWorlds Research — Lookup Order

**Always use `taom-src` first.** It runs `ilspycmd` against the installed DLLs (version auto-detected from `Version.xml`) and caches under `~/.taom-src/<version>/`. The `E:\Decompiled_Bannerlord\` dump matches the pin and is fine for browsing namespaces/patterns; for authoritative signatures prefer `taom-src` against the installed DLLs (the dump can lag after an engine bump).

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does X work" questions (lifecycle, formation, mount/rider, campaign-mission seam, heartbeat, spawn pipeline). Saves raw decompile time when the process is already documented. |
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | One command — decompiles the installed (v1.4.7) DLL on cache miss, returns absolute path | **For signature verification** (Harmony patch, GameModel override, adapter, API call) — authoritative; run after you understand the process conceptually |
| 2. **Browse `E:\Decompiled_Bannerlord\`** | `Read` / `Grep` / `find` against the dump | Finding which DLL a class lives in, exploring a namespace tree |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_assembly` / `mcp__ilspy__list_types` | Fallback if `taom-src` fails (e.g., need a full DLL type listing) |

See `.claude/skills/taom-src/SKILL.md` for full usage. Composes with standard tools:
```bash
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
```

**Decompiled source layout:** `E:\Decompiled_Bannerlord\` category tree = the SHIPPING-CLIENT decompile (STRIPS editor-only code — "absent from the dump" != "doesn't exist"; editor-only types live in the `{_shipping_build,_editor_build}` dual-build). Folder map, builds, native-DLL inspection: [bannerlord-engine-and-toolchain.md](bannerlord-engine-and-toolchain.md).

**DLL path** (for ILSpy MCP fallback): `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\` (shipping). **Editor build = `…\bin\Win64_Shipping_wEditor\`** — same-named DLLs with editor-only types compiled in.

## Configuration

Project-level MCP servers (Serena, ilspy, taom-moduledata, imagine, elevenlabs) are configured in `.mcp.json` at the project root and must be listed in `.claude/settings.local.json → enabledMcpjsonServers` to be trusted. (`taom-moduledata` is TAOM-authored — `tools/taom_mcp_server.py` — and requires the `mcp` Python SDK; a Claude restart is needed to pick up a newly-added server.) User-scope servers live in the top-level `mcpServers` of `~/.claude.json` (on 2026-10-08 only `davinci-resolve`). The older `~/.claude/.mcp/user.json` (sequential-thinking, context7) is not a file Claude Code reads: on 2026-10-08 neither server loaded, and neither appeared among the failed connections.

**Project servers are keyed by the folder's spelling.** `~/.claude.json` keeps a project's local
servers under the path as it was opened, case included: on 2026-10-16 `blender` and
`substance-painter` sat under `E:/repos/TAOM`, and a session opened as `e:\repos\TAOM` read the
lowercase key, which held none, so neither loaded (measured on the desktop). Open a checkout with the
same spelling every time.

## Plugin overlap (routing disambiguation)

Enabled plugins add their own skills alongside TAOM's and the MCP servers. Where they overlap, TAOM routing wins:

| Job | TAOM route | Overlapping plugin/server |
|-----|-----------|---------------------------|
| Pre-commit C# review | `/deep-review` (+ `/review-codex`) | `code-review` plugin (`/code-review` — kept for `/code-review ultra` cloud review) |
| GitHub issues/PRs | `gh` CLI | `github` plugin (disabled) |
| Redundant-code deletion | `/deslop` | `code-simplifier` plugin (`/simplify`) — disabled 2026-08-05 |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)

<!-- backlinks-end -->
