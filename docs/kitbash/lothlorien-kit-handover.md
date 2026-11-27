# Lothlórien kit: handover from the Rivendell kit sessions

Written 2026-11-26 by the lead session of the Rivendell kit (kingdom kits job, issue #1600), on the maintainer's
request. Second version, after a check by three agents (one proved the paths, quotes and facts; one played the new
session; one read this job's records for what was missing). Paste everything below the line into a fresh Claude Code
session that is started in `E:\repos\taom-kingdom-kits` (branch `feat/kingdom-kits`). Start it there and not in
`E:\repos\TAOM`: the kit docs, the kit standard and the `kitbash-architect` agent exist only on this branch, and
nothing of it is committed yet.

**Before the session starts, the maintainer saves his seven reference pictures** (they were pasted into a chat and are
not on disk) into `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\lothlorien\refs\owner_2026-11-26\` as `ref_01` to
`ref_07`, in the order of the list in section 2. The folder exists and is empty.

---

You start the settlement kit of Lothlórien for TAOM. The maintainer (Mike, the owner of every look decision) wrote on
2026-11-26: "Give me a handover for another session to pick up and start a lothlorien kitbash. You learned a shit ton
from these sessions. I need a hand over document with all of your stuff plus the directive to take that knowledge and
use these references to start a lothlorien one." and "An environment in the trees basically."

So the job is: a kit for a city in the trees, built with the method and the tools of the Rivendell kit, from his seven
pictures. This document tells you what exists, how he works, what went wrong before, and what to do first. It grants
you nothing: every permission of section 11 is yours to get from him.

## 1. Read first, in this order

1. `.claude/agents/kitbash-architect.md`: the role for every builder and judge of kit work. You are the lead: you
   brief that agent, look at every result yourself, decide each finding, and talk to the owner. Its third version had
   no trial. In its second trial no builder plan named a generator or a test, none counted placements, and the scene
   plan measured no control: ask for those three in every brief and check them in the plan. If your session does not
   list the agent, name the file in the brief as the worker's role.
2. `docs/kitbash/kingdom-kits-prompt.md`: the "Rules of this work" and Phases 3 to 6. Its Phases 1 and 2 (pick a pilot
   kingdom, decide a retexture route) and its Gates 1 and 2 are answered for you: the kit is Lothlórien and its
   geometry is new. Its sentence that the image connector was allowed held for the Rivendell session only.
3. `docs/kitbash/kingdom-kit-layout.md`: where a kingdom's kit lives in the map module, the file names, rules 1 to 10.
   Its family words (castle, house, hall, village, open, small, part) do not fit a tree kit. Propose your family table
   to the owner; the change of that doc goes through the Rivendell lead or the owner (section 11).
4. `docs/kitbash/rivendell-siege-set.md`: the build record of Rivendell, about 400 KB. Never read it whole. Run
   `grep -n '^## ' docs/kitbash/rivendell-siege-set.md` and read these sections of 2026-11-26: the maintainer's
   directions (14:47, and 14:55 to 15:00), "One asset a piece", the marks on the centre house, "The valley scene,
   round 1", "The prefab file is in the map module", and "The machines' heights, measured". Its facts on the Modding
   Kit since 2026-11-26 are newer than the header of the texture doc.
5. `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell\kit_full\design_v2\RIVENDELL_DESIGN_LANGUAGE_V2.md` with
   `LANGUAGE_AMENDMENT_1.md`: a design language written from his pictures. Yours has the same form, not the same
   content.
6. `docs/kitbash/tiling-texture-sets.md` (how the 1024 px sets were made, what the engine reads from them, its table of
   Painter traps, and at its end the rules for a band on a curved path), then
   `docs/reference/engine/static-mesh-lod-selection.md` and `castle-gates.md`.
7. The kit lessons: `grep -n '^### ' docs/reviews/lessons/build-tooling-workflow.md | grep -i '#1600\|kit'`, then read
   those entries.
8. The game data. `docs/modding/id-cheatsheet.md` gives the ids (culture and kingdom `lothlorien`, settlement prefix
   `L`) and counts only. The settlements themselves are in the live
   `Modules\TAOM_Map\ModuleData\settlements.xml` (read only): 19 rows carry `Culture.lothlorien`, among them `town_L1`
   Caras Galadhon and `castle_L1` Cerin Amroth. Read which scene each one loads today. `docs/kitbash/kingdom-coverage.md`
   holds nothing on Lothlórien: that review is your first research.
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

## 2. His seven pictures

They stay on this machine. They go to no outside service, and no file names a film, a studio or a person, also where
a picture carries a title or a mark. What I saw in them (my reading, for you to check against the pictures):

1. `ref_01`: a white stone city on a cliff beside waterfalls. Domes on round colonnaded drums, tiers of arcades, a
   stone bridge of one great arch, terraced stairs down to the water. It is stone and domes, not trees: ask him what
   he wants from it.
2. `ref_02`: a night painting of a tall house in a great tree. Dark swept roofs whose points turn up like leaves, tall
   glowing pointed windows with tracery, a railed walk along a bough to a small lantern pavilion, blue lights.
3. `ref_03`: the same design in another print (it carries a title mark).
4. `ref_04`: a city among many great trunks at dusk. Round lantern pavilions with domes hang on the trunks at several
   heights; spiral stairs and long ramps on slender arches run between the trees; gold leaves, blue glass.
5. `ref_05`: a drawing on brown paper. Ground stairs lead to a trunk; a walk of pointed arches winds round the trunk
   as a spiral; arcades bridge to other trunks; a small pointed shrine stands between roots.
6. `ref_06`: a white house in a tree at night. Swept shingled roofs with curled ridge ends, tall lace gables with leaf
   tracery, hanging lace pendants, a slatted stair down to round platforms, a small lace pavilion.
7. `ref_07`: a daylight scene. A wooden spiral stair with rails winds round a huge trunk whose roots stand in water;
   more platforms and lace arches on other trees; warm lamps.

In six of the seven the tree is the structure: platforms ring the trunk, stairs wind round it, walks bridge from
trunk to trunk, and the houses are tall, light and pointed, with more opening than wall.

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
- **His checklist for built pieces** is the architecture block of `feedback_asset_art_rules.md`. The rules that a city
  in trees meets first: a floor that troops walk on is one flush surface in mesh and body; every walk has a way up; a
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

Work folders are under `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\`. `rivendell` is the first kit. `lothlorien`
holds `refs` already: add `kit_full`, a state file and your own `_tools` beside it. Copy a tool into your folder
before you change it; call in place only what this table marks so.

| What | Where | Use for Lothlórien |
|---|---|---|
| The Blender lock tool (eight slots, a memory ceiling) | `rivendell\_tools\blender_one.py` | CALL IN PLACE for every headless Blender job, so that both jobs share one limit. A window for the owner is the one other way to start Blender (below) |
| Base methods of the design language: arch outlines, the free rib, the plate (K1 to K3 in the record), and from round 2 a turned solid and placement along a walk | `rivendell\kit_full\v2_base\tools` (read `CHECK.md`, and `CHECK_R2.md` when it exists; round 2 was in work on 2026-11-26) | COPY when round 2 is checked. They import one module of the kit tree (`rivendell_member_mesh`): copy that too. Your members are your own |
| The kit tree: piece modules, the build driver with lower levels of detail and collision bodies, and three checks (the probe: rays against a built piece; G4b: the mesh; G6b: the levels and bodies) | `rivendell\kit_full\src_k`; its state is `rivendell\kit_full\step2g\LEAD_DECISIONS_2G.md` | The pattern of a piece module and its checks. It was not green on 2026-11-26: read the decisions before you copy |
| The kit set tools: write a kit file (one FBX for a family of pieces), the manifest, the copy into the module with hash checks and an undo list, the check after his import | `rivendell\kit_full\editor_set\tools` | COPY AND CHANGE. They are wired to Rivendell (`KINGDOM = "rivendell"` in `kit_set_rules.py`, a default of `Scenes/Rivendell`, a wall staging argument). Run as they are, a copy without `--under` aims at the Rivendell folder of the module |
| The prefab generator and its owner steps | `rivendell\kit_full\delivery` (`PREFAB_OWNER_STEPS.md`) | COPY AND CHANGE; read the steps first |
| The Modding Kit import plan (the FBX dialog, the materials that the owner sets by hand, the checks) | `rivendell\build\v07\kit_import_prep\KIT_IMPORT_PLAN.md`, `KIT_SET_IMPORT_STEPS.md` | Read before the first import |
| The texture method (Substance 3D materials in Painter, the converter, the trim sheet) | `docs/kitbash/tiling-texture-sets.md`; `rivendell\painter_lab` | Wood, bark, leaf and pale stone sets. Painter is one shared instance: claim `painter-instance` first |
| The massing library and the sheet makers | `rivendell\kit_full\design_v2\massing\_scratch\mlib.py` and the `_scratch` folder of each massing round | COPY: the fastest way to a first sheet |
| The review page maker | `kingdom_kits\_shared\review_page\page_maker.py <HH:MM> <content json> <your work folder>`, with an example content file | One page for him: what changed, what you need. It writes `review_now.html` into the work folder that you name |
| A window for him | Copy the `.blend` to a `view` folder, start `blender-launcher.exe "<file>"` (under `%LOCALAPPDATA%\Microsoft\WindowsApps`), prove the window with `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\_shared\tools\capture_blender_windows.ps1 <folder>` | Tell him to press Home if the saved view is close. The window is his from then on |
| The Tripo CLI (he calls it "tripoli") for carved, organic and broken parts | `docs/features/tripo-cli.md` | Section 6 says when and how |
| How to take a reference result apart | `docs/kitbash/teardowns/` (four records); the procedure is the `/teardown` skill, on local trunk since 2026-11-26 (`E:\repos\TAOM\.claude\skills\teardown\SKILL.md`, not pushed). A session in this worktree does not list it until the branch has merged trunk: read the file | A part that a checker or he rejects twice gets a reference and a teardown before the next round |

Not for reuse: the forms of the Rivendell design language, the castle wall code with its siege wiring, the motif data
of the Rivendell interlace railing (it comes from the licensed elven building kit: your lace gets motif data of its
own, in its own folder), and the decisions of the Rivendell record. Those carry ids such as P, LD, WB, CH, SC, KS, MR,
PD, VB, H, and K1 to K9 of `step2d\LEAD_DECISIONS_2D.md`; they answer Rivendell's pictures and trees. Whether the
licensed kit's raw export is a donor of forms for Lothlórien is the owner's decision; do not use it on your own.

## 6. Tripo (he calls it "tripoli"): when and how

Tripo is a service that makes a textured 3D model from one picture, from several views or from a text. Its
command-line tool `tripo` is installed and logged in on this machine. The owner on 2026-11-25: "We also have more
tripoli we can do, keep that in mind", and on the credits of the kingdom kits job: "dont worry abotu credits, we have
plenty". On 2026-11-26 he asked that this handover says how to use it. The full record is
`docs/features/tripo-cli.md` (the setup, the rules, every command, every job with its cost, every lesson): read it
before your first run, and add a row to its "Jobs" table and a line to its lessons after every run.

**The rule of the job:** a carved, organic or broken part gets a Tripo proposal, with its cost, before it is built by
code. Our code-built rubble and fallen blocks read as boxes; the Tripo ones did not.

| Use it for | Do not use it for |
|---|---|
| The skin of a trunk: bark, root flares, a fork of a bough, burls | A platform, a stair, a rail, a bridge, a house: anything with an exact joint or an exact floor, door or step size |
| One-off carved parts: a pendant, a lantern body, a finial, a leaf cluster, a statue | A part that must be an exact mirror pair |
| A relief or a tiling texture taken from a model (below) | A part that sits on a tiling texture set |

The reason for the right column: a generated mesh has no exact dimension and no shared end section, and its flat
faces do not stay flat when it is reduced. The pattern that worked: **the exact skeleton by code, the organic skin
from Tripo.** For a great trunk that means a code-built trunk with exact ring joints, and the bark and root relief
taken from a Tripo model as a height field.

**The steps.**

1. **The picture.** One object, centred, with a margin; a plain mid-grey background; a three-quarter view from a
   little above (straight from the front for a relief such as bark); even soft light; no shadow, no figure, no
   plants, no text. Make it with the image service (his word first) or as a render of your own massing. His reference
   pictures never go to Tripo. The prompt and every file name are generic: no project, place or person name.
2. **The folder.** `kingdom_kits\lothlorien\tripo\refs` for the pictures and `kingdom_kits\lothlorien\tripo\out` for the results. Give every run an
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
- **Levels of detail.** The engine picks a level by distance only: the full mesh to 30 m, `.lod3` from 30 m, `.lod6`
  from 130 m (`static-mesh-lod-selection.md`).
- **The railing card is not proven.** Rivendell's railing is geometry near the eye and a card with alpha test from
  30 m. On 2026-11-26 its material in the Kit still had the wrong cut-off and blend mode, and no card had been seen in
  the game. Prove one card in the Kit and in the game before a lace family depends on it.
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

## 9. What is different in Lothlórien: settle these first

Questions for the owner (one at a time, each with options and your recommendation):

- **Who gives the trees.** He said that nature comes from the editor. But here the tree carries the building. A trunk
  that a platform, a stair and a house join to needs exact joints, a body and a nav mesh, and the editor's flora has
  no joints. Options: kit trunks with roots and bough stubs at a few sizes, with the leaves and the forest from the
  editor; platforms that clamp round the editor's trees; both. My recommendation is the first.
- **What `ref_01` is for.** The ground level of the city, its bridges and stairs, or another kit.
- **The first target and the defence.** Which settlement comes first (Caras Galadhon only, or the castles too), and
  whether a siege belongs to round one. The book gives Caras Galadhon a green wall and a fosse; check the text before
  you use it.
- **The family words** of the kit (for example trunk, platform, bridge, house, small) for layout rule 1.

Research and design that are yours (do not put these to him as questions; show results):

- **The joint system is round.** The Rivendell joints sit on a straight tile grid. A walk round a trunk joins by angle
  and radius, and a bridge between two trunks has a free length. Design it before any piece: the trunk radii, the ring
  heights, the angle step, and how a bridge takes up a distance that no grid gives. Rivendell met this in small: the
  curved terrace cut every 20 degrees, the round colonnade, the cage dome, the bridge hoops
  (`rivendell\kit_full\design_v2\scene_valley`).
- **People and AI walk at height.** Every platform, stair and bridge needs a flush floor, a body, a nav mesh that
  connects, a rail where a fall is possible, and stairs as their own entities. Find a vanilla or TAOM scene with
  walkways at height, measure it as your control, and find out from the code how the game handles a fall and a narrow
  spiral.
- **Lace costs triangles.** The approved Rivendell railing costs 604 triangles a metre as geometry
  (`docs/kitbash/teardowns/2026-11-26-railing-tracery.md`). Plan the levels of every lace member from the start, and
  count how often a scene places a piece against the entity cap.
- **Light.** Two of the pictures are night scenes with lamps and glowing windows, and one is dusk. The engine docs of
  this repo hold nothing on emissive materials or light entities in a prefab: find it in the installed game and
  record it, never guess.
- **Materials.** Silver or grey bark, pale wood or pale stone for the lace, dark shingles, gold leaves, blue or warm
  glass. Measure the colours from his pictures, and show him two readings where the pictures differ (white lace in
  `ref_06`, dark timber in `ref_02`, brown wood in `ref_07`).

## 10. Your first steps

1. Claim the work: `python tools/work_claims.py claim lothlorien-kit --note "<what>"`. The repo asks for a GitHub issue
   before implementation; creating one is public: ask him once whether it is a new issue or part of #1600, then use
   `/issue`, and never name a bought source kit, a film or a studio in it.
2. If the pictures are not in the refs folder, tell him, and do steps 3 and the joint system on the game side only.
3. Read section 1. Then review what the game has: the 19 settlements and their scenes, pieces that stand at height,
   the flora that the editor offers at the size of these trunks. Measure your control pieces.
4. Write the design language of Lothlórien from his pictures (one run: a writer, then a checker who holds it against
   the pictures).
5. Put the four questions of section 9 to him. Start with the trees.
6. The first massing round: one whole tree as a scene (a trunk, a ground stair, a spiral walk, two platforms at two
   heights, one house, one bridge to a second trunk, one lantern pavilion), in three forms that differ in plan, height
   and roof, cut into pieces with their joints shown. A sheet beside his pictures, an orbit file, a Blender window.
7. From his marks: the fine details, then the build.
8. Records: your own build record `docs/kitbash/lothlorien-kit.md` from the first result on, and a state file in your
   work folder. The rows in shared docs (`docs/kitbash/README.md`, the feature map, the doc lookup) go through the
   Rivendell lead or wait for the merge of the two jobs: say which in your record.

## 11. Limits, and what is not handed over

- Nothing enters a game module without his word for that step. On 2026-11-26 he allowed the Rivendell lead to copy
  kit files with the editor open; that word was given to that session for that kit.
- No commit, no push, no purchase without his word. The image service and downloads are his to allow
  for this kit: ask. Tripo: section 6.
- Never name the bought source kit of the Rivendell conversion ("a licensed elven building kit"). Never put his
  pictures, his name or his address into a request to an outside service.
- The Blender windows, the Modding Kit and Painter on this machine are his or another session's: stop no process that
  you did not start, and close no window.
- All work files on `E:`. Every texture 1024 px unless he says otherwise.
- The Rivendell job runs on in another session (work claim name "Rivendell Kit Bash"; its state is the top of
  `kingdom_kits\rivendell\painter_lab\STATE.md`). It writes to this worktree, to `kingdom_kits\rivendell` and to the
  map module's `Scenes\Rivendell`. You write new files only (`docs/kitbash/lothlorien-*.md`, the `lothlorien` work
  folder). For a change in a file that the Rivendell job owns (the agent file, the layout doc, the shared tools), ask
  the maintainer to pass it on, or message that session by its name.

## 12. Where the rest is

- The owner's page of the Rivendell job: `kingdom_kits\rivendell\review_now.html`.
- The two trials of the agent file: `kingdom_kits\_agent_trial\r1` (five plans of a kit job and three judgements) and
  `r2` (five plans and one judgement that compares both rounds).
- The check of this document: `kingdom_kits\_shared\handover_check` (what a fresh session could not find, and what the
  first version lacked).
- The Gondor and Erebor kits, for a building composed from held pieces: `docs/kitbash/gondor/`, `erebor/`.
