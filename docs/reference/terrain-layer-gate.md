# Terrain layer gate

`python tools/check_terrain_layers.py` reads the terrain of every `scene.xscene` in the live `TAOM` and
`TAOM_Map` modules and fails when a terrain node uses only flora layers, or no layer at all, in a season.

## The fault

Each `<node>` in a scene's `<terrain>` carries `layer_is_used_mask_summer`, `_fall`, `_winter` and `_spring`.
The mask is a bit set over the `<layers>` list in file order (bit 0 is the first layer). Each `<layer>` has one
child per season, and `is_flora_layer` on that child says whether the layer is a flora layer in that season.

A node whose mask holds flora layers and nothing else makes the engine build a terrain shader variant with no
layers. `pbr_terrain_gbuffer.rs` starts the world normal at `float3(0,0,0)` and adds to it only inside the loop
over used layers (`terrain_pixel_functions.rsh:613` and `:807`). With no layer in the compiled list the compiler
folds the normal to a constant 0, `normalize()` at line 818 becomes 0/0, and the compile ends in
`Error compiling shader file pbr_terrain_gbuffer.rs X4008`. Retry fails the same way, so nothing is cached and
the dialog returns on every load. Cancel lets the mission go on.

Read from the data and the shader source, not from the native code: that the engine drops flora layers when it
builds the layer list is not proven (no decompile on the machine that found it). The repair test is in the
findings of 2026-11-28 under `output/taom-mod/helms-deep-shader/`: after the fix, a second load of the scene
compiles no `pbr_terrain_gbuffer.rs ... 9, 0, 0, 1` line in the rgl log and shows no dialog.

## Helm's Deep

`taom_rohan_castle_helms_deep_forceatmo`, node idx 0, idy 3 (the 150 m square x 0-150, y 450-600): mask 1 in
all four seasons, layer 0 `default`, a flora layer in summer. Every other node of the scene uses a non-flora
layer too. Atmosphere persistence is not the cause, and neither is the vista (no TAOM_Map scene except
`Main_map` sets a vista texture; see [atmosphere-persistence](../features/atmosphere-persistence.md)).

Fix, in the Modding Kit by the scene owner: paint a little of any non-flora layer (for example `Base_Layer`) on
node (0,3) and save the scene. TAOM_Map is live and unversioned, so that fix reverts on reinstall; this gate
is what keeps a later copy from bringing the fault back.

## The gate

- Seasons are judged one by one: a layer that is flora in summer only fails only a summer mask.
- `KNOWN_FAULTS` in the tool lists faults tolerated while a fix is owed, each with its reason. Today: Helm's Deep
  node (0,3), and the fall entry of `taom_dwarves_battle_001_forceatmo` (15 nodes, mask 1, layer 0
  `rock_cliff` flagged flora in fall only; whether the fall variant compiles at load is unverified). Remove an
  entry when its scene is fixed. `--no-known` reports them as faults.
- Known entries are keyed on scene, node, season and mask; one that matches nothing in a scene that was read is
  printed as `STALE known fault ...: remove it` (exit code unchanged).
- Exit 0 clean, 1 a fault, 2 NOT RUN (no install, no scene, a file that is not a `scene.xscene`, does not
  parse, or a terrain with nodes but no mask), 3 crash. Each NOT RUN reason is an `ERROR:` line. One bad file
  does not hide the faults in the others: a fault wins with exit 1. The summary counts scenes, nodes and masks.
- `tools/data_gates.py` runs it as gate `terrain-layers` for a changed or named `scene.xscene`.
- Tests: `tools/tests/test_check_terrain_layers.py`.

## Open question

Which season's mask and flags the engine's shader generator reads when it builds the terrain variant is
unverified: that code is native and Ghidra is not set up here. The tool judges each season with its own flags.
The dwarves battle 001 case (layer 0 flora in fall only) exists only under that reading; if the generator reads
one fixed season, a fall-only flag is harmless and those 15 entries can go. To settle it: note the campaign
season of a Helm's Deep load that showed the popup (layer 0 is flora in summer only there, so a popup in
another season would point to a fixed season), or decompile `rglTerrain_shader_generator` with the native decompile tool.
