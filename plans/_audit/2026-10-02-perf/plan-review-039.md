# Plan review 039, round 1 (cold)

Plan: `plans/039-campaign-map-frame-profiler.md`. Code read at `0912e1b7` (planned-at), at `e64529b3`
(plan 028's tip the plan was written against) and at `765d3759` (plan 028's branch tip today, after its
convergence fixes `7ca09cc2` and docs `9d52bb86`, `765d3759`). Engine facts checked against the cached
v1.5.3 decompiles from `taom-src`. No earlier `plan-review-039*.md` existed, so there are no prior
blocking items to re-check. Nothing was built or run with dotnet (read-only role).

Verdict: a careful, mostly executable plan whose excerpts, oracles and literals hold up. Two blocking
items: the RefAsm binding gate command cannot load any game assemblies, and the PatchShield trade-off
the plan prescribes is narrower than what the five exclusions actually give up.

## Blocking

### B1. The "RefAsm binding gate" command fails every binding test as Inconclusive (plan lines 613, 704-711, 1447-1451, 1495-1496, 1531-1533)

The command is `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build
-p:TaomGameRefs=RefAsm ... --settings TAOM.Tests/binding-gate.runsettings ...`, after a RefAsm build
that also ran with both variables unset. Hosted CI does not do that: it points the gate at the
reference-assembly game folder (`.github/workflows/csharp.yml:76-83`: `$game = ...
'TAOM.Tests/bin/Debug/net472/refasm-game'`, `$env:BANNERLORD_GAME_DIR = $game`).

Following the code (not executed, so the outcome is derived, not observed):
- `Directory.Build.props:37-38`: with both variables unset, `GameFolder` is empty.
- `TAOM.Tests/TAOM.Tests.csproj:44-47`: the test DLL's `TaomGameFolder` metadata is that empty value.
- `TAOM.Tests/Migration/GameAssemblies.cs:133-141` and `:50-54`: no override, no game dir, empty built
  folder, so `EnsureLoaded()` returns false and every binding test calls `Assert.Inconclusive`.
- `TAOM.Tests/binding-gate.runsettings`: `MapInconclusiveToFailed` is true.

So in Step 1 the whole gate reports as failed. Step 1's **Verify** ("The RefAsm runs show at most the
base failures named under Status", line 711) then fails, because Status names only two unit-step
failures (lines 39-41). A weak executor either STOPs at Step 1 or records hundreds of failures as "the
base set". In both cases the STOP at lines 1531-1533 (a backing-field row that passes locally but fails
under reference assemblies) can never fire, because the gate never loads the reference assemblies.

Fix: run the gate as CI does, for example
`BANNERLORD_GAME_DIR="$PWD/TAOM.Tests/bin/Debug/net472/refasm-game" env -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm ...`
(Debug configuration, right after the RefAsm build, per `.ai/verification.md:23-37`). Also name the
gate's expected base failure set in Status, or say "record it in Step 1" and drop "named under Status"
from the Step 1 Verify.

### B2. The PatchShield trade-off is under-derived; the prescribed comment, commit body and registry sentences repeat the claim plan 028's convergence corrected (plan lines 345-364, 584-593, 680-684, 1342-1350, 1435-1438)

The plan says that after the five exclusions "a MissingMethod, MissingField or TypeLoad exception from
ANY owner's patch on those five methods ... is no longer swallowed and that patch is no longer
stripped" (lines 589-592). The prescribed `PatchShieldPolicy` comment (lines 1344-1349) and the commit
body (lines 682-684) say the same. What the code shows:

1. **The swallow covers the method's whole execution, not only its patches.** PatchShield's finalizers
   call `ShouldSwallow(__originalMethod, __exception)` (`Dependencies/Foundation/PatchShield.cs:263, 281`).
   `ShouldSwallow` (`:288-322`) swallows any of the three exception types, whatever threw it. A Harmony
   finalizer sees exceptions from the original body and everything it calls. `MapState.OnTick` holds the
   whole campaign tick: `Campaign.RealTick`, `Campaign.Tick`, which runs `CampaignEventDispatcher.Tick`,
   `_campaignPeriodicEventManager.OnTick` and the hourly and daily events, so every mod's campaign
   behaviour listener, the menu tick and `SaveHandler.CampaignTick` (decompile
   `TaleWorlds.CampaignSystem.GameState.MapState.cs` OnTick and OnMapModeTick, `Campaign.cs:974-1010`).
   Excluding `MapState.OnTick` for every player therefore removes the missing-API rescue from the
   campaign tick as a whole. PatchShield's own comment shows real player crashes cross this shield:
   `PatchShield.cs:265-268` cites bundle `2d446100`, "a childbirth failure reported as five frames ending
   at MapState.OnTick_Patch2". `docs/features/crash-report.md:174` describes the crash identity built on
   it. The plan's text describes a much smaller loss.
2. **When the shield is attached today.** Patch36 and Patch43 are applied inside TAOM's
   `OnGameInitializationFinished` once-per-process block (`Main/SubModule.cs:1817` and `:1913` at
   `e64529b3`). PatchShield pass 2 runs in the Dependencies submodule's `OnGameInitializationFinished`
   (`Dependencies/SubModule.cs:293`). So `MapState.OnTick` and `MapScreen.OnFrameTick` carry the shield
   only from a process's second game start; `Campaign.RealTick` (Patch89, `OnSubModuleLoad`,
   `SubModule.cs:434`) from the first. Line 361 says only "it does shield ... today". **UNVERIFIED:** the
   module order that puts Dependencies' hook first. It was inferred, not read.
3. **The lesson the plan predates.** Plan 028's reviewed tip added to `docs/reviews/lessons/harmony-il.md`
   (lines 721-743 at `765d3759`, after `e64529b3`): "Before claiming a caller's shield still covers a
   skipped method, check when that shield is attached ... When a change removes or narrows a catch,
   follow the exception to its next catch and read each unwound caller's code after the call: list what
   one throw skips". The plan was written against `e64529b3` and does neither for its five methods. For
   example: a missing-API exception escaping `MapState.OnTick` unwinds `GameStateManager.OnTick` and the
   game tick up to `Module.OnApplicationTick`, which skips every module's `OnApplicationTick` (TAOM's
   included), `JobManager.OnTick` and the rest of `MapState.OnTick`. One escaping
   `MapScreen.OnFrameTick` unwinds `ScreenManager.Tick` into `EngineScreenManager.Tick`, a native
   callback. Whether Patch37 or a Native2Managed finalizer then catches it is **UNVERIFIED**; the plan
   must say.

What the reviser must do: re-derive the trade-off per that lesson (when each shield is attached; where
each escaping exception goes; what it skips), then rewrite lines 589-593, the PatchShield comment, the
commit-body sentence and the three registry sentences to match. This is also a decision the maintainer
may want to make rather than an executor. The house rule (`harmony-il.md` 2026-09-26 and 2026-09-28)
requires excluding per-frame targets, so a narrower option needs his word. One such option: exclude
only `Campaign.Tick` and `CampaignEvents.Tick` (patched by Patch101 alone), and accept the finalizer
inside the `mapStateMs`, `realTickMs` and `mapScreenMs` brackets. Plan 034's maintenance note (lines
1580-1582) points the same way. Either way, the Done criteria should pin the corrected sentence.

## Non-blocking

1. **View entries are not always inside `mapScreenMs`** (lines 446, 1416-1417). In v1.5.3,
   `MapView.OnFrameTick` is called from `IMapStateHandler.Tick` (decompile `MapScreen.cs:1286-1313`).
   That runs as `Handler?.Tick(dt)` inside `MapState.OnMapModeTick`, so inside `mapStateMs`.
   `OnMenuModeTick` is reached from `MapState.OnMenuModeTick` (also inside `mapStateMs`), and
   `OnIdleTick` from `IMapStateHandler.OnIdleTick`, which runs only while the state is idle, so never
   in a closed frame. Only `OnMapScreenUpdate` (`MapScreen.cs:1366-1369`) is inside `mapScreenMs`. That
   is the only override TAOM has today, so today's numbers are right, but the feature doc's nesting
   sentence and the target table should say "per override: OnMapScreenUpdate inside mapScreenMs,
   OnFrameTick and OnMenuModeTick inside mapStateMs".
2. **`TickMapTime` excerpt omits the waiting rule** (lines 280-281). `StoppablePlay` and
   `StoppableFastForward` give `_dt = 0` while `IsMainPartyWaiting` (decompile `Campaign.cs:860-896`).
   So a frame classed `Play` or `FF` can advance no campaign time. This matters for "Reading the lines"
   in the feature doc (compare runs by speed class).
3. **150-line limit with no file to split into.** `MapFrameProfilerInstaller.cs` carries `TickEventSwaps`,
   `CoreTargets`, `CoreTargetNames`, `MapViewTargets`, two `OnGameInitialized` overloads, `LaterGameInit`,
   `IsPatchedByThisProfiler` and `ResetForTests`. An estimate puts it near 150 lines; for comparison,
   028's `MissionTickProfilerHooks.cs` is 149. Scope lists exact files and the size check is a Done
   criterion, so an executor over the limit has no sanctioned move. Name an allowed split, for example
   `Hooks/MapProfilerTargets.cs`.
4. **The PatchShield walk never runs in hosted CI.** Every `MapFrameProfilerBindingTests` method gets
   `RequiresGameIL` (line 1180). 028's `ProfiledTargets_AreOnPatchShieldsExclusionList` carries only
   `BindingVerification` (`MissionTickProfilerBindingTests.cs:74-91` at `765d3759`), so it runs in the
   RefAsm gate. `EveryCoreTarget_ResolvesInInstalledEngine` and `ProfiledEngineTargets_...` read no IL
   and could drop `RequiresGameIL`.
5. **`HarmonyPatchBindingTests` in the RefAsm gate** will call the views class's `TargetMethods()` and so
   `MapViewTargets()` (`GetTypes` on TAOM's assembly against reference assemblies). An empty result
   fails with "TargetMethods() returned an empty sequence" (`HarmonyPatchBindingTests.cs:115`).
   **UNVERIFIED** whether `MapView` resolves there. The general STOP at line 1543 covers it, but the plan
   does not anticipate it.
6. **No escape for the RefAsm restore.** The RefAsm build downloads BUTR packages. Plan 041 gives an
   explicit "RefAsm not run (environment)" escape (its line 608); 039 has none.
7. **A stale-claim grep that can never hit.** `"the one setting a Harmony category"` (lines 796, 1505)
   matches nothing even before the edit: the phrase is split across lines 27-28 of
   `SettingRequireRestartPostureTests.cs`. Grep a single-line fragment instead, for example
   `"setting a Harmony category is gated on at apply time"`.
8. **Drift check misses some read-only files.** It omits files whose excerpts the plan relies on:
   `Dependencies/Foundation/PatchShield.cs` (line 358-364 claims), `docs/reviews/lessons/harmony-il.md`,
   and the three map view files that `MapViewTargets_AreTheTaomMapViewPerFrameOverrides` pins.
9. **The wiring is not entirely failure-contained.** Insertion 1 (lines 1387-1392) resolves four IoC
   services outside the installer's `try`/`catch`, so "contains its own failures" covers the call, not
   the argument evaluation. All four are registered singletons, so the risk is low.
10. **The plan is not anchored on 028's reviewed tip.** It is written against `e64529b3`; 028's branch is
    now at `765d3759`. The drift check will show `TickProfileLines.cs` (added members only; the excerpts
    still hold) and `harmony-patch-registry.md` (Patch97 section only). The orchestrator step at line
    1552 covers it, and B2 is the substantive consequence.

## Excerpt mismatches

- Lines 147-157: "the end of `OnGameEnd` (lines 838-846)". At `e64529b3` the FactionUI `try` is lines
  842-846, the closing brace 847 and `OnGameStart` 849. The text matches; the range is off by a few
  lines.
- Lines 280-281: `TickMapTime` summary omits the `IsMainPartyWaiting` condition on `StoppablePlay` and
  `StoppableFastForward` (decompile `Campaign.cs:860-896`).
- Line 446 / 1416-1417: "view entries inside `mapScreenMs`" holds for `OnMapScreenUpdate` only (see
  non-blocking 1).
- Line 361: "it does shield `MapState.OnTick`, `Campaign.RealTick` and `MapScreen.OnFrameTick` today".
  For `MapState.OnTick` and `MapScreen.OnFrameTick` this holds only from a process's second game start
  (see B2.2).

Checked and matching: 028's primitives (`BehaviorTickTable` signatures and summary,
`AllocationCounter`, `CallSwap`, `Rewrite`, `Fits`, `TickProfileLines` helpers and the
`[TickProfiler]` site-count warning text); settings lines 63-71 and provider 70-74; `SubModule.cs`
1553-1565, 1929, 2000, 2192 (2177 at `0912e1b7`), 434; `TryPatchCategory` is
`private bool TryPatchCategory(string)` (line 907); IoC.cs 145 and 178; the eight listener lines, the
Messenger self-removal at 492, the three map views at 54/55/58; the Patch36, Patch43, Patch89 and
SceneReady excerpts; `MapLoadHeartbeatService.cs:83-87`; the time-control seams; PatchShieldPolicy at
`e64529b3`; `SettingsFingerprintTests` 205-211 (12, 340 = 320 + 12 + 7 + 1, 217, 123); the
`coop-interop.md`, `bannerlord-together-compat.md`, `mcm.md` and posture-summary anchor sentences;
the reflection-sites and feature-map anchors; the registry section anchors; the engine decompiles
for `MbEvent<T>` (whole file), `MapState.OnTick`, `CampaignEvents` 291/855/2083-2086,
`CampaignEventDispatcher.Tick` 1044-1051, `GameStateManager.OnTick` 196-209, `Module.OnApplicationTick`
534 and 536, `MapScreen`, `MapView`, `SandboxView`, `LoadingWindow`, `ScreenManager.TopScreen`. The
IL byte counts and opcode lists were not re-derived (UNVERIFIED); the plan's binding tests and STOP
conditions gate them.

Oracle arithmetic re-done and correct: `MapFrameProfilerTests` cases 2, 4, 13, 16 and 21; both pinned
data literals (KB divisions, `0.00` formatting, `t=+65s` from 65.2, and the identity
`wallMs = mapStateMs + mapScreenMs + appTickMs + otherMs` in each); and the 83-result count
(8+21+10+3+2+7+12+10+5 + 1 + 4).

## Checklist

1. Executable from the plan and repo: yes, except B1 (the gate command) and B2 (the trade-off text the
   executor would copy).
2. Every step ends in a command with an exact expected result: yes; Step 1's RefAsm expectation is
   unreachable (B1).
3. Excerpts: see above.
4. TDD order for every C# step: yes. Issue line: yes. Binding ADRs: 002, 003-005, 007, 008, each with
   one line. Protected files: none (Step 0 none). Single-owner `SubModule.cs`: two exact insertions,
   pinned by tests. `IoC.cs`: out of scope with a STOP. STOP conditions are specific. Done criteria
   are machine-checkable (the dash grep works under Git Bash; tested). Planned-at and drift paths are
   consistent with Scope (non-blocking 8). Every dotnet command carries `-p:ModuleId=`. No worktree
   path or branch name. No CHANGELOG step.
5. No em or en dash in the plan (scanned with Python); no secret values.
