# Updates for the existing Custom Creatures pages (v1.5.3)

Corrections and additions for the six live pages of the
[Custom Creatures](https://docs.bannerlordmodding.lt/guides/custom_creatures/) guide, checked against
Bannerlord v1.5.3 and written against the live text of each page as it stood on 2026-09-26. They arrive
with four new pages, and all of it must go live together: the notes at the end say why.

**Two ways to apply them.** The six refreshed pages in `guides/` (`custom_creatures.md`,
`custom_creature_skeleton.md`, `custom_creature_animation.md`, `custom_creature_xml.md`,
`custom_creature_reference.md` and `custom_creature_troubleshooting.md`) are that live text with every
section below applied, so the simplest route is to replace the six live files with them. If a page
changed on the site after 2026-09-26, paste its sections here instead: each names its target page,
exactly where the text goes and why; paste the contents of its fenced block as they stand. Either way,
this file is the list of what changed.

Source keys in the **Why** lines name TAOM's evidence notes: MAP (the animation system map, from TAOM's
reverse engineering of the v1.5.3 game and Modding Kit DLLs), SKA (skeleton authoring), FLG (clip flags),
CMA (the creature mount workflow), UEP (the Unreal Engine to Bannerlord asset pipeline), MUM, SPI, ELE,
RAM and TRL (the mumakil, spider, war elephant, war ram and troll race notes), SIZE (monster size), BP
(body properties), LES (the animation lessons), TRIAGE (the native crash triage method), and inventory
(the fact inventory compiled from those notes for this pack, cited by section).

| Target page | Sections |
|---|---|
| `/guides/custom_creatures/` | A1 version box, A2 page list, A3 "Which one" row, A4 the war ram's thin set, A5 terms, A6 the example creatures, A7 the attack row |
| `/guides/custom_creature_skeleton/` | B1 neck under the tail, B2 export rig, B3 materials, B4 physics, B5 hit capsules, B6 version box, B7 packages TpacTool cannot open |
| `/guides/custom_creature_animation/` | C1 `quad_movement`, C2 recipes, C3 compiling, C4 the export mapping, C5 diagnosing, C6 `anf_displace_position`, C7 renaming a clip |
| `/guides/custom_creature_xml/` | D1 thin set, D2 rider partial, D3 missing clip names, D4 size, D5 the reskin trap, D6 the `_map` set |
| `/guides/custom_creature_reference/` | E1 flag warning, E2 flag rows, E3 foot IK, E4 recipes, E5 being struck, E6 checksum, E7 fingerprints, E8 version |
| `/guides/custom_creature_troubleshooting/` | F1 version box, F2 quick index, F3 `quad_movement` crash, F4 three new crashes, F5 native crashes, F6 the log, F7 the zero-size fix |

## A1. Custom Creatures: the version box

**Target:** `/guides/custom_creatures/` > the `!!! note "Version"` box under the page list
**Action:** replace the box
**Why:** the guide now covers v1.5.3, and its engine-code findings come from reverse engineering of
v1.5.3 (MAP header).

````markdown
!!! note "Version"
    Measured on **Bannerlord v1.4.5 to v1.5.3**. Engine-code findings come from TAOM's reverse
    engineering of the v1.5.3 game and Modding Kit DLLs; native crash offsets move with every engine
    update, and each page marks facts measured on an older version. v1.4.6 changed which data mistakes
    are survivable: see [The 1.4.6 rule](/guides/custom_creature_xml/#the-146-rule) before porting
    anything older.
````

## A2. Custom Creatures: the page list

**Target:** `/guides/custom_creatures/` > the bullet list under the lede
**Action:** replace the list
**Why:** a flat list sends a mount author through race-only pages and a race author through
quadruped rigging; two reading orders keep each on the pages that apply.

````markdown
Two reading orders, by what you are building.

**A mount** (a creature a troop rides):

1. [Custom Creature: the skeleton](/guides/custom_creature_skeleton/)
2. [Custom Creature: the animation clips](/guides/custom_creature_animation/)
3. [Custom Creature: the XML](/guides/custom_creature_xml/)
4. [Custom Creature: the clip inspector](/guides/custom_creature_clip_inspector/)
5. [Custom Creature: big creatures in battle](/guides/custom_creature_battle/)
6. [Custom Creature: troubleshooting](/guides/custom_creature_troubleshooting/)

**A humanoid race** (a two-legged soldier with its own size or proportions):

1. [Races](/modding/races/), for the basic `skins.xml` race
2. [Custom Creature: a humanoid race on its own skeleton](/guides/custom_creature_race/)
3. [Custom Creature: the clip inspector](/guides/custom_creature_clip_inspector/)
4. [Custom Creature: melee attack clips](/guides/custom_creature_melee/)
5. [Custom Creature: big creatures in battle](/guides/custom_creature_battle/)
6. [Custom Creature: troubleshooting](/guides/custom_creature_troubleshooting/)

A race on its own skeleton also needs [the skeleton page](/guides/custom_creature_skeleton/) before
step 2. Both look things up in [Custom Creature: reference tables](/guides/custom_creature_reference/).
````

## A3. Custom Creatures: a row for races

**Target:** `/guides/custom_creatures/` > `#which-one`
**Action:** add row to the table, after "It has a different number of legs, or a radically different spine"
**Why:** a two-legged race with its own proportions is a third path the table does not offer (SKA
humanoid section; TRL).

````markdown
| It walks on two legs with its own proportions (a race, not a mount) | **Own skeleton, human bone names and axes.** See [a humanoid race on its own skeleton](/guides/custom_creature_race/). |
````

## A4. Custom Creatures: the war ram's thin set

**Target:** `/guides/custom_creatures/` > `#the-reskin-path`
**Action:** replace the sentence "TAOM's war ram is this. Here is the entire Monster definition:" with the
first block; insert the second after the paragraph ending "For free, and correctly."
**Why:** the ram now carries one clip through a thin child of `as_horse`, and only the `_map` child is
required (CMA reskin section and Phase 4).

````markdown
TAOM's war ram started as exactly this. Here is its entire Monster definition as a pure reskin:
````

````markdown
A reskin that needs one clip stays on this path: the war ram's head-butt lives in a thin set,
`action_set="as_war_ram"`, that inherits `as_horse` and adds one action
([the XML](/guides/custom_creature_xml/#the-minimum-if-you-are-reskinning)). With no clip, keep `as_horse`.

!!! warning "A thin set needs its `_map` child"
    The campaign map builds a mounted leader's mount from the Monster's set name plus `_map`, and the
    engine code throws when that set is missing. Build it on the donor's: `as_war_ram_map` inherits
    `as_horse_map`. A `_town_and_village` child is optional.
````

## A5. Custom Creatures: terms used across the pages

**Target:** `/guides/custom_creatures/` > `#what-a-creature-is-to-the-engine`
**Action:** insert after the tip box "Say the last part out loud before you start"
**Why:** "usage" carries three meanings across the pages, "action type" is easily read as the action
itself, and several terms are used before any page defines them (MAP sections 2, 4, 7 and 8; BP;
inventory sections 5, 6 and 11; Native's `action_types.xml` and `action_sets.xml`; the site's Create
LODs page).

````markdown
!!! note "Terms used across these pages"
    * **Usage** has three meanings: a skeleton's `Usage` (`horse`, `human` or `other`; an import
      arrives as `other`); a **clip usage**, a typed record on a clip such as `quad_movement`; and a
      Monster's `monster_usage`, which names the **monster usage set** that tells the engine which
      action to fire, and when.
    * **Action**: a named engine action such as `act_release_overswing_2h`, declared in
      `action_types.xml` as `<action name="..." type="...">` and bound to a clip per action set in
      `action_sets.xml` (where the attribute naming the action is also called `type`).
    * **Action type**: the kind in that declaration's `type` attribute, such as `actt_kick` or
      `actt_defend_shield`, which the engine branches on.
    * **Action code**: the runtime index the engine gives an action name.
    * **The human animation system**: the engine's native animation system for humans (horses have
      their own). Only it applies facial animation and hand poses; which monsters run it is not
      established.
    * **TAOM's reading**: a conclusion from TAOM's reverse engineering, not yet confirmed in game.
    * **Fab**: Epic's asset marketplace (fab.com), where TAOM bought its cave troll and Animalia clips.
    * **LOD0**: a mesh's full-detail level; LOD1 and later take over with distance
      ([Create LODs](/3d/create_lods/)). Race meshes carry morph channels on LOD0 only.
    * **`d6`**: a ragdoll joint type with six lock states, each `locked`, `limited` or `free`; the
      others are `hinge` and `ik`.
    * **BodyProperty**: a face range (a minimum and a maximum) each troop's face is rolled from.
      Its `key`, the **body key**, is the face itself: 128 hex characters from the in-game face editor.
    * **`DeformPercent`**: the FBX field holding a shape key's value; whether the Kit or the engine
      applies it is not established.
````

## A6. Custom Creatures: the example creatures

**Target:** `/guides/custom_creatures/` > `#which-one`
**Action:** insert after the table, before `## What you need installed`
**Why:** every page reuses TAOM's creatures as examples; one table introduces them once (RAM; SIZE and
the elk and Animalia notes; SPI; ELE; MUM; the chariot notes; TRL; the Armory's `skins.xml`).

````markdown
### The creatures on these pages

| Creature | Shape | Path | Size |
|---|---|---|---|
| War ram | a dwarf's war goat | **reskin** of `horse_skeleton`, one head-butt clip in a thin set | 1x: 2.27 m to the horns |
| Great elk | an elk | **reskin** playing the ram's set, the head-butt as an antler charge | 1.1x: 3.38 m to the antler tips |
| Animalia elk and moose | from purchased Fab packs | **reskin** playing the pack's own clips, retargeted | elk 1x, moose 1.5x |
| Giant spider | eight legs | **bespoke**: own 62-bone skeleton, clips and sets | 1x to 1.25x by skin |
| War elephant | an elephant with a howdah crew | **bespoke**: 60-bone skeleton and clips from Artem's ADOD_Beasts | 1x |
| Mumakil | the war elephant with a war tower | **bespoke**: the elephant's skeleton, sets and clips | 3x |
| Chariot | two horses and a cart on one skeleton | **bespoke**: one 60-bone skeleton, data only | not recorded |
| Cave troll | a large humanoid | **race** on `human_skeleton`, scaled by its skin | about 1.9x |
| Hill troll | a hunched humanoid | **race** on its own 28-bone skeleton: human names, order and axes | 3.6 m tall |
| Dwarf | a short humanoid | **race** on its own skeleton, human axes | about 82% of human height |

Byak0's warg, TAOM's control creature, is under Acknowledgements.
````

## A7. Custom Creatures: the attack row

**Target:** `/guides/custom_creatures/` > `#which-one`, the table row "You want it to attack with something
other than a kick"
**Action:** replace the row
**Why:** the row reads as if one authored clip makes the creature attack. The horse rig's only attack clip
is the kick, and the engine fires only the usage set's `kick_action` by itself, at whatever stands behind
the mount; every other attack is code that plays the action and applies the blow, and no test records a
new creature's own kick action firing (RAM "Why a separate action"; the battle page's scripted attacks
section).

````markdown
| You want it to attack with something other than a kick | **Bespoke**, or author one clip onto the existing rig, whose only attack clip is the kick. **A clip does not attack by itself:** code, such as a behaviour tree, has to play it and apply the blow. The only attack the engine fires on its own is the usage set's `kick_action`, and no test records a new creature's own kick action firing. See [scripted creature attacks](/guides/custom_creature_battle/#scripted-creature-attacks). |
````

## B1. Skeleton: the neck under the tail

**Target:** `/guides/custom_creature_skeleton/` > `#what-the-difference-actually-looks-like`
**Action:** replace the paragraph "The mesh rig parents the neck to a **tail** bone. It skins fine anyway.
It animates wrong."
**Why:** the tail parent is how the FBX node order matches the engine's bone list, measured in the Kit
(SKA fact 4).

````markdown
The mesh rig parents the neck to a **tail** bone on purpose. The engine lists `horse_skeleton`'s neck
**last**, after the rear legs and the tail. The Kit stores an animation's bone tracks in FBX node order,
which is depth-first, and never remaps them by name, so the tail parent is what makes the two orders
agree. Re-parent the neck to `horsespine3` and its tracks land in the rear legs' slots. The mesh rig's
bone **frames** are still wrong: see [the export mapping](/guides/custom_creature_animation/#the-export-mapping).
````

## B2. Skeleton: the rig you export from

**Target:** `/guides/custom_creature_skeleton/` > `#rest-frame-maths-if-you-are-rebuilding-the-rig-in-blender`,
the danger box "Knowing the engine stores bones X-along ..."
**Action:** replace the box's second paragraph ("The convention the engine **stores** ...") with the block,
which stays inside the box
**Why:** the export mapping is resolved: the Kit stores bone locals as they are and turns the root 180
degrees (SKA facts 1 and 2).

````markdown
    The convention the engine **stores** rest frames in and the export setting that reproduces a clip
    are two different questions. Export with `primary_bone_axis='Y'`, `secondary_bone_axis='X'`, **from
    an armature whose bone frames equal the engine's**, and bake a 180 degree turn about world Z into
    the keyed pose. The X-along construction above is for viewing the rig; how to build the export rig
    is in [the export mapping](/guides/custom_creature_animation/#the-export-mapping).
````

## B3. Skeleton: how the Kit binds materials

**Target:** `/guides/custom_creature_skeleton/` > `#materials-do-not-survive-an-fbx-re-import`
**Action:** replace the heading and the first paragraph ("An FBX carries only the material slots ...")
with the first block, and the closing line ("Re-assign materials in the Kit ...") with the second
**Why:** the Kit binds by name across the module and a stale same-named material wins silently; whether
hand bindings survive is reported both ways, so the heading and text say only that they may not (LES;
inventory section 7). The anchor becomes `#check-materials-after-every-fbx-re-import`.

````markdown
## Check materials after every FBX re-import

The Kit binds each imported mesh to the material its FBX names, lowercased, looked up across the whole
module. No match logs `Unable to find material` and leaves the mesh unbound. **An older Kit material with
the same name wins silently**: that is how TAOM's new hill troll came out wearing the old model's
textures. A re-import may also reset bindings assigned by hand in the editor.
````

````markdown
After every re-import, check each mesh at every LOD. Fix a name in Blender's material panel or with
TAOM's optional [fbx_remap_materials.py](https://github.com/haterade22/TAOM/blob/bannerlord-1.5.x/tools/blender/fbx_remap_materials.py).
````

## B4. Skeleton: physics fingerprints and re-imports

**Target:** `/guides/custom_creature_skeleton/` > `#physics-bodies-and-ragdoll-joints`
**Action:** replace the sentence "An FBX re-import rebuilds the bone definition but not this data." with
the first block; replace the table with the second
**Why:** the working spider skeleton is `horse`, the human's joints are counted, a race takes the human's
physics, and a re-import that keeps the skeleton kept its physics (SPI; SKA humanoid section; LES).

````markdown
A re-import that left the skeleton unchanged kept this data byte for byte in TAOM's checks; one that
rebuilds the skeleton empties it. Compare **decompressed** skeleton segments, not file bytes: the Kit's
LZ4 output differs on every compile.
````

````markdown
| Skeleton | Bones | Usage | Ragdoll constraints |
|---|---|---|---|
| `human_skeleton` | 28 | `human` | 34 (15 `d6`, 19 `ik`) |
| `horse_skeleton` | 32 | `horse` | |
| `skeleton_warg` | 49 | `horse` | 48 |
| `elephant_skeleton` | 60 | `horse` | 59 |
| `spider_skeleton` | 62 | **`horse`** | |
| `troll_skeleton_a` (a humanoid race) | 28 | `human` | 34 |

The human's `ik` joints are ragdoll constraints, not animation IK. TAOM's spider skeleton launch-crashed
as a raw `other` import with no constraints; as `horse` it fights. A race on its own skeleton
[carries the human's physics across](/guides/custom_creature_race/#physics-copy-the-humans-through-the-rest-pose).
````

## B5. Skeleton: hit capsules

**Target:** `/guides/custom_creature_skeleton/` > end of `#physics-bodies-and-ragdoll-joints`, before `## Next`
**Action:** insert the block
**Why:** weapons meet the per-bone hit capsule, and the Kit's default one is thin; the battle page owns
the detail (SKA "Hit capsules").

````markdown
### Hit capsules are what weapons strike

Each body has a **hit** capsule, which weapons and arrows strike (read from the field names and body
zones; no in-game hit test yet), and a **ragdoll** capsule, which only moves the corpse. Other agents
walk into the Monster's `body_capsule` instead. The Kit's default hit capsule is a rod whose radius is
about a ninth of its length: TAOM's war elephant had 41 of 60 like that, with under half its skin inside
any hit capsule. See [big creatures in battle](/guides/custom_creature_battle/#three-collision-layers).
````

## B6. Skeleton: the version box

**Target:** `/guides/custom_creature_skeleton/` > the `!!! note "Version"` box
**Action:** replace the box
**Why:** the export facts, the humanoid physics and the hit capsules were measured with v1.5.3 installed
(SKA, read against the date TAOM moved to v1.5.3).

````markdown
!!! note "Version"
    Measured against **Bannerlord v1.4.8**, except the export facts in the danger box, the humanoid
    physics and the hit capsules, measured on **v1.5.3**.
````

## B7. Skeleton: the packages TpacTool cannot open

**Target:** `/guides/custom_creature_skeleton/` > `#getting-the-real-skeleton`, the warning box "Do not give
up on the first package that fails"
**Action:** append the block to the box, after its only paragraph (the block's blank first line
separates the two)
**Why:** the new race and clip inspector pages resolve vanilla clips through `animation_clips.tpac`, which
this box lists as unreadable; TAOM reads its items through TpacTool's library (the docstring of
`read_anim_keyframes_tpac.ps1`).

````markdown

    The clip and master items inside are still readable from code. TAOM's optional
    [read_anim_keyframes_tpac.ps1](https://github.com/haterade22/TAOM/blob/bannerlord-1.5.x/tools/read_anim_keyframes_tpac.ps1)
    loads `animation_clips.tpac` and `animations.tpac` through TpacTool.Lib, the library in
    TpacTool's `bin` folder, and with `-ByClip` resolves each clip name to its master. Its default
    paths are one machine's.
````

## C1. Animation Clips: what `quad_movement` needs

**Target:** `/guides/custom_creature_animation/` > `#quad_movement-or-the-six-hour-crash`
**Action:** replace the bold paragraph "**Every gait clip compiled for ...** Walks, runs, ..." with the first
block, and item 4 of the numbered list with the second
**Why:** the usage is what the engine reads unchecked; step points are not shown to matter for safety,
and `+0x10` is a v1.4.x observation (MAP sections 5, 9 and 11).

````markdown
**Every gait clip in a `movement_system="quadrupedal"` action set must carry the `quad_movement` clip
usage.** Walks, runs, strafes, turns in motion, jumps. TAOM's reverse engineering of v1.5.3 found ten
places that read it with no null check. Step points make the footsteps (with `make_walk_sound`); they
are not shown to be needed against the crash, and 42 of the 142 vanilla clips with `quad_movement` have
none.
````

````markdown
4. Access violation at offset `+0x10`, in **every** mount context at once: the inventory thumbnail, the
   character tableau, and mission deployment. That offset was seen on v1.4.x; the v1.5.3 offset has not
   been observed.
````

## C2. Animation Clips: the recipes

**Target:** `/guides/custom_creature_animation/` > `#per-category-recipe`
**Action:** replace the sentence "A working quadruped's clips, by category:" and its table with the block
**Why:** vanilla gallops carry no `cyclic` and no native requirement for it was found; clips on the horse
rig need vanilla's horse recipe (MAP section 11; FLG "Vanilla HORSE and mount recipes").

````markdown
A working quadruped's clips on its own skeleton, by category:

| Clip category | Flags | Clip usages |
|---|---|---|
| gait (walk, run, gallop, turn, strafe) | `make_walk_sound` | **`quad_movement`**, plus step points for footsteps |
| attack | `client_prediction`, `lock_movement`, `enforce_all` | none |
| death | `make_bodyfall_sound`, `client_prediction`, `do_not_keep_track_of_sound`, `enforce_all`, `update_bounding_volume` | none |
| rear | `lock_movement`, `enforce_lowerbody` | none |

Gallop runs no longer get `cyclic`: vanilla's carry only `make_walk_sound`, and no native requirement
was found. A clip on the **horse** rig should copy vanilla's horse recipe:

| Vanilla clip | Priority | Flags | Blend in / out |
|---|---|---|---|
| `horse_kick` (attack) | 34 | `enforce_lowerbody`, `enforce_all` | 0.2 / 0.4 |
| `horse_rear` | 74 | `lock_movement`, `enforce_lowerbody`, `update_bounding_volume`, `ignore_slope` | 0.3 / 0.3 |
| `horse_hit_from_front` | 2 | `enforce_lowerbody` | 0.2 / 0.4 |

**Every vanilla horse clip read carries `enforce_lowerbody`.** Why priority decides what shows in battle:
[the clip inspector](/guides/custom_creature_clip_inspector/#priority-why-a-clip-plays-in-the-viewer-and-not-in-battle).
````

## C3. Animation Clips: compiling in the Kit

**Target:** `/guides/custom_creature_animation/` > `#compiling-in-the-kit`
**Action:** replace the first paragraph ("Import as a **Skeleton Animation** ... Blend in around 0.1.") with
the block, whose warning box goes before the existing rename box
**Why:** frame 0 is the rest frame, a clip name has 63 usable characters, Always keep in memory is the
conservative Loading Type for a bound clip though not a proven requirement, and a new clip's priority 0
and empty flags can hide it in battle; the war ram's fix is not yet seen in battle (SKA fact 3; MAP
sections 2, 3 and 6; FLG; RAM).

````markdown
Import as a **Skeleton Animation**, set its **Owner Skeleton**, then create an **Animation Clip**:
**Source 1 = 1** and Source 2 = the master's last frame, because frame 0 is the rest frame
([the export mapping](/guides/custom_creature_animation/#the-export-mapping)); Duration must come out
above zero. Set Loading Type to Always keep in memory (0) on any clip you bind: the conservative
choice, not a proven requirement
([why](/guides/custom_creature_clip_inspector/#the-fields-one-by-one)). Keep the name to **63
characters** (the Kit warns `Could not set fixed-size(64) string`). Every field:
[the clip inspector](/guides/custom_creature_clip_inspector/).

!!! warning "A new clip starts at priority 0 with no flags, and may never show in battle"
    The model viewer plays it unopposed; in battle, by TAOM's reading, locomotion takes the channel
    back. TAOM's war ram logged 1,300 head-butts at priority 0 and 1,005 at 34 with no visible head
    drop; it now copies `horse_kick`'s flags and blends too, not yet seen in battle. Copy the nearest
    vanilla clip's whole recipe and judge it in a battle.
````

## C4. Animation Clips: the export mapping

**Target:** `/guides/custom_creature_animation/` > `## An honest status note` (`#an-honest-status-note`)
**Action:** replace the whole section, heading included (the new anchor is `#the-export-mapping`)
**Why:** the note's open question is settled by reading Kit-compiled masters back on `human_skeleton` and
`horse_skeleton` (SKA "Status" section, facts 1 to 4; UEP "The clip stage").

````markdown
## The export mapping

Measured by reading Kit-compiled masters back against the FBX and the engine's rest frames, on
`human_skeleton` (52 clips) and `horse_skeleton`:

1. **The Kit stores an FBX's bone-local transforms as they are**, with no axis conversion. So the
   armature you export from must carry the engine's bone frames: each bone's `matrix_local` equal to
   the accumulated rest frame (`tail = head + Y column`, `align_roll(Z column)`). The bones then draw
   sideways in Blender, harmlessly. Export with `primary_bone_axis='Y'`, `secondary_bone_axis='X'`.
2. **The root is stored as its FBX world pose turned 180 degrees about Z,** with the armature object's
   transform applied on top. Keep the object at identity and bake the 180 degree turn into the pose.
3. **Frame 0 is the rest frame.** The root position track is stored relative to it, and every vanilla
   master opens on rest with its clip at Source 1 = 1. Open on a posed frame and the pelvis offset is
   lost: a hunched character stands too high and its feet skate. Key rest at frame 0, motion from
   frame 1 (the site's [animation notes](/modding/animations/#feet-above-the-ground) agree). Vanilla
   walks carry one root track, the pelvis bob; drop a pack's root travel and give it to the engine as
   the `bip_mov_ik` usage's loop displacement.
4. **Tracks are stored in FBX node order, and the engine reads slot i as bone i of the skeleton's
   list.** Blender writes nodes depth-first. `human_skeleton`'s list is depth-first; `horse_skeleton`'s
   is not (neck last), so a horse clip from the true hierarchy plays the tail on the neck. Export from a
   hierarchy whose depth-first walk equals the list (`horseneck1` under `horsetail3`, as TaleWorlds'
   own horse and goat FBX have it), with each local relative to its **engine** parent.
5. **The take name is the master's identity.** The master is named after the FBX take (the Blender
   action). A re-import under the same take keeps its GUID and its clips. A `.001` suffix makes a new
   master with an empty skeleton, and the old clip loses its animation.

TpacTool's `FixBoneForBlender` export has bone frames 90 to 180 degrees off the engine's: use its
meshes, not its bones. Optional helpers, where Blender and TpacTool do the same by hand: TAOM's
[transfer_clip_to_engine_rig.py](https://github.com/haterade22/TAOM/blob/bannerlord-1.5.x/tools/blender/transfer_clip_to_engine_rig.py)
moves a clip onto an engine-frame rig with points 2 to 4 handled;
[read_anim_keyframes_tpac.ps1](https://github.com/haterade22/TAOM/blob/bannerlord-1.5.x/tools/read_anim_keyframes_tpac.ps1)
reads a master back (set `-TpacToolBin`, `-NativeDir` and `-OutDir`; the defaults are one machine's).
````

## C5. Animation Clips: two diagnosing steps

**Target:** `/guides/custom_creature_animation/` > `#diagnosing-a-clip-that-looks-right-in-blender-and-wrong-in-game`
**Action:** insert before step 1 of the numbered list, then renumber the existing four steps as 3 to 6
**Why:** reading the compiled master back and checking frame 0 settled in an afternoon what side-view
renders did not (SKA "Diagnosing a clip").

````markdown
1. **Read the compiled master back** in TpacTool and compare each bone's rotation with the FBX's local
   and the engine's rest local. If limbs play another limb's motion, match each slot at frame 0 to the
   bone whose rest it equals; a mismatch means the node order is wrong
   ([the export mapping](/guides/custom_creature_animation/#the-export-mapping), point 4).
2. **Check frame 0 is the rest pose** when the limbs move right but the body floats or the feet skate.
````

## C6. Animation Clips: what `anf_displace_position` does

**Target:** `/guides/custom_creature_animation/` > `#locomotion-clips-are-authored-in-place`
**Action:** replace the paragraph "The corresponding flag is `anf_displace_position`, which turns root
motion on. ..." with the block
**Why:** the flag does not read baked root travel: it moves the agent by the displacement clip usage's
vector and reads null without that usage (MAP sections 4 and 5).

````markdown
`anf_displace_position` is not root motion from your keys: it moves the agent by the vector in the
clip's **displacement** clip usage, up to that usage's end progress, and without the usage the engine
reads null. Vanilla sets it on 337 clips, all with that usage, such as `death_fall_front`. **Keep it
off walks and runs:** their travel is the `bip_mov_ik` or `quad_movement` usage's loop displacement.
See [the clip inspector](/guides/custom_creature_clip_inspector/#clip-usages).
````

## C7. Animation Clips: renaming a clip

**Target:** `/guides/custom_creature_animation/` > `#compiling-in-the-kit`, the danger box "Do not rename a
clip inside the Modding Kit"
**Action:** replace both paragraphs of the box with the block, keeping the box's title
**Why:** a rename in the Kit corrupts the clip, and the restart the box calls a remedy is reported to
clear the Kit's state, but no source records a clip renamed in the Kit being checked afterwards, so it
is not a route to a usable clip. The game registers a clip by the item name stored inside its
`_anm.tpac`, so renaming only the file leaves the old name registered (MAP sections 1 and 6; the
docstring of `rename_anim_clip_tpac.py`).

````markdown
    Renaming corrupts it. The Kit keeps resolving the old name, the inspector reports
    `Size in KB = 0`, it refuses to save, the model viewer draws a scrambled pose, and the renamed
    file can vanish outright. Restarting the tools clears that state, but nobody has recorded
    checking a clip renamed in the Kit afterwards: **no rename inside the Kit, with or without a
    restart, is known to give a usable clip.**

    Instead: create the clip on the default `new_animation_clip` name, set its source range and
    flags, save and **close the Kit**. Then rename the clip **item** inside its `_anm.tpac`, not just
    the file: the game registers a clip by that stored name. TAOM's optional
    [rename_anim_clip_tpac.py](https://github.com/haterade22/TAOM/blob/bannerlord-1.5.x/tools/rename_anim_clip_tpac.py)
    does it, keeping the item's GUID, and writes `<name>_anm.tpac`; a hex editor can make the same
    two-field edit ([the byte layout](/guides/custom_creature_clip_inspector/#clip-names)). Reopen
    the Kit.
````

## D1. XML: the thin set, in XML

**Target:** `/guides/custom_creature_xml/` > `#the-minimum-if-you-are-reskinning`
**Action:** insert after the tip box "`<Flags>` is only reassigned if you supply a `<Flags>` child element"
**Why:** the ram's Monster now names `as_war_ram`, a thin child of `as_horse`; only `_map` is required
(CMA reskin section and Phase 4; XML copied from TAOM's `action_sets.xml`).

````markdown
When the war ram gained one clip, a head-butt, its Monster moved to `action_set="as_war_ram"`, a thin
set over `as_horse`. Two of its sets, from TAOM's `action_sets.xml`:

```xml
<action_set id="as_war_ram" skeleton="horse_skeleton" base_set="as_horse">
	<action type="act_war_ram_butt" animation="war_ram_butt" />
</action_set>
<action_set id="as_war_ram_map" skeleton="horse_skeleton" base_set="as_horse_map">
	<action type="act_war_ram_butt" animation="war_ram_butt" />
</action_set>
```

The attack is its own action, typed `actt_kick`, not a re-pointed `act_horse_kick`, which the inherited
usage set fires itself as its `kick_action`. The `_map` set is required and builds on the donor's
([below](/guides/custom_creature_xml/#action_typesxml-and-action_setsxml)); a `_town_and_village` set is
optional.
````

## D2. XML: where the rider partial goes

**Target:** `/guides/custom_creature_xml/` > `#action_typesxml-and-action_setsxml`, the third bullet
("The **rider partial** ...")
**Action:** replace the bullet
**Why:** on v1.5.3 the managed merge folds every module's `as_human_warrior` block into Native's before
the engine parses, so position should not matter; code read, not tested (MAP sections 7 and 11).

````markdown
* The **rider partial**, an `<action_set id="as_human_warrior">` fragment carrying your `act_<mount>_*`
  rider actions. On v1.5.3 the game merges every module's `as_human_warrior` into Native's before the
  engine parses any set, so its position should not matter (read from the code, not tested in game).
  At the top of the file it is harmless.
````

## D3. XML: a clip name that resolves to nothing

**Target:** `/guides/custom_creature_xml/` > `#action_typesxml-and-action_setsxml`
**Action:** insert after the warning box "A root-level `<action>` boots the client and kills a dedicated server"
**Why:** on v1.5.3 a phantom `animation=` logs and keeps the slot (MAP section 7, "Action set parse").

````markdown
!!! note "A missing clip name logs and keeps the slot"
    On v1.5.3 an `animation=` that names no registered clip logs `Could not find animation` and leaves
    the slot as it was: inherited from the `base_set`, or empty. A crash needs a later reader that
    skips the empty-slot check, so search the log for that line after every clip rename.
````

## D4. XML: size

**Target:** `/guides/custom_creature_xml/` > `#size`
**Action:** replace the danger box "`body_length` scales the RIDER too, not just the mount"
**Why:** in game the rider is not scaled, although the managed code reads as if it would (MUM "RESOLVED";
CMA reskin notes).

````markdown
!!! warning "`body_length` scales the mount, not the rider, and not your own offsets"
    Skeleton, capsules and ragdoll scale together; the rider does not. TAOM's mumakil, a war elephant at
    `body_length="300"`, carries a human-size rider beside human-size crew. The managed code reads as if
    the rider would scale, but the game does not.

    Anything your own code positions against the creature (a platform, a seat, an attack reach) does
    not scale either: read `Agent.AgentScale`. More in
    [big creatures in battle](/guides/custom_creature_battle/#size).
````

## D5. XML: the hit-reaction half of the reskin trap

**Target:** `/guides/custom_creature_xml/` > `#the-reskin-trap`
**Action:** replace the paragraph starting "**`act_horse_strike_front` and `_back` are typed `actt_mount_strike`,**"
**Why:** `Agent.IsInBeingStruckAction` tests a half-open range, so type 52 is not "being struck"; the clips
are the problem (CMA "The price of a reskin").

````markdown
**`act_horse_strike_front` and `_back` play hit reactions,** clips literally named `horse_hit_from_front`
and `horse_hit_from_back`. Bind an attack to those and the creature flinches as though hit at the moment
it deals damage. The type, `actt_mount_strike` (52), is harmless on its own:
`Agent.IsInBeingStruckAction` reads only 48 to 51 as being struck, and the warg's `actt_mount_strike`
attacks play.
````

## D6. XML: which set the `_map` child builds on

**Target:** `/guides/custom_creature_xml/` > `#action_typesxml-and-action_setsxml`, the first bullet
("**`as_<creature>_map` is required.**")
**Action:** replace the bullet's last sentence, "Author it as `base_set="as_<creature>"` with no actions.",
with the block
**Why:** the live sentence and D1 disagree: D1's ram builds its `_map` on its donor's `_map`, and
Native's `as_horse_map` inherits `as_horse` and binds the map clips, so a reskin keeps them that way
(CMA reskin section and Phase 4; Native and TAOM `action_sets.xml`).

````markdown
  A bespoke creature builds it on `base_set="as_<creature>"`; TAOM's warg, spider, elephant and
  chariot also bind the four `act_map_mount_attack_*` actions to a clip of their own, as Native's
  `as_horse_map` does. A reskin's thin set builds on its donor's `_map` instead, keeping the donor's
  map clips: `as_war_ram_map` on `as_horse_map`, which Native builds on `as_horse`. A reskin that
  keeps `as_horse` gets `as_horse_map` for free.
````

## E1. Reference Tables: what code can do with flags

**Target:** `/guides/custom_creature_reference/` > `#how-the-word-is-built`
**Action:** replace the warning box "You cannot toggle these from a Harmony patch"
**Why:** flags are saved as names, nine flags are tested in managed code, and a request can add flags and
replace the priority byte but not clear authored flags (MAP sections 4 and 11).

````markdown
!!! warning "Code can add flags to one request; it cannot clear a clip's own"
    The Kit saves flags as a list of **names**: an unknown name is dropped silently, and a bit with no
    name cannot be saved. TAOM's reverse engineering of v1.5.3 found nine flags tested in seven managed
    files. `SetActionChannel` can OR extra flags into one request and replace
    its priority byte, but cannot clear a flag the clip was authored with. A row marked "no native
    consumer found" describes an unverified effect.
````

## E2. Reference Tables: flag rows

**Target:** `/guides/custom_creature_reference/` > the four flag tables
**Action:** replace the row for each flag below, keeping the columns; then append one sentence to six rows
**Why:** each row was measured against vanilla's 6,177 clips and the v1.5.3 engine code (MAP sections 4,
5, 11 and 12; inventory section 18).

In `#movement-and-root-motion`:

````markdown
| `anf_synch_with_movement` | `0x2000000` | Takes the clip's progress from the agent's movement phase. On 65 vanilla rider and head-turn overlays, **none** of 435 human gait clips, and Artem's working elephant walks: allowed on a walk, not required. |
| `anf_displace_position` | `0x400000000000` | Moves the agent by the **displacement** clip usage's vector. **Needs that usage,** or the engine reads null. Deaths and other one-shots, not locomotion. |
| `anf_use_last_step_point_as_data` | `0x800` | Silences the fourth step point. On 62 vanilla equip clips; what reads the point as data is not established. |
| `anf_align_with_ground` | `0x100000000000` | On a human, ramps ground alignment over the **blend** clip usage's range. **Needs that usage,** or the engine reads null. |
````

In `#body-enforcement-and-lifecycle`:

````markdown
| `anf_cyclic` | `0x4000000000` | **Loops** the action at clip end. Not required on locomotion: 0 of 435 vanilla human gait clips and 48 of 142 `quad_movement` clips have it. A clip reused for an inventory or conversation idle takes that action's vanilla flags; without `cyclic` it plays once, by TAOM's reading. |
| `anf_disable_alternative_randomization` | `0x80000000` | **Not a clip flag:** no checkbox or saved name exists. Code passes it to `SetActionChannel` to skip the random pick among alternatives. |
| `anf_animation_layer_flags_mask` | `0xFFFF000000000` | Bits 36 to 51: ordinary Kit checkboxes, also passed to the renderer as layer bits. |
````

In `#sound-and-networking`:

````markdown
| `anf_spawn_particle` | `0x800000000` | Spawns the **particle** clip usage's particle at its bone. **Needs that usage,** or the engine reads null. |
````

Append to the "What it does" cell of `anf_affected_by_movement`, `anf_ignore_slope`,
`anf_ignore_scale_on_root_position`, `anf_disable_hand_ik`, `anf_disable_agent_agent_collisions` and
`anf_ignore_static_body_collisions`:

````markdown
**No native consumer found on v1.5.3.**
````

## E3. Reference Tables: foot IK

**Target:** `/guides/custom_creature_reference/` > `#ik-collision-physics`, the warning "The vanilla rig
grounds only two feet"
**Action:** replace the box
**Why:** the two-feet claim was inferred from the flag's name; the code shows foot IK only in the human
animation system (MAP section 4; FLG Cat 4).

````markdown
!!! note "Foot IK belongs to the human animation system"
    `anf_disable_foot_ik` makes the engine's human animation system skip foot IK. Nothing is established
    about foot grounding on a quadruped, or about which monsters run that system.
````

## E4. Reference Tables: per-clip recipe

**Target:** `/guides/custom_creature_reference/` > `#per-clip-recipe`
**Action:** replace everything from "Confirmed against shipped clips:" down to the note "Flags are not the
same thing as clip usages", keeping the note
**Why:** the old recipes rested on flag names; these are read from vanilla's own clips (FLG "Vanilla HUMAN
recipes" and "Vanilla HORSE and mount recipes").

````markdown
Read from vanilla's own clips on v1.5.3; copy the nearest and judge it in a battle.

| Vanilla clip | Priority | Flags | Clip usages | Blend in / out |
|---|---|---|---|---|
| `walk_forward_unarmed` | 0 | `make_walk_sound` | `bip_mov_ik` | 0.3 / 0 |
| `troop_stand_unarmed_1` (idle) | 1 | `allow_head_movement` | none | 0.5 / 0 |
| `strike_chest_front` (hit reaction) | 80 | `client_prediction`, `restart`, `enable_hand_blend_ik`, `enforce_root_rotation`, `update_bounding_volume` | none | 0.1 / 0.1 |
| `death_fall_front` | 95 | `make_bodyfall_sound`, `client_prediction`, `keep`, `disable_hand_ik`, `lock_movement`, `enforce_all`, `enforce_root_rotation`, `disable_foot_ik`, `update_bounding_volume`, `align_with_ground`, `displace_position`, `reset_camera_height` | `blend`, `displacement` | 0.3 / 0 |
| `horse_kick` | 34 | `enforce_lowerbody`, `enforce_all` | not recorded | 0.2 / 0.4 |

Every vanilla horse clip read carries `enforce_lowerbody`. Artem's elephant walks use
`synch_with_movement` and `cyclic` instead, and also work. Runtime effects:
[the clip inspector](/guides/custom_creature_clip_inspector/#flags-at-runtime).
````

## E5. Reference Tables: being struck

**Target:** `/guides/custom_creature_reference/` > `#action-types`
**Action:** replace the sentence "The engine's own classification constants, which is what makes the
[reskin trap] work the way it does:" with the first block, and the sentence "Anything in
`StrikeBegin .. StrikeEnd` is read by `Agent.IsInBeingStruckAction` as **being struck**." with the second
**Why:** the range test is half-open, and after D5 only the `Rear` half of the reskin trap turns on a
type; the strike half is the clips (CMA "The price of a reskin"; the live XML page's `#the-reskin-trap`).

````markdown
The engine's own classification constants. Of these, only `Rear` drives the
[reskin trap](/guides/custom_creature_xml/#the-reskin-trap): `Agent.Mount` refuses a mount whose
channel-0 action is typed `Rear`.
````

````markdown
`Agent.IsInBeingStruckAction` reads types from `StrikeBegin` up to but not including `StrikeEnd` (48 to
51) as **being struck**, so `MountStrike` (52) is not included.
````

## E6. Reference Tables: the item checksum

**Target:** `/guides/custom_creature_reference/` > `#item`
**Action:** insert after the paragraph starting "Segment header:"
**Why:** a tool that edits metadata must recompute the checksum; the formula matched every clip measured
(MAP section 6).

````markdown
The item `checksum` is xxHash64, seed 0, over `metadata_size` (as an int64) followed by the metadata;
each segment carries xxHash64 of its **decompressed** payload. Recompute it after any metadata edit
(TAOM's [tpac_fix_item_checksums.py](https://github.com/haterade22/TAOM/blob/bannerlord-1.5.x/tools/tpac_fix_item_checksums.py)
does). TpacTool.Lib writes zero, and such a package loads only with a
[RuntimeDataCache entry](/guides/custom_creature_clip_inspector/#what-save-writes-and-the-runtimedatacache).
````

## E7. Reference Tables: skeleton fingerprints

**Target:** `/guides/custom_creature_reference/` > `#known-good-skeleton-fingerprints`
**Action:** replace the table with B4's table plus the existing `chariot_skeleton` row; replace "which an
FBX re-import does silently" with the first block, and "Healthy creatures in this sample" under the table
with the second
**Why:** same corrections as B4 (SPI; SKA humanoid section; LES). The 48 to 61 range under the table was
measured on mounts, and with the humanoid rows beside it "creatures" would make 34 read as broken (CMA,
"The ways a re-export breaks a working mount").

````markdown
which a re-import that rebuilds the skeleton does silently. A humanoid race carries the human's 34.
````

````markdown
Healthy mounts in this sample
````

## E8. Reference Tables: version and credits

**Target:** `/guides/custom_creature_reference/` > the `!!! note "Version"` box, and the first paragraph of
`#sources-and-credits`
**Action:** replace the box with the first block and the paragraph with the second
**Why:** flag values and effects were re-checked on v1.5.3 (MAP section 4).

````markdown
!!! note "Version"
    Flag values and their effects are checked against **Bannerlord v1.5.3**, the effects through TAOM's
    reverse engineering of its game and Modding Kit DLLs. The rest was measured on v1.4.8 to v1.5.3.
````

````markdown
Flag values and engine constants are read from Bannerlord v1.5.3. The container format derives from
[TpacTool](https://github.com/szszss/TpacTool) by szszss, MIT licensed.
````

## F1. Troubleshooting: the version box

**Target:** `/guides/custom_creature_troubleshooting/` > the `!!! note "Version"` box
**Action:** replace the box
**Why:** the page now carries v1.5.3 crash signatures beside older ones (MAP section 9).

````markdown
!!! note "Version"
    Measured on **Bannerlord v1.4.6 to v1.5.3**; each crash offset names the version it was seen on,
    because offsets move with every engine update. Several of these only became crashes in **1.4.6**:
    see [The 1.4.6 rule](/guides/custom_creature_xml/#the-146-rule).
````

## F2. Troubleshooting: quick index rows

**Target:** `/guides/custom_creature_troubleshooting/` > `#quick-index`
**Action:** replace the row "Creature flinches while dealing damage" with the first row, and add the rest
before the row "Kit clip reports `Size in KB = 0` and will not save"
**Why:** each row points at the page that now owns the cause (inventory sections 6 to 16; MAP section 9).

````markdown
| Creature flinches while dealing damage | [An attack bound to a hit-reaction clip](/guides/custom_creature_xml/#the-reskin-trap) |
| Crash on a race's **first swing** or blocked recoil | [A swing clip with no melee-table row](/guides/custom_creature_melee/#the-first-swing-crash) |
| Crash about a second into deployment with a new race | [A race head with no face morph channels](/guides/custom_creature_race/#face-and-hand-morph-channels) |
| A package never appears, log silent | [No RuntimeDataCache entry](/guides/custom_creature_clip_inspector/#what-save-writes-and-the-runtimedatacache) |
| A mount's clip plays in the Kit viewer, never in battle | [Its priority and flags](/guides/custom_creature_clip_inspector/#priority-why-a-clip-plays-in-the-viewer-and-not-in-battle) |
| Body floats or feet skate, limbs right | [Frame 0 is not the rest pose](/guides/custom_creature_animation/#the-export-mapping) |
| Some limbs play another limb's motion | [Bone-track order](/guides/custom_creature_animation/#the-export-mapping) |
| Master renamed `<name>.001`, clip lost its animation | [The take name changed](/guides/custom_creature_animation/#the-export-mapping) |
| `Assigned skeleton animation not found` after a re-import | A junk `<armature>_notused.001` skeleton: delete it, re-import the FBX as an animation, remake the clip |
| Blows pass through parts of the creature | [Default hit capsules](/guides/custom_creature_battle/#three-collision-layers) |
| Broad units stand inside each other | [Foot units are spaced for a human](/guides/custom_creature_battle/#formation-spacing) |
| A big race carries the formation banner | [Banner jobs are open to any humanoid race](/guides/custom_creature_battle/#banners-and-other-jobs-meant-for-humans) |
| A race's fingers never close on the weapon | [No hand-pose morph channels](/guides/custom_creature_race/#face-and-hand-morph-channels) |
| A race character renders as a toddler | [No `<face>`](/guides/custom_creature_race/#what-a-race-is-to-the-engine) |
| An idle plays once in the inventory or a conversation | [A reused clip without that action's flags](/guides/custom_creature_clip_inspector/#flags-at-runtime) |
| Kit warns `Could not set fixed-size(64) string` | [Clip name over 63 characters](/guides/custom_creature_clip_inspector/#clip-names) |
| `Unable to find material` after a re-import | [A material name the module lacks](/guides/custom_creature_skeleton/#check-materials-after-every-fbx-re-import) |
| Null read while a clip plays | [A flag without its clip usage](/guides/custom_creature_clip_inspector/#clip-usages) |
| Crash just after a clip ends | [A Continue to action the set lacks](/guides/custom_creature_clip_inspector/#the-fields-one-by-one) (likely; not reproduced) |
````

## F3. Troubleshooting: the `quad_movement` crash

**Target:** `/guides/custom_creature_troubleshooting/` > `#crash-in-every-mount-context-at-once`
**Action:** replace the **Signature.** paragraph's first sentence with the first block, and the **Fix.**
paragraph with the second
**Why:** `+0x10` is a v1.4.x observation, and step points are not shown to matter for this crash (MAP
sections 5, 9 and 11).

````markdown
**Signature.** Access violation at offset `+0x10`, in `Skeleton.TickAnimations` or
`GetWalkSpeedLimitOfMountable`, seen on v1.4.x; the v1.5.3 offset has not been observed.
````

````markdown
**Fix.** [Add the `quad_movement` clip usage](/guides/custom_creature_animation/#quad_movement-or-the-six-hour-crash).
It is a Clip **usage**, not a Flag. Step points make footsteps; they are not shown to prevent this crash.
````

## F4. Troubleshooting: three new crashes

**Target:** `/guides/custom_creature_troubleshooting/` > after `#a-clip-reports-zero-size-and-will-not-save`
**Action:** insert the three entries, before the section F5 adds
**Why:** these are the v1.5.3 failures TAOM hit with a race and a hand-built package, each owned by a new
page (TRL "The swing CTD" and "Face morph channels"; MAP sections 3, 6 and 9).

````markdown
## Crash on the first swing

**Signature.** On v1.5.3, an access violation at `TaleWorlds.Native.dll+0x6590B9` reading `0x8` to
`0x50` when a race first swings or recoils from a block. The wind-ups play; the release never does.

**Cause.** The clip bound to a release or blocked action has no row in the engine's melee attack table,
or the action is unbound. Only the Kit's "Blends with animation" box gives a clip a row, so a clip
copied from a vanilla template must be keyed again with its own name.

**Fix.** [Melee attack clips](/guides/custom_creature_melee/#three-ways-to-make-a-swing-safe).

## Crash about a second into deployment

**Signature.** On v1.5.3, an access violation at `TaleWorlds.Native.dll+0x57070C` the first time an agent
of a new race is built, in the engine's static face morph ("No morph data found for face mesh").

**Cause.** The race's LOD0 head, eye or mouth has no face morph channels: the Kit writes an empty morph
record, and the engine checks only its pointer.

**Fix.** Give each of them the 101 face morph channels a working race head carries. Zero-offset shape
keys stop the crash, so no reference head is needed; check by counting 101 each on the LOD0 head, eye
and mouth. See [the race page](/guides/custom_creature_race/#face-and-hand-morph-channels).

## A package's items never appear

**Signature.** Meshes, skins or clips from one package never show, and the log is silent.

**Cause.** The game renders a package only when `<module>/RuntimeDataCache/<package GUID>.rdc` exists,
and only the Modding Kit writes it, on save. Animation masters are the exception.

**Fix.** Open the module in the Kit and save. To prove a skip, redefine an existing item name in a
throwaway package: no `Overriding item` log line means it never loaded. See
[the clip inspector](/guides/custom_creature_clip_inspector/#what-save-writes-and-the-runtimedatacache).
````

## F5. Troubleshooting: debugging a native crash

**Target:** `/guides/custom_creature_troubleshooting/` > before `#two-debugging-rules-worth-more-than-any-single-entry-above`
**Action:** insert the section (anchor `#debugging-a-native-crash`)
**Why:** this method named every native creature crash TAOM has fixed, without symbols (TRIAGE Phases 1,
2 and 4; CMA Phase 8; inventory section 16).

````markdown
## Debugging a native crash

Windows logs the faulting module and offset of a crash to desktop in its Application log, so no symbols
are needed. In PowerShell:

```powershell
Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='Application Error'; StartTime=(Get-Date).AddHours(-6)} |
  Where-Object { $_.Message -match "Bannerlord" } |
  ForEach-Object { ($_.Message -split "`n" | Select-Object -First 8) -join "`n"; "---" }
```

**The `Fault offset` is the same for one crash site on one engine build, so compare it across runs:**
the same offset after a fix means the fix failed; a new one is a different crash.

* A crash held by a debugger never reaches that log. Use instruction pointer minus module base, both
  from the same run: the DLL can load at a new address on each launch.
* The Modding Kit's own `TaleWorlds.Native.dll` has different offsets and updates on its own schedule.
  Compare `bin/Win64_Shipping_wEditor/Version.xml` with the game's version before trusting one.
* An assert dialog is a paused state: copy the `rgl_log` and take a full dump (Task Manager, Create dump
  file) before clicking. After Ignore, the logged offset is a secondary site.

Disassemble around the offset: shipping builds keep their assert strings, so functions often name
themselves. A read at a small offset from null means a missing data surface, such as the usage a flag
needs. A hash-map walk ending in a read means a table missing a key: make it total (extra rows are
inert); the key often survives in a register in the dump. TAOM's optional
[native_crash_triage.py](https://github.com/haterade22/TAOM/blob/bannerlord-1.5.x/tools/native_crash_triage.py)
names the function and its strings from an offset or a minidump (pass `--dll`; the default path is
one machine's); any disassembler does the same by hand.

### Known signatures

| Signature | Cause | Evidence |
|---|---|---|
| Access violation at `+0x6590B9` on the first swing | [No melee-table row](/guides/custom_creature_melee/#the-first-swing-crash) | seen in game, v1.5.3 |
| Access violation at `+0x57070C` a second into deployment | [No face morph channels](/guides/custom_creature_race/#face-and-hand-morph-channels) | seen in game, v1.5.3 |
| Access violation at `+0x10` in every mount context | [No `quad_movement`](/guides/custom_creature_troubleshooting/#crash-in-every-mount-context-at-once) | seen in game, v1.4.x |
| Null read at `+0x18`, `+8` or `+0x2C` while a clip plays | [A flag without its usage](/guides/custom_creature_clip_inspector/#clip-usages) | engine code, not reproduced |

### Method

* **Fight a control creature of the same shape first,** a single-creature mount such as a warg.
* **When a rework breaks a working creature, restore its whole backup first:** one file copy can save
  a day.
* **Check the failure signal was absent before your change,** and test in game early: one launch beats
  a long analysis.
````

## F6. Troubleshooting: what to search for in the log

**Target:** `/guides/custom_creature_troubleshooting/` > after the F5 section, before
`#two-debugging-rules-worth-more-than-any-single-entry-above`
**Action:** insert the section (anchor `#what-to-search-for-in-the-log`)
**Why:** each of these v1.5.3 strings points at one creature data mistake (MAP section 10.3).

````markdown
## What to search for in the log

The engine log is `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`; read the newest.

| Log text | Meaning |
|---|---|
| `Loading packages` | Which asset trees loaded |
| `Could not find animation:` | A set names an unregistered clip; the slot kept its old value |
| `Trying to use undefined action` | An action missing from `action_types.xml` |
| `could not be found, using default action set!` | A missing `base_set` |
| `Skeleton model could not be found` | A wrong `skeleton=` |
| `does not contain` | A request for an unbound action |
| `Combat parameter not found:` | A clip with no collision window |
| `Sound not found:` | A missing sound code |
| `Could not find face animation record with name:` | A missing facial animation id |
| `Clip usage data couldn't assigned` | A third clip usage, dropped |
| `Unable to register animation clip` | A duplicate clip name |
| `Please set hit_bone_index` | No usable hit bone |
| `get_monster_usage_set_index failed` | A `monster_usage` with no usage set |
| `not found for combat animation blending!` (Kit) | A missing `_balanced` twin |
````

## F7. Troubleshooting: the zero-size clip's fix

**Target:** `/guides/custom_creature_troubleshooting/` > `#a-clip-reports-zero-size-and-will-not-save`, the
**Fix.** paragraph
**Action:** replace the paragraph
**Why:** "rename on disk" reads as renaming the file, which leaves the old clip name registered, and the
restart clears the Kit's state without making the renamed clip usable. Same sources as C7 (MAP sections 1
and 6; the docstring of `rename_anim_clip_tpac.py`).

````markdown
**Fix.** Restart the tools to clear the Kit's state. That recovers the Kit, not the clip: no clip
renamed inside the Kit is known to be usable afterwards. Rename with the Kit closed, and rename the
clip **item** stored inside its `_anm.tpac`, not only the file name, because the game registers a clip
by that stored name. See [Clip names](/guides/custom_creature_clip_inspector/#clip-names) and
[Compiling in the Kit](/guides/custom_creature_animation/#compiling-in-the-kit).
````

## Notes for the maintainer (not for pasting)

* **Eleven files arrive together.** Ten pages go in `guides/`:
    * four new ones, each needing a nav entry: `guides/custom_creature_race.md` (nav label
      **Humanoid Race**), `guides/custom_creature_clip_inspector.md` (**Clip Inspector**),
      `guides/custom_creature_melee.md` (**Melee Attacks**) and `guides/custom_creature_battle.md`
      (**In Battle**);
    * the six refreshed pages, which replace the live files of the same name.

  The eleventh is this pack, which is not a page.
* **Everything must go live together.** Without the six updates, whether as the refreshed files or as
  this pack's sections, a reader of the new pages meets two answers to one question, and dead links:
    * `body_length`: the live XML page says it scales the rider; the battle page says it does not (D4).
    * Source 1: the live animation page says 0; the clip inspector and race pages say 1 (C3).
    * `cyclic` and `synch_with_movement`: the live reference page calls them required on locomotion;
      the clip inspector counts them on 0 of 435 vanilla human gait clips (E2).
    * `disable_alternative_randomization`: a clip flag on the live reference page, a request-only flag
      on the clip inspector (E2).
    * Six links in the new pages resolve only after the updates: the race page's links to
      `#the-export-mapping`, `#debugging-a-native-crash` and
      `#check-materials-after-every-fbx-re-import`, the melee page's to `#debugging-a-native-crash`
      and `#what-to-search-for-in-the-log`, and the clip inspector's to
      `#what-to-search-for-in-the-log`.

  Without the new pages, the refreshed pages and most sections of this pack link to pages that do not
  exist. One conflict sits inside the pack: the spider skeleton's Usage is `other` in the live skeleton
  and reference tables and `horse` in B4 and E7 (a raw `other` import launch-crashed), so when pasting,
  paste B4 and E7 together. The refreshed pages carry both.
* **Two anchors change.** C4 replaces `#an-honest-status-note` with `#the-export-mapping`; nothing links
  the old one. B3 renames `#materials-do-not-survive-an-fbx-re-import` to
  `#check-materials-after-every-fbx-re-import`: F2 and the race page's "materials" link (in
  `## The skeleton: human names, human order, human axes`) already use the new one, and none of the six
  live pages links the old one. When pasting, paste B3 with the race page, or that link lands on the page
  top.
* **The Custom Mount page:** the text and list under `!!! quote "Artem:"` on `/guides/custom_mount/` are
  not indented, so they render outside the box. Indenting them 4 spaces puts them back inside.
* **How it is sent:** all eleven files together, to the maintainer on Discord, as the six pages were, not
  as a pull request against the GitHub mirror, which lags the live site by a daily push.

## Screenshots wanted: an offer (not for pasting)

Not part of this update. The series has no images yet; if you want them, TAOM can capture these in the
Kit or Blender. Ideas from the newcomer review, by page:

* **Humanoid race:**
    * the Kit's Skeleton Inspector showing the skeleton's `Usage` and its body and joint lists;
    * Blender, side by side: an artist rig with its bones along +Y and the same rig re-framed to the
      human's axes;
    * the Kit's mesh view with the head's three sub-meshes (`<mesh>`, `.eye`, `.mouth`) and their tags;
    * Blender's shape key panel with the added channels at value 0.
* **Melee attack clips:**
    * the clip inspector with the Blends with animation and Blends with action boxes highlighted;
    * the Compute Reach button with its Skeleton combo;
    * a diagram of one melee table row: weapon balance mapped to slots 0 to 9.
* **Clip inspector:** the whole inspector, annotated in its four groups (range and timing,
  arbitration, events, links and combat), with Clip usages expanded.
* **Big creatures in battle:**
    * the Kit's default rod capsules beside fitted hit capsules on one skeleton;
    * the deployment screen before and after formation spacing;
    * a top-down diagram of the troll smash: the impact 1.8 m ahead, the 1.5 m full-damage ring, the
      3.5 m outer ring, and the nearest N enemies hit.
