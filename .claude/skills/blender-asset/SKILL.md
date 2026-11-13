---
name: blender-asset
description: Use when a mesh is authored, fixed, checked or exported in Blender for Bannerlord, over the Blender MCP or headless, and no narrower skill (Tripo props, hair) owns it.
argument-hint: "[<fbx or blend> ...]"
---

# Blender asset work for Bannerlord

Three jobs, in this order: keep a live Blender session safe, send the work to the workflow that
owns it, and pass two gates before anything goes to the Modding Kit. The check table with the
failure behind each check, the view set and the two templates are in
[blender-asset-gates.md](../../../docs/features/blender-asset-gates.md).

## 1. Rules for a live session (Blender MCP)

1. **Confirm the target before the first change.** Call `get_blendfile_summary_path_info` and
   `get_objects_summary`, and check that the file and the objects are the ones you mean. The
   connection can come back attached to another Blender window; a `save_as` then stores the wrong
   scene under your file name.
2. **Work in a saved WORK copy** (`<name>_WORK_<date>.blend`). Never save over the source file.
3. **Read state, never assume it.** After each change, read the result back (object names,
   triangle counts, the file on disk).
4. **In `execute_blender_code`:** set the mode, the active object and the selection before every
   operator; call `bpy.context.view_layer.update()` before you read `matrix_world`, `dimensions`
   or `bound_box`; return data by assigning a dict to `result`.
5. **A dry run copies through the data API:** `new = bpy.data.objects.new("DRYRUN_x", src.data.copy())`,
   link it, and assert `new.data is not src.data`. Never `bpy.ops.object.duplicate()`: over MCP the
   active object can stay the original, and the test then edits the real mesh.
6. **Keep the source.** Move it to a hidden collection; do not delete it. Copy an object before a
   destructive operator (apply, join, decimate, boolean).
7. **After a failed call, read the scene again before the next step.** A call that fails half way
   keeps the changes it made before the error, so a plain repeat can apply them twice.
8. FBX import and export need a `VIEW_3D` `temp_override` in a live session. Headless scripts run
   through `"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe" -b -P <script> -- <args>`
   and report in a file (`tools/blender/blender_job.py`): wait for it, then read its `status`.
9. If the server does not answer, tell Mike and stop the live work
   ([mcp-servers.md](../../../docs/reference/mcp-servers.md) has the folder-key trap). The headless
   gates below need no server.

## 2. Route the task

This skill owns none of these procedures. Open the owner and follow it; come back for the gates.

| Task | Owner |
|---|---|
| Tripo AI FBX to a prop or map icon; new UVs; Painter textures; rebuild from solids | `/prep-tripo-prop` |
| Hair, beards, eyebrows, grooms | `/groom-hair` |
| New creature or mount; replace a mount's FBX or tpac | [creature-mount-authoring.md](../../../docs/ai-includes/creature-mount-authoring.md) |
| Creature or rider clips | [creature-animation-blender-mcp-workflow.md](../../../docs/ai-includes/creature-animation-blender-mcp-workflow.md) |
| Bought quadruped pack onto the horse skeleton | [quadruped-pack-to-horse-skeleton-workflow.md](../../../docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md) |
| Humanoid race rig, retargeted human clips | [troll-race-arp-retargeting-workflow.md](../../../docs/ai-includes/troll-race-arp-retargeting-workflow.md), [troll-race.md](../../../docs/features/troll-race.md) |
| Skeleton, clip and rig export rules | [bannerlord-skeleton-authoring.md](../../../docs/reference/bannerlord-skeleton-authoring.md), `tools/blender/export_rig_for_kit.py` |
| Weapon mesh to tpac and XML | [weapon-creation-workflow.md](../../../docs/ai-includes/weapon-creation-workflow.md) |
| Kit buildings from measured parts | [erebor-buildings.md](../../../docs/kitbash/erebor-buildings.md), [gondor-buildings.md](../../../docs/kitbash/gondor-buildings.md) |
| Bought UE or FBX kit to a Bannerlord kit | [ue-to-bannerlord-asset-pipeline.md](../../../docs/reference/ue-to-bannerlord-asset-pipeline.md) |
| Static prop straight into a tpac, no Kit | [tpac-static-prop-authoring.md](../../../docs/reference/tpac-static-prop-authoring.md) |
| Head mesh | [head-mesh-and-groom-authoring.md](../../../docs/reference/head-mesh-and-groom-authoring.md) |
| Quad remesh or retopology | [cube-map-remesh.md](../../../docs/features/cube-map-remesh.md#required-workflow-selection): the required selection; never `xremesh.exe` |
| Add or fill LODs | `tools/blender/add_mesh_lods.py`, `tools/lod_fill_batch.py`; rule in [armory-guide.md](../../../docs/reference/armory-guide.md) "LODs in the FBX sources" |
| Add a `bo_` collision body | `tools/blender/add_collision_body.py`; rule in [weapon-creation-workflow.md](../../../docs/ai-includes/weapon-creation-workflow.md) Step A |

A mesh with no owner in this table (a prop modelled by hand, a fix to an existing FBX) is worked
here, under the rules of section 1.

## 3. Brief, before the first edit

Write these down in the issue or the reply. A missing line is a question for Mike, not a guess.

- **Asset class and profile:** prop (needs `bo_`), map icon (no `bo_`), weapon piece, armour, creature.
- **Final names:** mesh, material and texture names, lower case, checked free
  (`python tools/validate_mesh_refs.py --check-name <mesh>`, and a search of the faction's
  `AssetSources` for the material and `t_` names: the Kit binds by name module-wide).
- **Triangle budget:** the LOD0 total of the asset it replaces or stands beside, measured with
  `tools/blender/probe_fbx.py`. Not a table value.
- **LODs and body:** which levels its kind needs, and whether it takes a `bo_`.
- **Texture sets** and their sizes, from the owner's doc.

## 4. Order

Final topology comes before UVs, rigging and LODs. LOD0 is approved before its LODs and its `bo_`
are made. Both gates pass before a file goes to the Kit.

## 5. Gate 1: preflight

```
blender-launcher -b -P tools/blender/mesh_preflight.py -- --fbx <kit fbx> --out <staging>\preflight --profile prop|icon|mesh [--budget-tris N] [--interior]
python tools/audit_fbx_lods.py --fbx <kit fbx>
```

Read `<staging>\preflight\_report\DONE.txt`: `verdict`, then every finding, then `skipped_hidden`
(a hidden mesh was not checked: show it and run again if it ships).

- **FAIL** (a blocker): fix the mesh and run again. Status `error` with a `verdict` is a failed
  check; status `error` without one is a crashed run, so read `trace`.
- **Major:** fix it, or name it in the verdict with the evidence that it is harmless.
  `faces-inverted-suspect` is settled only by the views of Gate 2.
- **Minor and note:** report them; they do not hold the asset.
- `--interior` is only for a shell that is seen from inside (a hall, a cave).

In a live session the same check runs read-only on the open scene:

```python
import sys, importlib
sys.path.insert(0, r"E:\repos\TAOM\tools\blender")
import mesh_preflight; importlib.reload(mesh_preflight)
r = mesh_preflight.preflight_scene(names=["<mesh>", "bo_<mesh>"], profile="prop")
result = {k: r[k] for k in ("verdict", "counts", "findings")}
```

Compare the LOD chain that `audit_fbx_lods.py` prints with the rule of the asset's kind. A skinned
mesh also passes its owner's export gate (`export_rig_for_kit.py`, `/groom-hair` finalize).

## 6. Gate 2: views and verdict

```
blender-launcher -b -P tools/blender/qa_views.py -- --fbx <kit fbx> --out <staging>\views [--only <metamesh>] [--bodies]
```

Read all six PNG files. The mesh is grey, back faces are culled and the world is magenta: a hole
shows as magenta, or as the inside of the mesh seen through a missing wall. Check the silhouette
against the brief, the underside (`3q_below`), and every place Gate 1 named. `--bodies` shows the
collision bodies: compare their outline with the mesh.

`qa_views.py` changes render settings, so it never runs in Mike's open file. In a live session,
set `space.shading.show_backface_culling = True` on the 3D viewport and read
`get_screenshot_of_area_as_image` with `VIEW_3D`; that is also the way to see wireframes and
armatures. A textured look comes from `tools/blender/render_painter_preview.py`.

Then give the verdict in this form:

```
Verdict: SHIP | SHIP WITH NOTES | NO-SHIP
Budget: <LOD0 triangles> of <budget>; textures <sizes>
Preflight: <verdict>, <counts>
Views read: <the six names, plus any extra>
Blockers: ...   Majors: ...   Minors: ...
Not covered: Kit import, in-game check
```

A check in Blender may reject an asset; it never confirms one. SHIP means "ready for the Kit".
The Kit import and the in-game check stay owed to the asset's own workflow, and Mike's eye in game
decides whether it looks right.

## 7. When Mike gives a reference picture

1. Before any change, write the analysis (template in the gates doc): what the subject is, its
   main masses and their proportions, the camera angle, the materials, and the three to five
   features the result must have. For an existing file, add a table of what differs today.
2. Set the camera to the picture's angle first. A wrong frame makes every later comparison wrong.
3. Build in three passes: silhouette and proportions, then the secondary forms that give depth
   (steps, recesses, ribs, belts), then small detail. No small detail on a wrong silhouette.
4. After each pass, render from that camera and list at most five differences, largest first:
   frame, silhouette, flatness, materials, missing features. Fix them and compare again.
5. Stop after three compare passes. Report what still differs; do not call a partial match done.
6. Judge a direction (which way a part points) in an orthographic view, never from the picture.

## 8. Hand-off and records

Copy a file into a live module's `AssetSources` only on Mike's word, under a new name or with a
backup, and compare hashes after (`/prep-tripo-prop` Phase 4 has the full rules). Put the verdict
and the numbers in the issue.

## Traps

| Symptom | Cause and fix |
|---|---|
| Every render looks right, the game shows holes | Blender draws back faces, the engine culls them: judge from `qa_views.py` |
| `faces-inverted-suspect` on an approved building | Thin slabs wound inward with a face right behind them: harmless if the views show no hole |
| Preflight passes, the mesh still has flipped walls | The inverted share is a lower bound: a flipped face that sees out through a window is not counted. The views are the proof |
| Mesh or material missing in the Kit | A `.001` name, upper case in an XML reference, or a material the Kit does not have: fix the names, never the Kit |
| `transform_apply` changed nothing | The mesh data has several users: make it single-user first |
