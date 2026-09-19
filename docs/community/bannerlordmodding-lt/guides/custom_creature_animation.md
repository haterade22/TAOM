# Custom Creature: the animation clips

Authoring locomotion and attack clips for a creature, exporting them so the Modding Kit reads them
correctly, and the one tag whose absence crashes the game everywhere at once.

Part of the [Custom Creatures](/guides/custom_creatures/) guide. Read
[the skeleton page](/guides/custom_creature_skeleton/) first: if you author against the wrong rig,
nothing on this page will save you.

!!! note "Version"
    Measured against **Bannerlord v1.4.8**.

## Locomotion clips are authored in place

A walk cycle has **zero net root travel**. The feet cycle, the body stays at the origin.

The engine supplies the translation itself, from the movement system. If you bake root motion into a
walk, the engine adds its translation to yours and the creature moves at double speed while its feet
skate.

The corresponding flag is `anf_displace_position`, which turns root motion on. **Never set it on a
cyclic walk or run.** See [the reference tables](/guides/custom_creature_reference/#animation-flags).

## `quad_movement`, or: the six-hour crash

This is the single most expensive lesson on these pages, so it goes near the top.

**Every gait clip compiled for a `movement_system="quadrupedal"` action set must carry the
`quad_movement` clip usage, plus step points.** Walks, runs, strafes, turns in motion, jumps.

Without it:

1. The clip compiles fine. No error.
2. It even plays correctly on a detached, non-mount agent. So it tests clean.
3. Then a quadrupedal action set measures it, builds a **null** native gait structure, and the first
   `Skeleton.TickAnimations` or `GetWalkSpeedLimitOfMountable` dereferences it.
4. Access violation at offset `+0x10`, in **every** mount context at once: the inventory thumbnail,
   the character tableau, and mission deployment.

The secondary fingerprint, if you are staring at a log: resolving an unbound action through the
poisoned set returns a runtime-synthesised garbage name, something shaped like
`1002467048434979358_0`.

!!! danger "In the Modding Kit, `quad_movement` is a CLIP USAGE, not a Flag"
    It is in the collapsed **Clip usages** section, below the Flags list. People look for a checkbox
    in Flags, do not find one, and conclude the tag does not exist.

    `make_walk_sound` **is** a Flag. Step points are a third, separate field: they are the footstep
    timing fractions, and unset they read as `-1, -1, -1, -1`.

Attack, hit and death clips correctly do **not** carry `quad_movement`. Only movement clips.

### Per-category recipe

A working quadruped's clips, by category:

| Clip category | Flags | Clip usages |
|---|---|---|
| gait (walk, run, turn, strafe) | `make_walk_sound` | **`quad_movement`** + step points |
| gallop-pace run | `make_walk_sound` | `quad_movement` + step points + `cyclic` |
| attack | `client_prediction`, `lock_movement`, `enforce_all` | none |
| death | `make_bodyfall_sound`, `client_prediction`, `do_not_keep_track_of_sound`, `enforce_all`, `update_bounding_volume` | none |
| rear | `lock_movement`, `enforce_lowerbody` | none |

## Gait theory, or: why it looked wrong when everything was technically correct

Three things that made TAOM's creatures stop looking uncanny.

**Elephants use a four-beat lateral sequence** (left hind, left fore, right hind, right fore). Never
a trot, never a pace. Their mass means there is no aerial phase at all, so the "fast" gait is a
faster amble, not a different gait. Animating an elephant like a large horse reads as wrong
immediately and it is hard to say why.

**Spiders use an alternating tetrapod.** Four legs down, four legs moving, alternating sets.

**A spider idle is a braced, splayed stance, not a walk-in-place.** An idle built by damping a walk
looks like an animal treading water.

Amplitude edits have to be anchored to the **planted-frame** pose, or the stance foot floats.
Phase shifts are a cyclic time offset and are safe to apply freely.

## Exporting the clip

Armature only, and the armature object **and its data** are both renamed `<skeleton>_notused`.

```python
object_types={'ARMATURE'}
add_leaf_bones=False
primary_bone_axis='Y', secondary_bone_axis='X'
axis_forward='-Y', axis_up='Z'
bake_anim=True
bake_anim_use_all_bones=True
bake_anim_use_nla_strips=False
bake_anim_use_all_actions=False
bake_anim_force_startend_keying=True
bake_anim_step=1.0
bake_anim_simplify_factor=0.0
```

What shipped clips from three separate creatures agree on, and what they do not:

| Property | Verdict |
|---|---|
| UpAxis / FrontAxis / CoordAxisSign = `2 / 1 / -1` | **match this** |
| Model node types: `Null` 1 + `LimbNode` | match this |
| Bones the Kit drops on import | **must be 0** |
| TimeMode | **not load-bearing.** Shipped clips use both 24 and 30 fps |
| Clip flags count | **not load-bearing.** One shipped attack clip has zero flags and works |

Two traps in the export settings themselves:

* `bake_anim_use_all_bones=False` does **not** reduce the exported bone set. Blender bakes the whole
  armature whenever `bake_anim` is on. The flag name is misleading.
* Any `*_nub_notused` bone in the animation FBX is a difference from every shipped clip. Strip them
  before authoring.

!!! tip "Byte size is a reliable empty-bake detector"
    A healthy clip export is megabytes and takes seconds. An export that produced no keyframes comes
    out at roughly 0.06 MB in under 0.01 s. If your file is tiny, the bake found nothing.

    On recent Blender this is usually because renaming edit bones does **not** rewrite slotted-action
    fcurve data paths. Lowercase your bones and the fcurves still point at the old names, so the
    bake is empty.

## Compiling in the Kit

Import as a **Skeleton Animation**, set its **Owner Skeleton**, then create an **Animation Clip**
from it: Source 1 = 1 and Source 2 = the last frame when frame 0 is the rest frame (point 3 below),
and check that Duration comes out greater than zero.

!!! warning "A new clip starts at priority 0 with no flags, and on a mount that means invisible"
    The model viewer plays it, because nothing else competes for the action channel there. In
    battle the mount's walk and gallop take the channel back at once. Every vanilla horse clip
    carries `enforce_lowerbody`, and an attack uses priority 34 with `enforce_lowerbody` +
    `enforce_all` and blends 0.2 / 0.4 (vanilla `horse_kick`, and TAOM's warg attack). Copy the
    recipe of the nearest vanilla clip of the same kind, and check it in a battle, not in the viewer.

!!! danger "Do not rename a clip inside the Modding Kit"
    Renaming corrupts it. The Kit keeps resolving the old name, the inspector reports
    `Size in KB = 0`, it refuses to save, the model viewer draws a scrambled pose, and the renamed
    file can vanish outright. The only in-Kit remedy is restarting the tools after **every single
    rename**.

    Instead: create the clip, leave it on the default `new_animation_clip` name, set its source
    range, sample rate and flags, save, **close the Kit**, rename the file on disk, and reopen.

The clip name itself does not need a particular prefix form. Bare names, single-prefixed and
double-prefixed all work; the `<skeleton>|` prefix you see on compiled clips comes from the Kit's
Owner Skeleton field, not from your take name.

## Riders are not the creature skeleton

A mounted rider is the standard **28-bone `human_skeleton`**. It is not your creature's rig.

Mounted rider poses are authored as human clips and bound in the `as_human_warrior` action set under
`act_<mount>_*` names mapping to `rider_<mount>_*` clips. At runtime the engine parents the rider to
the mount's `rider_sit_bone`.

Sit bones from shipped creatures, as a sanity check on your own:

| Mount | `rider_sit_bone` |
|---|---|
| horse | `horsespine2` |
| warg | `Spine1_M` |
| spider | `chest_m` |
| elephant | ` Spine1_05` |

!!! warning "That leading space in the elephant's bone name is real"
    Bone names exported from some tools carry leading spaces, and the XML must reproduce them
    exactly. A bone name that does not match resolves to index **-1**, silently, and a -1
    `rider_sit_bone` seats the rider at the world origin. The symptom is "my rider is floating at
    his own feet".

You do not have to author rider clips at all if an existing creature's fit close enough. TAOM's
spider reuses the warg's `rider_warg_*` clips.

## Diagnosing a clip that looks right in Blender and wrong in game

Do these in order. Each is cheap and eliminates a whole class.

1. **Render a FRONT view.** A yaw is almost invisible from the side, and it is very easy to spend
   hours looking only at side views. Assert on bone **direction** vectors, not head positions.
2. **Count the bones the Kit will drop.** Any `*_nub_notused` is a difference from every shipped
   clip.
3. **Diff the FBX globals** against a shipped clip: UpAxis, FrontAxis, CoordAxisSign, node types.
4. **Compare the rig against the engine skeleton.** Parenting and bone axis are the two things a
   mesh FBX gets wrong with no visible symptom.

!!! warning "A verification that cannot fail is not a verification"
    Three hypotheses were "refuted" during one TAOM session by tests structurally incapable of
    detecting the fault, and two turned out to be real problems.

    Deleting the `_nub_notused` bones in Blender and seeing no pose change proves nothing: the nubs
    sit coincident with their child carrying identity rotation, so removing them is a no-op **by
    construction**. Re-parenting a bone in Blender and seeing no change fails the same way. And a
    bone's head position cannot detect a rotation about its own origin, so head checks are blind to
    exactly the error being hunted.

    Before trusting a negative result, ask what the test would show if the hypothesis were **true**.
    If the answer is "the same thing", the test is worthless.

## Pick the right reference creature

Compare your creature mount against a **single-creature mount**: a warg, a spider, an elephant.

Do not use a multi-creature rig such as a chariot as your reference. A two-horse-plus-cart rig is
its own skeleton asset whose horse-**named** bones are not the horse skeleton, and they sit 90
degrees off every mesh rig. That looks exactly like a smoking gun and sent two rounds of TAOM's work
in the wrong direction.

Measured against same-shaped creatures, a creature's animation FBX uses the **same** bone convention
as its own mesh FBX: median differences of 6.79 and 16.83 degrees, which are rest-pose differences,
not a 90 degree flip.

## The export mapping, resolved (2026-09-18)

The internal write-up closed with UNRESOLVED for a while. Measuring a Kit-compiled master against its FBX
settled it, on a vanilla rig (`human_skeleton`) with 52 clips:

1. **The Kit stores an FBX's bone-local transforms verbatim as engine locals.** No axis conversion, no
   rest-relative delta. So the armature you export from must carry the engine's own bone frames: build it
   from the engine rest frames (`tail = head + Y column`, `align_roll(Z column)`), which makes the bones
   draw sideways in Blender because the engine's bone axis is X and Blender draws Y. That is cosmetic.
   Export with `primary_bone_axis='Y'`, `secondary_bone_axis='X'`; the locals pass through untouched.
2. **The root bone is stored as `RotZ(180 deg) @ (FBX world pose)`, and the armature node's transform is
   applied on top.** Leave the node at identity and bake a 180 degree world-Z turn into the keyed pose.
3. **Frame 0 is the rest frame.** The Kit stores the root position track relative to frame 0, and every
   vanilla master opens on a rest pose with its clip starting at `Source1 = 1`. Open on a posed frame and
   the pelvis offset of that pose is lost: a hunched character stands too high and its feet skate. Key
   rest at frame 0, poses from frame 1. Vanilla locomotion also carries one root track (the pelvis bob,
   no forward travel); a pack with root-bone travel plus pelvis motion has two, so drop the travel and
   hand it to the engine as the clip usage's loop displacement instead.
4. **Bone tracks are stored in FBX node order, and the engine reads slot i as bone i of the skeleton's own
   list.** The Kit never remaps by name. Blender writes nodes depth-first; `human_skeleton`'s list is
   depth-first, `horse_skeleton`'s is not (neck last), so a horse clip exported from the true hierarchy
   plays the tail on the neck and the neck on the hind legs. When the list is not a depth-first walk, export
   from a hierarchy that makes it one (horse: `horseneck1` under `horsetail3`, as TaleWorlds' own horse FBX
   has it) while keeping each node's local transform relative to its ENGINE parent. Measured on the war
   ram against two vanilla horse masters, 2026-09-18.
5. **The take is the master's name, and the name is the identity.** The Kit names the compiled
   `SkeletalAnimation` after the FBX take (Blender: the action). A reimport under the same take keeps the
   master's GUID, so clips linked to it survive; a different take (a `.001` collision suffix will do it) is
   a new master with a new GUID and an empty skeleton, and the old clip loses its animation.

A mesh rig that skins perfectly (skinning is roll-independent) can still have frames 90 to 180 degrees off
the engine's, and rotations are not roll-independent: that is the whole mechanism behind clips that look
right in Blender and fold in the Kit. TaleWorlds' TpacTool FBX exports of vanilla skeletons are such mesh
rigs. Clips authored on bespoke rigs (spider, elephant, warg) never hit this because the Kit imported the
skeleton from the same rig, and never hit point 4 either: a skeleton the Kit compiled from an FBX lists its
bones in that FBX's depth-first order by construction.

## Next

[The XML that binds it all together](/guides/custom_creature_xml/).
