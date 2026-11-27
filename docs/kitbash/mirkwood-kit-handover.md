# Mirkwood kit: handover from the Rivendell kit sessions

Written 2026-11-26 by the lead session of the Rivendell kit (kingdom kits job, issue #1600), on the maintainer's
request. It shares its general sections with the Lothlórien handover of the same day, which three agents checked
(paths, quotes and facts; a fresh session's first day; what was missing). Paste everything below the line into a
fresh Claude Code session that is started in `E:\repos\taom-kingdom-kits` (branch `feat/kingdom-kits`). Start it
there and not in `E:\repos\TAOM`: the kit docs, the kit standard and the `kitbash-architect` agent exist only on this
branch, and nothing of it is committed yet.

**Version 2, 17:18.** Three agents checked the first version (reports in
`E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\_shared\handover_check_mirkwood`), and I verified their main counts in the
live module. A session that started from the first version reads again: section 2 (whole), the first paragraph
and rows 1 and 3 of section 5, the last four bullets of section 7, sections 9 and 10, and section 11.

**The maintainer's reference pictures are on disk:** nine pictures, `ref_01.png` to `ref_09.png`, in
`E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\mirkwood\refs\owner_2026-11-26\`. He saved them in a folder of his own on
2026-11-26 and the Rivendell lead copied them there; `SOURCE.txt` lists his file names, and a `manifest.json` lies
beside it. Section 2 says what each one shows.

---

You improve the settlement kit of Mirkwood for TAOM. The maintainer (Mike, the owner of every look decision) wrote on
2026-11-26: "Give me a complete handover for another session to do mirkwood, yes we have SOME stuff for mirkwood but
we need to make it better and iterate on it based on references."

So the job has three parts, in this order: a measured review of what we hold for Mirkwood (section 2), his
references, and then rounds of improvement that he judges by eye. The method and the tools are those of the
Rivendell kit. This document tells you what exists, how he works, what went wrong before, and what to do first. It
grants you nothing: every permission of section 11 is yours to get from him.

## 1. Read first, in this order

1. `.claude/agents/kitbash-architect.md`: the role for every builder and judge of kit work. You are the lead: you
   brief that agent, look at every result yourself, decide each finding, and talk to the owner. Its third version had
   no trial. In its second trial no builder plan named a generator or a test, none counted placements, and the scene
   plan measured no control: ask for those three in every brief and check them in the plan. If your session does not
   list the agent, name the file in the brief as the worker's role.
2. `docs/kitbash/kingdom-kits-prompt.md`: the "Rules of this work" and all six phases. Its Phase 1 (review what
   we have) and Phase 2 (identify how each piece was made, and for what) fit a kit that exists: they come first,
   and the pieces that exist are your control group. The prompt's own status line says that Gate 1 was decided
   as Rivendell; the maintainer's request of today answers Gate 1 for your session (Mirkwood). Its Gate 2 (the
   route) is his decision after your review. Its sentence that the image connector (the image generator of this
   machine's tools; this document calls it the image service) was allowed held for the Rivendell session only.
3. `docs/kitbash/kingdom-kit-layout.md`: where a kingdom's kit lives in the map module, the file names, rules 1 to 10.
   It says that the older kits, Mirkwood among them, keep their folders, because their imported assets depend on
   those paths. So nothing that exists under `Scenes\Mirkwood` is moved or renamed. New and improved pieces take
   the standard's names and subfolders beside the old file. Test every new mesh, material and texture name as an
   exact name against the 136 mesh names, the 31 Kit materials, the 32 material names of the FBX and the 85 maps
   (section 2), and against the whole module: a material binds by name module-wide. Rule 9's names for the prefab
   file and the showcase scene are taken by the old files (section 9).
4. `docs/kitbash/rivendell-siege-set.md`: the build record of Rivendell, about 400 KB. Never read it whole. Run
   `grep -n '^## ' docs/kitbash/rivendell-siege-set.md` and read these sections of 2026-11-26: the maintainer's
   directions (14:47, and 14:55 to 15:00), "One asset a piece", the marks on the centre house, "The valley scene,
   round 1", "The prefab file is in the map module"; and of 2026-11-25 "The machines' heights, measured". Its facts on the Modding
   Kit since 2026-11-26 are newer than the header of the texture doc.
5. `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell\kit_full\design_v2\RIVENDELL_DESIGN_LANGUAGE_V2.md` with
   `LANGUAGE_AMENDMENT_1.md`: a design language written from his pictures. Yours has the same form, not the same
   content.
6. `docs/kitbash/tiling-texture-sets.md` (how the 1024 px sets were made, what the engine reads from them, its table of
   Painter traps, and at its end the rules for a band on a curved path), then
   `docs/reference/engine/static-mesh-lod-selection.md` and `castle-gates.md`.
7. The kit lessons: `grep -n '^### ' docs/reviews/lessons/build-tooling-workflow.md | grep -i '#1600\|kit'`, then read
   those entries.
8. The game data. `docs/modding/id-cheatsheet.md` gives the ids: culture `mirkwood` (Silvan Elves), kingdom
   `mirkwood` (its name in the game is Lasgalen), the bandit culture `mirkwood_stalkers`, and the settlement prefix
   `M`, which is Mirkwood's alone (the LORD prefix `M` is the one that Mirkwood shares with Mordor). The
   settlements are in the live `Modules\TAOM_Map\ModuleData\settlements.xml` (read only): 24 rows carry
   `Culture.mirkwood`. Section 2 says which scene each kind loads.
9. The owner's standing preferences: the memory folder `C:\Users\mikew\.claude\projects\e--repos-TAOM\memory` (your
   session may not load it by itself, because it starts in another folder). Read `MEMORY.md` and these files:
   `feedback_precision_and_creativity.md`, `feedback_show_look_variants_early.md`, `feedback_asset_art_rules.md` (its
   block on built architecture is his checklist for exactly these pieces), `feedback_one_asset_a_piece.md`,
   `feedback_long_term_structure.md`, `feedback_lean_review_page.md`, `feedback_show_meshes_in_blender.md`,
   `feedback_learn_from_reference_tools.md`, `feedback_one_piece_one_blender.md`, `feedback_update_docs_as_you_go.md`,
   `feedback_everything_on_e_drive.md`, and the card `features/kingdom-kits.md`.
10. `docs/kitbash/settlement-scene-needs.md`: what each scene of a settlement needs from a kit (the solid wall, the
   broken wall, the towers, a gatehouse for two gates, the gates, the siege placement of the defenders and the
   attackers' side, the other scenes that a settlement loads), with the measured numbers, the entities, scripts and
   tags, and what each kingdom loads today. It was written so that you look none of this up again: read it before
   you size a wall, a tower or a gatehouse, and add what you measure to it. Its long tables are in
   `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\_shared\scene_needs`.
11. `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell\rivendell_reference.html`: the Rivendell kit as a worked example, on one page with the real pictures of each
   step (his picture read as built things, the massing variants, his marks, the fine details, the siege pieces,
   the broken wall, what an independent critic caught, the trim sheet). The maintainer called it a great
   reference: open it before you plan.

## 2. What we hold for Mirkwood, and his references

**Measured by me on 2026-11-26 in the live map module (read only). Check each number again before you use it.**

| What | Where | Fact |
|---|---|---|
| The source mesh | `Modules\TAOM_Map\AssetSources\Scenes\Mirkwood\kit_mirkwood_mod_env.fbx` | One FBX of 22.9 MB, last changed 2026-09-11. By a read of its names (the check; not an import): 760 objects; 136 meshes, each stored only as `<name>.lodN` (every one has `.lod0`, none has a bare base name), with irregular levels (`.lod1` 24, `.lod2` 116, `.lod3` 73, `.lod4` 117, `.lod5` 49, `.lod6` 110); 135 `bo_` bodies; no sub-objects; 32 material names (28 `t_mirkwood_*`, one decal material, and the body materials `stone`, `wood`, `metal`); a texture file is named for only 3 of them |
| The source textures | the `textures` folder beside it | 85 PNG files, every one 2048 x 2048 today: 30 `_d`, 26 `_n`, 28 `_s`, 1 `_d2`. Six vine maps were 4096 px until a downsizing pass of 2026-09-13; their originals are in `E:\taom-texture-backup-2026-09-13\TAOM_Map\AssetSources\Scenes\Mirkwood\textures`. Five sets carry the word `unique` (maps with their own UV: check) |
| The imported packages | `Modules\TAOM_Map\Assets\Scenes\Mirkwood` | 117 packages: ONE mesh package `kit_mirkwood_mod_env_geo.tpac` for all 136 meshes, 85 texture packages and 31 material packages |
| The prefab file | `Modules\TAOM_Map\Prefabs\taom_mirkwood_kitbash.xml` | 273 entity tags: 136 one-piece prefabs at the origin (the entity name is the mesh name, the body is `bo_` plus that name) and ONE layout prefab `a_taom_mirkwood_complete_set` whose 136 children lay every piece on a grid. It holds no authored join. 108 mesh names start with `st_mirkwood`, 28 with `pr_mirkwood` (a statue, an entrance, 24 vines in clean, mossy and rotten looks, a hanging lamp, a wall lamp). No entity carries a `body_flag`, so no stair or bridge has `has_steps`. One body is a borrow (`st_mirkwood_arch_a02_centerpiece_column_curved` takes the body of the `_dirty` column) |
| The showcase scene | `Modules\TAOM_Map\SceneObj\mod_taom_mirkwood_kitbash` | exists; it places the layout prefab once and names no mesh |
| Scenes that name a Mirkwood mesh | a search of every `SceneObj\*\scene.xscene` for `st_mirkwood` and `pr_mirkwood` | Ten. Counts of the two names: a Mordor town 1,440 and 13; the Minas Morgul keep 745; Minas Tirith 185 and 2; Pelargir 185; the main map 102; a Dale castle 84; the two Moria scenes 55 and 4, and 0 and 21; a second Mordor town 0 and 9; the Minas Morgul arena 0 and 2. No scene of a Mirkwood settlement is among them |
| Scenes and prefab files that set a Mirkwood MATERIAL on a mesh of another kit | `grep -c 'material="t_mirkwood'` over every `scene.xscene` and over `Prefabs\*.xml` (verified by me) | 3,069 overrides in nine scenes; two of them name no Mirkwood mesh (a dwarf town 1,449; the Minas Morgul tavern 72). Four other prefab files hold 5,829 more. So twelve scenes and four prefab packs depend on this kit |
| What the 24 Mirkwood settlements load | the live `settlements.xml` (verified by me) | Vanilla Battania scenes only: 2 towns on `battania_town_b` and `battania_town_c`, 5 castles on `battania_castle_a` and `battania_castle_b`, 17 villages on `battania_village` and its variants. The Rivendell castles load the same two castle scenes (`docs/kitbash/kingdom-coverage.md`) |
| Older copies of the kit | found by the check; listed, not opened | (i) The assets repo `E:\repos\lotraom-assets\v1.5\TAOM_Map\` holds the FBX (its hash equals the live file, by the check), the mesh package and the prefab file: a mirror that a session never edits. (ii) `E:\LOTRAOMAssets\Elves\Mirkwood_Kitbash\` holds an older FBX of 2026-02-10 and 82 maps. (iii) A sub-folder of `E:\LOTRAOMAssets\Elves\Mirkwood\` holds the first delivery of 2025-09-30: a `.blend` of 288 MB, an archive `SP.rar` (by its name a Painter archive) and a combined FBX |
| A baseline of today | `kingdom_kits\mirkwood\kit_full\baseline_01` and `kingdom_kits\mirkwood\_tools` | `geometry.json`, `installed.json`, `reference_sheet.jpg` and catalog tools, written from 17:05 on 2026-11-26. The Rivendell lead did not write them. Find out who did (a Mirkwood session that he started before you, most likely) and whether a checker saw them, before you write beside them |
| The repo's record | `docs/kitbash/README.md`, `docs/kitbash/kingdom-coverage.md` | "Not catalogued yet". No design language, no build record, no control records |

What follows from the table, before any design:

- **Twelve scenes and four prefab packs of other kingdoms depend on this kit,** by its meshes or by its materials.
  A re-import of the old FBX writes all 136 meshes again for every one of them, and the Kit binds a material by
  name module-wide and resets a binding set by hand on each re-import (trap sheet
  `.claude/rules/traps/creatures-assets.md`, "Kit FBX materials"). So: never add a piece to the old FBX; never change
  an old map or material in place; build an improved piece under a new name in a new kit file, as a further type
  beside the old one, unless he says that the old one is replaced everywhere. A new material that is named like an
  old one binds to the old one with no warning.
- **Mirkwood's own settlements show vanilla Battania scenes.** A Mirkwood settlement scene is a new scene, and its use
  is an edit of a `scene_name` in the live `settlements.xml` (his word). A change to one of the vanilla scenes would
  show in Rivendell and in every other kingdom that loads it. Agree with the Rivendell session before either of you
  plans a castle scene.
- **Where the kit came from is in no repo record** (`docs/reference/provenance-register.md` has no Mirkwood line). The
  older copies in the table answer the prompt's "how it was made": read the first delivery's files before you
  rebuild a source. If it is a bought kit, public text and outside prompts never name it, and its licence decides
  what may be changed and shared: ask him.
- **Three routes give an old form a new look** (the prompt's Phase 2, "How it binds"). Cost each one in packages,
  memory and scene edits before Gate 2. (a) A new FBX with the same geometry and new material names: a Kit import,
  new mesh names and packages, and no scene changes until a scene places it. (b) A clone of a mesh that binds
  another material, `tools/tpac_clone_metamesh.py`: it writes a new package, and where `TAOM_Map` would load such
  a package is UNVERIFIED. (c) A material set on the entity in a scene or a prefab: TAOM's scenes do this already
  (the 3,069 overrides), per sub-mesh and per level; it needs no new mesh, but every placement names every level.
- **A retexture changes no vertex.** Prove it with vertex and triangle counts and a hash of the positions against the
  donor at every level. Match the control's texel density and map conventions (the check read the kit's `_s` maps as
  R metallic, G gloss, B occlusion: verify it), and check the green sense of a normal map against a kit map. The
  cheapest new piece sits on the kit's own tiling and trim sets. Judge colour in the Kit beside the control.
- **Before a texture source is overwritten,** its original goes to the backup tree on `E:`, file by file (his rule:
  the memory file `feedback_texture_backups_on_e_drive.md`). 69 of the 85 maps are 16-bit PNG (by the check).
- **The palette also comes from Mirkwood's approved art** (the prompt's Phase 3): the Armory sources
  `Modules\LOTRLOME_Armory\AssetSources\mirkwood_assets`, the map icons
  `Modules\TAOM_Map\AssetSources\Campaign Map\Map Icons\Mirkwood`, and the Painter projects of the Mirkwood armour in
  `E:\LOTRAOMAssets\Elves\Mirkwood_SPPs`. Name the source of each colour. All of these are read only.

**His references: nine pictures in the folder named above.** They stay on this machine and go to no outside
service; no file names a film, a studio or a person, also where a picture carries a title or a mark. What I saw in
them (my reading, for you to check against the pictures):

1. `ref_01`: a gate front in a rock face. Tall slim columns whose shafts split into interlaced ribs that flare at
   the foot and at the head; arches between them; tall doors in teal; a stone causeway that leads to the doors.
2. `ref_02`: a vast hall under ground, dark, with warm lamps. Columns like tree trunks, pointed vaults, and a walk
   with no rail that winds up to a platform in the middle; lit arched chambers at the sides.
3. `ref_03`: the gate porch in daylight, as a render. Pale stone columns with a diamond lattice on the capitals and
   on the arch soffits, round arches in two rows, teal doors behind, a causeway whose side steps in waves.
4. `ref_04`: inside. A bridge that looks grown from roots crosses a chasm with waterfalls; a column of interlaced
   ribs at the left; lamps; a file of figures on the bridge gives the scale.
5. `ref_05`: outside. A stone bridge of one arch over a river and a fall leads to the gate porch in the cliff;
   huge twisted trees in autumn colours.
6. `ref_06`: five concept strips. Two of twisted giant trees (nature), two of the bridge with the gate (one pale,
   one teal), one of a hall with a walk on a root.
7. `ref_07`: the throne. A platform on a cluster of columns, a stair that winds up to it, a seat under great
   antlers, vault ribs like branches, a hanging lamp.
8. `ref_08`: the gate porch at sunset, as a render. A row of columns (count them) with carved lattice capitals, round arches with
   carved soffits, doors with a leaf pattern in teal and orange, a causeway; a great tree with roots at the left.
9. `ref_09`: a wide view inside. Root bridges with interlace on their arches, stairs cut in the rock, columns as
   carved trunks with interlaced collars, hanging amber lamps, waterfalls, and a bright hall of columns above.

What they share: a realm in a cliff and under ground. Outside there is one bridge and one gate porch in a rock
face. Inside there are caverns with columns like trunks whose ribs interlace, bridges and walks that look grown and
have no rails, stairs cut in rock, hanging amber lamps, and waterfalls. So this kit is two things: a gate front with
its bridge, and the pieces of great halls under ground, which is interior design at the scale of a town. Compare
that with the names in the table above (an entrance, a bridge with stairs, a bridge with a tree, vines, a statue, a
hanging lamp): the kit that exists aims at the same place. Your review shows how far it gets.

**A reference is a target for our own design, never a source to copy part for part.** `ref_03` and `ref_08` are
renders of a model that another artist made, and `ref_06` holds concept strips. No picture of another artist's
model is traced, and a part that comes too close to a reference model or to a bought model goes (the prompt's
Phase 3; `feedback_asset_art_rules.md`). The pictures are small screen captures (301 to 1,173 px wide, by the
check); each has a viewer button in a corner and one has a signature mark: cut those away before you measure a
colour, and copy no mark into a file.

## 3. How the owner works (each line cost us rounds)

- **He judges by eye, in a minute.** Show true geometry early: a sheet from fixed cameras, and a Blender window that
  he can orbit. He answers with loops and arrows drawn on your sheet. Those marks are the next brief.
- **Precision and creativity in balance.** "When making buildings you are very precise. 0 or 1. Black or white. I
  think that is limiting your creativity ... The balance is key! ... Try and be more creative." Craft is exact, design
  is free. Our first houses came from one rule set and he found them rigid.
- **Show every form; rank none.** Two AI critics ranked our forms and called some foreign. He had seen the sheets and
  wrote: "EVerything you showed off in the rivendell review_now.html is amazing and should be built out." A critic's
  craft finding is acted on. A critic's verdict of taste is input for him and never removes a variant.
- **More than one type of a piece is welcome.** "We can have more than one wall type is what I am saying! The approved
  one is A and now we can have B".
- **A look brief with two readings: build both in round one.** We spent five texture rounds on one reading of "smooth
  stone" that he did not hold.
- **The whole scene, cut into pieces.** "The goal here is to create the whole scene and be able to cut it up to be
  used elsewhere" and "We dont need the nature (trees, water, etc) We have that in the editor".
- **One asset a piece.** "These should be made all together, no one is going to need stair railings. So stairs come
  with the railings." (layout rule 10).
- **Final names and folders from the first build.** "Organize it for the long term project we are doing."
- **A short review page.** Only what changed and what you need from him: "just provide me the html with the
  changes/what you need not all of the fluff".
- **Records after every result.** "Update all documentation throughout the process as you never know when you will
  run out of usage."
- **He reads his reference closely.** He corrected a model where I had read stair flights as a carved band, asked for
  the carved design of a frame board, and for the tracery of a screen in place of plain arches. Read every part of a
  picture as something that is built and used, before you model it.
- **His checklist for built pieces** is the architecture block of `feedback_asset_art_rules.md`. The rules that such a kit
  meets first: a floor that troops walk on is one flush surface in mesh and body; every walk has a way up; a
  rail run ends at one post; a band is one continuous piece; a pattern is never stretched to a curved stop; curves and
  mirror pairs are perfect; a carrier is slim; no props that the editor places.
- **Say your reading and act; he corrects in a minute.** A number that you give him comes from a trial, not from an
  estimate. When he praises a set, change only what he marked.

## 4. The method that he confirmed on 2026-11-26

1. **A design language from his pictures**: the forms, members, materials and a colour table with measured values;
   what is exact and what is free; what the pictures have and what they never have.
2. **Concept pictures from plain words**, only with his word for the image service; generic prompts; never his
   pictures as input.
3. **Massing variants as true geometry**, then **the fine details beside his marks**, then **the build**, the kit file,
   his import and the look in the game: steps 4 to 7 of "How you work" in the agent file.

A step is one run with one builder (the `kitbash-architect` agent) and one independent checker beside his pictures,
with no repair pass inside the run: you read both results, look at the pictures yourself, verify the claims that your
next step rests on, decide each finding, and record it. Then you show him. In this job such a run was a script of the
Workflow tool (the `wf_` ids in the record). That tool needs the owner's word for your session ("ultracode", or his
own words for many agents). How to run one safely:

- Every stage writes its result file as its last act; a stage that returns nothing ends the script.
- Every brief names the worker's own scratch folder (workers that shared one overwrote each other's scripts) and the
  docs and trap tables of its area.
- The harness shows workflow agents the owner's newest message. A run whose task is something else needs one
  paragraph that says why the task is theirs, or the agents decline it (this happened on 2026-11-26).
- Never send a message to a running workflow agent: a second copy starts beside it.
- The weekly usage limit stopped every worker twice in this job, and both kit sessions draw on it.
- Memory: no full distance matrix between point sets; few heavy jobs at one time. Three measure jobs once grew to
  71 GB on this 62 GB machine.

## 5. What exists to reuse

Work folders are under `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\`. `rivendell` is the first kit. `mirkwood`
exists beside it: `refs` holds his pictures, and `kit_full\baseline_01` and `_tools` hold the baseline of section 2.
Add a state file. Copy a tool into your `_tools` folder before you change it (a repo tool too: the Rivendell job
uses those files); call in place only what this table marks so.

| What | Where | Use for Mirkwood |
|---|---|---|
| The catalog tools for a kit that exists | `tools/oneoff/blender_catalog_erebor.py --kit <kit> --folder <folder>`, `erebor_catalog_to_md.py --kit <kit>`, `kitbash_kits.py`, `tools/blender/probe_fbx.py -- <in.fbx> <out.json>`, `python tools/audit_fbx_lods.py --fbx <fbx>` (the prompt's Phase 1) | COPY into your `_tools` and add a `mirkwood` row to the copy of `kitbash_kits.KITS` (it holds only `erebor` and `gondor`; `mirkwood\_tools` has such a copy already). Check that the grouping takes `.lod0` as the base of a mesh that has no bare name. `blender_catalog_parts.py` takes no `--kit`: it reads the Gondor file and writes to a fixed Gondor folder, so copy and repoint it before any use. The Gondor and Erebor catalogs (`docs/kitbash/gondor/`, `erebor/`) show the form of the result |
| The Blender lock tool (eight slots, a memory ceiling) | `rivendell\_tools\blender_one.py` | CALL IN PLACE for every headless Blender job, so that both jobs share one limit. A window for the owner is the one other way to start Blender (below) |
| Base methods of the design language: arch outlines, the free rib, the plate (K1 to K3 in the record), and from round 2 a turned solid and placement along a walk | `rivendell\kit_full\v2_base` (`CHECK.md`, the interface files `K1_INTERFACE.md`, `K2_K3_INTERFACE.md`, `K4_INTERFACE.md`; round 2's check was in work on 2026-11-26: the folder `check_r2` and the files `r2_*_result.json`) | COPY when round 2 is checked, and repoint the copy: the tools put `rivendell\kit_full\src_m\tools\oneoff` on the path and import five modules from it (`rivendell_member_mesh`, `rivendell_joint_profile`, `rivendell_path_stations`, `rivendell_rib`, `rivendell_kit_paths`), and `rivendell_kit_paths` points to the Rivendell castle wall code by default. A copy that is not repointed runs on Rivendell's code. Your members are your own |
| The kit tree: piece modules, the build driver with lower levels of detail and collision bodies, and three checks (the probe: rays against a built piece; G4b: the mesh; G6b: the levels and bodies) | `rivendell\kit_full\src_k`; its state is `rivendell\kit_full\step2g\LEAD_DECISIONS_2G.md` | The pattern of a piece module and its checks. It was not green on 2026-11-26: read the decisions before you copy |
| The kit set tools: write a kit file (one FBX for a family of pieces), the manifest, the copy into the module with hash checks and an undo list, the check after his import | `rivendell\kit_full\editor_set\tools` | COPY AND CHANGE. They are wired to Rivendell (`KINGDOM = "rivendell"` in `kit_set_rules.py`, a default of `Scenes/Rivendell`, a wall staging argument). Run as they are, a copy without `--under` aims at the Rivendell folder of the module. Their replace mode knows "our own unchanged file" by a hash list of an earlier staging: no old Mirkwood file is in such a list, and none may get into one |
| The prefab generator and its owner steps | `rivendell\kit_full\delivery` (`PREFAB_OWNER_STEPS.md`) | COPY AND CHANGE; read the steps first |
| The Modding Kit import plan (the FBX dialog, the materials that the owner sets by hand, the checks) | `rivendell\build\v07\kit_import_prep\KIT_IMPORT_PLAN.md`, `KIT_SET_IMPORT_STEPS.md` | Read before the first import |
| The texture method (Substance 3D materials in Painter, the converter, the trim sheet) | `docs/kitbash/tiling-texture-sets.md`; `rivendell\painter_lab` | Pale carved stone, rock, root and bark, the teal of the doors, and the lamps' metal (his pictures). Painter is one shared instance: claim `painter-instance` first |
| The massing library and the sheet makers | `rivendell\kit_full\design_v2\massing\_scratch\mlib.py` and the `_scratch` folder of each massing round | COPY: the fastest way to a first sheet |
| The review page maker | `kingdom_kits\_shared\review_page\page_maker.py <HH:MM> <content json> <your work folder>`, with an example content file | One page for him: what changed, what you need. It writes `review_now.html` into the work folder that you name |
| A window for him | Copy the `.blend` to a `view` folder, start `blender-launcher.exe "<file>"` (under `%LOCALAPPDATA%\Microsoft\WindowsApps`), prove the window with `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\_shared\tools\capture_blender_windows.ps1 <folder>` | Tell him to press Home if the saved view is close. The window is his from then on |
| The Tripo CLI (he calls it "tripoli") for carved, organic and broken parts | `docs/features/tripo-cli.md` | Section 6 says when and how |
| How to take a reference result apart | `docs/kitbash/teardowns/` (four records); the procedure is the `/teardown` skill, on local trunk since 2026-11-26 (`E:\repos\TAOM\.claude\skills\teardown\SKILL.md`, not pushed). A session in this worktree does not list it until the branch has merged trunk: read the file | A part that a checker or he rejects twice gets a reference and a teardown before the next round |

Not for reuse: the forms of the Rivendell design language, the castle wall code with its siege wiring, the motif data
of the Rivendell interlace railing (it comes from the licensed elven building kit: a Mirkwood railing gets motif data of its
own, in its own folder), and the decisions of the Rivendell record. Those carry ids such as P, LD, WB, CH, SC, KS, MR,
PD, VB, H, and K1 to K9 of `step2d\LEAD_DECISIONS_2D.md`; they answer Rivendell's pictures and trees. Whether the
licensed kit's raw export is a donor of forms for Mirkwood is the owner's decision; do not use it on your own.

## 6. Tripo (he calls it "tripoli"): when and how

Tripo is a service that makes a textured 3D model from one picture, from several views or from a text. Its
command-line tool `tripo` is installed and logged in on this machine. The owner on 2026-11-25: "We also have more
tripoli we can do, keep that in mind", and on the credits of the kingdom kits job: "dont worry abotu credits, we have
plenty". On 2026-11-26 he asked that this handover says how to use it (he asked that for the Lothlórien handover; it holds here too). The full record is
`docs/features/tripo-cli.md` (the setup, the rules, every command, every job with its cost, every lesson): read it
before your first run, and add a row to its "Jobs" table and a line to its lessons after every run.

**The rule of the job:** a carved, organic or broken part gets a Tripo proposal, with its cost, before it is built by
code. Our code-built rubble and fallen blocks read as boxes; the Tripo ones did not.

| Use it for | Do not use it for |
|---|---|
| Organic skins and growth: bark, roots, a fork of a bough, burls, a vine that wraps a column | A platform, a stair, a rail, a bridge, a house: anything with an exact joint or an exact floor, door or step size |
| One-off carved parts: a pendant, a lantern body, a finial, a leaf cluster, a statue | A part that must be an exact mirror pair |
| A relief or a tiling texture taken from a model (below) | A part that sits on a tiling texture set |

The reason for the right column: a generated mesh has no exact dimension and no shared end section, and its flat
faces do not stay flat when it is reduced. The pattern that worked: **the exact skeleton by code, the organic skin
from Tripo.** For a root that carries a walk, or a column that is a living trunk, that means an exact code-built
core with its joints, and the bark and root relief taken from a Tripo model as a height field.

**The steps.**

1. **The picture.** One object, centred, with a margin; a plain mid-grey background; a three-quarter view from a
   little above (straight from the front for a relief such as bark); even soft light; no shadow, no figure, no
   plants, no text. Make it with the image service (his word first) or as a render of your own massing. His reference
   pictures never go to Tripo. The prompt and every file name are generic: no project, place or person name.
2. **The folder.** `kingdom_kits\mirkwood\tripo\refs` for the pictures and `kingdom_kits\mirkwood\tripo\out` for the results. Give every run an
   explicit `-o`; nothing from Tripo goes into a module without his word.
3. **Every command** starts from `export TRIPO_ANALYTICS_ENABLED=0 TRIPO_CLIENT_APP=claude`. Once a session read the
   tool's own guide (`tripo docs --llm`) and the balance (`tripo balance --json`). The key is in the tool's own config
   file: never ask for it, never print it.
4. **The dry run:** `tripo make ./ref.png --dry-run --json` prints the plan and an estimate with no network call and
   no credits. Tell him the cost. The first job cost 60 credits (a detailed model with PBR maps), as its dry run said.
5. **The run:** `tripo make ./ref.png -o <out folder> --json --yes`, with the model and the parameters of a job that
   worked: the "Jobs" table of the record holds the full command of each. It blocks until the files are on disk:
   give it 15 minutes or more, write no polling loop, read the one JSON line. Two to four views of one object go in
   one call.
6. **Look at `preview.png`** before the next step. Check the download: bytes 8 to 12 of a GLB hold its declared
   length, and a file that is shorter was cut off on the server.
7. **When it fails:** exit code 4 means no credits: stop and tell him, because a top-up is a purchase and his step.
   Exit code 6 with a timeout: the task still runs and is billed; fetch it with `tripo task watch <id> --download`,
   never start a new run. Exit code 7: read the balance before you run again. A second try is a new paid task.
8. **From a model to an asset.** A Tripo model is a start, not an asset: it has no fixed scale, about two million
   triangles in one fused shell, and a texture on an automatic unwrap. Measure it in Blender through the lock tool,
   one model a job. Then follow the `/prep-tripo-prop` skill (`.claude/skills/prep-tripo-prop/SKILL.md`): scale by a
   known size, reduce, new UVs, a new bake, the `bo_` body. Fuse a heap before you reduce it. Try the kit's own
   tiling set by a box projection first, so that the part needs no texture of its own. Every new texture is 1024 px.
9. **Two more uses that were proven.** A relief: render the model from the front with no perspective and no light,
   read the picture as a height field, and lay it on our own exact geometry. A tiling texture: render a dense model
   straight from above and take a patch twice as wide as the tile will be on the mesh.
10. **Show him the result in a Blender window, and record the run.** When a Tripo result beats our code on a part,
    tear it down (the row above on teardowns) and make the finding the default.

The image service and Tripo both spend his credits, and both get generic inputs only. For this kit, confirm once
with him that his word on credits holds here too, then say what each run cost.

## 7. The Modding Kit and the engine: what was measured

- **The prefab entity cap is nearly spent.** After the Rivendell prefab file went in on 2026-11-26, the editor loads
  124,879 of 131,072 prefab entities across every loaded module: 6,193 are spare for every later kit.
  `tools/check_prefab_budget.py` counts one folder a call; count all five (`docs/modding/module-map.md`). Over the cap
  the Kit stops at its start with a queue assert. Plan your prefabs and placement counts against that margin, and
  count again on the day.
- **A prefab that names a mesh that is not imported can crash the Kit.** A prefab file goes in only after his import
  of every mesh that it names, with the Kit closed, on his word. After a crash in the Prefabs list, read the newest
  `watchdog_log_*.txt` for "Meta mesh with name".
- **Never write a texture package by tool,** and never put a source file at the path of a cooked third-party texture:
  that crashed the Kit three times.
- **The import.** The FBX dialog keeps the choices of the last import, so "leave the defaults" is not safe. A file
  point (x, y, z) arrives as (-x, z, y) times 0.01. The owner makes and flags every material by hand from a table
  that you give him. A card's alpha test field counts to 255 (128 is the middle).
- **Names.** The Kit turns mesh names to lower case, and a prefab's lookup is case sensitive. The Kit makes one mesh
  from every FBX object that shares the prefix before the first dot (`docs/kitbash/gondor/README.md`); our own files
  had not proven that in the Kit by 2026-11-26.
- **Levels of detail.** The engine picks a level by distance only. A mesh with levels 0, 3 and 6 shows the full
  mesh to 30 m, `.lod3` from 30 m and `.lod6` from 130 m. The old Mirkwood meshes carry levels 1, 2, 4 and 5 too:
  read the distance of each level in `static-mesh-lod-selection.md`, and compare a control's budget level by level
  (a mesh with no `.lod3` shows its level 2 beyond 30 m).
- **The railing card is not proven.** Rivendell's railing is geometry near the eye and a card with alpha test from
  30 m. On 2026-11-26 its material in the Kit still had the wrong cut-off and blend mode, and no card had been seen in
  the game. Prove one card in the Kit and in the game before a family of railings or tracery depends on it.
- **Steps.** One body has one set of flags, so a stair is its own entity with its own `bo_` body that carries
  `has_steps` (the record, "What the engine needs").
- **Siege machines.** Read "The machines' heights, measured" in the record and `castle-gates.md` before any defence
  is sized: siege ladders exist as 6, 7, 8, 11 and 14; siege tower 12 has its ramp hinge at 11.90 m and serves a 12 m
  deck.
- **A check tool's first real FAIL is a claim about the tool.** Our after-import check passed its tests on made-up
  results and gave two false failures on the first real import. Read the package before a FAIL goes to him. Run the
  Kit check tools without `python -I`.
- **Round paths and texture tiles.** A band that turns round a trunk takes its U from the length of its path at one
  density, and a closed path holds a whole number of tiles (`tiling-texture-sets.md`, its last sections).
- **Between his import and any wiring.** `python tools/check_rdc_entries.py` (read its docstring for the module and
  folder arguments) must show no package without a cache entry, and `python tools/check_prefab_meshes.py` must pass
  before a prefab file goes in. Wire a pilot in a small test prefab or a copy of a scene area, never by a mass
  replace in a scene that is in use.
- **The old pieces in Blender.** The FBX names a texture file for only 3 of its 32 materials, so an import shows
  most pieces with no texture: bind the 85 maps by material name before a sheet or a window for him, or he reads
  grey pieces as the kit's look. Blender renames a repeated material to `.NNN`, which the editor cannot bind: fold
  those back before an export. Never change the export axes of `kitbash_kits.FBX_EXPORT`.
- **The prefab budget.** The 273 entity tags of the Mirkwood prefab file count against the cap already. A new
  prefab file adds to the count; a changed one replaces its own.

## 8. What went wrong before, so that it does not happen again

- Five stages of checkers judged the broken wall against rules and never against the concept that he had chosen. A
  checker's first input is his picture.
- I wrote times, counts and labels before I read them. Read the clock before a time; a label on a sheet says what the
  picture shows (a vertex value is not a picture tone).
- A test of a tool once named the live module as its output, and three files stood in the module for three minutes.
  No test, mutation run or trial run names a live game path.
- A gate that cannot fail proves nothing. Each rule gets a planted fault that its test catches.
- Several code trees grew apart and had to be merged. Keep one tree a kit family; one owner a file in a run.
- I exported parts as separate meshes with no engine need, and he got three assets for one stair.
- Blender draws both sides of a face and the engine does not. Judge with back faces culled, textured and lit.
- A seam tool sampled on the model's own round coordinates and reported a hole that did not exist.
- The first blockout of the Rivendell valley scene did not read as his picture: the approved houses stood at 0.52 to
  1.04 of the picture's size, one long wall filled a band where the picture shows rock, and a walk ended 2.55 m under
  a terrace with no stair. Size each house, platform and stair from his picture against the 1.8 m figure; every walk
  ends on a stair or a deck; a budget comes from placement counts, not from a massing count.
- Some repo docs have CRLF line ends: test with `file <path>`, change them with a script file, then run
  `python tools/lint_docs.py --only-paths <files>` and read the dash count.

## 9. What is different in Mirkwood: settle these first

Mirkwood is not a new kit. It is a kit that exists, that twelve scenes and four prefab packs of other kingdoms
depend on, and that has no record.
The owner's method of 2026-11-24 is the order of the work: "Review what we have -> identify how it was made and for
what -> make a new concept with the current piece as the control group -> create the piece -> texture it."

Questions for the owner (one at a time, each with options and your recommendation):

- **Who gives the rock.** He said for another kit that nature comes from the editor. A cavern is not terrain: its
  walls, its roof and the rock that a stair is cut in must be meshes, with bodies and a nav mesh. Options: rock
  pieces in the kit (walls, roof spans, a rock stair, a ledge); rock from another kit or from vanilla caves; both.
  Find out first what TAOM and vanilla hold for caves (the Moria scenes use Mirkwood meshes already: look at how
  they make their rock). Then ask him, with your recommendation.
- **What "better" means for him,** shown and not asked in words: put the present kit in a Blender window and on a
  sheet beside his nine pictures (the same piece families from fixed cameras, with a 1.8 m figure), and let him
  mark what is weak. His marks and his pictures are the brief.
- **Walks with no rails.** His pictures show bridges and walks with no rail over a chasm. Ask him how the game
  should treat them (no rail as in the pictures, a low kerb, or a rail of roots), with what you found out about
  falls and AI on a narrow walk.
- **The route** (Gate 2 of the prompt), after your review: a new surface on the old geometry, improved geometry, or
  new pieces that the kit lacks. Each piece may take another route.
- **Beside or in place.** Improved pieces as new types beside the old ones (my recommendation, because other
  kingdoms' scenes use the old ones), or the old ones replaced everywhere.
- **The texture size** for improved pieces: 1024 px by the job's rule, or 2048 px as the kit has now.
- **The first target, and its scene.** Which Mirkwood settlement gets the kit first, and whether a siege belongs to
  round one. Its scene is a new scene (section 2): the vanilla scene that it loads today is shared.
- **The names of the new prefab file and showcase scene.** Layout rule 9 gives `Prefabs\taom_<kingdom>_kitbash.xml`
  and `mod_taom_<kingdom>_kitbash`. For Mirkwood both names belong to the old files, which are read only: a
  generator that follows rule 9 would overwrite them. Recommend names of your own with a suffix, and new
  subfolders under `Scenes\Mirkwood` for the new kit files. The layout doc is the Rivendell lead's file: send the
  names there so that rule 9 gets its line for a kit that existed before the standard.

Research and design that are yours (show results; do not put these to him as questions):

- **The catalog** (Phase 1): every object of the FBX with its size, pivot, levels of detail, body, materials, texel
  density and triangle count; start from the baseline of section 2 once you know who made it. The frame rule of
  this kit (which way a piece faces in its file against the scene; the Gondor and Erebor kits are read turned 180
  degrees about Z): prove it at the file level with `tools/oneoff/fbx_frame_compare.py`, or from a group in a scene.
  The joins: the prefab file holds none, so read them from the scenes and from the four other prefab files, which
  place the arch pieces many times (`tools/oneoff/mine_erebor_prefab_joins.py` reads prefab files only; a scene
  needs a reader of its matrix form). Where the scenes place each piece and how often.
- **The control records** (Phase 2) for the pieces that he marks, before any new design.
- **What the kit lacks for a settlement:** list what a Mirkwood town, castle and village need (walls, a gate, towers,
  houses, a hall, stairs, bridges, props) against what the 136 meshes give. The siege code's needs are measured:
  section 7.
- **The halls as interiors.** Which scenes of a Mirkwood settlement are under ground (the keep's hall, the town
  itself, a dungeon), what each mission reads from the vanilla scene that the settlement loads today (your control), where the light comes from (the lamps and the
  shafts of his pictures: find what the engine gives), and how a figure moves from the gate to the throne.
- **Water.** His pictures have falls and a river under the bridges. Find what the editor gives inside a cavern.
- **The design language of Mirkwood,** written from his references and from the kit's own best pieces: what the
  pictures have, what they never have, a colour table with measured values, what is exact and what is free. It must
  be its own, not Rivendell's and not Lothlórien's: three elven kits that read the same would be a fault.
- **Round paths, growth and organic parts:** the proven patterns are in sections 6 and 7 (a band on a curved path,
  the exact core by code with the organic skin from Tripo).
## 10. Your first steps

1. Claim the work: `python tools/work_claims.py claim mirkwood-kit --note "<what>"`. The repo asks for a GitHub issue
   before implementation; creating one is public: ask him once whether it is a new issue or part of #1600, then use
   `/issue`, and never name a bought source kit, a film or a studio in it.
2. Read section 1. Check the table of section 2 against the module.
3. Look at his nine pictures yourself, each one, before you read my descriptions of them.
4. The review (one run: a cataloguer, then a checker who measures a sample of the pieces again): the catalog of the
   FBX, the textures and materials, the joins from the scenes, the use in the twelve scenes and four prefab packs,
   the older copies and what they say on how the kit was made. Write it as `docs/kitbash/mirkwood/` in the form of
   the Gondor and Erebor catalogs, with a coverage file of your own in that folder.
5. Show him the kit as it is: one sheet and one Blender window with every piece family from fixed cameras and a
   1.8 m figure. Ask him to mark what is weak and what is good.
6. From his pictures and his marks: the design language, then the questions of section 9, then the first round on
   two or three pieces that he marked, each as the control piece beside two or three improved variants at the same
   framing. He chooses.
7. Then the build, his import, the look in the Kit and in the game, by the agent file's steps.
8. Records: your own build record `docs/kitbash/mirkwood-kit.md` from the first result on, and a state file in your
   work folder. The rows in shared docs (`docs/kitbash/README.md`, the coverage doc, the feature map, the doc lookup)
   wait for the merge of the jobs, unless a row is needed sooner: then message the Rivendell lead. Keep the list of
   owed rows in your record.
## 11. Limits, and what is not handed over

- Nothing enters a game module without his word for that step. On 2026-11-26 he allowed the Rivendell lead to copy
  kit files with the editor open; that word was given to that session for that kit.
- No commit, no push, no purchase without his word. The image service and downloads are his to allow
  for this kit: ask. Tripo: section 6.
- Never name the bought source kit of the Rivendell conversion ("a licensed elven building kit"). Never put his
  pictures, his name or his address into a request to an outside service.
- The Blender windows, the Modding Kit and Painter on this machine are his or another session's: stop no process that
  you did not start, and close no window.
- All work files on `E:`. Every new texture 1024 px unless he says otherwise.
- Everything that exists under `Scenes\Mirkwood` in the live module (the source FBX, the 85 maps, the 117
  packages), the Mirkwood prefab file and its showcase scene are read only for you. The live module is not under
  git (the assets repo holds a mirror of some files): a change there needs his word and a check in the repo in
  the same change. A new folder or file under `Scenes\Mirkwood` is such a change.
- His standing rule on sources (`feedback_put_sources_in_assetsources.md`) lets a session write the changed source
  of an asset that he imported. It does NOT cover this kit's old files: an update of the old FBX or of one of its 85
  maps repaints twelve scenes and four prefab packs, so it needs his word on "in place". A new asset's first copy
  needs his word too, after the checks of that memory file.
- Yours without a question: read-only work, headless Blender through the lock tool, files in your work folder and
  in `docs/kitbash/mirkwood*`. His word first: a Workflow run, the image service, Tripo, a GitHub issue, anything in
  a game module, any git action that changes a ref, the index or the working tree.
- The Rivendell job runs on in another session (work claim name "Rivendell Kit Bash"; its state is the top of
  `kingdom_kits\rivendell\painter_lab\STATE.md`). It writes to this worktree, to `kingdom_kits\rivendell` and to the
  map module's `Scenes\Rivendell`. A Lothlórien kit session may run too (its handover is
  `docs/kitbash/lothlorien-kit-handover.md`). You write new files only (`docs/kitbash/mirkwood*`, the `mirkwood` work
  folder). For a change in a file that the Rivendell job owns (the agent file, the layout doc, the shared tools), ask
  the maintainer to pass it on, or message that session by its name.

## 12. Where the rest is

- The owner's page of the Rivendell job: `kingdom_kits\rivendell\review_now.html`.
- The two trials of the agent file: `kingdom_kits\_agent_trial\r1` (five plans of a kit job and three judgements) and
  `r2` (five plans and one judgement that compares both rounds).
- The check of the Lothlórien handover, whose general sections this document shares: `kingdom_kits\_shared\handover_check`
  (what a fresh session could not find, and what the first version lacked).
- The Gondor and Erebor kits, for a building composed from held pieces: `docs/kitbash/gondor/`, `erebor/`.
