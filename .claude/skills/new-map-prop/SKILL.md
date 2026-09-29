---
name: new-map-prop
description: Use when adding a custom static prop (camp, landmark, map icon) built from a GLB with MithrilForge instead of the Modding Kit.
---

# New Map Prop

Thin entry point over **[docs/reference/tpac-static-prop-authoring.md](../../../docs/reference/tpac-static-prop-authoring.md)**:
read it first. It holds the recipe, the four ways MithrilForge output differs from Kit packages, and why
TAOM's first four props (the field camp and refuge) render nowhere. This skill adds the order and the gates.

## Phase 0: can this run at all?

- **The tool.** MithrilForge (yotthani, MIT) lives outside the repo in `E:\Tools\MithrilForge\`, never
  installed into the harness. It needs yotthani's private `TpacTool-bannerlord` fork; until that source has
  been read (`docs/reviews/adopt-mithrilforge-2026-09-29.md`, owed item 2), stop and say so. Before first use,
  its own suite must pass in Debug and Release with `MITHRILFORGE_BANNERLORD` set to the install root.
- **The delivery.** Decide, and write down, which tree will load the package. TAOM, TAOM_Map and the Armory
  have a loose `Assets/` tree, so their `AssetPackages/` is never read on a dev install, and Publish Module
  does not carry a hand-written package into a release. Until the maintainer settles where props live, a
  prop built here renders nowhere, exactly like the camps. Never write "ships" before Phase 5 has proven it.

## Phase 1: the source model

One object per prop, one material slot (bake several into an atlas), UV kept, a diffuse image, about 1,000
triangles for a one-off (the tool refuses above 8,000). Export GLB from Blender (File, Export, glTF 2.0,
Selected Objects). The art source goes into `docs/reference/provenance-register.md` and `LICENSE-CONTENT.md`
before anything ships (`.claude/rules/provenance.md`).

## Phase 2: the name

`taom_` plus lowercase letters, digits and underscore. Then
`python tools/validate_mesh_refs.py --check-name <name>`: exit 0 free, 1 taken (it lists where), 2 unverified.
MithrilForge's own check reads only `Native/AssetPackages`.

## Phase 3: build

`MithrilForge build <file.glb> --name <name> --out <staging folder>`. Its verifier reloads the package and
deletes it on any mismatch; a deleted package is a stop, not a retry. Never rewrite the result with
`tpac_clone_metamesh.serialize` (it packs what MithrilForge aligns).

## Phase 4: wire it

- Place the package where Phase 0 decided.
- Code asks for it through `CampLayoutBuilder.PlaceCenteredPrefab` (or `MetaMesh.GetCopy(name, showErrors:
  false, mayReturnNull: true)`) with a vanilla fallback, and logs the placed-entity count so a fallback is
  visible in `taom_debug_*.log`.
- Add the constant to `MESH_CONSTANTS` in `tools/tests/test_prefab_asset_packages.py` if the package lives in
  `Main/_Module/AssetPackages/`, then run `python -m unittest tools.tests.test_prefab_asset_packages`.
- C# changes follow the normal gates: TDD, `/deep-review`, `/verify`.

## Phase 5: prove it loads

Restart the game (packages load at start). Read the client log's `Loading packages` line for the module:
it names the tree the engine read. Then the prop on screen, or its placed-entity count in the debug log. For
a release, open the packaged folder and confirm the package is in it. Record the result in the feature doc.

## Top traps

- The fallback hides every failure: no error, no log line beyond the count.
- A success in game proves the package format; a failure isolates nothing (alignment, no binding segment,
  zero checksums and no RDC entry all differ from Kit output).
- A name does not tell you its package; vanilla shards move between versions.
