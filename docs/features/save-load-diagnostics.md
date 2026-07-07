# SaveLoadDiagnostics (Patch61)

## Overview

Always-on `[SaveLoad]` lifecycle logging for save-game writes and loads. Captures the real exception the engine swallows behind the generic **"A problem occured while trying to load the saved game."** dialog, names the exact saved type/SaveId/behavior whose data failed, detects definer/build mismatches the engine silently null-fills, and catches bad save WRITES on the async writer thread at write time. Companion offline tool: `tools/inspect_sav.py`.

## Why This Exists

Multiple players reported "corrupted saves" (2026-07-07). Investigation mapped the engine's load pipeline and found it records effectively nothing:

- The dialog fires from `SandBoxSaveHelper.LoadGameAction` (SandBoxSaveHelper.cs:261-282) only when `MBSaveLoad.LoadSaveGameData` returns **null**.
- The real failure happens inside `LoadContext.Load` (LoadContext.cs:54-249), whose catch block is `catch (Exception ex) { Debug.Print(ex.Message); return false; }` — stack and inner exceptions discarded. The fill loops run under `TWParallel`, so the printed message is usually AggregateException's "One or more errors occurred."
- `LoadResult.CreateFailed` carries the hardcoded error string "Not implemented" (SaveManager.cs:164-167). `LoadResult.MetaData` is not set on failure.
- TAOM's CrashReport (Patch37) never fires — nothing escapes.
- A save written by a build with a *different* SaveableTypeDefiner set loads with **silently null objects**: `ObjectHeaderLoadData.CreateObject` leaves `LoadedObject` null when `TryGetTypeDefinition(SaveId)` misses, and `LoadContext.Load` skips containers whose `GetObjectTypeDefinition()` returns false — no engine log either way.
- Prior proven corruption (#292): an unserializable `[SaveableField]` made `GameData.Write` NRE on the `AsyncFileSaveDriver` background thread → the .sav on disk was corrupt from birth → discovered only at the next load.
- Field triage was blind twice over: the shipped "v2.0.9" label spanned 34 commits, and save metadata carried no TAOM build identity.

## Architecture

Thin hooks (ADR-002) → `ISaveLoadDiagnosticsService` (strings/Exception only across the boundary, ADR-007) → `IModLogger` → `Logs/taom_debug_*.log` (FileLogger flushes per line — the last stamp survives a frozen process).

**Two Harmony categories, both applied in `OnSubModuleLoad`** (loads fire from the main menu; the late `OnGameInitializationFinished` batch would miss the first load — Patch58 precedent):

| Category | Hooks | Why split |
|---|---|---|
| `Patch61_SaveLoadDiagnostics` | 10 hooks on public engine types (`typeof` targets) | — |
| `Patch61_SaveLoadDiagnostics_Reflection` | `ContainerLoadData_Fill_Patch`, `CampaignBehaviorDataStore_LoadBehaviorData_Patch` (internal engine types via `AccessTools.TypeByName`) | Engine drift in an internal type name must not kill the typeof-based hooks; each category has its own try/catch in SubModule.cs |

**Invariant: diagnostics never alter engine behavior.** Every Finalizer returns `__exception` unchanged; no Postfix writes `__result`; every hook body try/catch-swallows its own faults; the service swallows logger faults.

**Thread model:** hooks fire on TWParallel load workers and the async save-writer thread. The service is lock-free (Interlocked seq + fault counter, ConcurrentDictionary dedup) and fault-throttled (20 fault lines per attempt, 50 distinct unknown SaveIds) so a systematically corrupt graph cannot flood the log.

## Log contract

`[SaveLoad] seq=NN t=+1234ms phase=<SaveLoadPhase> key=value ...`

| Phase | Emitted by | Means |
|---|---|---|
| `LoadRequested` | `SandBoxSaveHelper.TryLoadSave` Prefix | Load clicked; lifecycle reset |
| `ModuleCheck` | same | Save identity: appVersion, created, character, `taomBuild`, full module:version list |
| `GraphFault` | `LoadContext.CreateLoadData` / `ContainerLoadData.*` Finalizers | **The money stamp** — exact failing type, SaveId, object idx / container step + flattened exception chain + stack |
| `UnknownSaveId` | `ObjectHeaderLoadData.CreateObject` / `ContainerHeaderLoadData.GetObjectTypeDefinition` Postfixes | Save carries a SaveId this build has no definition for (definer/build mismatch); deduped |
| `BehaviorSyncFault` | `CampaignBehaviorDataStore.LoadBehaviorData` Finalizer | Named behavior's SyncData failed (type changed between builds) |
| `LoadFault` | `SaveManager.Load` Finalizer/Postfix | Uncaught NRE (unreadable file) or `LoadResult.Successful=false` |
| `LoadFailed` | `MBSaveLoad.LoadSaveGameData` Postfix | Null LoadResult — the dialog is now on screen; cause is in the stamps above |
| `LoadDataOk` / `AllBehaviorDataLoaded` | milestones | Deserialization / behavior data completed |
| `SaveBegin` | `SaveManager.Save` Prefix | Save lifecycle reset |
| `SaveWriteFault` | `FileDriver.Save` Finalizer/Postfix | Write threw ON the async writer thread (#292 class), or non-Success `SaveResult` (disk full / antivirus / OneDrive lock) |
| `SaveStatusFault` / `SaveCompleted` | `SaveOutput.PrintStatus` | Faulted-task AggregateException at `Game.OnSaveCompleted` (#292 signature) / terminal result + serialization errors |

**Build stamp:** `MBSaveLoad_GetSaveMetaData_Patch` writes `TAOM_Build` (assembly version + informational version) into every save's metadata via the upsert indexer — inert to the engine (MetaData serializes its whole dict to JSON; only known keys are read). Future saves self-identify their exact build in `ModuleCheck` and in `inspect_sav.py` output.

## Offline inspector — `tools/inspect_sav.py`

`python tools/inspect_sav.py <path.sav> [--verify]` — stdlib-only. Dumps ApplicationVersion, CreationTime, character, `TAOM_Build`, module:version table. `--verify` inflates the raw-deflate data region and walks `GameData.Write`'s section layout: verdicts OK / TRUNCATED / corrupt with byte offsets. Exit codes: 0 ok, 1 unreadable metadata, 2 data-region failure.

.sav format (FileDriver.Save + MetaData.Serialize): `[4-byte LE JSON length][UTF-8 JSON metadata {"List":{...}}][raw-deflate GameData]`.

Triage split it gives without launching the game: **zero/garbage header** → save corrupt from birth (interrupted write — crash mid-save, antivirus, OneDrive sync); **metadata OK + truncated deflate** → partial write; **fully OK but fails in-game** → build/definer incompatibility → read the `[SaveLoad]` stamps.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/SaveLoadDiagnostics/Domain/SaveLoadPhase.cs` | Log-contract phase names (do not rename casually) |
| `Main/Features/SaveLoadDiagnostics/SaveLoadDiagnosticsService.cs` | Lock-free emit/fault/dedup core |
| `Main/Features/SaveLoadDiagnostics/Hooks/*.cs` | 12 thin hooks (see table above) |
| `Main/SubModule.cs` (Patch61 block, `OnSubModuleLoad`) | Init + two-category application, fail-safe |
| `tools/inspect_sav.py` | Offline .sav triage |
| `TAOM.Tests/Features/SaveLoadDiagnostics/SaveLoadDiagnosticsServiceTests.cs` | 17 service tests (format, throttle, dedup, flatten, parallel) |

## Tests

Service: 17 tests (seq/reset semantics, Aggregate/TargetInvocation flattening, stack line, 20-fault throttle + reset, unknown-SaveId dedup + reset, logger-throw resilience, 100-way parallel seq distinctness). Hooks: all 12 targets drift-guarded by `HarmonyPatchBindingTests` against the installed engine (the reflection hooks via their `TargetMethod(s)` members).

## How-To: triage a user's failing save

1. Get the user's failing `.sav` + `Logs/taom_debug_*.log` from one failed load attempt on an instrumented build.
2. `python tools/inspect_sav.py <save> --verify` → which build/modules wrote it; is the file physically intact?
3. Grep the log for `[SaveLoad]`: `GraphFault type=...` names the failing type → map to TAOM's SyncData surfaces/definers; `UnknownSaveId` → definer/build mismatch; `BehaviorSyncFault behavior=...` → that behavior's SyncData type changed; `SaveWriteFault` in an OLDER log → the save was corrupt from birth (#292 class / environment interference).

## Dependencies

`IModLogger`/`FileLogger` (Main/Core/Logging). No MCM, no config, no SyncData of its own.
