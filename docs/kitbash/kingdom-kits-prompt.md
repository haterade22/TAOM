# Kingdom kits: reuse our kitbash meshes, retexture them per kingdom, and build what is missing

Paste everything below the line into a fresh Claude Code session in `E:\repos\TAOM`.

**Status (2026-11-24):** Mike chose the pilot before Phase 1 ran, so Gate 1 is decided: Rivendell, as a
siege set of three pieces (a solid wall, a broken wall and a gatehouse). Concepts are our own: Blender
blockouts beside the control piece, plus pictures from the image connector, which he allowed for this
work. **Every new texture is 1K (1024 px), never 2K or 4K** (Mike, 2026-11-24); this replaces "the
same texture size" in Phase 5 wherever the control piece uses a larger map. The work is on branch `feat/kingdom-kits`, with its files in
`E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell\`. The coverage review of the other seven
kingdoms (Phase 1 in full, `kingdom-coverage.md`) is still owed.

Written 2026-11-24 from the chariot session (Rhûn and Erebor war chariots), whose working method this prompt
carries over to buildings. Your first tracked edit, in your own branch, is to save this prompt as
`docs/kitbash/kingdom-kits-prompt.md` and add its row to `docs/kitbash/README.md`, as the Erebor and Gondor
build prompts are saved.

---

You will make settlement art for the kingdoms that have none of their own yet. We hold a large stock of good
3D meshes (walls, gates, towers, houses, props) in the Gondor and Erebor kitbashes and in other kits. Most
kingdoms can use those meshes with new textures. Rivendell also needs new geometry of its own. The maintainer
(Mike) gave the method in one line, and it is the order of your phases:

**Review what we have -> identify how it was made and for what -> make a new concept with the current piece as
the control group -> create the piece -> texture it.**

Kingdoms to serve: Rohan, Rhûn, the Orcs, the Goblins, Harad, Khand, Dale, and Rivendell (new geometry). Do not
start with all of them. One kingdom and a small set of pieces is the pilot (Gate 1).

The existing, approved pieces are your definition of "right". Every new piece is judged beside the piece it
came from, on measured numbers and in the same view. A new piece that is weaker than its control piece on any
measure is not done.

## Rules of this work (the repo rules in AGENTS.md and CLAUDE.md apply as always)

- Work in phases. Stop at every GATE and wait for Mike's word. Between gates, do not ask: decide, record the
  decision, go on.
- Every source FBX and texture under a module's `AssetSources` is read only. A changed or new source goes into
  `AssetSources` only at the hand-off step, as a new file or with a backup, and Mike does every Modding Kit
  import himself.
- `TAOM_Map` and `LOTRLOME_Armory` are live and not versioned. Anything you change there needs a spec or a
  check in the repo in the same change, or a reinstall removes it without a trace.
- All work files on `E:`. Use a work folder `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\<kingdom>\` and keep
  every version (`iterations\vNN`).
- Never guess what the engine does with a material, a mesh name or a scene entity. Read `docs/reference/engine/`,
  then the installed DLLs, and quote the line.
- Claim the work first: `python tools/work_claims.py claim kingdom-kits --note "<what>"`.
- Rules added during the pilot, 2026-11-25 and 2026-11-26 (each was held only in one session's notes before):
  - A gate is never loosened, a mesh never gets a hole to pass a clearance, and a mesh is never mended by hand: the
    cause is repaired in the generator. (The maintainer rejected a hole that met a clearance on a chariot armour,
    2026-11-25; the rest is the lead's rule.)
  - A look brief with two readings gets both built in round one (the lesson "AI judges are not the owner's eye" in
    `docs/reviews/lessons/build-tooling-workflow.md`). A critic's verdict of taste is input for the maintainer: it
    never removes or holds back a variant before he has seen it. (Two critics had called forms foreign; he wrote on
    2026-11-26: "EVerything you showed off in the rivendell review_now.html is amazing and should be built out.")
  - A kit can hold more than one type of a piece (the maintainer, 2026-11-26: "We can have more than one wall type
    is what I am saying! The approved one is A and now we can have B"). A form that he keeps becomes its own type.
  - The maintainer's reference pictures stay on this machine. They go to no outside service, and no file names a
    film, a studio or a person. (The lead's rule, 2026-11-26.)
  - Public text and a prompt for an outside service never name a bought source kit: say "a licensed elven building
    kit". (The instruction for issue #1600.)
  - A worker stops only a process that it started, and closes no Blender, Painter or Modding Kit window: the
    maintainer and other sessions use them.
  - A carved, organic or broken part gets a proposal for an outside generator, with its cost, before it is built by
    code ([tripo-cli.md](../features/tripo-cli.md)).

## Read first

- `docs/kitbash/README.md`: the kits we hold and what is catalogued. On 2026-11-24 it lists Erebor (145
  pieces, full catalog), Gondor (330 kit parts and 166 props, full catalog), the merged Erebor and Gondor
  buildings, the Lond Cirion walls and houses, and, not catalogued yet, Mirkwood, Mordor, Rivendell (458
  modular meshes converted from a bought kit) and the tents. It also holds the naming rules (`t_`, `m_`, `sm_`,
  `bo_`, the kit prefixes, the texture suffixes).
- `docs/kitbash/erebor/blender-build-prompt.md` and `docs/kitbash/gondor/blender-build-prompt.md`: the two
  earlier prompts of this kind. Their phases, gates and probe stack are your template for geometry work.
- `docs/kitbash/erebor/design-patterns.md` ("The frame rule", the join matrix), `docs/kitbash/erebor-buildings.md`
  ("Pipeline", "Export rules", "Verification stack"), `docs/kitbash/gondor-buildings.md`,
  `docs/kitbash/lond-cirion-walls.md` and `docs/kitbash/lond-cirion-buildings.md`.
- `docs/reference/ue-to-bannerlord-asset-pipeline.md`: how a bought kit became a Bannerlord kit, the single-prop
  path and the kit-composition path.
- The `/blender-asset` skill and `docs/features/blender-asset-gates.md`: the brief, the two gates (preflight,
  views) and the verdict form. `/prep-tripo-prop` owns the Tripo and Substance Painter path.
- `docs/reviews/lessons/build-tooling-workflow.md` and `docs/reviews/lessons/data-content-cultures.md`: list the
  headings and read the entries on catalogs, frames, unversioned modules and package guards.
- `.claude/rules/traps/creatures-assets.md` and `data.md` (read them by hand when you design).

## Phase 1: Review what we have

Produce a measured inventory, not a description from memory. Reuse the catalog tools before you write a new
one (`tools/oneoff/blender_catalog_erebor.py --kit erebor|gondor`, `erebor_catalog_to_md.py`, `kitbash_kits.py`,
`blender_catalog_parts.py`, `tools/blender/probe_fbx.py`).

Start from the disk, not from the README. On 2026-11-24 `TAOM_Map\AssetSources\Scenes\` holds the folders
`Decals`, `Gondor`, `Mirkwood`, `Mordor`, `Nature`, `Rohan`, `Skyboxes`, `comprehend` and `erebor`. So a Rohan
folder exists that the kit table does not list, and the Rivendell folder the table names is not there: find
out what the first holds and where the second went before you plan either kingdom.

1. Kits: for each kit under `TAOM_Map\AssetSources\Scenes\` and each vanilla building set a TAOM scene uses,
   the piece families, piece counts, LOD levels, collision bodies, materials, and texture sets with their sizes.
   Extend the catalogs to the kits that have none (Mirkwood, Mordor, Rivendell) only as far as this work needs.
2. Use: which pieces the scenes and prefabs place, and how often. Read `TAOM_Map\SceneObj\<scene>\` and
   `TAOM_Map\Prefabs\` (the four `mod_taom_<kit>_kitbash` scenes are the kits' own showcases). The Gondor
   catalog mined its joins from prefab placements; do the same count per kit.
3. Kingdoms: for each kingdom in the list, which settlements it has, which scene each one loads today, and
   which kit or vanilla set that scene is built from. Read the culture and settlement ids from the data
   (`docs/modding/id-cheatsheet.md`: a vanilla culture id is not the kingdom its name suggests).
4. The gap: per kingdom, what it borrows today, what looks wrong for it, and which of our kits is the nearest
   donor by construction (stone, timber, earth, tent) and by scale.

Output: `docs/kitbash/kingdom-coverage.md` (tables, with the command that prints each count or its date) and
one filterable HTML page for Mike with a picture of each candidate donor piece.

**GATE 1.** Recommend the pilot: one kingdom and three to six pieces (for example one wall section, one gate,
one tower, one house, one prop set), with the reason. Mike picks.

## Phase 2: Identify how each donor piece was made, and for what

For each pilot piece, write its record before you design anything. This record is the control group.

- Source: the FBX, its sub-objects, the frame rule of its kit (prove it for a kit that has no proof yet), pivot,
  size, snap grid and the joins it takes part in.
- Budget: triangles per LOD, the LOD chain, the collision body, measured with `probe_fbx.py` and
  `python tools/audit_fbx_lods.py`.
- Surface: each material, its shader and flags, its texture sets, texture size, whether the UVs are unique or
  tiling, the texel density (pixels per metre) on the main faces, and whether two materials share one set
  (the `_d2`, `_d3` families).
- Colour and wear: the mean and spread of the diffuse map on the main surfaces, how the specular map splits
  metal, stone and wood, how much dirt and edge wear it carries. Measure these from the files.
- Purpose: where the scenes use it, at what distance the player sees it, what stands next to it, what the
  siege or mission logic needs from it (ladders, gates, navmesh, barriers).
- How it binds: how a mesh names its material, and the routes by which the same geometry can show another
  material. Find out with evidence which of these the engine and the Kit support, and what each costs:
  a new FBX with the same geometry and new material names (a Kit import, new packages); a metamesh clone
  bound to another material (`tools/tpac_clone_metamesh.py`); a material set on the scene entity or the prefab.
  State the cost of each in packages, memory and scene edits (`tools/audit_map_scene_memory.py`,
  `tools/check_prefab_budget.py`).

**GATE 2.** Show the records and your recommended retexture route. Mike decides the route, because it sets
how every later kingdom is made.

## Phase 3: Concept, with the current piece as the control

1. A style sheet per kingdom, one page: construction (what it is built of and how), a palette measured from
   that kingdom's approved art in the modules (armour, shields, banners, any scene it has), the materials and
   their wear, three to five signature features, and what it must never look like. Cite the source of each
   colour.
2. For each pilot piece, render the donor from fixed cameras. Then make three or four concepts of the same
   piece in the kingdom's style. A retexture concept keeps the donor's silhouette, openings and joins exactly.
   A new-geometry concept (Rivendell, or a kingdom feature the donor cannot carry, such as a roof form or a
   gate crest) keeps the kit's grid and joins.
3. Concept pictures may come from the image connector only when Mike has said so in your session; it spends
   his credits. Keep the prompts generic: no project name, no person's name. A concept picture is a target for
   our own design, never a source to copy part for part, and no picture of another artist's model is traced.
4. Put the control render beside each concept, at the same size, and say for each concept which donor pieces
   and which new texture sets it needs.

**GATE 3.** Mike picks the concepts and marks what to change. Expect corrections on the pictures; apply them
to the brief before any build.

## Phase 4: Create the piece

- Brief each piece as `/blender-asset` section 3 asks: class, final names (checked free with
  `python tools/validate_mesh_refs.py --check-name <name>`), the triangle budget taken from the control piece,
  LODs, body, texture sets.
- A retexture piece changes no vertex. Prove it: compare vertex and triangle counts and a position hash with
  the donor at every LOD.
- New geometry follows the order of `/blender-asset`: final topology, then UVs, then LODs and the `bo_` body.
  Build in three passes (silhouette and proportions, secondary forms, small detail) and compare with the
  concept after each pass: at most five differences, largest first, at most three compare passes.
- Long Blender builds run well in a background agent with a full brief and one report per piece. You run the
  gates yourself and look at the renders yourself; an agent's "done" is a claim.
- A handover for a kit session carries the scene research itself, not a pointer to a build record: what each
  scene needs (walls, the broken wall, towers, a gatehouse for two gates, the gates, the siege placement for the
  defenders) with the measured numbers. The reference is `docs/kitbash/settlement-scene-needs.md`; a handover names
  it in its read-first list and holds its summary table. A session that measures a new scene fact adds it there
  (the maintainer, 2026-11-26: "So that the next session doesnt have to look all of that up!").
- The agent for kit builds and for judgements is `kitbash-architect` (`.claude/agents/kitbash-architect.md`, written
  2026-11-26 on the maintainer's request): it holds the role (a designer outside and inside, an architect and an
  engineer), the order of work, the limits and the report form. Brief it with the task. A session that does not list
  it yet names the file in the brief as the worker's role.
- Blender runs headless through the launcher, each script writes a report file, and you wait for that file.
  Try the Blender MCP once; when it does not answer, go on headless.

## Phase 5: Texture

- Decide per piece whether it sits on an existing set (the cheapest result: a new piece mapped onto a kit's
  own tiling or trim textures needs no new texture at all), on a recoloured or re-weathered variant of the
  donor's set, or on a new set. A new set must earn its memory.
- Match the control: the same texture size, the same texel density, the same map conventions (`_d`, `_n`, `_s`
  as the kit writes them; check the normal map's green sense and the specular channels against a kit texture
  before you write one).
- Take every base colour from a measured reference, and write the measurement in the brief.
- Make the maps in Blender (procedural layers and bakes) or in Substance Painter through its MCP, whichever
  the piece needs; `/prep-tripo-prop` documents the Painter path.
- Judge colour and material in the Modding Kit beside the control piece, under the same atmosphere. A Blender
  render misleads on tone: in the chariot session a gold that matched in the Kit looked too dark in Blender,
  and the "fix" made it wrong in game.
- Never write a texture package by tool. A texture reaches the game through a real Kit import. In this module
  a cooked texture item that finds a source file on disk crashed the Kit three times.

## Gates for every piece, before hand-off

1. `python tools/audit_fbx_lods.py --fbx <fbx>` and `tools/blender/mesh_preflight.py`: read the verdict and
   every finding. "Seen only from behind" can be a false alarm on stacked or thin parts: look at the culled
   views before you flip a face, and never flip on a claim alone.
2. `tools/blender/qa_views.py`: read all views, at LOD0 and at two lower LODs.
3. The control comparison: budget, texel density and texture memory against the control piece, and one sheet
   with the control and the new piece from the same cameras.
4. The names are free, lower case, and follow the kit rules.
5. Show Mike the piece in a Blender window he can orbit, with its textures on, not only as pictures. The
   chariot session's helper is `E:\LOTRAOMAssets\_auto_workspace\chariot_artem\view_gui.py`, with
   `capture_blender_windows.ps1` as the proof that the window shows what you think; copy what you need. A
   freshly loaded image reports no data until it is drawn, so do not remove an image node for that.

## Phase 6: Hand-off, wiring and proof

1. On Mike's word, copy the sources into `AssetSources` under the kingdom's folder (new files, hashes compared
   after the copy). He imports them in the Kit.
2. After his import: `python tools/check_rdc_entries.py --module "<game>\Modules\TAOM_Map" --under <folder>`
   must show no package without a cache entry. A scene or prefab that names a mesh with no cooked package can
   hang a load, so wire nothing before this passes.
3. Wire the pilot: a small test prefab or a copy of one scene area, never a mass replace in a live scene.
   Check the prefab entity budget.
4. Mike looks at it in the editor and in game, beside the control piece.
5. Record: the catalog rows, a section in `docs/kitbash/`, the lesson entries, the in-game checks that are
   still owed, and the repo-held spec or check for every live file you changed.

**GATE 4.** With the pilot approved, propose the order of the remaining kingdoms and what the pilot showed
about cost per piece. Rivendell's new geometry gets its own plan at this gate.

## What to report at each gate

The decision you need, your recommendation and its reason, the evidence beside each claim (the command and
its output, or the file and line), what you did not check, and the paths of the pictures and Blender scenes.

A page that you open for the owner to review holds two things only: what changed (a picture, the fault, the repair,
what he will see) and what you need from him (the question, options with letters, your recommendation). Points that
you keep as they are go in one short block at the end. Job status, test counts and history stay in the record
(Mike, 2026-11-25: "just provide me the html with the changes/what you need not all of the fluff").
