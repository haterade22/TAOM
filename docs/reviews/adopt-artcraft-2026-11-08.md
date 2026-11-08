# Reviewing ArtCraft (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: nothing installed and no upstream code ported.** TAOM took the names and meaning of four
Tripo generation options, confirmed on fal's own API page, and wrote them down in
[ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md). No TAOM asset
has used them yet.

Mike asked whether ArtCraft holds anything TAOM should take, given that TAOM already uses ImagineArt,
Tripo, Blender and Substance Painter. `adoption-history.md` had no entry, so this is a first review.

## Source, and what was read

`storytold/artcraft` at `79d87b2b54b5b27223fa428bd0acd06bdd7abaf5` (committed 2026-11-07, 7,327 tree
entries). It is a Tauri desktop app in Rust and TypeScript for AI image and video work, aimed at
filmmakers. It sends generation requests to fal, to its own hosted service at `api.storyteller.ai`,
and to other providers. The backend is a separate repository, `artcraft-services`.

Everything was read through the GitHub API. Nothing was cloned, built or run.

| Depth | Files |
|---|---|
| Read in full | `README.md`, `AGENTS.md`, `ROADMAP.md`, `NOTICE`, `crates/desktop/artcraft/capabilities/default.json` |
| First part read | `tauri.conf.json`, `docs/website-login.md`, `_docs/artcraft_omni_api.md`, the three `.claude/rules` files, `crates/lib/browser_emulation`, `AnglesStore.ts`, the MCP session definitions, the fal request types for Tripo multi-view, Hunyuan Smart Topology and Qwen angles |
| Pattern sweep only | All 63 `package.json` files for install hooks; the file tree for telemetry, MCP, mesh and login paths |

**Not read:** the Rust code in full, the `build.rs` scripts and the release binaries. GitHub code
search returned 0 hits for this repository, including for `gltf`, so that search proves nothing and no
result here relies on it. `audit_claude_config.py --external` was not run, because there is no skill
to port and the review made no clone for it to scan.

## Security

Safe to learn from: yes. Safe to install: no, and TAOM has no need to.

- One install hook in 63 manifests: `"prepare": "husky"` in `frontend/apps/artcraft/package.json`.
- Login windows extract cookies for provider sites: `grok_login_webview_extract_cookies.rs` and
  `extract_midjourney_webview_cookies.rs`. The Sora service has code that reads credentials from disk.
- `crates/lib/browser_emulation` copies browser TLS and HTTP/2 fingerprints for what its own comment
  calls "Cloudflare-guarded scraping endpoints". A provider login used through the app carries that
  account risk. Whether a provider's terms allow it was not checked.
- The content security policy allows Microsoft Clarity and Google Analytics. `posthog-js` is a
  dependency, and `connect-src` lists no PostHog host.
- The webview policy allows `unsafe-eval` and `unsafe-inline`, scripts from `unpkg.com`, and an asset
  scope of `**`. The HTTP plugin may reach `https://*`.

**License.** The code is MIT or Apache-2.0. `NOTICE` excludes Nintendo sound effects, meme images,
two skyboxes, an MMD motion and Mixamo renders, and states that these remain the property of their
owners. Nothing may be copied from `frontend/apps/artcraft/app/public/resources/`.

## Mapping against TAOM

| Upstream part | Verdict | Reason |
|---|---|---|
| Tripo H3.1 options `face_limit`, `quad`, `image_urls` order, quality levels | **Taken** | A search of `docs/features`, `docs/reference`, `docs/kitbash`, `docs/ai-includes`, `tools` and `.claude/skills` found no use of `face_limit` and no multi-view input. `/prep-tripo-prop` starts from a finished FBX and repairs a dense triangle mesh, and these options act before that point |
| Multi-view input with camera-angle generation (`fal-ai/qwen-image-edit-2511-multiple-angles`) | Open | Needs a measured trial. The app snaps rotation to steps of 45 degrees, tilt to -30, 0, 30 or 60, and zoom to 0, 5 or 10 |
| Hunyuan Smart Topology (`fal-ai/hunyuan-3d/v3.1/smart-topology`) | Open | GLB or OBJ in, up to 200 MB, quad or triangle out. AGENTS.md "Blender retopology" requires the quad toolkit, so a trial needs Mike's decision first |
| Blocking a scene in 3D before generating | Open | A Blender viewport render could be the single layout reference that `mcp-servers.md` already calls for. Untested |
| The desktop app | Skip | The security points above. ImagineArt MCP and Tripo cover the use |
| Provider router and fal client | Skip | Rust plumbing for an app TAOM does not build |
| Omni API and MCP sessions | Skip | API keys need staff approval. This repository holds only the session definitions |
| Video, Suno audio, Gaussian splats | Skip | No hit in the folders named in the first row |
| Background removal | Skip | `banner-icon-generation.md` already keys generated images |
| House rules and `.claude/rules` | Skip | TAOM's rules cover the house rules. The three rules files describe its backend |

**Unverified.** The `imagine` MCP server was not authorized in this session, so which ImagineArt
models offer angle control is unknown. fal's page gives no price, and no fal use was found in `docs`,
`tools` or `.mcp.json`. How TAOM's existing Tripo FBX files were generated is not recorded in the
folders searched.

## Changed in TAOM

- `ue-to-bannerlord-asset-pipeline.md` has a "Generating the source" part in the single-prop path.
- `/prep-tripo-prop` points to that part when the FBX does not exist yet.

## Offered trial

This needs Mike's word, because it costs money. Take one real prop and one concept image. Generate it
from (A) the single image and (B) four views, with `face_limit` set from the budget on both. Compare
them with `tools/blender/probe_fbx.py` and side by side in Blender. The open rows above stay open
until a run like this produces a number.
