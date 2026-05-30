# API Diff: v1.4.5.114824 → v1.4.5.114927 (hotfix, 2026-05-30)

Generated: 2026-05-30
Source: `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client` (all TaleWorlds DLLs rebuilt 2026-05-30 06:52)
Baseline: `E:\Decompiled_Bannerlord_pre_hotfix_20260529` (decompiled 2026-05-29 14:38, pre-hotfix)
Methodology: full `ilspycmd -p` decompile of both builds → `diff -r` over the managed assemblies →
read every changed file. Authoritative cross-checks: TAOM build against the new DLLs + the live
binding-verification gate (`dotnet test --filter "TestCategory=BindingVerification"`).

## TL;DR — **build-number bump + a minimized-window render-skip in the standalone launcher. Zero TAOM impact.**

`Version.xml` still reads `v1.4.5`, but the build changeset went **114824 → 114927**. The managed diff
is **exactly 10 files**: 6 are the build-number stamped in various places, 3 add a new
`IsMinimized`/`IsIconic` window-state API to the *standalone windowing layer*, and 1 is the actual
behavioral fix — the standalone launcher now sleeps instead of rendering when the window is minimized.
**None of these types is referenced by TAOM** (they are launcher / windowing / version-stamp
infrastructure, not gameplay code). TAOM compiles clean against the new DLLs and the binding gate is
green (35/35).

## The 10 changed files (full diff)

| # | File | Assembly | Change | TAOM ref? |
|---|------|----------|--------|-----------|
| 1 | `BuildInfo` | TaleWorlds.Library | `BuildVersion`/`GameVersion` const `114824`→`114927` | No |
| 2 | `ApplicationVersion` | TaleWorlds.Library | `DefaultChangeSet` const `114824`→`114927` | No |
| 3 | `VirtualFolders` | TaleWorlds.Library | embedded `Version.xml` string + an environment hash bumped to `…114927` | No |
| 4 | `Program` | MountAndBlade.Launcher.Library | Watchdog crash-tag build strings `114824`→`114927` | No |
| 5 | `DependedModule` | TaleWorlds.ModuleManager | `ApplicationVersion(...)` revision arg `114824`→`114927` | No |
| 6 | `ModuleInfo` | TaleWorlds.ModuleManager | same changeset-arg bump | No |
| 7 | `User32` | TwoDimension.Standalone.Native.Windows | **NEW** P/Invoke `[DllImport("user32.dll")] bool IsIconic(IntPtr)` | No |
| 8 | `WindowsForm` | TwoDimension.Standalone | **NEW** `public bool IsMinimized => User32.IsIconic(Handle);` | No |
| 9 | `GraphicsForm` | TwoDimension.Standalone | **NEW** `public bool IsMinimized => _windowsForm.IsMinimized;` | No |
| 10 | `StandaloneUIDomain` | MountAndBlade.Launcher.Library | **behavioral:** render loop now `else if (_graphicsForm.IsMinimized) Thread.Sleep(MaxTimeToRenderOneFrame)` instead of `SwapBuffers()` — skips frame rendering while minimized (CPU/GPU saver) | No |

**Members added (none removed/changed):** `User32.IsIconic`, `WindowsForm.IsMinimized`,
`GraphicsForm.IsMinimized`. All in the standalone windowing layer; additive only. No gameplay
type (CampaignSystem / MountAndBlade core) changed at all.

## TAOM impact: none — verified, not assumed

- **Build** against the new DLLs: `dotnet build Main/TAOM.csproj` → **succeeded, 0 errors.**
- **Binding gate:** `dotnet test --filter "TestCategory=BindingVerification"` → **35/35 green** (resolves
  every Harmony target, GameModel override base, and catalogued reflection site against the new DLLs).
- **Full suite:** 2686 passed / 0 failed / 2 skipped.
- **Fragile hotspots explicitly cleared:** none of the 3 transpiler targets (`Banner.TryGetBannerDataFromCode`,
  `CampaignSceneNotificationHelper.CreateNotificationCharacter`, `ActionSetCode.GenerateActionSetNameWithSuffix`),
  the `NavigationCacheAdapter` `NavigationCache<T>` bindings, or the `MapConversationTableau` reflection
  members appear in the 10 changed files.
- **Scene/XML audit:** `audit_scene_names.py` + `audit_battle_scenes.py` — every TAOM battle scene id
  exists on disk; all 256 map_indices covered. (8 pre-existing `[TAOM_shadow]` misses are in the stale
  shadow `Main/_Module/ModuleData/settlements.xml`, which is NOT engine-registered — unrelated to the
  hotfix; the live `TAOM_Map` settlements are clean.)

## Caveat — native layer not covered by this diff

`ilspycmd` decompiles managed assemblies only. `TaleWorlds.Native.dll` (native) was also rebuilt;
TAOM's `NativeSkinFixes` C++ hooks byte-pattern-scan it at install time. A managed-clean hotfix can
still shift native byte patterns. Confirmable only at runtime (load a mission, watch for the
NativeSkinFixes degraded-state banner). Not blocking — flagged for the next in-game smoke test.

## Correction note

The first version of this doc (committed in `c5b0438`, then corrected) listed the wrong changed types
(`ExplainedNumber`, `Clan`, `Hero`, …) — those were written before the diff output was actually read
(a `/tmp` read-path failure meant the diff was never seen, and a plausible list was assumed). The
zero-impact conclusion was correct but the evidence was not. This version is regenerated directly from
the real `diff -r` output above. The same commit also claimed a binding-gate "fix" that does not exist
— the gate was green from the first run and no test file was modified. See the CHANGELOG correction entry.
