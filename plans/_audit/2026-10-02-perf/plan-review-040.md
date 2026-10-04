# Plan review 040, round 1 (cold)

Plan: `plans/040-load-time-stamps.md` (1583 lines). Reviewed against the worktree at `0912e1b7`
(HEAD `1a702d25`; `git diff --stat 0912e1b7..HEAD -- Main TAOM.Tests Dependencies docs tools` is
empty, so every in-scope file is as planned). Engine excerpts re-read from
`pwsh tools/taom-src.ps1 path <full type name>` (installed v1.5.3). No earlier review file for 040
existed, so there is nothing to re-check from a previous round.

Verdict: one blocking item. It does not stop a weak executor from finishing; it makes the finished
branch ship an undisclosed change on every player's load.

## Blocking

### B1. PatchShield wraps all seven new always-on targets: it changes exception behaviour and adds load time (plan :568-571, :584-596, :667, :687-689, :799, :1506-1528)

The plan applies both categories at `ApplyPhase.ProcessLoad` whatever the toggle says (:1160-1161,
:1420-1422). So `MBObjectManager.LoadXML`, `MBObjectManager.CreateMergedXmlFile` and the five
`CampaignEventDispatcher` lifecycle methods are Harmony-patched in every player's process.
`Dependencies/SubModule.cs:293` runs `PatchShield.Install()` (pass 2) at `OnGameInitializationFinished`.
That pass attaches PatchShield's finalizer to every patched method not excluded by
`PatchShieldPolicy.IsExcludedTargetMethod` (`Dependencies/Foundation/PatchShield.cs:184`). For a
campaign that happens before the first `OnNewGameCreated`/`OnGameLoaded` dispatch (engine order:
`Campaign.cs:1471` before `DoLoadingForGameType` :1685-1711). What follows:

1. **Exceptions change.** `ShieldFinalizerVoid`/`ShieldFinalizerWithResult`
   (`PatchShield.cs:246-271`) swallow `MissingMethodException`, `MissingFieldException` and
   `TypeLoadException` (`ShouldSwallow`, :273-306), the typical failure of another mod built against
   an older Bannerlord. Today an exception like that, thrown by any campaign behaviour's
   `OnNewGameCreated`, `OnSessionLaunched` or `OnGameLoaded` handler, propagates, because the dispatcher
   is unpatched. With this plan PatchShield swallows it at the dispatcher. The rest of that event's
   listener list is skipped, and so are the remaining PartialFollowUp rounds and the `IssueManager`
   and `QuestManager` receivers. The load then carries on half-initialized, and only `diag.log` says
   so. `TryUnpatchOffendingPatches` also strips `com.taom.mod`'s own patches on that method (FOR-MIKE
   item 15). `CreateMergedXmlFile` returns a value, so on a swallow it returns null. `LoadXML`'s
   `try { LoadXml(null) } catch {}` (MBObjectManager.cs:786-797) then eats the resulting NRE, and a
   ModuleData type silently fails to load. This contradicts the plan's own invariants: "Order,
   arguments and exceptions are unchanged" (:588) and "nothing about loading itself changes" (:688-689).
2. **Load time.** `PatchShieldPolicy.cs:163-169` records 5 to 10 ms and about 186 ms per attach on
   the maintainer's desktop. Seven new attaches therefore cost about 35 ms to 1.3 s of loading screen
   per process, and every player pays it, toggle off. Design decision 2 (:568-571, "far under a
   millisecond in total") leaves this out, and the hint text (:799, "nothing during play") does not
   cover it. For a plan whose purpose is load time, this matters.
3. The out-of-scope line (:667) excludes `Dependencies/` on hotness alone. The sibling plans of the
   same run (028 :9, 039 :12, 041 :17) all bring `Dependencies/Foundation/PatchShieldPolicy.cs` into
   scope for their own targets. Plan 042 makes the same omission for `CreateMergedXmlFile`.

**Fix (the writer's or the maintainer's choice; either way, state it in Status/Risk and the commit
body):**
- (a) Add the seven `DeclaringType.Method` keys to `PatchShieldPolicy.ExcludedTargetMethods`, plus a
  test that walks the real patch targets through `IsExcludedTargetMethod`. The precedent is
  `CreatureBanditsWiringTests.HotCreatureTargets_AreOnPatchShieldsExclusionList` (comment at
  `PatchShieldPolicy.cs:139-141`). Add `Dependencies/Foundation/PatchShieldPolicy.cs` and that test
  to Scope and the drift check. Name the trade-off: another mod's patch on these methods loses
  shield coverage. Coordinate the `CreateMergedXmlFile` key with plan 042.
- Or (b) apply `PatchNNN_LoadTimeStamps_Lifecycle` only when the toggle is on, read at the first
  `GameInit` (that read is already made there), and do (a) for the two `MBObjectManager` targets.
  Then a player with the toggle off never gets the dispatcher patched.
- Either way, add a STOP condition ("a new target is not on PatchShield's exclusion list") and a
  done-criterion check, and correct decision 2 and the "exceptions are unchanged" sentences.

## Non-blocking

- **N1. Step 4 (:838-843):** `HookStampService.Start` reads `gate.Enabled`, which logs
  `INFO [LoadStamps] detail on: ...` into the same `RecordingLogger` the test inspects. An
  exact-sequence assertion in `Mark_LogsTheTimeSinceThePreviousMark` then fails. Either give the gate
  its own logger in these tests or tell the executor to filter on `[LoadPhase]`.
- **N2. Step 5 (:863-867):** the helper `ApplierTiming` passes `_logger`, which is the class's
  NSubstitute logger (`PatchCategoryApplierTests.cs:30,36`). The oracles, though, are
  `INFO [PatchApply] ...` strings, the `RecordingLogger` format. Say which logger the helper takes.
  The existing `TryApply_WhenTheApplyThrows...` oracle "the existing ERROR line" also needs the
  `RecordingLogger` form (`ERROR [PatchApply] B FAILED ...`).
- **N3. Step 7 (:982-984):** the helper to copy, `SubModuleMethodBody`
  (`PatchCategoryApplierTests.cs:256-273`), reads through the class's private `CommentPattern`
  (:24-25) and `File.ReadAllText`, not `RepoPaths.ReadSource`. Tell the executor to replace its first
  line with `RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)`. I simulated the brace
  matcher on comment-stripped `SubModule.cs`: `OnGameInitializationFinished` closes at :2000 and
  `OnGameStart` at :891, so the helper works on both bodies.
- **N4. Step 11 (:1153-1156):** the `[HarmonyPatch(typeof(MBObjectManager),
  nameof(MBObjectManager.CreateMergedXmlFile))]` attribute is implied but not written. Spell it out,
  as :1137-1138 does for `LoadXML`. `CreateMergedXmlFile` has one overload
  (MBObjectManager.cs:962), so no argument-type array is needed.
- **N5. Done criterion (:1488-1491):** the patch classes and the binding tests reference
  `LoadTimeStampsModule.LoadXmlCategory`/`LifecycleCategory`, not the literal string. So
  `git grep -n "PatchNNN_LoadTimeStamps_LoadXml"` lists the module constant, the registry heading and
  any doc mention, not "the patch classes ... the tests". Give the exact expected file list, for
  example `LoadTimeStampsModule.cs`, `harmony-patch-registry.md`, `load-time-stamps.md`.
- **N6. Done criterion (:1500-1501):** "`git status --porcelain` is empty". The executor's worktree
  can hold run files it did not create (this worktree has six untracked plan files today). Word it as
  "no entry beyond those recorded at Step 1".
- **N7. Current state (:495-497):** the reservation list leaves out plan 039's `Patch101`
  (`plans/039-campaign-map-frame-profiler.md:435`, :695 "plan 040 picks its own"). Step 1's
  first-free search still yields `Patch100`, but say so. Note that `git grep` does not see untracked
  plan files.
- **N8. Step 5 (:888-939):** `PatchCategoryApplier.cs` (namespace `TAOM`, usings at :1-4) needs
  `using TAOM.Core.Diagnostics;` and `using TAOM.Features.LoadTimeStamps;`. A core class then depends
  on a feature's line builder. Name the usings, or put the two `[PatchApply]` formats beside the
  applier.
- **N9. Stage C semantics (:584-596, :1350-1368):** a listener added during a dispatch is
  head-inserted after the wrap. Example: an `OnNewGameCreated` handler registering a PartialFollowUp
  listener (`MbEvent`1.cs` `AddNonSerializedListener`). Such a listener runs untimed and is missing
  from `listeners=`. It is harmless, but the feature doc should say it.
- **N10. Step 6 (:945-978):** it has no RED step. The never-throwing forwarders in
  `LoadTimeStampsHooks` (`DetailEnabled`, `StartHook`, and later `BeginLoadXml`/`EndLoadXml`/
  `BeginDispatch`/`EndDispatch`) are tested only through the Step 11 shape test's no-service row.
  ADR-008 asks 80% of hooks. Add a small `LoadTimeStampsHooksTests` (unwired returns false/null; a
  throwing service is swallowed).
- **N11. Step 15 (:1384-1385):** "an owner of a type from `Main`, for example a `LoadXmlCall`" needs a
  constructor that :1091 does not specify. Name a concrete owner with a known constructor (for
  example `new StopwatchStampClock()`).
- **N12. Blast radius (:510-530):** I did not re-run `graphify_taom.py`. The quoted output is
  UNVERIFIED by this review.

## Excerpt check (item 3)

Matched at `0912e1b7` (each opened and compared):
- `Main/PatchCategoryApplier.cs`: 84 lines, class at :21, `TryApply` at :38-52 text identical,
  `RecordSkippedClasses` :58, `TakeFailureSummary` :74.
- `Main/SubModule.cs`: :206-209 constructor; :655-656, :675-685, :849-891, :1541-1566,
  :1953-1960, :1996-2000, :2010-2016. Every `TryPatchCategory` call sits in one of the four blocks
  (checked by line range). There is no existing `hookStamp`/`onceStamp` local and no `return`
  after the guard.
- The anchors of `FeatureModulesTests` :116-127 and :144-175; `PatchCategoryApplierTests` :42,53,59,
  158, :194-198 and :256-273; `PatchCategoryIndexTests` :85.
- `BattleLoadDiagnosticsSettings.cs` :58-61 (62 lines); the provider :41-42 (61 lines); the
  interface (31 lines); `BattleLoadDiagnosticsIoC.cs:10`. Nothing besides the provider implements the
  interface by hand.
- `SettingsFingerprintTests` :205-211, :217-252, :277, :449-458; `CoopSettingsRelevance` :70-72;
  `coop-interop.md` :310, :312, :316; `bannerlord-together-compat.md` :291. No edited line has a
  pre-existing em or en dash, so the dash done-check cannot false-fire on them.
- `FeatureModules.cs` :16-24; the exemplars `TournamentRewardsModule`, `CreatureBanditsModule`
  :57-58 and `TournamentRewardsService` :15, :25.
- Registry :1051, :1101, :1107; `feature-map.md` :85; `battle-load-diagnostics.md` :594 and the last
  row `MemorySampleIntervalSeconds`; `reflection-sites.md` Category B header and status lines;
  `ReflectionSiteBindingTests` last row :120 and `ResolveType`/`HasMember` (NonPublic|DeclaredOnly).
- `Patch85...BindingTests.cs:103`, `Patch83_StaleCharacterRepair.cs:33`,
  `CultureMarketplaceBehavior.cs:72`; listener counts 20/19/43/5; `SubModule.xml` 110 / 48 / 28 / 19.
- Engine: `MBObjectManager` :786-797, :913, :942, :962-978, single `LoadXML`;
  `CampaignEventDispatcher` :1053/:1062/:1071/:1080/:1089, one overload each, shape :1071-1078;
  `CampaignEvents` :293-307, :597, :857-869, :2088-2116; `MbEvent`1`/`MbEvent`2` whole files;
  `Campaign` :703/715, :752/754, :1389, :1471, :1481-1492, :1603-1609, :1685-1711; `CustomGame`
  :34-56 and :123-126; `SandBoxSubModule` :113-114; `Game.LoadBasicFiles`.

Mismatches (minor):
- :123-125: `MBObjectManagerExtensions.LoadXML` (`TaleWorlds.Core.MBObjectManagerExtensions.cs:7-17`)
  null-guards `Game.Current` (passing `gameType = ""` and `isDevelopment = false` without a game) and
  passes `current.GameType.IsDevelopment`. The plan says only that it passes
  `Game.Current.GameType.GameTypeStringId`. This does not change the design: `game=none` already
  covers the empty id, and `GameTypeStringId` defaults to `GetType().Name` (`GameType.cs:40`).
- :472: `MissionPerfHeartbeatBehavior.cs:52` is in `Main/Features/MissionPerf/Hooks/`.

## Checklist (item 4 and 5)

- TDD order: RED before GREEN in Steps 2-5, 7, 9-11 and 13-16. Step 6 has none (N10).
- Issue line: present ("filed by the orchestrator"). Binding ADRs 002/003/004/005/007/008 are named,
  one line each.
- Step 0: none, with the protected-file list. Single-owner: `SubModule.cs` edits are exact (Steps 7
  and 11), and `IoC.cs`/csproj are out of scope with a STOP.
- STOP conditions are specific: excerpt drift, exception or order changes in the shape and adapter
  tests, binding mismatch, source gates, the overhead bound, the settings phrases, patch-number
  exhaustion. The PatchShield condition is missing (B1).
- Done criteria are machine-checkable apart from N5 and N6.
- Planned-at `0912e1b7` and the drift-check paths match Scope. The exception is B1's fix, which adds
  `PatchShieldPolicy.cs`.
- Commands are non-deploying with `-p:DisableModuleCopy=true -p:ModuleId=`, and the RefAsm commands
  match `.github/workflows/csharp.yml` :59, :67, :83. There is no worktree path or branch name, and
  `CHANGELOG.md` is out of scope.
- No em or en dash in the plan (scripted check: 0 lines). No secret values, no local absolute paths.
