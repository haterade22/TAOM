# Investigation brief — a player's `TAOM.NativeSkinFixes.dll` fails to load (Win32 error 126)

> Hand this to a fresh session: "Read `docs/investigations/native-skin-fixes-load-failure-2026-06-18.md` and investigate."

## Symptom (from a player's TAOM log, 2026-06-18)

```
[WARNING] [NativeSkinFixes] TAOM.NativeSkinFixes.dll failed to load — feature inert.
Detail: LoadLibrary failed (Win32 error 126). Expected DLL at:
G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\bin\Win64_Shipping_Client\TAOM.NativeSkinFixes.dll
```

- **Win32 error 126 = `ERROR_MOD_NOT_FOUND`** — on a `LoadLibrary`, this means **either the DLL itself OR one of its DLL dependencies could not be found.** It is NOT "access denied" or "bad image" — it's "a module in the dependency chain is missing." This is the single most important clue: a *present* DLL still throws 126 when a **dependency** is absent.
- The feature failed **gracefully** (the managed wrapper is try/caught and goes inert) — not a crash. But the covers_head morph fix + hair/beard cloth simulation are OFF for this player.
- The player's install is on **`G:\SteamLibrary\`** (a different drive than the dev's `E:\Steam\`), and they are heavily modded (RTSCamera, MBSuperSpeed, ProvenSteel, UpgradeableWorkshops, EquipmentUpgradeMod, Palantir.Debugger, etc.). The mod list is almost certainly a red herring for THIS issue, but note it.

## What NativeSkinFixes is (TAOM facts — CLAUDE.md "Key Paths" + `docs/features/native-skin-fixes.md`)

- Managed wrapper (3 P/Invoke interop classes + an installer) over the native `TAOM.NativeSkinFixes.dll`. Loaded from `OnBeforeInitialModuleScreenSetAsRoot`, uninstalled from `OnSubModuleUnloaded`. Editor-mode skip.
- The native DLL is **vendored** at `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` (deploys to `<game>/Modules/TAOM/bin/Win64_Shipping_Client/`). It is **gitignore-allowlisted** (shipped binary, not built by `TAOM.sln`).
- It vendors **MinHook 1.3.4** as **`MinHook.x64.dll`** in the SAME bin folder — a hard runtime dependency (the hooks install via MinHook). If `MinHook.x64.dll` is absent next to the DLL, `LoadLibrary` returns 126.
- C++ source: `Dependencies/NativeSkinFixes.NativeHooks/` (standalone `.vcxproj`, NOT in `TAOM.sln`). Rebuild: `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1`. The `.vcxproj` writes the DLL straight into the bin folder. Hooks find targets via byte-pattern scan (`Signatures.h`).
- Prior RCA (port discipline): `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`.

## Hypotheses to test, in priority order

1. **[MOST LIKELY] Static-vs-dynamic CRT — missing VC++ Redistributable.** The native DLL is MSVC-built. If the `.vcxproj` `<RuntimeLibrary>` is `MultiThreadedDLL` (`/MD`), the DLL dynamically links `vcruntime140.dll` / `msvcp140.dll` / `vcruntime140_1.dll`, and any player **without the VC++ 2015-2022 x64 redistributable installed** gets exactly this 126. **Check `<RuntimeLibrary>` first — it's one line and likely decides the whole thing.** The clean fix for a mod shipped to arbitrary machines is `/MT` (`MultiThreaded`, static CRT) so the DLL is self-contained and needs no redist. (Bannerlord itself ships the redist, but a fresh/partial install or a non-default drive may not have it registered.)
2. **`MinHook.x64.dll` not in the shipped package.** It's gitignored, so verify it is actually in the **released distribution** (Steam Workshop item / Nexus zip), not just the dev's local bin. If the packaging step copies `TAOM.NativeSkinFixes.dll` but not `MinHook.x64.dll`, every player hits 126.
3. **The DLL itself wasn't deployed for this player** — a partial/failed mod update, or a manual install that missed the `bin/` subfolder. The log says "Expected DLL at G:\..." — confirm the file physically exists there for this player (ask them / check the package).
4. **MinHook itself needs the CRT** — if MinHook.x64.dll is `/MD`, it has the same redist dependency as #1. Inspect its imports too.
5. **[LOW] AV / security tool** blocking the unsigned native DLL, or a non-ASCII path issue on `G:\SteamLibrary`. Unlikely given 126 (not 5/ACCESS_DENIED), but note it.

## Investigation plan

1. **Dump the DLL's imports.** Use `python tools/pe_inspect.py <path-to>/TAOM.NativeSkinFixes.dll` (TAOM has it) OR `dumpbin /dependents TAOM.NativeSkinFixes.dll` (VS dev prompt) OR `Dependencies.exe` (lucasg/Dependencies). List every imported DLL. Flag: `MinHook.x64.dll`, `VCRUNTIME140*.dll`, `MSVCP140*.dll`, `api-ms-win-crt-*`, `ucrtbase.dll`. Repeat for `MinHook.x64.dll`.
2. **Read the runtime-library setting:** open `Dependencies/NativeSkinFixes.NativeHooks/*.vcxproj`, find `<RuntimeLibrary>` in BOTH Debug and Release `<ItemDefinitionGroup>`/`<ClCompile>`. `MultiThreadedDLL` = needs redist (the bug); `MultiThreaded` = static/self-contained.
3. **Audit the shipped package contents** — what does the TAOM Steam Workshop / release actually place in `Modules/TAOM/bin/Win64_Shipping_Client/`? Confirm BOTH `TAOM.NativeSkinFixes.dll` AND `MinHook.x64.dll` are present in the *distributed* artifact (check the publish/packaging script, not just local disk).
4. **Confirm with the player** (or a clean VM): does `MinHook.x64.dll` sit next to the failing DLL, and does installing the **VC++ 2015-2022 x64 redist** fix it? That single test discriminates hypothesis 1 vs 2/3.

## Deliverable

Root cause (which hypothesis, with PE-import evidence) + the fix:
- **If `/MD` + redist-missing (expected):** switch the `.vcxproj` (and MinHook if rebuilt) to `/MT` static CRT, rebuild via `Build.ps1`, re-vendor the DLL. Self-contained, no player-side redist needed. This is the durable fix for a widely-distributed mod.
- **If MinHook missing from the package:** fix the packaging/publish step to include `MinHook.x64.dll`.
- Either way: document the dependency + requirement in `docs/features/native-skin-fixes.md`, and consider a clearer in-game/log message that names the *likely* cause (missing VC++ redist) rather than just "Win32 error 126."

Apply the C++ port discipline in CLAUDE.md "Native C++ port discipline" + `.claude/skills/deep-review` C++ checks for any code change.
