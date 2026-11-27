# Blender asset gates

## Overview

Two checks that a mesh passes in Blender before it goes to the Modding Kit, and the skill that
runs them. `tools/blender/mesh_preflight.py` reads a mesh the way the export will write it and
gives one verdict. `tools/blender/qa_views.py` renders six fixed views with back faces culled, the
way the engine draws them. The [`/blender-asset`](../../.claude/skills/blender-asset/SKILL.md)
skill holds the rules for a live Blender session, sends each task to the workflow that owns it,
and runs the two gates. This is tooling: it changes no gameplay.

## Why This Exists

TAOM's Blender rules were strong where Bannerlord is particular (collision bodies, LODs, texture
packing, retopology selection, hair, rigs, clips) and thin in the layer around them.

- **Before:** the mesh checks lived inside six tools, each for its own asset kind (hair finalize,
  the rig export, the UV unwrap, the Erebor assembler, the LOD audit, the mesh reference
  validator). A prop modelled by hand, or changed over the Blender MCP, passed through none.
- **The failures this would have caught:** every box of the Carn Dûm icon wound inward through
  nine versions, because Blender draws back faces and the engine culls them; a material named
  `x.001` that the Kit binds to nothing; a missing `bo_` twin, which hangs the mission load (#352).
- **Also missing:** one place for the session rules of the Blender MCP, a standard set of views,
  a procedure for matching a reference picture, and a table that says which workflow owns which
  Blender task.

The idea of a routed pipeline with a validation gate and a screenshot-backed verdict comes from
[arjun988/blender-skills](https://github.com/arjun988/blender-skills) (MIT). Its text was rewritten
for Bannerlord and its engine rules were not taken: see
[the review](../reviews/adopt-blender-skills-2026-11-13.md).

## Architecture

```
/blender-asset (skill)
   |-- session rules, routing table, brief
   |-- Gate 1: mesh_preflight.py     pure rules  <- extract_mesh (bpy, bmesh, BVH rays)
   |             headless: --fbx | --blend -> <out>/_report/DONE.txt
   |             live:     preflight_scene(names) -> dict (read-only)
   |-- Gate 2: qa_views.py           six PNG files, back faces culled (headless only)
   '-- verdict: SHIP | SHIP WITH NOTES | NO-SHIP
```

**Design choices**

- The rules are pure functions over plain records, so `tools/tests` covers every rule without
  Blender. A thin layer reads the records from Blender. That layer is proven by the canary below.
- A mesh is read as the export will write it: modifiers applied, in world space, a mirrored
  object's faces reversed. The Kit's own frame (an FBX read back at rotation X 90 and scale 0.01)
  is uniform and is accepted.
- No commit gate. The check needs Blender, which is not on every machine, and most FBX sources
  live in the unversioned live modules. The skill step is the trigger.
- LOD chains stay with `tools/audit_fbx_lods.py`, UV fold-over and texel density with the probes
  of `tools/oneoff/blender_prep_tripo_prop.py`, name references with `tools/validate_mesh_refs.py`.

## The checks

A check is a blocker only where TAOM has a recorded failure or gate for it, and a major where a
recorded trap applies. The counts taken from the source's generic checklist are notes.

| Severity | Code | What it finds | Why (evidence) |
|---|---|---|---|
| Blocker | `faces-inverted` | Over 5% of the surface is seen only from behind | The Carn Dûm boxes ([lesson](../reviews/lessons/build-tooling-workflow.md), "A renderer that draws back faces hides a mesh wound inside out") |
| Major | `faces-inverted-suspect` | 0.1% to 5% of the surface is seen only from behind | Geometry cannot tell a hole from a face with another face right behind it; Gate 2 decides (see "Measured") |
| Note | `faces-inverted-few`, `faces-inverted-interior` | Under 0.1%, or any share with `--interior` | Crevices between overlapping shells (175 faces on Carn Dûm after the fix); a hall or cave is seen from inside |
| Blocker | `material-duplicate-suffix` | A material named `x.001` | The Kit binds by exact name and renders an unknown name unbound ([armory-guide.md](../reference/armory-guide.md), "Materials on a re-import"); the Erebor assembler refuses the same |
| Blocker | `uv-missing` | A visual mesh with no UV layer | A material has nothing to map; the hair and groom gates already refuse it for their meshes |
| Blocker | `body-missing` (`prop`) | A visual mesh with no `bo_` twin for its prefix | #352, the infinite mission load ([mesh-ref-validation.md](mesh-ref-validation.md)) |
| Blocker | `skin-unweighted` | A vertex with no weight in a group named after a bone | `tools/blender/export_rig_for_kit.py` refuses it. That gate is stricter: it first drops weights under 0.01 |
| Blocker | `skin-too-many-bones` | More than 64 bones | `Skeleton.MaxBoneCount` ([spider pipeline](spider-skeleton-animation-pipeline.md)) |
| Blocker | `no-meshes` | The file holds no mesh | An empty input must not read as a pass |
| Major | `material-empty-slot` | Faces with no material, or no slot at all | Inferred from the same binding rule; not observed |
| Major | `uv-collapsed` | Over 1% of the surface has UVs with no area | #760, 118 of 223 hair cards |
| Major | `transform-mirrored` | An object with a negative scale | Baking it flips the winding ([pipeline doc](../reference/ue-to-bannerlord-asset-pipeline.md), "Blender headless staleness"); what the Kit does with it unbaked is not verified |
| Major | `transform-multi-user` | Shared mesh data on a moved object | `transform_apply` skips it silently (same section) |
| Major | `skin-over-four-influences` | A vertex with more than 4 weights | The Kit keeps the 4 strongest, so the game bends the mesh differently from Blender |
| Major | `name-duplicate-suffix` | An object named `x.001` | The Kit reads `<mesh>.<part>` as a sub-mesh ([armory-guide.md](../reference/armory-guide.md), "Naming") |
| Major | `body-no-physics-material` | A `bo_` with no named slot | The body carries its physics material as a slot name (`stone`, `wood`) |
| Major | `icon-has-body` (`icon`) | A `bo_` in a map icon file | The settlement entity has its own ([map-icons.md](../kitbash/map-icons.md)) |
| Major | `over-budget` | LOD0 triangles over `--budget-tris` | The budget is the asset it replaces (`/prep-tripo-prop` Phase 1) |
| Minor | `material-unused-slot` | A slot no face uses | It reaches the Kit as an empty sub-mesh |
| Minor | `transform-non-uniform-scale` | Scale differs by axis | No recorded failure |
| Note | `name-upper-case`, `uv-extra-layers`, `topo-non-manifold`, `topo-loose-verts`, `topo-zero-area`, `topo-ngons` | Counts | No recorded failure. The Kit lowercases names, and an XML reference in another case finds nothing |

**Limits of the inverted-face rule.** A face counts when no ray leaves the mesh along its normal
and rays do along the reversed normal. The share is a lower bound: a flipped wall that sees out
through a window is not counted (100 of 320 flipped faces on the canary). Each mesh is tested
alone. Above 200,000 faces an even sample is read and the report says `inverted_sampled`.

**What is read.** Only meshes visible in the view layer. A mesh that is hidden or in an excluded
collection is listed in `skipped_hidden`. LOD0 is the bare name or `<name>_lod0`; a Blender `.001`
suffix does not change what a name is (`x.lod1.001` is still a LOD, `bo_x.001` still the body of
`x`), and is reported on its own.

## Art checks (the maintainer's standing corrections)

Run this list on the renders of a built mesh before the maintainer sees it. Each line cost a rebuild pass on
the Rhûn chariot work of 2026-11-24 to 2026-11-26, after he saw the fault in Blender. The words in quotes are his.

| Check | What fails it | How to hold it |
|---|---|---|
| Every strap, band and rope is attached at both ends | "The straps need to be attached to something": strap ends that stop in the air beside a post | The end wraps the rail, or lies under a plate with a rivet |
| Every fitting and every separate piece connects | "They dont connect to anything": two hip discs with no strap between them; a chest piece alone on the chest; a small scale panel alone on a shoulder | A disc or buckle is a junction of at least two straps. A plate or a garment hangs from straps that go to another part |
| A strap end is sewn on, and a crossing is joined | "Attach both ends with stiches and not on the button", "Straps again.", "Attach these reins or horse head armor straps better.", "1 should have a buckle here tying it all together": a strap that ends on a stud of a belt or of a metal plate; two strap ends in the air at the poll; bridle straps that cross on the cheek with nothing at the crossing | On leather the end laps 3 to 4 cm onto the part, between the studs, with a stitch box. On a metal plate it goes through a ring or buckle with a riveted tab and is sewn back onto itself. A stud sits only where two or more straps cross, and a crossing of bridle straps has a ring or buckle, the same on both sides. The small head straps are held to the same standard. A strap end sewn onto leather (a belt, a binding band) takes the weight set of that leather, so the two move as one. An end pressed onto plates cuts through them when the neck turns: on horse armour 5 v18t four strap ends seated on plates gave 19 strap and plate face pairs in the two neck turns |
| A strap that meets a helm or a rigid shell goes under it | "THis needs to go under the helm": a cheek strap that lies on top of a horse's head shell, with its stitch box on the plate | The strap comes out from under the shell's rim, the same on both sides, and no strap or stitching lies on the shell's outer face. Cut the hidden part off just inside the rim, so nothing clips. The strap is under the shell along its whole run: test the stretch between its path points too, and from below the strap ("Both sides the tab should be under...": a neck strap lay under the last neck band at each of its points and rose through the band between two of them) |
| A joint looks made | "Connection needs to be way better", "Connection needs to be refined here (red)": a strap that ends in a small buckle sitting on a scale row; two studs that cut into each other; plate tips that poke above the strap that carries the panel | A panel has a clean top binding band that covers the tops of its plates, and the strap is sewn onto that band |
| A hanging panel is carried | "Front armor just hanging and not tied to anything", "Also just hangs", "Very very cool but the front armor is still not connected to anything.": a chest panel or a front skirt with a top band and nothing above it | At least two suspension straps from the top band to a load-bearing part (neck guard, back plate, girth), with a made joint at both ends, visible from the side and the front |
| A strap or band lies on the plates, never through them | "clipping", "clippings between the strap and scalemail", "on both sides": a strap that cuts the top edge of the first neck lame at the poll; a binding band and a diagonal strap that cut through the top scale row of a shoulder | The strap or band lies 2 mm or more above the highest plate rim. A gate counts face pairs between straps or bands and plates or fittings at rest and in the poses, on every LOD, target 0 (`rharm_strapplate.py`: horse armour 1 had 2,237 pairs at v28 and 0 at v29). Look at every place where a strap, band or belt meets a plate field, on both sides |
| Armour covers what it sits on | "Pull out the neck just a bit so the horses neck is covered": skin that shows between the left and right neck lames along the crest | Look from above and behind, at rest and in the poses; no skin between parts that are meant to close |
| No part clips the body | "Pull this out as it is clipping": a chest plate that cuts the neck | The skin test on the part, at rest and in the rig's poses |
| A neck piece is a clean row of bands that stands off the neck | "This neck piec e needs to be cleaned up. It needs to be pulled back a bit from the neck and there is clipping.", "Black neck piece needs to be reworked. it is all off", "we do need to fix the horses neck armor as it isnt straight it is overlapping.": skin through the gold lames; lower edges with steps and notches; gold rims broken into offset pieces; lames that lie unevenly on their neighbours. "this is messed up and not straight": both ends of the lowest band as a gold spike with a notch beside it, seen from the front | Each lame is one continuous band with a smooth lower edge and one unbroken rim, the same left and right, with an even overlap. Every band ends in a clean rounded end in one line with the ends above it, and its lower edge runs straight to that end. A band is lifted only over parts it covers: a part that lies past its edge does not count. Look at the chest from the front at close range. No skin through or between the lames from any direction: the skin-view gate looks from 64 directions (see "A viewer shows the exported pose" below), not from the five axis directions of the first skin gate |
| An opening is clean | "The eye armor needs to be cleaned up": loose flat faces inside the eye ring of a horse's head plate | One ring, a clean cut behind it, a short inward lip, nothing loose in the opening, both sides alike. Check it again at each LOD with face culling on |
| A part on the centre line is symmetric and sits closed on its base | "Clean up the details, gaps and it should be symetrical? right?": air and a thin sliver between the foot of a crest fin and the head plate's rim; a blotchy fin face; two sides that differ | A closed socket, flush on its base with no gap. Symmetry proved by a number before he sees it (horse armour 1: the crest mirrors to 0.16 mm about the head's centre plane, lean 0.00 degrees). A close view of the foot of every new part from both sides |
| Plates sit on a backing | "I would remove as they are not attached to anything": single plates hanging in front of bare horse with gaps between them | A leather or cloth garment under the plates that shows as a border; plates tight on it; no gap wider than a plate is thick |
| Scale mail is single plates | "looks like a sheet of paper with scales printed on it": a smooth sheet with a scale pattern in the texture, in a few framed panels | Real overlapping plates at LOD0 and LOD1, a field edge scalloped by the plates, stepped rows at LOD2. The approved reference is the scale wall of the cart `rhun_war_chariot_d` |
| A plate field has no gaps | "Fill these gaps and make the scalemail straightish so it doesnt deform", "Fill out with scales in the middle like you did the sides": plates left out at a shoulder and a hip to keep a clearance, bare backing blocks with stepped edges, tilted rows, missing plates in the middle columns of a chest front | The field is full to its border; rows are straight and continue across the field; each plate is rigid. A clearance is met with tight, thin plates, not with a hole |
| Rows are tight and the backing stays behind the plates | "2 - Spacing needs to be tightened for gaps, connection", "Gaps and still not connected (front armor).", "Clipping and front armor not attached?": backing that shows in a zigzag between two rows of lamellae; a plate that lifts; red points of backing in front of plates | Each row overlaps the next so that no backing shows from the side, the front or from above; the backing shows only as the border. A count of backing faces in front of a plate face, target 0 |
| A seam between two cloths is straight | "this needs to be straight or just remove them and line up the top cloth to the side cloth": the lower edge of a back cover as a wavy line, with slanted, fanned plates along it and cut plate tips at both ends | First choice: one straight, level seam, with the lowest row of the upper cloth and the top row of the lower cloth on one grid (same pitch, columns in line, every plate upright). Second choice: the slanted rows removed and the two cloths lined up edge to edge under one straight binding. Both sides |
| Gold is a clean worked metal | "the gold texturing is very rough and looks bad": a coarse, lumpy normal-map noise | Nearly smooth faces, a fine mark at most, crisp bevels with an edge highlight, dome rivets. Match an approved asset's finish by measurement |
| Blades and weapon edges are steel or iron | Gold toned wheel scythes | A measured steel tone, with a brighter ground edge |
| Wood looks like wood | "could use better texturing to make it looks like wood": a smooth dark material with soft blotches | Real wood source textures ("we need to use more wooden sources"), projected along each part's grain axis; board seams, end grain |
| A plate's texture is not smeared | "Texture issues? Or something", "Weird texturing on those": blurred, wavy fluting on neck lames; a stretched pattern on large plates | The texel density of each part is measured and reported; every plate field is looked at at close range. Every visible surface has its own UV island with real texture: 0 cm² of visible surface with collapsed UVs or in a flat colour patch |
| Team colour areas are greyscale in the diffuse map | A coloured flag that cannot take the team colour | Grey cloth in `_d`, the mask in the second diffuse |
| The `_s` map's blue is ambient occlusion | A flat blue value per material (wood at about 160 lost its ambient light evenly and read dark and plain) | Blue is the baked occlusion, white where open, dark only in real crevices; the shader lines are in [substance-painter-mcp-setup.md](../reference/substance-painter-mcp-setup.md) |
| A set is one palette | "if the horse armor is black, the cart must be the same color. Black, gold trim and wood": a black scale armour in front of a gold cart | Judge armour and vehicle of one item together; they share the main colour, the trim metal and the wood |
| A set he praised changes only where he marked it | "Once we fix the issues this cart set up is AWESOME" (cart D with the lacquer horse armour 3) | Fix the marked joints and what the connection standard needs; the rest of the look stays as he saw it |
| The build matches its approved concept, at the size he asks for | A build "plainer than the concept": a low flat crest, thin signature parts. "Instead of a feathered helmet, I would just replace with a golden fins", then "make fin bigger, but v24b raider is good": narrow fin blades built from a brief that gave no width | Measure each signature part in the picture against the head length, and build it at that size. When he names a shape, the brief gives a width and a height; when he draws an outline, build to the outline |
| A build to a picture he approved takes its shape and its surface from that picture | "LOOKS AWESOME!" on the reference picture `01_black_neck_front.png` (in `out\concept_art\tripo_refs\` of the chariot work folder), a tile on a sheet beside the Tripo model made from it: his word was on the picture, not on the model. The horse armour 5 neck trial v17 differed from that picture in seven points. The models themselves were first measured a day later ([technique-library.md](../reference/technique-library.md), teardown T1) | The picture is the look target. Measure the shape on the picture (on 2026-11-25 a picture probe gave a gold bead of about 0.2 of the band pitch, so about 13 mm at the build's 65.6 mm pitch, the width he then approved on the build; that probe could not be repeated, and the two models measure 0.147 and 0.167: [technique-library.md](../reference/technique-library.md) L2 and L10) and set the surface from it (the session's pin from the picture's gold hue: gold `_d` median (170, 142, 88), gloss 215; black plates (16, 16, 17), gloss 200 on the bands and the head shell; his eye decides on the render). Show him the picture beside the build from the picture's own camera, at LOD0 and LOD2 (`rharmb_refsheet.py`) |
| Every part has a purpose | "This serves no purpose and wont ever be used, waste of triangles and polys remove it.": a fan ornament on the inside of a cart's front wall | No ornament where a player does not look: the inside of walls, under a floor, behind other parts |
| A part has a back where one can see it | "doesnt have a back like the front does": fan ribs with gaps one looks through from the rider's side | A closed, finished panel behind them |
| A vehicle that carries fighters is open | A roof, a rack over the floor, walls with slits | Open above and behind; nothing over the floor |
| A pointed part ends in one fine point | "These should end with a fine point.": crest fins that end blunt, with a thin needle on the blunt end | Width and thickness run out together toward the tip, so the outline narrows to one point |
| Lines that should be straight are straight | "2 should be straight not bent": a neck strap with a kink and steps just before its buckle; strap and lining lines that wander | Level them by measurement; a check of every strap for a kink between its joints |
| Rigid parts stay rigid in motion | A disc that doubles its length in a pose (found by the pose test, not by him) | One weight set per rigid island; edge lengths at 0.99 to 1.01 in every pose |

**A clearance gate must not be met with a hole.** On horse armour 5 two gates (a gap to the cart pole, and no
contact with the cart's forks in a neck turn) were met by leaving plates out. The session accepted v15 because
all its gates passed and listed the bare patches as open points. The maintainer rejected it at first sight.
When a clearance and the coverage of a part pull against each other, show him the trade-off before the build is
called done. The next version filled the zones in straight rows (410 plates, none left out), with the pole gap
allowed down to 0.050 m in the hip zone; it measured 0.055 m.

**One asset at a time, one Blender job at a time (the maintainer, 2026-11-25).** "We have like 36 blenders open,
can we close some?", "LEts also work on one blender item at a time to save memory" and "Lets work on one cart and
one horse armor set, when that is perfect move onto the next one". At 13:07 that day two armour builders, a
review of five carts and another session's work each ran their own Blender jobs: 49 Blender processes
held 37.8 GB of the desktop's 61.6 GB, with 4.2 GB free. Work one set to his approval before the next starts,
and run one Blender job at a time; fan out only work that needs no Blender. Since the log entry of 16:48 that
day, every Blender job of the two builders starts through `blender_slot.py` in the root of the chariot work
folder: it waits for a free slot, runs the command, and frees the slot when the command ends, also when it
fails (tested at 16:47: a second command waited for the first, an exit code of 5 passed through, the slot was
free after each). With it the cart run could start while the armour build went on. At 17:32 it gained fair
turns: after one owner's turn, a waiting process of the other owner goes first, so one builder's chain of jobs
cannot keep the other out (tested on a copy: cart, armour, cart, cart). In the evening he widened the rule:
"You can use more blender, just watch out for the memory." The number of slots is a setting since 18:32
(`out\_blender_slot_config.json`: 3 slots, 14 GB; tested on a copy at 18:32: three jobs ran together and a
fourth waited, and with a floor it could not meet a job waited and gave up with exit 97), and at 18:32 a job
started only while a slot was free and the free physical memory was at least 14 GB plus 3 GB for each of the
folder's jobs that started in the last 90 s. When none of the folder's jobs ran, 6 GB free was enough, so
another session's load could not hold the work for ever. At 19:24 the setting went to 6 slots and a floor of
10 GB free (`out\_blender_slot_config.json`, read 2026-11-26: 6 slots, `min_free_gb` 10), after his words "We
have a lot of room for CPU and Memory." The 3 GB for each job younger than 90 s and the 6 GB with
none of the folder's jobs running are constants of the tool (`YOUNG_GB`, `ALONE_GB` in `blender_slot.py`), so
they hold with the new setting too. The command must block until its Blender job has ended (the Store launcher
does), and `run` exits 97 when no slot comes free within its wait (6 hours by default).

**Near is not connected.** The link gate below measures distance only. Horse armours 2, 3 and 4 passed it with 0
unlinked islands, and the maintainer still marked their joints: a strap that ends on a stud, a panel that
touches a strap but is not carried by it. The gate finds parts that float; the rows on strap ends, joints,
hanging panels and tight rows are judged on close views of every joint, before he sees the build.

**A gate for "nothing floats".** The first two rows can be measured. List the connected islands of the mesh; link
two islands when their surfaces come within 4 mm; require every island to reach an anchor (a part that goes round
the body or lies on it over a wide area, or the vehicle's body) through such links, and every strap to be linked
at both ends. The count of unlinked islands and of loose strap ends must be 0. The chariot agents' pipelines carry
this gate since 2026-11-24 (the chariot work folder under `E:\LOTRAOMAssets\_auto_workspace`, not in the repo).

**An independent review before the maintainer sees a build.** The builders' gates passed on every armour version
he rejected on 2026-11-25, and the session's own reads before a showing covered only his last marks. Since then a
read-only review runs on each armour version before he sees it: four reviewers with one scope each (straps and
joints; plates, crest and neck piece; cloth, fringe, textures and LODs; the whole armour against his list of
rejections) render and read their own views at rest and in four poses, and one checker per scope tries to
refute each finding and adds what was missed. The first run, on horse armour 1 v29 (8 agents, 0 lost), read 381
views: 60 findings confirmed (26 seen at normal distance, 34 only close up), 3 refuted, and 20 added by the
checkers, so 80 visible faults on a version whose gates all passed. What the gates did not see: a crest holder
that hovers over the poll with daylight under it; loose tabs inside both eye rings; a noseband 22 mm off the jaw
with its buckle in the air; rings that lie flat on the straps with no strap through them; skin wedges between
the end corners of the neck lames; dark squares where faces are seen from behind after mirroring; rump straps
that pass through each other; red, smeared edge faces on the cloth; and, with face culling on, a block on the
back cloth at LOD2 and LOD3, lames that pierce each other at LOD1 and LOD2, and eye openings gone at LOD3. The
session sorts the findings into required ones (seen at normal distance or in motion, the LODs, close range) and
an approved look that it reports to him as a list. The run did not cover LOD4 and LOD5 in pictures, the leg
poses, the cart or the game's own shading. Since the memory incident above, the review runs one reviewer at a
time. Since the weekly usage limit stopped every agent at about 20:30 on 2026-11-25, no reviewer agent has run:
armour 5 `v18aq`, `v18av` and `v18ay`, in sets v23, v25, v26 and v27, were shown to him without this review (the
work log of 2026-11-26: "No reviewer has looked at set v27"). On 2026-11-26 at 09:22 set v27 went into the
Armory's `AssetSources` on his word ("put set 27 into the asset sources. This is amazing") with its gate table
HELD on four rows (the count flag, the 2 hidden hub discs, the armour's class a faces, the cart's UV check at the
far levels); he chose the import before the cart's far levels and hub discs are repaired, and imported the set
in the Kit at 09:36 to 09:38 ("done, looks good!").

**A viewer shows the exported pose.** The source armature of the horse armours carries a saved pose on three bones
(`horseneck1`, `horseneck2`, `horserleg2`). The builds exported it, so every viewer showed the armour bent away
from the horse at rest, while the gates reset the pose before they measured and saw nothing: the skin gate
reported 0 and the maintainer saw skin through the neck lames. On horse armour 1 v27, 955 of 10,500 neck-lame
samples lay inside the skin (worst 8.61 mm) with the saved pose, and 0 with it cleared; on horse armour 5 the
neck guard was bent by up to 24.8 mm. The fix: both armour builds reset every pose bone before the export, and
say so in their build report (`source_pose_reset`, `pose_reset_bones`). The skin-view gate (`rharm_skinview.py`)
casts rays from 64 directions in six cases: the saved pose against the horse at rest, rest, neck turns of 25
degrees each way, head down 30 degrees and head up 15. It gave 4,252 skin samples on v27 and 0 on v28. A gate
that changes the file's state before it measures also measures the file as exported.

**Review prepared scripts before a long run.** For the repairs of cart F the cart builder first prepared script
edits for 19 of the 21 review findings with no Blender job (Part 1); Part 2 was one runner, `run_f_review.sh`
with steps 0 to 9, through the build, the bake, the Painter wood and the renders. Before Part 2 a read-only
review read the scripts (workflow `wf_ce375cfc-246`, 8 agents, 0 lost, no Blender): four reviewers with one
scope each (the yoke measure and weights; the see-through repairs and the new see gate; regressions in the
scripts that also build carts A to E, the LOD change and the runner; the Painter wood pass, the three wood
colours and the lining planks), and one checker per scope that confirmed each finding from the files or refuted
it. It confirmed 29 findings (4 blockers, 9 majors, 16 minors), refuted 6, and found 4 of the reviewers'
"verified ok" entries untrue. Three blockers would have stopped Part 2 with nothing to show: the swap step took
its cart work files from a version folder that holds none of them; the wood compose read a density file that
such a folder does not have; the mask proof demanded a share of exactly 1.0, which each of the pilot's four
finished Painter runs misses by 2 to 12 texels. The fourth would have built a wrong rig: new yoke weights with no
part filter moved the inner fork prongs from their necks to the pole. A second read-only run re-checked the
fixes and traced every step of the runner: 23 of 24 rows fixed, one not (the control did not require each
known slit), and new defects, among them an open sliver at the front of the outer floor boards (in v14 too), a
wrapper timeout that could never fire, and a name check that could stop the run on a chance match in
compressed bytes. The record is `out\rhun2\_work\f_review_check\CONFIRMED.md` and
`out\rhun2\_work\yoke_design\RESULT.md` ("RE-CHECK of the fixes") in the chariot work folder.

**Measure a rig change on the game's clips.** The first review of cart F saw the two yoke halves split at the
centre in a synthetic neck turn of 20 degrees. Before any weight change, the yoke was measured on the game's
own clips in plain Python with linear blend skinning: a scout wrote one shared harness
(`out\rhun2\_work\yoke_design\facts\harness.py`: F's design vertices, the horse bodies' real meshes with their
own skin weights, 39 clips and 1,784 frames), three designers each measured one scheme with it, and one checker
per design recomputed it with its own code. Three points came out of it. The split on v14 is cart F's own: the
nearest-neighbour weight transfer gave F's bar and seat neck weights (own neck 0.785 at |x| < 0.08), while cart
A in the game carries its yoke centre on the pole bone (0.98 to 1.00). A metric can read 0 by construction: the
builder's first scheme put every vertex at the centre on the pole bone alone, so its centre split, seat and
lashing readings were exactly 0, while the inner prong sockets tore from the bar by 100 mm in the gallop; a
tear reading says something only between parts on different bones. A bound needs a reference: the builder's
own bounds failed every blend window it tried, and the asset that runs in the game gave the bound. The session
chose scheme S* (the middle of the yoke with both inner prongs as one stiff piece on the pole bone, each inner
prong 2.7 cm shorter at its top) because its bar flexes less than cart A's (gallop edge change 46.0 mm against
cart A's 118.5 mm, the checker's estimate), and the gate's bound for the bar's arm root in the two gallop turns
is 25 mm because cart A's own bar reads 18 to 25 mm there in the same measure (S* reads 21.2 mm). The limits
left (the turn-in-place clips, the quick stop, the rear) go to the maintainer with their numbers when he sees
the set.

**A fresh builder for each block.** The armour 5 rework agent took the armour from v08 on at the night entry
of 21:25 and built it through the v17 trials to v18t. At 17:11 on 2026-11-25 it reported that its working
memory of the job was too long to finish v18 reliably, wrote the hand-over first (`out\armours\HANDOVER_agent2.md`:
the state and gate tables, a section "v18: what the next builder does" with the session's decisions, seven
blocks with the code place of each fix, the commands, the traps, and a "Dead ends" section), and started none
of the blocks. Since then armour 5 is built by one fresh builder for each block, with a read-only checker after
each block that opens every view, one retry for a block that fails its check, and the blocks one after
another; the session reads the views of the first three blocks itself before blocks 4 to 7 start. Block 1 (the
straps under the helm) ended in v18y with 0 strap and shell face pairs at LOD0 to LOD3 by the design's own
report. This held until about 20:30 on 2026-11-25, when the weekly usage limit stopped every agent; armour 5
`v18am` to `v18ay`, cart F v22 and v24 and the sets v21, v23, v25, v26 and v27 were built by the session alone,
without block builders or checkers.

## The views

`qa_views.py` writes six square orthographic PNG files, named by where the camera stands in Blender's
axes: `3q_front_left` (-X -Y, above), `3q_back_right` (+X +Y, above), `front` (-Y), `side` (-X),
`top` (+Z) and `3q_below` (-X -Y, below). The mesh is one grey, back faces are culled, and the
world is magenta. A hole shows as magenta, or as the inside of the mesh seen through a missing
wall. Only the visual LOD0 meshes are shown; `--bodies` shows the collision bodies instead. The
picture is square because an orthographic frame spans the longer side: on a wide picture a tall
mesh lost its top and bottom.

"front" means Blender's -Y and nothing more. An FBX's facing in game follows its kit's frame rule,
so a direction is judged from the axes.

Not in these views: textures (`tools/blender/render_painter_preview.py`), wireframes and armatures
(the live viewport, through the Blender MCP screenshot tool).

## Saved scene checks

When a project adds a copied scene in background Blender, keep a persistent
user on the scene and reopen the actual saved file. Resolve all render
scenes and cameras by name before a batch render. See the
[Mirkwood save failure](../reviews/lessons/build-tooling-workflow.md#a-copied-scene-can-render-and-disappear-on-reopen-2026-11-27).

## Verdict

Defect levels, from the source's QA gate: a **blocker** stops the asset, a **major** is fixed or
named with the evidence that it is harmless, a **minor** and a **note** are reported. The verdict
form and what SHIP means are in the skill, section 6.

## Reference analysis template

Fill this in before any change when Mike gives a picture to match.

```markdown
## Reference analysis: <asset>
Reference: <picture>            Existing file: <path, or new scene>
Match goal: <similar | close | these features only>

1. Subject: <kind, style, forward axis, size in metres>
2. Silhouette: <3 to 5 main masses>; proportions <share of length or height each>
3. Camera: <angle, lens feel, how much of the frame the subject fills>
4. Materials: <surface kinds, matte to gloss, wear>
5. Must have: [ ] <feature 1>  [ ] <feature 2>  [ ] <feature 3>
6. Differs today (existing file only):
   | Area | Reference | Now | Priority |
```

The build passes and the compare loop are in the skill, section 7. In each compare, rate frame,
silhouette, depth, materials and features as match, close or miss; each miss gets one fix.

## Key Files

| File | Purpose |
|------|---------|
| `.claude/skills/blender-asset/SKILL.md` | Session rules, routing table, brief, the two gates, the verdict form |
| `tools/blender/mesh_preflight.py` | Gate 1: rules, the Blender reader, the headless and live entry points |
| `tools/blender/qa_views.py` | Gate 2: the view set and the culled renders |
| `tools/blender/blender_job.py` | Shared: the report every headless exit writes, the FBX import |
| `tools/oneoff/kitbash_kits.py` | Shared: `split_name` (base, LOD, `bo_`) |
| `tools/evals/route-blender-asset/case.yaml` | Routing eval for the skill |

## Tests

- `tools/tests/test_mesh_preflight.py`: names, topology counts, the ray rule, every finding with
  the state that raises it and the state that does not, the file-level rules per profile, the
  verdict, the arguments, the report order.
- `tools/tests/test_qa_views.py`: the view set, the camera frame, the object filter, the arguments.
- Run: `python -m unittest tools.tests.test_mesh_preflight tools.tests.test_qa_views`.

The Blender side has no unit test. It is proven by the canary.

## Measured

Blender 5.3.0, 2026-11-13, on `erebor_dwarf_house_01.fbx` (approved in the Kit 2026-10-15) and a
copy broken by script (the faces of one wall material flipped, one material renamed to `.001`,
the `bo_` removed). The source file's hash was the same before and after.

| File | Profile | Verdict | Findings |
|---|---|---|---|
| Approved house | `prop` | PASS WITH NOTES | 3 major `faces-inverted-suspect` (0.99%, 1.00%, 1.08% on the three levels), 3 notes |
| Broken copy | `prop` | FAIL | `body-missing`, `faces-inverted` (10.6% to 13.5%), `material-duplicate-suffix` |

The house's 36 suspect faces are thin trim slabs on the wall base, wound inward, each with a face
2 to 20 cm behind it. Its six views show no hole at those slabs, which is why that share is a
major and not a blocker. The `front` and `side` views do show magenta through three window
openings: the house has no faces behind them. Whether the world shows through them in game is a
question for the Kit or a game check, not for these tools.
The broken copy's `3q_front_left` view shows the missing walls and the inside of the house.

Three more runs, after the review:

| Case | Result |
|---|---|
| The house with the face cap forced to 1,000, so the sampling path runs | No crash; `inverted_sampled` true on the three visual meshes; PASS WITH NOTES |
| A 1 x 1 x 10 box in a `.blend`, with a 50 x 50 mesh in an excluded collection | PASS; `skipped_hidden` names the excluded mesh; the whole box is inside the `front` view |
| The house and the broken copy again, on the fixed code | The same verdicts and shares as above |

Not run: the live-session entry `preflight_scene` (no Blender was connected over MCP). It calls
the same reader as the headless run. No skinned mesh was run in Blender; the skin rules have unit
tests only.

## How to verify in game

These tools stop at the Kit boundary, so they have no in-game check of their own. Each asset keeps
the check of its own workflow (for a prop: the last step of `/prep-tripo-prop` Phase 4; for a creature:
[creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)).

**Preconditions:** Blender installed (the Store build), an FBX staged outside the live modules.

1. Run Gate 1 on a Kit-proven FBX. `_report\DONE.txt` must say `"status": "ok"` and a `verdict`
   that is not `FAIL`.
2. Run Gate 1 on a copy with one face group flipped. The report must say `"status": "error"`,
   `"verdict": "FAIL"` and name `faces-inverted`.
3. Run Gate 2 on both and read the PNG files. Only a person, or an agent that reads the pictures,
   can see this result.

## How to add a check

1. Find the recorded failure, gate or trap. Without one the check is a note.
2. Add a test to `tools/tests/test_mesh_preflight.py` for the state that raises it and the state
   that does not, and see it fail.
3. Add the field to the record in `extract_mesh` and the rule to `check_mesh` (one mesh) or
   `check_set` (the whole file).
4. Add its row to "The checks" above, with the evidence.

## Changelog

- 2026-11-13: first version (#1153): the skill, both gates, the canary.
- 2026-11-24 to 2026-11-26: the Rhûn chariot work added the "Art checks" table (the maintainer's standing
  corrections; its neck piece row extended on 2026-11-26 after his picture 68 and the collar end rule of armour
  5 v18ay) and the notes on the Blender slot, the independent review, the exported pose, prepared scripts, rig
  measures on the game's clips and the fresh builder per block.

## GitHub Issue

- **Issue:** #1153: [Blender asset gates](https://github.com/haterade22/TAOM/issues/1153)
- **Status:** Open
