# CS_Road — Behavioural Spec (input to clean-room rewrite)

**Source read once:** `Byak0/Alliance@version/0.6.0.0:Alliance.Common/Extensions/CustomScripts/Scripts/CS_Road.cs` (~380 lines, GPL v3). No code from this file is reproduced below — only the observable behaviour, property surface, and algorithmic description.

## Class shape

- Derives `ScriptComponentBehavior` (in `TaleWorlds.Engine`, see `docs/scene-scripts/sigs/scriptcomponentbehavior-v1.3.15.txt`).
- One-line purpose: procedurally generate a road, river, or strip mesh along a named scene Path entity, with adaptive sample spacing.
- Editor discovers it by reflection on loaded DLLs that contain `ScriptComponentBehavior` subclasses with public instance **fields** (Bannerlord v1.3.15 scans fields, not properties — see `CollectEditableFields` in the decompile).

## Editor-visible fields

Every entry below is a **public instance field** (the editor enumerates fields, not properties). `[EditableScriptComponentVariable(true)]` may be redundant for public fields but is set explicitly for clarity.

| Field name | Type | Default | What it controls |
|---|---|---|---|
| `PathName` | `string` | `""` | Name passed to `Scene.GetPathWithName`. Generation aborts if no path is found. |
| `Width` | `float` | `4f` | Half-width of the road quad (actual perpendicular spread = `2 * Width`). |
| `ElevationOffset` | `float` | `0.1f` | Extra `+Z` lift applied to every sampled point to keep the quad above terrain. |
| `StepCurve` | `string` | `"{0:1},{100:1}"` | Adaptive sample-spacing curve. See "StepCurve format" below. |
| `Material` | `Material` (`TaleWorlds.Engine`) | `null` | Editor picker. Generation aborts on null. |
| `CustomColor` | `string` | `"#ffffffff"` | RGBA hex. 7 or 9 chars (`#rrggbb` or `#rrggbbaa`). |
| `RepeatU` | `float` | `1f` | UV tile count along the flow axis. |
| `RepeatV` | `float` | `1f` | UV tile count across the road width. |
| `InvertU` | `bool` | `false` | Replace each U with `1 - U`. |
| `InvertV` | `bool` | `false` | Replace each V with `1 - V`. |
| `RotateUV` | `bool` | `false` | Swap U ↔ V (90° rotation in texture space). |
| `FlowDirection` | enum (`AlongU` \| `AlongV`) | `AlongU` | Which axis the texture flows along (i.e. which axis is the path-progress axis). |
| `FlipFaces` | `bool` | `false` | Reverse triangle winding (for facing the other way / back-face culling). |
| `Generate` | `SimpleButton` (`TaleWorlds.Engine`) | (button) | Editor button — clicking re-runs generation. |
| `Readme` | `SimpleButton` | (button) | Editor button — logs StepCurve format help. |
| `Live` | `bool` | `false` | When true, OnEditorTick regenerates every 0.5s for live preview. |

## Lifecycle method overrides

All four are `protected internal override` (matching the base class accessibility — verified in the decompile).

- `OnInit()` — parse `StepCurve`; call `Generate()`. Runs at mission start.
- `OnEditorInit()` — same as `OnInit()` but in editor context.
- `OnEditorVariableChanged(string variableName)` — routing:
  - `"Generate"` field changed (button click) → `Generate()` regardless of `Live` state.
  - `"Readme"` field changed (button click) → log a one-paragraph help string for the StepCurve format.
  - `"StepCurve"` field changed → re-parse; if `Live`, also `Generate()`.
  - Any other field changed → if `Live`, `Generate()`. Otherwise no-op (waits for explicit click).
- `OnEditorTick(float dt)` — accumulator. When `Live==true` and accumulator ≥ 0.5s, call `Generate()` and reset accumulator. When `Live==false`, no-op. Edit-tick is opt-in for ScriptComponentBehavior; no `SetScriptComponentToTick` flag is needed because `OnEditorTick` runs unconditionally for any subclass that overrides it (see decompile — it's a `protected internal virtual` empty body, no opt-in gate).

## StepCurve format

Used to vary sample density along the path. Denser sampling = more vertices = better curve fidelity at the cost of triangle count.

- Pairs separated by commas: `{0:1},{50:0.5},{100:2}`
- Each pair: `{percent:step}`. Braces are optional. Whitespace ignored.
- `percent` ∈ [0, 100]; values outside this range are clamped during evaluation.
- `step` is the world-distance per sample at that percent (path units, typically metres).
- Parse → list of `(percent, step)` pairs sorted ascending by percent.
- Evaluate at progress `p` ∈ [0, 100] → linear interpolation between the two adjacent keys. Before the first key, return the first key's step. After the last key, return the last key's step.
- **Lenient parsing:** individual malformed pairs (missing colon, non-numeric token, empty after splitting) are SKIPPED. The rest of the input is preserved if any valid pair remains. This matches map-maker workflow expectations — a typo in one pair shouldn't invalidate the whole curve.
- **Full fallback** (empty string, whitespace-only, or zero parseable pairs): degrade to the default curve `[(0, 1), (100, 1)]` — i.e. constant step `1`. The CALLER (CS_Road) logs a warning when input was non-empty but unparseable. Don't throw.
- **Minimum-step guard:** any parsed step ≤ 0 is replaced with `MinStep` (0.1) to prevent infinite loops in the sampling walk.
- **NaN/Infinity guard:** any pair containing NaN or ±Infinity in either percent or step is treated as malformed and skipped.

## Sampling walk + geometry generation

Pseudocode (intent — not Alliance's code):

```
path = Scene.GetPathWithName(PathName)
if path is null: log warn, return
totalDistance = path.TotalDistance
if totalDistance ≤ 0: return

samples = []
d = 0
while d < totalDistance:
  frame = path.GetFrameForDistance(d)
  samples.Add(frame)
  step = StepCurveEvaluator.Evaluate(parsedCurve, d / totalDistance * 100)
  d += max(step, 0.1)

// Always include the endpoint exactly so the strip closes:
samples.Add(path.GetFrameForDistance(totalDistance))
```

Per sample frame:
- `origin` is the path point.
- `rotation.s` is the path's "side" basis vector (already perpendicular to `forward` and `up`, length 1 — see `MatrixFrame` in `TaleWorlds.Library`).
- `rotation.u` is "up".
- `rotation.f` is "forward" (tangent).

Left and right edge vertices for sample `i`:

```
center = samples[i].origin + Vec3(0, 0, ElevationOffset)
side   = samples[i].rotation.s          // already unit-length, perpendicular to forward+up
left   = center + side * Width
right  = center - side * Width
```

(`Width` is the half-width, so total quad width is `2 * Width`.)

## Mesh construction

Bannerlord v1.3.15 `Mesh` API (per `docs/scene-scripts/sigs/mesh-v1.3.15.txt`):

```
var mesh = Mesh.CreateMeshWithMaterial(Material);
var lockHandle = mesh.LockEditDataWrite();
try {
    foreach quad between sample i and i+1:
        mesh.AddTriangle(p1, p2, p3, uv1, uv2, uv3, 0xFFFFFFFFu, lockHandle);   // 2 triangles per quad
} finally {
    mesh.UnlockEditDataWrite(lockHandle);
}
mesh.Color = HexColorParser.ToPackedArgb(CustomColor);
mesh.RecomputeBoundingBox();
```

Triangle ordering per quad (sample `i` left=L_i, right=R_i; sample `i+1` left=L_{i+1}, right=R_{i+1}):

```
Triangle A: L_i,   R_i,   R_{i+1}
Triangle B: L_i,   R_{i+1}, L_{i+1}
```

If `FlipFaces == true`, reverse vertex order in both triangles (or swap two vertices).

## UV assignment

Each vertex receives a `Vec2` UV. Two coordinate axes to choose between:

- **`AlongU`** flow: U = accumulated path distance × `RepeatU / totalDistance`, V ∈ {0, 1} × `RepeatV` (per left/right edge).
- **`AlongV`** flow: V = accumulated path distance × `RepeatV / totalDistance`, U ∈ {0, 1} × `RepeatU`.

Apply, in this order:
1. `RotateUV` → swap U ↔ V on each vertex.
2. `InvertU` → `U = 1 - U` (or `RepeatU - U` depending on whether tile count should mirror — keep simple: `U = baseRepeat - U`).
3. `InvertV` → same.

(The mirror-around-tile-count form keeps texture tiling intact when inverting. Verify visually with the map maker during handoff.)

## Replace prior generated mesh

Each `Generate()` call must remove the previously-generated MetaMesh before adding the new one. Otherwise repeated generation stacks meshes on the entity.

Strategy: hold a reference to the most-recently-added `MetaMesh` (instance field on CS_Road, non-editable). On Generate:
1. If `_lastGenerated != null` and `GameEntity` is non-null: `GameEntity.RemoveMultiMesh(_lastGenerated)`.
2. Build new mesh + MetaMesh.
3. `GameEntity.AddMultiMesh(metaMesh)`.
4. `_lastGenerated = metaMesh`.

## Live mode

When `Live == true`, OnEditorTick accumulates `dt` and triggers `Generate()` every 0.5s. Default to `false` (regeneration is non-trivial; map makers should opt in only for preview sessions and remember to disable before saving).

## Error / abort conditions

- Path not found → log warning, no-op.
- `TotalDistance` is 0 → no-op.
- `Material` is null → log warning, no-op.
- Malformed `StepCurve` → use default `(0,1)…(100,1)`, log warning, continue.
- `Width` ≤ 0 → no-op (degenerate quads).

## Dependencies replaced by TAOM-owned helpers

- Alliance `EntityUtils.ColorFromHex` → TAOM `HexColorParser` (clean-room).
- Alliance `Logger` → TAOM logging (whatever pattern the rest of the codebase uses).

## Out of scope for this spec (and the implementation)

- Mesh caching across regenerations (always rebuild).
- LOD generation (single LOD).
- Material per-segment (single material across the whole road).
- Cubic Hermite interpolation on the step curve (linear only).
- Path point editing (we read the path; the map maker draws it via the editor's path tools).
