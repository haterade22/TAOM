# Bannerlord engine, toolchain & decompiled-source reference

> **Purpose:** one place that explains *what Bannerlord is made of* — the two builds, the managed vs native
> split, every major component (incl. the editor/Qt/FBX toolchain), the managed↔native bridge, and the asset
> pipeline — so a future session understands the full picture instead of re-deriving it in pieces. Created
> 2026-06-06. Pairs with [bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md),
> [scene-reference-audit.md](scene-reference-audit.md), and `docs/tools/spider-skeleton-tpac-tools.md`.

## 1. The two builds (this is the key distinction)

Bannerlord ships **two** native+managed builds under `<game>/bin/`:

| Build dir | What it is | DLLs | Editor code? |
|---|---|---|---|
| `Win64_Shipping_Client` | The game players run. | 85 | **stripped** |
| `Win64_Shipping_wEditor` | The **Modding Kit** build (run via `Bannerlord.exe` from the launcher's "Modding Kit"). | 108 | **present** |

The TaleWorlds.* DLLs have the **same names** in both builds but **different content**: the wEditor versions
have editor-only types compiled in. Confirmed editor-only managed types (in wEditor `TaleWorlds.MountAndBlade.dll`,
absent from shipping): `MBEditor`, `EditorGame : GameType`, `EditorGameManager`, `EditorState : GameState`,
`MBUnusedResourceManager`, **`AnimalSpawnSettings : ScriptComponentBehavior`** (creature spawn authoring),
`VertexAnimator`, `MissionFacialAnimationHandler`, `SpawnerEntityEditorHelper`.

**Consequence:** the curated decompile at `E:\Decompiled_Bannerlord\` (category folders) is the **shipping
client** — it does NOT contain editor-only code. Some knowledge (how the Animation Clip Inspector applies
`AnimFlags`, FBX import, tpac asset authoring) lives only in the editor build (and much of it in *native* editor
code — see §3). When you can't find an editor concept in the shipping decompile, check the editor build.

## 2. Decompiled-source layout (`E:\Decompiled_Bannerlord\`)

| Path | Build | Form | Use |
|---|---|---|---|
| `Campaign\`, `MountAndBlade\`, `Core\`, `Engine\`, `UI\`, … (category folders) | shipping | curated, by namespace | **browse** patterns/namespaces (the long-standing reference) |
| `_shipping_build\<Dll>.cs` | shipping | one .cs per DLL | full per-DLL decompile; **diff** vs editor |
| `_editor_build\<Dll>.cs` | wEditor | one .cs per DLL | **editor-only code** (EditorGame, AnimalSpawnSettings, …) |
| `_{shipping,editor}_build\_native_dlls.txt` | both | list | the native DLLs that can't be decompiled (see §3) |

Regenerate with **`pwsh tools/decompile_bannerlord.ps1`** (re-run after an engine update). `ilspycmd` only
decompiles .NET assemblies; native DLLs are detected and listed, not decompiled.

**Authoritative signatures** still come from `pwsh tools/taom-src.ps1 path <Type>` (runs `ilspycmd` on the
*installed* shipping DLLs, auto-detects version). Use the decompiled folders for *browsing*; use `taom-src` for
*verifying a signature* you're about to call.

## 3. Managed (.NET) vs NATIVE DLLs — and what the native ones are

`ilspycmd` decompiles **managed (.NET)** assemblies. The rest are **native C++** (the engine + third-party libs)
and only their exported symbols are inspectable, not C# source. The native components (and why Bannerlord ships
them):

| Native DLL | What it is | Role in Bannerlord |
|---|---|---|
| **`TaleWorlds.Native.dll`** | The C++ **engine core** (`rglEngine`). | Rendering, physics, animation playback, skeletons, the scene graph, the tpac asset system. The managed code is a thin layer over this (see §4). **This is where `AnimFlags` behavior, `PreloadForRendering`, the gait builder, the bone-palette cap all live** — none of it is in the managed decompile. |
| **`Qt5Core/Gui/Widgets/Concurrent/Sql/Test/WinExtras`** | The **Qt 5** cross-platform C++ **GUI framework**. | The **Modding Kit editor is a Qt desktop application** — its windows, docks, the Scene/Skeleton/**Animation Clip Inspector** panels are Qt widgets. So the Inspector's logic (incl. how the flag checkboxes map to `AnimFlags` and get baked into the clip) is native Qt+engine code, not a managed assembly. Editor-build only. |
| **`libfbxsdk.dll`** | Autodesk **FBX SDK**. | FBX **import/export** in the editor — the entry point of the art pipeline (the animator's `.fbx` → engine mesh/skeleton/clip). Editor-build only. |
| **`nvtt.dll`** | NVIDIA **Texture Tools**. | Texture/DDS **compression + mipmap** generation when importing textures → tpac. Editor-build only. |
| **`ispc_texcomp.dll`** | Intel **ISPC Texture Compressor**. | Fast BCn texture compression (alongside nvtt). Editor-build only. |
| **`embree3.dll`** | Intel **Embree** ray-tracing kernels. | Offline **lighting / GI / lightmap bake** in the editor. Editor-build only. |
| **`tbb.dll`** | Intel **Threading Building Blocks**. | Parallelism backend used by embree/nvtt and the engine. |
| **`FreeImage.dll`** | **FreeImage** image library. | Loading/saving common image formats during import. Editor-build only. |
| **`d3dcompiler_47.dll`** | Microsoft **D3D shader compiler**. | Compiles HLSL shaders (the `compressed_shader_cache.sack` pipeline; see `feedback_shader_cache_invisible_cc`). |
| **`steam_api64.dll`** | **Steamworks** API. | Steam platform (workshop, achievements, friends). |
| **`EOSSDK-Win64-Shipping.dll`** | **Epic Online Services** SDK. | Cross-play/Epic platform services. |

(NAudio.* — managed audio lib, editor-build only, used by the Kit's audio tooling. MinHook/`TAOM.NativeSkinFixes`
are TAOM-vendored native hooks, not shipped by TaleWorlds — see CLAUDE.md.)

## 4. The managed↔native bridge (how C# calls the engine)

Almost everything visible/physical is implemented in `TaleWorlds.Native.dll` (C++); the managed assemblies call
it through a generated interface layer. Understanding this explains why so many behaviors are "not in the
decompile":

- **`MBAPI.IMB*`** interfaces (e.g. `IMBAgent`, `IMBAnimation`, `IMBSkeleton`, `IMBScene`) — the managed-side
  contract. Methods tagged **`[EngineMethod("name")]`** are implemented natively. Example:
  `MBActionSet.GetActionAnimationFlags(actionSet, action)` → `MBAPI.IMBAnimation.GetAnimationFlags(actionSetNo, actionIndex)`
  → native `[EngineMethod("get_animation_flags")]`. So a clip's `AnimFlags` are *read* by managed code but the
  bitfield is stored + interpreted natively.
- **`[EngineStruct("name")]`** — a managed struct mirroring a native one (e.g. `AnimFlags` is `[EngineStruct("Anim_flags")]`).
- **`TaleWorlds.*.AutoGenerated\ManagedCallbacks\*`** — the generated marshalling glue (the `enm_IMono_*` /
  `CoreInterfaceGeneratedEnum` switch tables). When you see a behavior bottom out in an `IMB*` `[EngineMethod]`,
  the logic is native — document it from behavior/naming, don't expect C# source.

**Practical rule:** managed decompile shows *what managed code asks the engine to do* (SetActionChannel, SpawnAgent,
SetFrame); the *how* (animation blending, render, physics) is native. The enum/constant definitions (`AnimFlags`,
`ActionCodeType`, `EquipmentIndex`) ARE in managed and authoritative.

## 5. TaleWorlds managed assembly families (where to look)

| Family (count) | What it covers |
|---|---|
| `MountAndBlade.*` (25) | Missions, agents, formations, **UsableMachine/StandingPoint**, `AnimFlags`/`ActionIndexCache`/`MBActionSet`, MissionBehaviors, MP. The bulk of in-battle gameplay. |
| `CampaignSystem.*` (6) | Campaign map, heroes, parties, settlements, GameModels, CampaignBehaviors, actions. |
| `Core` (4) | `BasicCharacterObject`, items, `Equipment`, `MBObjectManager`, localization, `SaveSystem` hooks. |
| `Engine.*` (6) | `GameEntity`, `Scene`, `MatrixFrame`, `MetaMesh`, `Skeleton`, the tpac/resource system surface. |
| `GauntletUI.*` (10) | The in-game (non-editor) UI framework (widgets, bindings) — distinct from the editor's Qt. |
| `TwoDimension.*` (5) | 2D sprite/UI rendering (`SpriteSheetGenerator` is editor-only). |
| `Diamond.*` (10), `PlatformService.*` (7), `PlayerServices`, `Network`, `ServiceDiscovery` | Networking, matchmaking, platform (Steam/Epic) integration. |
| `SaveSystem` (2) | `SaveableTypeDefiner`, the save serializer (TAOM's `*SaveDefiner` plug in here). |
| `ObjectSystem`, `ModuleManager`, `Starter`, `ScreenSystem`, `NavigationSystem`, `PSAI`, `DotNet` | Object registry, module load, game-start, screen stack, navmesh, MP AI, .NET interop helpers. |
| `SandBox.*`, `StoryMode.*` (separate top-level, not `TaleWorlds.`) | The campaign content layer (cultures, settlements, the main story) — TAOM's XSLT targets `SandBoxCore`. |

## 6. The asset pipeline (FBX/texture → tpac), in one picture

```
art source (.fbx, textures)
  │  (editor build only)
  ├─ libfbxsdk         → parse FBX (mesh, skeleton, animation takes)
  ├─ nvtt / ispc_texcomp → compress textures (BCn/DDS + mips)
  ├─ Qt Inspectors      → author per-asset metadata: Skeleton UserData (bodies/IK),
  │                        Animation Clip flags (AnimFlags), materials, LODs
  └─ engine serializer  → write .tpac (per-asset *_geo.tpac, *_anm.tpac, or AssetPackages/pack*.tpac)
                              │
                              ▼  (shipping client loads these)
                         Monster + action_set + monster_usage (XML) reference the baked assets by name
```

- **tpac** = TaleWorlds' packed-asset container. Holds Skeleton / Mesh / Animation / Material / etc. items, each
  with metadata + LZ4-compressed data segments. Format reverse-engineered in `docs/tools/spider-skeleton-tpac-tools.md`
  (Skeleton segments documented; Animation-clip metadata layout, incl. the `AnimFlags` field, is **not yet**
  reverse-engineered — a `tpac_clip_flags.py` tool is a TODO there).
- **Animation clip flags** are baked here (set in the Animation Clip Inspector) — see the dedicated
  [clip-flags reference](bannerlord-animation-clip-flags.md). They are NOT in `action_types.xml`.
- The **skeleton** lives inside a `*_geo.tpac` (e.g. LOTRLOME's `elephant_harad_armor_01_geo.tpac` carries
  `elephant_skeleton`); animation **clips** are skeleton-relative (bind by skeleton name).

## 7. Custom non-humanoid creature workflow (the cross-cutting recipe)

Pulling the above together — to add a renderable, animated, fighting custom creature (spider/warg/elephant):

1. **Monster** (XML): `id`, `skeleton` (via the action_set), `monster_usage`, `action_set`, capsules, flags
   (`IsHumanoid`, `Mountable`, …). The 1.4.X-native pattern (ArtemsHunts' animals) is `monster_usage="horse"` +
   a custom `action_set` bound to the creature skeleton.
2. **Skeleton + mesh** (tpac): import the FBX in the Kit → `*_geo.tpac`; patch SkeletonUserData (bodies/IK/Usage)
   with `tpac_skeleton_transplant.py`. Render gotcha: the **per-mesh bone-palette cap** (native, ~40) — a single
   mesh referencing too many bones AVs in `PreloadForRendering` (the spider's unsolved blocker).
3. **Clips** (tpac): one clip per action, each with the **right `AnimFlags`** per type (movement → `synch_with_movement`+`cyclic`;
   attack → `lock_movement`+`enforce_all`; priority in the low byte) — see the clip-flags reference. A clip needs
   both its `_anm.tpac` AND correct flags.
4. **action_set** (XML): bind `act_*` types → clip names, `skeleton="<creature>_skeleton"`, `movement_system="quadrupedal"`.
5. **Spawn** (C#): the public `Mission.SpawnMonster(mountItem, default rider, …)` for a riderless creature (wolf/
   ArtemsHunts pattern), or as a recruitable troop via the `Mission.SpawnAgent` chokepoint. Movement/AI via an
   `AgentComponent` (`OnTick`/`OnTickParallel` — **not** the dead-in-1.4.5 `OnTickAsAI`) or a behavior-tree
   MissionLogic (TAOM's spider).

References: [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md) (UsableMachine crew),
[spider.md](../features/spider.md) + its RCA (render AV), [elephant.md](../features/elephant.md).
