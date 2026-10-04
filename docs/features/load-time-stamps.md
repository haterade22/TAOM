# Load-Time Stamps

## Overview

Load-time stamps split every load in `taom_debug.log` into where its time went. Always written: one
`[LoadXml]` line per module XML type the engine loads (files, XSLTs, merge time, object-creation
time) with a summary per game, the phase totals of TAOM's Harmony patch application, and one
`[Lifecycle]` dispatch line for each campaign dispatch of a new game, a loaded save and the session
start (what the new-game fan-out took). With **Enable Load-Time Stamps** on (Battle Load
Diagnostics page, default off), the log also gets one line per patch group, the steps of TAOM's
game-start and game-initialization hooks, and the time of every campaign handler of those
dispatches. What loads, and in what order, is unchanged.

Refs: plan 040 (`plans/040-load-time-stamps.md`), #722.

## Why This Exists

- **Vanilla behavior:** the engine's own log (`rgl_log`) prints a line per ModuleData file it opens
  and a few milestones ("Finished starting a new game."), and nothing per mod.
- **TAOM requirement:** load-time work (plan 042's XML merge fast path, and whatever follows it) has
  to be chosen and confirmed by numbers from players' machines, not by inference.
- **Without this feature:** a new campaign's loading screen lasted 50.2 s on the maintainer's desktop
  on 2026-10-02 (`plans/_audit/2026-10-02-perf/evidence/load/campaign-load-gaps.txt`: "window span
  50.239 s"). The engine log attributes about 28 s of it to the module XML merge
  (`campaign-load-xml.txt`: NPCCharacters 56 files 13.302 s, Items 142 files 9.592 s), but
  `taom_debug.log` said nothing about it, nothing about how long TAOM's patch groups take to apply,
  and nothing about TAOM's `OnGameStart` and `OnGameInitializationFinished` work. Two stretches of
  that load, 3.37 s and 3.18 s, had no line in either log.

## Architecture

### Patch groups

`PatchCategoryApplier.TryApply` times every category it applies, whether the apply succeeds or
throws (the record is written in a `finally`; the return value and the `FAILED` error line are
unchanged). `SubModule.cs` calls `EndPhase` right after each of the four apply phases' runner call:

| Phase name | Where | Once per |
|---|---|---|
| `OnSubModuleLoad` | `OnSubModuleLoad`, after `RunPhase(ApplyPhase.ProcessLoad, ...)` | process |
| `MainMenu` | the first `OnBeforeInitialModuleScreenSetAsRoot` | process |
| `GameInit` | the first `OnGameInitializationFinished` | process |
| `Mission` | the first `OnMissionBehaviorInitialize` | process |

`EndPhase` logs the phase's total line at once (always) and holds its per-category records. The
per-category lines wait because the toggle that decides them cannot be read in `OnSubModuleLoad`
(MCM builds its settings after every module's `OnSubModuleLoad`), and whether it is readable inside
TAOM's main-menu hook is unverified. So the first game initialization calls
`WriteHeldCategoryLines(DetailEnabled)`, which writes every held record (the `OnSubModuleLoad`,
`MainMenu` and `GameInit` categories, each line naming its phase) when the toggle is on and drops
them when it is off; the first mission does the same for its own phase. The measured overhead of the
timing is 0.384 microseconds per category (`TryApply_TimingOverhead_IsUnderFiftyMicrosecondsPerCategory`,
1,000 calls, 2026-10-03), against about 100 categories per process.

### TAOM's hook steps

`LoadTimeStampsHooks.StartHook` returns a `HookTimer` when the toggle is on (null otherwise, and
null on any fault). Each `Mark` logs the step's time since the previous step's line was written (the
hook's start, for the first), so a step leaves out the write of the line before it. If the clock
read right after that write fails, the next step starts at the pre-write tick (the one read just
before that write) instead, so it carries that one write but none of an earlier step. `End` logs
the hook's wall-clock total from its start and its step count, once. The total includes the lines'
own writes, so the steps add up to the total less those writes and any work after the last mark
(plus any carried write). Neither ever throws.

| Hook | Steps, in order | When |
|---|---|---|
| `OnGameStart` | `session_snapshot`, `hand_wired` (every hand-wired model and behavior), `feature_modules` | every game start |
| `OnGameInitializationFinished` | `save_load_stamp`, `monster_size`, `armour_gate` | every game initialization, before the once-per-process guard |
| `GameInitOnce` | `patch_categories`, `manual_patches`; the Harmony census is the rest of the total | the first game initialization only |

### The toggle

`LoadStampDetailGate` reads `IBattleLoadDiagnosticsSettingsProvider.LoadTimeStampsEnabled` and logs
the `[LoadStamps] detail` line whenever its state (on, off, or unreadable) differs from the last one
it logged. A read that throws counts as off and logs a WARNING naming the exception (H3); the next
readable read logs H2 again, even when the value is still off, so a log always says why its detailed
lines are missing.

### XML loads (always on)

`Patch100_LoadTimeStamps_LoadXml` patches two engine methods, applied by the module at
`OnSubModuleLoad` for every player whatever the toggle (its apply time is in the
`[PatchApply] phase=OnSubModuleLoad scope=total` line, and in its own category line when the toggle is on):

- `MBObjectManager.LoadXML(string id, bool isDevelopment, string gameType, bool skipXmlFilterForEditor)`:
  a prefix starts a `LoadXmlCall` on a thread-local stack in `LoadXmlStampService`, and a `void`
  finalizer closes it, logs its line and adds it to the summary.
- `MBObjectManager.CreateMergedXmlFile(...)`: a `void` finalizer marks the end of the first merge of
  the call in flight on that thread and counts its files (entries with a path; an empty path is a
  placeholder the engine skips) and XSLTs (entries after the first that name one; the engine never
  applies the first).

`merge_ms` is the time from `LoadXML`'s entry to the end of its merge (building the file list plus
the merge); `objects_ms` is the rest (`LoadXml(XmlDocument)`, the object creation). A call with no
merge seen prints `none` for both. A merge outside `LoadXML` (GameText, the native merges, TAOM's
own `ObjectManagerAdapter` and `MonsterSizeCatalogAdapter`) finds no call in flight and gets no line;
plan 042's `[XmlMerge]` lines cover the validated ones. `SubModule.OnGameInitializationFinished`
writes the `[LoadXml] summary` of every call since the previous one, on every game initialization,
before the once-per-process guard. Every `LoadXML` of a campaign or custom battle runs before it:
inside the game type's `OnInitialize`, or (SandBox's `MusicInstruments` and `MusicTracks`) in
SandBox's own `OnGameInitializationFinished`, which runs first. A module that loads after TAOM and
calls `LoadXML` in its own `OnGameInitializationFinished` lands in the next game's summary.

Both finalizers are `void`, so Harmony rethrows the original exception with its stack, and the
forwarders in `LoadTimeStampsHooks` swallow their own faults; a fault inside the stamp logs one
WARNING per process. The `CreateMergedXmlFile` finalizer runs whether or not another mod's prefix
skipped the original, so it coexists with plan 042's fast-path prefix on the same method. PatchShield
skips both targets (`PatchShieldPolicy.ExcludedTargetMethods`): they are not hot, but a shield
finalizer would swallow a missing-API exception from the merge and return a null document, which
`LoadXML`'s own catch turns into a silently missing ModuleData type. The trade-off: another mod's
patch on either method loses PatchShield's rescue.

### Campaign dispatches (always) and handlers (toggle)

`Patch100_LoadTimeStamps_Lifecycle` patches the five `CampaignEventDispatcher` lifecycle methods
(`OnNewGameCreated`, `OnGameEarlyLoaded`, `OnGameLoaded`, `OnSessionStart`, `OnAfterSessionStart`)
with a prefix and a `void` finalizer each. It applies for every player at `OnSubModuleLoad`, toggle
on or off (the apply time is in the `[PatchApply]` lines). Every dispatch gets its dispatch line
(C3), whatever the toggle: with the toggle off the prefix reads the clock and swaps nothing, the
adapter is not asked, and the finalizer writes that one line with `listeners_ms=none`, so every
player's log says what the new-game fan-out took. A missing listener binding then writes no C4
warning: it is in the always-written `[Lifecycle] ready` line, and C4 waits for a dispatch that
wants the per-handler detail.

With the toggle on, the prefix also swaps every listener of the dispatch's `CampaignEvents` events
for a timing wrapper: `OnNewGameCreated` covers `OnNewGameCreatedEvent`, its 100
`OnNewGameCreatedPartialFollowUpEvent` rounds and `OnNewGameCreatedPartialFollowUpEndEvent`;
`OnGameEarlyLoaded`, `OnGameLoaded`, `OnSessionStart` and `OnAfterSessionStart` cover
`OnGameEarlyLoadedEvent`, `OnGameLoadedEvent`, `OnSessionLaunchedEvent` and
`OnAfterSessionLaunchedEvent`. `CampaignListenerAdapter` walks `MbEvent`'s private listener list from
its head (the order `Invoke` runs it) and replaces each record's `Action` through its private setter.
The finalizer writes the lines and puts every original delegate back, whether or not the dispatch
threw.

Why it is behaviour-neutral: the wrapper calls the original exactly once, with the same arguments,
in a `try/finally` with no catch, so the order, the arguments and every exception are unchanged; the
prefixes and finalizers are `void`, so Harmony rethrows a dispatch's exception with its stack; and
PatchShield skips the five dispatcher methods (a shield swallow there would end the dispatch early
and skip the remaining listeners and receivers). The trade-off: another mod's patch on one of these
five methods loses PatchShield's rescue.

Every owner's handler is timed, TAOM's and others', each line naming its assembly: the two silent
stretches of the 2026-10-02 load may be vanilla hero creation, which only a line per handler of every
owner can attribute. The dispatch line's `ms` runs from the end of the listener swap (or of the C4
line, when per-handler timing is off; or from the prefix, when nothing is swapped) to the
finalizer, read before any line is written, so neither the swap nor the stamp's own lines are in it.
With the toggle on, `ms` minus `listeners_ms` is the dispatch's non-listener time: the event
receivers other than `CampaignEvents` (in v1.5.3 the `QuestManager`, plus any receiver a mod adds),
the wrappers' own per-call cost, and any listener added during the dispatch. Not timed: the rest of
`Campaign.OnSessionStart` after the dispatcher (`ConversationManager.Build`, each settlement's
`OnSessionStart`, the managers' `RegisterEvents`), `Campaign.OnGameLoaded`'s `AfterLoad` calls, and
a loaded save's last fan-out, `CampaignEventDispatcher.OnGameLoadFinished`
(`SandBoxGameManager.OnLoadFinished`), whose event is a non-generic `IMbEvent` the adapter does not
bind.

A handler is TAOM's when the method it runs is declared in TAOM's assembly, whatever its owner:
`MbEvent<T>` stores the owner only as the token `ClearListeners` matches (v1.5.3 decompile), so a
TAOM lambda registered under a plain `object` (`StaleCharacterAdapter` does) still counts as
TAOM's. A C1 line's `handler` reads `<owner type>.<method>` and its `asm` names the assembly of the
method. A dynamic method has no declaring type, and the adapter's own timing wrapper, which a record
can still hold after a restore fault (C6), says nothing about the handler inside it; for both, the
owner's assembly stands in.

**Caution:** with the toggle on, an exception thrown inside any campaign handler during these
dispatches carries one TAOM wrapper frame (`CampaignListenerAdapter`) in its stack. The exception
itself is the handler's own. Toggle on or off, the seven targets of both `Patch100` categories are
Harmony replacement methods owned by `com.taom.mod` for every player, so a crash thrown through an
XML load or one of these dispatches shows a TAOM-patched frame in TAOM's crash report. The patches
only observe; the exception is the engine's or the handler's. After a restore fault (C6) a handler
whose delegate could not be put back keeps its wrapper frame for the rest of the session, toggle on
or off; it still runs once per call.

**Limit:** a listener added during a dispatch (for example a PartialFollowUp listener registered by
an `OnNewGameCreated` handler; `AddNonSerializedListener` inserts at the head) is not wrapped, so it
runs untimed and is missing from `listeners=`. If `MbEvent`'s private record layout changes, the
adapter's `BindingProblem` turns per-handler timing off with one WARNING, written at the first
dispatch with the toggle on (the always-written `[Lifecycle] ready` line names the missing member
either way), and the dispatch lines remain.

### Component diagram

```
SubModule.cs ----------------------> PatchCategoryApplier (EndPhase, WriteHeldCategoryLines)
     |                                        |
     v                                        v
LoadTimeStampsHooks (static entry) --> LoadTimeStampLines (every format)
     |
     +--> LoadStampDetailGate --> IBattleLoadDiagnosticsSettingsProvider
     +--> HookStampService --> HookTimer --> IStampClock, IModLogger
     +--> LoadXmlStampService <-- MBObjectManager LoadXML / CreateMergedXmlFile patches
     +--> LifecycleTimingService <-- CampaignEventDispatcher patches
               +--> ICampaignListenerAdapter (CampaignListenerAdapter: MbEvent listener records)
```

## Configuration

| Setting | Default | Effect |
|---|---|---|
| `EnableLoadTimeStamps` ("Enable Load-Time Stamps", Battle Load Diagnostics page, group "Load-Time Stamps") | `false` | Adds the per-category `[PatchApply]` lines, the `[LoadPhase]` hook steps and the `[Lifecycle]` handler and event lines (the dispatch line is always written). The hook steps and the `[Lifecycle]` handler timing read it live at each game start, game initialization and campaign dispatch, with no restart. The per-category lines read it once per process, at the first game initialization after launch (and the first mission for the `Mission` phase): turning it on later needs a restart for them. Local-only for co-op (`CoopSettingsRelevance` instrumentation). |

Always written, whatever the toggle: the `[LoadStamps] ready`, `[LoadXml] ready` and `[Lifecycle] ready` headers, the
`[LoadXml]` line per type and its summary per game, the four `[PatchApply]` phase totals, and the
`[Lifecycle] dispatch=` line of each campaign dispatch.

## Log lines

All INFO (flushed synchronously by `FileLogger`) except the WARNINGs (H3, X3, C4, C5, C6);
none is per frame. Every example below is taken from the tests (`LoadTimeStampLinesTests`,
`PatchCategoryApplierTests`, `HookStampServiceTests`, `LoadXmlStampServiceTests`,
`LifecycleTimingServiceTests`) and shows example values; the handler names are shapes, not
measurements. Milliseconds always print with two decimals and a dot.

| Id | Line | Level | When | Fields | Example |
|---|---|---|---|---|---|
| H1 | `[LoadStamps] ready` | INFO | once per process, when the module's statics are initialized (inside `IoC.Configure`) | none: says what is always on and what follows the toggle | `[LoadStamps] ready: [PatchApply] phase totals are always written; per-category [PatchApply] and per-hook [LoadPhase] lines follow "Enable Load-Time Stamps" (Battle Load Diagnostics page, default off): the per-hook lines read it at each game start and game initialization, the per-category lines once, at the first game initialization after launch` |
| H2 | `[LoadStamps] detail on` / `detail off` | INFO | the first read of the toggle, whenever its value changes, and the first readable read after an H3 | on or off | `[LoadStamps] detail off: only the always-on load-time totals are written; turn on "Enable Load-Time Stamps" for the per-category, per-hook and per-handler lines` |
| H3 | `[LoadStamps] detail off: ... could not be read` | WARNING | when a read of the toggle throws after a readable one (or on the first read); repeated failures log once | the exception's type and message; the consequence (only the always-on totals are written) | `[LoadStamps] detail off: "Enable Load-Time Stamps" could not be read (InvalidOperationException: mcm); only the always-on load-time totals are written` |
| P1 | `[PatchApply] phase=... category=...` | INFO | toggle on: at the first game initialization for the `OnSubModuleLoad`, `MainMenu` and `GameInit` categories, at the first mission for the `Mission` ones | `phase`, `category`, `ms` (apply time), `result` (`ok` or `failed`) | `[PatchApply] phase=GameInit category=Patch11_Diplomacy ms=4.21 result=ok` |
| P2 | `[PatchApply] phase=... scope=total` | INFO | always, at the end of each of the four phases | `phase`, `categories`, `failed`, `ms` (sum), `max_ms`, `max_category` (first on a tie, `none` without categories) | `[PatchApply] phase=GameInit scope=total categories=74 failed=1 ms=312.40 max_ms=41.07 max_category=Patch2_RefreshTableau` |
| L1 | `[LoadPhase] hook=... step=...` | INFO | toggle on: each step of a hook | `hook`, `step`, `ms` since the previous step's line was written (the hook's start, for the first) | `[LoadPhase] hook=OnGameStart step=hand_wired ms=120.50` |
| L2 | `[LoadPhase] hook=... scope=total` | INFO | toggle on: the end of a hook | `hook`, `game` (the game type's class name, `none` for `GameInitOnce`), `ms` since the hook started (wall clock, the hook's own lines' writes included), `steps` | `[LoadPhase] hook=OnGameStart game=Campaign scope=total ms=133.25 steps=3` |
| X0 | `[LoadXml] ready` | INFO | once per process, beside H1 | none | `[LoadXml] ready: one line per MBObjectManager.LoadXML call and a summary at every game initialization, always written` |
| X1 | `[LoadXml] id=...` | INFO | always: the end of each `LoadXML` call | `id` (the XML type, `null` if none), `files`, `ms` (the whole call), `xslt`, `merge_ms` and `objects_ms` (`none` when no merge was seen), `result` (`ok` when `LoadXML` returned, or the exception's type name; `LoadXML` swallows each object-creation fault itself, so `ok` does not mean every object loaded) | `[LoadXml] id=NPCCharacters files=56 ms=13302.00 xslt=2 merge_ms=13001.50 objects_ms=300.50 result=ok` |
| X2 | `[LoadXml] summary` | INFO | always: every game initialization | `game` (the last non-empty game type of the calls, `none` without one), `calls`, `files`, `xslt`, `ms`, `merge_ms` and `objects_ms` (summed over the calls whose merge was seen), `max_ms`, `max_id` (the slowest call, first on a tie), `failed` (calls that threw) | `[LoadXml] summary game=Campaign calls=26 files=329 xslt=6 ms=28000.00 merge_ms=26500.00 objects_ms=1500.00 max_ms=13302.00 max_id=NPCCharacters failed=0` |
| X3 | `[LoadXml] stamp fault` | WARNING | once per process, the first time the stamp itself throws | the exception's type and message; the consequence (some `[LoadXml]` lines may be missing) | `[LoadXml] stamp fault, some [LoadXml] lines may be missing this session: InvalidOperationException: x` |
| C0 | `[Lifecycle] ready` | INFO | once per process, beside H1 | whether the listener binding resolved, that the dispatch line is always written, the threshold, or the missing member and the fallback | `[Lifecycle] ready: listener binding ok; a dispatch line is always written for each new-game, game-loaded and session-start dispatch; with "Enable Load-Time Stamps" on, every handler of them is timed too, with a line for each at or over 10.00 ms`; binding missing: `[Lifecycle] ready: listener binding missing (MbEvent<CampaignGameStarter>._nonSerializedListenerList not found); only a dispatch line for each new-game, game-loaded and session-start dispatch is written, whatever "Enable Load-Time Stamps" says` |
| C1 | `[Lifecycle] event=... handler=...` | INFO | toggle on: each dispatch's end, one per handler whose summed time is 10.00 ms or more, in invoke order | `event`, `handler` (owner type and method), `asm` (the assembly of the method that runs, whatever the owner), `calls`, `ms` (summed), `max_ms` (slowest call), `max_index` (the PartialFollowUp round of the slowest call, `none` for the other events) | `[Lifecycle] event=OnNewGameCreatedPartialFollowUp handler=CultureMarketplaceBehavior.OnNewGameCreatedPartialFollowUp asm=TAOM calls=100 ms=3021.50 max_ms=2990.25 max_index=1` |
| C2 | `[Lifecycle] event=... scope=total` | INFO | toggle on: each dispatch's end, one per event, after its C1 lines | `event`, `listeners`, `taom_listeners`, `ms` (all listeners), `taom_ms`, `other_ms`, `over_threshold` (handlers with a C1 line), `max_ms`, `max_handler` (slowest by summed time, first on a tie, `none` without listeners) | `[Lifecycle] event=OnNewGameCreated scope=total listeners=84 taom_listeners=20 ms=3400.00 taom_ms=120.00 other_ms=3280.00 over_threshold=3 max_ms=2100.00 max_handler=HeroSpawnCampaignBehavior.OnNewGameCreated` |
| C3 | `[Lifecycle] dispatch=...` | INFO | always: each dispatch's end, and the only `[Lifecycle]` line of a dispatch with the toggle off; with the toggle on, last, after its C1 and C2 lines | `dispatch`, `ms` (the dispatcher call, from the end of the listener swap, or from the prefix when nothing is swapped, to the finalizer, without the stamp's own lines), `listeners_ms` (summed over its events, `none` when per-handler timing is off: the toggle is off, the binding is missing or a swap failed), `result` (`ok` or the exception's type name) | `[Lifecycle] dispatch=OnNewGameCreated ms=6650.00 listeners_ms=6600.00 result=ok` |
| C4 | `[Lifecycle] per-handler timing off` | WARNING | once per process, at a dispatch with the toggle on: a missing binding or a failed swap, which turn per-handler timing off for the session (never written with the toggle off) | the problem; the consequence (dispatch totals are still written) | `[Lifecycle] per-handler timing off for this session: boom; dispatch totals are still written` |
| C5 | `[Lifecycle] stamp fault` | WARNING | once per process, the first time the stamp itself throws (its clock or its logging); nothing is turned off, and its own latch never hides a later C4 or C6 | the exception's type and message; the consequence (some `[Lifecycle]` lines may be missing) | `[Lifecycle] stamp fault, some [Lifecycle] lines may be missing this session: InvalidOperationException: x` |
| C6 | `[Lifecycle] restore fault` | WARNING | once per process, the first time an original listener cannot be put back after a dispatch (nothing retries it); its own latch | the exception's type and message; the consequence (that handler keeps its timing wrapper for the session and still runs once per call; a later toggled dispatch wraps it again, names its C1 line after the wrapper and takes its `asm` from the owner) | `[Lifecycle] restore fault, a campaign handler may keep its timing wrapper this session (it still runs once per call): InvalidOperationException: x` |

The three total lines (P2, L2 and C2) mark themselves `scope=total`, a key=value pair, never a bare
word: plan 029's log parser joins a bare word after a value to that value, so
`phase=GameInit total` misparses. `LoadTimeStampLinesTests.EveryKeyValueLine_HasNoBareWordAfterAValue`
pins the rule for every line with pairs. The `summary` of X2 stands straight after the tag, not after
a value, and stays.

A category that throws still gets its `[PatchApply] <category> FAILED (...)` ERROR line at once, as
before; its time is in the phase total and its P1 line says `result=failed`.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/LoadTimeStamps/LoadTimeStampsModule.cs` | Registers the services, hands them to the static entry, logs H1 |
| `Main/Features/LoadTimeStamps/LoadTimeStampsHooks.cs` | The never-throwing static entry `SubModule.cs` calls |
| `Main/Features/LoadTimeStamps/LoadTimeStampLines.cs` | Every line format |
| `Main/Features/LoadTimeStamps/LoadStampDetailGate.cs` | The toggle read and the H2 and H3 lines |
| `Main/Features/LoadTimeStamps/HookStampService.cs`, `HookTimer.cs` | The hook step timers |
| `Main/PatchCategoryApplier.cs` | Times each category; `EndPhase`, `WriteHeldCategoryLines` |
| `Main/Features/LoadTimeStamps/LoadXmlStampService.cs`, `LoadXmlCall.cs` | The `[LoadXml]` calls, their thread-local stack and the summary |
| `Main/Features/LoadTimeStamps/Hooks/MBObjectManager_LoadXML_StampPatch.cs`, `MBObjectManager_CreateMergedXmlFile_StampPatch.cs` | `Patch100_LoadTimeStamps_LoadXml` |
| `Main/Core/Diagnostics/IStampClock.cs`, `StopwatchStampClock.cs` | The clock, faked in tests |
| `Main/Features/LoadTimeStamps/LifecycleTimingService.cs`, `LifecycleDispatchScope.cs`, `Domain/` | The `[Lifecycle]` timing per dispatch |
| `Main/Features/LoadTimeStamps/Hooks/CampaignEventDispatcher_Lifecycle_StampPatches.cs` | `Patch100_LoadTimeStamps_Lifecycle` |
| `Main/Adapters/ICampaignListenerAdapter.cs`, `CampaignListenerAdapter.cs` | The listener swap on `MbEvent`'s private records |
| `Dependencies/Foundation/PatchShieldPolicy.cs` | Excludes the stamps' targets from PatchShield |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | `EnableLoadTimeStamps` |

## Dependencies

- `IBattleLoadDiagnosticsSettingsProvider` (BattleLoadDiagnostics): the toggle.
- `IModLogger` (Core): every line.

## Tests

- `TAOM.Tests/Features/LoadTimeStamps/LoadTimeStampLinesTests.cs`: every format, literally, the
  invariant culture, and that no line with key=value pairs has a bare word after a value
  (`scope=total` on the three total lines).
- `LoadStampDetailGateTests.cs`, `HookStampServiceTests.cs`, `LoadTimeStampsHooksTests.cs`: the
  toggle, the hook timers (a step leaves out the write of the line before it; if the clock read
  after that write fails, the next step starts at the pre-write tick, so it carries that one write
  but none of an earlier step), the never-throwing entry.
- `LoadTimeStampsWiringTests.cs`: the `SubModule.cs` insertions and their order around the
  once-per-process guard. `LoadTimeStampsModuleTests.cs`: every registration resolves, as a
  singleton.
- `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`: phase totals, held lines, and the
  overhead bound (50 microseconds per category).
- `LoadXmlStampServiceTests.cs`: the merge and object split, the counts, nested calls, the summary.
- `LoadXmlStampPatchShapeTests.cs` (`RequiresGame`): real Harmony around a stand-in with `LoadXML`'s
  parameter names; the target runs once and an exception keeps its type, message and throw site.
  `MergeStampPatchShapeTests.cs` (`RequiresGame`): the `CreateMergedXmlFile` finalizer still marks
  the merge when another owner's prefix skips the original, and the replacement's result stands.
- `LifecycleTimingServiceTests.cs`: the toggle off (exactly the dispatch line, no swap, no adapter
  call, no C4 for a missing binding) and read per dispatch, the threshold, the TAOM split, the
  PartialFollowUp aggregate, the line order, the dispatch time without the swap and the stamp's lines
  on every path, the restore when logging fails, the binding and wrap fallbacks, a clock fault after
  the swap (C5, the swap kept), and the fault and restore-fault lines kept apart from the "timing
  off" line.
- `ListenerTimingTests.cs`: one handler's call sums and its slowest call.
- `CampaignListenerAdapterTests.cs` (`RequiresGame`): the swap on real `MbEvent` instances; order,
  exceptions and the restored delegates; the assembly a handler is attributed to (the callback's,
  not its owner's, except for a record still holding the adapter's own wrapper, after a failed
  restore or a second wrap); a swap that fails midway, and one whose rollback fails too (the adapter's
  `BeforeWrite` seam makes a write fail); a record another party replaced.
- `LoadTimeStampsBindingTests.cs` (`BindingVerification`): the targets' parameter names and
  overloads, the event accessors, both categories, and all seven targets on PatchShield's exclusion
  list. `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: the ten `MbEvent` listener-record rows.

## How to read a load

A new campaign writes, in order: the `[LoadXml]` lines and their summary, the
`OnGameInitializationFinished` stamps, then the `OnNewGameCreated`, `OnSessionStart` and
`OnAfterSessionStart` dispatches (each `dispatch=` line is always there; with the toggle on, its
handler and event lines come first). A save load writes `OnGameEarlyLoaded` and `OnGameLoaded`
instead of `OnNewGameCreated`. A custom battle has no `[Lifecycle]` lines. With the toggle on, the
`[LoadPhase] hook=OnGameStart` lines come before the `[LoadXml]` lines: `Campaign.OnInitialize` runs
every module's `OnGameStart` (v1.5.3 `Campaign.cs:1410`) before its first `LoadXML`
(`Game.LoadBasicFiles`, reached at :1417 or :1426).

Search `taom_debug.log` for `[LoadXml]`, `[PatchApply]`, `[LoadPhase]` and `[Lifecycle]`. The
`[LoadXml]` lines say which XML types the load spent its time on, and whether in the merge or in
building the objects; the summary gives the game's total. The four `phase=... scope=total` lines say
how long TAOM's patch application took in each phase and which group was slowest. The
`[Lifecycle] dispatch=` lines say what each campaign dispatch took, the new-game fan-out being
`OnNewGameCreated`. With the toggle on, the per-category lines name every group, the `[LoadPhase]`
lines split TAOM's own hooks, and the `[Lifecycle]` lines name every campaign handler of 10 ms or
more, with each event's total split into TAOM's handlers and everyone else's.

## Performance

What every player pays, toggle off:

- **Once per process:** seven `Harmony.Patch` targets at `OnSubModuleLoad` (two for `[LoadXml]`,
  five for `[Lifecycle]`, which with the toggle off only time the dispatch), inside the
  `[PatchApply] phase=OnSubModuleLoad scope=total` line; two clock reads per patch category (about
  100); the three ready headers and the four phase totals.
- **Per `LoadXML` call** (about 25 to 40 per load): a thread-local push and pop, a few clock reads and
  one synchronous INFO line; one summary line per game initialization.
- **Per campaign dispatch** (three per new campaign, four per save load): one read of the toggle, a
  small scope object, three clock reads and one synchronous INFO line (C3). **Per hook:** one read of
  the toggle.

Nothing runs per frame. With the toggle on, each dispatch adds the listener swap (a reflection read
and write per listener) and one INFO line per event and per handler of 10 ms or more. Applying the
`[Lifecycle]` category only when the toggle is on (option B of the plan 040 review; plan 040's
Maintenance note "Lifecycle only when on") no longer fits: the dispatch line needs its patches for
every player.

## Changelog

- 2026-10-03: convergence follow-ups: a record that still holds the adapter's own timing wrapper
  (after a restore fault, C6, or a second wrap before the restore) is attributed to its owner's
  assembly instead of TAOM's, because the wrapper is not evidence of whose handler runs inside it;
  the hook step's failed-clock fallback is pinned by a test.
- 2026-10-03: review and Codex follow-ups: a handler's assembly, and so the TAOM split, comes from
  the method it runs and not from its owner token (a TAOM callback under a plain `object` owner
  counted as someone else's); a hook step no longer includes the write of the previous step's line;
  the partial-swap rollback is tested on real `MbEvent` records; and this doc's clock-read count,
  Limit paragraph, option references and issue number are corrected.
- 2026-10-03: the `[Lifecycle]` dispatch line (C3) is written with the toggle off too: one line per
  dispatch, no listener swap, no C4 warning, so every player's log says what the new-game fan-out
  took (option A of the plan 040 review; the C0 header says so). The three total lines (P2, L2, C2)
  say `scope=total` instead of a bare `total`, which plan 029's log parser joins to the value before
  it.
- 2026-10-03: convergence fixes: the dispatch `ms` leaves out the C4 line and a failed swap on every
  path, and a clock fault after the swap is a stamp fault (C5) that keeps the swap; a restore fault
  gets its own WARNING (C6) naming what it leaves behind; a readable read after an unreadable one
  logs H2 again.
- 2026-10-03: review follow-ups: the dispatch `ms` leaves out the swap and the stamp's own lines;
  a stamp fault gets its own WARNING (C5) instead of the "timing off" one; an unreadable toggle says
  why (H3); a failed `[LoadXml]` line no longer drops its call from the summary.
- 2026-10-03: `[Lifecycle]` handler, event and dispatch lines (toggle), `Patch100_LoadTimeStamps_Lifecycle`.
- 2026-10-03: `[LoadXml]` per-type lines and summary (always on), `Patch100_LoadTimeStamps_LoadXml`.
- 2026-10-03: patch-group phase totals (always on), per-category lines and hook steps (toggle).
  Refs: plan 040.

## GitHub Issue

- **Issue:** #722. Refs: plan 040.
- **Status:** Open
