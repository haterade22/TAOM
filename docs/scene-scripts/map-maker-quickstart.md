# CS_Road — Map Maker Quickstart

Paint a road, river, or strip mesh along a path you've drawn in the scene editor. Instead of placing dozens of road tiles by hand, you draw one Path entity, attach the `CS_Road` component, pick a material, and the engine builds the geometry for you. Live preview while you tweak.

## Before you start

Open your launcher and switch to the editor profile. **All five of these mods must be enabled**, or the editor will crash on TAOM load:

- Bannerlord.Harmony
- Bannerlord.UIExtenderEx
- Bannerlord.MBOptionScreen
- Bannerlord.ButterLib
- TAOM (and TAOM_Map if you want the main map auto-opened)

You can use either of two editors:

- **The standalone modding kit** (`Win64_Shipping_wEditor`) — for scene-only edits.
- **The in-game scene editor** during an active singleplayer campaign — for editing while you can also playtest the result. Both pick up `CS_Road` automatically.

## The five-step workflow

1. **Draw a Path entity** in the scene. Give it a unique name like `road_to_minas_tirith`. Drop control points along the route you want the road to follow.
2. **Pick (or create) any entity** to host the road component. The road geometry is attached to whatever entity holds the `CS_Road` script — many map makers use a dedicated empty entity for this.
3. **Attach the script.** In the entity's component picker, search **`CS_Road`** and add it.
4. **Wire it up.** In the property panel, set:
   - `PathName` = the exact name of the Path entity (`road_to_minas_tirith`)
   - `Material` = pick a road / dirt / river material from the editor picker
5. **Click `Generate`.** A quad-strip mesh appears along the path.

To iterate: change a knob, click `Generate` again. Or enable `Live` for auto-regen every 0.5s while you drag path control points around — **disable it before you save**, see Cleanup below.

## The 16 knobs

| Field | Default | What it controls |
|---|---|---|
| `PathName` | `""` | Exact name of the Path entity in the scene |
| `Width` | `4` | Half-width in metres; total quad spread = 2 × Width |
| `ElevationOffset` | `0.1` | Lift in metres above terrain (raise if the road clips into hills) |
| `StepCurve` | `{0:1},{100:1}` | Adaptive sample spacing along the path — see cheatsheet below |
| `Material` | (picker) | Editor-assigned material; nothing renders until set |
| `CustomColor` | `#ffffffff` | RGBA hex tint applied on top of the material (7 or 9 chars) |
| `RepeatU` | `1` | UV tile count along the road's flow axis |
| `RepeatV` | `1` | UV tile count across the road's width |
| `InvertU` | `false` | Mirror the texture along the flow axis |
| `InvertV` | `false` | Mirror the texture across the width |
| `RotateUV` | `false` | Swap U ↔ V (rotate the texture 90°) |
| `FlowDirection` | `AlongU` | Which axis the texture flows along (`AlongU` or `AlongV`) |
| `FlipFaces` | `false` | Reverse triangle winding — toggle if the road looks invisible from above |
| `Generate` | (button) | Regenerate the mesh now |
| `Readme` | (button) | Print StepCurve syntax help to the editor log |
| `Live` | `false` | Auto-regenerate every 0.5s while editing — turn off before saving |

## StepCurve cheatsheet

The `StepCurve` field controls how densely the path is sampled. Denser sampling = more vertices = smoother curves, but more triangles. The format is a comma-separated list of `{percent:step}` pairs, where `percent` is your position along the path (0–100) and `step` is the world-distance in metres between samples at that point.

Three useful starting curves:

| Curve | Effect |
|---|---|
| `{0:1},{100:1}` | Constant 1m spacing along the whole path. **The default.** Good for most roads. |
| `{0:0.5},{50:2},{100:0.5}` | Dense at the start and end, sparse in the middle. Use for paths that have tight curves at both ends and a long straight in the middle. |
| `{0:0.25},{100:0.25}` | High-fidelity 0.25m spacing everywhere. Use for short, twisty paths where you can afford the triangle count. |

Tips:
- Click the `Readme` button on the entity to print the syntax help to the editor log.
- A typo in one pair (missing colon, junk text) is silently skipped — the rest still works. If you typo every pair, the curve falls back to the default and a yellow warning appears in the log.
- Braces are optional. `0:1,50:0.5,100:1` works the same as `{0:1},{50:0.5},{100:1}`.

## Troubleshooting — `TAOM:` lines in the editor log

If `Generate` does nothing, check the editor's log window for `TAOM:` lines (yellow = warning, white = info).

**Step 1 — confirm the click is reaching the script.** Look for a white line like:

```
TAOM: CS_Road on '<entity>': Generate button clicked.
TAOM: CS_Road on '<entity>': GenerateMesh start.
```

If you don't see "Generate button clicked" after pressing the button, the editor isn't routing the event to this script — re-select the entity, re-attach the component, or restart the editor.

**Step 2 — if you see "GenerateMesh start" but no success line**, look for a yellow warning explaining what bailed:

| Warning text | What you forgot / what to do |
|---|---|
| `GameEntity is not valid` | The host entity got invalidated. Re-attach `CS_Road` to a fresh entity. |
| `Width must be a finite value > 0` | Reset `Width` to the default `4` (or any positive number). |
| `ElevationOffset must be finite` | Reset `ElevationOffset` to the default `0.1`. |
| `RepeatU/RepeatV must be finite` | Reset both to the default `1`. |
| `Material is not set` | Pick a material in the `Material` field. |
| `Scene is null (editor context not yet ready?)` | The editor was still loading. Wait a moment and click `Generate` again. |
| `PathName is empty` | Fill in `PathName`. |
| `no path named '<X>' found in scene` | `PathName` doesn't match the Path entity's name. Check spelling and case. |
| `path '<X>' has TotalDistance=0` | The Path entity exists but has no length — drop more control points. |
| `produced only <N> sample(s)` | `StepCurve` is too sparse for this path length. Lower the step values (e.g. `{0:0.5},{100:0.5}`). |
| `geometry builder returned 0 triangles from <N> samples` | Internal — should not happen with a valid path. File a bug. |
| `StepCurve '<X>' is malformed; falling back to constant step 1` | The curve had no parseable pairs. Click the `Readme` button to see the syntax. |

**Step 3 — if you see the success line but no road appears in the viewport**, the mesh is generating but invisible. Look for a white line like:

```
TAOM: CS_Road on '<entity>': generated mesh from path '<X>' (totalDistance=42.50m, 86 samples, 170 triangles, material='dirt_road_a').
```

If you see that and still see no road, the mesh exists but isn't visible. Check:
- Camera angle — toggle `FlipFaces` (the road may be facing away from the camera).
- Material — pick a more obvious material temporarily (bright colour) to confirm the geometry is there.
- `ElevationOffset` — raise it (e.g. to `1.0`) to lift the mesh clearly above terrain in case it's clipping.
- Entity transform — the mesh is positioned in scene world coordinates from the path; the host entity's transform doesn't move it. If the host entity is far from the path, that's fine.

## Cleanup gotcha (read before you delete a `CS_Road`)

When `CS_Road` runs `Generate`, it attaches a generated mesh named `taom_cs_road_generated` to the host entity. **If you delete the `CS_Road` script after saving, that mesh stays attached** as an orphan — the script is no longer there to clean it up.

Two safe options:
- **Before deleting the script:** the script's own `OnRemoved` handler will clean up the mesh — so deleting via the property panel works cleanly. The orphan only happens if the script was already removed in a prior session and you're now opening the saved scene.
- **After-the-fact cleanup:** find the entity, look at its component list, find the mesh named `taom_cs_road_generated`, and remove it manually.

## `Live` mode — turn it off before saving

`Live = true` regenerates the mesh every 0.5s while you edit. Useful when you're dragging path control points and want to see the road update in real time. **Always set it back to `false` before saving the scene** — you don't want a shipped scene burning regen cycles every half-second when there's nothing to preview.

## When you've finished

Save the scene. The mesh persists. On scene reload (in editor or in-game), `CS_Road` re-runs once at init and re-attaches the mesh — no manual `Generate` needed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/scene-scripts.md](../features/scene-scripts.md)

<!-- backlinks-end -->
