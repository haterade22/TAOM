# Authoring a Bannerlord animation clip: the rig you must author against

Written 2026-08-29 after the war-ram head-butt took a full day and six wrong diagnoses; resolved 2026-09-18
when the Fab cave troll (52 clips on `human_skeleton`) and then the ram itself (`horse_skeleton`) went
through the Kit with every compiled master read back and compared. The four measured facts are in the
2026-09-18 status section; the procedure that follows from them is the section after it. The one rule
below still holds, and it now has a second half.

## The one rule

**Author the animation against the skeleton the ENGINE will play it on. Never against a mesh FBX.**
**And hand the Kit the bones in the ENGINE's order: it stores tracks in FBX node order and never remaps.**

A mesh FBX can carry arbitrary bone orientations and still deform perfectly in game, because skinning
uses bind matrices and is **roll-independent**. Rotations are **not** roll-independent. So a mesh rig
that produces a flawless-looking animation in Blender can produce a twisted one in the engine, and
nothing in Blender will ever show you the problem.

This is why "the mesh works in game with vanilla animations, therefore its rig is correct" is a false
inference. It proves the bone POSITIONS and weights are right. It says nothing about orientations.

## Getting the real skeleton

```
pwsh tools/dump_engine_skeleton.ps1 -List
pwsh tools/dump_engine_skeleton.ps1 -Skeleton horse_skeleton -OutFile horse_skeleton.json
```

Skeletons live in `Native/AssetPackages/skeletons.tpac`, which TpacTool parses. The per-creature rig
packages do **not** parse: `pack_horse_customrig`, `animations_horse_and_rider`,
`animations_movement_and_behaviour`, `pack_anim_cutscene`, `animation_clips.tpac` and `Assets.tpac`
all throw `Frames not equal` or `capacity was less than the current size`. A day was lost to treating
those failures as "the skeleton is unobtainable"; `skeletons.tpac` holds them all and works.

## What the engine data says (horse_skeleton, measured)

```
32 bones, no *_nub_notused entries
horsepelvis -> horsespine1 -> horsespine2 -> horsespine3 -> horseneck1 -> horseneck2 -> horse_head
```

Two facts that are invisible from any mesh FBX:

| Fact | Engine | `SK_EB_Goat_A.fbx` (mesh rig) |
|---|---|---|
| `horseneck1`'s parent | **`horsespine3`** | `horsetail3`: not a mistake, TaleWorlds' exporter writes nodes depth-first and that parent keeps the engine's slot order (fact 4) |
| Bone axis | **X along the bone** | Blender imports it Y-along-bone |
| `_nub_notused` bones | none | 7, which the Kit drops on import |

The bone LIST order matters as much as the frames: `horse_skeleton` lists the rear legs and tail before the
neck, which is not a depth-first walk of its hierarchy (fact 4 below). `human_skeleton`'s list IS depth-first.

The along-X convention is provable from the rest frames alone: every child's offset is `(+len, 0, 0)`
in its parent's space, and that `len` is exactly the parent's bone length.

```
horseneck1 -> horseneck2   offset +0.4032    horseneck1 length 0.403
horseneck2 -> horse_head   offset +0.4958    horseneck2 length 0.496
```

**Do NOT conclude from this that you should export with `primary_bone_axis='X'`.** That inference is
natural, was made here, and was tested: it produced the worst result of the whole session. The
convention the engine STORES its rest frames in is not the same question as which Blender export
setting reproduces a clip the Kit reads correctly. See "Dead ends" below. **Resolved 2026-09-18** (status
section below): the Kit stores an FBX's bone locals verbatim, so the rig you export from must carry the
engine's frames; `primary_bone_axis='Y'` / `secondary_bone_axis='X'` then passes them through untouched,
and the root needs a 180 degree world-Z turn baked into the pose.

## Rest-frame maths

`RestFrame` is row-vector (rows are the basis vectors, `M41..M43` the offset). Blender is
column-vector, so transpose the 3x3 and move the offset to the last column:

```python
Matrix(((m[0], m[3], m[6], m[9]),
        (m[1], m[4], m[7], m[10]),
        (m[2], m[5], m[8], m[11]),
        (0.0,  0.0,  0.0,  1.0)))
```

Accumulate down the hierarchy (`world = parent_world @ local`), then build each Blender bone with
`head = world.translation`, `tail = head + world_X * length`, and `eb.align_roll(world_Z)`.
The reusable copy is `build_engine_rig()` in `tools/blender/retarget_mannequin_to_human.py`: tail = head +
engine Y column, `align_roll(engine Z column)`, and an assertion that every `matrix_local` equals the
engine world rest to 1e-4. `tools/blender/human_skeleton_engine.json` is the human dump (from
`human.tpac`; `dump_engine_skeleton.ps1` reads `skeletons.tpac`, which does not hold the human).

**Rotation sign is per-rig and must be measured, never assumed.** On `SK_EB_Goat_A.fbx` a positive
world-X rotation lowers the head, because that FBX's armature matrix flips Y. On a rig reconstructed
from `skeletons.tpac` (identity armature matrix) the same rotation raises it. Apply +20 and -20 to
one bone and print the result before authoring anything.

## Export settings, verified against shipped clips

```python
object_types={'ARMATURE'}, add_leaf_bones=False,
primary_bone_axis='Y', secondary_bone_axis='X',   # 'X' was tried and is WORSE; see Dead ends
axis_forward='-Y', axis_up='Z',
bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
bake_anim_step=1.0, bake_anim_simplify_factor=0.0
```

Name the armature object **and** its data `<skeleton>_notused` (warg, spider and elephant all do).
Export from `frame_start = 0`, with frame 0 keyed as the rest pose (see fact 3 above). The FBX take (the
Blender action) is the master's name in the Kit, and a reimport keeps the master's GUID only while that name
matches; a Blender `.001` collision suffix on the action creates a new master and orphans the clip; the shipped creature
exports (`harness.export_clip_fbx`) start at frame 1 on a posed frame and were never checked for this.

What the shipped animation FBX agree on, and what they do not:

| Property | warg | spider | elephant | Verdict |
|---|---|---|---|---|
| UpAxis / FrontAxis / CoordAxisSign | 2 / 1 / −1 | 2 / 1 / −1 | 2 / 1 / −1 | **match this** |
| Model node types | Null 1 + LimbNode | same | same | match this |
| Bones the Kit drops | 0 | 0 | 0 | **must be 0** |
| TimeMode | 6 (30fps) | 6 (30fps) | 11 (24fps) | **not load-bearing** |
| Clip flags | 4 | **0** | 3 | **not required** |

`bake_anim_use_all_bones=False` does **not** reduce the exported bone set. Blender bakes the whole
armature whenever `bake_anim` is on; the flag name is misleading. Verified: 399 curves / 39 bones
either way.

## Diagnosing a clip that looks right in Blender and wrong in game

Do these in order. Each is cheap and rules out a whole class. Step 0, which solved the human case in one
afternoon after the ram had cost a day: **read the Kit's compiled master back** (TpacTool.Lib, as
`tools/read_anim_keyframes_tpac.ps1` does) and compare each bone's stored rotation with the FBX's local
and with the engine rest local. The three numbers tell you whether the Kit changed anything (it does not,
for children), whether your rig's frames were the engine's, and what the root got.

0. **Feet float or skate while the limbs move right?** Check frame 0 of the master: it must be the rest
   pose, because the root position track is stored relative to it (fact 3 in the 2026-09-18 status).
0b. **Some limbs play another limb's motion while the spine is right?** Read the compiled master back and,
   for each slot at frame 0, find the bone whose rest local it equals (a best-match table). If slot i is
   not file-order bone i, the FBX node order differs from the skeleton's list order (fact 4).
1. **Render a FRONT view.** A yaw is nearly invisible in a side view, and every render in the war-ram
   session's first several hours was a side view. Also print the bone direction and assert `|x| ~ 0`;
   note that a bone's HEAD position cannot detect a rotation about its own origin, so position checks
   are blind to yaw and to any error in the last bone of a chain.
2. **Count bones the Kit will drop.** Any `*_nub_notused` in the animation FBX is a difference from
   every shipped clip. Strip them before authoring.
3. **Diff FBX globals** against `Warg_Attack_Stand.fbx` (UpAxis, FrontAxis, CoordAxisSign, node types).
4. **Compare the rig against the engine skeleton**, per the rule at the top. Parenting and bone axis
   are the two things a mesh FBX gets wrong without any visible symptom.

## Dead ends, recorded so nobody re-walks them

Each of these was measured, believed, and turned out not to be the cause:

- **The `_nub_notused` bones**, dismissed by deleting them in Blender and seeing no pose change. That
  test cannot fail: the nubs are coincident with their child and carry identity rotation, so removing
  them in Blender is a no-op by construction. It says nothing about a Kit that drops them from a
  track list. (They still must be stripped; they just were not the yaw.)
- **`horseneck1`'s parent**, dismissed the same way and for the same reason. Read with fact 4 it is neither
  wrong nor a rig error: TaleWorlds' exporter hangs the neck off `horsetail3` so that a depth-first node
  walk reproduces the skeleton's list order (neck last). Re-parenting it to `horsespine3` in Blender put
  the neck nodes BEFORE the rear legs in the FBX, so the Kit stored the neck's tracks in the rear-leg
  slots and the tail's in the neck slots: that is the "head goes down and forward" row below.
- **Bone axis, "fixed" by re-exporting the mesh rig with `primary_bone_axis='X'`.** That re-orients
  bones rather than relabelling them (fact 1: the Kit takes the FBX locals as they are, so any export-side
  axis remap lands in the master), breaking the bind: the ram came out standing on its hind legs.
- **Rebuilding the rig from `skeletons.tpac` and exporting it with `primary_bone_axis='X'`.** This is
  the "obviously correct" move once you know the engine's convention, and it was the WORST result of
  the session: the ram folded in on itself. The reconstruction itself is sound (its bone positions
  reproduce the mesh rig's to three decimals), so the failure is in the export mapping, not the rig.
  **The engine's storage convention and the right Blender export setting are two different
  questions, and knowing the first does not answer the second.** Resolved: the rig must CARRY the engine
  frames (fact 1) and be exported with the identity bone correction (Y/X); that reconstruction also put the
  neck under `horsespine3`, so its node order was wrong too (fact 4). Two faults, one mangled result.
- **TimeMode**, changed from 30 to 24fps to match the chariot. Shipped clips use both.
- **Clip flags.** `an_spi_attack_front` ships with zero flags and works.
- **The chariot as a reference.** `as_chariot` uses `chariot_skeleton`: two horses plus a cart, on its
  own skeleton asset. Its horse-named bones sit 90 degrees from every mesh rig, which looked like a
  smoking gun and sent two rounds of work in the wrong direction. **Compare a creature mount against
  the warg, the spider or the elephant.** They are single-creature mounts with BT-driven attacks,
  which is the same shape as a war ram; the chariot is not.

## Status as of 2026-09-18: RESOLVED for an engine-native skeleton (human_skeleton), measured

The cave troll clips (Fab pack retargeted onto `human_skeleton`, `docs/reference/ue-to-bannerlord-asset-pipeline.md`
"The retarget stage") went through three Kit imports, each read back with TpacTool and compared bone by
bone against the FBX and the engine rest frames. Two facts came out, and together they turn the rule at
the top of this document into a procedure:

1. **The Kit stores an FBX's bone-local transforms verbatim as engine locals.** 27 of 28 bones at 0.0
   degrees difference between the compiled master and the FBX, on every import. There is no axis
   conversion and no rest-relative delta. So the FBX's bone frames must BE the engine's bone frames:
   build the Blender armature so that each bone's `matrix_local` (armature space) equals the engine's
   accumulated `RestFrame` (transposed, offset in the last column), which means `tail = head + Y column`
   and `align_roll(Z column)`. The bones then draw sideways in Blender and in the Kit's skeleton view,
   because the engine's bone axis is X and Blender draws Y; that is cosmetic. Export with
   `primary_bone_axis='Y'`, `secondary_bone_axis='X'` (identity bone correction) and the locals go
   through untouched. `tools/blender/retarget_mannequin_to_human.py --engine-skeleton` does this and
   asserts the frames to 1e-4; `tools/blender/human_skeleton_engine.json` is the dump.
2. **The root bone is stored as `RotZ(180 deg) @ (FBX world pose)`, and the armature node's transform is
   applied too.** Identity node: pelvis came back 180 degrees off. Node turned 180 degrees to cancel it:
   pelvis right in the master, but the character faced away in the Kit (the node transform also rides in
   through the package's `Geometry <file>.fbx` item). Fix that survives: keep the node at identity and
   turn the whole character 180 degrees about world Z in the keyed pose (`--root-yaw-mode pose`, the
   default). Children are unaffected either way.
3. **Frame 0 is the REST frame, and the Kit stores the root position track relative to it.** Every vanilla
   master opens on a rest frame (`anim_run_forward_unarmed` frame 0: 0.6 degrees from rest, root (0,0,0))
   and its clip starts at `Source1 = 1`. A clip that opens on a posed frame has its root track zeroed at
   that pose: the hunched troll stood 9 cm too high, legs posed for a lower hip, feet skating (Artem,
   2026-09-18). Key frame 0 as rest (root turned like the clip, children identity), export from frame 0,
   and set the clip's `Source1 = 1`. Vanilla walk/run masters also carry ONE root track (pelvis bob, no
   forward travel) and no pelvis position track; a UE pack has two (root travel plus pelvis), so drop the
   root's travel and keep the pelvis, and give the engine the travel as `BipMovIkUsage.LoopDisplacement`.
4. **A master's bone tracks are stored in FBX NODE order, and the engine reads slot i as bone i of the
   skeleton's FILE order.** Measured 2026-09-18 on the ram: the Kit-compiled master held slots 16..31 in
   Blender's depth-first node order (neck1, neck2, head, rear legs, tail) and matched the FBX values to 0.004
   deg under that mapping, while every vanilla horse master (`anim_horse_stand_4`, `horse_dash_forward`, read
   from `AssetPackages/animations.tpac` with `read_anim_keyframes_tpac.ps1 -Skeleton horse_skeleton
   -SkeletonPackage AssetPackages\skeletons.tpac`) holds slot i within a few degrees of file-order bone i and
   50 to 178 deg from the depth-first bone. The Kit never remaps by name. `human_skeleton`'s file order IS a
   depth-first walk, so the troll never met this and the "verbatim" measurement above was blind to it;
   `horse_skeleton` lists the rear legs and tail before the neck, so the neck played the tail's rotations, the
   rear legs the neck's, and the ram stood on its head with its hind legs in the air. TaleWorlds' own goat FBX
   parents `horseneck1` to `horsetail3` for exactly this reason: that hierarchy makes the depth-first order
   equal the file order. Fix, built into `transfer_clip_to_engine_rig.py`: when the file order is not a
   depth-first walk, export from a rig whose hierarchy makes it one (only the neck moves for the horse) while
   each node's local stays the ENGINE-parent-relative value the slot must carry; the re-import check asserts the
   bone order and the locals. Three TaleWorlds-side files read the same day (Downloads, from Mike): their
   `horse.fbx` mesh export and GulagEnabler's re-weighted horse both list Model nodes in the engine file order
   with `horseneck1` under `horsetail3`; the Kit's own `horse_skeleton.fbx` export keeps the true hierarchy
   (`horseneck1_29` under the ribcage nub), lists nodes depth-first, and writes the engine slot index into every
   bone name (`horselfemur_16`, `horseneck1_29`), Y-up, centimetres. Whether the Kit's importer honours such a
   suffix is UNTESTED; the hierarchy route above is the one measured.

This is the class the ram left open above: "reconstruction from skeletons.tpac + export X" mangled because
`primary_bone_axis='X'` re-expresses the frames the reconstruction had just made exact. The candidate for
the ram is therefore reconstruction + Y/X export + the pose yaw. **Built 2026-09-18:**
`tools/blender/transfer_clip_to_engine_rig.py` moves a clip authored on a mesh rig onto the engine rig of the
SAME skeleton by transferring each bone's rest-relative WORLD rotation (the frames cancel there), with the yaw
baked and a rest frame 0. On the head-butt it reports 0.0 deg delta error on all 32 bones and frame 0 at rest;
`tools/blender/horse_skeleton_engine.json` is the dump. Its first Kit import stood the ram on its head and
exposed fact 4 (bone-track ORDER); the second export carries the file-order hierarchy and plays correctly in
the Kit (confirmed 2026-09-18 12:36). A `--hold-bone horseneck1 --hold-seconds 2.5` export keeps the head
down after the butt (106 frames with the rest frame).

### The procedure (what the four facts add up to)

1. **Dump the engine skeleton**: `pwsh tools/dump_engine_skeleton.ps1 -Skeleton <name> -OutFile x.json` from
   `skeletons.tpac` (the human is in `EmAssetPackages/human/human.tpac`), converted to the
   `tools/blender/<skeleton>_engine.json` layout (`human_skeleton_engine.json`, `horse_skeleton_engine.json`).
2. **Build the rig from it** (`build_engine_rig`): every `matrix_local` equals the accumulated engine rest,
   asserted to 1e-4. Bones draw sideways in Blender; ignore that.
3. **Put the motion on it in WORLD space**: a different skeleton by rotation-delta retarget
   (`retarget_mannequin_to_human.py`), the same skeleton from a mesh rig by rest-relative world-rotation
   transfer (`transfer_clip_to_engine_rig.py`). Bone frames cancel in world space; positions are what a
   mesh rig gets right, so they need no fixing.
4. **Root**: bake a 180 degree world-Z turn into the keyed pose, armature node at identity (fact 2).
5. **Frame 0 = rest**, root turned like the clip, poses from frame 1; clip `Source1 = 1`,
   `Source2 = Duration - 1` (fact 3). Drop UE-style root travel; hand it back as clip metadata.
6. **Node order = skeleton list order** (fact 4): when the list is not a depth-first walk of the hierarchy,
   export from a rig whose hierarchy makes it one, node locals kept engine-parent-relative
   (`order_parents` / `build_export_rig` in the transfer tool).
7. **Export** armature only, `primary_bone_axis='Y'`, `secondary_bone_axis='X'`, `-Y` forward, `Z` up, 30
   fps, armature named `<skeleton>_notused`, the ACTION named as the master must be called (the take is
   the master's name; a `.001` collision makes a new master and orphans the clip).
8. **Check before the Kit**: re-import, assert bone order equals the list order, frame 0 reads 0 deg from
   rest, and report per-bone deviation at frame 1 (both tools do this).
9. **Kit**: import (a reimport under the same take name keeps the master GUID), then read the master back
   (`read_anim_keyframes_tpac.ps1` for vanilla packages; the same TpacTool data model for Armory tpacs) and
   check slot i against file-order bone i at frame 0; only then judge the viewer. Save so the RDC entry
   exists (`tools/check_rdc_entries.py`).
10. **Clip**: clone a vanilla clip of the same type (`gen_troll_anim_clips.ps1` for a set;
   `wire_anim_master_clip.ps1` to re-point one clip and set an empty master skeleton), fix item checksums
   (`tpac_fix_item_checksums.py`), then bind the action in an action set and, for a new action, `action_types.xml`.
The TpacTool `FixBoneForBlender` rig (`human_skeleton_with_male_body.fbx`) is a MESH rig in the sense of
the rule at the top: its frames differ from the engine's by 90 to 180 degrees per bone and produced the
folded troll of 2026-09-17. Use it for its body meshes only.

## Status as of 2026-08-29: UNRESOLVED (historical; superseded by the 2026-09-18 section above)

Ranked by how close each got in the Kit's model viewer:

| Rig | Bones | neck1 parent | Export axis | Result |
|---|---|---|---|---|
| mesh rig | 39 (nubs) | horsetail3 | Y | neck ~90 deg, head back then forward |
| mesh rig, local-Z rotations | 39 (nubs) | horsetail3 | Y | identical to the above |
| mesh rig, nubs stripped | 32 | horsetail3 | Y | neck ~90 deg, backwards |
| **mesh rig + reparent** | 39 (nubs) | **horsespine3** | Y | **head goes DOWN and FORWARD.** Over-rotated, drags the spine, but the only variant failing in the right direction |
| reconstruction from `skeletons.tpac` | 32 | horsespine3 | X | fully mangled |

**What that table actually showed, read with the four facts (2026-09-18).** Every mesh-rig row had the
wrong FRAMES (fact 1) and, through the `horsetail3` parent, the right slot ORDER (fact 4): the neck bent
on the mesh rig's axes, hence "90 degrees backwards". The reparent row swapped that: same wrong frames,
and now the wrong ORDER as well, so the neck slots received the tail's rotations and the head "went down
and forward". The reparent was not load-bearing in the sense claimed; it moved the neck's tracks into
other bones' slots. The reconstruction row had the right positions, frames re-expressed by the X export
(fact 1) and the wrong order (neck under `horsespine3`): fully mangled. The "residual over-rotation"
reading of the reparent row was wrong. The ram was re-run through the resolved path on 2026-09-18
(`transfer_clip_to_engine_rig.py`) and plays correctly in the Kit (Mike, 12:36).

**Do not treat anything in the "What the engine data says" section as suspect.** Those numbers are
read straight out of the engine's own asset. The step from those facts to a working export is the
procedure in the 2026-09-18 section.

## Hit capsules: what weapons strike (2026-09-18, the war elephant)

Players called the war elephant's collision too small. A creature has three collision layers, and each answers a
different complaint:

| Layer | Where it lives | What it does | Edit with |
|---|---|---|---|
| Body capsule | `<Capsules><body_capsule>` in the Monster XML | other agents bump into it and path around it | the XML (the elephant's top must stay under its howdah's physics floor: `features/elephant.md` "Collision") |
| Hit capsule, one per bone (inferred from field names and zones; see below) | `SkeletonUserData` in the skeleton's tpac: `CollisionPosition1/2` (bone-local ends), `CollisionRadius`, `CollisionMaxRadius`; `BodyType` names the hit zone (`head`, `chest`, `legs`, `abdomen`, `arm_left` and so on) | what melee weapons and missiles strike | the Kit's skeleton editor, or `tools/skeleton_hit_capsules.py` |
| Ragdoll capsule, one per bone | the same record: `RagdollPosition1/2`, `RagdollRadius`, plus each body's mass and the joint constraints | the corpse's physics after death; nothing while the creature lives | the Kit |

So "make the ragdoll bigger too" changes how the body falls, not what can be hit.

**The Kit's default hit capsule is almost nothing.** A skeleton nobody authored gets, per bone, a capsule along the
bone's local x from 0.1 to 1.0 of the bone's length, with a radius of a tenth of that length: the radius is about a
ninth of the capsule's own length, which is how to spot one. `elephant_skeleton` had 41 of its 60 bodies like that:
the neck 0.03 to 0.05 m wide around 0.56 to 0.60 m of neck, and 29 trunk bones at 0.01 to 0.02 m. Measured against
the skin each bone drives (the vertices whose largest weight is that bone), 48% of the elephant's skin sat inside any
hit capsule, while its authored torso capsule stood 0.6 m above the animal's back. The same test over every skeleton
in the Armory on 2026-09-18 (radius within 10% of length / 9):

| Skeleton | Bodies | Kit-default hit capsules |
|---|---|---|
| `chariot_skeleton` | 60 | 57 |
| `elephant_skeleton` | 60 | 41 before the fit; 27 after, all bones owning under 15 skin vertices (nine bones of one trunk chain, all 14 of the duplicate chain, both clavicles, three tail bones), whose skin the neighbouring capsules cover |
| `spider_skeleton` | 62 | 40 |
| `skeleton_warg` | 49 | 2 |
| `dwarf_skeleton_a` | 28 | 0 |

**Fitting them to the mesh.** `tools/blender/export_skin_for_capsules.py` exports the skin headless, then
`tools/skeleton_hit_capsules.py fit` sizes each bone's capsule to its own skin, a little bigger than it. Two things
a first fit got wrong, both measured on the elephant:

- One round capsule cannot hug a torso that is taller than it is wide. Enclosing all of a bone's skin makes the
  capsule stand out elsewhere, so the fit caps the stand-out (20 cm at the 90th percentile of surface samples,
  signed by the nearest vertex normal) and leaves the rest to the neighbouring bones' capsules. Their union is what
  a weapon meets.
- A bone's skin is cut flat where the next bone takes over. Ends pulled in by the radius, the classic capsule fit,
  leave that rim uncovered, and the radius then inflates to reach it (a synthetic cylinder of radius 0.5 came out at
  0.69). The fit also tries the ends at the skin's extent and at half the radius, and keeps whichever covers most.

Result on the elephant: 32 capsules refit, 28 kept (bones with under 15 skin vertices, or whose old capsule already
covered more), 98.2% of the skin inside a hit capsule, every capsule within 20 cm of the skin. Neck 0.05 to 0.81 m,
upper neck 0.03 to 0.48 m, haunches 0.33 to 0.53 m, head re-centred at 0.50 m, ears, trunk and tail 0.07 to 0.32 m.
Read back from the patched file, per region: legs 99.9%, body and head 97.1%, trunk 93.1%, ears 99.5%, tail 100%,
and no skin vertex left outside sits more than 5 cm from a capsule.

**Writing them.** The bodies sit LZ4-compressed in the skeleton item's `SkeletonUserData` segment (the last segment
of `adod_elephant_geo.tpac`). `skeleton_hit_capsules.py patch` rewrites only the refit bodies' ends and radii,
recompresses, and fixes the segment's storage size and its hash, the xxHash64 of the uncompressed payload (the formula
`tools/tpac_clone_metamesh.py` and the #616 lesson record; all 23 elephant segments match it). The container goes
through `tpac_clone_metamesh.py`'s parser and serializer, which round-trips the elephant package byte for byte. The
patched package read back through TpacTool.Lib, an independent reader, with all 60 bodies as fitted and 10 changed
header bytes (the segment's size and hash). The package's `.rdc` holds no bone or body names (cooked mesh data), so
the bodies are read from the tpac itself; that is an inference until the in-game hit test. Load the module in the Kit
once after a patch: whether the client reads the bodies from the tpac or from the cooked `.rdc` is unverified.
Why "hit" is inferred: the bodies' `BodyType` zones read back from the tpac (`abdomen`, `neck`, `chest`, `head`,
`legs`, ...) match the engine's `BoneBodyPartType`, which `MBAgentVisuals.GetBoneTypeData` reads per bone for a
blow's armour zone; that the Collision geometry, not the Ragdoll one, is what weapons meet rests on the field
names until an in-game hit test.

**Testing them in a mission.** `Mission.RayCastForClosestAgentsLimbs(Vec3 sourcePoint, Vec3 targetPoint, int
excludedAgentIndex, float rayThickness, out float collisionDistance, out sbyte boneIndex)` (public, verified on
the installed 1.5.3 DLL) casts against agents' limb capsules and returns the agent and the bone it hit. Per
the Yotthani handoff (`docs/reviews/adopt-yotthani-animation-handoff-2026-09-18.md`), it is the query that meets weapons where they land, so a debug
command sweeping rays across a creature can show which bones answer where; the same call beats
distance-to-bone-origin checks for scripted creature strikes, which miss the body between two joints. TAOM
calls it nowhere yet.

## Related

- [lotrlome-war-ram-changes.md](lotrlome-war-ram-changes.md): the external-module ledger for the ram.
- [../ai-includes/creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md): the Blender-MCP loop.
- [../features/war-ram.md](../features/war-ram.md): the feature this came out of.
- `tools/rename_anim_clip_tpac.py`: renaming a clip inside the Kit corrupts it; rename on disk instead.
- `tools/skeleton_hit_capsules.py`: read, fit and patch a skeleton's per-bone hit capsules ("Hit capsules" above).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/troll-race-arp-retargeting-workflow.md](../ai-includes/troll-race-arp-retargeting-workflow.md)
- [docs/features/troll-race.md](../features/troll-race.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/recipe-add-a-race-or-creature.md](../modding/recipe-add-a-race-or-creature.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reference/ue-to-bannerlord-asset-pipeline.md](./ue-to-bannerlord-asset-pipeline.md)

<!-- backlinks-end -->
