# NativeSkinFixes

## Overview

Fixes two Bannerlord native engine bugs by hooking C++ functions in TaleWorlds.Native.dll via MinHook. Resolves the "jazz hands" bug when helmets use `covers_head="true"` and enables cloth physics simulation on hair/beard meshes that have cloth simulation data.

## Why This Exists

- **Vanilla behavior:** When `covers_head="true"`, the engine clears the HeadVisible bit in the visibility mask. This skips Face_mesh creation entirely, which breaks the GPU morph pipeline -- hand grip animations freeze permanently ("jazz hands"). Separately, the cloth factory only processes MetaMesh (type 0) and skips Face_mesh (type 6), so hair/beard cloth simulation data is ignored.
- **TAOM requirement:** Many LOTR helmet designs use `covers_head="true"`. Without this fix, every helmeted character has frozen hands. Cloth-enabled hair/beard meshes need physics to look correct.
- **Without this feature:** Characters wearing full helms cannot grip weapons. Hair/beard meshes with cloth data render as static geometry.

## Architecture

### Design Challenge

Both bugs are in native C++ code (`TaleWorlds.Native.dll`), not managed C#. They cannot be fixed with Harmony patches, GameModel overrides, or any managed API. The fix requires detouring native functions at the assembly level.

### Solution Approach

Forked from the NativeSkinFixes community mod. Uses MinHook (a lightweight x64 function hooking library) to detour three native functions. The C++ hooks are compiled into a native DLL (`TAOM.NativeSkinFixes.dll`). A C# managed interop layer loads the DLL, resolves function addresses via RVAs, and calls the exported Install/Uninstall functions.

### Component Diagram

```
TaleWorlds.Native.dll (engine)
        |
  MinHook detours (3 functions)
        |
TAOM.NativeSkinFixes.dll (C++ native hooks)
        |
  P/Invoke exports (Install/Uninstall)
        |
Main/Features/NativeSkinFixes/ (C# interop)
        |
  NativeSkinFixesLoader (orchestrator)
        |
  SubModule.cs (lifecycle: install on init, remove on unload)
```

### Three Hooks

| Hook | RVA | What It Does |
|------|-----|-------------|
| CoversHeadHook | 0x617B50 | Forces HeadVisible bit ON so Face_mesh is always created with morph pipeline. Tracks hidden faces in thread-safe set for render suppression. |
| HairClothHook | 0x359C10 | Detours cloth factory to rescue orphaned cloth from Face_mesh+0x1A0. Registers cloth in entity list (rendering) and sim list (physics). Also handles beard cloth at +0x108. |
| FaceMeshObserveHook | 0x61FE20 | Hooks render list builder. For covers_head: suppresses all face components. For cloth: suppresses static hair/beard so only animated cloth renders. |

## Configuration

No configuration files. The feature is always active when the native DLL loads successfully.

## Key Files

| File | Purpose |
|------|---------|
| `Dependencies/ThirdParty/NativeSkinFixes/CoversHeadHook.cpp` | covers_head morph fix (C++) |
| `Dependencies/ThirdParty/NativeSkinFixes/HairClothHook.cpp` | Cloth factory detour (C++) |
| `Dependencies/ThirdParty/NativeSkinFixes/FaceMeshObserveHook.cpp` | Render list suppression (C++) |
| `Dependencies/ThirdParty/NativeSkinFixes/dllmain.cpp` | DLL entry point with safe termination guard |
| `Dependencies/ThirdParty/NativeSkinFixes/TAOM.NativeSkinFixes.vcxproj` | C++ project (VS 2022, x64) |
| `Dependencies/ThirdParty/NativeSkinFixes/MinHook/` | Vendored MinHook library (header + lib) |
| `Main/Features/NativeSkinFixes/NativeHookLoader.cs` | DLL loader, RVA resolver, export getter |
| `Main/Features/NativeSkinFixes/CoversHeadHookInterop.cs` | P/Invoke for covers_head hook |
| `Main/Features/NativeSkinFixes/HairClothHookInterop.cs` | P/Invoke for cloth factory hook |
| `Main/Features/NativeSkinFixes/FaceMeshObserveInterop.cs` | P/Invoke for render list hook |
| `Main/Features/NativeSkinFixes/NativeSkinFixesLoader.cs` | Orchestrator with rollback on partial failure |
| `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` | Built C++ output |
| `Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll` | MinHook runtime dependency |

## Dependencies

- MinHook x64 (vendored, MIT license)
- TaleWorlds.Native.dll (runtime, not compile-time reference)
- TaleWorlds.ModuleManager (for module path resolution)

## Tests

No unit tests -- native P/Invoke hooks require the live game engine. Verified by:
- C# compilation (zero errors from new code)
- C++ compilation with VS 2022 toolchain
- RVA validation against installed v1.4.0 TaleWorlds.Native.dll (7/7 valid function prologues)
- In-game visual verification (helmets with covers_head, cloth-enabled hair)

## How-To

### How to update RVAs for a new Bannerlord version

1. Open the new `TaleWorlds.Native.dll` in a disassembler (Ghidra, IDA, x64dbg)
2. Find the three target functions by signature/pattern
3. Update the RVA constants in the C# interop files:
   - `CoversHeadHookInterop.cs` -- `AddSkinMeshesRva`
   - `HairClothHookInterop.cs` -- `ClothFactoryRva`, `AddToListRva`, `GpuInitRva`, `HasClothDataRva`
   - `FaceMeshObserveInterop.cs` -- `RenderListBuildRva`
4. If struct layouts changed (offsets in C++), update the hardcoded offsets in the `.cpp` files and rebuild

### How to rebuild the C++ DLL

```powershell
# From repo root, using Visual Studio 2022 MSBuild
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
    "Dependencies\ThirdParty\NativeSkinFixes\TAOM.NativeSkinFixes.vcxproj" `
    /p:Configuration=Release /p:Platform=x64
```

Or open `TAOM.sln` in Visual Studio and build the `TAOM.NativeSkinFixes` project.

## Version Compatibility

RVAs and struct offsets are version-specific. Current values verified for Bannerlord v1.4.0 (TaleWorlds.Native.dll, 14,127,552 bytes). If the game updates, re-verify using the RVA validation script or a disassembler.
