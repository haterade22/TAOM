# Substance 3D Painter MCP — Setup & Usage

Drive a **live** Adobe Substance 3D Painter session from Claude Code — inspect/build layer
stacks, apply smart materials, bake mesh maps, and export textures conversationally. This is
the texturing-side sibling of the [Blender MCP workflow](../ai-includes/creature-animation-blender-mcp-workflow.md);
Substance Painter is where TAOM's LOTRLOME armor + creature textures are produced.

This is **personal environment tooling** (like the Blender MCP), not TAOM source. It lives in
the machine-local Claude config, not in the repo.

## What it is

**MCP Pro for Painter** v1.2.1 (commercial, $15 one-time, https://painter-mcp.abyo.net/). Chosen
after a code-level deep-dive against three free GitHub repos (josharghhh, elliezu, mdanuz) — it
won on architecture (no in-Painter plugin to maintain), live-session safety (`ScopedModification`
undo), tool coverage (179 tools / 33 modules), and being verified on this exact Painter version.

## Architecture

```
┌──────────────┐  stdio (MCP)  ┌─────────────────────┐  HTTP :60041  ┌─────────────────────┐
│ Claude Code  │ ←───────────→ │ painter-mcp server  │ ←───────────→ │ Substance 3D Painter│
│              │               │ (uv, full mode)     │  /run.json    │ --enable-remote-... │
└──────────────┘               └─────────────────────┘               └─────────────────────┘
```

No Painter plugin is installed. Painter exposes a JSON-over-HTTP endpoint on `localhost:60041`
**only when launched with `--enable-remote-scripting`**; the MCP server forwards Python snippets
to it and returns JSON. The MCP server itself speaks stdio only (no listening socket).

## What's installed where

| Component | Path | Notes |
|---|---|---|
| Server package | `C:\Users\mikew\substance_painter_mcp\` | Copied from the purchased v1.2.1 archive (kept off the E: asset folder). `uv` builds its `.venv` here on first run. |
| MCP registration | `C:\Users\mikew\.claude.json` → `projects["e:/repos/TAOM"].mcpServers."substance-painter"` | Machine-local, sibling to `blender`. `uv run --directory … painter-mcp --mode full`. **Not** in the repo — no `.mcp.json` / `settings.local.json` change. |
| Painter launcher | `C:\Users\mikew\substance_painter_mcp\launch-painter-remote.bat` | Starts Painter 12.0.3 with `--enable-remote-scripting`. |
| Painter | `C:\Program Files\Adobe\Adobe Substance 3D Painter\` | v12.0.3 (Python API 0.3.5), standalone Adobe install. |

## Using it

1. **Launch Painter with the bridge:** run `launch-painter-remote.bat` (or append
   ` --enable-remote-scripting` to a Painter shortcut's Target). Open or create a project.
2. **Start/restart Claude Code** in the TAOM project — MCP servers initialize at startup, so a
   newly-added server won't appear until a full restart. The `mcp__substance-painter__*` tools
   then load **on demand** (Claude Code uses deferred tool loading, so all 179 cost almost no
   context until called).
3. **Probe the connection:** ask Claude to *"call painter_connect"* — expect `painter_version` +
   `api_version` back. Then drive it in prose: *"List my texture sets with their resolution and
   channel count."*

## Modes

`--mode {minimal 25 | lite 74 | default 134 | full 179}`. We run **full** — the vendor's own
recommendation for Claude Code specifically, because deferred tool loading makes the 179-tool
surface nearly free in context. Drop to a smaller mode only if a different client with a hard
tool-count limit is ever pointed at this server.

## Local fixes applied (Painter 12.0.3)

These edits live in the local **editable** copy under `C:\Users\mikew\substance_painter_mcp\server\`.
They are **not** committed to this repo (the package is proprietary — its license §3 forbids
publishing the source) and must be **re-applied after a vendor update** (lifetime updates overwrite
`server/`). Because `uv` installs the package editable, a Claude Code restart picks the patch up
with no reinstall — confirmed: `import server.tools.texture_set` resolves to the source path, not a
`.venv` copy.

**Channel reads — fixed.** 12.0.3 removed `TextureSet.all_channels()`; channels moved to the
**`Stack`**. The vendor's channel-enumeration tools returned empty `channels: []` and one smoke
test failed (`textureset_all_channels`). Fix: route each channel read through the stack with a
legacy fallback —

```python
ts.get_stack().all_channels() if hasattr(ts, "get_stack") else ts.all_channels()
```

Applied at 8 call sites in 5 files: `server/serializers.py`, `server/tools/texture_set.py` (×2),
`server/tools/introspection.py` (×2), `server/tools/layer.py` (×2, preserving the `{{}}` snippet
brace-escaping), `server/tools/validation.py` (×2). Verified two ways: the bundled `painter-cli`
(`set list` reports `BaseColor/Height/Roughness/Metallic/Normal`; `probe smoke --category all` →
**15/15**, was 14/15) and the reloaded Claude-Code-hosted server (`list_texture_sets` now returns
the channels).

**Undo — not fixable (Adobe limitation).** A probe of all 18 `substance_painter` submodules found
**zero** undo/redo callables, so `verify_undo_works` / the `history` tools correctly report
`UnsupportedError`. Nothing to patch. Manual **Ctrl+Z** still works (every mutation is wrapped in
`ScopedModification`); for AI-driven layer additions, use the rollback helper below.

## Layer rollback helper — `taom_layer_rollback.py`

TAOM-owned (`C:\Users\mikew\substance_painter_mcp\taom_layer_rollback.py`, separate from the vendor
package — **update-safe**). A practical layer-level stand-in for the missing scriptable undo:
snapshot the layer UIDs before an AI batch, then delete any layers added since.

```text
cd C:/Users/mikew/substance_painter_mcp
uv run python taom_layer_rollback.py snapshot --out baseline.json   # before the AI works
# ... AI adds layers via MCP ...
uv run python taom_layer_rollback.py rollback baseline.json         # remove what was added
uv run python taom_layer_rollback.py rollback baseline.json --dry-run  # preview only
uv run python taom_layer_rollback.py list                           # inspect current layers
```

It reuses the bundled `PainterClient` for the wire protocol and mirrors the vendor's delete path
(`node.delete()` inside `ScopedModification`). **Scope:** reverts layer **additions** only — not
paint strokes, property/opacity/blend tweaks, channel edits, or deletions. Verified: snapshot → add
a fill layer → rollback deleted exactly the new layer, baseline untouched.

## Security

Sound model (see the package's `SECURITY.md`): stdio-only, loopback-only to Painter, **no
telemetry**, scoped tools embed params via `repr` (`py_literal`) so they can't be coerced into
arbitrary execution. Two tools — `execute_python` / `execute_js` — are genuine arbitrary code
execution inside Painter. To strip them (e.g. when pasting untrusted text into prompts), add
`"--no-execute-arbitrary"` to the `args` in `.claude.json`; the other 177 tools keep working.
Every layer mutation is wrapped in `ScopedModification`, so a single **Ctrl+Z** in Painter
reverts a whole AI edit sequence (for scripted layer-level rollback of an AI batch, see
`taom_layer_rollback.py` under "Layer rollback helper"). Note there is **no scriptable undo** in
12.0.3 — Ctrl+Z is manual-only.

## Verify / troubleshoot

- **Environment check (no Painter needed):** `uv run --directory C:/Users/mikew/substance_painter_mcp painter-mcp-setup doctor`
  → reports Python, mcp SDK, tool count (179/33), Painter reachability, configs.
- **Tools never appear in Claude Code** → you didn't fully restart after the config edit; or the
  `.claude.json` entry was lost (the running session can re-serialize that file — re-add the
  sibling block and restart).
- **`ConnectionError: Cannot reach Painter`** → Painter not running, or launched without
  `--enable-remote-scripting` (use the `.bat`), or no project open.
- **`ModuleNotFoundError: substance_painter`** → Painter too old (pre-11.0). N/A here (12.0.3).
- **`channels: []` on every texture set** → the pre-patch 12.0.3 `all_channels` bug; fixed locally
  (see "Local fixes applied"). If it reappears, a vendor update overwrote the patch — re-apply it.
- **`TimeoutError` on long bakes/exports** → use the async export tools, or raise `--timeout`.

## Updating

New release email from itch.io → unzip → overwrite the contents of
`C:\Users\mikew\substance_painter_mcp\` → `uv` re-syncs on next run. Restart Claude Code.
