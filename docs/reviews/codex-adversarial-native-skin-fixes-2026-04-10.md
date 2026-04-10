# Codex Adversarial Review: NativeSkinFixes
**Date:** 2026-04-10
**Reviewer:** Codex (GPT-5.4)

## Summary
CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 1

## Known Suspects

1. **RVA VERSION SAFETY** -- CONFIRMED. No runtime version/hash/prologue validation before hook creation.
2. **THREAD SAFETY** -- DISPUTED. Locking strategy is sound; all set accesses go through SRWLOCK. Small timing windows exist but are not unsound lock usage.
3. **STRUCT OFFSET HARDCODING** -- CONFIRMED. No structural verification on any reverse-engineered offset.
4. **NOTIFY_PHYSICS OFFSET CALCULATION** -- CONFIRMED. Derived from relative RVA delta rather than its own passed RVA.
5. **DLL LOAD ORDER** -- DISPUTED. TaleWorlds.Native.dll is guaranteed loaded before managed submodules run; the starter library imports it for bootstrap.
6. **EDITOR DETECTION** -- DISPUTED as null-safety bug. The `MainModule?.FileName.Contains("wEditor") == true` pattern is null-safe. The wEditor heuristic itself remains unverified against all editor host executables.

## Findings

### HIGH -- Partial install without rollback
`NativeSkinFixesLoader.cs:34`: If `FaceMeshObserveHook` fails after earlier hooks succeed, the feature runs in a broken half-installed state. CoversHead and HairCloth may be active without the render list suppression hook, leading to visual artifacts. Fix: make install transactional -- if any hook fails, uninstall the ones that succeeded.

### MEDIUM -- DLL_PROCESS_DETACH under loader lock
`dllmain.cpp:13`: DLL_PROCESS_DETACH runs full MinHook teardown (MH_DisableHook, MH_RemoveHook) under loader lock. When `lpReserved != nullptr` (process termination), this is risky -- other DLLs may already be unloaded. Fix: skip heavy cleanup when `lpReserved != nullptr`.

### LOW -- SetDllDirectory mutates process-global state
`NativeHookLoader.cs:43`: SetDllDirectory temporarily changes the DLL search path for the entire process. If another thread loads a DLL concurrently, it could pick up the wrong directory. Fix: use LoadLibrary with an absolute path instead.
