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
to `_250`, `Basis_0` to `Yell_100`), so the engine takes them by order. How the 63 `deform_key` sliders in
`skins.xml` map onto the 101 channels is UNVERIFIED.

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

A lord's `<face>` needs the exact `<BodyProperties version="4" ... key="..."/>` string of a face built in the
game's face editor, and v1.5.3 has no way to get it out: the face editor has no copy or export action (no
clipboard code in `FaceGenVM`), and vanilla ships no console command for it.

1. Build the face in character creation, or in the in-campaign face editor.
2. Open the console (<kbd>Alt</kbd>+<kbd>~</kbd>) and run `taom.print_face` for the player hero, or
   `taom.print_face <hero_id>` for any other hero already in the campaign.
3. The console echoes the report, but the line that matters is hard to select there. Read it instead from the TAOM
   debug log, `Logs\taom_debug_<timestamp>.log` under the Bannerlord install directory, tagged `[PrintFace]`.
4. Copy the `<BodyProperties .../>` line verbatim into the lord's `<face>` in `heroes.xml` or `lords.xml`.

## Tools

| Tool | Does |
|---|---|
| `tools/blender/add_face_morph_channels.py` | 101 zero-offset channels on a head's LOD0 parts; stops the null-buffer crash |
| `tools/blender/fit_eye_morphs.py` (math: `eye_follow.py`) | Eye channels that follow their sockets; validated on the male dwarf's authored eyes, mean error 0.97 mm |
| `tools/blender/transfer_hand_morphs.py` | 26 hand-pose channels fitted from a reference hand |
| `tools/check_race_morph_channels.py` | Reinstall gate on the FBX sources: exact channel counts |
| `tools/check_eye_follow.py` (export: `export_face_morphs.ps1`) | Gate on the compiled package: no eye left behind |
| `tools/oneoff/restore_adult_woman_dwarf.py` | The female dwarf's skin restore; its dry run prints the live state |

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

- How the `skins.xml` `deform_key` sliders map onto the 101 channels.
- Whether the Kit or the engine applies `DeformPercent`.
- The male eye's eyeball-only channels (60 to 63 and 15, iris and gaze) move the eye without the socket; a socket fit
  cannot reproduce them, so a fitted eye does not respond to those sliders.
- Whether vanilla hand meshes carry the 26 channels, and which Kit panel sets the face tags.

## Sources

[#385](https://github.com/haterade22/TAOM/issues/385) (the dwarf eye, the `0x24C` decode),
[#684](https://github.com/haterade22/TAOM/issues/684) (the hill troll face crash);
[troll-race.md](../features/troll-race.md) "Hand pose morphs" and the face-morph entries;
[animation-skeleton.md](../reviews/lessons/animation-skeleton.md) (the lessons); the community guide's
[race page](../community/bannerlordmodding-lt/guides/custom_creature_race.md) "Face and hand morph channels".
