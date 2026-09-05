# Authoring a Bannerlord animation clip: the rig you must author against

Written 2026-08-29 after the war-ram head-butt took a full day and six wrong diagnoses. Every one of
those came from the same root error, stated here first because it is the only thing in this document
that really matters.

## The one rule

**Author the animation against the skeleton the ENGINE will play it on. Never against a mesh FBX.**

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
| `horseneck1`'s parent | **`horsespine3`** | `horsetail3` |
| Bone axis | **X along the bone** | Blender imports it Y-along-bone |
| `_nub_notused` bones | none | 7, which the Kit drops on import |

The along-X convention is provable from the rest frames alone: every child's offset is `(+len, 0, 0)`
in its parent's space, and that `len` is exactly the parent's bone length.

```
horseneck1 -> horseneck2   offset +0.4032    horseneck1 length 0.403
horseneck2 -> horse_head   offset +0.4958    horseneck2 length 0.496
```

**Do NOT conclude from this that you should export with `primary_bone_axis='X'`.** That inference is
natural, was made here, and was tested: it produced the worst result of the whole session. The
convention the engine STORES its rest frames in is not the same question as which Blender export
setting reproduces a clip the Kit reads correctly. See "Dead ends" below. As of 2026-08-29 the
best in-game result still comes from `primary_bone_axis='Y'` off the mesh rig, and the residual
error is unexplained.

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
`tools/blender/` has no reusable copy of this yet; the war-ram build script is the reference.

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

Do these in order. Each is cheap and rules out a whole class.

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
- **`horseneck1`'s parent**, dismissed the same way and for the same reason. It IS wrong in the mesh
  FBX, and it does matter, but a Blender re-parent test could not show it.
- **Bone axis, "fixed" by re-exporting the mesh rig with `primary_bone_axis='X'`.** That re-orients
  bones rather than relabelling them, breaking the bind: the ram came out standing on its hind legs.
- **Rebuilding the rig from `skeletons.tpac` and exporting it with `primary_bone_axis='X'`.** This is
  the "obviously correct" move once you know the engine's convention, and it was the WORST result of
  the session: the ram folded in on itself. The reconstruction itself is sound (its bone positions
  reproduce the mesh rig's to three decimals), so the failure is in the export mapping, not the rig.
  **The engine's storage convention and the right Blender export setting are two different
  questions, and knowing the first does not answer the second.**
- **TimeMode**, changed from 30 to 24fps to match the chariot. Shipped clips use both.
- **Clip flags.** `an_spi_attack_front` ships with zero flags and works.
- **The chariot as a reference.** `as_chariot` uses `chariot_skeleton`: two horses plus a cart, on its
  own skeleton asset. Its horse-named bones sit 90 degrees from every mesh rig, which looked like a
  smoking gun and sent two rounds of work in the wrong direction. **Compare a creature mount against
  the warg, the spider or the elephant.** They are single-creature mounts with BT-driven attacks,
  which is the same shape as a war ram; the chariot is not.

## Status as of 2026-08-29: UNRESOLVED

Ranked by how close each got in the Kit's model viewer:

| Rig | Bones | neck1 parent | Export axis | Result |
|---|---|---|---|---|
| mesh rig | 39 (nubs) | horsetail3 | Y | neck ~90 deg, head back then forward |
| mesh rig, local-Z rotations | 39 (nubs) | horsetail3 | Y | identical to the above |
| mesh rig, nubs stripped | 32 | horsetail3 | Y | neck ~90 deg, backwards |
| **mesh rig + reparent** | 39 (nubs) | **horsespine3** | Y | **head goes DOWN and FORWARD.** Over-rotated, drags the spine, but the only variant failing in the right direction |
| reconstruction from `skeletons.tpac` | 32 | horsespine3 | X | fully mangled |

**The reparent is load-bearing.** Changing only `horseneck1`'s parent flipped the failure from "90
degrees backwards" to "too far in the correct direction", which matches the engine data and confirms
that the Blender re-parent test which "refuted" it was worthless. Note that world-X and local-Z
rotations produced identical in-game results, so the storage form of the rotation is not the issue.

The residual over-rotation is the expected signature of the remaining problem: with the correct
parent, the engine composes the clip's local rotations against ITS `horsespine3` orientation, which
differs from the mesh rig's. That is exactly the roll error a rig reconstructed from the engine data
removes, which is why `wr_f_engine_y` / `wr_g_engine_z` (reconstruction, untested export axes) are
the current candidates.

**Do not treat anything in the "What the engine data says" section as suspect.** Those numbers are
read straight out of the engine's own asset and are the only ground truth this session produced. It
is the step from those facts to a working export that remains open.

## Related

- [lotrlome-war-ram-changes.md](lotrlome-war-ram-changes.md): the external-module ledger for the ram.
- [../ai-includes/creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md): the Blender-MCP loop.
- [../features/war-ram.md](../features/war-ram.md): the feature this came out of.
- `tools/rename_anim_clip_tpac.py`: renaming a clip inside the Kit corrupts it; rename on disk instead.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/recipe-add-a-race-or-creature.md](../modding/recipe-add-a-race-or-creature.md)

<!-- backlinks-end -->
