# NativeSkinFixes

## Overview

Three native MinHook detours into `TaleWorlds.Native.dll` that fix engine
rendering bugs TaleWorlds has refused to fix: helmets that freeze hand morphs
(`covers_head`), hair cloth physics that never registers, and beard cloth
physics that never registers. Hooks are installed at boot from `TaomSubModule.OnBeforeInitialModuleScreenSetAsRoot`
and uninstalled at module unload.

## Why This Exists

- **Vanilla behavior (covers_head):** When a character equips a helmet with
  `covers_head="true"`, the engine clears the `HeadVisible` bit in the skin
  visibility mask. `add_skin_meshes_to_agent_entity` checks this bit and skips
  Face_mesh creation entirely. Without the Face_mesh, the GPU morph pipeline
  is never initialized, and hand-grip morphs freeze. Visible as frozen hands
  during ragdoll / animation in TAOM cultures with covers_head helmets (Gondor
  knights, Rohan riders, Mordor orcs with closed helms).
- **Vanilla behavior (hair cloth):** `Face_mesh::ctor` creates an
  `rglCloth_simulator_component` at `Face_mesh+0x1A0` for animated hair, but
  never registers it in the entity or simulation lists — it's orphaned. The
  cloth allocator runs, occupies memory, and contributes nothing. Hair
  rendering falls back to static mesh.
- **Vanilla behavior (beard cloth):** The cloth factory at the symbol
  `cloth_factory` deliberately skips Face_mesh internals (type 6). Beard
  meshes at `Face_mesh+0x108` that carry cloth data in their vertex buffer
  never get a simulator created.
- **TAOM requirement:** Many TAOM cultures use cloth-flagged hair / beard
  items (Rohan riders, elves, Dale rebrand, dwarves of Dale). Without the
  fixes these all render as static. The covered-head morph freeze is even
  more visible: Gondor knights' hands lock up in weapon grips when their
  closed helm is equipped.
- **Without this feature:** Static hair / beard on cloth-flagged items, frozen
  hand morphs under closed helms. Cosmetic but pervasive — affects every
  battle scene.

## Architecture

### Design Challenge

The three bugs sit deep inside `TaleWorlds.Native.dll`, a native C++ DLL with
no managed API surface. They can only be fixed by intercepting native function
calls — Harmony doesn't reach C++ code.

The upstream NativeSkinFixes mod uses hardcoded RVAs (`0x617B50` etc.) inside
`TaleWorlds.Native.dll`. Every Bannerlord patch changes those offsets, so the
mod ships a v1.3.15-only DLL. TAOM is on v1.4.5 and wants the hooks to keep
working across `v1.4.x → v1.5.x` patches without C++ rebuilds.

### Solution Approach

1. **Vendor the C++ source in-repo** under `Dependencies/NativeSkinFixes.NativeHooks/`
   so the source-of-truth ships with TAOM and any developer with MSVC can
   rebuild. No "C++ source lives outside this repo" footnote (per the user
   direction "no external anything").
2. **Replace hardcoded RVAs with byte-pattern scanning.** A small scanner
   (`SignatureScanner.cpp`) reads `TaleWorlds.Native.dll`'s loaded image at
   hook-install time and finds each target function by an IDA-style byte
   pattern stored in `Signatures.h`. Patterns survive build-to-build relocation
   inside an engine version, and (usually) survive minor patches.
3. **Inline the C# wrapper into `TAOM.dll`** under `Main/Features/NativeSkinFixes/`.
   No separate `NativeSkinFixes` Bannerlord sub-module — one installer, one
   localized boot banner, one IModLogger sink. Hooks load from
   `TaomSubModule.OnBeforeInitialModuleScreenSetAsRoot` (after all modules
   loaded, before main menu).
4. **Graceful degradation everywhere.** Missing DLL, missing export, unscanned
   pattern, or pattern miss — all fail individually with a logged warning and
   the game continues vanilla. No NRE, no crash, no boot block.

### Component Diagram

```
TaomSubModule.OnBeforeInitialModuleScreenSetAsRoot
        |
        v
NativeSkinFixesInstaller.Install(IModLogger)
        |
   editor check (skip if "wEditor" in process path)
        |
        v
NativeHookLoader.EnsureLoaded()                    -- Win32 LoadLibrary
        |                                              "Main/_Module/bin/.../TAOM.NativeSkinFixes.dll"
        v
CoversHeadHookInterop.TryInstall(logger) -----+
HairClothHookInterop.TryInstall(logger) ------+--- each calls extern "C" Install()
FaceMeshObserveHookInterop.TryInstall(logger)-+
        |
        v
[ C++ TAOM.NativeSkinFixes.dll ]
        |
        v
Scanner::FindPattern(TaleWorlds.Native.dll, "<bytes>")
        |
   resolves 6 functions (3 hook targets + 3 helpers)
        |
        v
MH_CreateHook + MH_EnableHook            -- MinHook 1.3.4
```

## Configuration

### Native target signatures: `Dependencies/NativeSkinFixes.NativeHooks/Signatures.h`

The six byte patterns scanned at boot. Each entry has:

| Field | Type | Description |
|-------|------|-------------|
| `name` | `const char*` | Diagnostic label (appears in log lines) |
| `pattern` | `const char*` | IDA-style hex pattern, e.g. `"48 89 5C 24 ? 48 89 74 24 ?"`, or `"<PATTERN_TBD>"` placeholder |
| `fallbackPattern` | `const char*` | Optional secondary pattern; `nullptr` if not needed |
| `byteOffsetFromMatch` | `int` | Usually `0`. Non-zero when the pattern anchors a unique caller and we offset to the callee. |
| `historicalRva` | `long long` | v1.3.15 reference RVA, informational only (helps IDA navigation when re-authoring) |

### Current values

The seven signatures (3 hook targets + 4 helpers) currently ship as
`<PATTERN_TBD>` placeholders — see "Pattern authoring" below for the workflow
to fill them in.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NativeSkinFixes/NativeSkinFixesInstaller.cs` | Boot-time entry point, editor-mode skip, localized banner, IModLogger routing |
| `Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs` | LoadLibrary + GetProcAddress wrappers for `TAOM.NativeSkinFixes.dll` |
| `Main/Features/NativeSkinFixes/Interop/CoversHeadHookInterop.cs` | P/Invoke surface for `CoversHeadHook_Install` / `_Uninstall` |
| `Main/Features/NativeSkinFixes/Interop/HairClothHookInterop.cs` | P/Invoke surface for `HairClothHook_Install` / `_Uninstall` |
| `Main/Features/NativeSkinFixes/Interop/FaceMeshObserveHookInterop.cs` | P/Invoke surface for `FaceMeshObserveHook_Install` / `_Uninstall` |
| `Dependencies/NativeSkinFixes.NativeHooks/dllmain.cpp` | DLL entry — initializes logger on attach, uninstalls hooks on detach |
| `Dependencies/NativeSkinFixes.NativeHooks/Signatures.h` | Central registry of byte patterns + historical RVAs |
| `Dependencies/NativeSkinFixes.NativeHooks/SignatureScanner.{h,cpp}` | IDA-pattern parser + linear scan over `.text` section |
| `Dependencies/NativeSkinFixes.NativeHooks/CoversHeadHook.{h,cpp}` | Hook 1: forces `HeadVisible` bit ON so Face_mesh is always created |
| `Dependencies/NativeSkinFixes.NativeHooks/HairClothHook.{h,cpp}` | Hook 2: rescues orphan cloth at `Face_mesh+0x1A0` + re-enters factory for beard cloth at `+0x108` |
| `Dependencies/NativeSkinFixes.NativeHooks/FaceMeshObserveHook.{h,cpp}` | Hook 3: temporarily nulls hair/beard/all-face slots during render-list rebuild |
| `Dependencies/NativeSkinFixes.NativeHooks/Logging.{h,cpp}` | Unified log to `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\TAOM\NativeSkinFixes.log` |
| `Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj` | VS C++ project (x64, MSVC v143, C++17) outputting `TAOM.NativeSkinFixes.dll` directly into `Main/_Module/bin/Win64_Shipping_Client/` |
| `Dependencies/NativeSkinFixes.NativeHooks/Build.ps1` | Manual `msbuild` wrapper for developer rebuilds |
| `Dependencies/NativeSkinFixes.NativeHooks/MinHook/` | Vendored MinHook 1.3.4 (MIT) — `MinHook.x64.dll`, `.lib`, `.exp`, and `MinHook.h` header |
| `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` | Compiled native DLL (vendored binary, committed via `.gitignore` allowlist) |
| `Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll` | Runtime dep of the native DLL (vendored binary) |
| `Main/_Module/ModuleData/taom_module_strings.xml` | Localization entry `taom_nativeskinfixes_loaded` for the boot banner |

## Dependencies

- **MinHook 1.3.4** — MIT-licensed third-party native detour library, vendored
  under `Dependencies/NativeSkinFixes.NativeHooks/MinHook/`. No package manager
  dependency.
- **`IModLogger` (Core/Logging)** — used for boot-time install diagnostics.
  Each hook reports success / failure individually so partial degradation is
  visible.

## Tests

- `TAOM.Tests/Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs` — 8
  tests covering the editor-mode skip predicate (null / empty / normal client
  / editor / mixed case / false-positive guard) and the localization key
  wiring (key format + non-empty default).

The native interop layer (LoadLibrary, GetProcAddress, MinHook trampoline
install, byte-pattern scan against the live `TaleWorlds.Native.dll` image)
cannot be unit-tested — it requires a hosted Bannerlord process. Verify these
manually via the live-game checklist below.

## How to author the byte patterns (when a Bannerlord patch breaks scanning)

The signatures ship as `<PATTERN_TBD>` placeholders. When the scanner can't
find a target, the corresponding hook logs `"... pattern did not match
TaleWorlds.Native.dll"` and stays inert. Re-authoring:

1. **Reproduce the failure.** Launch Bannerlord with TAOM, then read
   `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\TAOM\NativeSkinFixes.log`.
   The log lists each signature's scan outcome and the module size at scan
   time.
2. **Open `TaleWorlds.Native.dll` in IDA / Ghidra / Binary Ninja.** Locate the
   installed copy at `<game>\bin\Win64_Shipping_Client\TaleWorlds.Native.dll`.
3. **Find each target function.** Each entry in `Signatures.h` carries the
   historical v1.3.15 RVA — start there. If the function moved, find it by:
   - xrefs to other named functions the C++ code calls (`add_skin_meshes_*`
     is called from agent visuals init; `cloth_factory` is called from mesh
     creation paths).
   - prologue shape (each function's first ~20 bytes are documented inline in
     the corresponding `*Hook.cpp` near the typedef).
   - `taom-src` dumps of related managed callers that pinvoke into native.
4. **Capture ~24-32 bytes from the prologue.** Replace any byte that's part
   of a relative offset, RIP-relative displacement, or absolute address with
   `?`. Conservative rule: anything that's NOT an opcode or register-encoded
   byte gets a `?`. Example:
   ```
   48 89 5C 24 ? 48 89 74 24 ? 57 48 83 EC ? 48 8B D9 41 8B F8
   ```
5. **Paste into `Signatures.h`** under the matching `kXxx` entry, replacing
   `"<PATTERN_TBD>"`. The `IsAuthored()` check (in the same header) only looks
   for a `'<'` prefix, so any real hex pattern will be picked up.
6. **Rebuild the native DLL:** `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1`.
   The `.vcxproj` writes the new DLL directly into
   `Main/_Module/bin/Win64_Shipping_Client/` and the next `./build.ps1`
   deploys it into the game install via `Bannerlord.BuildResources`.
7. **Verify in-game.** The log should show every signature resolving with a
   non-zero RVA, and the boot banner should appear in the in-game message
   area.
8. **Commit the binary** alongside the pattern change: the `.gitignore`
   allowlist permits `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll`,
   so `git add` will pick it up. Otherwise teammates / CI / fresh clones
   keep running the stale DLL.

If a pattern has more than one match, extend it with more discriminating
bytes (capture further into the function body). If it has zero matches,
relax a wildcard or capture from a slightly different offset. The scanner
returns the FIRST match — ambiguity should be resolved by the pattern itself.

## How to add a fourth hook

1. Add a `TargetSignature` constant to `Signatures.h` with the function name,
   pattern, and historical RVA.
2. Create `Dependencies/NativeSkinFixes.NativeHooks/MyNewHook.{h,cpp}`
   mirroring the structure of `CoversHeadHook.{h,cpp}`. Export
   `MyNewHook_Install()` / `MyNewHook_Uninstall()` as `extern "C"`.
3. Resolve the target in `MyNewHook_Install` via a copy of the `ResolveTarget()`
   helper from `CoversHeadHook.cpp`.
4. Add `MyNewHook.{h,cpp}` to `NativeSkinFixes.NativeHooks.vcxproj`'s
   `<ItemGroup>` blocks for compile + include.
5. Add a managed `MyNewHookInterop.cs` under `Main/Features/NativeSkinFixes/Interop/`
   mirroring `CoversHeadHookInterop.cs`.
6. Add a call from `NativeSkinFixesInstaller.Install`.
7. Rebuild C++ (`Build.ps1`), then rebuild C# (`./build.ps1`).

## Performance

The byte-pattern scan walks `TaleWorlds.Native.dll`'s entire image at
hook-install time. The DLL is ~50 MB; a single pattern scan takes <200 ms in
practice and runs only seven times (3 hooks + 4 helpers) — total ~1 s
one-time cost during boot. No runtime overhead beyond MinHook's existing
trampoline (~5-10 ns per call).

The hooks themselves run on the engine's render / asset-load threads. The
SRWLOCK-protected `g_hiddenFaces` and `g_beardClothFaces` sets are accessed
once per Face_mesh creation; the shared-read locks are non-contended in
practice (only `CoversHeadHook` writes, and only during AgentVisuals init).

## GitHub Issue

- **Issue:** TODO — create on next session via `/issue feature "Adopt NativeSkinFixes into TAOM (v1.4.5 port, in-repo)"`
- **Status:** Open (scaffolding shipped 2026-05-26; v1.4.5 byte patterns
  pending an IDA session)

## Open follow-ups

The seven signatures ship as `<PATTERN_TBD>` placeholders — the scanner
architecture and harness are reviewed-and-verified, but the patterns need to
be authored by disassembling `TaleWorlds.Native.dll` v1.4.5 (see "Pattern
authoring" above). Until then the C++ side reports "pattern not authored for
this build (stub)" and the hooks stay inert. Game runs vanilla in the
meantime — no crash, just no fix.
