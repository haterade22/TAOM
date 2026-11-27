# Mirkwood kit catalog

The existing Mirkwood kit is a set of stone arcades, columns, bridges, root-supported walks,
walls, openings, roofs, stairs and dressing. The installed FBX holds 136 LOD0 meshes and 135
collision objects. All 136 mesh names occur in the installed geometry package and prefab palette.

| File | What it covers |
| --- | --- |
| [Delivery](delivery.md) | Blender master, library, verified stages, and commands to repeat the checks |
| [Meshes](meshes.md) | Every LOD0 mesh, dimensions, pivot, source LOD triangles, body and materials |
| [Materials](materials.md) | Source PNG channels, the material names that use them and the diffuse maps used on the sheet |
| [Coverage](coverage.md) | Prefab and scene placements, settlement scenes, and gaps for a town, castle and village |
| [Reference study](reference-study.md) | Architectural observations and candidate controls to discuss during the first visual review |
| [Design language](design-language.md) | Stone and timber concept, material colours and proposed trim atlases |
| [Textures](textures.md) | Stone and heartwood maps, trim atlases and Blender checks |
| [Architectural trim](trim.md) | Six limestone and six heartwood bands, application and checks |
| [Siege controls](siege-controls.md) | Existing gate and defender equipment clearances |
| [Vaulted hall](hall.md) | Widening vestibule, repeat hall, dais and exterior cover |
| [Settlement buildings](settlement.md) | Town and village pieces, routed compounds and visual staging |
| [Ravine approach](approach.md) | Reference-led bridge, stone screen, repeated timber gallery and measured layouts |
| [Siege kit](siege-kit.md) | Shared castle and town modules, assembly checks and review files |
| [Distance and walk studies](distance-studies.md) | Measured visual levels and simpler walk meshes |
| [Lessons](lessons.md) | Checks to carry into the next kit and the next Mirkwood round |

## Source and frame

Read on 2026-11-26 from the v1.5.7 desktop installation. The client and editor each report the
same version. The catalog uses files beneath `Modules/TAOM_Map`.

| Input | SHA-256 |
| --- | --- |
| `AssetSources/Scenes/Mirkwood/kit_mirkwood_mod_env.fbx` | `a9bf533e9dc6166aa2a1f1e7728f2a8e18868ca29705f78d21d99878b196c3f1` |
| `Assets/Scenes/Mirkwood/kit_mirkwood_mod_env_geo.tpac` | `8b0e1a7957e304d372c437d3a9b675af72c6d40a0cb7f8a3d407d16c079bbdec` |
| `Prefabs/taom_mirkwood_kitbash.xml` | `9cf77f19842c183dd75b50a8b11f5e681b31f8a646e9820a1f12a182e519ffc0` |
| `ModuleData/settlements.xml` | `d029fddc09856154a7b4a1fedd4a0ad52241663ba1514cfd39b87ba4a9c868c7` |

The FBX is 22,928,348 bytes. Blender 5.3.0 and the repo's binary FBX reader agree on the
760 mesh object names and every triangle count. The objects comprise 625 visual LODs and
135 bodies. The cooked package has 136 metameshes and 135 bodies. The assets folder has
117 TPACs including the geometry package.

**Blender to Kit frame.** Turn the Blender import 180 degrees about Z. For placement review
in Blender, convert an authored Kit position `(x,y,z)` to `(-x,-y,z)` and keep the Z rotation.
The union of each cooked LOD0's material-split bounds agrees with the rotated Blender FBX
bounds to 0.5 mm on all 136 meshes using coordinates rounded to 1 mm. The identity frame
agrees within 1 mm on 115. The wall lamp and an asymmetric eave extension distinguish the
frames. A symmetrical wall alone can pass either test.

**Provenance.** The Mirkwood source FBX and its cook are verified. The repo's provenance register
and its UE conversion guide do not establish this kit's upstream source or license. The FBX
material split and naming support cataloging its current construction. They do not establish
where the shapes came from or what can be redistributed. Keep the source artifacts local
and obtain provenance before authoring from or redistributing a held asset.

## Local evidence

`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/kit_full/baseline_01` holds
`geometry.json`, `installed.json`, `derived.json`, the fixed-camera sheets and the Blender view.
The scripts in the Mirkwood `_tools` folder read the installed module and write to this baseline
folder. The Blender jobs used the shared `blender_one.py` lock. The same work folder holds the
locally stored references and a hash-checked manifest. [Build record](../mirkwood-kit.md).
