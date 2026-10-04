# The engine's shader caches and TAOM's startup compiles, read from v1.5.3, 2026-10-03

Written for: Mike and the next reader of FOR-MIKE item 1. Sources: `TaleWorlds.Native.dll` v1.5.3
(14,209,376 bytes) through `python tools/native_decompile.py`; the runtime caches under
`C:\ProgramData\Mount and Blade II Bannerlord\Shaders`; the module and release folders; the engine's rgl
logs of 2026-10-02. Scripts beside this file: `shader_variants.py`, `shader_combos.py`,
`sack_mapping_compare.py`, `compile_timeline.py`; the decompiled functions are in
`../native/shader-cache/`.

## 1. When the runtime shader cache is dropped (verified)

The engine keeps the shaders it compiles at run time in `Shaders\CoreShaders\D3D11\*.sacx`, indexed by
`shader_mapping.bin`. At startup `FUN_1801d91a0` (RVA `0x1D91A0`, the only code that references "Mod
change detected") reads that file: a format word (`0x783`), the build's changeset string, then, for this
cache, a string it compares with one built for the session. A difference shows or logs "A change in your
active mods was detected. Deleting the runtime shader cache to avoid rendering issues." and the cache is
invalid; a different changeset invalidates it too ("Shader cache version is old").

After the last session of 2026-10-02 (14:56) the file holds the changeset `122516` and
`CustomBattle;Native;SandBoxCore;Sandbox;StoryMode;TAOM;TAOM.Dependencies`: the active module ids,
sorted, with no versions. So the cache is dropped when the set of active module ids changes or the game
build changes, and not by a TAOM update that keeps the same modules. On disk it now holds only the 6
shaders that 14:56 session compiled; the 1,335 compiled at 13:16 were deleted at the list change.

What a player pays: about half a minute (Mike's machine: 32.5 to 34.3 s) at the first start after an
install, after a game update, and after any session on another module list (vanilla or another mod). A
player who plays only TAOM pays it once per install or game update. The 2026-10-02 sessions changed the
list four times (12:48 without the Armory, 12:49 another mod set, 13:16 back, 14:56 without the Armory or
the map).

The terrain caches (`Shaders\TerrainShaders\<module>\<scene>\D3D11`) are separate: TAOM_Map's holds 497
files (20 MB), 98 of them Main_map's, dated 2026-09-28 and 2026-09-30, and they survived those list
changes (the module check runs only for the null-argument case of `FUN_1801d91a0`).

## 2. What the 1,335 startup compiles are (verified from the logs)

The log line is `compile_shader: <source>, <entry>, <profile>, <int>, <int>, <int>, <int>.` (format string
at `0xAE9F60`). The four integers, read from their value patterns since the decompiler lost the variadic
arguments: an input layout (3 or 13), the system flags, and the material flags as two 32-bit halves (the
engine's own debug print is `Material flags : 0x%x%x`). For both cold starts (12:40, 13:16):

| Pass | Compiles (vertex plus pixel) |
|---|---|
| `pbr_metallic` (forward) | 768 |
| `pbr_metallic_gbuffer` | 288 |
| `pbr_metallic_constant_output` | 160 |
| `pbr_metallic_shadowmap` | 80 |
| `pbr_metallic_pointlight` | 39 |

- **40 material flag combinations**: five base types (`0x00400080`, `0x00400090`, `0x00401080`,
  `0x00481085`, `0x004c1084`), each with bit 1, bit 14 and the high half's bit 0 both off and on.
- **The cost sits in one type.** The eight `0x...90` combinations (bit 4 set) are compiled in 104 system
  contexts each: 888 of the 1,335 compiles. The other 32 combinations get 8 to 13 contexts each.
- **The system contexts are engine state, not content.** The system flag names come from a static table
  of 32 names in bit order (`FUN_1800120a0`; a second copy at `FUN_1800145d0` keeps an empty name in slot
  11, an unused bit). The bits that vary across the compiles: 3 blood, 4 rain, 5 snow, 10 high-quality
  shaders, 13 SSR, 20 smooth fade-out, 22 two-sided render, 23 motion vectors, 29 custom clipping, 30
  dynamic instancing, 31 PRT ambient. No normal-map format bit (16 to 19, DXT5 and BC5) is set.
- Not decoded: which `pbr_metallic` material option each varying material bit stands for.
- Not identified: what requests these variants at startup. It is not the engine's compile-all path
  (`FUN_1801dff30`, which asks the `module.get_item_mesh_names` console command for every crafting
  piece's mesh and then logs "Starting Shader compilation."): that line is in none of the 2026-10-02 logs.

## 3. Where compiles happen in play (verified from the logs; Mike's machine, warm cache)

The 12:40 and 13:16 sessions compiled only in their first minute. The 14:30 session compiled 46 at
14:34:38 to 14:34:44, while the custom battle menu rendered lord previews (`[TableauDiag]` lines), not in
the battle; the battle that followed (from 14:35:15) logged 544 ms and 1,091 ms frames with no compile
line in the log. So, on this machine, shader compiles are not behind the measured battle hitches. With
a cold cache, a variant the startup pass did not compile is compiled on first use; whether that blocks
the frame depends on a path the decompile shows both ways (queued to a job, or compiled in place).

## 4. Module sacks: read, but they did not serve these variants (partly verified)

The dev install carries Kit-written module sacks: `LOTRLOME_Armory\Shaders\D3D11` (sack 108.8 MB,
written 2026-10-02 12:33, its `shader_compile_report.log` 7.9 MB the same minute, `shader_mapping.bin`
14.2 MB from 2026-09-26), `TAOM_Map` (57.8 MB) and `TAOM` (an empty 36-byte sack). At 13:16 the engine
logged four `read_compressed_shader_cache_package` reads (0.144, 0.0015, 0.0003 and 0.00003 s); their
order and durations match the index sizes in the four sacks' first words (vanilla's 19.9 MB, the
Armory's 487 KB, TAOM_Map's 157 KB, TAOM's 32 bytes), so the module sacks are most likely read
(inferred from timing, not traced). Yet all 1,335 compiled variants, matched on input layout, system
flags and material flags, are listed in the Armory's `shader_mapping.bin` (505,968 records of 28 bytes).
So the Armory sack written at 12:33 did not serve them; why (an index that no longer matches its sack,
or a lookup key that also mixes in a per-shader value from the engine, `FUN_1801ef4b0`) is not settled.

The sack reader (`FUN_1801ef6d0`) stops the game with "Application crashed because a fatal error
occurred while reading a file. Common cause of this is a corrupted game file." on six different read
failures. A damaged or truncated sack crashes at startup rather than being skipped, which fits the
experience that shipped sacks are problematic.

## 5. What the release channels ship

All three channels carry the Kit's `shader_mapping.bin` and `shader_compile_report.log` for the Armory
(14.2 MB and 7.4 to 7.9 MB) and TAOM_Map (3.2 MB and 1.6 to 1.7 MB), about 26 MB, and no sack. The
report is a text listing (MetaMesh, Material, Shader, Variants). In the code traced, a mapping belongs
to a cache that one loader (`FUN_1801d6e30`) opens together with that cache's sack, and the loader's
paths fit the runtime and per-scene caches; whether it ever runs on a module folder is not established.
So with no sack these files most likely do nothing in the game, which is not proven.

## 6. The campaign map's terrain step (a correction)

At the 12:45 campaign load the step from `rglTerrain_shader_generator::clear` to the next line took
2.97 s and logged 138 "Missing shader from sack" lines for `pbr_terrain` variants, but no compile: the
variants came from the terrain cache in section 1. The 2.97 s covers loading those cached shaders and the
terrain setup of a map far larger than a battle scene (one with its own sack took 0.14 s); the split is
unmeasured, so "a Main_map sack would save about 3 s" is an upper bound, not a measurement.

## Next measurements

1. One cold start (empty `CoreShaders` folder) with the Armory's module sack, mapping and report moved
   out of the module, against one with them in place: the compile count shows whether the module sack
   serves anything, and whether the two Kit files matter without a sack.
2. Decode the sack index (the first word is its size) to see whether the 12:33 Armory sack holds the
   1,335 keys; if it does, the miss is in the lookup key.
