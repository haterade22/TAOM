# Race faces and hands: morph channels, the engine, the process

How a race head's face morphs and a race hand's grip morphs work in Bannerlord v1.5.3, what goes wrong, and the
process and gates TAOM uses when it builds new heads, shape keys and hand keys. Written 2026-09-27 from three
incidents: the hill troll's face crash (#684), the female dwarf's face crash (#385) and her still eyes, and the hill
troll's torn wrist. The per-incident detail stays in [troll-race.md](../features/troll-race.md) and the
[snapshot README](lotrlome-armory-snapshot/README.md); this page is the rules and the method.

Evidence tags: **[Certain]** read from the engine DLL, a package or a measurement; **[Likely]** TAOM's reading of
decompiled code; **UNVERIFIED** not established.

## What the engine does with a face

**The head is a metamesh the skin names.** `skins.xml` gives each skin a `face_meta_mesh`; its LOD0 sub-meshes are
the face base, the eyes and the mouth, each tagged in the package: `face_base_mesh`, `face_eye_mesh`,
`face_mouth_mesh` (and optionally `face_eyelash_mesh`). The tags live in the package, not the XML (TpacTool shows
them as a mesh's `MaterialFlags`). The Kit makes one sub-mesh per FBX object, so export the head as separate objects
`<mesh>`, `<mesh>.eye` and `<mesh>.mouth`; joined, it splits by material and a mouth sharing the head's material
cannot be tagged [Certain].

**The face builder picks each part by its tag, not by its position** [Certain]. Decompiled on v1.5.3
(`TaleWorlds.Native.dll` function 0x56D5C0, `tools/native_decompile.py --rva 0x56DDBA`): one pass records the index
of the sub-mesh tagged `face_eye_mesh`, `face_mouth_mesh` and `face_eyelash_mesh`; a second finds `face_base_mesh`
and falls back to the first sub-mesh only when nothing carries that tag. Two more functions that use the base mesh
(0x574C30, 0x583350) find it the same way. So the order of the sub-meshes does not matter: the female dwarf's and
Saruman's heads store the eye first and the elf's, uruks' and male dwarf's store the base first.

**Materials** [Likely]. In the same builder, every sub-mesh that is not the eye, the eyelash or the mouth gets the
skin's `face_texture` material, the mouth gets its `mouth_texture`, and the eye keeps the material the package gives
it. The tattoo code tests a tattoo name for `scar`, `blindlefteye` and `blindrighteye` and writes the blind-eye
values onto the eye sub-mesh. A mesh's second material slot plays no part in how an eye draws: the working elf,
uruk and pale uruk eyes leave it empty.

**The static face morph** [Certain]. Every time an agent is built, the engine morphs the face parts by the
character's face keys (function 0x570550; its only string is "No morph data found for face mesh. Can not do static
morph."). It skips a mesh only when the mesh has no morph object at all. The Kit gives a mesh with no shape keys an
empty morph record whose buffer is null, which passes that check, and the routine then reads each vertex's row from
the null buffer: an access violation at `+0x57070C` on v1.5.3 (`+0x58232C` on v1.4.7). The faulting read address
divided by 6 is the vertex count, which names the mesh: `0x24C` is 98, the dwarf's eye; `0x168C` is 962, the hill
troll's head. The Kit never runs this morph, so every Kit look passes.

**Channels** [Certain]. A working race head's LOD0 face base, eye and mouth each carry 101 channels; LODs carry
none (the cave troll's head has 100 and works). Names differ by author (`shape_01` to `shape_101`, `..._frame_150`
to `_250`, `Basis_0` to `Yell_100`), so the engine takes them by order. Each of the 63 `deform_key`s in a Native
human skin names one channel by its `key_time_point`, 1 to 63 [Certain, Native `skins.xml` 2026-09-30]: 1 to 58 are
sliders, 59 is `eyebump`, 60 to 63 are weight, build, height and age. No key names 64 to 100; yotthani's FaceLearner
found those carry the facial animation, and zeroing them froze the eyelids (unmeasured by TAOM;
[head-mesh-and-groom-authoring.md](head-mesh-and-groom-authoring.md) section 1). A tool that zeroes face channels
keeps 59 and up.

**The eye must move with its socket** [Certain]. The morph moves each part by its own channels. On a working head
the eye's channels carry the eyeball with the socket around it: the male dwarf's socket ring moves 6.2 mm on channel
14 and his eyeball 8.0 mm; Saruman's socket 30.2 mm on channel 46 and his eyeball 29.8 mm. When the head's channels
move the socket and the eye's move nothing, the socket opens around a still eyeball and the game shows the socket's
skin and a dark gap where the eye should be. The Kit shows the head with no morph, so it looks right there. A head
whose channels all move nothing (the hill troll's zero-filled face) opens no gap, and its sliders do nothing.

**Channel weights.** Blender 5.2's `Object.shape_key_add` creates a key at value 1.0 and the FBX exporter writes that
as the channel's `DeformPercent`, so a file re-imports with every channel applied; write every key at 0. Whether the
Kit or the engine applies `DeformPercent` at all is UNVERIFIED (the hill troll's face channels carry 100 and hold no
offsets, so nothing shows).

## What the engine does with a hand

`human_skeleton` has no finger bones below `finger0`, so every grip and fist is shape keys: **26 hand-pose channels
on the LOD0 hand or arm mesh** [Certain]. Without them nothing crashes; the fingers never close on the weapon (the
hill troll's first build). A clip's two hand poses, 0 to 4 each, select morph key `5L + R + 1` of 25, unchecked in
the game and asserted below 5 by the Kit [Likely]; that the 26th is a neutral is likely, not proven. The uruk, pale
uruk and dwarf carry them on LOD0 only. Hand poses and facial animation run in the engine's human animation system;
which monsters use it is UNVERIFIED, but the hill troll's hand morphs work in game.

- **Take them from a hand mesh, never an arms mesh.** The Isengard uruk's arms mesh moves at mid-forearm, so its
  channels pulled the troll's wrist seam by up to 16% of the channel peak: a torn wrist.
- **Fit, don't copy.** `transfer_hand_morphs.py` frames each hand by its geometry (wrist to knuckles, the palm side
  from the thumb), fits per axis and binds with Surface Deform so thick fingers turn rather than shear.
- **Judge the wrist by eye.** Its seam gate reads the seam after the pin has zeroed it, so it cannot fail; the
  `--preview` renders and an in-game look are the real check (lesson "A gate that runs after the fix it checks can
  never fail").

## The process for a new head or hand

1. **Export** the head as separate objects `<mesh>`, `<mesh>.eye`, `<mesh>.mouth`, and the hands as their own mesh.
2. **Face channels.** Keep an artist's 101 channels where they exist. Where a face part has none,
   `tools/blender/add_face_morph_channels.py` adds 101 zero-offset channels at weight 0 (it refuses any other
   existing count). **If the head's channels move but the eye's are zero, run `tools/blender/fit_eye_morphs.py`**:
   it sets each eye channel to the least-squares translation and uniform scale of that eye's socket ring (the head
   vertices within half an eyeball's width), and refuses an eye that already moves.
3. **Hand channels.** `tools/blender/transfer_hand_morphs.py` from a working race's hand mesh, with `--preview`.
4. **Before the Kit:** `python tools/check_race_morph_channels.py` (exact counts; add a new rig's meshes to its `SPEC`
   first) and `python tools/audit_fbx_lods.py --diff <old> <new>`.
5. **The Kit:** re-import the FBX and Save (that writes the package's RuntimeDataCache entry); tag new face parts;
   check every sub-mesh and LOD kept its material, since a re-import can reset them.
6. **After the Kit:** `python tools/check_eye_follow.py --package <_geo.tpac> --metamesh <head>` (fails when an
   eyeball stays still in a moving socket; prints socket and eye motion per channel) and
   `python tools/check_rdc_entries.py --under "<folder>"`.
7. **In game:** a character of that race with non-neutral face sliders, close up; for hands, a weapon in the grip.
   A new crash here is `/native-crash-triage`; divide the faulting address by 6 to name the mesh.

### Exporting a face for a lord

A lord's `<face>` takes the exact `<BodyProperties version="4" ... key="..."/>` string of a face built in the
game's face editor. The editor copies it: **Ctrl+C** in the face editor puts
`BodyGen.CurrentBodyProperties.ToString()` on the clipboard, and Ctrl+V pastes one back [Certain]
(`BodyGeneratorView.TickInput`, hotkeys from `FaceGenHotkeyCategory`; the clipboard code lives in the view, not in
`FaceGenVM`). The same view hosts character creation and the barber.

1. Build the face in character creation or the barber, on a character of the lord's race and sex.
2. Press Ctrl+C and paste the string into the lord's `<face>`: `characters/lords.xml` for a TAOM lord,
   `lords.xslt` for a vanilla lord TAOM overrides (Sauron, `lord_1_17`). `heroes.xml` holds no faces.
3. To make a face on a race no culture offers, add the race to a culture's `races` in
   `charactercreation/cultures.json` for the session and take it out again before committing (Sauron,
   2026-09-28): a race without `as_<race>_facegen` sets falls back to the human set in character creation, and
   a player race takes every system keyed on that race name.

### What the face key holds

The `key` is 128 hex characters, eight 64-bit parts (`StaticBodyProperties.KeyPart1..8`); every conversion is native
(`get_params_from_key` 0x57A0E0, `produce_numeric_key_with_params` 0x57B830, v1.5.3) [Certain for the layout, from
the decompile]:

- **Part 1:** six-bit fields for the hair, beard, face texture and tattoo indices, then the tattoo, hair, eye and
  skin colour positions (each value / 63).
- **Parts 2 to 5: one hex digit per slider**, low digit first, in the skin's `deform_key` document order. A slider's
  position is digit / 15 (encoding rounds, ties down), and its channel weight is
  `key_min + position * (key_max - key_min)`. The `weight`, `build`, `height` and `age` keys sit at the end without a
  range; `weight` and `build` take the `<BodyProperties weight= build=>` floats directly.
- **Parts 6 to 8:** hair, beard, tattoo and face texture filters, then voice, mouth texture, eyebrow, hair flip,
  height multiplier and voice pitch.

**A face that starts as the head was authored** puts every channel at weight 0, so each slider's digit is the one
whose weight lands nearest 0. A range that straddles 0 evenly puts it at digit 7.5, which cannot be stored, so the
nearest digit is half a step off (small once the ranges are tuned, below). A range that excludes 0 never reaches the
authored head: vanilla's `face_ratio` (0.5 to 1.1) held Saruman's face 5 mm off his FBX until its near end was moved
to 0. Computed this way from Mike's exported Saruman key (keeping its hair, beard, colours, voice and height), the
largest remaining offset from his FBX was 0.69 mm (`kid_face`, whose range excludes 0); Mike then chose a face of
his own for the lord (2026-09-29). A key stores slider positions, not shapes, so a face built before a range change
looks different after it.

## Slider reach

A `<deform_key>` in the skin is one face slider: `key_time_point` names the morph channel it drives, and `key_min`
and `key_max` are that channel's weight at the two ends of the slider [Certain for the attributes; the exact
slider to weight formula is being researched]. Keys with no range (`age` on channel 63 and three others) are driven
by the engine. **Every TAOM race copied vanilla's human ranges** (male dwarf 58 of 60 keys, Saruman 59 of 59, the
adult female dwarf 59 of 60), but vanilla tuned those weights for its own head's channels, so a head authored with
larger channels moves further on the same slider. Measured 2026-09-29 as channel travel times the larger weight: the
female dwarf reached 1.5 to 3.3 times the male dwarf's travel on 27 sliders, Saruman up to 9 times on 32.

A new race head gets its ranges scaled to the male dwarf's reach (the yardstick: it runs vanilla's ranges, and
vanilla's own head packages do not open in TpacTool 0.4.0): export the heads with `tools/export_face_morphs.ps1`,
then `tools/oneoff/tune_face_slider_reach.py` (dry run, `--apply`, `--check`). It scales both ends of a range by
one factor, so the slider position that shows the head as authored does not move. Vanilla also fixes `eyebump`
(channel 59) at weight 1 on every skin; on a vanilla head that channel barely moves, but a custom head can author it
large (Saruman's moved 13 mm), and then the face in game never matches the FBX.

## Hair, beards and eyebrows

The skin's `hair_meshes`, `beard_meshes` and `eyebrow_meshes` are "upper meshes": separate metameshes the face
builder attaches over the head. **Vanilla is the reference, and it gives them no morph channels at all**
[Certain]: every beard and hair metamesh in `Native/EmAssetPackages/pack3/pack3.tpac` (98 metameshes: `beards_c_a`,
`beards_c_k`, `hair_male_c_b` and the rest, 50 to 3,728 vertices at LOD0) has 0 morph frames.

**How they follow the face** [Likely, from the v1.5.3 decompile]. The face builder (0x56D5C0) takes the GUID of
the head sub-mesh tagged `face_base_mesh` and hands it, with each upper mesh, through 0x572B40 to 0x56EBA0. That
function looks up a table by the two GUIDs and, for each of the head's channels (the count read off the head),
adds to every upper-mesh vertex the delta of one head vertex (a uint16 index per upper vertex). A missing head
entry logs "Mapping data could not be found between base mesh and upper mesh(beard, hair, eyebrow)."; a missing
upper entry returns silently. The same tables are packed for the GPU as the shader constant `gpu_morph_mapping`
(0x209360), logged once per skin at load. Nothing decompiled reads an upper mesh's own channels. Where the index
table comes from (the rest proximity of the two meshes at load, or the Kit) is UNVERIFIED.

**So an upper mesh fits when its rest shape fits** the head it is built for:

- Author the hair or beard on the exact LOD0 `face_base_mesh` of the skin's `face_meta_mesh`, in its rest pose, and
  weight it to the same `head` bone. The roots sit on the skin; each vertex follows the head vertex it maps to, so a
  strand modelled clear of the face stays clear of it at every slider value.
- Give it no morph channels, as vanilla does.
- A beard shared by several heads fits only the head it was modelled on; each head needs its own fitted copy.

**LOTRLOME's own upper meshes differ from vanilla.** The dwarf beards in `Race Test/Beards/SK_Dwarf_Beards_geo.tpac`
carry 101 channels (`beard_a_03` 202, `beard_a_02` none), and `sk_dwarf_beard_a_01`'s channels copy the dwarf head's
motion at its roots (channel 46: head 5.58 mm, beard 5.61 mm; mean error under 1.6 mm on the top ten channels).
Saruman's hair and beard came with 101 channels, zero on the head's largest ones (46: scalp 42 mm, hair 0). Whether
the engine reads those channels at all is unproven; vanilla does not need them (Mike, 2026-09-28: follow vanilla).
A tool that fitted them (`fit_hair_morphs.py`, 2026-09-28) was written, applied to Saruman's FBX and removed the
same day for that reason. Saruman's hair and beard were then stripped to vanilla's shape (no channels) with
`tools/blender/strip_upper_mesh_channels.py` (2026-09-28 20:05; backups `.bak-stripupper`, the fitted file, and
`.bak-hairfollow`, the original); a Kit re-import and an in-game look are owed.

**An upper mesh with no weights does not move with the head.** `SK_Dwarf_Beard_A_12` shipped with all 15,344
vertices unweighted, while beards 01 to 11 are weighted to `head` and `neck` (the long ones `spine1` and `spine2`
too). Its rest shape fitted the male head like its siblings, so only the weights were missing.
`tools/blender/transfer_upper_mesh_weights.py` copied weights to it and its five LODs from beards 01 to 11 pooled
(2026-10-02), so it keeps the artist's head-to-chest fade; a copy from the head mesh alone was tried first and
replaced, because nothing of the head lies near a braid and the fade then follows distance from the neck skin.
Beard 12 sits a median 0.07 mm from sibling strands. Per height band it now reads head 97% above z 1.25, head 83%
from 1.15, head 66% from 1.05 and spine 61% below, between beards 07 and 09. Backup
`SK_Dwarf_Beards.fbx.bak-upperweights`. Mike re-imported and saved it in the Kit the same day; TpacTool on
`SK_Dwarf_Beards_geo.tpac` then showed every vertex of every LOD weighted, dominant bone 13 (`head`), the same as
beard 11. An in-game look is owed. A beard that "floats" or stays put while the head turns: check its weights before
its morphs.

## Eye colour

A skin's `eye_color_gradient_points` are the eye slider's stops, in document order; a character stores its eye
colour as a 0 to 1 position along them (`FaceGenerationParams.CurrentEyeColorOffset`) [Certain]. Appending stops to
a race therefore most likely moves every existing character's colour of that race (the sampler is not decompiled),
so a new colour goes on a race no existing character depends on: Sauron's gold and red went on `sauron`, not `elf`
(`tools/oneoff/add_sauron_eye_colours.py`, 2026-09-28). The engine holds at most **32** stops per skin in a fixed
array and does not clamp the count (the skin parser 0x577410, v1.5.3), so a gradient never grows past
32. The skins XSD wants each stop unique; the engine does not check.

## Tools

| Tool | Does |
|---|---|
| `tools/blender/add_face_morph_channels.py` | 101 zero-offset channels on a head's LOD0 parts; stops the null-buffer crash |
| `tools/blender/fit_eye_morphs.py` (math: `eye_follow.py`) | Eye channels that follow their sockets; validated on the male dwarf's authored eyes, mean error 0.97 mm |
| `tools/blender/transfer_hand_morphs.py` | 26 hand-pose channels fitted from a reference hand |
| `tools/check_race_morph_channels.py` | Reinstall gate on the FBX sources: exact channel counts |
| `tools/check_eye_follow.py` (export: `export_face_morphs.ps1`) | Gate on the compiled package: no eye left behind |
| `tools/oneoff/restore_adult_woman_dwarf.py` | The female dwarf's skin restore; its dry run prints the live state |
| `tools/blender/strip_upper_mesh_channels.py` | Removes every morph channel from named hair and beard meshes and their LODs, the vanilla shape; refuses face parts |
| `tools/blender/transfer_upper_mesh_weights.py` | Weights an unweighted hair or beard mesh and its LODs from its finished siblings or a head; refuses one that already has weights |
| `tools/oneoff/tune_face_slider_reach.py` | Scales a race head's slider ranges to the male dwarf's reach in millimetres; `--zero` pins a key at 0; `--check` is the reinstall gate |
| `tools/oneoff/add_sauron_eye_colours.py` | Gold and red stops on the `sauron` race's eye slider; `--check` exits 1 when a skin lacks them |

## What the 2026-09-26 investigation ruled out

The female dwarf's eyes showed skin in game and looked right in the Kit. Two structural differences from the
working male head looked like causes and were not; each was tested against a case that separates it.

- **Sub-mesh order.** Her head and Saruman's store the eye first. The decompiled builder picks parts by tag, and
  Saruman's authored eyes follow his sockets, so order is not a cause.
- **An empty second material on the eye.** Hers is empty and the male's is set, but the elf, uruk and pale uruk eyes
  are empty too and draw correctly.
- **The cause, measured on the compiled package:** her eye's 101 channels were all zero while her sockets moved up
  to 8.5 mm. After `fit_eye_morphs.py` and a Kit re-import, her eyeball moves 7.9 mm on channel 14, and in game the
  eyes sit in their sockets (Mike, 2026-09-27).

The method that found it: compare the broken asset with a working one of the same kind, then refuse each difference
as a cause until the code that consumes it, or a third asset that has the difference without the fault, says so.

## Open questions

- Whether channels 64 to 100 carry the facial animation on 1.5.3 (yotthani's finding on the 1.4 line; the key to
  channel map above is settled).
- Whether the Kit or the engine applies `DeformPercent`.
- The male eye's eyeball-only channels (60 to 63 and 15, iris and gaze) move the eye without the socket; a socket fit
  cannot reproduce them, so a fitted eye does not respond to those sliders. Native's `skins.xml` names 15
  `eye_shape` and 60 to 63 weight, build, height and age, so "iris and gaze" is an inference to recheck.
- Whether vanilla hand meshes carry the 26 channels, and which Kit panel sets the face tags.
- Where the upper-mesh index table comes from (rest proximity at load, or the Kit), and whether LOTRLOME's
  channels on the dwarf beards change anything in game.

## Sources

[#385](https://github.com/haterade22/TAOM/issues/385) (the dwarf eye, the `0x24C` decode),
[#684](https://github.com/haterade22/TAOM/issues/684) (the hill troll face crash);
[troll-race.md](../features/troll-race.md) "Hand pose morphs" and the face-morph entries;
[animation-skeleton.md](../reviews/lessons/animation-skeleton.md) (the lessons); the community guide's
[race page](../community/bannerlordmodding-lt/guides/custom_creature_race.md) "Face and hand morph channels";
[head-mesh-and-groom-authoring.md](head-mesh-and-groom-authoring.md) (yotthani's FaceLearner: head packages without
the Kit, the neck seam, the head material, groom on a baked head).
