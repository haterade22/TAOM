# Bannerlord engine, toolchain & decompiled-source reference

> **Purpose:** one place that explains *what Bannerlord is made of* — the two builds, the managed vs native
> split, every major component (incl. the editor/Qt/FBX toolchain), the managed↔native bridge, and the asset
> pipeline — so a future session understands the full picture instead of re-deriving it in pieces. Created
> 2026-06-06. Pairs with [bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md),
> [scene-reference-audit.md](scene-reference-audit.md), and `docs/tools/spider-skeleton-tpac-tools.md`.

## 1. The builds (this is the key distinction)

Bannerlord ships **two** native+managed builds under `<game>/bin/`, plus **two more** in a separate
Steam app (*Mount & Blade II Dedicated Server*):

| Build dir | What it is | DLLs | Editor code? | Install |
|---|---|---|---|---|
| `Win64_Shipping_Client` | The game players run. | 85 | **stripped** | `<game>/bin/` |
| `Win64_Shipping_wEditor` | The **Modding Kit** build (run via `Bannerlord.exe` from the launcher's "Modding Kit"). | 108 | **present** | `<game>/bin/` |
| `Win64_Shipping_Server` | Dedicated-server host, Windows. | 80 | n/a | *Dedicated Server* app |
| `Linux64_Shipping_Server` | Dedicated-server host, Linux. | 63 | n/a | *Dedicated Server* app |

The TaleWorlds.* DLLs have the **same names** in the two `<game>/bin/` builds but **different content**: the wEditor versions
have editor-only types compiled in. Confirmed editor-only managed types (in wEditor `TaleWorlds.MountAndBlade.dll`,
absent from shipping): `MBEditor`, `EditorGame : GameType`, `EditorGameManager`, `EditorState : GameState`,
`MBUnusedResourceManager`, **`AnimalSpawnSettings : ScriptComponentBehavior`** (creature spawn authoring),
`VertexAnimator`, `MissionFacialAnimationHandler`, `SpawnerEntityEditorHelper`.

**Consequence:** the curated decompile at `E:\Decompiled_Bannerlord\` (category folders) is the **shipping
client** — it does NOT contain editor-only code. Some knowledge (how the Animation Clip Inspector applies
`AnimFlags`, FBX import, tpac asset authoring) lives only in the editor build (and much of it in *native* editor
code — see §3). When you can't find an editor concept in the shipping decompile, check the editor build.

### 1.1 The dedicated-server build — and why a whole class of failures is server-only

**The engine that runs a dedicated server is a different build from the one that runs the client**, and
it is **stricter about ModuleData structure**. That single fact explains bugs that no single-player
session can reproduce: the client loads a malformed XML file silently while the server throws on it at
boot. The 2026-08-03 case was 168 root-level `<action>` elements in LOTRLOME_Armory's `action_sets.xml`
— tolerated by build 1.4.7.117484, fatal on build 117131 (`KeyNotFoundException` in
`MBObjectManager.MergeElements` at `/action_sets/action`). Both build numbers come from the co-op field
report; they are **not** locally verifiable — the installed client's `bin/Win64_Shipping_Client/Version.xml`
carries only `<Singleplayer Value="v1.4.8"/>` and every exe/DLL reports FileVersion `1.0.0.0`.

What *is* verifiable on disk is that the two installs ship different schema sets: `<game>/XmlSchemas/`
has 51 `.xsd`, the Dedicated Server app 45 — it lacks the single-player/naval set (`SPCultures.xsd`,
`partyTemplates.xsd`, `MissionShips.xsd`, `ShipHulls.xsd`, `ShipPhysicsReferences.xsd`,
`ShipUpgradePieces.xsd`). `soln_action_sets.xsd` is byte-identical between them, so the divergence that
bit us is in the loader, not the schema.

**Don't misidentify the Steam app.** *Mount & Blade II Dedicated Server* is a **multiplayer appliance,
not a campaign host**: its `Modules/` are `BattleLinkServer`, `ExampleSoundMod`, `FastMode`,
`Multiplayer`, `Native`, `SandBoxCore`, `SandBoxCoreMP` — no `SandBox`, no `StoryMode` — and its `bin/`
ships **no `TaleWorlds.CampaignSystem.dll`** at all. It is a .NET-Core host (`TaleWorlds.Starter.DotNetCore.exe`,
`Rgl.dll` / `Game.dll` / `FairyTale.{DotNet,Library,ModuleManager}.dll`, plus an ASP.NET
`DedicatedCustomServer.WebPanel`). It is therefore **not** the campaign co-op server the 2026-08-03 field
report used. What is established for our purposes is narrower: a server resolves a module's binaries from
`<module>/bin/Win64_Shipping_Server/`, which is the folder named in the `Cannot find: ...` log line that
motivated the mirror below.

### 1.2 Module-side `bin/` folders — what TAOM ships

Distinct from the engine builds above: each module carries its **own** `bin/<build>/` folders, and the
engine loads the one matching the build it is running. TAOM populates them from the assembled client
folder via mirror targets:

| Target | csproj | Mirrors into | Deployed file count |
|---|---|---|---|
| `MirrorWin64ShippingClientToEditor` | `Main/TAOM.csproj` | `Modules/TAOM/bin/Win64_Shipping_wEditor/` | 12 |
| `MirrorWin64ShippingClientToServer` | `Main/TAOM.csproj` | `Modules/TAOM/bin/Win64_Shipping_Server/` | 10 (same set as Client) |
| `MirrorWin64ShippingClientToServer` | `Dependencies/TAOM.Dependencies.csproj` | `Modules/TAOM.Dependencies/bin/Win64_Shipping_Server/` | 42 (same set as Client) |

Both server targets run `AfterTargets="PostBuildCopyToModules"`, gated on `$(DisableModuleCopy) != 'true'`
and on the assembled client folder existing. **Mirroring the assembled folder rather than the build output
is deliberate** — it picks up the vendored natives (`MinHook.x64.dll`, `TAOM.NativeSkinFixes.dll`) and NuGet
companions that only exist after `PostBuildCopyToModules`. There is no editor mirror in
`TAOM.Dependencies.csproj`, so `Modules/TAOM.Dependencies/bin/Win64_Shipping_wEditor/` being empty is
expected, not a bug.

Two honesty caveats on the server mirror: **no dedicated server has been booted against these binaries**
(commit 5f373df9 carries `Not-tested:` to that effect), and `Main/_Module/SubModule.xml` still declares
`<Tag key="DedicatedServerType" value="none" />` — shipping the binaries does not by itself mark TAOM
server-capable.

## 2. Decompiled-source layout (`E:\Decompiled_Bannerlord\`)

| Path | Build | Form | Use |
|---|---|---|---|
| `Campaign\`, `MountAndBlade\`, `Core\`, `Engine\`, `UI\`, … (category folders) | shipping | curated, by namespace | **browse** patterns/namespaces (the long-standing reference) |
| `_shipping_build\<Dll>.cs` | shipping | one .cs per DLL | full per-DLL decompile; **diff** vs editor |
| `_editor_build\<Dll>.cs` | wEditor | one .cs per DLL | **editor-only code** (EditorGame, AnimalSpawnSettings, …) |
| `_modules_build\<Module>__<Dll>.cs` | module bins | one .cs per DLL, module-prefixed | the assemblies that ship inside `Modules\*\bin\Win64_Shipping_Client` (SandBox.View, TaleWorlds.MountAndBlade.View, the GauntletUI satellites, …) |
| `_{shipping,editor,modules}_build\_native_dlls.txt` | each | list | the native DLLs that can't be decompiled (see §3) |

Regenerate with **`pwsh tools/decompile_bannerlord.ps1`** (re-run after an engine update). `ilspycmd` only
decompiles .NET assemblies; native DLLs are detected and listed, not decompiled.

**Current version: v1.4.8**, regenerated 2026-08-10. `_manifest.json` carries `"version": "v1.4.8"`,
and `TaleWorlds.Library`'s `public const string GameVersion` reads `v1.4.8.119303` in both
`_shipping_build` and `_editor_build`. Counts at that regen: 56 `.cs` in `_shipping_build`, 66 in
`_editor_build`, 125 in `_modules_build`.

**`_modules_build` (added 2026-08-10) covers assemblies nothing else did.** The two
`<GameBin>\Win64_Shipping_*` folders hold only the base binaries, and the category tree's generator
(`tools/decompile_to_folder.ps1`) takes just the PRIMARY DLL per module (`SandBox.dll`,
`StoryMode.dll`, …). So `SandBox.View`, `SandBox.ViewModelCollection`, `SandBox.GauntletUI`,
`TaleWorlds.MountAndBlade.View`, `TaleWorlds.MountAndBlade.GauntletUI`,
`TaleWorlds.MountAndBlade.Platform.PC` and the StoryMode / Multiplayer / CustomBattle / NavalDLC /
BirthAndDeath / FastMode satellites appeared in **no** decompile artifact — while TAOM patches into
several of them. Because Steam overwrites the install in place, an assembly missing from the stack
when an update lands has no recoverable baseline afterwards; that is what made the 1.4.7 → 1.4.8
assembly diff silently partial. The pass walks every `Modules\*\bin\Win64_Shipping_Client`, so it
also picks up TAOM's own modules and every other installed one (`DOTS`, `ADOD_Beasts`,
`ServeAsSoldier`, …) — those 125 files are not 125 vanilla assemblies.

**Why `<Module>__<Dll>.cs` rather than `<Dll>.cs`:** DLL basenames collide across module folders, so
a flat layout would silently drop one. `TaleWorlds.MountAndBlade.Multiplayer.dll` ships in both
`CustomBattle\` and `Multiplayer\`, and the vendored companions collide harder still — `0Harmony`
appears in three module bins, `System.Runtime.CompilerServices.Unsafe` in four.

**Preserved baselines** (rename before regenerating — `/engine-bump` Phase 2):
`_shipping_build_v1.4.5` / `_v1.4.6` / `_v1.4.7`, `_editor_build_v1.4.5`, and
`_categories_v1.4.5` / `_v1.4.6` / `_v1.4.7` for the category tree. There is no
`_modules_build_v<older>` — the folder did not exist before 1.4.8.

**The wEditor build follows its own Steam schedule and can skip versions.** On 2026-08-10 it went
`v1.4.5.114928` → `v1.4.8.119303` — three engine versions in one update — while the client moved
1.4.7 → 1.4.8 (compare `GameVersion` in `_editor_build_v1.4.5\TaleWorlds.Library.cs` against
`_editor_build\TaleWorlds.Library.cs`). Anything derived from a wEditor binary is valid only against
the version that binary carried at the time, which is why `/native-crash-triage` checks
`bin/Win64_Shipping_wEditor/Version.xml` before trusting a fault offset.

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

### 3.1 Verified facts (PE inspection 2026-06-06 — NOT guesses)

Inspected with **`tools/pe_inspect.py`** (stdlib PE export/import parser — how to "see into" a native DLL when
ilspycmd can't) + the PE **version resource** (PowerShell `(Get-Item).VersionInfo`). Exact versions:

| DLL | Product (version resource) | Version |
|---|---|---|
| `Qt5Core/Gui/Widgets/Concurrent/Sql/Test` | Qt5, "C++ Application Development Framework", The Qt Company Ltd. | **5.11.2.0** |
| `libfbxsdk` | FBX SDK, Autodesk, Inc. | **2016.0 Release (232667)** |
| `nvtt` | NVIDIA Texture Tools, NVIDIA Corporation | **2.1.0.0** |
| `embree3` | Intel Embree Ray Tracing Kernels, Intel | **3.6.1** |
| `tbb` | Intel Threading Building Blocks, Intel | **2018.0** |
| `FreeImage` | FreeImage library | **3.17.0** |
| `d3dcompiler_47` | Direct3D HLSL Compiler, Microsoft | 10.0.18362 |
| `steam_api64` | Steam Client API, Valve | 06.91.21.57 |
| `ispc_texcomp` | *(blank resource)* — exports `CompressBlocksBC1/BC3/BC6H/BC7/ASTC/ETC1` + `GetProfile_*` ⇒ **Intel ISPC Texture Compressor** | — |
| `EOSSDK-Win64-Shipping` | *(blank resource)* — 680 exports `EOS_Achievements_*`/… ⇒ **Epic Online Services SDK** | — |

**`TaleWorlds.Native.dll` = the engine — its IMPORT TABLE is the engine's real tech stack (fact, 53 imports):**

| Dependency | What it proves |
|---|---|
| **`mono-2.0-sgen.dll`** | Bannerlord's managed code runs on the **Mono** runtime (sgen GC), hosted by the native engine — NOT CoreCLR/.NET Framework directly. |
| **`PhysX_64` + `PhysXCommon/Cooking/Foundation_64`** | The physics engine is **NVIDIA PhysX**. (So collision capsules, ragdoll, `body_capsule` in monsters.xml feed PhysX.) |
| **`grCore` / `grGranite` / `grGraniteDX11`** | Texture streaming is **Granite** (Graphine virtual texturing). |
| **`d3d11.dll` / `dxgi.dll` / `D3DCOMPILER_47`** | Renderer is **DirectX 11**. |
| `nvtt`, `ispc_texcomp`, `embree3`, `libfbxsdk`, `FreeImage`, `Qt5*` | The editor/import toolchain is linked into the engine (wEditor build). |
| `GfeSDK.dll` | NVIDIA GeForce Experience (highlights/recording). |

**`TaleWorlds.Native.dll` exports (36, fact):** mostly `NVSDK_NGX_*` (**NVIDIA NGX / DLSS** AI upscaling), plus
the **managed↔native bootstrap**: `WotsMain` / `WotsMainNative` / `WotsMainNativeCoreCLR` / `create_game_application`
/ `get_ftdn_managed_interface` / `pass_managed_initialize_method_pointer` / `pass_managed_library_callback_method_pointers`
/ `pass_controller_methods`. These `pass_managed_*` exports are the mechanism behind the `[EngineMethod]` bridge in
§4 — the engine receives managed method pointers at startup, then calls back into Mono. (No per-API named exports
like `get_animation_flags` — those are dispatched through the callback table, not exported symbols, which is why
the engine's per-feature logic isn't visible as exports.)

**To see into any other native DLL:** `python tools/pe_inspect.py <dll> [--max-names N]` → machine, PE32+,
internal name, export count + sample, import list. For .NET DLLs use `ilspycmd` (full source) instead.

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

### 6.1 The four asset folders, and who each one is for

A module can carry four asset directories. They are not four stages of one pipeline that the game
walks in order; three of them are **distribution formats**, and which one you need depends entirely
on who is receiving the module.

| Folder | What it holds | Who it is for |
|---|---|---|
| `AssetSources/` | The raw art: FBX, source PNG, uncompressed textures | Nobody outside the team. Not shipping this is the whole point of packaging |
| `Assets/` | Compiled per-asset tpac (`*_geo`, `*_mtl`, `*_anm`, `*_tex`) | The working tree. Both the editor and the game read it on a dev install |
| `AssetPackages/` | Cooked packs | **Players.** This is the runtime form in a release |
| `EmAssetPackages/` | Cooked packs in editor form | **Other modders**, so they can build against the module in the Modding Kit without receiving your `AssetSources` |

Three consequences that are easy to get backwards:

- **Neither packages folder matters on a dev install.** The editor reads `Assets/`, and so does the
  game. Deleting `AssetPackages` locally costs nothing; Publish Module regenerates it.
- **A creature with no entry in either packages folder is in its normal pre-release state, not
  broken.** On 2026-08-29 an invisible warg was misattributed to exactly this, on a correlation
  across six creatures whose failing side had two samples and one of them, the war ram, had never
  been checked. The ram is loose-only with no cooked entry and renders fine. The real cause was four
  missing mesh-to-material bindings ([lotrlome-warg-changes.md](lotrlome-warg-changes.md) section 12).
- **`EmAssetPackages` is the editor-distribution form, which settles an open question.**
  `docs/investigations/native-commit-audit-2026-08.md` correction (c) demoted it from "exclude" to
  "candidate" because vanilla `Modules\Native` ships 26.36 GB of it, reasoning that this "is not
  what an editor-only directory looks like". It is exactly what one looks like: Native is the module
  every modder opens in the Kit, so TaleWorlds ships its editor form deliberately. Shipping it is
  evidence for the editor-distribution reading, not against it.

**Open decision, not yet taken.** `tools/package_release.py` currently ships `EmAssetPackages`
(11.74 GB) to players as a `candidate=True` include, with the reason string "vanilla Native ships
26.36 GB of these -- unproven as editor-only". If the editor-distribution reading is right, a player
build does not need it, and a modder build is a separate artifact. That is a release-shape decision
and the packager is unchanged pending it.

## 7. Custom non-humanoid creature workflow (the cross-cutting recipe)

Pulling the above together — to add a renderable, animated, fighting custom creature (spider/warg/elephant):

1. **Monster** (XML): `id`, `skeleton` (via the action_set), `monster_usage`, `action_set`, capsules, flags
   (`IsHumanoid`, `Mountable`, …). The 1.4.X-native pattern (ArtemsHunts' animals) is `monster_usage="horse"` +
   a custom `action_set` bound to the creature skeleton.
2. **Skeleton + mesh** (tpac): import the FBX in the Kit → `*_geo.tpac`; patch SkeletonUserData (bodies/IK/Usage)
   with `tpac_skeleton_transplant.py`. Bone cap: `Skeleton.MaxBoneCount = 64` (skeleton-total; author ≤63). **There
   is NO per-mesh bone-palette cap — corrected 2026-06-13:** the "~40 per-mesh" gotcha was a misdiagnosis (the
   elephant renders 59 active bones in one mesh, the chariot 54). A single mesh skins the whole skeleton; never
   split a body for bone count. See `feedback_no_40_bone_per_mesh_limit`.
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

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/investigations/native-commit-audit-2026-08.md](../investigations/native-commit-audit-2026-08.md)
- [docs/modding/items-armor.md](../modding/items-armor.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/reference/adod-beasts-architecture-and-taom-port.md](./adod-beasts-architecture-and-taom-port.md)
- [docs/reference/engine/agent-spawn-and-render-pipeline.md](engine/agent-spawn-and-render-pipeline.md)
- [docs/reference/lotrlome-warg-changes.md](./lotrlome-warg-changes.md)
- [docs/reference/mcp-servers.md](./mcp-servers.md)

<!-- backlinks-end -->
## Decompiled-source folder layout (E:\Decompiled_Bannerlord\ category tree)

Moved from CLAUDE.md (repo-reorg 2026-07-12). The category tree is the SHIPPING-CLIENT decompile (strips editor-only code) — for editor-only types use the dual-build decompile `{_shipping_build,_editor_build}` described above.

**Decompiled source layout** (`E:\Decompiled_Bannerlord\` — for browsing only, never signatures):

> ⚠️ **The category folders below are the SHIPPING-CLIENT decompile — they STRIP editor-only code.** Editor-only
> managed types (`EditorGame`, `MBEditor`, `AnimalSpawnSettings`, `VertexAnimator`, FBX-import / scene / animation
> authoring) live ONLY in the **wEditor** build of the *same-named* DLLs — **"absent from this dump" ≠ "doesn't
> exist."** Lookup order: **shipping → if missing, the editor build → if missing, `_modules_build\` (the
> module-bin satellites: SandBox.View, TaleWorlds.MountAndBlade.View, the GauntletUI satellites — managed,
> but in neither `bin\` build) → if still missing, it's native (Qt/C++).** For
> both builds side-by-side use the dual-build decompile at `E:\Decompiled_Bannerlord\{_shipping_build,_editor_build}\`
> (regen: `tools/decompile_bannerlord.ps1`); inspect native DLLs with `tools/pe_inspect.py`. Full map (builds,
> managed-vs-native, the Mono/PhysX/Granite/DX11 engine stack, FBX→tpac pipeline):
> this doc.

| Folder | Contents |
|--------|----------|
| `Campaign/` | `TaleWorlds.CampaignSystem` — GameModels, behaviors, actions (1,556 files) |
| `MountAndBlade/` | `TaleWorlds.MountAndBlade` — missions, agents, game logic (1,977 files) |
| `Modules/` | `SandBox`, `StoryMode` — module behaviors, views (1,362 files) |
| `Core/` | `TaleWorlds.Core`, Library, SaveSystem, Localization (666 files) |
| `Engine/` | Engine, InputSystem, ScreenSystem, Navigation (386 files) |
| `UI/` | GauntletUI, PrefabSystem, PSAI (285 files) |
| `Network/` | Diamond, Network, PlayerServices (147 files) |
| `Platform/` | PlatformService, Achievements, ModuleManager (69 files) |
| `Launcher/` | Launcher.Library, Launcher.Steam (40 files) |
| `ThirdParty/` | Newtonsoft.Json, Steamworks.NET, jose-jwt (1,081 files) |

**DLL path** (for ILSpy MCP fallback): `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\` (shipping). **Editor build = `…\bin\Win64_Shipping_wEditor\`** — same-named DLLs with editor-only types compiled in.
