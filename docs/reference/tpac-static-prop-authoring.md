# Static props in a `.tpac` without the Modding Kit

How a static mesh (a map prop, a camp) is written straight into a `.tpac` package the game loads from a
module's `AssetPackages/` folder. Source: yotthani's MithrilForge (MIT, (c) yotthani), its `docs/donor-findings.md`,
README and `TpacPropWriter`, read in full at commit `91149e11` (2026-09-27); measured by its author on Bannerlord
1.4.6 (its name-check tests were updated for 1.5.3 on 2026-09-27). Review:
[adopt-mithrilforge-2026-09-29.md](../reviews/adopt-mithrilforge-2026-09-29.md). Lines marked **TAOM** were
measured here; everything else is MithrilForge's finding, restated, and has not been re-measured by TAOM.

## What TAOM has built this way, and why none of it renders

**TAOM:** the four packages in `Main/_Module/AssetPackages/` (`fieldcamp_camp_a`, `fieldcamp_palisade_ring`,
`refuge_camp_a`, `refuge_palisade_ring`) are MithrilForge output. Each holds five items, named `<mesh>_d`, `<mesh>_n`,
`<mesh>_s`, `<mesh>` (the Metamesh) and `<mesh>_mat`, which is the layout `TpacPropWriter.WriteMany` produces (read
2026-09-29). `CampLayoutBuilder.PlaceCenteredPrefab` (TAOM.Client) asks for them by name through
`MetaMesh.GetCopy(name, showErrors: false, mayReturnNull: true)`; on a miss the caller draws the vanilla siege-camp
layout, and nothing is logged beyond the entity count.

**No player and no dev install has ever loaded them** (measured 2026-09-29):
- **Releases** [Certain]: `E:\LOTRAOM_Releases\testing` and `\patreon` carry only the editor's
  `Modules/TAOM/AssetPackages/pack0.tpac`: 120 items, no Metamesh, none of the four. Publish Module writes that
  folder from `Assets/`; it does not copy hand-written packages into it.
- **Dev install** [Certain for the log, Likely as a rule]: every client session logs one `Loading packages` line per
  module, and TAOM's is `$BASE/Modules/TAOM/Assets...` (`rgl_log_73032.txt:203`); no line loads
  `TAOM/AssetPackages`. A module with a loose `Assets/` tree loads that tree instead of its packages
  ([armory-guide.md](armory-guide.md) "Two asset trees"); the native selection step is untraced.

**Delivery evidence from the other side** (yotthani, `yotthani/bannerlord` commits `2e14c22dfc`, 2026-09-19, and `478587b482`, 2026-10-02; git trees and commit messages, read 2026-10-15): yotthani's own FieldCamp and Refuge modules load these four packages from an `AssetPackages/` folder at game start (the `2e14c22dfc` message), and their source folders hold no `Assets/` tree (git tree of `478587b482`); the `478587b482` message says the camps were seen visible and working in game on 1.5.3 on 2026-10-02. **TAOM:** the git blob hashes of TAOM's four tpacs (`1e26222f`, `078dc719`, `4d9670eb`, `4453ec79`) equal those at `2e14c22dfc`. All four differ at `478587b482`, which rewrote them to the version numbers game 1.5.3 itself uses (texture metadata 3, mesh entry 2, vertex stream segment 1) and changed nothing else per its message. `478587b482` is therefore the version to adopt when the delivery decision lands; TAOM has not diffed the bytes beyond the hashes.

So every field camp and refuge shows the vanilla fallback. Where the packages should live so both deployments load
them (a Kit import into `Assets/`, a release step that copies them, or a module with no loose tree) is an open
decision; see [adopt-mithrilforge-2026-09-29.md](../reviews/adopt-mithrilforge-2026-09-29.md).

**How to tell in game:** `taom_debug_*.log` records `[FieldCamp] placed N ...` (`CampVisualService`) and
`[Refuge] placed N ...` (`RefugeVisualService`). A Field camp standing as the prefab places one entity; the fallback
places a command tent plus a ring of tents.

**A hand-built prop can stay undrawn for a whole first session** with the package intact: the shader variants `pbr_main_map_gbuffer` and `pbr_main_map_shadowmap` are not in the shipped shader sack (`Missing shader from sack: ...` in `rgl_log`), the engine compiles them on first use into `ProgramData/Mount and Blade II Bannerlord/Shaders/CoreShaders`, and until the compile is stored the prop is not drawn. Before blaming a package, grep the log for that line and look at the cache's file dates. (yotthani, MithrilForge `docs/engine/modding-kit.md`@0a2f4761 lines 1081-1086, observed on 1.5.3; not re-measured by TAOM.)

**Gate:** `tools/tests/test_prefab_asset_packages.py` keeps the source copies and the code in step: the prefab meshes
the code passes to `PlaceCenteredPrefab` are exactly its table, each is a Metamesh in a package here, no item name or
GUID repeats, and every package is structurally sound. It cannot see whether the game loads them.

## The recipe

1. **Clone a static donor; never build a mesh from nothing.** The donor is `mi_market_tent_b` in
   `Native/AssetPackages/map_icon_parts.tpac` (96 vertices, 58 faces, one submesh, no LODs, no morph keys). A clone
   inherits every metadata field nobody has reverse-engineered. A donor without LODs cannot leave a stale distant LOD
   behind, which on the campaign map (seen almost always at distance) reads as "sometimes the wrong mesh".
2. **The material lives in another package.** `map_icon_parts.tpac` holds no materials. The donor's material,
   `arab_map_icons` (`6db07f83-8320-4e54-ab33-44c68d84fc24`), is in `materials.tpac`. Clone it under a new GUID and
   name; editing it in place would repaint every Aserai map icon. **Set no shader:** the clone carries
   `c3479109-ae83-4ed8-ab3d-082599f08526`, the general-purpose shader (`328d3572-...` is a head-project shader).
3. **Repoint diffuse slots 0 and 1**, which vanilla points at the same texture. **Slots 2 (normal) and 4
   (specular) get flat 8x8 maps** (normal `128,128,255`): the donor's maps are authored for the tent's UV, and on
   foreign UV the engine samples them at random, so the prop renders dark and mushy. Slot 8 stays. Clear the
   material's inherited dependency list, which still names the donor's assets.
4. **Fill every vertex channel the donor populates, at the new vertex count.** The donor fills all 14, `Uv2`,
   `Colors1`, `Colors2` and zeroed `BoneWeights`/`BoneIndices` included: a static prop uses the skinned vertex
   declaration with zero bone data. One channel left at the donor's length misaligns the channel size table.
   Enumerate the donor's channels; never hardcode the list.
5. **Recompute the shading basis** after any geometry change: area-weighted smooth normals, tangents from the UV
   gradient (Gram-Schmidt against the normal), and the QTangent with rows `(Normal, Tangent, Bitangent)`,
   canonicalised to `w >= 0` and then negated again for a left-handed tangent. Both negations are needed; one gives
   lighting that is almost right. Recomputed on the undeformed donor this matches the shipped normals exactly (mean
   cosine 1.0000 over 96 vertices).
   **The sign of the second negation is UNVERIFIED, and yotthani's two projects disagree.** MithrilForge's recipe
   (`src/MithrilForge.Core/Geometry/TangentBasis.cs@d09f44a4:120-123`) computes `w = dot(cross(N, T), B) < 0 ? -1 : 1`,
   canonicalises to `q.w >= 0`, then negates when `w < 0`, so `q.w` ends negative for `Tangent.w = -1`. FaceLearner
   (`bn faces/FaceLearner.HeadExtract/Program.cs@6265f4a2:4147-4151`, a comment dated 2026-10-08, after commit `73b6fe29`)
   canonicalises and then negates when `w > 0f`, the opposite, and says "VORZEICHEN GEMESSEN": vanilla "setzt q.w NEGATIV
   bei Tangent.w = +1 und POSITIV bei Tangent.w = -1" in `head_male_a` (1389 of 1389 vertices) and `body_male_a`
   (1584 of 1586), measured with its `dumpnrm` tool on a package; the comment says its earlier code, which matched
   MithrilForge's, had the handedness "vertauscht" on every mesh it built and had never been checked against the sign
   because its validator "vergleicht nur die Normale aus q". The two files use the same `w` definition and an opposite
   second negation; MithrilForge's pinned head still carries the old sign. Both stated engine versions are older than
   v1.5.6 or unstated. **TAOM's check cannot see it:** `q` and `-q` are one rotation, so the decoded normal is identical
   and the "mean cosine 1.0000 over 96 vertices" compares normals only (TAOM's reading); only the bitangent sign differs,
   which acts through the normal map's green axis, and TAOM's props use flat 8x8 normal maps (step 3), where it is
   invisible. A prop with a real normal map built from this recipe would show it. **To settle it:** read one vanilla
   skinned mesh's stored QTangent and its tangent `w` and compare the sign of `q.w` (TAOM notes that vanilla head
   packages do not open in TpacTool 0.4.0, so use a body or armour mesh in `core_game.tpac` or another readable
   vanilla package). Until then do not copy either side's sign into a prop that has a real normal map.
6. **UV is not flipped.** tpac UV is top-down, the same as glTF. The note "tpac is bottom-up" in older material is
   about image rows, not the UV convention. A flip mirrors the texture on the model and only shows in game.
7. **Build textures from scratch; do not clone a texture.** The donor's diffuse is a 4096x4096 BC7 texture streamed
   from the tile store (`UnknownUlong2 != 0`); a module must ship embedded pixels (`UnknownUlong2 == 0`). A new
   `Texture` needs `GeneratedAssets` and `BillboardMaterial` set, or `Texture.WriteMetadata` throws on save. Raw
   `R8G8B8A8_UNORM` is a quality choice; DXT1 also loads.
8. **Force alpha to 255 when the source has none.** The donor material carries `alpha_test` (threshold 0.314). A
   JPEG decoded with alpha 0 discards every pixel: no prop, no error.
9. **One package** holding the metamesh, its material and every texture it references. A split into `mesh.tpac`
   plus `mesh_assets.tpac` failed to resolve the material in the head project. No `SubModule.xml` or
   `project.mbproj` entry is needed; `AssetPackages/` is scanned at game start (a restart, not a save reload).
   **TAOM:** only in a module with no loose `Assets/` tree. TAOM, TAOM_Map and the Armory all have one, so their
   `AssetPackages/` is not read on a dev install (see above).
10. **Fresh GUIDs** for the metamesh, the mesh and every data segment's owner, or the clone collides with the donor.
    **A zero item checksum is tolerated** in MithrilForge's 1.4.6 tests; TAOM has not seen it load without an RDC
    entry (below).
11. **Round-trip every write** before claiming it loads: reload the package and compare vertex count, positions, UV,
    channel lengths and the material's diffuse against the source. MithrilForge's `PropVerifier` deletes a package
    that fails.
12. **If the package lives in `Main/_Module/AssetPackages/`**, add its constant to `MESH_CONSTANTS` in
    `tools/tests/test_prefab_asset_packages.py`, then run `python -m unittest tools.tests.test_prefab_asset_packages`.

## Package layout facts

**TAOM**, measured on the four packages and the 1.5.3 install on 2026-09-29. They differ from every package the
engine is known to load in four ways, so a success in game proves all four harmless, and a failure isolates none:
- **Alignment.** MithrilForge pads the TOC-size field (offset 28) so the data starts on an 8-byte boundary and aligns
  each segment; every Kit and vanilla package is packed (`map_icon_parts.tpac`: 6,266 segments, no gap, no pad).
  `tools/tpac_clone_metamesh.py` parses both; its `serialize` writes packed, so it re-serialises these 25, 16, 21 and
  20 bytes short. MithrilForge's author reports that aligned clip packages were rejected on 1.4.6 while aligned props
  loaded, so its clip writer switches alignment off [Unverified by TAOM].
- **No binding segment.** Each prop's Metamesh carries two segments (`97f81dbb`, `5f98413d`); the donor carries
  three, the third being the `f6304064` binding segment that names each LOD's mesh and material, and so do all 228
  metameshes in `map_icon_parts.tpac`. `TpacPropWriter` rebuilds the segment list without it.
- **Zero item checksums and no `RuntimeDataCache` entry.** TAOM has seen zero checksums load only where an RDC entry
  existed ([animation system map](bannerlord-animation-system-map.md) section 6).
- **The donor's name survives:** the inner LOD mesh is still `mi_market_tent_b`; the writer renames the Metamesh
  only. `tpac_clone_metamesh.py` renames every LOD name. What the engine does with the duplicate is UNVERIFIED.

Do not rewrite these packages with `tpac_clone_metamesh.serialize` as a fix: packed is the layout vanilla uses, but a
rewrite is an unreviewed change to commissioned art. It is a fair experiment if the delivery fix still shows the
fallback.

## Names

- A mesh is found by name alone, and a name does not tell you its package: `mi_market_tent_a` lives in a
  `meshes_shared_*` package, not in `map_icon_parts`, and the shard number changes between game versions
  (`_7` on 1.4.6 per MithrilForge; `_9` on 1.5.3, read by TAOM). Check a new name against every tpac of every loaded
  module, not one package. MithrilForge's own check covers `Native/AssetPackages` only; TAOM also loads the `Assets/`
  trees of `TAOM_Map`, `LOTRLOME_Armory` and TAOM. **TAOM:** `python tools/validate_mesh_refs.py --check-name <name>`
  checks both trees of every module (exit 0 free, 1 taken, 2 unverified). The procedure is the recipe below.
- Names go into XML attributes: lowercase ASCII letters, digits and underscore. Prefix TAOM props `taom_`.

## Budget

Vanilla map props measured by MithrilForge, largest LOD0 submesh only: `mi_bat_tent_a` 12 faces, `mi_market_tent_a`
40, `mi_market_tent_b` 58, `mi_aserai_city_house_a` 330, `mi_aserai_arena` 487 (the whole props are larger where they
have more than one submesh). A one-off prop can carry about 1,000 triangles; MithrilForge warns above that and refuses
above 8,000. TAOM's four props run from about 4,500 to 7,500 triangles each (the review's count), inside the cap and
well above that guide. A 512x512 texture is enough for a building seen at map distance; TAOM's four each carry an
uncompressed single-mip diffuse of 16 MB.

## Collision: `bo_` nodes and prefab `<physics>`

What the Modding Kit does with collision, for a prop that needs a body (the Limits section says the packages here have none). Source: yotthani, MithrilForge `docs/engine/modding-kit.md`@0a2f4761 lines 1630-1662, from reading TaleWorlds' glue around the FBX SDK in the Kit (1.5.3). Upstream marks one part of it as inferred where Ghidra lost operands (a sphere's centre and radius); TAOM has not re-read it.

- **A node is a body when** its name starts with `bo_` or `Bo_` (case-sensitive), it has a mesh or is a `bo_composite_` node, no direct child is a skeleton, and `import_physics_shapes` is on (the default). A `bo_` node is never imported as a render mesh (1639-1643).
- **The kind comes from the raw node name alone:** `bo_capsule...` a capsule, `bo_sphere...` a sphere, `bo_composite_...` a composite, anything else a triangle mesh. The geometry is not inspected, and `Bo_Sphere` is a triangle mesh named `bo_sphere` (1647-1650).
- **The shape's name** is the node's name in lower case. With `remove_shape_type_names` (default off) a leading `bo_capsule`, `bo_sphere` or `bo_composite` becomes `bo`, so `bo_capsule_arm` becomes `bo_arm` (1644-1646).
- **In a prefab** a body is named by `<physics shape="bo_..." mass="..."/>` inside the `game_entity`, optionally with `override_material` and `center_of_mass`, plus a `<body_flags>` list of `<body_flag name="..."/>` (1630-1635). **TAOM re-count** on the installed v1.5.5 `Native/Prefabs` (2026-10-15): 23,828 `game_entity` elements, 13,333 `<physics shape="bo_..."`, 2,961 with `override_material`, 280 with `center_of_mass`, 1,408 `occlusion_body_name` attributes. These match upstream's 1.5.3 counts.

**`body_flag` names in shipped prefabs** (TAOM count of `<body_flag name=...>` in `Native/Prefabs/**/*.xml`, v1.5.5, 2026-10-15; the effect of each flag is not decompiled here and the names are the only evidence):

| `body_flag` | Uses | Prefab files |
|---|---|---|
| `dynamic` | 556 | 18 |
| `dynamic_convex_hull` | 437 | 12 |
| `has_steps` | 291 | 32 |
| `ignore_sound_occlusion` | 216 | 2 |
| `agent_only` | 184 | 9 |
| `barrier` | 143 | 6 |
| `moveable` | 105 | 6 |
| `disabled` | 71 | 4 |
| `only_collide_with_raycast` | 45 | 8 |
| `ladder` | 39 | 3 |
| `dont_transfer_to_physics_engine` | 30 | 4 |
| `exclude_path_snap` | 22 | 4 |
| `ai_limiter` | 15 | 3 |
| `missile_only` | 15 | 2 |
| `stealth_box` | 1 | 1 |
| `destructible_door` | 1 | 1 |

This table is derived from shipped prefabs, not from the Kit's own `bf_` flag table, which TAOM has not compared. MithrilForge's prop writer can add a body to a package (`TpacPropWriter.CollisionCooker`, CLI `build ... --collision <material>`, lines 1627-1629 at the same commit); TAOM has not used it.

## Limits

- One material slot per object: bake several materials into one atlas first.
- A prop placed at one terrain height does not tilt to the slope; that is the placing code, not the package.
- No LODs and no collision. A clickable map object needs its click target from a prefab, not from the prop.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/field-camp.md](../features/field-camp.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/bannerlord-animation-system-map.md](./bannerlord-animation-system-map.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](./bannerlord-engine-and-toolchain.md)
- [docs/reference/head-mesh-and-groom-authoring.md](./head-mesh-and-groom-authoring.md)
- [docs/reference/provenance-register.md](./provenance-register.md)
- [docs/reference/ue-to-bannerlord-asset-pipeline.md](./ue-to-bannerlord-asset-pipeline.md)
- [docs/reviews/adopt-mithrilforge-2026-09-29.md](../reviews/adopt-mithrilforge-2026-09-29.md)

<!-- backlinks-end -->
