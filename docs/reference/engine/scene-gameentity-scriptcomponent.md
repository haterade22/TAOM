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
`[EditableScriptComponentVariable]` fields. The ADOD howdah prefabs (`adod_howdah_{1,2,4}_agent`) are exactly this:
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
  bug class (`.claude/rules/csharp-architecture.md` "Config Providers MUST Validate" category 3; `feedback_editor_fields_are_config`
  — shipped 3×: Career #31, EditorCacheRebuild #38, CS_Road 2026-05-13). Use `FiniteFloatValidator`.
- **A howdah (future)** would be `ScriptComponentBehavior` subclasses + a prefab, instantiated per-elephant via
  `GameEntity.Instantiate(scene, "<howdah_prefab>", …)` and frame-glued via `SetFrame` each tick (the howdah doc).
- **Editor-only script types** (e.g. `AnimalSpawnSettings : ScriptComponentBehavior`, with a `SpawnerPermissionField
  : EditorVisibleScriptComponentVariable`) exist only in the wEditor build — check `_editor_build` for scene-authoring
  scripts (Phase-0 lookup order).
- `<script name>` must exactly match the class name, and the class must be in a loaded module assembly, or the prefab
  silently has no script.

## The native boundary
`GameEntity` is a `NativeObject` — the scene node, its transform, meshes, and physics live **native**; the managed
class is a thin wrapper (Instantiate/SetFrame/GetChild are native calls). `ScriptComponentBehavior` is the managed
half: the engine (native scene driver) calls back into the C# virtuals (`[EngineCallback]`) each frame. So scene
*logic* is managed (yours), the scene *graph* is native.

## Evidence (file:line, v1.4.5)
- `TaleWorlds.Engine/GameEntity.cs`:10 (`sealed : NativeObject`), 379/398/403 (`Instantiate` overloads), 504/516/529 (`GetFirstScriptOfType[Recursive/InFamily]`).
- `TaleWorlds.Engine/ScriptComponentBehavior.cs`:9 (`abstract : DotNetObject`), 11-22 (`TickRequirement`), 78 (`CacheEditableFieldsForAllScriptComponents`), 82 (`Construct`), 174 (`SetScene`), 180 (`OnInit` `[EngineCallback]`), 185/191 (`HandleOnRemoved`/`OnRemoved`), 38/54 (`GameEntity`/`Scene`).
- TAOM: `Main/SceneScripts/` (CS_Road), `docs/features/scene-scripts.md`, `feedback_editor_fields_are_config`; the howdah prefab pattern in [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md).
