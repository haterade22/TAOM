# Bannerlord animation system map: from the Modding Kit to an agent's pose

> **Engine and date:** Bannerlord v1.5.3 (`.claude/pinned-game-version.txt`), read on 2026-09-26.
>
> **Binaries:** the client `bin/Win64_Shipping_Client/TaleWorlds.Native.dll` (14,209,376 bytes, dated 2026-09-15,
> SHA-256 prefix `45be32c57c451f78`) and the Modding Kit's `bin/Win64_Shipping_wEditor/TaleWorlds.Native.dll`
> (26,318,688 bytes, dated 2026-09-16, SHA-256 prefix `85c4d16945700701`), both disassembled offline. Managed code:
> the v1.5.3 decompile cache `C:/Users/mikew/.taom-src/v1.5.3/`, `E:/Decompiled_Bannerlord/_shipping_build/` and an
> ilspycmd 10.0.1 decompile of the installed DLLs.
>
> **RVAs** are offsets into those two DLLs. "Kit" marks the editor DLL; an unmarked RVA is the client. Every engine
> bump moves them, so re-derive one with the scripts in section 13 before relying on it.
>
> **Tags:** [Certain] means read or run on 2026-09-26; [Likely] is a strong inference; UNVERIFIED means nobody has
> established it. Four evidence reports feed this page, cited by name: **R-Kit** (the inspector and Save, in the Kit
> DLL), **R-Fields** (how the client reads every metadata field), **R-Flags** (flags and clip usages at runtime) and
> **R-Life** (packages, XML and the lifecycle). Where two reports disagree, both are cited.

**Audience:** TAOM developers and AI agents authoring clips, races and action sets. This page maps the whole path
once so nobody re-derives it. It links the owning docs instead of restating them:

| Question | Owner |
|---|---|
| Which flags and priority a clip type needs (the recipe) | [bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md) |
| Authoring a rig against the engine skeleton | [bannerlord-skeleton-authoring.md](bannerlord-skeleton-authoring.md) |
| UE and Blender export, the clip stage, the bind stage | [ue-to-bannerlord-asset-pipeline.md](ue-to-bannerlord-asset-pipeline.md) |
| Creature mounts and the monster usage tables | [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) |
| Managed binding and `Agent.SetActionChannel` | [engine/animation-binding-and-playback.md](engine/animation-binding-and-playback.md) |
| The hill troll: history, the swing CTD, the face and hand morphs | [troll-race.md](../features/troll-race.md) |
| Asset trees, LODs, materials on a re-import | [armory-guide.md](armory-guide.md) |
| Lessons, one per incident | [lessons/animation-skeleton.md](../reviews/lessons/animation-skeleton.md) |

## 1. Summary and flow

A Bannerlord clip is a metadata record, not keyframes. The Modding Kit imports each FBX take into a
SkeletalAnimation "master" that holds the keyframes (`<take>_geo.tpac`). An AnimationClip item (`<clip>_anm.tpac`)
names that master by GUID and adds everything else: the frame range, duration, flags, priority, sounds, blend
partners, combat parameter and up to two typed "clip usages". The inspector edits a local copy. Only Save writes the
record, checksums it, cooks the RuntimeDataCache entry and runs the blend-child generator. At launch the client reads
each record, builds a runtime clip object (resolving names to indices and packing priority and weight into the flag
word), registers it by name, and files release and blocked swings into a melee attack table. The action XML then
binds action codes to clips per action set, a Monster names its set, and `Agent.SetActionChannel` asks the agent's
anim system to play an action on channel 0 or 1. There, priority, alternatives, the melee table and blend partners
decide which clip plays, and the per-tick update drives progress, step events, blend-out and the continue chain.

```
AssetSources/<path>/<take>.fbx
   | Kit FBX import (Kit 0xAA2FC0): each take sampled frame by frame
   v
Assets/<path>/<take>_geo.tpac          SkeletalAnimation master: keyframes (never gets an RDC entry)
   | "New animation clip" (Kit 0xB15A70), then Animation source set in the inspector
   v
Assets/<path>/<clip>_anm.tpac          AnimationClip item: metadata only, 0 segments
   | inspector edits a local copy; Save (Kit 0xB9704E) -> apply_patch (Kit 0x5B170):
   |   metadata v6 written, item checksum xxh64, RDC cooked,
   |   signal_package_item_change -> blend-child generator (Kit 0xB88540)
   v
<module>/RuntimeDataCache/<PACKAGE-GUID>.rdc   (stamp at 0x68 = the item checksum)
   | game launch: package loader (0x73960)
   v
metadata reader (0x58C500) -> Animation_clip_item slot 10 (0x58C3C0)
   |   runtime clip built (0x591C30) and registered by name (0x58F4C0)
   |   melee attack table insert (slot 5, 0x58BEA0 -> 0x5682B0, table 0xDB0360)
   v
action_types.xml -> combat_parameters.xml -> action_sets.xml
   |   managed merge (MBObjectManager) -> native parse (0x58EC00, 0x58FFF0 per set)
   v
Monster action_set + monster_usage -> Agent.SetActionSet
   | Agent.SetActionChannel -> 0x6E1B40 -> 0x5EDDB0 -> anim system virtual slot 48
   v
0x655480: alternatives, melee table, blend in, blend partner, priority gate (0x658E80)
   | every tick 0x655F90: progress, look slope, particles, step events (0x5F8FD0),
   |   finish at 1 - blendOut/duration -> continue to action, or cyclic, keep, act_none
   v
rgl layer word, IK, face and hand morphs (Human_anim_system only) -> the pose on screen
```

The load order in the diagram is the Kit's, from its log `rgl_log_11008`: packages, then `action_types.xml`
(Native, then the Armory), then combat parameters, then `action_sets.xml` (Native, then the Armory) [Certain]. That
the game client loads in the same order is [Likely] (R-Life).

## 2. The Animation clip inspector, field by field

**How the inspector behaves** [Certain unless tagged] (R-Kit):
- Class `AnimationClipInspector` (Kit vtable 0x14392C8, constructor 0xB987F0). It clones each selected clip's
  metadata (a 0x1B8-byte object; clone Kit 0xB89C40) and every widget writes only to that clone, through one
  dispatcher (Kit 0xB96030). **Nothing reaches disk until Save.** Whether closing the inspector without Save
  discards the edits is UNVERIFIED.
- Multi-selection works. A spin box shows `----` when the selected clips differ, and an unchanged `----` keeps each
  clip's own value. A combo gets a `----` item that does the same. Checkboxes go tri-state, and a partial box keeps
  each clip's own bit. Step points and every usage add or delete need a single selection.
- Text fields commit on Enter or focus loss, not per keystroke [Likely].
- The warning label ("No skeleton animation is assigned", "Assigned skeleton animation not found") fills only for a
  single selection.

**File order** is the position in the metadata writer (Kit 0xB8C890). W0 is `int 6`, the metadata version. The client
reader (0x58C500) reads the same order. At version 6 and later it reads Loading Type as a byte; versions 2 to 5 map
an old bool to 2 or 0; older versions give 0 [Certain] (R-Kit). **Runtime** offsets are in the runtime clip object
that 0x591C30 builds (vector 0xDB00C8, indexed through the remap array 0xDB00A8) [Certain] (R-Fields).

| Label | Editor field (offset, type) | File order | Game consumer | What it does | TAOM notes and gotchas |
|---|---|---|---|---|---|
| **Animation source** | `+0x24`, 16-byte GUID (`skeleton_anim`) | W8 | Not copied to the runtime clip. The keyframe loaders 0x5919D0, 0x591790 and 0x591590 read it, and 0x5927B0 keys the keyframe cache on source GUID plus start frame [Certain] (R-Fields) | Names the SkeletalAnimation master whose frames this clip cuts | The setter looks the name up among "Skeleton animation" items and writes only the GUID: Duration and the sources are not filled in [Certain] (R-Kit). Empty gives the zero GUID. Save updates the package dependency when it changes [Likely] |
| **Duration** | `+0x08`, f32 (`duration`) | W1 | Runtime `+0x188`. Read by `GetAnimationDuration` (0x6EA020), `GetActionAnimationDuration` (0x6EA3A0), the finish test 0x653A40, the blend in 0x655480, death and fall timing (0x5EC470, 0x5EADF0) and combat timing (0x6825C0, 0x6754E0) [Certain] | Playback length in seconds; progress = time / duration | Kit range 0..1000, step 0.001. With Loading Type "Load when needed", Save warns when Duration is 3.0 s or less [Certain] (R-Kit) |
| **Source 1**, **Source 2** | `+0x0C`, `+0x10`, f32 (`source_1`, `source_2`) | W2, W3 | Runtime `+0x18C`, `+0x190`, truncated to int frame numbers (0x473153, 0x47315E); the keyframe cache key; pose sampling 0x5F8090; step events 0x5F8FD0 [Certain] | First and last frame cut from the master | Kit 0..65000, step 1. Source 2 below Source 1 plays backward (vanilla `blocked_slashright_2h` plays its master 110 to 1, troll-race.md); Save warns "Reverse animations does not support Load when needed" [Certain] (R-Kit). The keyframe cache is shared between clips with the same key [Likely] |
| **Sample Rate** (read-only) | none | none | none | Shows the absolute difference of Source 2 and Source 1 over Duration; `----` for several clips or Duration 0 or less | A quick check that the frame range and Duration agree |
| **Size in KB** (read-only) | none | none | none | Sum of the selection's cooked sizes (10 dwords, Kit 0x842900) over 1024 | |
| **Param 1** | `+0x14`, f32 (`param_1`) | W4 | Runtime `+0x1D8`; `GetAnimationParameter1` (0x6EA100); see "Param 1, 2 and 3 by action kind" | Depends on the action kind: melee reach in metres, look-slope blend at level look, release progress, death timing | Kit 0..10000, step 0.01. Compute Reach writes it |
| **Auto-Compute Param 1** (group: Skeleton, Spine Lower, Spine Upper, Thorax, Main Hand Item, Compute Reach) | none (UI only) | none | none | Measures the maximum reach and writes it to Param 1 on every selected clip (see "Compute Reach") | Defaults `human_skeleton`, `biped_spine_1`, `biped_spine_2`, `biped_thorax`, `biped_item_r`. For a race on its own skeleton, pick that skeleton model, since the bone indices come from the model named in the combo [Likely] |
| **Param 2** | `+0x18`, f32 (`param_2`) | W5 | Runtime `+0x1DC`; `GetAnimationParameter2` (0x6EA1E0) | Reload-phase completion progress on `actt_reload` clips [Certain]; a timer in seconds on fall reactions (purpose UNVERIFIED) | Meaning elsewhere UNVERIFIED |
| **Param 3** | `+0x1C`, f32 (`param_3`) | W6 | Runtime `+0x1E0`; `GetAnimationParameter3` (0x6EA2C0); the death path 0x5EC470 | Only consumer found: death timing (the earlier of the p1 and p3 points, with an agent flag) [Certain for the math] | Meaning UNVERIFIED |
| **Priority** | `+0x20`, i32 shown as a float; the setter truncates | W7 | Low byte ORed into the runtime flag word `+0x1D0` (0x591CAB) [Certain] (R-Fields, R-Flags); the priority gate 0x658E80 | Channel arbitration: a request below the current priority is rejected, a tie wins (section 8) | Kit 0..100. TaleWorlds' own metadata dump labels this field "param_3", a copy-paste bug [Certain] (R-Kit). A request with a nonzero priority byte replaces it. Managed bands (`AnimFlags.cs:10-32`): attack 10, defend 14, parry, blocked and throw 15, kick 33, reload 60, mount 64, equip 70, striked 80, die 95 [Certain]. A clip left at the Kit default 0 plays in the viewer and loses in battle (lessons, "A mount's clip needs a priority") |
| **Randomization weight** | `+0x40`, i32 (`randomization_weight`; TpacTool's `UnknownInt`) | W21 | Bits 60..63 of `+0x1D0` (0x591CB9, `shl 0x3c`) [Certain] (R-Fields, R-Flags) | Weight in the action's `alternative_group` pick (0x655310) | Kit 0..15; a larger value would wrap mod 16. A group whose weights sum to 0 plays the requested action. R-Kit left the packing UNVERIFIED; both client-side reports read it at 0x591CB9. Vanilla: all 526 clips in the 111 groups have weight 1 or more [Certain] |
| **Step points X Y Z W** | `+0x44`, 4 x f32 (`step_points`) | W9 | Runtime `+0x6C..+0x78`: each value v of 0 or more becomes min(v, 0.99), a negative one becomes -1.0 [Certain]; step events 0x5F8FD0; footsteps 0x6D9C00 | Progress points for events: the clip sound at index 0 or 1, the voice at a step with no sound or index 2 or more, the bodyfall sound with `make_bodyfall_sound`, footsteps with `make_walk_sound` | Single selection only, range -1..1. `use_last_step_point_as_data` silences index 3. The progress comparator itself is UNVERIFIED. Step points are not only sound triggers (section 11) |
| **Sound code** | `+0x58`, string (`sound_code`) | W10 | Runtime `+0x7C` (int16) through the sound manager. A miss logs "Sound not found: %s. It is used for animation: %s" and stores -1 [Certain] | FMOD event played at step 0 or 1 | 1,547 vanilla clips use 133 events |
| **Voice code** | `+0x78`, string | W11 | Runtime `+0x1F4` (int16) from the voice-type table (0x637BB0); unknown gives -1 [Certain] | Voice type from `voice_definitions.xml` `<voice_type>` | 134 vanilla clips, 15 types |
| **Facial animation id** | `+0x98`, string (`facial_anim_id`) | W12 | Runtime `+0x1B0` (copy) and `+0x1F8` (index, 0x594AE0). A miss logs "Could not find face animation record with name: %s" [Certain]. Applied only by `Human_anim_system` (0x63EFA0), looping when the clip is `cyclic` | Plays a face animation record | Ids are `face_animation_record` entries in `Native/ModuleData/voices.xml` (246). Which monsters run `Human_anim_system` is UNVERIFIED |
| **Blends with action** | `+0xB8`, string | W13 | Runtime `+0x1EC` (action code). Empty or `act_none` gives -1, and so does an unknown name, silently [Certain]. Read by 0x655480, 0x655F90, 0x678310, 0x6820B0 | Runtime two-layer blend with the partner action's clip at a factor (section 8) | Required by `blends_according_to_look_slope`: the load validator strips the flag without it. All 175 vanilla self-keyed swings leave it empty |
| **Blends with animation** | `+0xD8`, string (TpacTool's `UnknownClipName`) | W22 | Not copied to the runtime clip. Read only by the melee-table insert 0x58BEA0 [Certain for 0x591C30; Likely that nothing else reads it] | The melee-table key: its own name self-keys the clip; a twin's name makes the Kit generate 10 blend children on Save (section 3) | TaleWorlds' metadata dump prints this field as "blends_with_action", a copy-paste bug [Certain] (R-Kit). Empty on a release or blocked clip means the swing CTD |
| **Continue to action** | `+0xF8`, string | W14 | Runtime `+0x1F0`. The tick starts it at blend-out start with flags 0x2000 (0x655F90); also 0x5EC0C0, 0x5EC470, 0x6FB520 and 0x66C6C0 [Certain] | Chains the next action | An unknown name becomes -1 silently. An action the agent's set lacks is a [Likely] crash (section 9). The validator strips `keep` and `cyclic` from a clip that has one |
| **Left hand pose**, **Right hand pose** | `+0x138`, `+0x13C`, i32 (`left_hand_pose`) | W15, W16 | Packed into runtime byte `+0x1A8`: high nibble left + 1, low nibble right + 1 (0x591E4D..0x591E76) [Certain for the packing; the left and right order is Certain in R-Fields from the Kit labels and Likely in R-Flags]. Applied by `Human_anim_system` (0x63EFA0 -> 0x4728D0 -> 0xA09A0 -> 0x6CCE10) | Selects hand morph key 5L + R + 1 of 25 (see "Hand poses") | The Kit accepts -1000..1000 without validation; valid values are -1..4 [Likely]. 5 or more reads past the 25-entry vector [Likely], UNVERIFIED in game. The race's LOD0 hand mesh needs the 26 hand-pose channels (troll-race.md "Hand pose morphs") |
| **Combat parameter id** | `+0x118`, string | W17 | Runtime `+0x7E` (int16) index into the parsed `combat_parameters.xml` table (0xDAC618, stride 0x68); the entry's `weapon_offset` is copied to runtime `+0x00`. A miss logs "Combat parameter not found:" and stores -1 [Certain] | Collision window, hit bone, look and rider limits (see "Combat parameter") | A swing with no valid id has no collision window [Certain from the code; not reproduced in game] |
| **Blend in period** | `+0x140`, f32 | W18 | Runtime `+0x1E4`. Used when the request's blend-in is within 0.001 of -0.2, the managed default (0x65573D..0x65575F), or negative in `SetAnimationAtChannel` (0x6FB290); `GetAnimationBlendInPeriod` (0x6EA440) [Certain] | Cross-fade into the clip | Kit 0..100, step 0.01 |
| **Blend out period** | `+0x144`, f32 | W19 | Runtime `+0x1E8`. Used when the request's blend-out is negative. Blend-out starts at progress 1 - blendOut / duration (0x653A40), which is also when Continue to action fires [Certain] | Cross-fade out | The validator sets it to 0 on a `keep` or `cyclic` clip |
| **Do not interpolate** | `+0x148`, u8 | W20 | Not copied. No client consumer in 0x460000..0x600000 [Likely]; elsewhere UNVERIFIED | UNVERIFIED | 0 on all 6,177 vanilla clips |
| **Loading Type** | `+0x1B1`, u8 (`loading_type`; byte 0 of TpacTool's `UnknownUInt2`) | W26 | The keyframe loaders: 2 passes a null keyframe set to 0x592520 (no keyframes), 1 uses the async loader and a 3-second cache key, 0 is the default path [Certain] (R-Fields) | 0 Always keep in memory, 1 Load when needed, 2 Never load [Certain] (R-Kit, from the combo table and the enum names; R-Fields tags the combo-to-value order Likely) | Vanilla: 5,040 at 0, 563 at 1, 574 at 2 [Certain]. This byte is the one the clip-flags doc reads as a segment selector: see section 3 and section 11 |
| **Do not optimize** | `+0x1B2`, u8 (`do_not_optimize_`; byte 1 of `UnknownUInt2`) | W27 | Not copied; no consumer found [Likely editor or cook only], UNVERIFIED | UNVERIFIED | 17 vanilla clips (inventory and naval NPC clips) |
| **Flags** (43 checkboxes) | `+0x38`, u64 | W28 (`int 0`, skipped by the client), W29 (count and names) | Runtime `+0x1D0`, with the priority byte and the weight nibble folded in | Section 4 | Saved as names; a bit with no name is lost |
| **Clip usages** | `+0x150`, vector of records | W30 | Runtime `+0x198`, `+0x1A0`: at most two | Section 5 | A third is dropped at load |
| (no control) `parent_animation_1_`, `parent_animation_2_` | `+0x170`, `+0x190`, strings (TpacTool's `ClipSource1Name`, `ClipSource2Name` [Likely]) | W23, W24 | The melee-table insert 0x58BEA0 (a child fills slot `child_index` of parent 1's row); combat parameter blending in 0x591C30 [Certain] | Marks a Kit-generated blend child | No inspector function reads or writes them; the clone preserves them. Never set them to a vanilla clip's name (section 3) |
| (no control) `child_index_` | `+0x1B0`, signed byte (TpacTool's `GeneratedIndex`) | W25 | Melee-table slot; -1 selects the non-generated keyframe loader path (0x5919D0, 0x591790) [Certain] | 0..9 on a generated child, -1 otherwise | A self-keyed row stores 0xFF |
| **Save** | none | none | none | Section 6, "What Save does" | |

### TpacTool names

TAOM's tpac tools use TpacTool.Lib 0.4.0's field names. The map, from R-Kit and R-Fields:

| TpacTool name | Kit label | Offset | Tag |
|---|---|---|---|
| `UnknownClipName` | Blends with animation | `+0xD8` | [Likely] (R-Kit) |
| `ClipSource1Name`, `ClipSource2Name` | none (`parent_animation_1_`, `parent_animation_2_`) | `+0x170`, `+0x190` | [Likely] (R-Kit) |
| `GeneratedIndex` | none (`child_index_`) | `+0x1B0` | [Likely] (R-Kit); R-Fields confirms the old `engine_meta.py` label |
| `UnknownInt` | Randomization weight | `+0x40` | [Certain] (R-Fields) |
| `UnknownUInt2` byte 0, byte 1 | Loading Type, Do not optimize | `+0x1B1`, `+0x1B2` | [Certain] (R-Fields); the rest of `UnknownUInt2` and `UnknownUShort` cover W28's `int 0` [Likely] |

### Param 1, 2 and 3 by action kind

Counts join every vanilla clip's fields with the vanilla `action_sets.xml` and `action_types.xml` [Certain for the
counts] (R-Fields).

| Kind | Values | Code evidence | Meaning |
|---|---|---|---|
| `actt_release_melee` Param 1 (313 of 339 set) | 0.58..1.2 (fists about 0.58 to 0.92, thrusts about 1.15, lance about 1.18) | 0x5E5740 takes the max and average of p1 over a weapon usage's release actions; 0x5D9460 adds weapon length terms and caches the result at agent `+0x9B0`/`+0x9B4`, read by 14 functions in 0x68xxxx..0x6Bxxxx; 0x6AA730 scores `1 - (min(p1/dist, 1) - 1)^2` | **Attack reach in metres** [Certain for the code and the Kit's "Compute Reach" label]. That the reading cluster is the AI is [Likely] |
| Look-slope clips Param 1 (123 of the 124 `defend_shield` clips with p1 set) | 0.26..0.6 | 0x655F90 remap | **Blend factor at level look** for the up and down pair [Certain] |
| Release and launch clips Param 1 | throwing 0.31..0.45 | Managed siege engine: `MissileLaunchDuration = FireDuration x p1`; Naval hook: `progress > p1("usage_hook_release")`; 0x5F8090 samples the pose at frame `src1 + (src2 - src1) x p1` | **Release progress** [Certain for the managed uses; Likely for the native throwing use] |
| `actt_reload` Param 2 (12 of 28) | 0.5..0.85 | Crosshair: a phase is done when progress plus the blend-out remainder passes p2; phase length p2 x duration; chained by Continue to action | **Reload-phase completion progress** [Certain]. Reload Param 1 (26 of 28, 0.1..1.1) is UNVERIFIED |
| Death, 0x5EC470 | fall p1 201 of 314, p3 162 of 314 | The time along the continue chain to the p1 point goes to agent `+0xBE8` (below 0.01 s becomes 0.5 s); 0x5FA680 fires a mission callback once the time since death passes it | **Hand-off time after death** [Certain for the math]; ragdoll or body-down is UNVERIFIED |
| Hit and fall reaction, 0x5EADF0 | fall p2 35 of 314 (0.2..1.4) | Timer on the mount agent = p2 seconds when set, otherwise duration | p2 in seconds [Certain for the math]; purpose UNVERIFIED |
| Blocked and ready melee Param 1 | 0.909 on 26 and 9 clips | No reader tied to these types | UNVERIFIED |

### Compute Reach (Kit 0xB9753E..0xB9841A)

[Certain unless tagged] (R-Kit):
1. Finds the skeleton model named in the Skeleton combo, or logs "Skeleton model could not be found".
2. Maps the four bone combos from `biped_*` bone-type names (28 names, Kit table 0x12F2EE0) to bone indices through
   the model. Errors: "Invalid bone type.", "Unrecognized bone type", "One or more bones, which are required to compute
   reach, are undefined".
3. For each selected clip, samples 30 points across the runtime clip's frame range, composes the chain from the Main
   Hand Item bone to the root (a partial slerp at the spine bones, weights 0.66 and 0.34 [Likely]) and takes the
   distance from the thorax to the item point.
4. Writes the square root of the largest squared distance to Param 1 on every selected clip. Whether the item point
   includes a weapon tip offset is UNVERIFIED.

### Hand poses

The client builds a cache of 25 `rglMorph_anim` objects (0xA09A0); entry i carries one key, morph index i + 1 at
weight 1.0. It decodes L = high nibble - 1 and R = low nibble - 1 (0x4728D0), looks up `5L + R` (0xA0C59), clamps a
negative index to 0 and does **no upper bound check**, then stores the object in visuals slot `+0x1120 + layer x 8`
(layer 2 for channel 0, layer 1 for channel 1) [Certain] (R-Fields). The Kit's resource manager asserts both values
are below 5 (Kit 0xD0890) [Certain] (R-Kit). So each pair in 0..4 selects morph key 1..25, which fits the 26 channels
(a neutral plus 25) that TAOM puts on each race's LOD0 hands [Likely]. The pose names are UNVERIFIED. Vanilla uses
0..4 on both hands and 3 most often (left 4,827, right 4,863) [Certain].

### Combat parameter

- **File** [Certain] (R-Fields): only `Native/ModuleData/combat_parameters.xml`, with 95 `<def>` entries and 165
  `<combat_parameter>` elements in the text, 28 of them inside comments, so **137 are live**. 105 live entries set an
  explicit collision window (128 counting the commented ones). Every live id is used by a vanilla clip, and every
  vanilla clip's id resolves.
- **Live attributes:** `id`, `vertical_rot_limit_multiplier_up`/`_down`, `left_rider_rot_limit`,
  `right_rider_rot_limit`, `left_rider_min_rot_limit`, `right_rider_min_rot_limit`, `rider_look_down_limit`,
  `left_ladder_rot_limit`, `right_ladder_rot_limit`, `collision_check_starting_percent`,
  `collision_check_ending_percent`, `collision_damage_starting_percent`, `hit_bone_index`,
  `shoulder_hit_bone_index`, `weapon_offset`, `collision_radius`, `alternative_attack_cooldown_period`, the three
  `look_slope_*` attributes, and a `<custom_collision_capsule p1 p2 r>` child on the 3 weapon bashes.
- **Parsed struct** (0x5A8A90, stride 0x68) [Certain]: `+0x00` weapon_offset (vec4), `+0x10` capsule pointer, `+0x18`
  id, `+0x20` collision check start (default 1.0, clamped 0..1), `+0x24` damage start (default = check start), `+0x28`
  check end, `+0x2C..+0x34` look slope up, down, speed, `+0x38`/`+0x3C` vertical rot, `+0x40..+0x4C` rider limits,
  `+0x50` rider look down, `+0x54`/`+0x58` ladder, `+0x5C` collision radius (default 0.05), `+0x60` cooldown, `+0x64`
  hit bone and `+0x65` shoulder hit bone (bytes).
- **Consumers** [Certain unless tagged]: the collision window in 0x6ABBC0, 0x6778F0, 0x673700, 0x683D60 and 0x6ACDF0;
  the hit bone in 0x66CEB0, which takes the frame from the skeleton's bone rows, so `hit_bone_index` is a raw bone
  index into the agent's skeleton. A missing entry or negative bone logs "Please set combat parameter with
  hit_bone_index for %s" or "Please set hit_bone_index for %s" (non-fatal [Likely]). Look pitch in 0x6571A0, look
  slope in 0x657470 and 0x657880, ladder in 0x657FA0, rider and ladder limits in 0x691BA0. No native reader was found
  for collision_radius, the capsule, the cooldown or the shoulder bone: UNVERIFIED.
- **Generated children:** see section 3.

## 3. Blends with animation and the melee attack table

**Two different fields.** "Blends with action" (`+0xB8`) names an *action*: at runtime the channel plays this clip
and the partner action's clip together at a factor (section 8). "Blends with animation" (`+0xD8`) names a *clip*: it
is read once, at load, to key the melee attack table, and on Save it drives the Kit's generation of blend children. It
never reaches the runtime clip [Certain] (R-Fields).

**The table** [Certain] (R-Life, R-Fields): a hash table at 0xDB0360 keyed by clip index. A row holds the i32 key at
`+0`, a second i32 index at `+8` [Likely], 10 clip pointers at `+0x10..+0x58` and the next pointer at `+0x60`
(0x5682B0 stores a child at `row + 0x10 + 8 x slot`; 0x659030 reads the same address). At package load, clip item slot 5 (0x58BEA0)
inserts through 0x5682B0, which copies the key name into a 64-byte buffer, so 63 characters are usable.

| The clip's metadata | What 0x58BEA0 does |
|---|---|
| Blends with animation equals its own name | A row keyed by its own index, all 10 slots pointing to itself (`child_index` 0xFF): **self-keyed** |
| `parent_animation_1_` is set | Fills slot `child_index` in the parent's row, creating the row if needed: a **generated child** |
| Neither | No row |

**The balanced twin and the 10 generated children** [Certain] (R-Kit, R-Life, R-Fields). When Blends with animation
names another clip (vanilla `release_overswing_2h` names `release_overswing_2h_balanced`), Save's
`signal_package_item_change` (Kit 0x67C60) calls item slot 25, the generator (Kit 0xB88540). It builds and applies its
own patch with 10 children, each with both parents set and `child_index_` 0..9. All 1,070 vanilla children are named
`FNV1a64(src1_src2)_idx`. When the two parents' combat parameters differ, a child's parameter is
`Blended_<a>_<b>_<%.3f>`, every float field lerped at weight 0.05, 0.15, ... 0.95 by child index (0x5AB000, weights at
0xB1BD28); equal ids keep the parent's. A missing twin logs "%s not found for combat animation blending!".

**Self-keyed rows.** Vanilla self-keys 175 swings that have no twin (fists, lances, staff thrusts), and all 175
leave Blends with action empty. Self-keying generates no children [Certain] (R-Life).

**Vanilla census** [Certain] (R-Life): 6,177 clips, of which 175 are self-keyed, 107 twin-keyed and 1,070
generated. Every keyed clip is a release or blocked clip, and the 296 `as_human_warrior` actions bound to a keyed clip
all belong to the four families `act_release_*`, `act_quick_release_*`, `act_blocked_*` and `act_quick_blocked_*`.

**The weapon balance pick** [Certain unless tagged] (R-Life, R-Fields):
1. The agent's action code comes from agent state.
2. 0x58F840 maps (the agent's action set, the action) to a clip index. An unbound action logs "<set> does not contain
   <action>" and returns -1.
3. The callers 0x6825C0 and 0x683290 read a float from the weapon record at `+0x8C`, clamp it to [0.05, 0.95] and
   call 0x659030(0xDB0360, clip, balance). With no weapon the value is 1.0, clamped to 0.95: slot 9. Reading `+0x8C`
   as `WeaponBalance` (`weapon_balance` x 0.01, `WeaponComponentData.cs:353`) is [Likely].
4. 0x659030 picks slot `trunc(min(x, 0.99999988) x 10)` of the clip's row.
5. The chosen clip's own fields are then read (for example `+0x1E8` at 0x682761).

0x659030 is also reached from 0x655480 when the request's `+0x2C` byte is set: the blend factor becomes the balance and
is then zeroed, so no runtime pair blend happens on that path. Managed `SetActionChannel` always writes 0 there. Which
native callers set it (0x5F0900, 0x5F6080, 0x66CEB0, 0x66F060 and others) is UNVERIFIED, and so is a fourth caller of
0x659030, 0x6FE170 (R-Fields, R-Life). Not every release reaches the table: vanilla binds 280 `_balanced` codes and 42
ranged-release codes to clips with no row, and humans do not crash [Certain for the counts; Likely for the
inference] (R-Life).

**The crash** [Certain] (R-Life; [troll-race.md](../features/troll-race.md) "The swing CTD"). A release or blocked
action whose clip has no row, or an action the set does not bind (-1 from 0x58F840, passed on unchecked by 0x6825C0
and 0x683290; 0x655480 does check), makes 0x659030 miss and read the slot at null + 8 + 8 x slot, faulting at
`+0x6590B9` (address 0x8 to 0x50; the troll dumps read 0x8, slot 0). The hill troll hit it with 480 clips that had both
fields empty.

**Making a race swing clip safe:**

| Way | How | Caveats |
|---|---|---|
| Bind the vanilla clip (the hill troll's current data) | `tools/bind_hill_troll_action_set.py` rule 0 (`MELEE_TABLE`) binds the four families to vanilla clips; every other action keeps the troll clip | The troll swings with human motion |
| Self-key the race clip | In the Kit, type the clip's own name (63 characters at most) into Blends with animation, leave Blends with action empty, keep the parent fields empty and the child index -1, and Save. `tools/set_clip_balance_name.py` (untracked on 2026-09-26) makes the same edit offline, rewrites the item checksum and the RDC stamp, and refuses while the Kit or the game runs | Every weapon balance plays the one clip. Check Loading Type first (below). Since 14:12 on 2026-09-26 the troll binder's rule 0 and `wire_hill_troll_race.py --check` read the packages and accept a self-keyed clip (section 9) |
| Author a `_balanced` twin | Author a second clip for the balanced end, name it in Blends with animation and Save; the Kit generates the 10 children | Never name a vanilla clip, and never give a race clip a vanilla parent name: a child fills a slot of the parent's row, which would change every human's attack (troll-race.md) |

**Check Loading Type before binding a self-keyed troll clip.** Two readings of the same byte disagree, and both
say the value matters:
- **R-Kit and R-Fields:** byte 0 of TpacTool's `UnknownUInt2` is Loading Type (`+0x1B1`) [Certain], and 2 is Never
  load: the keyframe loader passes a null keyframe set to 0x592520 [Certain]. The value census fits (vanilla 5,040 at
  0, 563 at 1, 574 at 2; 361 of the 574 are bound base clips such as `quick_release_slashleft_2h`, and 120 are
  generated children).
- **R-Life,** following [bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md) "A clip can carry its
  own motion" (a 1.4.6 handoff measurement): 2 means "play the clip's own segment".
- **Both agree on the data** [Certain] (R-Life; re-read by the checker at 14:24): 48 of the 62 troll release and
  blocked clips are at 2 with 0 segments, including the self-keyed `anim_hill_troll_release_overswing_2h`; the other
  418 troll clips (outside the four families) are all at 0, and so were all 299 troll clips bound before the 14:13
  rebind. What the engine plays for a self-keyed row whose clip is at 2 is UNVERIFIED. Setting Loading Type to Always
  keep in memory (0) before binding is the conservative choice [Likely]; a Custom Battle proves it.

**The troll today** (read by the checker at 14:24 on 2026-09-26; this data was changing during the session)
[Certain]:
- **30 clips are self-keyed:** `anim_hill_troll_release_overswing_2h` and `anim_hill_troll_blocked_overswing_2h` in
  the Kit at 13:21, and 28 more at 13:28 by `set_clip_balance_name.py --apply` (backups
  `*.bak-balancename-20260926-132826` beside the packages and the `.rdc` files). They cover the release, quick
  release, blocked and quick blocked clips for overswing, slash left, slash right and thrust, each with its
  `_left_stance` twin (quick blocked has no thrust clip). No clip has parent names and no children exist. Every
  one of the 480 packages has its item checksum equal to its RDC stamp.
- **32 melee codes bind them:** the Armory's `action_sets.xml` was rebound at 14:13 (backup
  `action_sets.xml.bak-hilltroll-bind-20260926-141305`), and `as_hill_troll_warrior` now binds 32 of its 618 melee
  codes to those 30 clips (both thrust blocked clips also serve the quick blocked thrust codes). The other 586 use
  vanilla clips.
- **24 of the 30 bound clips are at Loading Type 2**, the reading above that says no keyframes load; only the six
  thrust clips are at 0. Whether they play correctly is UNVERIFIED until a Custom Battle.

That the Kit edits were Mike's comes from the `set_clip_balance_name.py` docstring [Likely]; who ran the 13:28 and
14:13 changes is not recorded here.

## 4. Flags

**How they are stored** [Certain] (R-Kit, R-Flags):
- The Kit saves flags as a **list of names**, not a bit word: W28 is an int 0 that the client skips, and W29 is a
  count plus one length-prefixed name per set bit, in the order of the 43-entry name table (Kit 0x1841370). The client
  matches each name against its identical table (0xD11660) and ORs the bit into metadata `+0x38` (0x58C956). An
  unknown name is ignored silently. All 43 names and values match the managed `anf_` values
  (`AnimFlags.cs:33-76`).
- A bit with no name cannot be saved. `anf_disable_alternative_randomization` (bit 31) is in neither table, so no
  clip can carry it; code passes it per request (section 8). The priority byte and the weight nibble are separate
  Kit fields (section 2).
- The runtime word `+0x1D0` = metadata flags OR the priority byte OR (weight << 60) (0x591C30).
- **Effective flags per channel** (0x605F90, 0x658FB0): the clip's word, with the low byte replaced by the request's
  additional low byte when that is nonzero and the top nibble replaced when the additional top nibble is nonzero,
  then ORed with the additional flags. `Agent.GetCurrentAnimationFlag` returns the effective flags,
  `MBActionSet.GetActionAnimationFlags` the clip's raw word, and `GetCurrentActionPriority` the effective low byte.
- **Layer word to rgl:** bits 36..51 are ordinary Kit checkboxes that travel to rgl as layer bits 0..15, computed as
  ((clip OR additional) >> 36) AND 0xFFFF, plus bit 16 (auto-increment progress) unless `synch_with_ladder_movement`
  or `disable_auto_increment_progress` is set (0x6412C0, 0x6FB31A). It is stored at `+0x24` of an rgl blend entry
  (0x48-byte entries at channel `+0x18`, channel = anim system `+0x110 + c x 0x11B0`). Managed calls the range
  `anf_animation_layer_flags_mask`.

**Load-time validator** (0x592930) [Certain] (R-Flags). Each rule logs a line and fixes the clip; vanilla and the
installed Armory and TAOM clips break none.

| # | Condition | Fix |
|---|---|---|
| 1 | `allow_head_movement` with `lock_camera` | removes `allow_head_movement` |
| 2 | `keep` with `cyclic` | removes `keep` |
| 3 | `keep` with a Continue to action | removes `keep` |
| 4 | `cyclic` with a Continue to action | removes `cyclic` |
| 5 | `blends_according_to_look_slope` without a Blends with action | removes the flag |
| 6 | `keep` with a nonzero blend out | sets blend out to 0 |
| 7 | `cyclic` with a nonzero blend out | sets blend out to 0 |

**Kit checkbox order** (the name table order, also the save order): disable_agent_agent_collisions,
ignore_all_collisions, ignore_static_body_collisions, use_last_step_point_as_data, make_bodyfall_sound,
client_prediction, keep, restart, client_owner_prediction, make_walk_sound, disable_hand_ik, stick_item_to_left_hand,
blends_according_to_look_slope, synch_with_horse, use_left_hand_during_attack, lock_camera, lock_movement,
synch_with_movement, enable_left_hand_ik, enable_hand_spring_ik, enable_hand_blend_ik, synch_with_ladder_movement,
do_not_keep_track_of_sound, enforce_lowerbody, enforce_all, cyclic, enforce_root_rotation, allow_head_movement,
disable_foot_ik, affected_by_movement, update_bounding_volume, align_with_ground, ignore_slope, displace_position,
reset_camera_height, ignore_scale_on_root_position, blend_main_item_bone_entitially,
enforce_weapon_tip_with_rope_stretched, enforce_weapon_tip_with_rope_relaxed, disable_auto_increment_progress,
switch_item_between_hands, attach_sound_to_agent, spawn_particle [Certain] (R-Kit).

**Runtime map, by bit.** Vanilla counts come from R-Flags' parser over all 6,177 vanilla clips. Managed file:line
references point at the ilspycmd decompile. For which flags a clip *type* needs, the recipe stays in
[bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md); section 11 lists where that recipe conflicts
with this map.

| Flag | Bit (value) | Subsystem | Runtime effect (RVA) | Set it on (vanilla clips) | Tag |
|---|---|---|---|---|---|
| Priority (Kit field) | 0..7 (`0xFF`) | arbitration | The priority gate (0x658E80); many managed overrides clamp it, for example `Math.Min(GetCurrentActionPriority(c), 73)` | every clip that must win a channel | [Certain] |
| `disable_agent_agent_collisions` | 8 (`0x100`) | collision | No native consumer found | 136 | UNVERIFIED |
| `ignore_all_collisions` | 9 (`0x200`) | collision | Skips a block on both channels (0x5EE3B9, 0x5EE3D1, on the record and replay chain); tested in `compute_animation_displacement` (0x5DD7DB); NavalDLC ORs it in | 537 | tests [Certain], semantics UNVERIFIED |
| `ignore_static_body_collisions` | 10 (`0x400`) | collision | Not found | 18 | UNVERIFIED |
| `use_last_step_point_as_data` | 11 (`0x800`) | sound | Step index 3 or more plays no sound or voice (0x5F907D). The reader of W as data is UNVERIFIED | 62 equip-type clips (W 0.2 or 0.58) | [Certain] |
| `make_bodyfall_sound` | 12 (`0x1000`) | sound | A step event plays the bodyfall sound (0x5FAF40); channel 1 is suppressed when channel 0 also has it (0x5F90A4) | 204 | [Certain] |
| `client_prediction` | 13 (`0x2000`) | network | No replication message is queued when set, only in net or record modes (global 0xDAC684 in {2, 3, 5}) (0x5EE1DB) | 3,428 | code [Certain], mode meaning UNVERIFIED |
| `keep` | 14 (`0x4000`) | lifecycle | Holds the last pose at clip end (0x6563CB); validator rules 2, 3, 6 | clips that end in a held pose (449) | [Certain] |
| `restart` | 15 (`0x8000`) | lifecycle | Re-requesting the playing action restarts it instead of a no-op (0x6555B1); part of the resync mask | 457 | [Certain] |
| `client_owner_prediction` | 16 (`0x10000`) | network | Copied into the replication message (0x5EE1F5..0x5EE207) | 115 | [Certain] |
| `make_walk_sound` | 17 (`0x20000`) | sound | Footsteps when progress crosses the step points of the clip that owns the lower body (0x6D9C00); 0x5F8FD0 returns early for it | clips whose step points should make footsteps (508) | [Certain] |
| `disable_hand_ik` | 18 (`0x40000`) | IK | Not found | 426 | UNVERIFIED |
| `stick_item_to_left_hand` | 19 (`0x80000`) | item | Not found | 0 | UNVERIFIED |
| `blends_according_to_look_slope` | 20 (`0x100000`) | blend | Blend factor with the Blends-with-action partner from look pitch, level look = Param 1 (0x65697B); validator rule 5 | clips with an up and down look partner, such as the shield defends (151, 123 with Param 1 set) | [Certain] |
| `synch_with_horse` | 21 (`0x200000`) | sync | Progress set from the tick's 4th argument (0x65690E); resync mask | 143 | [Certain] |
| `use_left_hand_during_attack` | 22 (`0x400000`) | item | Tested at 0x5F7D88 | 26 | test [Certain], effect UNVERIFIED |
| `lock_camera` | 23 (`0x800000`) | camera | Managed: camera bearing from the pose (`MissionScreen.cs:1070`), first-person look skipped (`:2070`); validator rule 1 | 247 | [Certain] |
| `lock_movement` | 24 (`0x1000000`) | movement | On start the agent's movement target resets to its current value (0x5EDE59, 0x5EDEB3); managed bearing clamp (`MissionScreen.cs:3224`); seven more native tests UNVERIFIED | 721 | tests [Certain], freeze [Likely] |
| `synch_with_movement` | 25 (`0x2000000`) | sync | Progress comes from the movement-phase provider (Human 0x640950, Horse 0x6426E0), the agent's own or another agent's (the mount [Likely]) (0x656B62, 0x656D03, 0x656DC9); resync mask | rider and head-turn overlays (65); 0 of 435 human gait clips | [Certain] code |
| `enable_hand_spring_ik` | 26 (`0x4000000`) | IK | Tested at 0x66B9C9 | 278 | test [Certain], effect UNVERIFIED |
| `enable_hand_blend_ik` | 27 (`0x8000000`) | IK | On start, captures the current hand-bone frames, gated by `enforce_all` or an empty channel 1 (0x5EDEF5); IK mask (0x63F8CD) | 200 | [Certain] tests |
| `synch_with_ladder_movement` | 28 (`0x10000000`) | sync | Action speed forced to 0 (0x655629), progress from ladder position (0x656E87), auto-increment cleared; managed `AgentVictoryLogic.cs:204, :323`, `MissionScreen.cs:3224` | ladder clips (1) | [Certain] |
| `do_not_keep_track_of_sound` | 29 (`0x20000000`) | sound | The sound plays without a tracking handle (0x5F913A) | 19 | [Certain] |
| `reset_camera_height` | 30 (`0x40000000`) | camera | Managed only: camera height 0.5 on channel 0 (`MissionScreen.cs:1258`) | 215 | [Certain] |
| `disable_alternative_randomization` | 31 (`0x80000000`) | alternatives | Not a Kit flag: skips the alternatives pick when passed as `SetActionChannel` additional flags (0x655480) | pass it from code, never on a clip | [Certain] |
| `disable_auto_increment_progress` | 32 (`0x100000000`) | playback | Clears the rgl auto-increment bit (0x6412C0, 0x6FB33F, 0x63DD26) | 37 | [Certain] packing |
| `switch_item_between_hands` | 33 (`0x200000000`) | item | Channel 1: between switch_progress and switch_back_progress the weapon moves to the other hand bone, offset by weapon_displacement (0x6C61EF). **Needs a hand_switch usage** | 0 | [Certain] |
| `attach_sound_to_agent` | 34 (`0x400000000`) | sound | While playing, the sound position follows the agent (0x5F91D0) | 20 | [Likely] |
| `spawn_particle` | 35 (`0x800000000`) | particle | Spawns `particle_name` at `bone_index` when progress crosses `particle_start_progress` (0x65652C). **Needs a particle usage** | 0 | [Certain] |
| `enforce_lowerbody` | 36 (`0x1000000000`), layer 0 | body | Picks which channel owns the footsteps (0x6D9C00, mask 0x3000000000); managed conversation check (`MissionConversationLogic.cs:300`); other sites UNVERIFIED | 376; every vanilla horse clip read, including hit reactions (lessons) | [Certain] tests |
| `enforce_all` | 37 (`0x2000000000`), layer 1 | body | On channel 0: rejects channel-1 requests unless `ignorePriority` (0x655659) and clears channel 1 on start (0x655A0F); passive-usage conditions fail (0x6746A0); channel-0 strike checks (0x5F08BD, 0x6A1F3D); hand-blend-IK gate (0x5EDF1A) | 584 | [Certain] tests |
| `cyclic` | 38 (`0x4000000000`), layer 2 | lifecycle | At clip end, replays the action with the priority byte stripped (0x656366); facial animation loops (0x63F009); validator rules 2, 4, 7; other tests (0x5F9A3D, 0x675333, 0x676954) UNVERIFIED | 1,411; 0 of 435 human gait clips, 48 of 142 quad clips | [Certain] |
| `enforce_root_rotation` | 39 (`0x8000000000`), layer 3 | root | Managed conversation check only; native consumer not found | 2,528 | UNVERIFIED native |
| `allow_head_movement` | 40 (`0x10000000000`), layer 4 | head | Validator rule 1 only | 1,453 | native effect UNVERIFIED |
| `disable_foot_ik` | 41 (`0x20000000000`), layer 5 | IK | `Human_anim_system` skips foot IK, mounted (0x63F1A3) and on foot (0x63F57A) | 634 | [Certain] |
| `affected_by_movement` | 42 (`0x40000000000`), layer 6 | root | Not found | 1,323 | UNVERIFIED |
| `update_bounding_volume` | 43 (`0x80000000000`), layer 7 | render | rgl sets the update-bound byte `+0x1043` (0x49E3A4) | 715 | [Certain] test |
| `align_with_ground` | 44 (`0x100000000000`), layer 8 | root | Human: the ground-alignment weight ramps over the blend usage's start to end (0x6585B0). **Needs a blend usage**; NavalDLC reads and sets it | 171, all with a blend usage | code [Certain], ramp [Likely] |
| `ignore_slope` | 45 (`0x200000000000`), layer 9 | root | Not found | 29 | UNVERIFIED |
| `displace_position` | 46 (`0x400000000000`), layer 10 | root | Moves the agent by the displacement usage's vector, linear up to its end progress (channel 0 per tick 0x655E00; rgl root accumulators 0x49DEB0); managed `AnimationPoint.cs:248-250`. **Needs a displacement usage** | 337, all with a displacement usage | [Certain] |
| `enable_left_hand_ik` | 47 (`0x800000000000`), layer 11 | IK | 0x66BDFD, 0x66BE77; rgl bone indices `+0x50`/`+0x51` (0x49E042) | 1,056 | [Certain] tests |
| `ignore_scale_on_root_position` | 48 (`0x1000000000000`), layer 12 | root | Not found | 125 | UNVERIFIED |
| `blend_main_item_bone_entitially` | 49 (`0x2000000000000`), layer 13 | item | rgl keeps the main-item bone's entitial frame across sampling (0x49DFAC, 0x49DFE7) | 204 | test [Certain], meaning [Likely] |
| `enforce_weapon_tip_with_rope_stretched` | 50 (`0x4000000000000`), layer 14 | item | Not found (the RTTI `Weapon_with_rope_anim_system` exists) | 204 | UNVERIFIED |
| `enforce_weapon_tip_with_rope_relaxed` | 51 (`0x8000000000000`), layer 15 | item | Not found | 13 | UNVERIFIED |
| Randomization weight (Kit field) | 60..63 | alternatives | Weighted pick within the action's `alternative_group` (0x655310) | grouped alternatives | [Certain] |

## 5. Clip usages

**Rules** [Certain unless tagged] (R-Kit, R-Flags):
- Added from the inspector's "Add clip usage" menu and deleted with a confirmation, both for a single selection only.
- Saved as the usage name, an int32 0, then the payload (all 1,197 vanilla records carry the 0; R-Flags' parse
  counted 2,394 because it read both the `AssetPackages` and the `EmAssetPackages` copy of every clip). Vectors are
  saved as x, y, z, 1.0.
- **At most two per clip at runtime** (`+0x198`, `+0x1A0`, each a clone). A third is dropped with "Clip usage data
  couldn't assigned. Limit of number of clip usage datas is %d!" (0x5923A8); the Kit asserts the same limit. The
  vanilla maximum is 2.
- An unknown usage name does not consume its payload, so every later field is misread (0x58CA9D..0x58D014). The Kit
  cannot write one; a hand-built tpac can.
- A new Blend record starts at zero; the defaults of the other types are UNVERIFIED. The ranges of the vec3 widgets
  are UNVERIFIED.
- Every record derives from `rglAnimation_clip_data`; the Kit and the client name the classes
  `rglAnimation_clip_<type>_data`.

| Kit menu entry | Saved name, type id | Fields (offset, Kit range) | Runtime consumers | Null-checked? | Needed by | Vanilla (installed Armory) |
|---|---|---|---|---|---|---|
| Displacement | `displacement`, 0 | `+8` displacement vector (vec3); `+0x18` end progress (-100..100) | Getter 0x47DDF0. At clip end the tick applies it (0x6560D6, checked). `GetAnimationDisplacementAtProgress` (0x6EA655, checked): the full vector once progress reaches the end progress, otherwise vector x progress / end. Per tick 0x655E00 and rgl 0x49DEF6 (not checked, read `+0x18`). `GetDisplacementVector` (0x6E9E57, not checked, `+8`; vanilla calls it only after testing `displace_position`) | mixed | `displace_position` | 337 of 337 flagged clips (Armory 273 of 273) |
| Quadrupedal movement | `quad_movement`, 1 | `+8` loop displacement; `+0xC`, `+0x10` pace switch limit min, max (-100..100) | 0x747840, 0x74C820: the horse movement node slot 1 (0x74C950, `+8`), 0x747AA0, 0x747BB0, 0x695490 (`+0x10`), 0x6808A0 (`+0x10`, preload chain), and `Agent.WalkingSpeedLimitOfMountable` -> 0x6E4C80 (`+0x10` of movement clip 0) | **no** (10 sites) | clips bound in horse movement tables | 142 (42 with no step points) |
| Bipedal movement and ik | `bip_mov_ik`, 2 | `+8` loop displacement; `+0xC`/`+0x10` snapping duration 0/1; `+0x14`/`+0x18` snapping start 0/1; `+0x1C`/`+0x20` adjusting start 0/1 (all -100..100). Saved as loop, adjusting 0/1, snapping duration 0/1, snapping start 0/1 | 0x7477D0; 36 sites in `Human_anim_system` and the AgentVisuals tick 0x745A80 (loop displacement over duration) | **no** | human locomotion tables | 435 (0 cyclic, 0 `synch_with_movement`) |
| Blend | `blend`, 3 | `+8` blend start progress; `+0xC` blend end progress (-100..100) | Inline in 0x6585B0: the ground-alignment weight ramp | **no** (reads `+8` at 0x65868D) | `align_with_ground` (Human) | 171 of 171 (Armory 95 of 95) |
| Mount change | `mount_change`, 4 | `+8`, `+0xC` scale blend start, end progress (0..1, step 0.05) | 0x6039F0; 0x63B370 and 0x5DEF20 checked; the stun and death path 0x5EC470 and 0x5F0900 not checked: duration x scale_blend | mixed | mount, dismount and rider-fall clips [Likely] | 66 |
| Hand switch | `hand_switch`, 5 | `+8` switch progress (0..1); `+0xC` switch back progress (0..1); `+0x10` weapon displacement (-2..2) | 0x6CAAA0; 0x6C620D (`+8`) | **no** | `switch_item_between_hands` (channel 1) | 0 |
| Particle | `particle`, 6 | `+8` particle name (string); `+0x28` bone index (s8, -1..64); `+0x2C` start progress (0..1); `+0x30` local position (vec3); `+0x40` local direction (vec3) | Inline in 0x655F90 at 0x65652C | **no** (reads `+0x2C` at 0x65658B and 0x6565AC) | `spawn_particle` | 0 |

The installed Armory and TAOM clips pass every flag-to-usage pairing today (R-Flags' one-off `taomcheck.py`, not a
committed gate) [Certain].

## 6. Packages, RDC and load order

**What the Kit makes from an FBX** [Certain] (R-Life, read with `tools/tpac_skeleton_scan.py --all-types`):

| Input (`AssetSources/<path>/`) | Kit output (`Assets/<path>/`) | Items inside |
|---|---|---|
| Rig and mesh FBX, for example `hill_troll_a.fbx` | `hill_troll_a_geo.tpac` | 5 Metamesh, 1 Skeleton (`troll_skeleton_a`), 1 source-geometry item |
| One-take animation FBX, for example `anim_hill_troll_2h_stand_idle_1.fbx` | `<take>_geo.tpac`, the master | A SkeletalAnimation item with a compressed keyframe segment, plus the source `.fbx` item |
| An AnimationClip on a master | `<clip>_anm.tpac` | One AnimationClip item: metadata only, 0 segments, 0 or 1 dependency entry of 48 bytes (the Animation source GUID, then 32 zero bytes). 5 of the 480 troll clips carry one, including the two self-keyed in the Kit; the 28 self-keyed by script carry none [Certain for four; the fifth, `anim_hill_troll_2h_bash`, from its cooked copy in the Armory's `AssetPackages/pack0.tpac`] |

- The hill troll's `animations/` folder holds 480 `_anm` and 307 `_geo` packages from 307 FBX [Certain]. The Kit
  binary carries the strings `_geo`, `_anm`, `%s_anm`, `_notused`, `.lod`, `AssetSources` [Certain]; that they drive
  the suffixes and paths is [Likely].
- **Import:** the "FBX Import Settings" dialog (Kit 0xF0A980) offers skeletons, skeletal animations and morph
  animations. The importer (Kit 0xAA2FC0) enumerates the FBX takes, reads each take's time span and samples it frame
  by frame into quaternion plus translation, logging "Ignoring bone %s" for skipped bones [Certain for the API calls;
  Likely for the per-take sampling]. How a take name becomes the master's item name is UNVERIFIED. Frame 0 as rest,
  locals stored verbatim, the root yaw and the `_notused` armature:
  [ue-to-bannerlord-asset-pipeline.md](ue-to-bannerlord-asset-pipeline.md) "What the Kit did with the FBX" and
  [bannerlord-skeleton-authoring.md](bannerlord-skeleton-authoring.md). LOD folding and materials:
  [armory-guide.md](armory-guide.md) "LODs in the FBX sources" and "Materials on a re-import".
- **New animation clip** (Kit 0xB15A70; a second path at Kit 0xB17580): the default name is `new_animation_clip`,
  with `_%d` appended until unique; it checks for or creates a `<name>_anm` package [Likely], registers the item and
  saves it through a patch. A new clip has no Animation source [Certain].

**TPAC v2 layout** [Certain] (R-Life, read from `anim_hill_troll_release_overswing_2h_anm.tpac`):

| Offset | Field |
|---|---|
| 0 | `TPAC` |
| 4 | u32 version 2 |
| 8 | package GUID (16 bytes, .NET little-endian; it names the `.rdc` file) |
| 24 | u32 item count |
| 28 | u64 TOC size = file size - 36. The engine takes `data_start = 36 + TOC size`; a zero makes the TOC read as data and trips an rglVec3 assert ([lessons](../reviews/lessons/animation-skeleton.md), "A tpac's header carries the TOC size") |
| 36 | per item: type GUID, item GUID, u32 version, i32 name length and name, i64 metadata length, metadata, 8-byte checksum, i32 segment count x 69 bytes, i32 dependency count x 48 bytes |

**Item checksum** = xxh64 (seed 0) over the i64 metadata length plus the metadata; it matches on 480 of 480 troll
clips [Certain] (R-Life). Any metadata edit outside the Kit must recompute it (`tools/tpac_fix_item_checksums.py`).
TpacTool.Lib writes a zero checksum and those packages still load when they have an RDC entry (lessons, "A hand-built
tpac package is invisible").

**What Save does** [Certain unless tagged] (R-Kit):
1. For each item, the Save branch (Kit 0xB9704E) opens a package patch (Kit 0x4276D0, "There can exist only one patch
   at a time"), clones the edited metadata, updates the patch's dependency records when the Animation source GUID
   changed (that these are the dependency on the master is [Likely]), adds the metadata (Kit 0x4280A0) and calls `rglAsset_manager::apply_patch` (Kit 0x5B170). A failure shows
   "Unable to save animation clip package %1".
2. `apply_patch` validates the patch and runs each item's pre-metadata update. For a clip that slot is Kit 0xB88CE0,
   which can refuse with "runtime data is not valid. Animation clip will not be saved!" [Likely that the call reaches
   it]. It writes the package (8-byte length slots pre-filled with 0xFAFAFAFAFAFAFAFA, back-patched), computes the
   xxh64 checksum (Kit 0x4626D0), writes the package RDC through `rglRuntime_data_cache_manager` (Kit 0x45EB90,
   "Unable to write RDC for package %s : %s"), moves the files into place and calls `signal_package_item_change`.
3. `signal_package_item_change` (Kit 0x67C60) calls item slot 25, the blend-child generator (Kit 0xB88540), which
   applies its own patch (section 3), then notifies dependents and listeners.
4. Per-clip RDC cooking is item slot 33 (Kit 0xB8A080, "Unable to write RDC for animation clip %s."), which also
   raises the Loading Type warnings: Duration 3.0 s or less at Load when needed, a reversed range at Load when needed,
   and "better be Always_keep_in_memory" when the load-on-demand overhead exceeds the clip's size. How Save reaches
   0xB8A080 is UNVERIFIED; the only slot-33 call found is in `create_publish_packages`. A lead: the same error string
   is also referenced from 0xB88CE0, the pre-metadata update in step 2 [Certain for the reference].

**RuntimeDataCache** [Certain unless tagged] (R-Life):
- The file is `<module>/RuntimeDataCache/<PACKAGE-GUID>.rdc`. The item GUID sits at 0x14 and again at 0x24; the data
  offset, raw size and stored size are u64s at 0x44. **The stamp at 0x68 equals the tpac's stored item checksum** on
  480 of 480 troll clips.
- The writer strings exist only in the Kit; the client carries only `RuntimeDataCache` and "RDC cache path is not
  valid". A package without an entry is skipped whole with no log line (lessons, "A hand-built tpac package is
  invisible", #616) [Likely]; the client's skip path is not traced (UNVERIFIED), and neither is what it does when the
  stamp and the checksum differ.
- Masters never get an entry and play anyway (`check_rdc_entries.py` docstring and the same lesson).
- **The Kit watches `.rdc` files.** A path handler (Kit 0x796C0) raises "External .rdc file modification detected.
  RDC files cannot be updated outside the editor. Please restart the game" [Certain for the string and code; Likely
  that it is a file-watch callback]. `set_clip_balance_name.py` refuses to run while the Kit runs, which avoids it.
- **Gate:** `python tools/check_rdc_entries.py [--under <folder>]` checks `_geo` and `_anm` packages, exempts
  masters, and exits 1 on a missing entry or an empty scan.

**Which asset tree loads.** The client's loader loop (0x73960) logs "Loading packages %s...", "Could not find any
asset directory in module folders" and "Loading done..."; the client carries the names `AssetPackages`,
`DsAssetPackages` and `EmAssetPackages` [Certain]. The selection rule is not traced: UNVERIFIED. The Kit session
`rgl_log_11008` loaded `Native/EmAssetPackages`, `SandBox/EmAssetPackages` and `LOTRLOME_Armory/Assets` [Certain]; for
the game client, loose `Assets` wins even where cooked packs exist ([armory-guide.md](armory-guide.md) "Two asset
trees", from logs of 2026-09-01 and 2026-09-26; no game rgl log survives on disk today). Per module on 2026-09-26,
tpacs counted recursively [Certain] (R-Life, recounted by the checker at 14:30):

| Module | Loose `Assets` tpacs | `AssetPackages` tpacs | `EmAssetPackages` tpacs | `RuntimeDataCache` `.rdc` files |
|---|---|---|---|---|
| LOTRLOME_Armory | 5,417 | 10 | 237 (folder dated 2026-09-19) | 4,584 (plus 2,125 `.rtemp` and 28 backups) |
| TAOM | 120 | 5 | 0 | 98 (plus 17 `.rtemp`) |
| TAOM_Map | 1,898 | 5 | 70 in 7 folders | 2,814 (plus 299 `.rtemp`) |
| Native | 0 | 151 | 1,039 in 733 folders | 0 |

**Names** [Certain] (R-Life):
- Registration (0x58F4C0) hashes the clip name with FNV-1a-64 into the map at 0xDB0070 and stores a running index at
  clip `+0x68`. A second clip with the same name is refused: "Unable to register animation clip %s. Another clip with
  same name already exist". Lookup (0x58F6F0) returns -1 on a miss.
- The client copies a clip name into a 64-byte buffer before a table lookup (0x5682B0), so keep names to 63
  characters; the Kit has "Could not set fixed-size(%d) string to: %s". The hill troll's 15 over-long names are
  mapped in `tools/blender/hill_troll_clip_renames.json`.
- Never rename a clip in the Kit: the orientation trap "Kit clip rename" and `tools/rename_anim_clip_tpac.py`.

**Clip item vtable** (0xB1B0B8) [Certain for the wiring; Likely for the names] (R-Life): slot 10 (0x58C3C0) builds the
runtime clip (0x58BDB0) and registers it; slot 8 (0x58C170) unregisters, rebuilds and registers; slot 11 (0x58C400)
unregisters and cleans the melee table; slot 3 (0x58C100) hands the runtime clip to another item or unregisters it;
slot 5 (0x58BEA0) is the melee-table insert. The load chain is 0x592400 -> 0x5919D0 (keyframes) -> 0x591C30 (copy
and resolve), logging "Unable to read animation clip data for %s" on failure [Certain] (R-Fields).

## 7. Action types, action sets and monster usage

**The XML path** [Certain] (R-Life; `Module.cs:1415-1440`, `MBObjectManager.cs:916-1004`, `XmlResource.cs:210-249`):
1. Each module's `project.mbproj` `<file id="soln_action_sets|soln_action_types|soln_monster_usage_sets">` feeds
   `GetMergedXmlForNative`.
2. That applies the module's `<name>.xsl(t)` to the accumulated document (the Armory's `action_sets.xslt` injects the
   elephant rider actions into `as_human_warrior`), then `MergeElements` merges keyed by the XSD's unique attributes
   (`action_set@id`, `action@type`).
3. `CreateProcessedActionSetsXMLForNative` removes the remaining duplicate ids and hands the XML to native.
4. Live contributors are Native and the Armory only. TAOM_Map's `project.mbproj` uses `<Module>` elements, which
   `XmlResource.GetMbprojxmls` ignores because it selects `base/file` (`XmlResource.cs:219`), and its files do not
   exist.

**Action set parse** (driver 0x58EC00, per set 0x58FFF0) [Certain] (R-Life):

| Attribute | Effect | On failure |
|---|---|---|
| `base_set` | Linear search (0x58FAC0); **copies the base's skeleton, movement system and every slot at parse time** | Logs `Action set "%s" could not be found, using default action set!` and uses set 0 (`as_human_warrior` in merged order [Likely]) |
| `skeleton` | Skeleton model lookup (0xA0CA0) | Logs "Skeleton model could not be found: %s. Fetching a random skeleton model, animations may look wrong."; with no skeleton at all, "Skeleton model is empty for action_set: %s" |
| `movement_system` | `bipedal` = 0, `quadrupedal` = 1 | Logs "Unexpected movement system name: %s" |
| `<action type>` | Action-type map (0xDB02F0) | Logs "Trying to use undefined action ... Please define the action code in action_types.xml file!" and skips the action |
| `animation` | Clip registry lookup (0x58F6F0); the slot becomes {clip, `alternative_group`} | Logs "Could not find animation: %s." and **leaves the slot as it was**: inherited from `base_set`, or {-1, -1} (slots start at -1, 0x592BC0 via 0x5937B0) |

The driver keeps the first node per id and parses in document order. Live data on 2026-09-26: 1,339 sets, 1,323 with
a `base_set`, 0 bases parsed after their child, 0 missing bases, 0 root-level `<action>`; the Kit log shows 0 "Could
not find animation", 0 "undefined action" and 0 "default action set" lines [Certain] (R-Life, `base_order.py`).

**Three corrections that follow** (section 11 lists the owning lines):
- **A phantom `animation=`** does not build a "degenerate record" on 1.5.3. It logs and keeps the slot; a crash needs
  a later consumer that skips the -1 check [Certain] (R-Life).
- **The rider partial's position cannot matter on 1.5.3.** `MergeElements` folds a later module's `as_human_warrior`
  block into Native's element, which is parsed first, before native sees anything [Certain for the code; Likely for
  the conclusion]. The parse-time `base_set` snapshot is real. The cause of the 2026-06-11 goblin "thrust-loop" stays
  UNVERIFIED.
- **Why the client tolerates a root-level `<action>`:** `MergeElements` throws `KeyNotFoundException`
  (`MBObjectManager.cs:830-833`) only when a *later* merge walks an accumulated document holding an element whose
  path is not in the schema, and the Armory is the last module that contributes action sets [Certain for the code
  and the mbproj files; Likely as the explanation]. [armory-guide.md](armory-guide.md) blames the build numbers
  instead; the two explanations are not exclusive.

**Monster to action set** [Certain] (R-Life):
- `Monster` reads `action_set`, `female_action_set` and `monster_usage` (`Monster.cs:304-321`); `MonsterMissionData`
  resolves the set lazily with `MBActionSet.GetActionSet` (`:22-36`).
- `FillAnimationSystemData` (`MonsterExtensions.cs:8-24`) picks the female set when valid and sets
  `MonsterUsageSetIndex = Agent.GetMonsterUsageIndex` (`Agent.cs:5648`). Native 0x620780 searches the usage sets
  (stride 0x1B0) linearly; a miss logs "get_monster_usage_set_index failed %s." and returns -1.
- Bone attributes resolve through the action set's skeleton: `GetBoneIndexWithId(ActionSetCode, bone)`
  (`Monster.cs:646`, `MBActionSet.cs:80`).
- Suffixed sets (`_villager`, `_map`) come from `MBGlobals.GetActionSetWithSuffix` (`MBGlobals.cs:28-40`, a
  `FailedAssert` when missing). `ActionIndexCache.Create(name)` resolves through `MBAnimation.GetActionCodeWithName`.
- The usage tables themselves and their crash rules: [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
  Phase 5.

**Skeleton against clip.** `as_hill_troll_warrior` declares `skeleton="troll_skeleton_a"`, binds 3,909 human clips
beside 792 troll clips (3,941 and 760 before the 14:13 rebind on 2026-09-26), and plays in game (troll-race.md). So binding by clip index does not enforce skeleton identity
[Likely]. Whether bone-count parity (28) is required is UNVERIFIED.

## 8. Runtime playback

**Call chain** [Certain] (R-Fields, R-Flags): managed `Agent.SetActionChannel` (a native pass-through; binding id
164) -> 0x6E1B40 -> the agent's 0x5EDDB0 -> the anim system's virtual slot 48 (`+0x180`). `Agent_anim_system` and
`Horse_anim_system` put 0x655480 there directly; `Human_anim_system`'s 0x63EFA0 calls 0x655480 and then applies the
facial animation and the hand pose. So **facial animation and hand poses apply only to agents whose skeleton runs
`Human_anim_system`**; which monsters get which system is UNVERIFIED. Managed defaults: `blendInPeriod` -0.2,
`blendOutPeriodToNoAnim` 0.4, `blendOutPeriod` -0.2 (`Agent.cs:2402-2405`, R-Life).

**The request struct** packed by 0x6E1B40 [Certain]: `+0` channel, `+4` action, `+8` additional flags, `+0x10` start
progress, `+0x14` blend-with-next-action factor, `+0x18` speed, `+0x1C` blend in, `+0x20` blend out, `+0x24` blend out
to no animation, `+0x28` ignore priority, `+0x29` linear smoothing, `+0x2B` force face morph restart, `+0x2C` melee-table
lookup (always 0 from managed, 0x6E1BD0).

**Channel state** [Certain]: channel c lives at anim system `+0x2470 + c x 0x48` (current action; clip pointer
`+0x2480`; additional flags `+0x2490`; partner action `+0x2474`; partner clip `+0x2488`; look-slope factor
`+0x24AC`). The rgl channel is at `+0x110 + c x 0x11B0`.

**Setting a channel, 0x655480** [Certain] (R-Fields, R-Flags):
1. **Alternatives:** unless the request's additional flags carry bit 31, 0x655310 makes a weighted random pick among
   the action's `alternative_group`, summing each clip's `+0x1D0 >> 60`; a total of 0 keeps the requested action.
2. **Melee table:** with request `+0x2C` set, 0x659030 picks the generated child by the factor, and the factor is
   zeroed (section 3).
3. **Restart:** requesting the action already playing only updates the blend weight, unless the clip has `restart`
   (0x6555B1).
4. **Ladder:** `synch_with_ladder_movement` forces the speed to 0 (0x655629).
5. **`enforce_all`:** a channel-0 clip with it rejects channel-1 requests unless `ignorePriority` (0x655659); starting
   such a clip clears channel 1 (0x655A0F..0x655ABC).
6. **Blend in:** a requested blend-in within 0.001 of -0.2 is replaced by the clip's Blend in period
   (0x65573D..0x65575F). R-Life tagged "negative means use the clip's period" UNVERIFIED; R-Fields and R-Flags read
   this exact test, so it is the -0.2 default, not any negative value, that selects the clip's own period here.
7. **Blend partner:** with a factor f above 0 and a Blends with action that resolves to a different clip in this set,
   both layers play at weights (1 - f) and f, speed normalised by `dur1 (1 - f) + dur2 f`, and the partner is stored
   at `+0x2474`/`+0x2488` (0x655767..0x655987).
8. **Resync mask:** `restart`, `synch_with_horse`, `synch_with_movement` and `synch_with_ladder_movement` (0x12208000)
   are passed as a bool into the rgl channel-set call (0x6557F4, 0x6559A1); its meaning is UNVERIFIED.
9. **Priority gate, 0x658E80:** the new priority is the request's low byte when nonzero, otherwise the clip's. A
   request lower than the current priority is rejected; a tie wins; `ignorePriority` or an empty channel skips the
   check (0x658EAC..0x658F15).

**The tick, 0x655F90** [Certain unless tagged]:
- **Progress:** auto-increment unless cleared; `synch_with_movement` takes it from the movement phase (Human 0x640950,
  Horse 0x6426E0); `synch_with_horse` from the tick's 4th argument; ladder clips from the ladder position.
- **Look slope:** a clip with `blends_according_to_look_slope` and a partner gets a per-tick factor from look pitch,
  through the combat parameter's `look_slope_blend_speed_factor` (0x657880), remapped so level look equals Param 1 and
  clamped by `look_slope_blend_factor_up_limit`/`_down_limit` (0x657470) (0x65697B..0x656A53).
- **Particles:** `spawn_particle` with a particle usage (0x65652C).
- **Step events** (0x5F8FD0, referenced from 0x5D0D00): `use_last_step_point_as_data` suppresses index 3 or more;
  `make_walk_sound` returns early; with `make_bodyfall_sound` a step plays the bodyfall sound; otherwise the clip
  sound (`+0x7C`) plays at step 0 or 1 (0x5BF950), tracked unless `do_not_keep_track_of_sound`, and the voice
  (`+0x1F4`) plays through 0x5D5580 at a step with no sound or index 2 or more. The partner clip is used when its frame
  range and name differ from the base.
- **Finish:** slot 42 (0x653A40) reports the channel finished when progress reaches 1 - blendOut / duration, with the
  request's blend-out when 0 or more and the clip's `+0x1E8` otherwise. Then, in order: the displacement usage is
  applied to the agent; a Continue to action is started with flags 0x2000 (the factor carries over only when the
  continue clip also has a partner, 0x6561C5..0x656254); else `cyclic` replays the action with the priority byte
  stripped (0x656366); else `keep` holds the pose (0x6563CB); else the channel goes to `act_none` with the clip's
  blend out.
- **Human only, 0x63EFA0:** sets the face state (facial index `+0x1F8`, the loop bit from `cyclic`, time reset),
  skipped when the face animation is unchanged and no restart is forced; then applies the hand pose.

**Managed getters** [Certain] (R-Fields, identified by their `CoreInterfaceGeneratedEnum` ids at 0x62F200):

| Managed | Native | Reads |
|---|---|---|
| `MBAnimation.GetAnimationDuration` | 0x6EA020 | `+0x188` |
| `MBAnimation.GetAnimationParameter1/2/3` | 0x6EA100, 0x6EA1E0, 0x6EA2C0 | `+0x1D8`, `+0x1DC`, `+0x1E0` |
| `MBAnimation.GetAnimationBlendInPeriod` | 0x6EA440 | `+0x1E4` |
| `MBAnimation.GetAnimationBlendsWithActionIndex` | 0x6EA520 | `+0x1EC` |
| `MBAnimation.GetAnimationDisplacementAtProgress` | 0x6EA5F0 | the displacement usage (0x47DDF0) |
| `MBActionSet.GetActionAnimationDuration` | 0x6EA3A0 | `+0x188` |
| `MBActionSet.GetActionBlendOutStartProgress` | 0x6EA760 | `1 - (+0x1E8)/(+0x188)` |
| `MBActionSet.GetActionAnimationFlags` | 0x6E9F10 | the whole `+0x1D0` |
| `MBActionSet.GetActionAnimationContinueToAction` | 0x6EA3F0 | `+0x1F0` |
| `SkeletonExtensions.DoesActionContinueWithCurrentActionAtChannel` | 0x6FB520 | clip `+0x1F0` against the channel's current action |
| `SkeletonExtensions.SetAnimationAtChannel` | 0x6FB290 | a negative blend-in uses `+0x1E4` |
| `Agent.GetCurrentAnimationFlag` | 0x6E1990 -> 0x605F90 | effective flags |

Vanilla managed callers include the crosshair reload phases (Param 2 and the continue chain), NavalDLC machines (Param
1, the blend partner, displacement) and `SettlementVisual`'s siege timings (Param 1). Nothing in TAOM's `Main/` calls
these getters (R-Fields grep).

**TAOM's own requests.** `TrollBruteForceCombat.StartFlags` passes `amf_priority_reload | anf_enforce_all |
anf_lock_movement` as additional flags: the priority byte 60 replaces the clip's priority for the channel's lifetime,
and `enforce_all` triggers the channel-1 rules above [Certain] (R-Flags). See
[troll-brute-force.md](../features/troll-brute-force.md).

## 9. Crash surfaces and gates

| Symptom | Cause | Doc | Gate | Gap |
|---|---|---|---|---|
| AV at `+0x6590B9` reading 0x8 to 0x50 (0x8 observed) on the first swing or blocked recoil | A release or blocked clip with no melee-table row, or an action the set does not bind (-1) reaching 0x659030 through 0x6825C0 or 0x683290 [Certain] | Section 3; [troll-race.md](../features/troll-race.md) "The swing CTD"; lessons, "A melee release bound to a custom clip crashes the first swing" | `bind_hill_troll_action_set.py` rule 0 (`MELEE_TABLE`, unit tested); `wire_hill_troll_race.py --check`; `set_clip_balance_name.py --check` (all three uncommitted on 2026-09-26) | Covers `as_hill_troll_*` only, with no general gate. R-Life found the rule-0 and `--check` logic failing any `anim_*` clip on a release or blocked code; the versions edited at 14:11 and 14:12 read the packages (`set_clip_balance_name.keyed_clips`) and accept a self-keyed clip [Certain for the code; the gates' unit tests not run here] |
| AV at `+0x57070C` about a second into deployment | A race head, eye or mouth on LOD0 with no face morph channels: the Kit writes an empty morph record, and 0x570550's null check covers only the record pointer, not its buffer [Certain] | troll-race.md "Face morph channels"; [armory-guide.md](armory-guide.md) "Only LOD0 carries morphs" | `check_race_morph_channels.py` (untracked) | Its spec covers only the hill troll and dwarf f1 FBX sources, not compiled tpacs |
| Null read at `+0x18`, `+8` or `+0x2C` while a clip plays | A flag without its usage: `displace_position` without displacement (0x655E9C, 0x49DEF8); `align_with_ground` without blend (0x65868D); `spawn_particle` without particle (0x65658B or 0x6565AC); `switch_item_between_hands` without hand_switch (0x6C6230) [Certain from the code; not reproduced] | Section 5 | none; the installed clips pass a one-off scratch check | No committed gate |
| AV in a mount context at `+8`, `+0xC` or `+0x10` | A horse movement-table clip without `quad_movement`: 10 unchecked sites, one reachable through `WalkingSpeedLimitOfMountable` (0x6E4C80, `+0x10`) [Certain for the sites] | [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) Phase 1 and "The ways a re-export breaks a working mount" | `verify_mount_assets.py` CHECK 2 | Only the creatures in its config. The 1.5.3 crash offset has not been observed (R-Life) |
| AV in human locomotion | A human locomotion-table clip without `bip_mov_ik`: 36 unchecked sites [Certain for the sites] | Section 5 | none | Not observed; vanilla-derived sets inherit valid clips |
| Crash after a clip ends | Continue to action names an action the agent's set lacks: 0x58F840 logs "does not contain" and returns a negative index; the tick indexes `remap[-1]` (0x6561EB) and faults at 0x65621E when that entry is null [Likely]; the entry's content is UNVERIFIED | Section 8 | none; grep the rgl log | No gate |
| Null read at 0x6553C8 | A clip missing from an alternatives group: when the remap entry is -1, 0x6553C6 zeroes the pointer and the weighted pick reads `[0 + 0x1D0]` [Certain for the read]; reachability UNVERIFIED | Section 8 | none | |
| Delayed crash after "Could not find animation" | A phantom `animation=`: the slot keeps its inherited value or -1; a crash needs a later consumer that skips the -1 check [Certain] | creature-mount-authoring.md gotcha 7 (describes older behaviour) | `verify_mount_assets.py` CHECK 3 (creatures); the troll binders bind only clips on disk | No gate for humanoid sets; grep the rgl log |
| Dedicated server dies on boot | A root-level `<action>`: `KeyNotFoundException` in `MergeElements` [Certain] | armory-guide.md "action_sets structure"; [module-armory.md](../modding/module-armory.md) | `audit_action_set_parity.py` (exits 1) | none |
| A package's clips never appear | No `.rdc` entry: the package is skipped whole, silently [Likely] | lessons, "A hand-built tpac package is invisible" | `check_rdc_entries.py` | The client's skip path is UNVERIFIED |
| Divide by zero in `CreateAgent` | A `monster_usage` miss: -1 (2026-06-04) | creature-mount-authoring.md Phase 5 | `audit_mount_parity.py` | It always exits 0 (per the creature doc), so nothing fails |
| AV in `monster_usage.cpp` | A usage-table key miss dereferences the end sentinel (1.4.6); the path string still exists in 1.5.3 [Certain] | creature-mount-authoring.md Phase 5 | the parity audit | 1.5.3 offsets UNVERIFIED |
| A clip silently missing | A duplicate clip name: the second is refused with a log line [Certain] | Section 6 | none | |
| rglVec3 assert on load | TOC size 0 in a rewritten tpac | lessons, "A tpac's header carries the TOC size" | the writers' round-trip checks | |
| Hand poses wrong or a read past the vector | Hand pose 5 or more: no upper bound check on the 25-entry vector [Likely], UNVERIFIED in game | Section 2 | none | The Kit accepts -1000..1000 |
| A usage silently ignored | A third clip usage, dropped with a log line [Certain] | Section 5 | none | |
| Every later field misread | An unknown usage name in a hand-built tpac [Certain] | Section 5 | none | The Kit cannot write one |
| Kit: "External .rdc file modification detected" | An `.rdc` rewritten while the Kit runs | Section 6 | `set_clip_balance_name.py` refuses while the Kit runs | Restart the Kit |

**Also not a crash, but silent:** an unknown Blends with action or Continue to action name becomes -1; an unknown
combat parameter leaves a swing with no collision window; an unknown flag name is dropped; the load validator
rewrites seven flag combinations (section 4).

## 10. How-tos

### 10.1 Make a new race clip usable for melee

1. **Sort the actions.** The four families `act_release_*`, `act_quick_release_*`, `act_blocked_*` and
   `act_quick_blocked_*` go through the melee table; wind-ups, defends, parries, kicks, bashes, hit reactions and
   falls bind like any other clip (section 3).
2. **Give each clip bound to those families a row.** Self-key it (Blends with animation = its own name, Blends with
   action empty, parents empty, child index -1) or author a `_balanced` twin. Never name a vanilla clip.
3. **Set Loading Type to Always keep in memory** before binding [Likely]; the 2 on 48 troll release and blocked clips
   is untested, and 24 of them are bound since 14:13 on 2026-09-26 (section 3).
4. **Combat parameter id:** give the swing an id with a collision window. `hit_bone_index` is a raw bone index into the
   agent's skeleton [Certain], so on a race skeleton confirm the index lands on the weapon hand [Likely].
5. **Param 1 = reach:** run Compute Reach with the race's skeleton model in the Skeleton combo; the reach cluster reads
   it [Likely: the AI].
6. **Priority and flags:** copy the nearest vanilla clip's recipe
   ([bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md)); hand poses 0..4 only.
7. **Save in the Kit,** or run `python tools/set_clip_balance_name.py --clips-file names.txt --apply` then `--check`
   with the Kit and the game closed. Confirm the RDC entry (`check_rdc_entries.py`).
8. **Bind and run the gates.** Bind the clips in the race's set. For the hill troll, the binder's rule 0 and
   `wire_hill_troll_race.py --check` accept a self-keyed clip since 14:12 on 2026-09-26 (section 9); a new race needs
   the same rule in its own binder.
9. **Smoke:** a Custom Battle with a light and a heavy weapon (balance slot 0 against slot 9), then read the rgl log
   (10.3).

### 10.2 Add clips for a new race skeleton

1. **Author on the engine skeleton's frames,** rest pose at frame 0
   ([bannerlord-skeleton-authoring.md](bannerlord-skeleton-authoring.md)); a retarget keeps the root height
   ([ue-to-bannerlord-asset-pipeline.md](ue-to-bannerlord-asset-pipeline.md)).
2. **Import** each take into the Kit, then census the masters (lessons, "Every animation the Modding Kit imports may
   name no skeleton"). Re-importing a rig resets hand-bound materials and can drop the skeleton
   ([armory-guide.md](armory-guide.md), creature-mount-authoring.md).
3. **Create the clips,** in the Kit or from vanilla templates with `tools/gen_troll_anim_clips.ps1`
   (ue-to-bannerlord-asset-pipeline.md "The clip stage"). A template copy carries the vanilla clip's Blends with
   animation, Blends with action and Loading Type: check all three.
4. **Fill each clip:** Animation source, Duration, Source 1 and 2 (the Sample Rate display should match the master),
   flags and priority from the recipe, the usages its flags need (`displace_position` with displacement,
   `align_with_ground` with blend, locomotion tables with `bip_mov_ik` or `quad_movement`), a Continue to action the
   set binds, hand poses 0..4, sound and voice codes that exist, and a combat parameter on attacks. Keep names to 63
   characters, and never rename a clip in the Kit.
5. **Save,** then `python tools/check_rdc_entries.py --under <folder>`.
6. **Meshes:** the race's LOD0 head, eye and mouth need 101 face morph channels and its hands 26 hand-pose channels
   (`check_race_morph_channels.py`, troll-race.md).
7. **Action set:** `skeleton=`, `movement_system=`, and a `base_set` so the set inherits the full action surface (a
   standalone set rots: lessons, "A standalone race action_set"); bind only clips that exist; for melee, follow 10.1.
8. **Monster:** `action_set`, `female_action_set`, `monster_usage`.

### 10.3 Check a package before a battle

| Command | Checks |
|---|---|
| `python tools/check_rdc_entries.py --under <folder>` | Every `_geo` and `_anm` has an RDC entry (masters exempt); exit 1 on a miss or an empty scan |
| `python tools/check_race_morph_channels.py` | Race head and hand morph channels (FBX sources in its spec only) |
| `python tools/verify_mount_assets.py` | Creature clips: `quad_movement` (CHECK 2) and bound clips that exist (CHECK 3) |
| `python tools/audit_action_set_parity.py` | Root-level `<action>` (exit 1) and inheritance parity |
| `python tools/audit_mount_parity.py` | Monster usage coverage (report only; always exits 0) |
| `python tools/wire_hill_troll_race.py --check` | Hill troll wiring, including no unkeyed troll clip on a melee-table code (uncommitted on 2026-09-26) |
| `python tools/set_clip_balance_name.py --clips-file <names> --check` | Self-keyed clips have the right bytes, checksum and RDC stamp |
| `python tools/validate_moduledata.py` | Items, troops and cultures across the three modules |

Then launch the Kit or the game and search the newest `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`:

| Log text | Meaning |
|---|---|
| `Loading packages %s...` | Which asset trees loaded |
| `Could not find animation:` | An action set names a clip no package registered; the slot kept its old value |
| `Trying to use undefined action` | An action type missing from `action_types.xml`; the action was skipped |
| `could not be found, using default action set!` | A missing `base_set`; set 0 was used |
| `Skeleton model could not be found` | A wrong `skeleton=` |
| `does not contain` | A runtime lookup of an unbound action (0x58F840) |
| `Combat parameter not found:` | A clip with no collision window |
| `Sound not found:` | A sound code miss |
| `Could not find face animation record with name:` | A facial id miss |
| `Clip usage data couldn't assigned` | A third usage was dropped |
| `Unable to register animation clip` | A duplicate clip name |
| `Please set hit_bone_index` | A combat parameter without a usable hit bone |
| `not found for combat animation blending!` (Kit) | A missing `_balanced` twin on Save |
| `get_monster_usage_set_index failed` | A `monster_usage` miss |

## 11. Corrections owed to other docs

These docs were read and not edited here; each line is a finding from the reports above.

| Doc | Claim | Finding |
|---|---|---|
| [bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md) "Where the flags live" | The clip is recompiled with the flag bitfield | Flags are saved as a list of names; a bit with no name is dropped (section 4) [Certain] (R-Kit) |
| same, Cat 1 line 128 | Only two flags are bit-tested in managed C# | Seven managed files test nine flags; `AnimationPoint.cs:305` is `:248`/`:543` in the 1.5.3 decompile [Certain] (R-Flags) |
| same, lines 26, 93, 101, 150 | `synch_with_movement` is the anti-skate flag for gaits | 0 of 435 human gait clips carry it; it drives progress from the movement phase and sits on 65 rider and head-turn overlays [Certain] (R-Flags) |
| same, line 171 | `cyclic` is required on all locomotion | 0 of 435 bipedal and 48 of 142 quadrupedal clips are cyclic; the movement system drives gait progress [Likely] (R-Flags) |
| same, lines 103, 151 | `use_last_step_point_as_data` marks the stride reference | It sits on 62 equip-type clips and silences step index 3; `*_stand_for_movement_data` clips carry no flags and a `quad_movement` usage [Certain] (R-Flags) |
| same, line 152 | `displace_position` moves the agent by baked root travel | The travel is the displacement usage's vector, and the usage is required [Certain] (R-Flags) |
| same, lines 102, 158 | `align_with_ground` suggested on idles | On humans it needs a blend usage or it reads null [Certain] (R-Flags) |
| same, lines 169-170 | Layer bits 36..51: "don't hand-set" | They are ordinary Kit checkboxes packed into the rgl layer word [Certain] (R-Flags) |
| same, line 175 | `disable_alternative_randomization` opts a clip out | It is not a clip flag; it works only as a request flag (R-Kit, R-Fields, R-Flags) |
| same, line 197 | `spawn_particle` enables baked VFX keys | It reads the particle usage and crashes without one [Certain] (R-Flags) |
| same, line 207 | `switch_item_between_hands` | Needs a hand_switch usage, on channel 1 [Certain] (R-Flags) |
| same, lines 58-60 | Step points are sound triggers | They also fire the voice and the bodyfall sound [Certain] (R-Fields) |
| same, "A clip can carry its own motion" | `UnknownUInt2` 0 or 2 selects the master or the clip's own segment | Byte 0 is Loading Type (0, 1, 2 = Always keep, Load when needed, Never load) and byte 1 Do not optimize [Certain] (R-Kit, R-Fields); value 1 occurs on 563 vanilla clips; the segment reading came from a 1.4.6 handoff (R-Life relays it) |
| same, lines 241-246 | 130 entries; 165 entries, 130 with a window | 165 in the text, 28 commented out, 137 live; 105 live entries set a window [Certain] (R-Fields) |
| same, TODO at lines 248-255 | The metadata layout is not reverse-engineered | Resolved: R-Flags' `clipparse.py` reads every byte of all 6,177 vanilla clips; the field map is section 2 |
| [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) Phase 4.3 and gotcha 8; [module-armory.md](../modding/module-armory.md) line 387 | The rider partial must sit at the top of the file | Managed merging happens first, so position cannot matter on 1.5.3 [Certain for the code; Likely for the conclusion] (R-Life) |
| creature-mount-authoring.md gotcha 7, and the `verify_mount_assets.py` FAIL text | A phantom binding makes a degenerate record | On 1.5.3 it logs and keeps the slot [Certain] (R-Life) |
| creature-mount-authoring.md gotcha 4 | `quad_movement` plus step points | The unchecked reads are of the usage; 42 vanilla quad clips have no step points, so step points do not look required for safety (R-Flags); no in-game test |
| creature-mount-authoring.md gotcha 5, lines 268, 485 | Gallop run clips need `cyclic` | Vanilla horse gallop clips carry only `make_walk_sound`; no native `cyclic` requirement was found; the cause of the mid-charge CTD is UNVERIFIED (R-Flags) |
| [troll-race.md](../features/troll-race.md) "The swing CTD" | All 480 hill troll clips have both fields empty | As of 14:24 on 2026-09-26, 30 are self-keyed (2 in the Kit at 13:21, 28 by script at 13:28) and 32 melee codes bind them since 14:13; 24 of the 30 are at Loading Type 2. Add the Loading Type risk and the unbound-action path (section 3) [Certain] (R-Life, checker) |
| [armory-guide.md](armory-guide.md) "Two asset trees" | Two trees | The Armory also has an `EmAssetPackages/` tree (237 tpacs, 2026-09-19) [Certain] (R-Life) |
| armory-guide.md "action_sets structure" | The root-level `<action>` split is build numbers | Also explained by merge order (section 7) [Likely] (R-Life) |
| `tools/audit_action_set_parity.py` | Unions `base_set` inheritance regardless of order | The engine snapshots at parse time; live data has 0 violations, but the audit cannot catch one (R-Life) |
| `engine_meta.py` labels (old scratch folder) | Unknown bytes and `unknown_int` | They are Loading Type, Do not optimize and padding, and Randomization weight (R-Fields) |

## 12. Open questions

Everything below is UNVERIFIED. Grouped by where it sits.

**Kit**
- How Save reaches per-clip RDC cooking (Kit 0xB8A080).
- How an FBX take name becomes the master's item name.
- Whether closing the inspector without Save discards the edits.
- The ranges of the vec3 usage fields and the defaults of new usage records other than Blend.
- Whether Compute Reach's item point includes a weapon tip offset.
- The hand pose names, and whether pairs map one to one onto the 26 hand-pose channels [Likely].

**Clip fields at runtime**
- What plays for a clip at Loading Type 2 when bound directly or through a self-keyed row.
- Param 1 on reload, blocked and ready clips; Param 2 and Param 3 outside reload and death; what the death hand-off
  time means (ragdoll or body-down).
- Consumers of Do not interpolate and Do not optimize outside 0x460000..0x600000.
- The step-point progress comparator, and the reader of step point W used as data.
- Hand pose values of 5 or more in game.
- Native readers of `collision_radius`, the custom capsule, the attack cooldown and the shoulder hit bone.

**Flags**
- No native consumer found for `disable_agent_agent_collisions`, `ignore_static_body_collisions`, `disable_hand_ik`,
  `stick_item_to_left_hand`, `affected_by_movement`, `ignore_slope`, `ignore_scale_on_root_position` and the two rope
  flags; the native effect of `allow_head_movement` and `enforce_root_rotation`.
- The effect of `enable_hand_spring_ik`, `use_left_hand_during_attack` and `ignore_all_collisions`; the other test
  sites of `cyclic`, `enforce_lowerbody`, `enforce_all` and `lock_movement`; the mode meaning of `client_prediction`.
- The meaning of the resync bool passed to rgl.

**Runtime**
- Which monsters run `Human_anim_system` (and so get facial animation and hand poses).
- The native writers of the melee-table request byte `+0x2C`, and whether vanilla twin-keyed swings always take the
  table path; the fourth table caller 0x6FE170.
- The content of `remap[-1]` when a Continue to action is unbound (crash [Likely]).
- The virtual path from `ISkeleton.tick_animations` (0x50C3B0) to the horse movement node slot 0x74C950.
- Whether bone-count parity (28) is required when a set binds clips from another skeleton.
- The mid-charge gallop CTD's cause.

**Packages and XML**
- The asset-tree selection rule in 0x73960 (`Assets`, `AssetPackages`, `EmAssetPackages`).
- The client's skip path for a package without an RDC entry, and its behaviour when the stamp and the checksum differ.
- Whether the game client's XML load order matches the Kit's [Likely].
- That set 0 is `as_human_warrior` in merged order [Likely].
- The 1.5.3 crash offsets for a missing `quad_movement` and a usage-table key miss.
- The cause of the 2026-06-11 goblin "thrust-loop".

## 13. Sources

**Evidence reports** (2026-09-26, this session; their text lives only in the workflow run, so this page is the
durable record): **R-Kit** (the inspector controls, flag table, usages and Save, Kit DLL), **R-Fields** (every metadata
field's client consumer, Param meanings, combat parameters, hand poses), **R-Flags** (flags and clip usages at runtime,
null-check audit, doc audit), **R-Life** (import, packages, RDC, XML path, melee table, crash surfaces).

**Kit RVAs** (`Win64_Shipping_wEditor/TaleWorlds.Native.dll`):

| RVA | What |
|---|---|
| 0x14392C8, 0xB987F0 | `AnimationClipInspector` vtable, constructor |
| 0xB919C0, 0xB93FE0 | Inspector builder (labels, ranges), fill and bind |
| 0xB96030 | Edit dispatcher (vtable slot 59) |
| 0xB9704E | Save branch |
| 0xB9753E..0xB9841A | Compute Reach |
| 0xB89C40 | Metadata clone (item slot 15) |
| 0xB8C890 | Metadata writer |
| 0xB8B2A0 | TaleWorlds' metadata dump (item slot 43): internal field names |
| 0xB892D0 | Warning text (item slot 42) |
| 0xB88CE0 | Pre-metadata update, can refuse a save |
| 0xB88540 | Blend-child generator (item slot 25) |
| 0xB8A080 | Per-clip RDC writer and Loading Type warnings (item slot 33) |
| 0x5B170, 0x4276D0, 0x4280A0 | `apply_patch`, patch open, add metadata |
| 0x4626D0 | XXH64 |
| 0x45EB90 | `rglRuntime_data_cache_manager` (RDC write) |
| 0x67C60 | `signal_package_item_change` |
| 0x1841370 | Flag name table (43 entries) |
| 0x1439520 | Loading Type combo table |
| 0xAA2FC0, 0xF0A980 | FBX animation importer, FBX Import Settings dialog |
| 0xB15A70, 0xB17580 | New animation clip |
| 0x796C0 | External `.rdc` modification handler |
| 0xD0890 | Hand pose table (asserts below 5) |

**Client RVAs** (`Win64_Shipping_Client/TaleWorlds.Native.dll`):

| RVA | What |
|---|---|
| 0x73960, 0x72F00 | Package loader loop, package reader |
| 0x58C500, 0x58B980, 0x58C210 | Metadata reader, constructor, copy |
| 0xD11660 | Flag name table |
| 0xB1B0B8 | `Animation_clip_item` vtable; slots 3, 5, 8, 10, 11 at 0x58C100, 0x58BEA0, 0x58C170, 0x58C3C0, 0x58C400 |
| 0x5682B0, 0xDB0360 | Melee table insert (64-byte key), the table |
| 0x659030 | Melee table reader; the swing CTD at `+0x6590B9` |
| 0x6825C0, 0x683290, 0x6FE170 | Melee table callers (balance clamp in the first two) |
| 0x58F4C0, 0x58F6F0, 0x58F840 | Clip registration, clip lookup, action-to-clip in a set |
| 0x592400, 0x5919D0, 0x591790, 0x591590, 0x592520, 0x5927B0 | Load chain, keyframe loaders, keyframe set, cache key |
| 0x591C30 | Runtime clip build (every field copy) |
| 0x592930 | Load-time flag validator |
| 0x5A8A90, 0xDAC618, 0x5AB000 | Combat parameter parser, table, blended child parameter |
| 0x58EC00, 0x58FFF0, 0x58FAC0 | Action set driver, per set, `base_set` search |
| 0x620780 | Monster usage set index |
| 0x6E1B40, 0x5EDDB0 | `SetActionChannel` binding, agent set channel |
| 0x655480, 0x655310, 0x658E80 | Set channel, alternatives pick, priority gate |
| 0x655F90, 0x653A40, 0x655E00 | Channel tick, finish test, per-tick displacement |
| 0x5F8FD0, 0x6D9C00 | Step events, footsteps |
| 0x63EFA0, 0x63F0B0, 0x6585B0 | Human set channel override, Human foot IK, align with ground |
| 0x605F90, 0x658FB0, 0x6412C0 | Effective flags, layer word |
| 0x4728D0, 0xA09A0, 0x6CCE10 | Hand pose decode, morph cache, apply |
| 0x570550, 0x57070C | Static face morph; the face morph CTD |
| 0x6E9F10..0x6EA760, 0x6FB290, 0x6FB520 | Managed getters |

**Managed files:** `C:/Users/mikew/.taom-src/v1.5.3/TaleWorlds.MountAndBlade.AnimFlags.cs` (flag values and priority
bands, read 2026-09-26); `Agent.cs`, `MBActionSet.cs`, `MBAnimation.cs`, `Monster.cs`, `MonsterExtensions.cs`,
`MBGlobals.cs`, `ActionIndexCache.cs`, `MBObjectManager.cs`, `Module.cs`, `XmlResource.cs`,
`WeaponComponentData.cs`, `MissionScreen.cs`, `MissionConversationLogic.cs`, `AnimationPoint.cs`,
`AgentVictoryLogic.cs` and the NavalDLC machines, as cited inline.

**Scratch evidence** (session scratchpad, not durable:
`C:/Users/mikew/AppData/Local/Temp/claude/e--repos-TAOM/846fe3ce-a4a5-49cc-b77d-7ca82f1fb3d1/scratchpad/re/`):
- `map/kit-controls/`: `kc.py`, `show.py`, `spinargs.py`, `tables.py`, `usages.py`, `usage_bind.py`, `bindscan.py`,
  `vtslots.py`, `find_vt.py`, `fstr.py` and `d_*.txt` disassemblies (R-Kit).
- `map/gameside/`: `vanilla_full.tsv` (every field of every vanilla clip), `join.py`, `census.py`, `cp_schema.py`,
  `cpmap.py`, `p2c.py`, `d_591c30.txt` and more (R-Fields).
- `map/flags-usages/`: `clipparse.py` (the full metadata parser), `vanilla_clips_full.jsonl`, `armory_clips.jsonl`,
  `taomcheck.py`, `flagscan4.py`, `nullcheck.py`, `bindmap.json`, `d_655480.txt`, `d_655f90.txt` and the `src/`
  decompile (R-Flags).
- `map/lifecycle/`: `census.py`, `uint2.py`, `melee_bind.py`, `base_order.py`, `d_58fff0.txt`, `d_58ec00.txt`,
  `d_73960.txt` (R-Life).
- Earlier: `engine_meta.py`, `vanilla_clips.tsv`, `troll_clips.tsv` at the `re/` root.

`clipparse.py` is the reader worth promoting to `tools/` if a committed clip-metadata gate is built.

**TAOM docs and tools:** the owners table at the top;
[rca-hill-troll-and-loc-sweep-2026-09-25.md](../reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md);
[lotrlome-armory-snapshot/README.md](lotrlome-armory-snapshot/README.md); [tools/README.md](../../tools/README.md) for
`check_rdc_entries.py`, `tpac_fix_item_checksums.py`, `tpac_skeleton_scan.py`, `rename_anim_clip_tpac.py`,
`bind_hill_troll_action_set.py`, `wire_hill_troll_race.py`, `set_clip_balance_name.py`,
`check_race_morph_channels.py`, `verify_mount_assets.py`, `audit_action_set_parity.py` and `audit_mount_parity.py`.
