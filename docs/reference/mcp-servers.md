# MCP Servers

> Extracted from CLAUDE.md 2026-08-05. CLAUDE.md keeps a short "MCP and shell" summary and AGENTS.md
> the one-line research order; this file holds the server table (which tool for which task), the
> research lookup-order detail, and configuration.

## Server table

| Server | Scope | Purpose | Config |
|--------|-------|---------|--------|
| **ilspy** | Project | Decompile TaleWorlds DLLs — fallback when `E:\Decompiled_Bannerlord\` doesn't have what you need | `.mcp.json` |
| **taom-moduledata** | Project | Query TAOM ModuleData integrity (validate, item/troop/culture exists, find-references, list cultures/schemas) — wraps `tools/taom_query.py`. Needs the `mcp` SDK; restart Claude to load. See `docs/features/moduledata-validation.md`. | `.mcp.json` |
| **imagine** | Project | AI image generation for TAOM's 2D work (`https://mcp.imagine.art`, HTTP; needs auth, so unauthenticated sessions can't use it) | `.mcp.json` |
| **elevenlabs** | Project | Voice design and generation into `.voice-scratch/` ([kingdom-voices.md](../features/kingdom-voices.md)) | `.mcp.json` |
| **blender** | Local (`E:\repos\TAOM` only) | Live Blender session for creature animation ([workflow](../ai-includes/creature-animation-blender-mcp-workflow.md)) | `~/.claude.json` |
| **substance-painter** | Local (`E:\repos\TAOM` only) | Live Substance Painter session for texturing | `~/.claude.json` |
| **davinci-resolve** | User | DaVinci Resolve control; machine-local, no TAOM skill, agent or rule calls it | `~/.claude.json` |

**Removed 2026-09-29: `github`, `git`, `filesystem`.** None was called by any TAOM skill, agent or rule; each
duplicated a tool with gates of its own (`gh`, which is authenticated; git through Bash, where the git
hooks live; Read, Glob and Grep, with the Modules folder and `E:\LOTRAOMAssets` as allowed directories
in this machine's `settings.local.json`). The rule, from affaan-m/ECC's connector policy: a server earns
its slot only when a CLI cannot do the job ([adopt-ecc-2026-09-29.md](../reviews/adopt-ecc-2026-09-29.md),
Step 4). **A plugin can declare a server too:** `github@claude-plugins-official` ships only a `.mcp.json`
declaring the same `github` server, so it is disabled in `.claude/settings.json` as well; deleting the
`.mcp.json` entry alone left the server loaded. Codex keeps its own copies in `.codex/config.toml`.

**Serena left on 2026-11-03** (the maintainer's decision). Its `.mcp.json` entry and its sixteen deny entries went with it.

**Local-scope servers are keyed by the project path.** `claude mcp add --scope local` stores a
server under the current folder's entry in `~/.claude.json`. When the repo moved from
`C:\Users\mikew\source\repos\TAOM` to `E:\repos\TAOM`, blender and substance-painter stayed behind
under the old key and no session loaded them, silently, until 2026-09-29. After a move, or in a new
worktree, check `/mcp` inside the session (a shell's `claude mcp list` resolves its own folder) and re-add.

## imagine (2D art)

The `imagine` server (`https://mcp.imagine.art`, HTTP, in `.mcp.json`) is the default source of 2D art for new features: building and map-bar icons, UI sprites, popup illustrations. Do not block a feature on hand-made art. Check first that the server is authorized: it needs a sign-in, and on 2026-10-18 a VS Code session had to run `/mcp` and authorize the connector before its tools worked. If it is still unauthorized, tell Mike.

- **Uploads.** The `request_image_upload` widget does not render in the Claude Code VS Code extension, and `user_upload` wants base64 far too large to send inline. Mike uploads on the ImagineArt website; `list_assets` (with `service="user"`) then returns each upload's `mediaUrl` (`https://asset.imagine.art/processed/<id>`) for `generate_image`'s `image_url`. Identify a file by the width, height and size it reports, and fetch the `mediaUrl` to confirm a pixel match before spending a generation. The organisation id is internal: never write it in a doc, issue or commit.
- **One reference image per layout.** To trace a layout, give the structure picture as the only `image_url` and describe the style in the prompt. On 2026-09-30, with a style picture as a second reference, nano-banana-pro copied that picture's geography (97 percent of the coast within 30 of 1600 units); with the guide alone it traced it (97 percent within 2.3). gpt-image-2 traced too, but looser (10.8) and darker. Measure alignment instead of judging by eye; the atlas parchment used `E:\LOTRAOMAssets\atlas_parchment\scripts\atlas_measure.py`.
- **Provenance.** Record every generation in a `provenance.json` beside the files (model, asset id, prompt), as `tools/realm_border_art/provenance.json` does.

## Denied write tools

No MCP write tool is denied now. Until 2026-09-29 nine entries in `permissions.deny` covered the `git` and `filesystem` servers' write tools; those servers are gone, and the entries with them. Sixteen Serena write tools were denied from 2026-09-25 (decision 58) until Serena left on 2026-11-03. If a server with write tools is added again, deny them in the tracked `.claude/settings.json` before enabling it: derive the list from the pinned version, from each tool's `readOnlyHint: false` annotation or its can-edit marker, never from names.

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

Follow the order below; `taom-src` is the authority for signatures. It runs `ilspycmd` against the installed DLLs (version from `Version.xml`, build stamp from `TaleWorlds.Library.dll`) and caches under `~/.taom-src/<version>-<stamp>/`. The `E:\Decompiled_Bannerlord\` dump matches the pin and is fine for browsing namespaces/patterns; for authoritative signatures prefer `taom-src` against the installed DLLs (the dump can lag after an engine bump).

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does X work" questions (lifecycle, formation, mount/rider, campaign-mission seam, heartbeat, spawn pipeline). Saves raw decompile time when the process is already documented. |
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | One command: decompiles the installed DLL on cache miss, returns absolute path | **For signature verification** (Harmony patch, GameModel override, adapter, API call): authoritative; run after you understand the process conceptually |
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

Project-level MCP servers (ilspy, taom-moduledata, imagine, elevenlabs) are configured in `.mcp.json` at the project root and each developer trusts them in their own `.claude/settings.local.json → enabledMcpjsonServers`. That file is per-user and untracked: a tracked trust list would approve every server on every clone. (`taom-moduledata` is TAOM-authored, `tools/taom_mcp_server.py`, and requires the `mcp` Python SDK; a Claude restart is needed to pick up a newly-added server.) User-scope servers live in the top-level `mcpServers` of `~/.claude.json` (on 2026-10-08 only `davinci-resolve`). The older `~/.claude/.mcp/user.json` (sequential-thinking, context7) is not a file Claude Code reads: on 2026-10-08 neither server loaded, and neither appeared among the failed connections.

**Project servers are keyed by the folder's spelling.** `~/.claude.json` keeps a project's local
servers under the path as it was opened, case included: on 2026-10-16 `blender` and
`substance-painter` sat under `E:/repos/TAOM`, and a session opened as `e:\repos\TAOM` read the
lowercase key, which held none, so neither loaded. Open the folder as `E:\repos\TAOM`.

**Pins.** Every auto-fetched project server (`.mcp.json`) runs an exact package version (`name@x.y.z`). A pin bump changes every copy of the launch string together: `.mcp.json`, `.codex/config.toml` (Codex still runs filesystem and git) and the snippet in `docs/features/kingdom-voices.md`. `tools/audit_claude_config.py` flags only an unpinned `npx -y` in `.mcp.json`, so an unpinned `uvx` server or a stale copy elsewhere goes unflagged. User-scope servers in `~/.claude.json` are machine-local and not pinned.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/kingdom-voices.md](../features/kingdom-voices.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)
- [docs/reference/substance-painter-mcp-setup.md](./substance-painter-mcp-setup.md)

<!-- backlinks-end -->
