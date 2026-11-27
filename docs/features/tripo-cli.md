# Tripo CLI (3D models from a picture or a text, from the command line)

## Overview

Tripo is a service that makes a textured 3D model from one picture, from several views or from a
text. Its command-line tool, `tripo`, lets a session run that service without the web page: it
sends the input, waits, and writes the model, a preview picture and a task record to a folder. This
doc is the record of how TAOM uses it: the setup, the rules, the commands, what it is good for, and
the lessons of each job. Add to "Jobs" and "Lessons learned" after every run.

**Two routes since 2026-11-27.** The command-line tool spends the API credits. The Tripo Studio MCP server
(section "Tripo Studio MCP" below) gives a session Tripo's tools as tool calls and spends the credits of the web
studio. The rules of this doc hold for both routes; each row of "Jobs" names its route.

Tracking issue: #1604. First job: the breach heap of the Rivendell siege set (#1600).

## Why this exists

- **Before:** Mike made Tripo models by hand in the web studio and handed the FBX to a session. The
  [`/prep-tripo-prop`](../../.claude/skills/prep-tripo-prop/SKILL.md) skill covers what happens after
  that (scale, reduce, new UVs, textures, the collision body).
- **Now:** a session can make the model itself, from a reference picture that Mike has seen, and
  can repeat a run with other settings. Mike asked for this on 2026-11-25 for the kingdom kits job
  ("if you give me images of what you want I can also try it in triplo, we have an MCP and API to
  it").
- **Without it:** carved and broken parts (a heap of fallen stone, a statue, a finial) are built
  from boxes by a script and read as boxes.

## What it is good for here, and what not

| Use it for | Do not use it for |
|---|---|
| One-off carved or organic parts: statues, finials, lantern tops, a carved keystone, a door leaf | Modular architecture: walls, towers, gatehouse bodies |
| Broken and heaped things: rubble, fallen blocks, debris | Anything with an exact joint, an exact door or deck size, or a tiling texture set |
| A layout to measure and rebuild from (the Carn Dum icon) | Parts that must be exact mirror pairs |

The reason for the right column: a generated mesh has no exact dimension, no shared end section and
no flat face that stays flat when it is reduced. The Carn Dum map icon is the precedent: it was
built again from clean solids on the layout that was measured from a Tripo model
([map-icons.md](../kitbash/map-icons.md)).

## Setup on this machine (2026-11-25)

| Item | Value | How it was read |
|---|---|---|
| Tool | `tripo-cli` 0.11.4, installed with `npm install -g tripo-cli` | `tripo --version` |
| Node | v22.21.1 (the tool needs 20 or later) | `node --version` |
| Account site | `ov`, the international site | `tripo login --region ov` |
| Key | saved by the login in `~/.tripo/config.json`; never in the repo, never in a chat | the login result |
| Self check | every critical check passes | `tripo doctor` |
| Balance of the API account | 0 after the login, 1,000 after Mike's top-up, 940 after the first job, 840 after two jobs of the chariot session (that session's report; read again as 840 at 14:29) and 670 after the three jobs for the broken wall (all on 2026-11-25); read the present value with the command | `tripo balance --json` |

The login needs a person: the command prints an address and a one-time code, the person signs in on
that page and clicks "Authorize". `tripo login --region ov` waits for that (up to about 15 minutes);
`tripo login --region ov --no-wait` prints the code and ends, and the same command run again
finishes the login.

**API credits and studio credits.** The web studio showed a large credit balance on the same day,
and the key from the login has 0. Tripo's billing and pricing pages (read 2026-11-25) do not say
whether the two are one balance. They say that the API balance is "usable only for Tripo API and
related service fees", that "100 credits = $1.00 USD", and that a top-up starts at 1 dollar with
presets of 50, 100 and 250 dollars. So the working rule is: the tool needs credits on the developer
site, and the studio credits are spent in the studio. A top-up is a purchase and is Mike's step.

## Tripo Studio MCP (tool calls in the session, on the studio's credits)

Tripo published an official MCP server for the web studio on 2026-11-27 (its page: `https://studio.tripo3d.ai/mcp`).
Mike ordered the install the same day ("Yes, install this so that we can use it!"). It is the answer to the open
question of this doc: the studio's credits can now be spent by a session, without the API credits.

### Setup on this machine (2026-11-27)

| Part | State |
|---|---|
| MCP server | Name `tripo`, address `https://msapi.tripo3d.ai/mcp`, HTTP transport, OAuth with client ID `studio-claude-code` and callback port 3000, at user scope (every project). Added with `claude mcp add --transport http --scope user --client-id studio-claude-code --callback-port 3000 tripo https://msapi.tripo3d.ai/mcp` |
| Sign-in | `claude mcp login tripo` in MIKE'S OWN terminal. A session's shell cannot do it: the command stops with "stdin isn't a terminal". After his sign-in, `claude mcp get tripo` showed `Connected` |
| Skill | `tripo-studio-mcp`, at user scope in `~/.claude/skills/tripo-studio-mcp`, copied from `github.com/Tripo-Developer/Tripo-Studio-MCP` after a read of every file (text only: one `SKILL.md`, six reference notes, no hook, no script). `SOURCE_COMMIT.txt` in the folder names the commit |
| Remove | `claude mcp remove tripo -s user`, and delete the skill folder |

A session that was open before the install has no Tripo tools: reconnect the server with `/mcp`, or start a new
session. No API key is used. Tripo's page says that a personal account works and a team account does not, and that
free trial tries and free retries of the web page do not apply through the server.

### Which route for which job

| Need | Route | Why |
|---|---|---|
| One picture or one text to a model, repeated with other settings, with the files on disk at the end | The CLI (`tripo make`) | One blocking command writes the model, the preview and the task record; a dry run prints the plan and an estimate; the balance can be read (`tripo balance`) |
| Work on a model that is already in the studio: new textures, a remesh, a split into parts, a low poly version, an export in another format | The Studio MCP | These tools exist only there. They work on the assets of the studio account, the ones that Mike made by hand too |
| The API credits are used up and Mike does not want a top-up | The Studio MCP | It spends the studio's pool |
| Several views of one subject to one model | Either | The CLI takes several pictures; the MCP has `tripo_multiview_to_model` (front and at least one of left, right, back) |
| A job with a hard spending cap | The CLI | The MCP has no tool that reads the balance or quotes a price |

### What the server offers

The tool names below are from the skill's reference notes (commit in `SOURCE_COMMIT.txt`), NOT from the connected
server: no session has listed the live tools yet. The live tool list and its input schemas are the contract.

| Work | Tools |
|---|---|
| Make a model | `tripo_text_to_model`, `tripo_image_to_model`, `tripo_multiview_to_model`; then `tripo_get_model_task` and `tripo_get_model_asset` |
| Pictures | `tripo_generate_multiview_images`, `tripo_upscale_image`, `tripo_split_image`; then `tripo_get_image_task` and `tripo_get_image_asset` |
| Textures | `tripo_texture_model`, `tripo_texture_upscale`, `tripo_uv_unwrap`, `tripo_generate_pbr` |
| Geometry | `tripo_remesh_model` (an explicit quad choice and a face limit of 500 to 50,000), `tripo_smart_low_poly_model`, `tripo_segment_model`, `tripo_complete_model`, `tripo_copy_model` |
| Rig and motion | `tripo_rig_model`, `tripo_text_to_motion`, `tripo_list_motion_presets`, `tripo_retarget_model`, `tripo_get_motion_asset` |
| Files in and out | `tripo_create_image_upload_grant`, `tripo_create_model_upload_grant`, `tripo_import_model`, `tripo_export_model`, `tripo_get_model_export`, `tripo_list_model_assets` |
| Recovery | `tripo_get_model_task_by_idempotency_key`, `tripo_get_image_task_by_idempotency_key`, `tripo_cancel_model_task` |

Model families, as the skill's notes give them: HD (releases such as `v3.1-20260211`; up to 50,000 quad faces, 1,000,000
standard or 2,000,000 detailed triangle faces) and Smart Mesh (P1 is Nexus v1 with up to 20,000 triangle faces; P2 is
Nexus v2 with up to 25,000 quad or 50,000 triangle faces, and it alone takes a symmetry flag).

### Rules for a session (in addition to the rules below)

1. **Load the skill `tripo-studio-mcp` before the first call**, and read the live schema of each tool that you call.
2. **Use it freely; record each job.** Mike, 2026-11-27: "Just tripo MCP at will we have a ton of credits." So a
   generation through the Studio MCP needs no question about credits. The server cannot read the balance, so state
   the reference cost from the table below in the job's record, and add the row to "Jobs" with the route `MCP`.
   A purchase or a top-up stays his step.
3. **Prompts, pictures and file names stay generic**, as for the CLI. A picture goes to Tripo through an upload
   grant: Mike's reference pictures are never uploaded. Use only a picture that was made for this job from a generic
   prompt, and that Mike has seen.
4. **Submit once.** Give each write a stable `idempotency_key`, and save the tool name, the key and the task id in the
   job folder at once (`task.json`). If the answer is unclear, find the task by its key
   (`tripo_get_model_task_by_idempotency_key`); never send the write again. A second write is a second charge.
5. **Name `family`, `model_version`, `texture` and `face_limit` yourself.** With no face limit an HD job asks for
   the largest mesh. A limit is a request, not a promise: measure the result.
6. **Wait with a bound.** Poll `tripo_get_model_task` by id at intervals; a progress of 100 percent with the state
   "pending" is not a success.
7. **Keep signed links out of every file and message.** A download link and an upload link carry a credential in
   their query. Write the asset id, not the link.
8. **Files go where the CLI's files go** (section "Where files go"): one folder a job, with the model, a preview
   that you looked at, and `task.json`.
9. **A Tripo model is a source, not an asset.** The way into the game is unchanged: `/prep-tripo-prop`, the mesh
   gates, the Kit import by Mike.

### Reference costs (the skill's notes, read 2026-11-27; the studio's page is the authority)

| Operation | Credits |
|---|---|
| Text or one picture to an HD model (v2.5, v3.0, v3.1) | 15 |
| Text or one picture to a Smart Mesh P1 model | 35 |
| Text, picture or several views to a Smart Mesh P2 model | 100 |
| Texture at 2K (`standard`); 4K (`detailed`); 8K (`extreme`) | 10; 20; 30 |
| HD with quads; HD with detailed geometry | 5 more; 15 |
| PBR maps | 5 |
| Triangle retopology; quad retopology | 5; 10 |
| Smart low poly | 30 |
| Split into parts (segmentation); part generation | 40; 30 |
| Texture upscale; picture upscale | 10; 10 a picture |
| Rig; motion | 20; 20 |
| Export of an own model, import of a model, several views of a picture | No credits, with plan limits |

Example: an HD model from one picture with a 2K texture is 15 + 10 = 25 credits by this table. A blank or missing
price never means zero.

### A first job, step by step (not run yet)

1. Name the part and its use in the job's record (no question about credits is needed since 2026-11-27).
2. Make the picture with a generic prompt (the image connector), show it to Mike.
3. `tripo_create_image_upload_grant` (the MIME type and the size in bytes), then PUT the bytes to the granted
   address with the required headers.
4. `tripo_image_to_model` with the grant's `object_key` as `image_key`, `family: hd`, the model version from the live
   schema, `texture: true`, `texture_quality: standard`, a `face_limit`, and the `idempotency_key`.
5. Poll `tripo_get_model_task`; on success read each `model_id` with `tripo_get_model_asset`.
6. Download the model from its `model_url` into the job folder. If Blender cannot read the GLB (the skill's notes
   name a Meshopt packed form), decode it first; the notes give the `gltf-transform` commands. Not tried here.
7. Look at the preview, measure the mesh in Blender, add the row to "Jobs", then `/prep-tripo-prop`.

### What it could change for the kits (ideas, none tried)

- A source for carved wood and tree forms of the forest kingdoms, where a script gives boxes.
- `tripo_segment_model` on a heap or a statue group, so that each block or figure becomes its own part.
- `tripo_remesh_model` or `tripo_smart_low_poly_model` as a second reduction beside our own (compare both on one
  model before a rule is made; our rule for retopology in Blender stays the quad toolkit).
- `tripo_texture_model` with a text prompt on a mesh that we built, as a look study only: a kit piece keeps the
  kit's tiling sets and trim sheet.

## The two bridges (the studio's own credits)

Mike has two plugin archives from Tripo in his Downloads folder. I read their file lists and entry
files on 2026-11-25 without running them:

| Archive | What it is | Facts from its files |
|---|---|---|
| `Tripo3d_Blender_Bridge-latest.zip` | The Blender add-on "Tripo Bridge" 1.1.0, written for Blender 4.1 and later, with a panel in the 3D view sidebar | It runs a WebSocket server inside Blender (it carries its own `websockets` library; port 60600 is named in the code) and has an update check that fetches from Tripo's CDN |
| `Tripo3d_Substance_Painter_Bridge-latest.zip` | The Painter plugin "Tripo Bridge" 1.1.0 | The same pattern: a WebSocket server, a model loader, an update check |

A bridge receives a model that a person makes on the studio page, so it uses the studio credits and
needs no API credits. It needs the program open with the plugin on, and a person on the studio page.
Neither is installed by a session yet. Not checked: the add-on in Blender 5.3 (it names 4.1).

## Rules for a session that drives it

The tool carries its own guide for agents. Read it before the first run of a session:
`tripo docs --llm`, and for one command `tripo docs --topic commands/make` (also `commands/generate`,
`commands/process`, `examples/game-asset`, `common-errors`). The web page lags the tool: on
2026-11-25 the page described 0.9.0 and the installed tool was 0.11.4.

1. **Set two variables on every command:** `TRIPO_ANALYTICS_ENABLED=0` (no usage analytics) and
   `TRIPO_CLIENT_APP=claude` (the tool's guide asks for the name of the calling program).
2. **Keep every prompt and file name generic.** No project name, no place name of the game, no
   person. The picture and the prompt go to an outside service.
3. **Validate before you spend:** `tripo make <input> ... --dry-run --json` prints the plan, the
   warnings and an estimate with no network call. A missing estimate never means zero credits.
4. **`tripo make` blocks until the files are on disk.** Wait for it (give it 15 minutes or more);
   write no polling loop; read the one JSON line on stdout. Use `--json --yes`.
5. **Look at `preview.png` before the next step.**
6. **A second try is a new paid task.** `tripo redo` has no plan and asks nothing. Mike's word of
   2026-11-25 for the kingdom kits job: "dont worry abotu credits, we have plenty". Still say what
   each run cost (`credits_consumed` in the result) in the "Jobs" table.
7. **Exit code 4 is "no credits": stop.** Do not try other settings or models. Tell Mike; the
   top-up (`tripo topup`) is his step.
8. **Exit code 6 with a timeout:** the task still runs and is billed if it succeeds. Use
   `tripo task watch <id> --download`, never a new run. **Exit code 7** after a submit: the task may
   exist; check `tripo balance` before you run again, and fetch a finished task with
   `tripo task get <id> --download`.
9. **Never let the tool or yourself change the model version of a paid task without a decision.**

## Commands that we use

```bash
export TRIPO_ANALYTICS_ENABLED=0 TRIPO_CLIENT_APP=claude
tripo doctor                                   # key, network, balance
tripo balance --json                           # {"balance", "frozen"}
tripo models                                   # model ids and aliases, no network call
tripo make ./ref.png --dry-run --json          # the plan and the estimate, no credits
tripo make ./ref.png -o <out folder> --json --yes          # one picture to a model
tripo make ./front.png ./back.png -o <out folder> --json --yes   # two to four views
tripo make @last --then convert:fbx --json --yes           # go on from the last task
tripo mesh decimate <model.glb> --model v3.0 --face-limit 5000 --smart-uv
tripo task get <task id> --download            # fetch the files of a finished task
```

Facts from the tool's guide of 0.11.4:

- The default 3D model is P2 (`P2-20260801`). A P2 generation costs 100 to 130 credits by its
  texture tier. P2 takes a face limit of 48 to 50,000 triangles (48 to 25,000 with `-p quad=true`).
- The H series (`--model H3.1`) is needed for `smart_low_poly`, `generate_parts` and
  `geometry_quality`.
- PBR maps are off by default; `-p pbr=true` turns them on.
- `--then` steps: `texture`, `convert`, `rig-check`, `rig`, `retarget`, `segment`, `complete`,
  `decimate`, `uv`. A step takes arguments as `convert:format=FBX,texture_size=2048`.
- `tripo mesh uv` fails over 80,000 triangles (a quad counts as two): reduce first.
- The result folder holds the model file, `preview.png` and `task.json`.
- `tripo mcp` runs the tool as an MCP server. It is not set up in this repo.

## From a Tripo model to a Bannerlord asset

A Tripo model is a start, not an asset. It has no fixed scale, a texture of its own on an
automatic unwrap, and more triangles than a prop may have.

1. Make the model (this doc).
2. Bring it to the game's form with the [`/prep-tripo-prop`](../../.claude/skills/prep-tripo-prop/SKILL.md)
   skill: scale by a known size, reduce, new UVs, new bake, the `bo_` collision twin.
3. Every new texture is 1K (1024 px) in the kingdom kits job. Where the part belongs to a kit with
   tiling sets, try the kit's own set by a box projection first, so that the part has the kit's
   stone and needs no texture of its own.
4. Show the result to Mike in a Blender window before any hand-off.

## Where files go

- Reference pictures and results of a job: the job's work folder, in a `tripo` folder. For the
  kingdom kits job: `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/tripo` (`refs`, `out`,
  `docs` with a copy of the tool's guide).
- Give `tripo make` an explicit `-o`. Its default is `tripo-out` in the folder where it runs.
- Nothing from Tripo goes into a module's `AssetSources` without Mike's word.

## Reference pictures: the brief that we use

One object, centred, with a margin; a plain mid-grey background; a three-quarter view from a little
above; even soft light; no shadow on the ground; no figure, no plants, no text; the sizes in metres
in the text. Whether this brief gives the best model is not proven yet: record the result under
"Lessons learned".

## Candidates in the Rivendell siege set (2026-11-25)

Mike, about 13:35: "We also have more tripoli we can do, keep that in mind" (his name for the tool). So a carved,
organic or broken part gets a Tripo proposal, with its cost, before it is built by code. The set goes one piece at a
time, so each candidate waits for its piece. A dense generation cost 60 credits on the first job.

| Piece | Part | Why Tripo fits | What we take from the model |
|---|---|---|---|
| Broken wall (next piece) | Four to six single fallen blocks with broken corners and a split face | The builder's loose blocks are clean cuboids; a broken stone is an organic shape | The shape, reduced to about 150 to 300 triangles each; the kit's wall stone by a box projection |
| Broken wall | The torn faces at both sides of the gap | A real break has no regular steps | The relief of a break as a height field over our core, as with the mound |
| Gatehouse `b` | Capitals and bases of the columns under the turret roofs, and the finials of the spires | Carved, small, seen against the sky | One half or one quarter, mirrored, so that the part is exactly symmetric |
| Towers and wall | A carved bud or leaf corbel under a free rib foot, if Mike chooses the collar version with no block | A carved form in place of a box | One half, mirrored; the trim's cream by the vertex colour |
| Railing | The finial of the post | The kit's finial reads as a blob at 15 m in the game mesh | The shape, at 60 to 100 triangles |

Not a candidate: any member with an exact joint, an exact size or a tiling surface (walls, piers, arches, bands, the
railing pattern). Mike's standard for those is "elven smiths didnt make mistakes", and a generated mesh is neither exact
nor symmetric.

## Jobs

| Date | Job | Input | Command | Credits | Result | Teardown record |
|---|---|---|---|---|---|---|
| 2026-11-25 | Breach heap of the Rivendell siege set (kingdom kits, #1600): the low rubble spread | `heap_b_low_spread.png` (Mike's choice of three pictures from the image connector) | `tripo make ./rubble_spread.png --model tripo-v3.1 -p texture_quality=detailed -p geometry_quality=detailed -p pbr=true -p auto_size=true -o ./out --name rubble-spread --json --yes` | 60 (the dry run estimated 60) | Success in 4 min 45 s: `model.glb` (72 MB), `preview.png`, `rendered_image.webp`, `task.json`. A low spread of separate stones on a dust bed, true to the picture. Mike: "This is fine but needs to be part of the broken wall, like Gondor and Erebor have it". Used in two ways (2026-11-25): its height from above is the relief of the breach mound, and its colour, normal and height from above are the kit's new 1K heap map. Mike on the trial piece: "This is great, excellent work". The model's own mesh and 4K maps are not used | [2026-11-26-rubble-heap.md](../kitbash/teardowns/2026-11-26-rubble-heap.md) |
| 2026-11-25 | Chariot work, a black banded horse neck guard, front view (`01_black_neck_front`, task 7ba2cb6f). Reported by the chariot session; not checked by the session that keeps this table | One generated studio picture: the part alone on a plain grey horse bust, plain background | `tripo make <png> --model tripo-v3.1 -p geometry_quality=detailed -p pbr=true -o <dir> --name <name> --yes --json --no-open --timeout 1200` | 50 | Success, 13:40:33 to 13:43:18; `model.glb` about 58 MB. Judged from the preview only: it matches the picture in band count, rims and silhouette. Use: a ratio reference for a parametric build (band pitch, rim thickness, flare), not geometry to ship | none: a run of the chariot session; its record is the knowledge library's `2026-11-26-horse-armour-neck-and-head.md` |
| 2026-11-25 | Chariot work, the same neck guard, rear view (`02_black_neck_rear`, task 928b4119). Reported by the chariot session; not checked here | One generated studio picture, as above | The same command | 50 | Success, 13:43:18 to 13:46:05; `model.glb` about 58 MB. The balance read 940 before the two jobs and 840 after | none: a run of the chariot session; its record is the knowledge library's `2026-11-26-horse-armour-neck-and-head.md` |
| 2026-11-25 | Broken wall of the Rivendell siege set (kingdom kits, #1600): three fallen blocks (`fallen-blocks-c-003bff1f`, task 003bff1f) | `blocks_c.png` (one picture from the image connector, generic prompt, chosen by the session) | `tripo make ./<picture> --model tripo-v3.1 -p geometry_quality=detailed -p texture_quality=standard -p pbr=true -p auto_size=true -o <out> --name <name> --json --yes --no-open --timeout 1500` (the runner `WORK\tripo\_scripts\run_make.sh`, after a dry run) | 50 | Success, 14:41:11 to 14:43:56; `model.glb` 58 MB. Judged from the preview only: three separate blocks, true to the picture (a sheared corner, a stepped break, chipped corners), with flat dressed faces. Use: the shapes as loose stones on the breach mound, reduced, with the kit's stone. Not opened in Blender yet (the wall is the piece in work) | none: used as geometry (the 8 blocks of `fallen_blocks.fbx`, loose pieces of the heap), never measured as a reference |
| 2026-11-25 | The same piece: a torn wall face, square, seen from the front (`torn-face-d-a0310a28`, task a0310a28) | `wall_end_d.png` (one picture from the image connector, generic prompt, chosen by the session) | `tripo make ./<picture> --model tripo-v3.1 -p geometry_quality=detailed -p texture_quality=detailed -p pbr=true -p auto_size=true -o <out> --name <name> --json --yes --no-open --timeout 1500` (the runner `WORK\tripo\_scripts\run_make.sh`, after a dry run) | 60 | Success, 14:41:13 to 14:44:59; `model.glb` 65 MB. Judged from the preview only: one dressed skin at the side and a torn core of rubble with big blocks that stand out; the stones are softer than in the picture. Use: the relief of one side of the gap as a height field from the front | none: only a trial mesh (`torn_face_a.fbx`); neither flank of the built wall reads it |
| 2026-11-25 | The same piece: a torn wall face, tall (5 by 12 m in the picture), seen from the front (`torn-face-tall-a-8571328f`, task 8571328f) | `torn_tall_a.png` (one picture from the image connector, generic prompt, chosen by the session) | `tripo make ./<picture> --model tripo-v3.1 -p geometry_quality=detailed -p texture_quality=detailed -p pbr=true -p auto_size=true -o <out> --name <name> --json --yes --no-open --timeout 1500` (the runner `WORK\tripo\_scripts\run_make.sh`, after a dry run) | 60 | Success, 14:42:10 to 14:45:27; `model.glb` 65 MB. Judged from the preview only: true to the picture over the whole height: the two toothed skins of dressed blocks at the sides and a torn core of angular fragments between them. Use: the relief of the other side of the gap, and the better of the two for a tall face | [2026-11-26-torn-wall-core.md](../kitbash/teardowns/2026-11-26-torn-wall-core.md) (the left flank, `b_tall`) |
| 2026-11-25 | The same piece: a torn wall face with an irregular break (`torn-face-irregular-a-5265962e`, task 5265962e) | `torn_irr_a.png` (one picture of four from the image connector: skins broken at random lengths, tumbled blocks in the core, an uneven top; chosen by the session) | The same command as the two torn faces before, with `texture_quality=detailed` (the runner `run_make.sh`, after a dry run) | 60 | Success, 18:09:23 to 18:12:39; `model.glb` 38 MB. Judged from the preview only: a box of wall with two skins broken at uneven lengths and a core that lies far back, with tumbled blocks at its foot; the top came out flat, not uneven as in the picture. The balance read 610 after it. Not measured or prepared yet | [2026-11-26-torn-wall-core.md](../kitbash/teardowns/2026-11-26-torn-wall-core.md) (the right flank, `irregular_a`) |
| 2026-11-27 | House B2 high poly study of the Rivendell kit (kingdom kits, #1600), set 1: four hero parts (foot scroll, post capital, finial with corbel, door leaf) | Four renders of the builder's own code sculpts (`WORK\kit_full\hp_bake_b2\tripo_inputs`) | Studio MCP: `tripo_create_image_upload_grant`, a PUT of the picture, `tripo_image_to_model` (family `hd`, `v3.1-20260211`, `geometry_quality` detailed, `texture` false, `face_limit` 800,000), then `tripo_export_model` to `stl` | 4 x 30 by the skill's reference notes (15 base, 15 detailed geometry; the server gives no real charge) | Success, about 11:52: four STL files of 747,858 to 779,362 triangles. Each model repeats the code sculpt that its picture showed, so the gain over the code sculpt is small. Kept as the fallback high polys | none: used as geometry for a bake, never measured as a reference |
| 2026-11-27 | The same study, set 2: the same four parts from concept pictures | Four pictures of carved oak parts from the image connector, generic words; the foot scroll and the door leaf with our own render as the shape reference (`WORK\kit_full\hp_bake_b2\tripo_inputs_concept`) | Studio MCP: `tripo_create_image_upload_grant`, a PUT of the picture, `tripo_image_to_model` (family `hd`, `v3.1-20260211`, `geometry_quality` detailed, `texture` false, `face_limit` 1,000,000), then `tripo_export_model` to `stl` | 4 x 30, plus one failed job (its charge is not known) and one more job for the foot scroll | Success for the post capital (969,874 triangles), the finial (957,702), the door leaf (934,760; its first job failed with no reason, the second worked) and the foot scroll (940,218): rich carvings by the lead's look at renders of each, and the study's high polys. The lead first called the foot scroll plain, from two renders of its back, and submitted a second job from the same picture (940,024 triangles, equal in carving, the board parallel to one axis plane); that job was not needed. The job list: `WORK\kit_full\hp_bake_b2\TRIPO_JOBS.md` | none: used as geometry for a bake |

## Lessons learned (newest first)

- **2026-11-27. The input decides what a sculpt job adds.** A render of our own code sculpt gave that sculpt back (set 1 of the house B2 study). A concept picture of the carving gave a real carving for all four parts (set 2). For a hero part, make the concept picture first, with our render as the shape reference where the outline must hold. Look at each model from both sides before a verdict: a relief board has a plain back, and two renders of that back made the lead call a good sculpt plain and pay for a second job. The model comes as a Meshopt packed GLB that Blender cannot read: export it as STL through `tripo_export_model` (no credits) and download with plain curl.
- **2026-11-25. What the three models for the broken wall held (measured in Blender, one model a job, under 2.5 GB each).**
  About 1.9 to 2.0 million triangles each. The blocks model: three separate closed shells, as the picture showed, each about
  0.7 m long as made (slabs of 1 : 0.4 : 0.3, flatter than a wall block). The tall torn face: not a flat cross section but a
  half-cylinder piece with the torn face on its flat side, 2.1 m high as made, so it covers 5.0 by 9.4 m at the wall's
  thickness and a 12 m end needs a stretch of 1.28. The square torn face: a wall FACE with a deep bite, one skin only, turned
  33 degrees about the vertical axis: not a wall end. A front picture gives a front relief, but the model invents the rest:
  measure what a model is before you plan with it.
- **2026-11-25. A clean low block from a dense generated block.** Scale it by its longest side, lay it on its largest face,
  fuse and reduce to about 220 triangles: the flat dressed faces stay flat within 1 to 3 cm and the broken faces stay rough.
  Eight blocks came from three shapes (as made, cut in two along a break, mirrored and turned over). The kit's wall stone by
  a box rule fits; the heap map on a 0.4 m piece shows as a mosaic. The convex hull (58 to 122 triangles) can be the body.
- **2026-11-25. A torn face as a height field.** Render the model from the front with no perspective and no light: the
  distance to the viewer (16 bit and float), the colour and the normal at 300 px a metre, and a mask of skin against core (a
  flat part within 6 cm of the skin plane). A grid moved by that field, reduced to about 2,600 triangles for 5 by 12 m, reads
  as the torn end of a thick wall against a plain wall block. Weak points that a critic measured: the skins end in regular
  right-angle teeth (a zipper look; the pictures draw bonded masonry that way), the outline of the end is two straight
  vertical edges (all the tearing is in the relief), and a constant tone meets the wall's tone with a step. So: ask the
  picture for skins broken at random lengths with blocks missing and an uneven top, cut an irregular outline per flank,
  and take the tone from the wall's own rule.

- **2026-11-25. For the relief of a broken surface, give Tripo a picture straight from the front.** A three quarter picture of a
  wall end gave a thin wall, regular steps or an old ruin (three of four pictures). A picture "front view, straight on" of
  the cross section gave what a height field needs: the two dressed skins at the sides and the torn core between them, flat
  to the camera. Several separate objects in ONE picture on one seamless background came back as separate shapes (three
  blocks, judged from the preview); a picture made of four panels was not tried.

- **2026-11-25, from the chariot session (judged from the preview pictures only; the models were not opened yet).** A
  reference picture of ONE part on a plain grey bust with a plain background gave a model that matches the picture in
  band count, rims and silhouette, from the front view and from the rear view.
- **2026-11-25, from the chariot session.** A detailed v3.1 image-to-model job with PBR and no detailed texture took about
  2 minutes 45 seconds and cost 50 credits (60 with `texture_quality=detailed`, see the rubble job).
- **2026-11-25, from the chariot session.** A run script written before this document did not set
  `TRIPO_ANALYTICS_ENABLED=0` and `TRIPO_CLIENT_APP=claude` for two jobs; it sets both now. Put the two variables into
  the script, not into the command you type.
- **2026-11-25, from the chariot session.** For a rigid, symmetric part with a triangle limit (armour bands, 10,500
  triangles) the model is a ratio reference for a parametric build, not geometry to ship: it is not symmetric, not
  rigid per band, and far above the limit.
- **2026-11-25. What the first result holds (measured in Blender).** One fused shell of 1,961,462 triangles: the stones are
  not separate objects, so a stone cannot be taken out or placed alone. Three maps of 4096 px (colour, normal, a packed
  occlusion, roughness and metal map). `auto_size=true` gave a model 1.53 by 1.88 by 0.30 m for a spread that is about
  13 m wide in the game: the size must be set by a known measure. For separate stones, ask for parts (`generate_parts`,
  H series) or make single stones.
- **2026-11-25. For a tile from a model, take a patch twice as wide as the tile will be on the mesh.** A 2.64 m patch gave big
  stones in one half and dust in the other, which showed as lanes on a ramp. A 5.28 m patch, mapped at half size, gave four times
  as many stones and no lanes. Map a mound from above, not by a box rule: three projections break the pattern at every face.
- **2026-11-25. A Tripo model can give a tiling texture, not only a mesh.** A render of the dense model straight from above (colour,
  normal and height as emission passes, no light), cut to a tile along a path of least difference, gave a 1K rubble map at 461 px
  per metre with real stones. The model's own 4K bake on a reduced mesh gave about 50 px per metre.
- **2026-11-25. Fuse a Tripo heap before you reduce it.** A collapse of the raw model (to 5,000, 15,000 or 30,000 triangles) gave
  shards, gaps and faces through each other. A voxel remesh at 6 cm first (1.96 million to 133,188 triangles), then the
  collapse, gave one clean closed surface that reads as a heap at 15,000. The blocks get soft: put crisp blocks on top.
- **2026-11-25. A reduced rubble spread does not unwrap well.** At 5,000 triangles and 11 m the two UV methods of the prep tool gave
  824 islands at 30 percent use (`chart_abf` 75) and 2,020 islands at 14 percent (`sharp_seams` 50); the target is 40 percent.
  On one 1K map that is about 50 px per metre, against 461 px per metre on the kit's tiling sets. The Gondor broken wall does
  not bake its rubble either: a mound of 804 triangles on a tiling rubble material, with 18,844 triangles of single bricks on it.
  So for a heap, take the SHAPES from Tripo and the surface from the kit's tiling sets; a bake of the whole heap is the comparison only.
- **2026-11-25. The prep tool reads an FBX with a `.fbm` folder; the tool gives a GLB.** A short Blender script makes the pair
  (the packed occlusion, roughness and metal map split into two files). Or ask the tool for FBX with `--then convert:fbx`
  (the cost of that step is not measured).
- **2026-11-25. The key from the login had a balance of 0.** The studio page showed credits; the
  tool's account did not. Run `tripo doctor` and read the balance before you plan a run, and tell
  Mike at once when it is 0. Ask the question "which credits does this path spend" before a tool is
  installed, not after.
- **2026-11-25. The dense model (H3.1) cost half of the default model and gave a true rubble.** 60 credits with detailed geometry, detailed
  texture and PBR, against 100 to 130 for P2. For a part that is all small shapes, start dense and reduce after.
- **2026-11-25. The dry run's estimate was exact for a single generation** (60 estimated, 60 charged).
- **2026-11-25. A top-up on the developer site reached the key at once** (0 to 1,000 credits); no new key was needed.
- **2026-11-25. Tripo's quick start, billing and pricing pages do not say which credits the tool spends.**
  Three pages were read for that one question. The measured balance of the key was the only evidence.
- **2026-11-25. The tool's own guide is the source, not the web page.** The page described an older
  version. `tripo docs --llm` prints the guide that belongs to the installed version.
- **2026-11-25. The login works from a session with no terminal.** The command prints the address
  and the code; the session opens the page for Mike; the command ends when he has authorized.
- **Earlier (the Carn Dum icon). Architecture from Tripo loses its flat faces when it is reduced.**
  Measure the layout from the model and build it again from clean solids.

## Not checked

- The cost of each `--then` step (the guide gives an estimate for the P2 generation only).
- Whether the studio credits can be used by the CLI: the measured balance says no. The Studio MCP uses them (Tripo's page, 2026-11-27).
- The Studio MCP in use: no session has listed its live tools or run a job through it. The tool names and costs in its section are from the skill's notes.
- The Meshopt decode step for a model from the Studio MCP, and whether Blender 5.3 reads such a GLB directly.
- Either bridge in use.

## How to verify in game

Not applicable: this is a tool that makes source models. A model from it reaches the game only through
the `/prep-tripo-prop` steps and a Modding Kit import, and that asset's own doc holds its in-game check.

## Key files

| File | Purpose |
|---|---|
| `docs/features/tripo-cli.md` | This record |
| `.claude/skills/prep-tripo-prop/SKILL.md` | The steps from a Tripo FBX to a prop or a map icon |
| `tools/oneoff/blender_prep_tripo_prop.py` | Scale, reduce, new UVs, bakes and the `bo_` twin |
| `tools/oneoff/convert_tripo_prop_textures.py` | Packs the maps into the engine's `_d`, `_n`, `_s` form |
| `~/.claude/skills/tripo-studio-mcp/` | The Tripo Studio MCP skill (user scope, outside the repo): `SKILL.md` and six reference notes |
| MCP server `tripo` (user config) | `https://msapi.tripo3d.ai/mcp`; state with `claude mcp get tripo` |

### A model file can be cut off on the server: check the header against the size

`torn-face-irregular-a` (2026-11-25) arrived as 37,748,736 bytes, 36 MiB exactly, where the GLB header declares
68,966,172. `tripo task get <id> --download` gave the same bytes again, and a one-byte range request to the model URL
reports that length: the stored file itself is cut off. No credits are used by a second download.

- **Check every download:** bytes 8 to 12 of a GLB hold its declared length; compare it with the size on disk.
- **What survives:** a GLB stores the index buffer first, then positions, UVs, normals, images. In this file the indices
  and the positions were whole, so the shape was usable; the UVs, normals and textures were not.
- **When only the shape is needed** (a height field, a relief), read the two whole buffers and go on. When the textures
  are needed, the model must be generated again.
