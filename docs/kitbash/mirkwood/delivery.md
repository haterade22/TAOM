# Mirkwood Blender delivery

The master compound and the Asset Browser library are the starting files for authoring.
The build record covers the design iterations and the individual kit records carry
the measured joins, material settings, and verification results.
[Build record](../mirkwood-kit.md) · [Lessons](lessons.md) · [Game verification](../../features/mirkwood-kit.md#how-to-verify-in-game)

## Local files

The work root is `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood` on the
desktop. Start at `review_now.html` for the rendered compound, same-scale plans,
and comparisons. The paths below are relative to that root. The handoff files,
builders, and evidence live there. This repository carries the catalog and build
record.

| Use | File | What to compare |
| --- | --- | --- |
| Layout authoring | `kit_full/site_01/view/mirkwood_compound_layouts.blend` | Castle and town compound scenes. `kit_full/site_01/evidence/site_reopen.json` records 31 and 44 placements, 21 packed maps, and the 22 inherited scenes. |
| Reusable pieces | `kit_full/library_01/mirkwood_asset_library.blend` | Register the `kit_full/library_01` directory in Blender. Keep `blender_assets.cats.txt` beside the blend. `README.md` explains placement and Append. `evidence/library_reopen.json` holds the master and library hashes and all 26 checked origins. |
| Names and bounds | `kit_full/site_01/evidence/kit_manifest.json` | Named collections, measured bounds, source reports, and texture hashes. The name comparison is against the documented Mirkwood catalog. Check new names against the whole module before import. |
| Stone and timber trim | `kit_full/trim_03/view/mirkwood_architectural_trim.blend` | Use `kit_full/trim_03/maps` and the measured usable V bands. `kit_full/trim_03/evidence/trim_reopen.json` and `kit_full/trim_mip_01/trim_mip_proof.json` cover the saved map and UV checks and local filtering. |
| Render coverage | `kit_full/morning_01/index.html` and `review_hall_light_01.html` | All 22 main Cycles stills and two amber comparison stills passed. The corrected amber blend is under `kit_full/lighting_02` and the renders under `kit_full/morning_lighting_02`. |

The gate leaves, fitted hall joints, flagstone paving, bridge runoff, and village slate
are editable study files. Open them through the section links in `review_now.html`.
Under `kit_full` the folders are `gate_leaf_01`, `hall_joint_01`, `court_paving_01`, `weather_01`,
and `village_roof_02`. For the reviewed town paving view in Blender, open
`kit_full/court_paving_01/view/mirkwood_court_paving.blend` and select
**Mirkwood | Town court limestone setts** and the **town_market_court** camera.
The Asset Browser catalog contains the base parts used in the master.

## Visual FBX stages

| Folder under `kit_full` | Contents | Verification record |
| --- | --- | --- |
| `siege_04/staging` | One base siege FBX, 10 LOD0 meshes, 15 maps | `review_export.json` |
| `houses_01/staging_02` | Five civic family FBXs, 13 LOD0 meshes, 21 maps | `civic_export_02.json` |
| `lod_siege_01/staging_02` | Five siege families, 15 meshes across LOD0/3/6, 21 maps | `siege_lod_pilot_export.json` and `siege_tower_correction.json` |
| `lod_02/staging` | Four civic families, 12 meshes across LOD0/3/6, 21 maps | `lod_pilot_export.json` |

Stage files are visual export candidates. A civic LOD pilot carries one base asset
at three levels and uses the name of its larger family FBX. Keep the pilot exports
in their own folders. When preparing an asset for import, assemble its selected
visual levels, body, and any scene-bound parts using the layout standard. Likewise, the five siege LOD
pilots cover a subset of the ten-piece base kit. Compare the reports by mesh name
before replacing a family file. Verify each new asset's frame and own collision
body in the Modding Kit. The [layout standard](../kingdom-kit-layout.md) gives the
target import arrangement.

## Repeat the checks

Run Blender 5.3.0 through the shared headless launcher and write each new check
or export to an explicit folder. The local scripts and their inputs must travel
with the work root. Use a fresh output for each rerun so the original receipt
still identifies the reviewed files.

```powershell
$m = 'E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood'
$runner = 'E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/_tools/blender_one.py'
$master = "$m/kit_full/site_01/view/mirkwood_compound_layouts.blend"
$manifest = "$m/kit_full/site_01/evidence/kit_manifest.json"
$out = "$m/kit_full/library_recheck"

python "$m/_tools/site_01/test_site_report.py" "$m/kit_full/site_01" "$m/kit_full/siege_04/evidence/kit_report.json"
python $runner --script "$m/_tools/library_01/build_asset_library.py" --wait-for "$out/evidence/library_reopen.json" --minutes 8 -- --out $out --source $master --manifest $manifest
python $runner --script "$m/_tools/library_01/verify_append_library.py" --wait-for "$out/evidence/append_smoke.json" --minutes 8 -- $out
```

The site check compares layout transforms with the saved siege frontages and
validates all 1,728 route samples in the report. The library check independently
reopens the compound and the new library. It compares placement anchors, evaluated
geometry, material assignments, and all packed map hashes. The Append test places
a battery, lantern, and merchant house into a fresh scene.

The shared export entry points are under `_tools`. For the civic base export use
`houses_01/export_civic_02.py` with the saved housing blend. For the five siege
distance pilots use `lod_01/export_siege_lod_pilot.py` with the saved siege
distance study. Both receive `--out <fresh-stage>` after Blender's `--` separator.
The reports above record the source files, exact FBXs, and round-trip checks.
