# Bannerlord scene objects — GameEntity / ScriptComponentBehavior / Prefab (Phase 8)

> **One process, traced from the decompile** (`TaleWorlds.Engine`, v1.4.5): the scene-object system — how a scene
> is built from `GameEntity`s, how C# logic (`ScriptComponentBehavior`) attaches to them via prefabs, and how the
> editor surfaces per-instance config. This is the backbone under TAOM's `Main/SceneScripts` (CS_Road, etc.) and
> the howdah prefab. Part of the phased engine study.

## WHAT it is

A mission/campaign scene is a tree of **`GameEntity`** nodes (each with a transform, optional meshes, physics, and
attached scripts). **`ScriptComponentBehavior`** is the engine-discovered C# logic you attach to an entity. A
**prefab** (XML) is an authored entity-tree (meshes + scripts + children + per-instance variable values) that code
or the editor instantiates. Map authors compose entities in the Modding Kit; code drives them by name.

## HOW it works

### `GameEntity` (GameEntity.cs:10 — `sealed class GameEntity : NativeObject`)
A managed handle over a native scene-graph node. Key API (mostly native calls):
- **Instantiate from a prefab:** `Instantiate(Scene, string prefabName, bool callScriptCallbacks, bool createPhysics=true, string scriptInclusingTag="")` (379), `Instantiate(Scene, prefabName, MatrixFrame, …)` (398), `InstantiateWithRestOffset(…)` (403). Creates the entity tree (+ its scripts) from the named prefab.
- **Transform:** `SetFrame(ref MatrixFrame, …)`, `GetFrame`, `GetGlobalFrame` — position/rotation/scale.
- **Hierarchy:** `GetChild`/`GetChildren`, `AddChild`, `Parent`, `Remove`.
- **Find scripts:** `GetFirstScriptOfType<T>()` (516), `GetFirstScriptOfTypeRecursive<T>()` (529),
  `GetFirstScriptOfTypeInFamily<T>()` (504) — locate an attached `ScriptComponentBehavior`.
- (`WeakGameEntity` is the non-owning handle variant — same query API.)

### `ScriptComponentBehavior` (ScriptComponentBehavior.cs:9 — `abstract : DotNetObject`)
The engine-discovered script base. A subclass becomes attachable to an entity by **its class name**.
- **Discovery:** at startup the static ctor runs `CacheEditableFieldsForAllScriptComponents()` (78) — the engine
  enumerates every `ScriptComponentBehavior` subclass and caches its **`[EditableScriptComponentVariable]`** fields
  (the per-instance config the editor surfaces). So just *defining* a subclass makes it discoverable.
- **Attachment + lifecycle:** when a prefab's `<script name="X">` is instantiated, the engine creates the `X` instance,
  calls `Construct(entityPtr, scriptComponent)` (82) to wire it to its `GameEntity`/`Scene`, sets the editable-variable
  values from the prefab, then drives the `[EngineCallback]` virtuals:
  `OnInit()` (180) → `OnTick(dt)` (per cadence) → `OnRemoved(removeReason)` (191) (via `HandleOnRemoved` 185).
  Editor variants (`OnEditorInit`, `OnEditorTick`, `OnEditorVariableChanged`) run in the Kit.
- **Tick cadence — `TickRequirement`** (11-22): `None`/`TickOccasionally`/`Tick`/`TickParallel`/`TickParallel2/3`/
  `FixedTick`/`FixedParallelTick`. A script overrides `GetTickRequirement()` to declare *when/how* `OnTick` fires;
  `SetScriptComponentToTick(req)` updates it at runtime. (The howdah `OR`s `TickParallel` into its requirement so its
  per-tick neck-frame follow runs.)
- **Properties:** `GameEntity` (38), `Scene` (54) — the entity/scene the script lives on.

### Prefab XML
```
<game_entity>
  <components> … meshes / physics … </components>
  <script name="X"><variables><variable name="Width" value="3.5"/>…</variables></script>   ← attaches class X + sets its editable vars
  <children><game_entity> … </game_entity></children>                                         ← nested entities
</game_entity>
```
The `<script name>` must equal the `ScriptComponentBehavior` subclass name. `<variable>` values populate the
`[EditableScriptComponentVariable]` fields. ADOD_Beasts's howdah prefabs (`adod_howdah_{1,2,4}_agent`) are exactly this:
a root `<script name="ADODHowdahObject">` + child `<game_entity>`s carrying `<script name="ADODHowdahStandingPoint">`.

## WHY it's shaped this way

The scene graph + script-component pattern is the engine's ECS-ish composition: artists assemble entities (meshes,
collision, scripts) in the editor with no code, and programmers attach behavior to entities by class name + tune it
via editor variables. The `[EngineCallback]` virtuals are the native→managed hooks (the native scene driver calls
`OnInit`/`OnTick`/`OnRemoved`); `GetTickRequirement` lets a script opt into the tick passes it needs (and off when
idle, for perf).

## TAOM relevance + gotchas
- **`Main/SceneScripts`** are `ScriptComponentBehavior` subclasses (e.g. `CS_Road`) — engine-discovered **by class
  name**, attached via prefabs/scenes authored in the Kit. No registration needed beyond defining the class.
- **`[EditableScriptComponentVariable]` fields ARE config** — the map author/player edits them in the editor. They
  must be **validated** (NaN/range/finite) before use, exactly like JSON/MCM config. This is the recurring NaN-gate
  bug class (`.claude/rules/config-providers.md` "Config Providers MUST Validate" item 3; [lesson](../../reviews/lessons/gamemodels-services.md)
  — shipped 3×: Career #31, EditorCacheRebuild #38, CS_Road 2026-05-13). Use `FiniteFloatValidator`.
- **The elephant howdah** is a `UsableMachine` subclass (`TaomHowdahMachine`) in a prefab, instantiated per elephant via
  `GameEntity.Instantiate(scene, ElephantConfig.HowdahPrefabName, true)` (`ElephantMissionBehavior.cs:99`); how it is positioned is in the howdah doc.
- **Editor-only script types** (e.g. `AnimalSpawnSettings : ScriptComponentBehavior`, with a `SpawnerPermissionField
  : EditorVisibleScriptComponentVariable`) exist only in the wEditor build — check `_editor_build` for scene-authoring
  scripts (Phase-0 lookup order).
- `<script name>` must exactly match the class name, and the class must be in a loaded module assembly, or the prefab
  silently has no script.
- **A run-time prop on the campaign map is not saved.** Map `GameEntity` objects are not serialized, so recreate them
  on load and after map re-entry from your own saved state. [yotthani, `yotthani/bannerlord`
  `TAOM_POI/docs/2026-07-06-feasibility-runtime-map-props.md`@c45d8374, read from the 1.4 decompile and said to be proven in
  `HoN/FieldCamp`; not re-checked by TAOM.] The public path in that memo: `MapScreen.Instance?.MapScene`,
  `MetaMesh.GetCopy(name, false, true)`, `GameEntity.CreateEmpty(scene)`, `AddMultiMesh`, `SetGlobalFrame` with the terrain
  height from `MapSceneWrapper.GetHeightAtPoint`, and `e.Remove(0)` to clean up. The prop has no interaction: selection
  and encounter need a settlement or proximity anchor. TAOM's own map-scene entity user is `BorderRenderAdapter.AddEntity`
  (`BorderRenderAdapter.cs:474`, `GameEntity.CreateEmpty`).

## Siege walls: two child scripts the set-up reads with no null check

A siege mission's `SiegeMissionPreparationHandler.SetUpScene` runs `ArrangeDestructedMeshes`
(`SiegeMissionPreparationHandler.cs:56, 94`, v1.5.7). It takes every `WallSegment` whose `DefenseSide` is set and that
has a direct child tagged `broken_child` (`:142`), and calls `WallSegment.OnChooseUsedWallSegment` on it (`:150-161`).
That method takes the first direct child tagged `solid_child` and the first tagged `broken_child`
(`WallSegment.cs:145-146`; `WeakGameEntity.GetFirstChildEntityWithTag` reads direct children only,
`WeakGameEntity.cs:567`) and calls `GetFirstScriptOfType<WallSegment>()` on each, with no null check, in both branches
(`:150-151` for a breached wall, `:253-254` for a solid one).

So this is what a breachable wall needs in the scene file:

| Entity | Needs |
|---|---|
| The wall | A `WallSegment` script whose `SideTag` is `left`, `middle` or `right` (`WallSegment.cs:85` maps only these to a side) |
| | A direct child tagged `solid_child` and a direct child tagged `broken_child` |
| The `solid_child` | Its own `WallSegment` script (the defaults are right: every navmesh id `-1`, an empty `SideTag`) |
| The `broken_child` | Its own `WallSegment` script. `visible="false"` is the convention, not a need: the set-up hides this child on a solid wall and shows it on a breached one (`WallSegment.cs:151, 254`), so the flag decides only what shows when the set-up fails |

A child that has the tag and no script throws a `NullReferenceException` at the start of every siege mission on that
scene. [Mission Start Guard](../../features/mission-start-guard.md) catches it, writes one `[ERROR]` line and one
`[WARNING]` summary, and shows a notice, so the mission starts with the set-up half done. The exception ends the loop
over the walls, so a wall the loop had not reached yet is not arranged either. For a solid wall, the lines that
disable its over-debris navmesh faces (`WallSegment.cs:255-266`) come after the two script calls and never run;
whether those faces load enabled is native and UNVERIFIED. On 2026-11-10 that was Helm's Deep: an art pass had
replaced the right breach mesh and kept the tag but not the script, and the first battle on the scene ended in a
stand-off in wave 1 ([RCA](../../reviews/rca-helms-deep-siege-wall-2026-11-10.md)).

- **Gate:** `python tools/check_siege_wall_scripts.py` reads every `scene.xscene` of the live `TAOM_Map` (or of a
  folder given with `--scenes`, or the scene files named on its command line). For each sided wall that has a
  `broken_child`, it fails when the `solid_child` is missing or when either child has no script, and it fails a scene
  that holds an odd number of such walls. It runs in the tool suite and as the `siege-walls` row of
  `tools/data_gates.py`, which hands it the scene files you named.
- **Two walls and a ram:** `ArrangeDestructedMeshes` calls `FindRightMostWall` once per wall hit-point entry and
  removes the wall it gets (`SiegeMissionPreparationHandler.cs:145-158`). The campaign passes two entries
  (`Settlement.WallSectionCount` is 2 for a fortification, `Settlement.cs:149-158`) and so does The Long Defeat
  (`LongDefeatMissionLauncher.cs:110`); the Custom Battle caller was not traced. `FindRightMostWall` returns the wall
  when one is left. For any other count it first takes the mission's first `BatteringRam`, which throws when there is
  none, then picks one of two by position and returns `null` otherwise (`:183-199`). So a siege scene needs exactly
  two sided breach walls among the entities the mission loads, and a battering ram: one wall, or three, throws in the
  same set-up. Every TAOM siege scene has two today.
- **Not judged by the gate:** a wall placed as a bare prefab instance (its children are in the prefab file), the
  ram, and an even wrong number of walls. Base game scenes hold two walls for each of three upgrade levels in one
  file, and which entities a level loads is native and UNVERIFIED, so the gate fails only an odd number.
- A replaced breach mesh needs the tag and the script. A new mesh with only the tag is this failure.

## Editor-side navmesh and terrain API

Items tagged [Certain] were read in the v1.5.7 decompile (`E:\Decompiled_Bannerlord\_categories_v1.5.7\`) this turn. The
rest is Kysaro's (TAOM's lead scener) from his scene tool notes, unverified in TAOM.

- **`TerrainEditContext` writes terrain** [Certain]. `Engine/TaleWorlds.Engine/TaleWorlds.Engine/TerrainEditContext.cs`
  has a constructor that creates the terrain (`:77`), `SetHeightData(int nodeX, int nodeY, float[] heights)` (`:83`),
  layer and flora methods, and `FinalizeEditing()` (`:118`). The class is in the v1.5.5 and v1.5.7 decompiles and absent
  from v1.4.8. The first build that carries it was not checked here; Kysaro says 1.5.3 (his note
  `scenes/project_scene_forge.md`, 2026-11-12), and his older note calling terrain read-only from C# is outdated.
- **Per-face navmesh switches** [Certain]: `Scene.SetAbilityOfFacesWithId(int faceGroupId, bool isEnabled)` returns int
  (`Scene.cs:1458`); `Scene.SetAbilityOfFaceWithIndex(int faceIndex, bool isEnabled, bool updateIslandIndices)` returns
  bool (`Scene.cs:1468`). Whether they serve a `bo_` or slope filter is untested (his note
  `scenes/project_navmesh_generator.md`, 2026-09-13).
- **`Scene.SetNavMeshRegionMap(bool[] regionMap)` takes an array indexed by `TerrainType`** [Certain]. Vanilla builds it
  as `new bool[max(TerrainType) + 1]` and sets `array[terrainType]` from
  `model.IsTerrainTypeValidForNavigationType` (`Modules/SandBox/Sandbox/SandBoxHelpers.cs:425-432`,
  `GetRegionMapping`). Kysaro filled it per face and saw no effect: the wireframe did not change and Find Path still
  routed through the excluded prop (same note, 2026-09-13).
- **`GetNavMeshCenterPosition(int faceIndex, ref Vec3 centerPosition)` takes the position by `ref`** and returns void
  [Certain, `Scene.cs:696`].
- **`SaveScene` crashes (0xC0000409, a null `FILE*`) unless `<Module>\SceneEditData\<scene>\` exists** (it receives
  `terrain_ed.bin`). Kysaro measured it (his note `scenes/project_scene_forge.md`, 2026-11-12).
- **`ManagedMeshEditOperations.Create(mesh)` needs `FinalizeEditing()` before any `AddEditDataUser()` or
  `LockEditDataWrite()` on the same mesh.** Without it the engine deadlocks with no exception and no log; he hit it
  twice (his note `scenes/project_vertex_paint_module.md`, 2026-09-17). Wrap the operations in try/finally.
- **`MetaMesh.GetMeshAtIndex` is said to count only the active entries of the metamesh's mesh list** [yotthani, read from
  code, no in-game observation: `yotthani/MithrilForge` commit `854acad` ("keys meshes by ordinal among the ACTIVE metamesh
  entries (= engine GetMeshAtIndex)", cache format 2 to 3) and `SkinCacheFile.cs@ea7a7bfb:12`; the same statement in
  `yotthani/bannerlord` `HoN/RaceArmorFit` README, German, "liefert nur aktive Entries"]. A tool that indexes by raw entry
  position drifts as soon as an entry in the middle is inactive. **Not re-checked by TAOM:** on v1.5.7 the managed method is
  only `EngineApplicationInterface.IMetaMesh.GetMeshAtIndex(base.Pointer, meshIndex)` (`TaleWorlds.Engine.MetaMesh.cs:208-210`
  in the `taom-src` cache), so whether inactive entries count is native. Next check:
  `python tools/native_decompile.py --engine-method IMetaMesh.GetMeshAtIndex`. Neither `tools/validate_mesh_refs.py` (TOC
  only) nor TAOM's `Main` code indexes a metamesh's meshes by entry order today as far as this doc's searches went (scope:
  `docs`, `tools`, `.claude`, `Main`).
- **Vertex colour is four independent grayscale mask channels, not an RGB tint.** `Mesh.SetEditDataFaceCornerVertexColor`
  takes a packed uint: R ambient occlusion, G diffuse occlusion, B second material mask, A vertex alpha. Only R was
  confirmed live (R=0 darkens, R=255 is neutral); the packing order `(a<<24)|(r<<16)|(g<<8)|b` is his assumption (same
  note).
- **`GameEntity.SetForceDecalsToRender(true)` makes decals show on buildings that showed none** (two building types
  confirmed, same note, 2026-09-16 entries). **Never call `Scene.AddDecalInstance`:** it crashed the whole game process
  in his test.
- **`terrain_materials.xml` is one global file**, `Modules\Native\ModuleData\terrain_materials.xml`, read by every module
  (Native is a dependency of all of them). The editor's "Save Layer to Library" button crashed the editor every time
  for him (two tries) after the file write had finished; he now writes the XML directly (his note
  `scenes/project_terrain_generator.md`, 2026-09-13).
- **A Gauntlet layer added to the Scene Editor screen did not render.** `LoadMovie` succeeded and `HasLayer` was true, but
  neither he nor a screenshot showed any UI; he concludes the editor does not composite Gauntlet layers and exposes
  tool fields through a `ScriptComponentBehavior` with `[EditableScriptComponentVariable]` instead (his note
  `scenes/project_vertex_paint_module.md`, 2026-09-16 entry). The screenshot may not capture UI layers, which he notes.
- **The `bo_` body name lives on the entity's physics shape, not on the entity name or the mesh name.** Read it with
  `GameEntityPhysicsExtensions.GetBodyShape(entity)` then `.GetName()`; the entity and first-mesh names matched 0 times
  on a real prop. Composite prefabs hold shapes on child entities, so walk `GetChildrenRecursive()` (his note
  `scenes/project_navmesh_generator.md`, 2026-09-13).

## The native boundary
`GameEntity` is a `NativeObject` — the scene node, its transform, meshes, and physics live **native**; the managed
class is a thin wrapper (Instantiate/SetFrame/GetChild are native calls). `ScriptComponentBehavior` is the managed
half: the engine (native scene driver) calls back into the C# virtuals (`[EngineCallback]`) each frame. So scene
*logic* is managed (yours), the scene *graph* is native.

## Evidence (file:line, v1.4.5)
- `TaleWorlds.Engine/GameEntity.cs`:10 (`sealed : NativeObject`), 379/398/403 (`Instantiate` overloads), 504/516/529 (`GetFirstScriptOfType[Recursive/InFamily]`).
- `TaleWorlds.Engine/ScriptComponentBehavior.cs`:9 (`abstract : DotNetObject`), 11-22 (`TickRequirement`), 78 (`CacheEditableFieldsForAllScriptComponents`), 82 (`Construct`), 174 (`SetScene`), 180 (`OnInit` `[EngineCallback]`), 185/191 (`HandleOnRemoved`/`OnRemoved`), 38/54 (`GameEntity`/`Scene`).
- TAOM: `Main/SceneScripts/` (CS_Road), `docs/features/scene-scripts.md`, [lesson](../../reviews/lessons/gamemodels-services.md); the howdah prefab pattern in [howdah-crew-mechanism.md](../../features/elephant/howdah-crew-mechanism.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
