# TAOM Retopology Toolkit for KEYforce

A source-only garment retopology package containing the independent TAOM engine and Petik 2.0.7.
No RetopoPlanes `.blend`, node assets, demo meshes, textures, or activation data are included.

## Requirements

- Blender 5.2.3 LTS is the tested runtime. The TAOM manifest declares Blender 4.5 or newer, but older
  versions have not been verified.
- Ordinary Python for source tests and rebuilding the package.
- No RetopoPlanes, Exoside, or Petik installation is required for the independent TAOM engine.

## Install the add-ons

Extract this archive, then in Blender open **Edit > Preferences > Add-ons > Install from Disk** and
install both archives from `installable/`:

1. `taom_retopology.zip`: the independent engine and **TAOM** sidebar.
2. `Petik.zip`: Mihai Dobrin's original multi-patch and line tools under GPL-3.0-or-later.

Enable both add-ons. Petik uses **Ctrl+Alt+Shift+X** for multi-patch and **Ctrl+Alt+Shift+Z** for the
line tool. The TAOM panel is in **3D View > Sidebar > TAOM > TAOM Retopology**.

## Retopologize a garment

1. Apply the source garment's rotation and scale. Give it a complete UV map with separate,
   non-overlapping islands.
2. Assign the original garment and a collection of quad guide meshes in the TAOM panel.
3. Run **3D to Patterns** for a flat reference. The original source is preserved.
4. Edit or generate quad guides. Use ordinary mesh editing, **Initialize Planes**, mirroring, auto
   fill, or Petik. Triangles and ngons are rejected with their face indices.
5. Configure density, snapping, smoothing, subdivision/reprojection, separation, thickness, and
   preview options. Every option in the behavioral contract is exposed in the panel.
6. Run **Generate / Update Retopology**. The result is an ordinary editable mesh with an active UV
   layer, not a proprietary Geometry Nodes dependency.
7. Review helpers and UVs, then export to a staging directory outside the live Armory. Complete
   TAOM's weight, LOD, staged-FBX, and Kit review before shipping art.

Use **Output Generated Planes** when you want flat guides to edit. Use **Only Patterns** or
**Only Seams** for reference geometry. Read `docs/features/retopology-toolkit.md` for backend
selection and `docs/research/retopo-planes-1-6-3-clean-room.md` for the behavior contract.

## Headless example

Replace `blender-launcher` with your Blender executable. The active object in `scene.blend` must be
the source garment, with a guide collection named `RetopoPlanes`.

```bat
blender-launcher -b scene.blend --python-exit-code 1 -P tools/examples/retopo_active.py -- --output "D:\staging\retopo.blend" --subdiv 1
```

The script refuses to overwrite the input or an existing output unless `--overwrite` is explicit.
It writes `<output-stem>-retopo-report.json` beside the staged output and applies the same overwrite
policy to that report.

## Verify the extracted source

From the `retopo-toolkit` directory:

```bat
python -m unittest tools.tests.test_retopo_engine tools.tests.test_retopo_differential tools.tests.test_retopo_contract_regressions tools.tests.test_petik_compat tools.tests.test_package_retopology_toolkit
python -m ruff check tools/retopo_engine tools/blender/retopo_adapter.py tools/blender/retopo_operators.py tools/tests/test_retopo_engine.py
blender-launcher -b --factory-startup --disable-autoexec --python-exit-code 1 -P tools/blender/retopo_selftest.py
blender-launcher -b --factory-startup --disable-autoexec --python-exit-code 1 -P tools/blender/petik_selftest.py
set BLENDER_USER_SCRIPTS=D:\staging\blender-test-addons
set BLENDER_USER_CONFIG=D:\staging\blender-test-config
blender-launcher -b --factory-startup --disable-autoexec --python-exit-code 1 -P tools/blender/retopo_installable_selftest.py
```

Set `BLENDER_USER_SCRIPTS` to a disposable directory before running the installable test. The script
refuses to run when it is unset, and its `overwrite=True` installation targets that directory. The
add-ons are disabled after the test.
The Blender runs write `TestResults/Blender/retopo_selftest_report.json`,
`TestResults/Blender/petik_selftest_report.json`, and
`TestResults/Blender/installable_report.json`; require `"status": "ok"` in all three.
Generated self-test reports and staged meshes are not package inputs.

## Rebuild the ZIP

```bat
python tools/package_retopology_toolkit.py --out dist/keyforce-retopology-toolkit.zip
```

The builder is `tools.package_retopology_toolkit`. It uses an explicit manifest, fixed ZIP timestamps,
and nested installable add-on archives.
It prints the main archive's SHA-256 digest. The recipient package does not include its own build
outputs, proprietary reference files, demo content, or generated meshes.

## License and provenance

The independent TAOM engine, adapter, operators, examples, tests, and packaging code are MIT
licensed. Petik is a separate GPL-3.0-or-later add-on with six recorded compatibility fixes and
its own source, license, and notice. See `LICENSE.txt`, `NOTICE.txt`, and each installable archive.

Retopo Planes 1.6.3 was used as a behavioral reference and for measured synthetic output comparisons.
No original node graph or product asset is redistributed. The implementation is not claimed to be
byte-identical to the proprietary solver; assisted output still requires artist review.
