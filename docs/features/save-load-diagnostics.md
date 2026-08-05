# SaveLoadDiagnostics (Patch61)

## Overview

Always-on `[SaveLoad]` lifecycle logging for save-game writes and loads. Captures the real exception the engine swallows behind the generic **"A problem occured while trying to load the saved game."** dialog, names the exact saved type/SaveId/behavior whose data failed, detects definer/build mismatches the engine silently null-fills, and catches bad save WRITES on the async writer thread at write time. Companion offline tools: `tools/inspect_sav.py` (triage) + `tools/repair_sav_strings.py` (recovery for the momentum >32 KB corruption — see the RCA below; player how-to: `docs/SAVE-REPAIR-GUIDE.md`).

**First real-world find (2026-07-07):** this stack root-caused the multi-user v2.0.9 "corrupted save" reports — `WarOfTheRingMomentum` serialized its event log as one `SyncData` string that crossed the engine's 32,767-byte archive-entry limit, corrupting the save at write time (`ArchiveSerializer` writes entry length as `(short)Data.Length`). The `ArchiveDeserializer.LoadFrom` hook stamps it live; `repair_sav_strings.py` recovers already-bricked saves. Full write-up: `docs/reviews/rca-momentum-save-corruption-2026-07-07.md`. The permanent fix (chunked momentum serialization) is in `docs/features/war-of-the-ring-momentum.md`.

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

Thin hooks (ADR-002) → `ISaveLoadDiagnosticsService` (strings/Exception only across the boundary, ADR-007) → `IModLogger` → `Logs/taom_debug_*.log`. `[SaveLoad]` stamps are INFO, and since 2026-07-16 `FileLogger` writes INFO/WARNING/ERROR **synchronously on the calling thread** — so the last stamp survives a frozen process *and* a hard crash. Before that it was queued to a background writer, which covered the freeze case but lost the queue on a crash (#350); this feature's whole premise is stamping the exact failing type/SaveId/chunk, so that durability is load-bearing, not incidental. DEBUG remains async and is still dropped by a hard crash — never read the last DEBUG line as the stopping point.

**Four Harmony categories, all applied in `OnSubModuleLoad`** (loads fire from the main menu; the late `OnGameInitializationFinished` batch would miss the first load — Patch58 precedent):

| Category | Hooks | Why split |
|---|---|---|
| `Patch61_SaveLoadDiagnostics` | 12 hooks on public engine types (`typeof` targets) | — |
| `Patch61_SaveLoadDiagnostics_ContainerFill` | `ContainerLoadData_Fill_Patch` (internal type) | Harmony aborts a category on the FIRST failing class, so each reflection-target hook gets its own category — one drifted internal type can't kill a sibling. Each category has its own try/catch in SubModule.cs, and every `TargetMethod(s)` logs the specific missing binding (Patch57 precedent). |
| `Patch61_SaveLoadDiagnostics_BehaviorData` | `CampaignBehaviorDataStore_LoadBehaviorData_Patch` (internal type; covers `LoadBehaviorData` + `SaveBehaviorData`) | same |
| `Patch61_SaveLoadDiagnostics_ArchiveParse` | `ArchiveDeserializer_LoadFrom_Patch` (internal type) | same |

**Invariant: diagnostics never alter engine behavior.** Every Finalizer is **void** — it reads `__exception` but cannot replace or swallow it, and Harmony keeps true-rethrow semantics (original stack preserved for downstream consumers like CrashReport). No Postfix writes `__result`; every hook body try/catch-swallows its own faults; the service swallows logger faults.

**SaveShield interplay (review 2026-07-07 HIGH):** TAOM.Dependencies' SaveShield finalizes `SandBoxSaveHelper.TryLoadSave`, `MBSaveLoad.LoadSaveGameData`, both `SaveManager.Load` overloads, and `LoadResult.InitializeObjects/AfterInitializeObjects` at default priority and **swallows** exceptions (its recovery design). SaveShield installs earlier (TAOM.Dependencies loads before TAOM), so at equal priority its finalizers run first and Patch61 would see `__exception == null`. Every Patch61 Finalizer therefore carries `[HarmonyPriority(Priority.First)]` — it observes and logs the original exception, then SaveShield swallows exactly as before.

**Thread model:** hooks fire on TWParallel load workers and the async save-writer thread. The service is lock-free (Interlocked seq + fault counter, ConcurrentDictionary dedup) and fault-throttled (20 fault lines per attempt, 50 distinct unknown SaveIds) so a systematically corrupt graph cannot flood the log.

## Log contract

`[SaveLoad] seq=NN t=+1234ms phase=<SaveLoadPhase> key=value ...`

| Phase | Emitted by | Means |
|---|---|---|
| `LoadRequested` | `SandBoxSaveHelper.TryLoadSave` Prefix | Load clicked; lifecycle reset |
| `ModuleCheck` | same | Save identity: appVersion, created, character, `taomBuild`, full module:version list |
| `GraphFault` | `LoadContext.CreateLoadData` / `ContainerLoadData.{InitializeReaders,FillCreatedObject,Read,FillObject}` / `HeaderLoadData_Readers` (`InitialieReaders` ×2 + `ContainerHeaderLoadData.CreateObject`) / `ArchiveDeserializer.LoadFrom` Finalizers | **The money stamp** — exact failing type, `SaveId.GetStringId()`, object idx / container step / header phase / raw chunk size + flattened exception chain + stack. `kind=` distinguishes object / container / objectHeader / containerHeader / archiveParse |
| `UnknownSaveId` | `ObjectHeaderLoadData.CreateObject` / `ContainerHeaderLoadData.GetObjectTypeDefinition` Postfixes | Save carries a SaveId this build has no definition for (definer/build mismatch); deduped by `GetStringId()` |
| `BehaviorSyncFault` | `CampaignBehaviorDataStore.{Load,Save}BehaviorData` Finalizer | Named behavior's SyncData failed (`dir=load`: type changed between builds; `dir=save`: collection-pass fault before `SaveBegin`) |
| `LoadFault` | `SaveManager.Load` Finalizer/Postfix, `LoadResult.Initialize*` Finalizer | Uncaught exception (unreadable file / definer registration / metadata version), `LoadResult.Successful=false` (message says explicitly when NO interior stamp was captured), or a deferred `[LoadInitializationCallback]` throw |
| `LoadFailed` | `MBSaveLoad.LoadSaveGameData` Postfix | Null LoadResult — the dialog is now on screen; cause is in the stamps above |
| `LoadDataOk` / `ObjectsInitialized` / `AllBehaviorDataLoaded` | milestones | Deserialization / deferred init callbacks / behavior data completed. `ObjectsInitialized` matters: SaveShield swallows callback-phase exceptions into a silent half-load — a log ending at `LoadDataOk` with a `LoadFault step=InitializeObjects` stamp is that case |
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

Service: 20 tests (seq/reset semantics, Aggregate/TargetInvocation flattening incl. the composed Aggregate-wrapping-TIE production shape, stack line, 20-fault throttle + reset, `FaultCount` reset, unknown-SaveId dedup + 50-cap + reset, logger-throw resilience, 100-way parallel seq distinctness). Bindings: all 15 targets drift-guarded by `HarmonyPatchBindingTests` (via `TargetMethod(s)`), plus `SaveLoadDiagnosticsBindingTests` (4 tests) pinning what that suite can't see — the `ContainerHeaderLoadData` PROPERTY binding (a rename silently degrades container attribution to `<null>`), both `CampaignBehaviorDataStore` methods, `ArchiveDeserializer.LoadFrom(byte[])`, and `SaveId.GetStringId()`.

## Known limitations

- **Cross-attempt straggler stamps:** the async save-writer can still be flushing when the next save/load resets the lifecycle — a late `SaveWriteFault`/`SaveCompleted` can appear under the next attempt's seq numbers. Match by the `name='...'` field, not seq.
- **Unhooked sites (accepted):** `LoadContext.LoadString`'s per-entry reads and `ObjectHeaderLoadData.ResolveObject/AdvancedResolveObject` faults reach the engine swallow without a specific interior stamp — the `SaveManager.Load` Postfix then says explicitly that no interior stamp was captured (naming the unhooked phases) instead of pointing at nonexistent stamps. `ArchiveDeserializer.LoadFrom` covers the raw parse of those phases' chunks.
- **Pre-`SaveBegin` save faults:** `OnBeforeSave`/`SaveBehaviorData` fire before `SaveManager.Save`, so a fault there stamps `BehaviorSyncFault dir=save` under the previous attempt's lifecycle — unambiguous via the phase + dir fields.

## How-To: triage a user's failing save

1. Get the user's failing `.sav` + `Logs/taom_debug_*.log` from one failed load attempt on an instrumented build.
2. `python tools/inspect_sav.py <save> --verify` → which build/modules wrote it; is the file physically intact?
3. Grep the log for `[SaveLoad]`: `GraphFault type=...` names the failing type → map to TAOM's SyncData surfaces/definers; `UnknownSaveId` → definer/build mismatch; `BehaviorSyncFault behavior=...` → that behavior's SyncData type changed; `SaveWriteFault` in an OLDER log → the save was corrupt from birth (#292 class / environment interference).

## Dependencies

`IModLogger`/`FileLogger` (Main/Core/Logging). No MCM, no config, no SyncData of its own.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
