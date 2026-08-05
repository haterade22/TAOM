# Erebor dwarven kitbash catalog

Reference for the erebor dwarven kit under
`Modules/TAOM_Map/AssetSources/Scenes/erebor/` and its packaged form at
`Modules/TAOM_Map/Assets/Scenes/erebor/`.

## Files in this catalog

| File | What's in it |
|---|---|
| [textures.md](textures.md) | Raw `t_dw_*` textures (diffuse/normal/specular/height maps) |
| [materials.md](materials.md) | `m_dw_*` materials — what textures each one binds and what it's used for |
| [meshes.md](meshes.md) | `sm_dw_*` meshes — every piece with shape, intended use, and family membership |
| [design-patterns.md](design-patterns.md) | Composition rules, confirmed calibration data, and ready-to-use building templates |
| [runes.md](runes.md) | Dwarven rune pieces — carved wall/trim meshes and their texture pipeline |

## Quick-reference: the A1 / B1 / C1 system

The letter suffix on any mesh (`_a1`, `_b1`, `_c1`) is a **material family**.
The digit is either a shape variant (`_win_a1` / `_a2` / `_a3` = three window
shapes in A texture) or just an identifier when only one shape exists
(`_door_a1` is the only A-family door).

- `_a*` — primary dwarven stonework (clean grey, used on most Erebor builds)
- `_b*` — weathered brick (rougher, good for older or ruined structures)
- `_c*` — tertiary variant

**Within a single building, stick to one letter** for visual consistency.
Mixing across letters produces visible texture breaks mid-wall.

## Kit scope — what's in it

| Family | Count | Notes |
|---|---|---|
| Ground tiles + trim | ~10 | Floor stonework with raised perimeter trim |
| Pillars | 4 | Decorative columns, different heights and ornamentation |
| Wall beams / columns / capitals | ~12 | Decorative overlays for wall faces |
| Walls (3m modular) | ~30 | Plain / corner / door / window variants in A, B, C textures |
| Platform (6m) | ~14 | Raised floors with corners, extensions, middles, stairs |
| Roof | ~30 | Pitched roof system — panels, ridges, side edges, inside/outside corners |
| Castle tower | ~11 | Defensive tower with crenellations |
| Castle wall (straight / gate / ramp / stairs) | ~47 | Fortification wall system with merlon toppers |
| Decoration | 1 | Pile of gold (Smaug's hoard) |
| Nature (cliffs) | ~24 | Shared nature pieces added for mountainside carving |
| **Total** | **~164** | Matches `taom_erebor_kitbash.xml` |

## Related files

- Kitbash prefab: `Modules/TAOM_Map/Prefabs/taom_erebor_kitbash.xml` (164 entries)
- Source FBX: `Modules/TAOM_Map/AssetSources/Scenes/erebor/{meshes,walls}/*.fbx`
- Packaged meshes: `Modules/TAOM_Map/Assets/Scenes/erebor/{meshes,walls}/*_geo.tpac`
- Build tool: `tools/build_erebor_kitbash.py`
- FBX mesh lister: `tools/list_fbx_objects_all.py`
