# The gate of Erebor: handover from the Rivendell kit sessions

Written 2026-11-26 at 19:46 by the lead session of the Rivendell kit (kingdom kits job, issue #1600), on the
maintainer's request. Version 2: two independent checks of the first version are applied (their reports are in
`E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\_shared\handover_check_erebor_gate`). GIVE THE WHOLE OF THIS FILE to a
fresh Claude Code session, this block too.

**Where you work.** Start in a new sibling worktree on `E:` (for example
`git worktree add E:\repos\taom-erebor-gate -b feat/erebor-gate bannerlord-1.5.x` from `E:\repos\TAOM`): never edit in
a checkout on the trunk branch. Trunk holds the kit docs, the kit tools and the `kitbash-architect` agent since
commit `a4611ba10`. Newer kit records may be uncommitted in `E:\repos\taom-kingdom-kits`: run
`git -C E:/repos/taom-kingdom-kits status --short`, and read every file that it lists as new OR modified from that
worktree, also where your own worktree has an older copy (the scene reference and the Rivendell record are such
files).

**The maintainer's reference pictures are on disk:** ten pictures, `ref_01.webp` and `ref_02.png` to `ref_10.png`, in
`E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate\refs\owner_2026-11-26\`. He saved them in a folder of his
own on 2026-11-26 and the Rivendell lead copied them there; `SOURCE.txt` lists his file names and a hash of each.
Section 3 says what each one shows. They stay on this machine and go to no outside service. No file of yours names a
film, a studio, a maker or a person, also where a picture carries a title or a mark (two of them do: copy neither
into a file); "the gate of Erebor" and "his references" are the names to use.

---

You build the great gate of Erebor for TAOM (a Lord of the Rings total conversion for Mount and Blade II Bannerlord,
engine v1.5.7). The maintainer (Mike, the owner of every look decision) wrote on 2026-11-26:

"Give me a hand over document for making the erebor gate from the movies. I am going to give it to another session.
I'll make a folder for Erebor references. But I want the gate to be made as one piece with the mountain in it and it
has to be siegeable (walls able to be broken like in the scenes, etc). Precision and master level carving of the 3D
objects is critical for this one."

So four things bind you: the look of his references; ONE piece, with the mountain in it; a siege that works on it;
and carving of a master's quality with exact craft. This document tells you what "siegeable" means in this engine
with the measured numbers, what we hold for Erebor, how he works, what went wrong before, and what to do first. It
grants you nothing: every permission of section 12 is yours to get from him.

## 1. Read first, in this order

1. `.claude/agents/kitbash-architect.md`: the role for every builder and judge of kit work. You are the lead: you
   brief that agent, look at every result yourself, decide each finding, and talk to the owner.
2. `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell\rivendell_reference.html`: the Rivendell kit as a worked
   example, on one page with the real pictures of each step: his picture read as built things, the massing
   variants, his marks, the fine details, the siege pieces with their levels and bodies, the broken wall, what an
   independent critic caught, and THE TRIM SHEET (carving that is modelled as a high model and baked to maps).
   The maintainer called it a great reference: read it and open its pictures before you plan. Then
   `docs/kitbash/settlement-scene-needs.md`: what each scene of a settlement needs from a kit: the solid wall, the
   broken wall, the towers, a gatehouse for two gates, the gates, the siege placement of the defenders, the
   attackers' side. Every number, entity, script and tag is there with its source line, checked on 2026-11-26. It
   was written so that you look none of this up again. Section 2 below is its short form for this job; the
   reference is the authority. Its long tables and its census scripts are in
   `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\_shared\scene_needs`. Add what you measure to it.
3. `docs/reference/engine/castle-gates.md`: how a castle gate works, what a custom gate must have, and the routes.
4. `docs/kitbash/erebor/README.md`, then `design-patterns.md`, `meshes.md`, `materials.md`, `textures.md` and
   `runes.md` in that folder: the dwarven kit that exists, measured on 2026-10-15 (145 pieces, the frame rule, the
   joins, the rune pipeline). It is your control group and your material library. `docs/kitbash/erebor-buildings.md`
   shows how a whole building became one FBX with its levels and its body.
5. `docs/kitbash/kingdom-kits-prompt.md` (the "Rules of this work" and the six phases) and
   `docs/kitbash/kingdom-kit-layout.md` (the folders, the names, rules 1 to 10; rule 10 is "one asset a piece"). The
   layout doc says that the older kits, Erebor among them, keep their folders: nothing that exists under
   `Scenes\erebor` is moved, renamed or changed in place.
6. `docs/kitbash/rivendell-siege-set.md`: the build record of the Rivendell siege pieces, about 450 KB. Never read it
   whole. Run `grep -n '^## ' docs/kitbash/rivendell-siege-set.md` and read: "What the engine needs", "The machines'
   heights, measured, and the wall's foundation", the sections on the broken wall's design and its stage 6, and
   "Gates: how the game does them, the routes, and the decisions".
7. `docs/kitbash/tiling-texture-sets.md` (how the 1024 px sets and a trim sheet were made, what the engine reads from
   `_d`, `_n`, `_s`, the Painter traps), then `docs/reference/engine/static-mesh-lod-selection.md`.
8. The kit lessons: `grep -n '^### ' docs/reviews/lessons/build-tooling-workflow.md | grep -i '#1600\|kit'`.
9. The owner's standing preferences: the memory folder `C:\Users\mikew\.claude\projects\e--repos-TAOM\memory`. Read
   `MEMORY.md` and these files: `feedback_precision_and_creativity.md`, `feedback_asset_art_rules.md` (its block on
   built architecture is his checklist), `feedback_one_asset_a_piece.md`, `feedback_show_look_variants_early.md`,
   `feedback_show_meshes_in_blender.md`, `feedback_learn_from_reference_tools.md`, `feedback_lean_review_page.md`,
   `feedback_long_term_structure.md`, `feedback_update_docs_as_you_go.md`, `feedback_everything_on_e_drive.md`,
   `feedback_kit_handover_scene_needs.md`, and the file `tripo-cli.md`.

## 2. What "one piece, with the mountain, siegeable" means in this engine

This is the hard part of the job, so it comes before everything else. Each fact below is from the scene reference
(item 2 above), where its source line stands. This section is a summary: where the two differ, the reference wins.
Before you size anything, copy the checklist of the reference's section 1 (29 rows: what the kit delivers, what the
scene builder adds, which decision is owed) into your build record and mark every row for this gate.

**"One piece" cannot be one mesh, and it can be one thing that is placed.** A siege reads ENTITIES with scripts and
tags, and one entity has one mesh and one body with one set of flags. My reading of his words, for you to put to
him with a massing model: the gate is ONE PREFAB that a scene builder places once, with the mountain in it. Inside
it, the meshes are as few as the engine allows (layout rule 10): the mountain and every part that never changes are
one mesh, and a part is its own entity only where this table says so.

| Part | Its own entity? | Why (the engine's need) |
|---|---|---|
| The mountain face and the fixed architecture of the gate front | One mesh (or a few, by size) | Nothing reads it. It needs a body, and levels of detail |
| Each of the two wall sections that can be broken | Yes: a root with the `WallSegment` script and a side (`left` or `right`), with two direct children tagged `solid_child` and `broken_child`; both children carry the script too | The breach swaps the two children. A siege level needs EXACTLY TWO sided breach walls, and a battering ram in the mission: one wall or three walls make the mission throw |
| The parapet parts where a siege tower's ramp lands, and every part that can be shot away | Yes: each a part with a `DestructableComponent`; the landing parts carry the tag `tower_merlon` | The engine hides the tagged parts when the attackers bring a tower, and only on an entity with a synched script |
| The outer gate and the inner gate | NOT in your prefab: the scene places them | A gate is its own scene entity, never a child of a gatehouse prefab: no prefab file nests a prefab reference (`castle-gates.md`, the checklist of what a scene must set). The proven route is a vanilla gate prefab that the scene places (the `CastleGate` script, a door child tagged `gate` with a skeleton, a `wait_pos` child). Your prefab holds the passage, the room between the two gates and the deck over it, sized for those gates |
| Every stair | Yes: its own mesh and `bo_` body with the body flag `has_steps` | One body has one set of flags |
| The rubble of a breach | Part of the `broken_child` | It is the attackers' walk: its body and its walk faces matter as much as its look |

**What the scene builder adds, and what your prefab holds.** The scripts on the wall roots and their children, the
meshes, the bodies and the destructible parts can stand in the prefab. The side tags, the navmesh face ids, the
position markers (`middle_pos`, `wait_pos`, `attacker_wait_pos`), the ladders, the siege towers with their paths,
the ram, the machine points and the spawn points are placed in the scene. The reference has the table and the 16
entities of one breach wall. The prefab entity cap is nearly spent: 6,193 of 131,072 were spare on 2026-11-26, so
count your prefab's entities before you propose it (`tools/check_prefab_budget.py`, one folder a call).

**The front is the whole wall line of its scene.** For each wall level a siege scene carries: two breach walls, one
on each side, each paired by its side with two ladders and one siege tower; four machine places for the defenders;
and the gatehouse. The gatehouse numbers of the scenes that work (the reference, sections 7 and 8): an outer
passage at least 4.70 m clear and 6.9 m high for the largest vanilla door; 7.15 to 9.81 m between the two gates; a
deck with a rock hole over that room, at most about 14 m over its targets; and open ground for at least 6.25 m in
front of the outer gate for the ram. In the two TAOM towns that were measured, the roots of the two breach walls
stand 96.1 m and 175.3 m apart, and the attackers' side reaches out about 100 m for the archers, about 135 m for the
trebuchets and 150 to 215 m for the paths of the towers and the ram. All of that must fit in front of, and across,
a recess in a cliff. So ask him early how wide the front may be, and draw the frontage in plan before you design
the recess. A scene has three wall levels; the reference recommends one deck height for all three in a kit's first
version (its section 2.4).

**The machines set the height of the fight, not the look.** No code reads a wall's height. The attackers' machines
have fixed sizes: siege ladders of 6, 7, 8, 11 and 14 m, and siege towers of several heights (the reference has
the list; the tallest has its ramp hinge at 11.90 m and serves a deck at about 12 m, the height that the
maintainer chose for Rivendell's walls; the one dwarf scene with siege entities uses lower machines: count them). A battlement that the attackers can reach is therefore a walk at about 12 m above their
ground (or lower, for a shorter ladder). The gate front of his references stands far
above that (section 3). So the job's first design question is WHERE THE SIEGE LINE IS on a monumental front. Three readings to
build as massing and show him (rank none):

- **A. A fighting rampart in front of or across the gate mouth:** a lower wall with its walk at about 12 m, its two
  breach sections left and right of a gate passage, and the great carved front behind it as one fixed mesh. No
  picture of his shows such a wall: it would be our addition, so show it as that.
- **B. The front's own lowest gallery is the walk:** the monumental face is built so that a continuous battlement
  runs at about 12 m, with the breach sections in it, and everything above is fixed.
- **C. Two heights:** a low outwork that ladders reach and a high gallery for the defenders' archers and machines
  that no attacker reaches.

Whichever he picks: the walk is one flush surface in mesh and body, at least as wide as the walks of the scenes that
work (Rivendell's wall has 4.27 m clear, the Gondor wall 6.0 m), with a way up for the defenders, and with the wall
faces marked for the navmesh (a defender is refused a face whose id does not end in 1; an attacker is refused one
that does; the reference has the rule and the ids).

**The broken walls "like in the scenes".** The scenes that work (the two TAOM towns that the reference measured, and
the vanilla castles) build a breach from a solid state and a broken state of the same 20 m section, with a gap of
about 12 m (measured on the Gondor control piece) and a mound of rubble that the attackers climb. Take the entity tree from the reference. Take the lesson
from Rivendell's broken wall: five stages of checkers judged it against rules and never against the picture that he
had chosen, and it passed 133 gate rows and did not look like the picture. Your breach is judged beside his
reference from the first proof on. Rubble built by code read as boxes; rubble from Tripo did not (section 9).

**The gates.** A siege has an outer and an inner gate with a room between them, and the ram attacks the outer one.
The vanilla gate leaves are small beside a monumental door (the tallest measured leaf pair is 4.695 m wide and
6.838 m high, the widest 5.321 m wide and 5.921 m high). A gate needs more than the row of the table says: the
full list of what a scene must set on a gate is the checklist in `castle-gates.md`. `castle-gates.md` gives the routes: a vanilla gate prefab inside our gatehouse; our own leaves on a
vanilla door skeleton; our own skeleton and clips. No route with our own leaves is proven in the Kit yet (the probe
of route B has not run). So a door of the size of his references is a question for him with options, for example: a
fixed monumental door surround with a working gate of vanilla size set in it, or a great door on our own skeleton
(the most work, and unproven). Show him the options as massing with a 1.8 m figure and the ram beside them.

**The defenders' side.** One deployment point shows one weapon at a time, so a machine platform is sized for the
larger weapon: a clear level deck of 5.50 x 5.72 m for a mangonel with its crew and its stone pile, and no body in the
120 degree arc toward the field (the reference has the distances and the tags, and the archer position tags that the defender AI
reads). Decide with him where the defenders' machines stand on the front before you carve it.

**The mountain.** He wants the mountain in the piece, so the rule of the other kits (nature comes from the editor)
does not hold here. That makes these your design problems, each with a number to measure before you promise it:

- How much mountain: the face around the gate out to where the scene's terrain can meet it. The piece needs a
  skirt that goes under the terrain, so that a scene builder sinks the skirt and not the piece. The Erebor kit's
  own rule for buildings is the opposite (no skirt below grade; `docs/kitbash/erebor/design-patterns.md`): put the
  skirt to him as a question.
- The body: an entity's collision body is built from convex parts. A rock face needs a body that troops and
  missiles meet where they see rock, and a camera does not fall through.
- The levels of detail: the engine picks a level by the distance from the camera to the SURFACE of the entity's
  bounding sphere (`static-mesh-lod-selection.md`). One mesh as large as a mountain has a sphere that the camera
  is inside or near during a whole siege, so it would draw its full detail all the time. Cut the rock and the
  fixed front into several entities by where the eye comes near (the foot, the middle, the heights, the far
  flanks), each with its own levels. That is still one prefab.
- The texel density: a rock face of this size cannot have a texture of its own at 1024 px. It sits on tiling sets
  and a blend, with real carving where the eye comes near.
- The budget: measure the triangles of the control (a whole gate front of a scene that works) and of the Erebor
  kit's castle wall before you set yours. A budget comes from a count, not from a guess.

## 3. What we hold for Erebor, and his references

**Measured by me on 2026-11-26 in the live map module (read only). Check each number again before you use it.**

| What | Where | Fact |
|---|---|---|
| The dwarven kit's sources | `Modules\TAOM_Map\AssetSources\Scenes\erebor` | Folders `buildings` (6 files), `meshes` (8), `statues` (2: `SD_DW_IH_Statue_A.fbx`, `_B.fbx`), `walls` (2: `SM_DW_Castle_Wall_A.fbx`, `kys_dw_wall.fbx`), `textures` (69). The catalog `docs/kitbash/erebor/` measured 145 pieces; its image `06-nature-cliffs-rocks-01.png` shows cliff and rock pieces (shared nature meshes named `sm_nat_*`, not part of the 145 dwarven pieces) |
| The kit's prefab file | `Modules\TAOM_Map\Prefabs\taom_erebor_kitbash.xml` | 3,978 entity tags. It holds a prefab `dwarf_gate_a`: the mesh `sm_dw_castle_wall_a1_gate` with its body and its merlons `sm_dw_castle_wall_a1_gate_mrln_01` to `_12` as child entities. That is a castle wall piece with a gate opening, not the great gate |
| The showcase scene | `Modules\TAOM_Map\SceneObj\mod_taom_erebor_kitbash` | exists |
| A dwarf town scene with siege entities (not proven in a siege) | `SceneObj\taom_dwarf_town_01_forceatmo` | 6 `WallSegment` scripts, 2 `CastleGate` scripts, the gate prefabs `empire_castle_gate_outer_l3` and `empire_castle_gate_inner_l3`, 2 `dwarf_gate_a`. No row of the live `settlements.xml` names this scene, and no module XML does (a second search by the fact check). `docs/features/ered-luin.md` proposes it for another dwarf town and says that it needs his check in the editor: an open decision |
| An Erebor town scene in work | `SceneObj\taom_haterade_erebor_town_z` | 0 `WallSegment`, 0 `CastleGate`, 2 `sturgia_castle_gate_outer_l3`, 1 `dwarf_gate_a`. No settlement row names it either. The changelog calls it a stub. It is the maintainer's own scene by its name: ask him what it is for |
| What the Erebor settlements load today | the live `ModuleData\settlements.xml`, 52 rows with `Culture.erebor` | Vanilla Sturgia scenes only: 4 towns (`town_E1` to `town_E4` on `sturgia_town_a` to `_d`), 9 castles (on `sturgia_castle_003`, `sturgia_castle_c`, `sturgia_castle_siege_001`), the villages on `sturgia_village_*`. The Dale town (`sturgia` culture) loads a Sturgia scene too |
| Other dwarf prefab packs | `Prefabs\fink_dwarf_prefabs.xml`, `shogun_dwarf_prefabs.xml`, `dwarf_armory.xml` | exist; not surveyed by me |

What follows from the table:

- **The great gate does not exist yet.** The kit has a castle wall with a gate opening, two statues, walls, cliffs
  and rocks. Survey first what of it can serve (the statues, the rune pieces, the stone sets), and what the two
  dwarf scenes built from it.
- **The gate needs a scene, and that is the maintainer's decision.** No Erebor settlement loads a dwarf scene
  today. Ask him which settlement the gate is for (the name of `town_E1` in the game data tells you which town is
  Erebor: read it), and whether it goes into his scene `taom_haterade_erebor_town_z`, into a new scene, or stays a
  kit piece for now. Using a scene is an edit of a `scene_name` in the live `settlements.xml`: his step.
- **The dwarven kit is in use.** Other scenes and prefab packs place its meshes and set its materials. Never change
  one of its files in place; new pieces get new names in new files beside the old ones. A new material that is
  named like an old one binds to the old one with no warning.
- **The frame rule of the Erebor kit** (the engine reads each of its FBX files turned 180 degrees about Z from
  Blender's import) is proven in `docs/kitbash/erebor/design-patterns.md`. Our own generated kit files follow the
  Rivendell wall's proven export; label the frame on every coordinate that you write.

**His references: ten pictures in the folder named at the top.** What I saw in them (my reading, for you to check
against the pictures; every size is an estimate until you measure it):

1. `ref_01`: a film still, the front from the left. A tall front of green-grey stone stands in a recess of a cliff.
   Three bays with stepped, canted arch heads; rows of small square slots; galleries with close-set merlons at
   several heights; a lattice panel over the middle door. At the foot each bay ends in A ROW OF STANDING CARVED
   FIGURES, shoulder to shoulder, with a small arcade above the row and a small golden door in the middle bay: it
   is the base of the front, not a wall in front of it. A causeway leads to the door over a gorge, and water falls
   in the gorge below. Left: a colossal dwarf warrior carved from the cliff, in armour of hexagonal scales, with a
   faceted helm and an axe. Right: its twin on a slant against the cliff, with another weapon (a hammer, as far as
   the picture shows); no break is visible. A road climbs along the right.
2. `ref_02`: another design, a concept picture. A pale front of tall tapering fins with pointed heads and carved
   panels, one pointed door, and in front of it stepped terraces with low dry-stone walls and a broad stair. A crowd
   at the foot gives the scale: the front is many tens of metres high.
3. `ref_03`: a film still, square on, in shadow. The same front as `ref_01`: the three bays, the galleries at
   several heights, and a causeway that runs straight to a door that is tiny beside the front. How far the tiers
   step back cannot be seen from this side. Right: the statue on its slant.
4. `ref_04`: a photograph of a sculpted model, square on. THE WHOLE THING AS ONE OBJECT: a mountain peak of rough
   brown rock, the green front set in a deep recess of it, a statue on a plinth at each side, small trees, and a
   track to the door. This is the clearest picture of "one piece with the mountain in it".
5. `ref_05`: a render of another maker's model for printing, lit from inside. A compact gate in a rock: a double
   door with geometric panels that fills the whole lower bay, a ramp in front of it, and THREE CRENELLATED GALLERIES
   one above the other, each with merlon posts, the lowest directly over the door; a statue with a long beard at each
   side.
6. `ref_06`: the painted form of such a model, close. Grey-green stone with bronze and orange inlay: knotwork
   panels on the merlon posts and the central pier, lattice grilles in the openings, the rock closing over the top.
7. `ref_07`: the sculpted model from the right: how deep the recess is, how the rock wraps the front, the base.
8. `ref_08`: a statue close, from the side: the scale armour in hexagons, the banded shoulder plates, the faceted
   helm, the two-handed axe, the octagonal plinth. It carries a title and a maker's mark.
9. `ref_09`: the right statue against the lower front: the canted arches with their grilles, the rows of slots, a
   buttress with a flat head, the row of standing carved figures at the foot, and two tiny figures on the ground
   before the door: the only scale cue with figures (the door reads as roughly 5 m high against them, a rough
   estimate from a model). It carries the same marks.
10. `ref_10`: the foot of the front from above: the door bay between small piers, the stepped plinths, a track to
    the door and a stream beside it.

What they share: a monumental front in green-grey stone with a verdigris tone, strictly symmetric in its
architecture, vertical, in three bays, made of straight lines and canted corners (no round arch); galleries with
merlons at several heights; bands of small slots; lattice and knotwork panels; a row of carved figures at the
foot; two colossal warrior statues that are part of the cliff, alike in armour and not a mirror pair (their
weapons differ from picture to picture); a causeway to the door; and the mountain around all of it. In the film
front the door is small beside the front; in the compact model of `ref_05` the door fills the lower bay: these are
two designs, not one. The film stills show the right statue on a slant; the models show both upright. `ref_02` is
a third design language (fins and pointed heads): ask him whether it is a variant that he wants or only a mood.

What they mean for section 2: no picture shows an outwork in front of the gate mouth, so reading A is our own
addition. Reading B fits the film front at the arcade over the row of figures, and the compact model's lowest
gallery directly over its door: measure each height against the 12 m of the machines. The high galleries are
reading C's upper line. The terraces of `ref_02` are a fourth reading: a defended approach. No picture shows a
stair, a way onto a gallery from behind, the passage behind the door, or the top: every way up and every room is
yours to design.

**A reference is a target for our own design, never a source to copy part for part.** `ref_04` to `ref_10` show
models that other makers built and sell. No picture of another maker's model is traced, and no part of ours may be
a copy of a part of theirs (the kit prompt's Phase 3; `feedback_asset_art_rules.md`). Design the front from the
common language above with your own rule for each member. The pictures are screen captures: each has a viewer
button in a corner; cut it away before you measure a colour.

## 4. How the owner works (each line cost us rounds)

For this job he added: "Precision and master level carving of the 3D objects is critical for this one." Read the
lines below with that weight.

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

## 5. The method that he confirmed on 2026-11-26

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

## 6. Carving of a master's quality: how to reach it, and how to prove it

His word is "master level carving". What we learned on what makes carved stone read as carved, and what made it
fail:

- **Exact first.** The work in his references is geometric: straight arrises, canted corners, true planes, exact
  repeats, strict symmetry. Build it by code: every repeat from one rule, every mirror exact to the vertex, every
  mitre true, every curve inside the turn limit (4 degrees between neighbour faces of a smooth run; a corner is a
  declared corner). A chamfer or a bevel on every arris that the eye comes near: a knife edge reads as a model.
- **The trim sheet is the proven way to carve.** Every moulding, bead, band and pattern that repeats along a run
  is modelled once as a high model on a strip of one sheet, and the sheet's height, normal, occlusion and
  curvature maps are baked from it; the colour map is made from those maps. A member takes its V from its strip
  and its U from the length of its run, so a band of any length repeats cleanly. The Rivendell reference page
  shows the sheet with its maps (its section 9). A front of this size needs more than one sheet: plan the sheets
  (for example the slot bands and mouldings; the lattice and knotwork panels; the scale armour) with the library
  of step 1 below.
- **Depth is geometry near the eye and a baked normal far from it.** A relief that changes the outline within 30 m
  is real geometry. Fine tooling goes into a normal map that is baked from a high model: the tools
  `tools/oneoff/blender_build_trim_highpoly.py` and `tools/oneoff/blender_bake_trim_reference.py` did this for the
  Rivendell trim sheet, and `docs/kitbash/tiling-texture-sets.md` holds the method with its traps (the green sense of
  the normal map, the height range, a band on a curved path). The Erebor kit's rune pipeline is in
  `docs/kitbash/erebor/runes.md`.
- **The exact skeleton by code, the organic skin from Tripo.** Statues, weathered rock, broken blocks and rubble
  are where code reads as boxes. Section 9 says how a Tripo model becomes a relief on our own exact geometry. A
  colossal statue is a Tripo proposal with its cost, then a real retopology (the quad toolkit of
  `docs/features/cube-map-remesh.md`, never the remesher directly).
- **Scale tells.** A figure of 1.8 m stands in every picture. Block courses, steps and mouldings are sized against
  it: a course that is 2 m high reads as a toy at the gate and as a mountain from the valley. Decide the module of
  the stone with him early.
- **The gates of a builder are the measures of its critic.** Twice on 2026-11-26 a builder's sheet printed "0
  inside-out shells, 0 crossings" and an independent critic found 32 flipped faces a piece and ribs pushed through
  each other. A whole-shell volume test finds no single flipped face. So every builder holds these gates, each proven
  on a clean control (0) and on a planted fault (fails): the winding of each face against its neighbours, with the
  back hits of view rays from where a man stands; crossing pairs of parts; the turn of every edge chain; the seam by
  a slice just inside each joint; open edges; zero-area faces; the mirror error. A sheet prints measured values,
  never a parameter.
- **Judge as the engine draws.** Back faces culled, textured and lit, from the eye of a man on the ground, on the
  walk and at the far distances, in a low sun. Blender draws both sides of a face; the engine does not.
- **One piece until it is right.** His rule: one piece in one Blender window until he calls it perfect, and only
  what he marked changes after he praised a set.

The order of work that follows from this (a method, not only cautions):

1. **The library first.** From his pictures, draw the members that repeat: the canted arch head, the merlon post,
   the slot band, the lattice panel, the knotwork panel, the pier with its stepped plinth, a scale of the armour.
   One rule a member, with its sizes against the 1.8 m figure.
2. **One carving standard, chosen by him on a test panel.** Build ONE bay of the foot (about 6 x 8 m, with a
   figure) three ways: geometry only; geometry with a normal map baked from a high model; a Tripo relief laid on
   the exact core. Show the three in one Blender window from 2 m, 8 m and 30 m, textured and lit, with the
   triangles and the texel density of each. He picks the standard, and the budget of the whole front follows
   from the measured panel.
3. **The statues are their own track,** started early: they take the longest and decide the look from far away.
4. **Then the front, tier by tier from the foot up,** each tier from the library, each with the gates of the
   critic before he sees it; the rock last, round a front that is right.

## 7. What exists to reuse

Work folders are under `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\`. Make `erebor_gate` there, with `refs`,
`kit_full`, a state file and your own `_tools`. Copy a tool into your `_tools` before you change it (a repo tool
too: other kit sessions use those files); call in place only what this table marks so.

| What | Where | Use for the gate |
|---|---|---|
| The scene reference and its census scripts | `docs/kitbash/settlement-scene-needs.md`; `kingdom_kits\_shared\scene_needs\scripts` | The siege facts, and the scripts that count a scene's siege content |
| The Erebor catalog and its tools | `docs/kitbash/erebor/`; `tools/oneoff/blender_catalog_erebor.py --kit erebor`, `tools/oneoff/kitbash_kits.py` | The control group, the stone sets, the statues, the runes |
| The Blender lock tool (eight slots, a memory ceiling) | `kingdom_kits\rivendell\_tools\blender_one.py` | CALL IN PLACE for every headless Blender job, so that all kit sessions share one limit. Call it with `--wait-for <the file that the job writes last>` and read that file: its exit code alone proves no job |
| The castle code: the solid wall, the broken wall with its tear, mound and bodies, the towers, the gatehouses, with their gates | `kingdom_kits\rivendell\build\v07\_src_castle` (one tree; `CASTLE_TREE.md`, `tools\oneoff\castle_gate.py`) and `build\v07\_src_b` (the broken wall, in repair on 2026-11-26) | READ as the pattern of a siege piece by code: the joint profile, the levels, the bodies, the gate script that proves a tree after each change. Not for copy as a whole: its forms are Rivendell's |
| The base methods: arch outlines, a swept rib, a plate with holes and a chamfer, a turned solid, placement along a walk | `kingdom_kits\rivendell\kit_full\v2_base` (`CHECK_R3.md`, the interface notes; round 4 in work on 2026-11-26) | COPY AND REPOINT for mouldings, bosses and turned members. They load modules of the Rivendell trees by default |
| The kit set tools: a kit file, the manifest, the copy into the module with hash checks and an undo list, the check after his import | `kingdom_kits\rivendell\kit_full\editor_set\tools` | COPY AND CHANGE: they are wired to Rivendell (`KINGDOM`, a default folder). Run as they are, a copy aims at the Rivendell folder of the module |
| The prefab generator and the prefab survey | `kingdom_kits\rivendell\kit_full\delivery` (`PREFAB_OWNER_STEPS.md`, `PREFAB_SURVEY.md`) | What a prefab holds and what a scene adds; the breach tree of 16 entities |
| The Modding Kit import plan | `kingdom_kits\rivendell\build\v07\kit_import_prep\KIT_IMPORT_PLAN.md`, `KIT_SET_IMPORT_STEPS.md` | Read before the first import |
| The massing library and the sheet makers | `kingdom_kits\rivendell\kit_full\design_v2\massing\_scratch\mlib.py` | COPY: the fastest way to a first sheet |
| The review page maker | `kingdom_kits\_shared\review_page\page_maker.py <HH:MM> <content json> <your work folder>` | One page for him: what changed, what you need |
| A window for him | Copy the `.blend` to a `view` folder, start `blender-launcher.exe "<file>"` (under `%LOCALAPPDATA%\Microsoft\WindowsApps`), prove the window with `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\_shared\tools\capture_blender_windows.ps1 <folder>` | The window is his from then on: never stop it |
| The Tripo CLI | `docs/features/tripo-cli.md` | Section 9 |
| How to take a reference result apart | `docs/kitbash/teardowns/`; the `/teardown` skill | A part that a checker or he rejects twice gets a reference and a teardown before the next round |

## 8. The Modding Kit and the engine: what was measured

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

## 9. Tripo (he calls it "tripoli"): when and how

Tripo is a service that makes a textured 3D model from one picture, from several views or from a text. Its
command-line tool `tripo` is installed and logged in on this machine. The owner on 2026-11-25: "We also have more
tripoli we can do, keep that in mind", and on the credits of the kingdom kits job: "dont worry abotu credits, we have
plenty". The full record is
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
2. **The folder.** `kingdom_kits\erebor_gate\tripo\refs` for the pictures and `kingdom_kits\erebor_gate\tripo\out` for the results. Give every run an
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

## 10. What went wrong before, so that it does not happen again

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
- On 2026-11-26 two builders reported clean craft that an independent critic disproved (section 6). A stair set was
  built on a ledge that no walk of the scene reached, with a landing over an 11 m drop. Every walk of your gate ends
  on a stair, a door or a walk; draw the walks in plan and section before any detail.
- A run that the usage limit stops leaves half-written files. Every brief says: if your files exist, a worker before
  you stopped; read them and continue. Write the record after each result.

## 11. Questions for the owner, and your first steps

Questions (one at a time, each shown as massing with a 1.8 m figure where a look is in it, each with your
recommendation):

1. **Where the siege line is** on the front: readings A, B and C of section 2 (A is our addition; no picture has it).
2. **What "one piece" means to him:** one prefab placed once (my reading), and how much mountain belongs to it.
3. **The door:** a working gate of vanilla size in a fixed monumental surround, or a great door on a skeleton of
   our own.
4. **Which state of the gate** he wants: both statues whole (the models), the right statue broken (the film
   stills), or both as variants; and whether the fins and terraces of `ref_02` are a variant or a mood.
5. **The scene:** which settlement, and which scene file.
6. **The texture size:** every new texture of the kingdom kits job is 1024 px. A front of this size may need a
   larger carved set: ask, with a measured texel density.
7. **Tripo and the image service** for this job: both spend his credits.
8. **Where the files go.** Layout rule 9 names the prefab file `taom_erebor_kitbash.xml` and the showcase scene of a
   kingdom, and for Erebor both exist and are read only for you. Propose a new prefab file, a new kit file folder
   under `Scenes\erebor` and their names, and get his word before the first build.
9. **"Like in the scenes":** the game's breach walls (my reading), or the gate broken as in a scene of his
   references, or both.
10. **The skirt** under the piece, against the Erebor kit's rule of no skirt below grade (section 2).
11. **How wide the front may be,** with the frontage plan of section 2.

First steps:

1. Make your worktree and branch. Claim the work: `python tools/work_claims.py claim erebor-gate --note "<what>"`.
   The repo asks for a GitHub issue before implementation; creating one is public: ask him once whether it is a new
   issue or part of #1600, and never name a bought source kit, a film or a studio in it.
2. Look at his ten pictures yourself, each one, before you read my descriptions of them. Write what you see as
   things that are built and used: the walks, the stairs, the doors, the battlements, the statues, the courses of
   the stone, the rock. Measure the front against a figure where a picture has one.
3. Read section 1. Check the table of section 3 against the module.
4. The control: measure one gate front of a scene whose siege works (the reference names the scenes) and the
   Erebor kit's castle wall, gate and statues: sizes, triangles by level, texel density, the entity tree.
5. Write the design language of the gate from his pictures: the forms, the module of the stone, the mouldings, the
   colour table with measured values, what is exact and what is free, what the pictures never have.
6. Massing as true geometry, with the siege line in its readings, the walks in plan and section, the machines of
   both sides placed as boxes of their measured sizes, and a 1.8 m figure. One sheet from fixed cameras and one
   Blender window. He marks it.
7. Then the fine details beside his marks, the build, the levels and bodies, the prefab with its siege entities, a
   siege test scene (count the walls, the gates and the ram by hand), his import, and the look and a siege in the
   game. A siege that was not played in the game is not proven.
8. Records: your own build record `docs/kitbash/erebor-gate.md` from the first result on, a state file in your work
   folder, and each new scene fact into the scene reference.

## 12. Limits, and what is not handed over

- Nothing enters a game module without his word for that step. A prefab that names a mesh that he has not imported
  can crash the Kit: a prefab file goes in only after his import, with the Kit closed, on his word.
- No commit, no push, no purchase without his word. The image service, Tripo and downloads are his to allow for
  this job.
- Never name the bought source kit of the Rivendell conversion ("a licensed elven building kit"). Never put his
  pictures, his name or his address into a request to an outside service.
- The Blender windows, the Modding Kit and Painter on this machine are his or another session's: stop no process
  that you did not start, and close no window. Painter is one shared instance: claim `painter-instance` first.
- All work files on `E:`. Every new texture 1024 px unless he says otherwise.
- Everything that exists under `Scenes\erebor` in the live module, the Erebor prefab file and the dwarf scenes are
  read only for you. The live module is not under git: a change there needs his word and a check in the repo in the
  same change.
- Other kit sessions run: Rivendell (work claim name "Rivendell Kit Bash"; its state is the top of
  `kingdom_kits\rivendell\painter_lab\STATE.md`), Mirkwood and Lothlórien. You write new files only
  (`docs/kitbash/erebor-gate*`, the `erebor_gate` work folder, your own worktree). For a change in a shared file
  (the scene reference is the exception: add facts to it), ask the maintainer or message the session that owns it.
- A test, a trial run or a planted fault never names a live game path as an output, a working folder or a target.
