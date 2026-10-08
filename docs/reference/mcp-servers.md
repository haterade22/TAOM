# MCP Servers

> Extracted from CLAUDE.md 2026-08-05. CLAUDE.md keeps a short "MCP and shell" summary and AGENTS.md
> the one-line research order; this file holds the server table (which tool for which task), the
> research lookup-order detail, and configuration.

## Server table

| Server | Scope | Purpose | Config |
|--------|-------|---------|--------|
| **Serena** | Project | Symbolic code navigation (C# classes, methods, references) | `.mcp.json` |
| **ilspy** | Project | Decompile TaleWorlds DLLs — fallback when `E:\Decompiled_Bannerlord\` doesn't have what you need | `.mcp.json` |
| **taom-moduledata** | Project | Query TAOM ModuleData integrity (validate, item/troop/culture exists, find-references, list cultures/schemas) — wraps `tools/taom_query.py`. Needs the `mcp` SDK; restart Claude to load. See `docs/features/moduledata-validation.md`. | `.mcp.json` |
| **imagine** | Project | AI image generation for TAOM's 2D work (`https://mcp.imagine.art`, HTTP; needs auth, so unauthenticated sessions can't use it) | `.mcp.json` |
| **elevenlabs** | Project | Voice design and generation into `.voice-scratch/` ([kingdom-voices.md](../features/kingdom-voices.md)) | `.mcp.json` |
| **blender** | Local (`E:\repos\TAOM` only) | Live Blender session for creature animation ([workflow](../ai-includes/creature-animation-blender-mcp-workflow.md)) | `~/.claude.json` |
| **substance-painter** | Local (`E:\repos\TAOM` only) | Live Substance Painter session for texturing | `~/.claude.json` |
| **sequential-thinking** | User | Extended reasoning for complex design decisions | `~/.claude/.mcp/user.json` |
| **context7** | User | Library documentation lookup | `~/.claude/.mcp/user.json` |

**Removed 2026-09-29: `github`, `git`, `filesystem`.** None was called by any TAOM skill, agent or rule; each
duplicated a tool with gates of its own (`gh`, which is authenticated; git through Bash, where the git
hooks live; Read, Glob and Grep, with the Modules folder and `E:\LOTRAOMAssets` as allowed directories
in this machine's `settings.local.json`). The rule, from affaan-m/ECC's connector policy: a server earns
its slot only when a CLI cannot do the job ([adopt-ecc-2026-09-29.md](../reviews/adopt-ecc-2026-09-29.md),
Step 4). **A plugin can declare a server too:** `github@claude-plugins-official` ships only a `.mcp.json`
declaring the same `github` server, so it is disabled in `.claude/settings.json` as well; deleting the
`.mcp.json` entry alone left the server loaded. Codex keeps its own copies in `.codex/config.toml`.

**Local-scope servers are keyed by the project path.** `claude mcp add --scope local` stores a
server under the current folder's entry in `~/.claude.json`. When the repo moved from
`C:\Users\mikew\source\repos\TAOM` to `E:\repos\TAOM`, blender and substance-painter stayed behind
under the old key and no session loaded them, silently, until 2026-09-29. After a move, or in a new
worktree, check `/mcp` inside the session (a shell's `claude mcp list` resolves its own folder) and re-add.

## Denied write tools (2026-08-31)

Sixteen MCP write tools are listed under `permissions.deny` in the tracked `.claude/settings.json`, so every clone gets them. Until 2026-09-29 nine more denied the `git` and `filesystem` servers' write tools (`mcp__git__git_add` · `git_commit` · `git_reset` · `git_checkout` · `git_create_branch` · `mcp__filesystem__write_file` · `edit_file` · `move_file` · `create_directory`); those servers are gone, and the entries with them. If either is ever re-added, restore the nine first.

The sixteen are Serena tools (2026-09-25, decision 58), `mcp__serena__` followed by:
`create_text_file` · `replace_content` · `replace_in_files` · `delete_lines` · `replace_lines` ·
`insert_at_line` · `replace_symbol_body` · `insert_after_symbol` · `insert_before_symbol` ·
`rename_symbol` · `safe_delete_symbol` · `execute_shell_command` · `jet_brains_move` ·
`jet_brains_safe_delete` · `jet_brains_rename` · `jet_brains_inline_symbol`

The nine removed entries were exactly the tools the pinned `git` and `filesystem` versions annotated
`readOnlyHint: false`. The Serena entries are every tool the pinned Serena commit marks
can-edit (`ToolMarkerCanEdit` in `src/serena/tools/`) except its four memory tools, which Mike chose
to keep (`write_memory`, `edit_memory`, `rename_memory`, `delete_memory` write only Serena's
memory folders: the repo's `.serena/memories/` and, for a `global/` name,
`~/.serena/memories/global/`). The can-edit rule includes classes whose names lack the usual
`Tool` suffix (`SafeDeleteSymbol`, `JetBrainsInlineSymbol`), so derive the list from the class
markers, not from names ending in `Tool`. `execute_shell_command` is among them because it would run a shell command
past every Bash hook. The optional beta `serena_repl` is not marked can-edit and not enabled, so it
is not listed. On a Serena pin bump, re-derive the entries from the new version, so a newly added
write tool is denied too.

**Why:** every git safety hook in this repo is registered against `matcher: "Bash|PowerShell"` (plan 027), and
`config-protection.sh` against `matcher: "Edit|Write"`. Nothing matches `mcp__*`. So the MCP
write tools went straight past the force-push block, the CHANGELOG-staged gate, the
`.claude/`-tracked-files gate, the ModuleData ref gate and the settings/ADR protection, all
at once and silently. That was not a theoretical hole: CLAUDE.md's own MCP Usage Guide routed
git work to those tools.

Stage and commit through Bash, and write files through Edit/Write, which is where the gates live.

Note also that `mcp__ilspy__decompile_type` **does not exist** and never did; it was documented
across several sites until 2026-08-31. The real name is `decompile_assembly(assembly_path, type_name=...)`.

## TaleWorlds Research — Lookup Order

**Always use `taom-src` first.** It runs `ilspycmd` against the installed DLLs (version auto-detected from `Version.xml`) and caches under `~/.taom-src/<version>/`. The `E:\Decompiled_Bannerlord\` dump matches the pin and is fine for browsing namespaces/patterns; for authoritative signatures prefer `taom-src` against the installed DLLs (the dump can lag after an engine bump).

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does X work" questions (lifecycle, formation, mount/rider, campaign-mission seam, heartbeat, spawn pipeline). Saves raw decompile time when the process is already documented. |
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | One command — decompiles the installed (v1.4.7) DLL on cache miss, returns absolute path | **For signature verification** (Harmony patch, GameModel override, adapter, API call) — authoritative; run after you understand the process conceptually |
| 2. **Browse `E:\Decompiled_Bannerlord\`** | `Read` / `Grep` / `find` against the dump | Finding which DLL a class lives in, exploring a namespace tree |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_assembly` / `mcp__ilspy__list_types` | Fallback if `taom-src` fails (e.g., need a full DLL type listing) |
| 4. **`python tools/native_decompile.py`** | `--engine-method <name>`, `--string <text>` or `--rva <offset>`: native engine code as C, through headless Ghidra | When the managed trail ends at an `[EngineMethod]`, or the engine parses the data itself. [ghidra-native-decompile.md](../features/ghidra-native-decompile.md) |

Full usage is in the help block at the top of `tools/taom-src.ps1`. Composes with standard tools:
```bash
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
```

**Decompiled source layout:** `E:\Decompiled_Bannerlord\` category tree = the SHIPPING-CLIENT decompile (STRIPS editor-only code — "absent from the dump" != "doesn't exist"; editor-only types live in the `{_shipping_build,_editor_build}` dual-build). Folder map, builds, native-DLL inspection: [bannerlord-engine-and-toolchain.md](bannerlord-engine-and-toolchain.md).

**DLL path** (for ILSpy MCP fallback): `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\` (shipping). **Editor build = `…\bin\Win64_Shipping_wEditor\`** — same-named DLLs with editor-only types compiled in.

## Configuration

Project-level MCP servers (Serena, ilspy, taom-moduledata, imagine, elevenlabs) are configured in `.mcp.json` at the project root and each developer trusts them in their own `.claude/settings.local.json → enabledMcpjsonServers`. That file is per-user and untracked: a tracked trust list would approve every server on every clone. (`taom-moduledata` is TAOM-authored, `tools/taom_mcp_server.py`, and requires the `mcp` Python SDK; a Claude restart is needed to pick up a newly-added server.) User-scope servers live in the top-level `mcpServers` of `~/.claude.json` (on 2026-10-08 only `davinci-resolve`). The older `~/.claude/.mcp/user.json` (sequential-thinking, context7) is not a file Claude Code reads: on 2026-10-08 neither server loaded, and neither appeared among the failed connections.

**Pins.** Every auto-fetched project server (`.mcp.json`) runs an exact version: serena a commit SHA, the others a package version (`name@x.y.z`). A pin bump changes every copy of the launch string together: `.mcp.json`, `.codex/config.toml` (Codex still runs filesystem and git) and the snippet in `docs/features/kingdom-voices.md`. `tools/audit_claude_config.py` flags only an unpinned `npx -y` in `.mcp.json`, so an unpinned `uvx` server or a stale copy elsewhere goes unflagged. User-scope servers in `~/.claude.json` are machine-local and not pinned.

## Plugin overlap (routing disambiguation)

Enabled plugins add their own skills alongside TAOM's and the MCP servers. Where they overlap, TAOM routing wins:

| Job | TAOM route | Overlapping plugin/server |
|-----|-----------|---------------------------|
| Pre-commit C# or XML review | `/deep-review` (+ `/review-codex`) | `code-review` plugin (`/code-review`, kept for `/code-review ultra` cloud review) |
| GitHub issues/PRs | `gh` CLI | `github` plugin |
| Redundant-code deletion | `refactoring-specialist` agent | `code-simplifier` plugin (`/simplify`) — disabled 2026-08-05 |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)

<!-- backlinks-end -->
