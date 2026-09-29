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
2026-09-29). `CampLayoutBuilder.PlaceCenteredPrefab` asks for them by name through
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

So every field camp and refuge shows the vanilla fallback. Where the packages should live so both deployments load
them (a Kit import into `Assets/`, a release step that copies them, or a module with no loose tree) is an open
decision; see [adopt-mithrilforge-2026-09-29.md](../reviews/adopt-mithrilforge-2026-09-29.md).

**How to tell in game:** `taom_debug_*.log` records `[FieldCamp] placed N ...` (`CampVisualService`) and
`[Refuge] placed N ...` (`RefugeVisualService`). A Field camp standing as the prefab places one entity; the fallback
places a command tent plus a ring of tents.

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
  checks both trees of every module (exit 0 free, 1 taken, 2 unverified). The procedure is `/new-map-prop`.
- Names go into XML attributes: lowercase ASCII letters, digits and underscore. Prefix TAOM props `taom_`.

## Budget

Vanilla map props measured by MithrilForge, largest LOD0 submesh only: `mi_bat_tent_a` 12 faces, `mi_market_tent_a`
40, `mi_market_tent_b` 58, `mi_aserai_city_house_a` 330, `mi_aserai_arena` 487 (the whole props are larger where they
have more than one submesh). A one-off prop can carry about 1,000 triangles; MithrilForge warns above that and refuses
above 8,000. TAOM's four props run from about 4,500 to 7,500 triangles each (the review's count), inside the cap and
well above that guide. A 512x512 texture is enough for a building seen at map distance; TAOM's four each carry an
uncompressed single-mip diffuse of 16 MB.

## Limits

- One material slot per object: bake several materials into one atlas first.
- A prop placed at one terrain height does not tilt to the slope; that is the placing code, not the package.
- No LODs and no collision. A clickable map object needs its click target from a prefab, not from the prop.
