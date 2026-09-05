# Main Map Vista

The vista is the distant terrain the campaign map draws **beyond** the terrain node bounds. TAOM's
terrain is 16 x 16 nodes at 100 units, so 1600 x 1600 world units, and every settlement sits inside
that rectangle. Everything the camera sees outside it is vista.

When the vista is misconfigured the map renders correct terrain in a central rectangle and a
repeating checkerboard everywhere else. It is invisible at normal zoom and shows up only when the
player pulls the camera back, which is how it can ship unnoticed.

## Where the settings live

One element, `<terrain>`, in the **LIVE** `Modules/TAOM_Map/SceneObj/Main_map/scene.xscene`. The
repo carries no copy of the scene at all, so unlike `settlements.xml` there is not even a stale
shadow to confuse matters. See the "TAOM_Map settlements" trap in CLAUDE.md for the general rule
about editing live module data.

Read the current values without opening the Modding Kit:

```bash
grep -o '<terrain [^>]*>' \
  "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/SceneObj/Main_map/scene.xscene" \
  | head -1 | grep -o 'vista_[a-z_]*="[^"]*"' | tr '\n' ' '
```

### Attribute to Kit field map

Kit path: select the Terrain node, then `Terrain property_browser`, `Properties` tab, `Vista Textures`
group. Field names confirmed against the label table in `bin/Win64_Shipping_wEditor/TaleWorlds.Native.dll`.

| XML attribute | Kit field |
|---|---|
| `vista_diffuse_name` | **Texture** (first slot in the group) |
| `vista_diffuse_winter_name` | Winter Texture |
| `vista_diffuse_fall_name` | Fall Texture |
| `vista_normalmap` | Vista Normalmap |
| `vista_detail_albedo_name` | Detail Albedo |
| `vista_detail_normal_name` | Detail Normal |
| `vista_tileset` | Tileset Name |
| `vista_diffuse_blend_type` | Blend Mode |
| `vista_diffuse_blend_amount` | Blend Weight |
| `vista_layer_detail_distance` | Layer Render Distance |
| `vista_albedo_multiplier` | Albedo Multiplier |
| `vista_detail_tile` | Detail Texture Tile |
| `vista_blend_start` / `vista_blend_end` | Vista Blend Start / End Distance |

The **Texture** slot sits above Winter and Fall at the top of the group, so it is the first thing to
scroll out of view. A screenshot of the panel that begins at Winter Texture is not showing the field
that matters most.

## Two ways to drive a vista, and TAOM uses the harder one

**Vanilla uses a tileset.** `SandBox`, `NavalDLC` and the archived 1.3.0 SandBox copy all leave every
vista texture slot empty and set `vista_tileset` to `WorldMap`. That resolves to
`SandBox/SceneObj/Main_map/TileSets/WorldMap.gts` (4 MB) plus a sibling `pages/` directory, a virtual
texture the engine streams. There is also a module-wide `Native/TileSets/Bannerlord.gts` at 122 MB
alongside `ValidMaterials.xml`.

**TAOM uses a single flat texture.** `TAOM_Map` ships **no `TileSets/` folder**, leaves
`vista_tileset` empty, and drives the vista entirely from `vista_diffuse_name`. So does A Dance of
Dragons. This is the normal route for a total conversion that replaces the whole world, because
authoring a `.gts` tileset is a separate pipeline nobody here has run.

The practical consequence: **TAOM cannot copy vanilla's vista values**, because vanilla's are tuned
for a tileset TAOM does not have. Use the custom-texture maps as the reference instead.

## Reference table

Measured 2026-09-04 by reading each `scene.xscene` directly.

| Map | Normalmap | Texture | Blend type | Blend wt | Albedo mult | Detail tile | Tileset |
|---|---|---|---|---|---|---|---|
| lemmy (TAOM lineage, `E:\LOTRAOMAssets\sceneobj_map_lemmy`) | *empty* | `16K_Vista_02` | 1 | 0.000 | 0.670 | 20.000 | *empty* |
| A Dance of Dragons | *empty* | `IAF_colormap` | 0 | 1.000 | 1.000 | 1.000 | *empty* |
| SandBox (live) | *empty* | *empty* | 1 | 1.000 | 0.670 | 1.000 | `WorldMap` |
| SandBox 1.3.0 | *empty* | *empty* | 1 | 1.000 | 0.670 | 1.000 | `WorldMap` |
| NavalDLC | *empty* | *empty* | 1 | 1.000 | 0.670 | 1.000 | `worldmap` |

**`vista_normalmap` is empty in all five.** Nothing in any reference map, vanilla or community, puts
a texture in the Vista Normalmap slot, and every vista texture in TAOM's own set is imported with
usage `albedo` rather than as a normal map. Treat a non-empty value there as the first thing to clear.

TAOM's target config, matching lemmy (same map lineage, same texture family):

```
vista_normalmap=""            vista_diffuse_name="16K_Vista_02"
vista_diffuse_blend_amount="0.000"  vista_albedo_multiplier="0.670"
vista_detail_tile="20.000"    vista_tileset=""
```

**Blend Mode is not load-bearing and is not worth chasing.** The two working custom-texture maps
disagree on it (lemmy uses 1, A Dance of Dragons uses 0), and with Blend Weight at 0.000 there is
nothing for it to act on. The editor binary registers exactly one label next to "Blend Mode", the
string `Blend`, so the dropdown offers nothing else to pick. TAOM sits on type 2 and that is fine.

## What is NOT the lever: Texture Inspector import settings

The `Texture Flags` in the Kit's Texture Inspector (For Terrain, Dont Degrade, Is Bumpmap, Dont
Resize in Atlas, and the rest) were **not** involved in the 2026-09-04 breakage. Checking them is not
a fix to reach for first.

Proof, and the technique worth reusing: byte-compare the known-good `.tpac` descriptor against the
current one.

```bash
cmp -l old/16K_Vista_tex.tpac new/16K_Vista_tex.tpac | wc -l
cmp -l old/16K_Vista_tex.tpac new/16K_Vista_tex.tpac | awk '{printf "%s ", $1}'
```

For both vista textures the result was **identical file size and exactly 103 differing bytes, every
one inside a 16-byte aligned block** (offsets 9-24, 53-68, then four more blocks). Diffing the
extracted strings showed only incidental 3-to-4 character garbage out of those blocks. Every setting
string was identical: `DXT1`, `none`, `albedo`, `B8G8R8`, same `$BASE/...` source path.

Those blocks are asset GUIDs. **Re-importing a texture regenerates the GUIDs and preserves the import
settings.** So a re-import is safe with respect to flags, and a flags hypothesis can be ruled in or
out with one command instead of by clicking through the inspector.

## Vista asset formats

Measured from PNG IHDR headers and `.tpac` strings on 2026-09-04. Some of these were removed from the
module during that session's cleanup; the formats are recorded because the choice between them matters.

| Source PNG | Dimensions | PNG colortype | Cooked format | Alpha |
|---|---|---|---|---|
| `16K_Vista_02.png` | 16384 x 16384 | 6 (RGBA) | DXT5 | `has_alpha` |
| `16K_Vista.png` | 16384 x 16384 | 2 (RGB) | DXT1 | none |
| `Vista_map_settlements.png` | 2048 x 2048 | 6 (RGBA) | DXT5 | `has_alpha` |
| `Vista_dot.png` | not measured | not measured | DXT5 | `has_alpha` |
| `16K_VISTA_022.png` | 1316 x 1317 | 6 (RGBA) | R8G8B8A8_UNORM | `has_alpha` |

Every one is imported with usage `albedo`.

`16K_Vista` is the odd member: the only alpha-free texture in the set, cooking to DXT1. The Kit
reports it as 15 mip / 170.667 MB at runtime against a 506.919 MB source. `16K_Vista_02` is the same
resolution *with* alpha, and is what the working lineage map points at. When picking between them,
prefer the one with alpha.

`TAOM_Map` ships **no `AssetPackages/`**, only loose `Assets/**/*.tpac` descriptors, matching the
"no cooked tree" finding recorded for `LOTRLOME_Armory` in CLAUDE.md.

## Diagnosing a vista regression

1. **Read the Kit's own single-step backup first.** `TAOM_Map/SceneObj/Backups/Main_map/scene.xscene`
   is the save immediately before the current one. Comparing its `<terrain>` line against the live one
   isolates exactly what the last save changed, which is far better evidence than a weeks-old asset
   backup. On 2026-09-04 this turned "compare two divergent maps" into "two attributes moved".
2. **Do not roll back to a backup file.** Both candidates carried real losses: the Aug 20 copy predated
   two weeks of map work, and even the nine-minute-old `Backups/` copy differed from the live scene by
   661 lines of entity transforms and skeleton poses. Make the surgical attribute change instead.
3. **Confirm nothing else was lost** before assuming the regression is the whole story:

   ```bash
   grep -c game_entity <scene>                              # entity totals
   grep -o '<game_entity name="[^"]*"' <scene> | sort       # then comm -23 / -13 the two lists
   ```

   On 2026-09-04 both saves held 45,592 entities with identical name sets, which ruled out an
   accidental deletion and pointed at the terrain attributes.
4. **Widen the comparison before trusting one backup.** The first repair attempt failed precisely
   because it was derived from the `Backups/` copy alone. Five reference maps is what surfaced the
   unanimous empty normalmap.
5. **Only then reach for the texture pipeline**, using the `cmp -l` technique above.

The engine's own attribute vocabulary is readable straight out of the binary when a field name is
unclear, which is how the Kit label table above was confirmed:

```bash
f=".../bin/Win64_Shipping_wEditor/TaleWorlds.Native.dll"
off=$(grep -aob "Blend Mode" "$f" | head -1 | cut -d: -f1)
dd if="$f" bs=1 skip=$((off-3000)) count=6000 2>/dev/null | tr -c '[:print:]\n' '\n' \
  | grep -E '^[A-Za-z][A-Za-z0-9_ ]{2,40}$' | uniq
```

## Gotchas

| Gotcha | Detail |
|---|---|
| **The Texture slot scrolls out of view** | It is the first field in Vista Textures, above Winter and Fall. A panel screenshot that begins at Winter Texture hides it. |
| **Clearing the Texture flips Blend Weight to 1.000** | Observed across the 11:14 to 11:23 saves. When restoring the Texture, set it first and then correct Blend Weight, because assigning a texture can move the weight on its own. |
| **An older backup is not automatically the right one** | The Aug 20 copy had the correct empty `vista_normalmap` but predated the whole 16K vista import. Two backups can each be right about a different field, which is why the reference table exists. |
| **Re-importing changes asset GUIDs** | 103 bytes across six 16-byte blocks per descriptor. Import settings survive; anything keying on the old GUID does not. Scene references are by name, so the vista itself is unaffected. |
| **The live module is unversioned** | Every fix here lives only in the game folder and a module reinstall reverts it silently, the same trap as the `LOTRLOME_Armory` and `TAOM_Map` fixes recorded in CLAUDE.md. |
| **The Kit holds the scene in memory** | Hand-editing `scene.xscene` while the Kit has `Main_map` open loses the edit on its next save. Make the change in the UI, or close the Kit first. |
| **Blend Mode has one label** | The editor exposes only `Blend`. Do not go hunting for a different entry; the value is inert while Blend Weight is 0. |

## Incident record, 2026-09-04

A vista texture was cleared by accident, producing white and then checkerboard terrain when zoomed
out. Sequence reconstructed from file mtimes and the Kit's `Backups/` folder:

| Time | State |
|---|---|
| Jun 26 | Working set imported. Five vista textures in `Assets/Vista_16K/`. |
| Aug 20 07:29 | Backup snapshot: `normalmap=""`, `diffuse="Vista_map_settlements"`, `blend=0.000`. |
| Sep 4 11:09 to 11:11 | Vista textures re-imported. New asset GUIDs. |
| Sep 4 11:14 | `normalmap="16K_Vista"`, `diffuse="Vista_map_settlements"`, `blend=0.000`. |
| Sep 4 11:23 | **The mistake.** `diffuse=""`, `blend=1.000`. White when zoomed out. |
| Sep 4 12:15 | `diffuse="16K_Vista"`, `blend=0.000`. Checkerboard persisted. |
| Sep 4 12:43 | `normalmap=""`, `diffuse="16K_Vista_02"`, `albedo_mult=0.670`, `detail_tile=20.000`. |

Also noted while comparing, and not part of the regression: TAOM runs `colormap_detail_level="10"`
where SandBox and lemmy both run `0`, and SandBox carries `terrain_render_version="1"` which TAOM and
lemmy both omit. Both predate this incident and lemmy works without the render version, so neither
was pursued.

**Verification still owed:** an in-game zoom-out on the 12:43 save.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)

<!-- backlinks-end -->
