# CrashReport

## Overview

Comprehensive crash diagnostic capture for TAOM. When an exception escapes any of 5 TaleWorlds lifecycle methods (or one of the allowlisted native-to-managed callback shims in `Native2ManagedTargets`), TAOM captures a full snapshot of process, game, and Harmony state, writes it to the debug log + a per-crash ZIP bundle, optionally suspends BUTR's exception handler, and surfaces a 2-button native dialog to the player.

This feature is **inspired by but not a port of** [BetterExceptionWindow](https://www.nexusmods.com/mountandblade2bannerlord/mods/3535) (BEW) v8.0.0 — BEW is GNU AGPL v3, so we authored TAOM-native equivalents from scratch using BEW only as a design reference for *what to patch* and *what to display*. The TaleWorlds API surface BEW patches is itself uncopyrightable.

## Why This Exists

Players running TAOM hit exceptions all the time — third-party mod conflicts, vanilla edge cases, save-game corruption, our own bugs. Before CrashReport, they got:

- Vanilla red error screen (often unhelpful and discards game state)
- Silent freezes (when the throw is inside a Harmony Transpiler's JIT prep)
- BUTR ButterLib's stock crash window (limited diagnostic surface)

After CrashReport, every crash produces a structured `Logs/taom_debug_*.log` entry plus a `Logs/taom_crash_<timestamp>_<sig>.zip` containing:

- `report.txt` — full diagnostic dump (sectioned plain text)
- `report.json` — machine-parseable equivalent
- `taom_debug.log` — current session's TAOM log
- `rgl_log.txt` — current session's TaleWorlds RGL log
- `diag.log`: TAOM.Dependencies' log, carrying PatchShield's swallowed `MissingMethod` / `MissingField` / `TypeLoad` exceptions (the engine-mismatch signature) when the path resolves
- `manifest.txt` — file inventory + size + SHA1

Players upload one ZIP, we reproduce locally without playing twenty questions.

## Architecture

```
HarmonyFinalizer ──────► CrashReportPatchHelper ──► ICrashReportService.HandleException
   (5 lifecycle methods)                               │
AppDomain.UnhandledException ────────────────────────► │
Native2ManagedPatcher (allowlisted shims) ───────────► │
                                                       │
                                                       ▼
                                          ComposeContext (calls every collector)
                                                       │
                                                       ▼
                              ┌────────────────────────┼─────────────────────────┐
                              ▼                        ▼                          ▼
                       PlainTextRenderer        JsonRenderer            CrashBundleWriter
                              │                        │                          │
                              ▼                        ▼                          ▼
                       IModLogger              (bundle JSON)            Logs/taom_crash_*.zip
                       (taom_debug.log)
                              │
                              ▼
                      CrashNotifier (InformationManager.ShowInquiry)
```

### Catch points (Patch37_CrashReport)

| # | Type.Method | Why |
|---|---|---|
| 1 | `TaleWorlds.DotNet.Managed.ApplicationTick` | Top-of-stack tick |
| 2 | `TaleWorlds.MountAndBlade.Module.OnApplicationTick` | Module tick |
| 3 | `TaleWorlds.ScreenSystem.ScreenManager.Tick` | Screen system tick |
| 4 | `TaleWorlds.ScreenSystem.ScreenManager.Update` (private, no-arg) | Screen system update |
| 5 | `TaleWorlds.MountAndBlade.Mission.Tick` | Mission tick. Its body is one native call (`MBAPI.IMBMission.Tick`), so it catches only a managed throw that unwinds back out through native code; mission behaviours' `OnMissionTick` runs later from `Mission.OnTick` and is caught at row 2 |
| 6 | Native2Managed allowlist (`Native2ManagedTargets.All`): `EngineScreenManager_PreTick`, `EngineScreenManager_LateTick`, `EngineScreenManager_Update`, `ManagedScriptHolder_TickComponents`, `ThumbnailCreatorView_OnThumbnailRenderComplete`, `RenderTargetComponent_OnPaintNeeded` | Native-to-managed callback shims whose managed work no row above wraps; attached by hand at startup |

Plus `AppDomain.CurrentDomain.UnhandledException` as a final safety net.

Rows 1 to 5 run at Harmony priority 800, matching BEW, and route through `CrashReportPatchHelper.HandleAndSwallow`. The row 6 bridge (`Native2ManagedBridge.Finalizer`) is attached with `new HarmonyMethod(...)` and no priority, so it runs at Harmony's default 400; it reaches `HandleAndSwallow` only while Enable Native-to-Managed Capture is on. Harmony runs a finalizer on every call of its target, with a null `__exception` when nothing threw, and that path returns at once. Every exception a finalizer hands back (the bridge with the toggle off, and `HandleAndSwallow` when capture is off, the service is unreachable or a capture is already on the stack) goes through `RethrowStackPreserver.PreserveForRethrow`, so Harmony's rethrow keeps the throw site. Four more targets (`ScriptComponentBehavior.OnTick`, `MissionView.OnMissionScreenTick`, `MissionBehavior.OnMissionTick`, `MBSubModuleBase.OnSubModuleLoad`) were removed on 2026-09-24: they are base virtuals with empty or assert-only bodies, and a finalizer on a base method never runs for an override, so they could never fire. `Patch37TargetShapeTests` now refuses an overridable virtual target.

### Re-entry guard

Two layers of protection prevent infinite recursion if a collector / renderer itself throws:

1. `CrashReportPatchHelper._onPatchStack`, a thread-static `bool`. Set by every Finalizer entry; if already set, hands the original exception back with its throw site preserved (lets vanilla / BUTR take over).
2. `CrashReportService._handling` (also thread-static) — short-circuits a second `HandleException` call on the same thread.

### Bundle deduplication / throttle

The thread-static re-entry guards above stop a *single* `HandleException` from recursing — they do **not** stop the *same crash from being captured on successive ticks*. Because the catch points are per-tick/per-frame lifecycle methods, a crash that recurs every frame (the normal failure mode for a broken mission/campaign — the same NRE throws each tick) called `_bundleWriter.Write(...)` on every cycle. Each cycle re-zipped the ever-growing `taom_debug.log` (which `WriteToLog` had just appended the full report to), producing **hundreds of monotonically-growing `taom_crash_*.zip` files** at a few-second cadence. (Reported 2026-06-15.)

[`CrashBundleThrottle`](../../Main/Features/CrashReport/CrashBundleThrottle.cs), a pure, lock-guarded, clock-injected session singleton, sits at the `HandleException` chokepoint (consulted right after the BUTR-suspend block, before the heavy `ComposeContext`). The signature is computed cheaply up front (frozen stack only, no engine collectors); on any non-`WriteBundle` decision the method returns at once, skipping collection, bundle, and the player inquiry. It writes a suppression log line only on occurrences 1, 2, 10, 100, 1000 and so on of that signature (`CrashBundleThrottle.IsLoggedOccurrence`), so a throw that recurs every frame no longer writes one line per frame into the log the bundle tails. This simultaneously caps disk writes **and** breaks the growing-log feedback loop.

Three session-scoped layers (reset only on a fresh game launch; constants in `CrashReportIoC`, **not** MCM):

| Layer | Rule | Decision |
|---|---|---|
| **Dedup** | a signature that already produced a bundle is never re-bundled | `SuppressDuplicate` |
| **Cap** | at most **25** distinct bundles per session | `SuppressCap` |
| **Cooldown** | a *new* signature within **30 s** of the last write is held off — but **not** marked bundled, so it can still get its one bundle later | `SuppressCooldown` |

The very first bundle is never cooldown-gated (`_bundlesWritten > 0` guard), so the real crash is always captured. Net effect: a recurring crash produces **exactly one** zip; a degenerate "different exception every tick" burst is bounded by cap + cooldown. Unit-tested in [`CrashBundleThrottleTests`](../../TAOM.Tests/Features/CrashReport/CrashBundleThrottleTests.cs).

### BUTR coexistence

TAOM ships ButterLib 2.11.0 via `TAOM.Dependencies`, which contains its own `ExceptionHandlerSubSystem`. By default we suspend it on the first capture via reflection — `ButterLibExceptionHandlerAdapter.TrySuspend()` calls `ExceptionHandlerSubSystem.Instance.Disable()`. This avoids both BUTR's window and ours firing for the same crash. The user can disable our suspension via MCM (`TAOM — Crash Report → Master → Suspend BUTR Exception Handler`).

## Configuration (MCM)

Settings live on a dedicated MCM page **TAOM — Crash Report** (separate from the main `TaomSettings` page).

| Group | Setting | Default | Effect |
|---|---|---|---|
| Master | Enable Crash Capture | true | Master toggle. When off, every Patch37 finalizer and the AppDomain hook hand exceptions back uncaptured (the Patch37 path returns the raw exception, so the rethrow loses its throw site unless PatchShield wraps that method). Live: the patches are always installed. |
| Master | Suspend BUTR Exception Handler | true | On first capture, calls ButterLib's `Disable()`. |
| Master | Enable Native-to-Managed Capture | true | When off, the allowlisted callback-shim finalizers hand exceptions back uncaptured, with the throw site recorded (`RethrowStackPreserver`). Live: the shims are always patched. |
| Bundle | Write Crash Bundle ZIP | true | Produces `Logs/taom_crash_*.zip`. Set false if you only want the log line. |
| QA — Dev Triggers | Throw On Next Mission Tick | false | Throws `TaomDevTriggerException` on the next `MissionLogic.OnMissionTick`. Auto-resets. |
| QA — Dev Triggers | Throw On Next Application Tick | false | Throws `TaomDevTriggerException` on the next `Module.OnApplicationTick`. Auto-resets. Works on the main menu (no mission needed). |

## Data captured (sections in `report.txt`)

Each section is gathered by a dedicated collector. Any collector that throws is reported in the `Collector failures` section at the bottom — partial reports are always written.

| Section | Source |
|---|---|
| Identity | `Native` + `TAOM` module versions, Bannerlord.exe FileVersion, TAOM.dll SHA1, language code |
| Exception | Type, message, source, HResult, target site, stack trace, `Exception.Data` dictionary, full inner chain (≤10 deep) |
| Stack Frames | Per-frame method, declaring type/assembly, file/line (when PDB present), IL offset |
| Harmony Correlation | Patches affecting **every** frame in the stack, plus full inventory (every patched method grouped by owner). Harmony replacement frames (`Foo_PatchN`) are resolved back to their original method via `Harmony.GetOriginalMethodFromStackframe` before the patch lookup — without this every patched frame printed `(no patches)`, exactly the frames that matter (#339, fixed 2026-07-13) |
| Modules | Per-mod: id, version, load index, official flag, main DLL SHA1, manifest path, declared deps, dep-order inversion flag, XML files declared |
| Assemblies | Every loaded assembly with name, version, location, GAC flag |
| Campaign | UniqueGameId, time, hero (level/clan/kingdom/gold/renown/influence/race), party (size/wounded/prisoners/morale/food/tier histogram), current settlement or map position, recent CampaignEvents ring buffer |
| Mission | Scene, mode, time-of-day, teams (alive/total per team), player formations (count/orders), player agent (health/pos/mount/wielded item) |
| TAOM State | Career, special resources, cultural feats, revolt tuning, active messengers — populated when feature adapters are wired (v1 ships with stubs; subsystems can register state providers later) |
| MCM Settings | Reflected snapshot of every `AttributeGlobalSettings` / `AttributePerCampaignSettings` provider (TAOM + third-party) |
| Process | Working set, private bytes, GC totals + gen 0/1/2 counts, thread count, uptime, throwing-thread id+name+bg+apartment |
| System Memory | Process priv/ws/heap **plus** system commit used/limit, headroom + %, available/total physical, memLoad %, and a MEMORY PRESSURE verdict. Rendered `(unavailable)` wholesale when the reader fails, never as zeros |
| GPU | Per adapter: name, driver version, driver date, VRAM, video processor (via WMI `Win32_VideoController`) |
| Display | Resolution, refresh rate, monitor count |
| OS | Description, version, 64-bit flag, CPU count, architecture, locale, CLR version |
| AppDomain & Environment | Friendly name, base dir, trust flag, env vars matching `BANNERLORD_* / TAOM_* / DOTNET_* / STEAM_*` |
| Performance | Last N frame deltas (ms), avg, FPS — currently empty ring buffer (v1 doesn't wire the frame-time hook; reserved for v2) |
| Logs | TAOM debug log path + tail, RGL log path + tail (newest across `%ProgramData%\Mount and Blade II Bannerlord\logs\` and the `Documents` equivalent, probed in that order), TAOM.Dependencies `diag.log` path + tail |
| Collector failures | Per-section reason if any collector threw |

### The memory verdict (#385 follow-up)

**#385 was diagnosed BY a figure this bundle did not carry.** A tester's facegen CTD was attributed
to commit exhaustion (20.3 GB against a 31.6 GB limit on a 16 GB machine, managed heap only 654 MB)
and doing so required hand-parsing a 1.3 GB minidump. The bundle held `WorkingSet64` and
`PrivateMemorySize64` and nothing else: no commit, no headroom, no physical state. An
out-of-memory death and a logic fault looked identical.

The verdict now sits on **line 4 of `report.txt`, above the exception**, and is repeated in
`manifest.txt` so the ZIP self-describes without being unzipped:

```
Memory:    MEMORY PRESSURE - privMB=4211 wsMB=3900 heapMB=654 (managed 15% of private), commit 29847/31646MB, headroom 1799MB (5%)
```

Three choices worth knowing before changing any of it:

| Choice | Why |
|---|---|
| **The threshold is not defined here.** `MemoryPressureVerdict.IsUnderPressure` calls `MemoryPressureSampler.IsLowHeadroom` | Those constants already have a C#/Python mirror, and a deep review caught that pair drifting in the integer-floor band. A third copy is how it recurs. `IsUnderPressure_AgreesWithMemoryPressureSamplerForTheSameInputs` asserts the two never fork across a boundary table |
| **Native-vs-managed is reported, not classified** (`managed 15% of private`, no "native-dominant" label) | Labelling it means inventing a ratio threshold nobody can defend. `TableauDiagnostics.LogRenderCensus` sets the precedent: it states observations because an earlier version asserted a conclusion that the decompile later refuted. "managed 3%" reads as native-dominant to any reader and carries nothing to drift |
| **`SystemMemorySnapshot?` is a nullable SIBLING of `ProcessSnapshot`, not extra fields on it** | `ProcessSnapshot` has a `?? new ProcessSnapshot(0, ...)` fallback, so widening it would force a fabricated `0` into `availPhysMB` on every failed read, and the renderer could not tell that from a real and alarming reading. Null renders `(unavailable)` instead, the same omit-on-failure discipline as `MemStats()` and `MemoryProbeReportFormatter` |

The token names (`privMB`, `wsMB`, `heapMB`, `sysCommitUsedMB`, `sysCommitLimitMB`, `availPhysMB`,
`memLoad`) are byte-identical to `MemoryPressureSampler.FormatSample`, so one `grep privMB` over an
unzipped bundle hits the report, the manifest, the `[MemSample]` trajectory in the bundled
`taom_debug.log` and the `[BattleLoad]`/`[MemStation]` lines alike. The reader is
`MemorySampleReader` (`GlobalMemoryStatusEx` + `GetProcessMemoryInfo`), reached across the feature
boundary on purpose rather than duplicated: see
[battle-load-diagnostics.md](battle-load-diagnostics.md).

### Crash signature

`CrashSignatureCalculator.Compute(exception, originatingPatchTarget, topFiveStackFrameNames)` produces a SHA1 hash over the exception's IDENTITY, the originating patch target, and the top five frame names. Lets us dedup crash reports: multiple players hitting the same bug produce the same signature even with totally different game state. Short prefix (8 chars) goes into the bundle filename for quick visual matching.

**The identity is the whole inner chain, not just the outer type** (#552, added 2026-09-06). `DescribeIdentity` walks `InnerException` to `InnerChainDepth` (3), appending each inner type and its `TargetSite`, and stops early on a self-referential chain. An exception with no inner chain still yields exactly its type name, so signatures already in the wild keep pointing at the same crash.

Hashing the outer type alone was a real failure, not a theoretical one. Every crash dispatched through the Gauntlet UI arrives as `TargetInvocationException @ ScreenManager.Update` over the same eight frames, so the outer identity is a constant and any two such crashes collided. In bundle `31942985` two clicks in one broken menu produced a `NullReferenceException` and then an `IndexOutOfRangeException`; the throttle suppressed the second as a duplicate, and it was the one that named the state corruption behind a fatal CTD three seconds later. The chain was only reconstructable because the raw `rgl_log.txt` rode along in the surviving bundle.

**A preserved throw site joins the identity when a shield recorded one** (2026-09-22, player bundle `2d446100`). PatchShield and SaveShield hand non-swallowed exceptions back to Harmony, which rethrows them with `throw`, and that used to replace the stack trace at every shielded method the exception crossed. `RethrowStackPreserver` (`Dependencies/Foundation`) now keeps the frames: the report's `StackTrace` prints the original frames first, with one `--- End of stack trace from previous location (rethrown by a Harmony finalizer on <method>) ---` line per rethrow, and the report's `Data` block carries `TAOM.ThrowSite`, the first five frames of the original throw. `DescribeIdentity` appends that site as `|site=...` after each level of the inner chain that carries one, because the frame list it hashes comes from `new StackTrace(ex)`, which only sees the segment after the last rethrow: before this, every NullReferenceException through the shielded `MapState.OnTick` hashed the same five frames, and occurrences #2 and #3 of `40de8e64` were suppressed unread. Per level, not just the outer: for a `TargetInvocationException` the outer's recorded site is the reflection path and the inner's `TargetSite` is the shielded wrapper, both constants, so only the inner's site separates two UI crashes. The `Stack Frames` and `Patches on throwing call stack` sections are built from `new StackTrace(ex)` too, so they still cover the last segment only, and when `TAOM.ThrowSite` is present the report says so under both headings; read the trace text for the inner frames. An exception that never crossed a shield has no site and keeps its old signature; one that did gets a new signature once, on purpose. RCA: [rca-shield-rethrow-stack-2026-09-22.md](../reviews/rca-shield-rethrow-stack-2026-09-22.md).

Both `CrashReportService` call sites use the same overload, the cheap early one that feeds `CrashBundleThrottle` and the one inside `ComposeContext`. They MUST agree: deriving the identity two different ways would admit a bundle under one signature and file it under another. The walk stays cheap enough for the early path because it reads only already-materialised managed state, no collectors.

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/CrashReport/Domain/](../../Main/Features/CrashReport/Domain) | 19 record types, pure CLR DTOs, no TaleWorlds deps (ADR-007) |
| [Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs](../../Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs) | Sectioned text format for log + clipboard |
| [Main/Features/CrashReport/Rendering/JsonCrashReportRenderer.cs](../../Main/Features/CrashReport/Rendering/JsonCrashReportRenderer.cs) | Newtonsoft.Json serialization for bundle |
| [Main/Features/CrashReport/Rendering/CrashBundleWriter.cs](../../Main/Features/CrashReport/Rendering/CrashBundleWriter.cs) | ZIP via in-BCL `System.IO.Compression.ZipArchive` |
| [Main/Features/CrashReport/Collectors/](../../Main/Features/CrashReport/Collectors) | 14 collectors + 5 utility classes (RingBuffer, DllHasher, CrashSignatureCalculator, ExceptionFrameBuilder, StackFrameSnapshotBuilder) |
| [Main/Features/CrashReport/Adapters/ButterLibExceptionHandlerAdapter.cs](../../Main/Features/CrashReport/Adapters/ButterLibExceptionHandlerAdapter.cs) | Reflection-only adapter for ButterLib's `ExceptionHandlerSubSystem.Disable()` |
| [Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs](../../Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs) | 5 Harmony Finalizer patches + category marker |
| [Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs](../../Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs) | Attaches the crash finalizer to the callback-shim allowlist, and holds `Native2ManagedBridge` |
| [Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs](../../Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs) | The callback-shim allowlist, one stated reason per entry |
| [Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs](../../Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs) | `AppDomain.CurrentDomain.UnhandledException` safety net |
| [Main/Features/CrashReport/CrashReportService.cs](../../Main/Features/CrashReport/CrashReportService.cs) | Composes context, renders, writes log + bundle, notifies player |
| [Main/Features/CrashReport/CrashReportIoC.cs](../../Main/Features/CrashReport/CrashReportIoC.cs) | DryIoc singleton registrations |
| [Main/Features/CrashReport/CrashReportSettings.cs](../../Main/Features/CrashReport/CrashReportSettings.cs) | MCM settings page |
| [Main/Features/CrashReport/DevTriggers/](../../Main/Features/CrashReport/DevTriggers) | QA-only throw triggers + tagged `TaomDevTriggerException` |
| [Main/Features/CrashReport/UI/CrashNotifier.cs](../../Main/Features/CrashReport/UI/CrashNotifier.cs) | `InformationManager.ShowInquiry` 2-button dialog (Continue / Open Bundle Folder) |

## Dependencies

- `System.Management` (.NET Framework 4.7.2 BCL) — WMI for GPU info
- `System.IO.Compression` + `System.IO.Compression.FileSystem` (.NET BCL) — ZIP bundle
- `HarmonyLib` 2.4.2 — Finalizer attribute + reflection patching
- `Newtonsoft.Json` 13.0.3 (already a TAOM PackageReference) — JSON renderer
- `Bannerlord.MCM` 5.12.1 (existing) — settings page
- `TaleWorlds.{DotNet,Engine,MountAndBlade,ScreenSystem}` — patch targets, type references

No new third-party dependencies.

## Tests

[TAOM.Tests/Features/CrashReport/](../../TAOM.Tests/Features/CrashReport), among them:

- `ExceptionFrameBuilderTests` — depth cap, null handling, inner-chain walking, `Data` dictionary
- `StackFrameSnapshotBuilderTests` — null exception, real thrown exception
- `CrashSignatureCalculatorTests`: deterministic, sensitive to exception type and origin, ignores frames beyond depth 5, separates two crashes that differ only in their INNER exception while still deduplicating identical ones (#552), keeps an inner-less exception's signature identical to the old plain-type hash, caps the inner-chain walk, separates two exceptions of one type through one frame when a shield preserved different throw sites (`TAOM.ThrowSite`), including two `TargetInvocationException`s whose INNER sites differ, and ignores a non-string value under that key
- `RethrowStackPreserverTests` (`TAOM.Tests/Infrastructure/Dependencies/`, 19): real in-process Harmony patches pinning the premise (a value-returning finalizer's rethrow drops the throw-site frame), PatchShield's own two finalizers keeping it across one and two rethrows, the recorded site staying the innermost and exactly five frames long, idempotency, the same instance returned, the rethrow counter kept apart from swallows, a priority-800 reporter finalizer seeing live frames before PatchShield's 400 preserves (and Patch37 declaring a priority above `Priority.Normal`), and IL call presence of the preserver on every shield rethrow path
- `RingBufferTests` — push order, overflow chronological, clear, capacity, empty snapshot
- `Patch37TargetShapeTests` (`BindingVerification`): every Patch37 target resolves, through the same resolver as `HarmonyPatchBindingTests`, and is not an overridable virtual
- `Native2ManagedTargetsTests`: every allowlisted shim resolves against the installed engine (`BindingVerification`); the list is exactly the six reviewed shims, small and distinct; a missing assembly, type or method, or a lookup that throws, is reported and skipped
- `Native2ManagedBridgeTests`: with the toggle off the bridge hands back the same exception with `TAOM.ThrowSite` recorded; with the toggle on and the service unreachable its hand-back still names the throw site after a `throw` of the same object (what Harmony's wrapper does); with no exception it returns null
- `CrashReportPatchHelperTests`: `HandleAndSwallow` with the service unreachable hands back the same instance, which keeps its throw site across that `throw`; with no exception it returns null
- `CrashBundleThrottleTests`: dedup, session cap and cooldown admission, and the 1, 2, 10, 100 suppression-log cadence
- `PlainTextCrashReportRendererTests`: all 19 sections render, signature in header, inner exception chain, collector failures, and the "last segment only" note under both frame sections exactly when `TAOM.ThrowSite` is present

Components NOT unit-tested (per ADR-008 + the plan's "Not-tested:" trailer policy):

- The 5 Harmony Finalizers: `ModuleOnApplicationTickFinalizer` is exercised in game by both MCM dev triggers (the mission trigger's `OnMissionTick` runs inside `Module.OnApplicationTick`); the other four have no trigger. Both triggers return early while Enable Crash Capture is off, so they cannot test the master toggle's pass-through
- The live Native2Managed attach: only the `attached N of M` launch log line checks it; no trigger throws inside a callback shim, so the native toggle's pass-through has no in-game check
- `CrashReportService.HandleException` composition — depends on 14 collectors, each touching TaleWorlds APIs; integration-only
- TaleWorlds-facing collectors (`Modules`, `Assemblies`, `Campaign`, `Mission`, `Process`, `Gpu`, `Logs`) — best-effort with try/catch on every field; failure surfaces in `CollectorFailures` section
- MCM settings reflection collector — reflection over loaded third-party assemblies, hard to fixture
- `CrashNotifier` — `InformationManager.ShowInquiry` requires a running engine
- `ButterLibExceptionHandlerAdapter` — reflection wrapper, exercised by integration

## How-To

### Trigger a test crash to validate the pipeline

1. Launch the game with TAOM.
2. Open MCM → **TAOM — Crash Report** → **QA — Dev Triggers**.
3. Toggle either:
   - **Throw On Next Application Tick** — fires on the main menu (no mission needed).
   - **Throw On Next Mission Tick** — start a mission first, then flip.
4. Within ~1 tick: the Inquiry dialog appears with "Exception: TAOM.Features.CrashReport.DevTriggers.TaomDevTriggerException" plus paths to the log + bundle.
5. The MCM toggle auto-resets to OFF.

### Verify a captured crash

```bash
# Find the most recent bundle.
ls -t Logs/taom_crash_*.zip | head -1

# Inspect contents:
unzip -l Logs/taom_crash_<...>.zip

# Read the plain-text report:
unzip -p Logs/taom_crash_<...>.zip report.txt | less
```

Grep the live log for the report (each line is prefixed `[CrashReport]`):

```bash
grep '\[CrashReport\]' Logs/taom_debug_*.log
```

### Disable a captured section

Crash UI doesn't expose per-section toggles in v1 — every collector runs by default. To skip an expensive collector, edit `CrashReportSettings` and add a new toggle, then gate the collector call in `CrashReportService.ComposeContext`.

### Coexist with another crash mod (BEW, third-party)

Default: TAOM's handler wins on the Patch37 targets (rows 1 to 5: priority 800, first registration, suspends BUTR). The row 6 callback shims carry TAOM's bridge at Harmony's default 400, so another mod's finalizer above 400 on those shims runs before it; whether BEW patches them is unverified. If you want another mod's UI:

1. MCM → **Master** → uncheck **Suspend BUTR Exception Handler**.
2. MCM → **Master** → uncheck **Enable Crash Capture**.

No restart is needed before TAOM's first capture of the session: TAOM's finalizers stay installed but pass every exception through, so the other mod's Finalizers take over. After a capture with Suspend BUTR on, TAOM has already called ButterLib's `Disable()`, and nothing in TAOM re-enables it: re-enable it on ButterLib's own MCM page or restart the game.

## Performance

- **Boot cost: one `harmony.Patch` per target, plus one PatchShield attach per Native2Managed target at the first game start** (PatchShield's pass 2 shields every patched method not declared in a TAOM assembly, outside its namespace exclusions, and `ManagedCallbacks` is not excluded). Each attach cost about 120 to 190 ms on the maintainer's desktop on 2026-09-23 (the old sweep of all 247 `*CallbacksGenerated` methods cost 29 to 33 s on 30 of 30 launches, and PatchShield timed 186 ms per attach in the same process). On 11 player processes the same 247-method sweep took 0 to 1 s, under about 8 ms per attach ([followup-patch-tax.md](../../plans/_audit/2026-09-23-opus/followup-patch-tax.md)), so the saving is large on that desktop and about a second for players. The allowlist still drops 241 attaches at boot and about as many PatchShield attaches at the first game start. `[CrashReport] Native2Managed: attached N of M Finalizer(s) in X ms` in `taom_debug.log` shows the current cost.
- **Steady-state cost: small, not zero.** Harmony runs a finalizer on every call of its target (with a null `__exception` on success, which returns at once), and the patched method becomes a replacement wrapped in try/catch.
- **Suppression log cadence trades recency for volume.** A throw that recurs every frame logs its 1000th occurrence after about 17 s at 60 fps and its 10,000th after about 3 minutes, so before a later hard crash the last suppression line can be minutes old. The bundle (occurrence 1) and the lines at 2, 10 and 100 remain.
- **WMI for GPU info** runs once per captured crash (not per tick). ~50ms typical.
- **MCM reflection collector** scans every loaded assembly for `AttributeGlobalSettings<>` derivatives — runs once per captured crash. ~10ms typical.

## Risks & Known Limitations

- **Other mods' `OnSubModuleLoad` throws are not captured.** TAOM applies Patch37 inside its own `OnSubModuleLoad`, and a finalizer on the base `MBSubModuleBase.OnSubModuleLoad` would never see an override's throw, so none is attached; those throws land in vanilla or BUTR.
- **Most native-to-managed callbacks are no longer wrapped.** The 2026-09-24 allowlist keeps six of the 247 shims. A managed throw inside any of the other 241 (for example `Mission_OnAgentRemoved`, `Mission_MeleeHitCallback` or `Agent_UpdateAgentStats` in `CoreCallbacksGenerated`) is caught only if it unwinds through native code into a Patch37 finalizer such as `Mission.Tick`; whether it can is UNVERIFIED, so such a throw may now reach vanilla's handler, or crash to desktop with no bundle. The sixth entry is the tableau render callback `RenderTargetComponent_OnPaintNeeded`, which raises `RenderTargetComponent.PaintNeeded`, the render function each `TableauView.AddTableau` caller registers (`CharacterTableau`, `ItemTableau`, `BannerTableau`, `MapConversationTableau` in v1.5.3). It replaced `BannerlordTableauManager_RequestCharacterTableauSetup` on 2026-09-24 (#650), because nothing in v1.5.3 assigns that one's `RequestCallback`. Which thread native runs the tableau and thumbnail callbacks on is UNVERIFIED. Adding the mission combat callbacks back is decided but not done: see "Mission combat callbacks" below.
- **Mission combat callbacks (decided 2026-09-24, not yet applied).** The maintainer decided to put the combat callbacks back on the allowlist. The trace (below) finds a verified path into TAOM code for each, but several can arrive off the main thread (`harmony-patches.md`, "Which thread runs your target"), and a bridge capture there is not tagged off-main, so `CrashReportService` would run the Mission and Campaign collectors and `CrashNotifier`'s inquiry on that thread, which its own comments call unsafe. The entries wait for that to be settled.

  | Callback (`CoreCallbacksGenerated`) | Managed target (v1.5.3) | TAOM code reached |
  |---|---|---|
  | `Mission_MeleeHitCallback` | `Mission.MeleeHitCallback` | `OnMeleeHit` (`SignatureStrikesMissionLogic`); `TaomCombatMechanicsModel.DecideWeaponCollisionReaction` (campaign); `RegisterBlow` into `Agent.HandleBlow`: `Mission.OnAgentHit` (`BehaviorTreeMissionLogic.OnAgentHit`, `CareerPerkMissionBehavior.OnScoreHit`), `Agent.Die` and `Agent.HandleBlowAux` (BlowDiagnostics and Spider patches) |
  | `Mission_MissileHitCallback` | `Mission.MissileHitCallback` | the same `RegisterBlow` chain |
  | `Mission_ChargeDamageCallback` | `Mission.ChargeDamageCallback` | the same `RegisterBlow` chain |
  | `Mission_FallDamageCallback` | `Mission.FallDamageCallback` | the same `RegisterBlow` chain |
  | `Mission_MissileAreaDamageCallback` | `Mission.MissileAreaDamageCallback` | the same `RegisterBlow` chain |
  | `Mission_OnAgentHitBlocked` | `Mission.OnAgentHitBlocked` | `Mission.OnAgentHit`: `BehaviorTreeMissionLogic.OnAgentHit`, `CareerPerkMissionBehavior.OnScoreHit` |
  | `Mission_GetDefendCollisionResults` | `Mission.GetDefendCollisionResults` | `MissionCombatMechanicsHelper.GetDefendCollisionResults`: `TaomCombatMechanicsModel.DecideCrushedThrough` (campaign) |
  | `Mission_OnAgentRemoved` | `Mission.OnAgentRemoved` | `OnAgentRemoved` in `BehaviorTreeMissionLogic`, `CareerPerkMissionBehavior`, `EnlistmentMeritMissionBehavior`, `FieldCommissionMissionLogic`, `MountDespawnMissionBehavior`; `Agent.OnRemove` into `BehaviorTreeAgentComponent.OnAgentRemoved` |
  | `Mission_OnAgentDeleted` | `Mission.OnAgentDeleted` | `OnAgentDeleted` in `BehaviorTreeMissionLogic`, `AdvancedCombatBehavior`, `CareerPerkMissionBehavior`, `MixedFormationsMissionBehavior`, `MountDespawnMissionBehavior`, `SignatureStrikesMissionLogic` |
  | `Mission_OnAgentShootMissile` | `Mission.OnAgentShootMissile` | `OnAgentShootMissile` in `BehaviorTreeMissionLogic`, `ElephantMissionBehavior` |

  Not combat, left out: `Agent_OnDismount` and `Agent_OnMount` (`Mission.OnAgentDismount`/`OnAgentMount`: `WargMissionBehavior`, `BehaviorTreeMissionLogic`) and `Agent_OnAgentAlarmedStateChanged` (`BehaviorTreeMissionLogic`). `Mission_GetAgentState` reaches no TAOM override.
- **Crash UI re-entry.** A throw in our own collector or renderer would loop. Two layers of thread-static `_handling` flags break the loop and let the original exception bubble out to vanilla.
- **ZIP bundle write to `Logs/`.** If `Logs/` is read-only or full, only the log line lands (no bundle). Bundle write is wrapped in try/catch.
- **Frame timing buffer not yet populated.** v1 instantiates the `FrameTimingBuffer` singleton but no hook pushes into it. v2 will add a `Mission.Tick` / `ScreenManager.Tick` Postfix that samples `dt`. For now the section shows 0 samples.
- **TAOM-state collectors are stubs.** `CareerStateSnapshot`, `SpecialResourceEntry`, etc. are part of the DTO but the collector returns null/empty until each feature wires a state-provider lambda. Comprehensive captures of these will land per-feature as PRs ship.

## Future Enhancements (v2 candidates)

- Frame-timing Postfix hook
- CampaignEvents ring buffer subscription (subscribe to every CampaignEvents.* at session start)
- Per-feature state providers (career, resources, feats, messengers, revolt) wired into `TaomStateCollector`
- Gauntlet overlay replacing the InquiryData dialog (richer formatting, copyable sections, screenshot button)
- Per-section MCM disable toggles
- Save-game attachment toggle (currently the player attaches manually)
- Crash signature de-dup ring buffer (suppress duplicate-signature crashes after N within a session)

## Changelog

- 2026-09-24: **Boot cost and dead finalizers** (plan 006). The Native2Managed sweep patched all 247 `*CallbacksGenerated` methods and cost 29 to 33 s of every launch on the maintainer's desktop (0 to 1 s on player machines); it is now an allowlist of six shims (`Native2ManagedTargets`) and logs its attach time. Four Patch37 finalizers on empty base virtuals were deleted. Both MCM toggles are read at capture time (the `OnSubModuleLoad` gate always saw a null MCM instance) and no longer ask for a restart. Suppression log lines for a recurring crash are written at occurrences 1, 2, 10, 100 and so on.
- 2026-09-24: **Maintainer decisions** (#650). The sixth allowlist entry is now `RenderTargetComponent_OnPaintNeeded`, the tableau render callback, in place of the never-armed `BannerlordTableauManager_RequestCharacterTableauSetup`. Every exception the bridge or `CrashReportPatchHelper.HandleAndSwallow` hands back now goes through `RethrowStackPreserver.PreserveForRethrow`, so Harmony's rethrow keeps the throw site. The bridge stays at Harmony priority 400, the suppression log keeps its powers-of-ten cadence with no time floor, and capture stays on by default with Enable Native-to-Managed Capture as its toggle. Adding the mission combat callbacks back waits on off-main-thread capture (see Risks).
- 2026-09-01: **Added the System Memory section + the header memory verdict** (#385 follow-up). The bundle carried no commit or headroom figure, which is the exact number #385 was diagnosed by. Reuses `MemorySampleReader` and delegates the threshold to `MemoryPressureSampler.IsLowHeadroom` rather than copying its constants; the snapshot is a nullable sibling record so a failed read renders `(unavailable)` instead of zeros.

- 2026-06-15 — Deduplicated crash bundle ZIPs: new session-scoped `CrashBundleThrottle` (dedup + ≤25/session cap + 30s cooldown) at the `HandleException` chokepoint so a per-tick recurring crash produces exactly one zip instead of hundreds; 10 throttle tests added.
- 2026-05-25 — Codex adversarial review (Review 41): 8 confirmed findings fixed (2 HIGH including post-reload disposed-logger Finalizers and a decorative `EnableCrashCapture` toggle, plus 4 MED / 2 LOW such as the dead per-frame Harmony-correlation block).
- 2026-05-25 — Initial feature: TAOM-native comprehensive crash diagnostic capture — 10 Harmony Finalizers (Patch37_CrashReport) + `*CallbacksGenerated` reflection hooks + AppDomain safety net, full sectioned `report.txt`/`report.json` capture, ZIP bundle, ButterLib coexistence, and a dedicated MCM page.

## GitHub Issue

- **Issue:** #650 (plan 006: crash-capture boot cost, live toggles and the callback allowlist)
- **Status:** Open. Owed in game: the boot time and the `attached 6 of 6` line, live MCM toggling, and the probe for a throw in a dropped callback (for example a behaviour's `OnAgentRemoved`)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-load-diagnostics.md](./battle-load-diagnostics.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/troubleshooting.md](../modding/troubleshooting.md)
- [docs/reference/provenance-register.md](../reference/provenance-register.md)

<!-- backlinks-end -->
