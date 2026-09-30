# Handoff: why custom hair, beards and eyebrows do not follow the face (2026-09-29)

For the next session picking up this investigation. It opens with a start prompt, then the state of the work,
what is proven, what is ruled out, the tools already built, and the next steps. Engine: Bannerlord v1.5.3,
`bin\Win64_Shipping_Client\TaleWorlds.Native.dll`. Background: [race-face-and-hand-morphs.md](../reference/race-face-and-hand-morphs.md).

## Start prompt for the new session

> Continue the upper-mesh mapping investigation in `docs/investigations/upper-mesh-mapping-2026-09-29.md`. Read it
> first, then the "Hair, beards and eyebrows" section of `docs/reference/race-face-and-hand-morphs.md`. The adult
> female dwarf's eyebrows (and likely Saruman's hair and beard) do not move with the face sliders in the face
> editor, while vanilla's do. Four hypotheses are refuted and the engine's table builder is found; the open lead is
> the skinning difference. Commit the uncommitted work listed in the doc first if Mike asks (stage only the
> listed hunks; another session has many files open). Use /investigate; the 3-strike rule was reached once already.

## The symptom

- Adult female dwarf (live `LOTRLOME_Armory/ModuleData/skins.xml`, skin with `face_meta_mesh="sk_dwarf_bm_f1_head"`,
  eyebrow meshes `sk_dwarf_bm_f1_eyebrow_01..05`). In the face editor the brows stay put while the skin moves: they
  are hidden by default and show only when `temple_width`, `eye_socket_size` and `eye_position` are reduced. The
  brow sliders do not move them. With `eye_position` at its maximum a dark piece appears under her left eye. Mike's
  screenshots: session images 8 to 13 (2026-09-29).
- Saruman's hair and beard floated off his scalp the same way (2026-09-28). His channels were stripped to 0 the
  same way as the brows; whether they follow now is NOT yet checked in game.

## Proven (with the evidence)

| Fact | Evidence |
|---|---|
| Vanilla upper meshes carry 0 morph channels | TpacTool on `Native/EmAssetPackages/pack3/pack3.tpac` (98 beard and hair metameshes) and `pack_temp_29/pack_temp_29.tpac` (`female_eyebrow_*`, `male_eyebrow_*`) |
| The engine builds a per-vertex table for every upper mesh at load | `produce_vertex_map_from_mesh`, function 0x56F360: for each upper vertex, the nearest vertex (squared distance) of the head metamesh's largest sub-mesh; error text "could not find corresponding vertices correctly ... (check skinning properties)" when the nearest is over 1.0 (squared, so 1 m), and it still uses it |
| Before the search it compares a flag bit on both meshes | bit 25 of the dword at mesh data (`+0x1B8`) `+0x390`; when they differ, the upper positions go through the head bone's matrix first (0x56F66C to 0x56F6C7) |
| Live trace at load | 72,084 builds before the main menu, **0 failures**, the first 400 all with flags agreeing (`00 00`); no new builds when the face editor opened (cdb, `E:\dbg\vmap_console.txt`) |
| The table is packed for the GPU | registry object in the global at `TaleWorlds_Native+0xDA12F8`, dirty flag at `+0xE9`; inserts from 0x56FED4 (inside 0x56F360, 45 at load) and 0x56CD67 (inside 0x56C590, the skin loader with "Body mesh name fetched", 19); packer 0x209360 logs `gpu_morph_mapping` (cdb, `E:\dbg\builder_console.txt`) |
| The CPU mapper 0x56EBA0 is NOT what the face editor uses | a breakpoint on it logged zero calls in the editor (`...scratchpad\brow_dbg\brow_trace.log`); it is probably the baked path for agents in missions |
| The dwarf brows would map exactly under nearest-vertex | every brow vertex sits 0.7 mm off a head vertex, and the nearest head vertex's channel motion equals the brow's own authored channels to 0.00 mm on all 101 channels |
| The brows use vanilla's own eyebrow material | both `female_eyebrow_2` and `sk_dwarf_bm_f1_eyebrow_01` reference material `aea6a6ee-1296-47f5-a559-b0f7a3006a1b` |

## Ruled out

1. **Nearest-vertex error buries the thin brow.** Refuted: the nearest head vertex gives the exact motion (0.00 mm).
2. **Own channels block the mapping (or are applied twice).** Refuted: the brows were stripped to 0 channels (vanilla
   shape), re-imported in the Kit (package 09:46, 0 morph frames verified) and nothing changed in game.
3. **Our Blender re-exports reordered the head's vertices.** Refuted: all 4,791 base vertices sit at the same index in
   the August package (read from the lotraom-assets mirror history, LFS object `51d554ad...`) and today's.
4. **A different material or shader.** Refuted: same material GUID as vanilla.
5. **Per-vertex skinning.** Refuted (2026-09-29, second session): every vanilla eyebrow (17 in `pack_temp_29`) and
   all five dwarf brows are skinned 100% to bone 13 with one influence per vertex (TpacTool: the vanilla vertex
   stream, and LOTRLOME's `MeshEditData.Bones` because its packages carry no stream). Scratch script
   `skin_dump.ps1` in session scratchpad `41fb92d2-...`.

## Open lead: skinning

The one remaining difference between our upper meshes and vanilla's is TpacTool's `UnknownInt2`: **28 on every
LOTRLOME mesh** (heads, brows, beards, Saruman's hair) against 2 (vanilla eyebrows), 4 (a vanilla beard) and 0 (a
vanilla hair). The builder's own error text says "check skinning properties". Vanilla hair `hair_male_c_b` has
`SkinDataSize=0` (unskinned) while vanilla eyebrows are skinned, so both kinds exist in vanilla. Vanilla's head
package (`Native/EmAssetPackages/head_male/head_male.tpac`) does not open in TpacTool 0.4.0 ("capacity was less than
the current size"), so a vanilla head cannot be compared yet.

**Refined (second session).** On vanilla `UnknownInt2` is the used-bone count plus one: 2 on every one-bone
eyebrow and on the one-bone beard `beards_c_k`, 4 on the three-bone `beards_c_a`, 0 on the unskinned hair. On
LOTRLOME it is 28 whatever the mesh uses (the one-bone brows, eye and mouth, the seven-bone head), so it most
likely counts the FBX's whole armature, clusters with zero weight included. That is the only difference left
in the brow asset. No LOTRLOME upper mesh with 0 channels is known to follow the face (every dwarf hair carries
101 channels and 28), so no third asset separates it yet. Note too that the dwarf skin uses `dwarf_skeleton_a`,
not `human_skeleton`.

**Second session, debugger results (`E:\dbg\names.cdb`, log `E:\dbg\names_trace.log`).** The builder takes
`param_1` = head metamesh (it picks the sub-mesh with the most vertices, `+0x1F8`), `param_3` = upper mesh,
`param_2` = owner; tables are cached by the GUID pair (`+0xB8`, `FUN_1802087c0`) and attached per owner by upper
GUID (`FUN_1802232c0`). At load, `sk_dwarf_bm_f1_eyebrow_02..05` each got a sane 104-entry table against the face
base `sk_dwarf_bm_f1_head` (indices 0x14D to 0xD0C, head bone 13, flag bits agreeing). Yet in the face editor
**all five brow styles stay still** (Mike, 2026-09-29), so the table is not the fault: the consumer is.
Side finding: the male dwarf's beards pair with `sm_dwarf_basemesh_a1_head.mouth`, because his mouth (1,615
vertices) outnumbers his face base (1,404).

**The one head-side difference.** The dword at `mesh+0x1B8 → +0x390` is the material's vertex layout: vanilla
`eyebrow_mat` (bumpmap, skinning) is `03000000`, vanilla `head_female_a` (bumpmap, skinning, doubleuv) is
`0B000000`. Every LOTRLOME head reads `03000000` or `83000000`: bit 27, **doubleuv**, is missing, and no custom
head (female dwarf, male dwarf, Saruman) carries a second UV set. Hypothesis to test: the upper-mesh GPU morph
needs a doubleuv head. No native code was found testing that bit directly, so this is UNVERIFIED.

**The swap test (done, inconclusive).** `brow_swap.py` (session scratchpad `41fb92d2-...`) points all
five `eyebrow_meshes` slots of the adult female dwarf skin in the live `skins.xml` at vanilla `female_eyebrow_2`
(`--apply` backs up to `skins.xml.bak-browswap-<time>`, `--restore` puts it back). If the vanilla brow follows
the dwarf head's sliders, the fault is in the dwarf brow asset (then `UnknownInt2`); if it stays still too, the
fault is on the head or skeleton side (then name the builds with the debugger, step 3 below). Result: the
vanilla brow floated about half a metre above the dwarf's face (it keeps a human rest height), so its nearest
head vertices were on the crown, which the brow sliders do not move; the test could not separate. The live
`skins.xml` is restored.

## Next steps

1. Decode `UnknownInt2` (TpacTool source: `...\scratchpad\builder_research\tpaclib\TpacTool.Lib.decompiled.cs`) and
   compare the brows' bone weights and bone indices with the head's around the brows. Hypothesis to test: the GPU
   applies the head's deltas only to vertices skinned in a way the brows are not (for example the bone palette or
   the bone the vertices are weighted to).
2. Trace the GPU consumer of `gpu_morph_mapping`: which shader and which per-mesh data select a mesh for the upper
   mesh morph. A data breakpoint on the per-mesh table pointer set by `FUN_1802232c0` (called at 0x56FED4's
   neighbour, `FUN_1802232c0(owner, upper, table)`) would show which mesh objects keep a table.
3. Name the builds: the builder only prints names on a failure; a breakpoint at 0x56F66C with `s -a @rbx L0x400
   "eyebrow"` (and `@rdi`) would show whether the dwarf brows are built at all.
4. Check Saruman in game (hair and beard now with 0 channels): the same question on a second asset.

Fallback if the lead dies: take the eyebrow meshes off the adult female dwarf (the male dwarf, elves and Saruman
have none).

## Debugger notes (read before attaching again)

- cdb is inside the WinDbg package: `(Get-AppxPackage Microsoft.WinDbg).InstallLocation\amd64\cdb.exe`.
- Scripts live in `E:\dbg\` (short path, forward slashes in nested `$$><` paths; cdb mangles backslashes inside
  quoted command strings, and an apostrophe in a `$$` comment is a syntax error).
- **A cdb run from a script has no stdin: any unplanned stop (a failed command, an unfiltered exception) makes it
  quit and takes the game down.** Put `sxd av`, `sxd eh`, `sxd clr` first, and keep detach logic out of bp strings
  (`qd` inside an `.if` block failed with a syntax error and killed the game once).
- Visual Studio launching the game holds the debug port (Debug, Detach All), and so does TaleWorlds' Watchdog.
  `E:\dbg\launch_vmap.ps1` starts the game under cdb with the launcher's module list; deferred `bu
  TaleWorlds_Native+0x...` breakpoints resolve at DLL load.
- Scratch tools used today (session scratchpad `E:\Temp\claude\e--repos-TAOM\846fe3ce-...\scratchpad\`):
  `list_items.ps1`, `export_meshes.ps1` (`-Frames`), `mesh_flags.ps1`, `material_dump.ps1`, `export_obj.ps1`,
  `compare_upper_follow.py`, `brow_nearest_sim.py`, `dis_range.py`.

## Uncommitted work from this session (stage only these)

- `tools/oneoff/tune_face_slider_reach.py`, `tools/tests/test_tune_face_slider_reach.py` (9 tests pass).
- `Main/_Module/ModuleData/characters/lords.xml`: one line, Saruman's `<BodyProperties>` (Mike's latest key).
- `docs/reference/lotrlome-armory-snapshot/skins.xml` and `README.md` (the slider-reach APPLIED EDIT entry).
- `docs/reference/race-face-and-hand-morphs.md` (face key layout, slider reach).
- `tools/README.md`: only the `tune_face_slider_reach.py` row; the other added lines are another session's.
- This file.

Live Armory edits already applied (unversioned, backups beside each file): `skins.xml` slider reach
(`.bak-slider-reach-20260929-065619`, `-081426`, `-095157`); `AssetSources/Race Test/dwarf/sk_dwarf_bm_f_eyebrows.fbx`
channels stripped (`.bak-stripupper`, the March original); Saruman's FBX stripped earlier (`.bak-stripupper`).
Not yet written into the morphs doc: the builder and trace results above (fold them into its "Hair, beards and
eyebrows" section when the lead resolves).
