# Scene Scripts

## Overview

Custom `ScriptComponentBehavior` subclasses that TAOM's map authors can attach to scene entities in Bannerlord's built-in scene editor. The first script is `CS_Road` — a procedural road/river mesh generator that walks a named scene Path entity, samples it at adaptive spacing, and builds a quad-strip mesh between path edges.

## Why This Exists

A TAOM map author wanted Alliance mod's `CS_Road.cs` so they could paint roads/rivers along scene paths instead of hand-placing individual mesh tiles. The Alliance code is GPL v3 (copyleft), so a verbatim copy would force TAOM into GPL v3 too. Instead, we did a clean-room rewrite: read the Alliance source once to extract a behavioural spec, then implemented from the spec without re-reading the source.

- **Vanilla behavior:** Bannerlord ships scene script primitives (`UsableMachine`, `SiegeWeapon`, etc.) but no procedural mesh generation. Map authors must place individual road tiles.
- **TAOM requirement:** A reusable scene-script library so map authors can express "road follows this path with this width and material," and the engine builds the geometry automatically. Live preview in the editor.
- **Without this feature:** Map authors hand-place dozens of road tile prefabs per scene; iteration on path shape is tedious; ad-hoc copies of Alliance's GPL code would contaminate TAOM's license.

## Architecture

### Design Challenge

1. **License hygiene.** Alliance is GPL v3. Verbatim copy means TAOM-as-a-whole must distribute under GPL v3. Solution: clean-room rewrite from a behavioural spec, documented in `docs/scene-scripts/ATTRIBUTION.md`.
2. **Engine discovery contract.** Bannerlord v1.3.15 `ScriptComponentBehavior.CollectEditableFields` enumerates **public instance fields** (not properties) for editor exposure. Auto-properties would not appear in the editor's component panel.
3. **Reflection-only registration.** Scene scripts are discovered when their containing DLL loads — no IoC, no SubModule.xml entry. TAOM.dll is already loaded by the active TAOM module, so simply adding `CS_Road : ScriptComponentBehavior` to it makes the script available.
4. **Thin-entry-point ADR-002.** The class can't shrink below ~200 lines because every editor knob must be a class field and every lifecycle method must be overridden in the same class. The fix: keep `CS_Road` as the engine-boundary class and push every line of algorithmic logic into pure C# helpers (parsers, evaluator, geometry builder, sampler, attacher). The helpers are 100% unit-tested.

### Solution Approach

```
Bannerlord scene editor
        │ (reflects on public fields, calls lifecycle methods)
        ▼
CS_Road  ── public instance fields with [EditableScriptComponentVariable]
        │
        ├─→ StepCurveParser.TryParse(StepCurve)   ── parse the "{pct:step},…" string
        ├─→ StepCurveEvaluator.Evaluate(curve, p) ── linear-interpolate at progress p
        ├─→ RoadPathSampler.SampleDistances(...)  ── adaptive distance walk along path
        ├─→ RoadGeometryBuilder.Build(samples)    ── perpendicular vertices + UVs + triangles
        ├─→ HexColorParser.ToPackedArgb(color)    ── "#rrggbbaa" → ARGB uint
        └─→ RoadMeshAttacher.BuildAndAttach(...)  ── Mesh + MetaMesh + entity.AddMultiMesh
```

The entry point's body is purely orchestration + validation + lifecycle handling. All algorithmic logic is in the helpers, which take simple value types (`IReadOnlyList<StepKey>`, `Vec3`, `Vec2`, primitive floats) and have no engine-side dependencies beyond `TaleWorlds.Library` value structs.

### Engine integration semantics

- **Field reflection** — verified against `docs/scene-scripts/sigs/scriptcomponentbehavior-v1.3.15.txt` lines 339-398. Public instance fields are auto-discovered. Private fields (`_parsedCurve`, `_lastGenerated`, `_liveAccumulator`) are skipped.
- **`SimpleButton` fields** — `Generate` and `Readme` are typed `SimpleButton` (a marker class). The editor renders them as buttons; clicking calls `OnEditorVariableChanged(fieldName)`.
- **Override accessibility** — base methods are `protected internal virtual` in `TaleWorlds.Engine`. Cross-assembly overrides MUST use `protected override` (not `protected internal override`), because the `internal` portion of `protected internal` is inaccessible from outside the declaring assembly.
- **`WeakGameEntity` vs `GameEntity`** — `ScriptComponentBehavior.GameEntity` returns `WeakGameEntity` (a struct holding a `UIntPtr`). Use `IsValid` instead of null checks. `WeakGameEntity` exposes `AddMultiMesh` and `RemoveMultiMesh` directly.
- **`MatrixFrame.rotation.s`** — the "side" basis vector from `Mat3 rotation`, already perpendicular to forward and up, unit-length. Use it directly for the quad's perpendicular offset; no cross-product needed.

## Configuration

There are no XML/JSON config files for this feature. All configuration lives on each `CS_Road` instance in the scene, exposed as editor fields.

### CS_Road editor fields

| Field | Type | Default | Purpose |
|---|---|---|---|
| `PathName` | string | `""` | Name of the scene's Path entity to follow |
| `Width` | float | `4f` | Half-width of the road quad (total spread = 2 × Width) |
| `ElevationOffset` | float | `0.1f` | `+Z` lift to keep mesh above terrain |
| `StepCurve` | string | `"{0:1},{100:1}"` | Adaptive sample spacing curve |
| `Material` | Material | (picker) | Editor-assigned material |
| `CustomColor` | string | `"#ffffffff"` | RGBA hex; 7 or 9 chars |
| `RepeatU`, `RepeatV` | float | `1f` | UV tile counts |
| `InvertU`, `InvertV`, `RotateUV` | bool | `false` | UV transforms |
| `FlowDirection` | enum | `AlongU` | Which axis the texture flows along |
| `FlipFaces` | bool | `false` | Reverse triangle winding |
| `Generate` | SimpleButton | — | Manual regeneration trigger |
| `Readme` | SimpleButton | — | Logs StepCurve syntax help |
| `Live` | bool | `false` | Auto-regenerate every 0.5s while editing |

### StepCurve format

Pairs separated by commas: `{0:0.5},{50:2},{100:0.5}`. Each pair is `{percent:step}` (braces optional). Percent ∈ [0, 100]; step is world-units between samples at that point. Linear interpolation between adjacent keys.

- Lenient parser: a typo in one pair skips that pair and keeps the rest.
- Full fallback: empty input or zero parseable pairs → default `(0,1)…(100,1)` (constant step 1).
- NaN/Infinity in any pair → that pair is skipped.

## Key Files

| File | Purpose |
|---|---|
| `Main/SceneScripts/CS_Road.cs` | Entry point. Public fields + 5 lifecycle overrides + thin orchestration |
| `Main/SceneScripts/Roads/HexColorParser.cs` | Parse `"#rrggbb[aa]"` → ARGB uint |
| `Main/SceneScripts/Roads/StepCurveParser.cs` | Parse `"{pct:step},…"` → sorted list of keys |
| `Main/SceneScripts/Roads/StepCurveEvaluator.cs` | Linear-interpolate the parsed curve at a percent |
| `Main/SceneScripts/Roads/RoadPathSampler.cs` | Adaptive distance walk along a path |
| `Main/SceneScripts/Roads/RoadGeometryBuilder.cs` | Quad-strip vertex / UV / triangle generation |
| `Main/SceneScripts/Roads/RoadMeshAttacher.cs` | Build `Mesh` + `MetaMesh`, attach to entity, remove previous |
| `Main/SceneScripts/Roads/StepKey.cs` | `(percent, step)` value struct |
| `Main/SceneScripts/Roads/RoadSampleFrame.cs` | `(origin, side, distance)` value struct |
| `Main/SceneScripts/Roads/RoadTriangle.cs` | 3 positions + 3 UVs value struct |
| `Main/SceneScripts/Roads/FlowAxis.cs` | `AlongU` / `AlongV` enum |
| `docs/scene-scripts/specs/cs-road.md` | Behavioural spec (clean-room input) |
| `docs/scene-scripts/ATTRIBUTION.md` | Clean-room procedure + Alliance credit |
| `docs/scene-scripts/sigs/*.txt` | Pinned v1.3.15 ilspycmd outputs |

## Dependencies

- `TaleWorlds.Engine` — `ScriptComponentBehavior`, `Scene`, `Path`, `Mesh`, `MetaMesh`, `Material`, `GameEntity`, `WeakGameEntity`, `SimpleButton`
- `TaleWorlds.DotNet` — `EditableScriptComponentVariable` attribute
- `TaleWorlds.Library` — `Vec3`, `Vec2`, `MatrixFrame`, `Debug.Print`
- `TAOM.Core.Validation.FiniteFloatValidator` — NaN/Infinity guards on editor floats

## Tests

- `TAOM.Tests/SceneScripts/Roads/HexColorParserTests.cs` — 14 tests (7/9-char parsing, invalid input, ARGB packing)
- `TAOM.Tests/SceneScripts/Roads/StepCurveParserTests.cs` — 16 tests (braces, whitespace, malformed, NaN/Infinity, sort, fallback)
- `TAOM.Tests/SceneScripts/Roads/StepCurveEvaluatorTests.cs` — 14 tests (lerp, clamp, single-key, null/empty)
- `TAOM.Tests/SceneScripts/Roads/RoadGeometryBuilderTests.cs` — 14 tests (vertex count, perpendicular math, UV bounds, FlipFaces, InvertU, RotateUV)
- `TAOM.Tests/SceneScripts/Roads/RoadPathSamplerTests.cs` — 9 tests (distance walk, NaN/Infinity guards, dense-curve density check)

67 unit tests total. `CS_Road.cs` itself is engine-bound and is tested manually in the editor (see "How to verify in editor" below).

## How to add a new scene script (clean-room from external inspiration)

1. **Read the external source once.** Extract a behavioural spec into `docs/scene-scripts/specs/<script-name>.md` — describe properties, lifecycle methods, observable behaviour. No code snippets.
2. **Write the implementation from the spec.** Helper-class split → unit tests → engine-boundary entry point. Don't re-read the external source.
3. **Cross-check pass.** Re-read the external source one final time. If any structural collision (same private method names, identical helper-class split, identical loop structure), restructure.
4. **File header on every TAOM file** porting external inspiration: cite source + spec doc + ATTRIBUTION.md.
5. **Append to `docs/scene-scripts/ATTRIBUTION.md`** with the new entry.

## How to verify CS_Road in the editor (hand-off checklist for map maker)

1. Open Bannerlord scene editor with TAOM + TAOM_Map active.
2. Open a test scene; add a generic entity.
3. Component picker → search "CS_Road" → attach.
4. Verify all 16 editor fields appear with correct defaults (cross-check against `docs/scene-scripts/specs/cs-road.md`).
5. Click the entity, click `Readme` button → editor log shows StepCurve format help.
6. Create a Path entity, name it `test_path`, draw a few control points.
7. On the CS_Road entity, set `PathName = "test_path"`, pick a material, click `Generate`.
8. Confirm a road quad appears along the path.
9. Tweak `Width`, `ElevationOffset`, `RepeatU/V`, `InvertU/V`, `FlipFaces`, `FlowDirection` — observe each takes effect on next `Generate`.
10. Set `StepCurve = "{0:0.5},{50:2},{100:0.5}"` → confirm denser sampling at start/end, sparser in middle.
11. Enable `Live = true` → move path control points → confirm mesh regenerates within ~0.5s.
12. Save scene, reload, confirm the component re-resolves cleanly and mesh re-appears on play.

## Known limitations

- **MetaMesh cross-session lifecycle:** If a scene is saved with a `CS_Road`-generated MetaMesh attached, then the `CS_Road` script is later removed, the MetaMesh is orphaned in the scene (named `taom_cs_road_generated`). The map maker can find and remove it manually from the entity's component panel. Workaround: remove the generated MetaMesh from each affected entity before removing the script.
- **`Live` mode regen cost:** Each regen creates a new `Mesh` + `MetaMesh`. At 0.5s throttle, this is fine for editing sessions but should not stay enabled for shipped scenes. Default is `false`.
- **Cubic interpolation:** Step curve uses linear interpolation between keys. No cubic option.

## Triage of other Alliance CustomScripts (deep-dived, deferred or skipped)

Alliance's `Alliance.Common/Extensions/CustomScripts/Scripts/` folder has 13 scene scripts. Only CS_Road was ported — the rest are documented here with realistic complexity notes for future scoping.

| Script | Verdict | Realistic complexity |
|---|---|---|
| CS_Array | **Defer** | ~291 lines SP-clean but depends on Alliance's `EntityUtils` blend math (path-following + influence weights). Simplified port (basic offset duplication only) is ~150 lines. Worth doing as a follow-up if CS_Road lands well. |
| CS_TextPanel | **Defer — blocked** | ~100 lines, but depends on Alliance's opaque `EnqueueTextPanel` glyph-mesh generator. Porting requires building a fresh glyph atlas system. Multi-week subproject. |
| CS_StateObject | **Defer — needs SP redesign** | ~310 lines on `SynchedMissionObject`. State-machine concept (states-as-child-entities) is sound but MP plumbing dominates. If TAOM needs a destructible/state system, design SP-native from scratch using this as conceptual reference. |
| CS_UsableObject | **Defer — blocked** | ~245 lines on `UsableMachine`. Depends on Alliance's custom `AnimationPlayer.AnimationSystem.Instance` — that's its own multi-hundred-line port. Use TaleWorlds' native `UsableMachine` + vanilla `act_*` action lookup instead if TAOM ever needs this. |
| CS_DestructibleWall | **Skip** | ~823 lines on `SynchedMissionObject`. MP-heavy. Has a TODO in source admitting the original devs couldn't get progressive destruction working ("clamped to 1 HP per hit"). Fragile + huge — not worth the salvage. |
| CS_StandingPoint | **Defer** | ~120 lines on `StandingPoint`. Depends on Alliance's `AnimationSystem.Instance` — same blocker as CS_UsableObject. |
| CS_StandingPointWithItemRequirement | **Defer** | ~65 lines on `StandingPoint`. Transitive dep on CS_StateObject (calls parent.SetState). Re-port if CS_StateObject ever ships. |
| CS_HealingObject | **Skip** | Subclass of CS_UsableObject → blocked. SP-clean conceptually but no value w/o the base. TAOM's healing model lives in `TaomPartyHealingModel` anyway. |
| CS_RefillAmmo | **Skip** | Subclass of CS_UsableObject → blocked. MP-flavoured (siege ammo restock); not relevant to SP campaign. |
| CS_ShieldRepair | **Skip** | Subclass of CS_UsableObject → blocked. Trivial logic; not worth the base-class cost. |
| CS_PartyHard | **Skip** | MP-only; admin-gated particle/sound trigger; no SP analogue. |
| CS_EditorFixer | **Skip** | Editor utility for fixing prefab `VectorArgument2` import bugs in Alliance's own pipeline. Doesn't affect gameplay. Not applicable to TAOM. |

## Performance

Editor-only feature with 0.5s minimum regen interval in Live mode. Per regeneration: 1 `List<float>` (distances) + 1 `List<RoadSampleFrame>` + 4 small arrays (lefts/rights/leftUVs/rightUVs) + 1 `List<RoadTriangle>` + 1 native `Mesh` + 1 native `MetaMesh`. Allocations are bounded and short-lived; GC pressure is acceptable for editor use.

## GitHub Issue

- **Issue:** [#119](https://github.com/haterade22/TAOM/issues/119) — Scene scripts library: CS_Road procedural mesh generator (clean-room port)
- **Status:** TBD (closed at commit time)
