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

### Current values (Bannerlord v1.4.6 — authored 2026-06-30)

All 7 signatures are authored + statically verified against the installed v1.4.6
`TaleWorlds.Native.dll` (each is a single match at the RVA below). See the
"v1.4.6 native port" section below for the method and the RVA/verification map.

| Signature | v1.4.6 RVA | How pinned |
|-----------|-----------|-----------|
| `add_skin_meshes_to_agent_entity` (CoversHead) | `0x61C7D0` | interior byte-triangulation (prologue changed + heavy inlining) |
| `cloth_factory` (HairCloth) | `0x35B0C0` | interior byte-triangulation of the 1.3.15 factory (166 votes); rdx=mesh signature verified |
| `AddToList` | `0x0C3E90` | cloth_factory call graph (identical prologue) |
| `GpuInit` | `0x2936E0` | cloth_factory call graph (identical prologue) |
| `HasClothData` | `0x2C45A0` | cloth_factory call graph (identical prologue) |
| `NotifyPhysics` | `0x34BA20` | cloth_factory call graph (identical prologue) |
| `render_list_build` (FaceMeshObserve) | `0x625670` | +0xE0-from-submeshes fingerprint (identical prologue) |

RVAs are informational; the shipped DLL resolves each function by byte-pattern
scan at boot (survives minor relocation). Re-author per the workflow below when
porting to a new engine version.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NativeSkinFixes/NativeSkinFixesInstaller.cs` | Boot-time entry point, editor-mode skip, localized banner, IModLogger routing |
| `Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs` | LoadLibrary + GetProcAddress wrappers for `TAOM.NativeSkinFixes.dll` |
| `Main/Features/NativeSkinFixes/Interop/CoversHeadHookInterop.cs` | P/Invoke surface for `CoversHeadHook_Install` / `_Uninstall` |
| `Main/Features/NativeSkinFixes/Interop/HairClothHookInterop.cs` | P/Invoke surface for `HairClothHook_Install` / `_Uninstall` |
| `Main/Features/NativeSkinFixes/Interop/FaceMeshObserveHookInterop.cs` | P/Invoke surface for `FaceMeshObserveHook_Install` / `_Uninstall` |
| `Dependencies/NativeSkinFixes.NativeHooks/dllmain.cpp` | DLL entry — initializes logger on attach, uninstalls hooks on detach |
| `Dependencies/NativeSkinFixes.NativeHooks/Signatures.h` | Central registry of byte patterns + historical RVAs (v1.4.6 patterns authored 2026-06-30) |
| `tools/native_sig_author.py` | RE helper that authored the v1.4.6 patterns — RTTI vtable resolution, disasm, IDA-pattern uniqueness scan, rip-relative xref, function-start finder, old→new prologue `diff`, interior byte-triangulation. See "v1.4.6 native port" below. |
| `Main/Features/TaomSettings.cs` (`EnableNativeSkinFixes`) | MCM toggle "Native Skin Fixes → Enable Native Skin Fixes" gating the install at boot |
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

## Build & CRT requirement (static CRT is mandatory)

The native DLL **must link the C runtime statically.** This is the line between
"loads on every player's machine" and "fails with `LoadLibrary` Win32 error 126
for anyone without Visual Studio."

- **TAOM ships the Debug build** (built from Visual Studio). The vcxproj Debug
  config therefore sets `<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>`
  (`/MTd`, **static** debug CRT); Release sets `MultiThreaded` (`/MT`, static).
  Either is self-contained — never vendor a dynamic-CRT build.
- **Why it matters:** a dynamic CRT (`/MDd` debug, or `/MD` release) makes the
  DLL import `vcruntime140*.dll` / `msvcp140*.dll` / `ucrtbase*.dll`. The
  **debug** CRT (`*140d.dll`, `ucrtbased.dll`) is **not redistributable** — it
  ships only with Visual Studio — so a Debug `/MDd` build loads on a dev machine
  but errors 126 for every player. Installing the VC++ redist does NOT help: it
  contains the *release* CRT, not the debug CRT. MSBuild's Debug default with no
  explicit `<RuntimeLibrary>` is `/MDd` — that exact gap shipped a Debug DLL that
  failed for players (2026-06-18).
- **A correct static-CRT build imports only `MinHook.x64.dll` + `KERNEL32.dll`**
  (plus the OS-guaranteed `SHELL32.dll` / `ole32.dll` the static CRT pulls in —
  both present on every Windows machine, so neither causes error 126). Verify any
  rebuild with `python tools/pe_inspect.py
  Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` — any
  `VCRUNTIME*` / `MSVCP*` / `MSVCR*` / `ucrtbase*` / `api-ms-win-crt*` import means
  the build is dynamic and must be redone.
- **`MinHook.x64.dll` is a required sidecar.** It is dynamically linked, but its
  own only import is `KERNEL32.dll`, so it is itself self-contained. It must sit
  next to the plugin in `bin/Win64_Shipping_Client/`. The `.gitignore` allowlist
  force-includes both DLLs and the vcxproj `CopyMinHookSidecar` post-build target
  keeps it in sync.

**Three automated guards enforce this** (none replaces an in-game load test, but
they stop the regression at the source):

1. `Build.ps1` runs `pe_inspect.py` on its own output and `throw`s on any
   dynamic-CRT import before the DLL can be vendored.
2. The `check-native-dll-crt.sh` PreToolUse hook blocks a `git commit` that
   stages a dynamic-CRT DLL.
3. The `validate-xml` CI job (`.github/workflows/build.yml`) re-runs the same
   check on the committed binary.

> As of 2026-06-30 all 7 byte patterns are authored for v1.4.6 (see "v1.4.6
> native port" below), so the static-CRT-linked DLL both loads AND installs its
> hooks. The static-CRT fix remains a prerequisite for the DLL to load at all.

## Tests

- `TAOM.Tests/Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs` — 11
  tests covering the editor-mode skip predicate (null / empty / normal client
  / editor / mixed case / false-positive guard), the localization key wiring
  (key format + non-empty default), and the `EnableNativeSkinFixes` default
  (pins the MCM master toggle so a refactor can't silently change it).

The native interop layer (LoadLibrary, GetProcAddress, MinHook trampoline
install, byte-pattern scan against the live `TaleWorlds.Native.dll` image)
cannot be unit-tested — it requires a hosted Bannerlord process. Verify these
manually via the live-game checklist below.

## v1.4.6 native port (2026-06-30)

The port from the v1.3.15 reference to the installed v1.4.6 build, and the
tooling + hard-won lessons behind it. Context: TAOM was crashing / infinite-
loading into battle because a hand-built DLL carried v1.4.0-era offsets against
the v1.4.6 engine. All 7 targets were re-derived from scratch against the
installed `TaleWorlds.Native.dll`.

### The toolkit: `tools/native_sig_author.py`

No IDA/Ghidra was used — a scripted `capstone` + `pefile` helper did the work
(subcommands): `rtti <name>` (resolve a class's vtable RVA from its RTTI type
descriptor), `vtable <name>` (dump vtable slots), `disasm <rva>`, `scan
"<pattern>"` (IDA-pattern uniqueness test — same semantics as the C++ scanner),
`fxref <rva>` (fast rip-relative/`E8` xref), `funcstart <rva>` (int3-padding
boundary), `diff --old <dll>` (capture old prologues + scan the new DLL), and
the interior-triangulation logic used inline for the two hard functions. Point
it at the installed client DLL (its default) or `--dll <path>`.

### Methods that worked (and the order to try them)

1. **RTTI-anchored offset verification.** `Face_mesh` and
   `rglCloth_simulator_component` both keep RTTI type names in v1.4.6, so their
   vtables resolve directly. Every struct offset + vtable index the hooks use
   was proven against them — e.g. `Face_mesh vtable[0xA0]` = `mov eax,6;ret`
   (the type discriminator), `vtable[0xA8]` reads `[+0x1A0]` (cloth), cloth
   `vtable[0x1F0]` writes the scene to `[+0x28]`, `vtable[0xF0]` forwards via
   `[+0x48]` (renderable). The cloth/Face_mesh layout is STABLE v1.3.15→v1.4.6.
2. **Prologue diff** (`diff --old`). 5 of 7 targets have BYTE-IDENTICAL
   prologues in the genuine v1.3.15 DLL (AddToList/GpuInit/HasClothData/
   NotifyPhysics/render_list_build) — a pattern from either build matches both.
3. **Interior byte-triangulation** for the 2 functions whose prologues changed
   (`cloth_factory`, `add_skin_meshes`). Build a wildcarded byte-stream of the
   whole 1.3.15 function, slide a 40-byte window across it, scan each window in
   the new DLL, and for every window that matches exactly once compute
   `newRVA − windowOffset` — the mode of that value is the new function start.
   `add_skin_meshes` got 22 votes → `0x61C7D0`; the `cloth_factory` RCA below
   got 166.

### RCA: the shared-body sibling (why the first deploy threw)

The first v1.4.6 deploy hooked `0x35AF00` as `cloth_factory` — identified by its
body replicating the hook's cloth-registration writes (type dispatch,
`+0x1E8/+0x208` lists, cloth-ctor call). **It was the wrong function.** `0x35AF00`
is an ADJACENT SIBLING that shares that registration body but takes `rdx` as a
BYTE FLAG (`movzx r14d,dl`), not the mesh pointer. In-game, the HairCloth
post-process received `rdx` values like `0x18`/`0xD`/`0x1D` (indices) where it
expected a `Face_mesh*` → per-call access violation. The SEH `__except` caught
every one (no CTD, game playable) but the feature did nothing and spammed
`sample-AV` log lines.

The real factory is `0x35B0C0`: it does `mov rbx,rdx; mov rax,[rdx];
call[rax+0x28]` then `mov rax,[rbx]; call[rax+0xA0]` — it DEREFERENCES `rdx` as
the mesh and dispatches on its type, the exact `(rcx=factory, rdx=mesh)`
signature the hook needs. It was pinned by interior triangulation of the 1.3.15
factory (166 votes), its prologue is byte-identical to 1.3.15 (the prologue
never changed — the earlier "changed prologue" note had compared the wrong
sibling), and all 12 factory-struct offsets match 1.3.15 (96% whole-body
overlap).

**LESSON (recorded in `Signatures.h` + LESSONS-LEARNED): a shared-body sibling
defeats structural body-matching. Pin by interior triangulation, and ALWAYS
verify the ARGUMENT SIGNATURE (which register is the pointer you dereference),
not just that the body looks right.** After this, all three hooks' signatures
were re-checked: `cloth_factory` rcx=factory/rdx=mesh, `add_skin_meshes`
rcx=AgentVisuals/rdx=SkinGenParams\* (writes `[rdx]|=1`), `render_list_build`
rcx=Face_mesh — the last two were correct, which is why only HairCloth threw.

### Safety rails (added with the port)

- **MCM master toggle** `EnableNativeSkinFixes` (`TaomSettings`), gated in
  `SubModule.OnBeforeInitialModuleScreenSetAsRoot`, fail-closed if MCM isn't
  ready. Default ON for verified v1.4.6; flip OFF to fully disable.
- **Required-cloth-pair all-or-nothing.** HairCloth + FaceMeshObserve are a
  coupled pair (FaceMeshObserve suppresses the static hair HairCloth animates);
  if either fails to resolve, ALL hooks roll back (no lone-hook half-state).
  CoversHead is structurally optional — its failure never rolls back the pair —
  so a future engine bump that breaks only its pattern still ships the cloth fix.
- The scanner is **fail-closed**: a pattern miss (or `<PATTERN_TBD>` stub) → the
  hook doesn't install, never a wrong-address hook.

## How to author the byte patterns (when a Bannerlord patch breaks scanning)

> The fastest path is now `tools/native_sig_author.py` (see "v1.4.6 native port"
> above) rather than a manual IDA session — especially `diff --old <old-dll>`
> plus interior triangulation for functions whose prologue changed. The manual
> IDA workflow below still applies if you prefer it.

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

## Changelog

- 2026-06-30 — feat: author + verify all 7 byte patterns for **Bannerlord v1.4.6** and activate the feature (RTTI-anchored disassembly + interior byte-triangulation via `tools/native_sig_author.py`; no IDA). Added the `EnableNativeSkinFixes` MCM toggle (default ON) + required-cloth-pair all-or-nothing install. RCA in-line: the first deploy hooked a shared-body SIBLING of the cloth factory (`0x35AF00`, rdx=byte-flag) instead of the real `0x35B0C0` (rdx=mesh) → per-call SEH-caught AVs; fixed by triangulating the 1.3.15 factory (166 votes) and verifying the argument signature. **In-game confirmed** (v1.4.6.115628): full battle stable ~20 min, all 7 resolved, zero `sample-AV`.
- 2026-06-18 — fix: ship a static-CRT DLL (`/MTd`) so the native DLL loads for players instead of failing with `LoadLibrary` Win32 error 126; documented the Build & CRT requirement (the byte-pattern signatures still ship as `<PATTERN_TBD>` placeholders).
- 2026-05-26 — feat: adopt + port NativeSkinFixes into TAOM (v1.4.5, in-repo, pattern-scanning) — C++ source vendored under `Dependencies/NativeSkinFixes.NativeHooks/`, C# wrapper inlined into `TAOM.dll`, hardcoded RVAs replaced with byte-pattern scanning, unified logging, boot banner, and 8 installer unit tests.
- 2026-04-10 — feat: fork the community NativeSkinFixes mod into TAOM — covers_head morph (jazz-hands) fix + hair/beard cloth physics via a C++ native DLL with 3 MinHook detours and a C# P/Invoke interop layer (7 RVAs verified against Bannerlord v1.4.0).

## GitHub Issue

- **Issue:** TODO — create via `/issue feature "NativeSkinFixes v1.4.6 native port (all 7 patterns authored + verified)"`
- **Status:** v1.4.6 patterns authored + verified 2026-06-30. In-game confirmed
  same day (v1.4.6.115628): a full battle ran ~20 min, all 3 hooks installed,
  all 7 resolved at expected RVAs (`cloth_factory` at the corrected `0x35B0C0`),
  **zero `sample-AV`**. Stability confirmed; the cloth-rescue visual is not yet
  observed (see follow-up).

## Open follow-ups

- **Observe the cloth-physics effect on cloth-hair troops.** Stability is
  confirmed (zero AVs, battle stable), but the verification battle (looters +
  Gondor line) had no cloth-flagged hair/beard, so the hook fired without
  finding anything to rescue (no `sample-processing`/`sample-success` lines).
  Fight a **Rohan / elf / Dale** battle to see the hook actually register hair/
  beard cloth (the log should then show `sample-processing` with real Face_mesh
  pointers + `sample-success`), and to visually confirm the animated cloth.
- **CoversHead `+0x830` exactness.** `add_skin_meshes` is confirmed
  (`0x61C7D0`) and its mask write (`+0x00` bit 0x01) is verified. The
  `AgentVisuals+0x830` Face_mesh pointer read is verified in-bounds (the struct
  extends to `+0x8D0`) and is read-only (fed to a tracking set), so a slight
  mismatch is cosmetic at worst — but the exact field hasn't been traced to a
  callee write. Confirm the render suppression works in-game before relying on
  the covers_head fix visually.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/migration/dr3-maintenance.md](../migration/dr3-maintenance.md)

<!-- backlinks-end -->
