# lane-perf findings: /improve run 2026-11-12-fable

Baseline `7087fa633` (worktree `wt-baseline`). Read-only. Scope: production C# under `Main/` and
`Client/` changed since 2026-10-04, minus the BRIEF skip list.

## How this lane worked

- **Files in scope.** `git -C wt-baseline log --since=2026-10-04 --name-only --format= -- Main Client`,
  skip list removed: 2,550 `.cs` paths, 2,436 still present at the baseline. Of these, 130 hold a
  per-frame, per-hit or campaign-tick method (grep for `OnMissionTick`, `OnLateUpdate`, `OnRender`,
  `TickEvent`, `HourlyTickPartyEvent` and similar), and 229 hold a Harmony patch. I read the hot
  targets among the patches (per-agent movement, per-frame mission views, per-nameplate map
  methods, roster writes) and every new campaign `TickEvent`, `HourlyTickPartyEvent` and
  `DailyTickPartyEvent` listener added since 2026-10-04.
- **Smell scan.** `python -B scratch/lane-perf/hotbodies.py <worktree>` prints every LINQ call,
  `IoC.Resolve`, `TaomSettings.Instance`, `new List/Dictionary/HashSet`, string interpolation and
  `Invoke` inside a per-frame or per-hit method body under `Main/` and `Client/` (skip list out):
  55 lines. Almost all are on an exception path or behind a run-once flag; the real ones are below.
- **Measurement.** The in-game profilers from plans 028 and 041 left data on this desktop.
  `python -B scratch/lane-perf/ticksummary.py "<game>/bin/Win64_Shipping_Client/Logs" "taom_debug_2026-1*.log"`
  reads every `[TickSummary]` and `[PerfContext]` line (output saved as
  `scratch/lane-perf/ticksummary.out`). These are desktop numbers, not player numbers.
- **Code quality context.** The code added since October is written against the October lessons:
  the per-agent patches (`MarshalPacePatch.cs:19-26`, `MarshalThrowerPatch.cs:21-37`) read one
  volatile table and allocate nothing, the map nameplate prefix (`PartyNameplateVM_RefreshBinding_HiddenHosts_Patch.cs:39-66`)
  works from a cached snapshot, the HUD mixins read MCM every 30 refreshes
  (`MissionAgentStatusBattleHudMixin.cs:35, :48-52`) and cache the key label
  (`AbilityInputAdapter.cs:17-24`). Few findings follow from that, and the leverage of each is small.

## Findings

### [PERF-01] Run one full-attribution profile before any battle-frame refactor

- **Evidence**: 23 profiled missions on 2026-11-10 to 2026-11-12 (`taom_debug_2026-11-10_19-57-46.log`
  to `taom_debug_2026-11-12_10-07-48.log`, 46,647 to 170,328 frames each, about 295 agents at peak per
  `[MissionPerf] agents=`): the mean frame is 8.69 ms, of which `missionTickMs` (every behaviour's
  `OnMissionTick`, vanilla and TAOM together) is 0.388 ms (4.5%), `agentTickMs` 0.36 ms, `cameraMs`
  0.6 ms; main-thread allocation is 9.4 to 11.5 KB per frame (gen0 about 3 to 8 per 5 s). Every line
  says `top=none` and every `[PerfContext]` says `tickProfiler=off`: no log on this desktop has
  per-behaviour attribution, and none has a `[MapProfile]` line (29 starts logged `[MapProfiler] off`).
- **Impact**: TAOM's whole share of the mission tick is under half a millisecond at 295 agents, so no
  mission-side refactor this lane can name is worth more than a fraction of that, and the October run's
  per-behaviour ranking was never confirmed by a full-mode run. The campaign map frame has no
  measurement at all since plan 039 shipped.
- **Effort**: S (one session by the maintainer: `Enable Tick Profiler` on, restart, one large battle of
  500+ agents; `Enable Map Profiler` on, 10 minutes on the map at play and fast-forward speed).
- **Risk**: LOW: settings only, both default off.
- **Confidence**: HIGH (numbers read from the logs with the script named above).
- **Fix sketch**: no code. Read the `[TickSummary] top=` and `[MapProfileSummary]` rows with
  `tools/perf_runs.py` and rank the next perf run from them.
- **Delta**: pre-existing (instrument exists since plans 028, 039, 041).
- **P1**: no.
- **Plan candidate**: yes, a measurement plan for the maintainer; it decides whether a battle-frame
  perf plan is worth writing at all.
- **Issue**: none found (the October issues #710 to #724 are closed or owned by their plans; not checked
  on GitHub this run).

### [PERF-02] Fords match allocates nine times the main-thread garbage of every other battle (skip-list area: observation only)

- **Evidence**: `taom_debug_2026-11-11_00-36-00.log`, scene `taom_fords_01_forceatmo`, 71,481 frames,
  423 agents at peak: 91.9 KB allocated per frame and `missionTickMs` 0.753 ms per frame, against
  9.4 to 11.5 KB and 0.23 to 0.48 ms in the 22 other missions (siege and field scenes at about 295
  agents). The window lines show it is sustained, not a spike: 80.8 KB per frame at t=+58 s, 89 to
  101 KB from t=+108 s to t=+608 s (`[TickProfile]` allocKB over frames).
- **Impact**: about 9 MB of gen0 garbage per second at 100 fps on the main thread in the Fords mode,
  and twice the mission-tick time; on a slower player machine the collections become frame spikes.
- **Effort**: unknown (not read: skip list).
- **Risk**: n/a for this run.
- **Confidence**: HIGH that the number is real; the source inside the Fords features is UNVERIFIED (part
  of the extra may scale with the 423 agents rather than come from Fords code).
- **Fix sketch**: for the Fords owner after tonight's match: one Fords match with `Enable Tick Profiler`
  on names the behaviour in `[TickSummary] top=` (its allocKB column).
- **Delta**: introduced (FordsOfWar is post-October).
- **P1**: no.
- **Plan candidate**: no (BRIEF skip list: FordsOfWar runs live tonight); hand to the Fords owner.
- **Issue**: none known.

### [PERF-03] Measure the engine's unvalidated native merges before scoping the XML fast path wider

- **Evidence**: the `[XmlMerge]` lines of 30 game starts in this desktop's logs: `soln_action_sets`
  (3 files, 2 XSLTs) takes a mean 1,492 ms (1,147 to 2,739 ms), `soln_skins` 453 ms, `soln_action_types`
  81 ms, all `path=vanilla reason=skip-validation`, once per process start. The fast path covers only
  validated merges by scope (`docs/features/xml-merge-fast-path.md:63-67`, which says unvalidated
  merges "cost little"). The two action-set XSLTs come from DualWield (`Modules/DualWield/ModuleData/action_sets.xslt`,
  53 KB) and the Armory (`LOTRLOME_Armory/ModuleData/action_sets.xslt`), not from TAOM.
- **Impact**: about 2 s of every game launch sits in a merge the fast path does not touch. How much of
  it the fast path could save is unknown: with three files there are only two round trips to drop, and
  the XSLT transform of the native action-set document may be most of it.
- **Effort**: S to measure (the plan 042 harness already merges any type offline both ways); M if the
  scope then widens.
- **Risk**: MED if widened: the native merges feed the engine's native loader, so the byte-identity proof
  must cover each unvalidated type.
- **Confidence**: LOW on the saving, HIGH on the 1.5 s.
- **Fix sketch**: run the 042 harness on `soln_action_sets` and `soln_skins` and split load, XSLT and
  round-trip time. Widen the fast path to unvalidated merges only if round trips are a large share;
  otherwise correct the doc's "cost little" with the measured figure.
- **Delta**: pre-existing (the doc's scope line); the measurement is new.
- **P1**: no.
- **Plan candidate**: yes, an investigate plan (measure first).
- **Issue**: none found.

### [PERF-04] Drop the per-frame list copy and boxed enumerators in the custom-battle horn call

- **Evidence**: `Main/Features/CustomBattles/Muster/CustomBattleHornCallBehavior.cs:43`:
  `foreach (var b in _blowers.Values.ToList())` runs in `OnMissionTick` (line 30) every frame; the two
  loops at lines 35 and 50 enumerate `_roles.HornBlowers`, typed `IReadOnlyList<Agent>`
  (`CustomBattleRolesBehavior.cs:24`), so each `foreach` boxes a `List<Agent>.Enumerator`. Added only to
  a mustered custom battle (`CustomBattleRolesBehavior.cs:10-11`, `CustomBattleMuster.cs:85-87`).
- **Impact**: three heap objects per frame (a `List<HornBlower>` with its array and two boxed
  enumerators, a few hundred bytes with one horn blower per unit), every frame of a mustered custom
  battle. Small against the 10 KB per frame baseline in PERF-01.
- **Effort**: S.
- **Risk**: LOW: iterate a reused `List<Agent>` of removals after the loop (or a for loop over the
  values snapshot kept in a field), and index the `IReadOnlyList` with a for loop. Same order, same
  removals.
- **Confidence**: HIGH on the allocation, the size is an estimate.
- **Fix sketch**: replace `ToList()` with a reused field buffer filled per frame (or collect stale keys
  into a reused list and remove after the loop); use `for (int i = 0; i < list.Count; i++)` for the two
  `HornBlowers` loops. A unit test can pin the removal behaviour with fake agents only if the class
  takes an agent seam; otherwise it stays game-tested (ADR-008, as its summary says).
- **Delta**: introduced (2026-11-12).
- **P1**: no.
- **Plan candidate**: yes, but low leverage; bundle with any other custom-battle muster change.
- **Issue**: none.

### [PERF-05] Measure the battle HUD ring's draw count before it ships to large battles

- **Evidence**: `Client/Features/BattleHud/Widgets/TaomBattleHudRingWidget.cs:133-187` draws every
  frame as runs of 4.5 degree tangent rectangles (`BattleHudGeometry.cs:33`): 80 per full ring, so
  the shell rim, three bands (fill plus track plus two rims, 240 rectangles each) and the centre rim
  come to about 885 rectangles, as the class summary says ("About 900 rectangles a frame at most",
  line 28). Each stone segment takes its own pooled material (`MaterialFor`, line 273-295), and the
  engine queues one `TwoDimensionDrawData` per `DrawSprite` with no managed batching
  (v1.5.7 `TwoDimensionDrawContext.cs:141-161`, flushed one by one at `:125-139`).
- **Impact**: UNVERIFIED. About 885 UI draws per frame for one widget, against a whole vanilla battle
  UI of an unmeasured size; the cost lands in the UI render step, which the mission profiler counts
  only inside `otherMs`.
- **Effort**: S to measure; M to L to redesign (fewer segments or ring sprites change the look, so a
  fix is a design decision, not a refactor).
- **Risk**: LOW to measure.
- **Confidence**: LOW (a cost by construction, never measured).
- **Fix sketch**: one battle, the same scene twice, the game's own UI-hide toggle on and off; compare
  `[MissionPerf] mainMs` and `renderMs`. Act only if the difference is a material share of the frame.
- **Delta**: introduced (2026-11-10, colour modes 2026-11-12).
- **P1**: no.
- **Plan candidate**: investigate only; behaviour-preserving fixes do not exist here.
- **Issue**: #1095 is the HUD's own issue (named in the source, state not checked).

## Kimi lead 3: IoC.Resolve in LogTask.Execute and TaomBTLogger.LogMessage

**Verdict: not worth doing; the paths are not hot.**

- `TaomBTLogger.LogMessage` (`Main/Features/AdvancedCombat/TaomBTLogger.cs:8-13`) is the behaviour-tree
  library's `ILogger`. Its only callers are the tree's catch block, which then stops the tree
  (`Main/BehaviorTrees/BehaviorTreesCore.cs:74-78`: `LogMessage(...)` then `IsRunning = false`), and the
  listener dispatcher's catch, at most once per listener type per mission
  (`Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs:275-279`, `_throwingListeners.Add`). Frequency:
  zero in a healthy battle, at most once per tree or listener type on a fault.
- `LogTask.Execute` (`Main/Features/AdvancedCombat/BaseBehaviorTree/LogTask.cs:17-21`) is built only in
  the warg tree (`Main/Features/Warg/WargBehaviorTree.cs:58, :61, :66`), in the "player controlled" rage
  branch (`:51-67`): it runs when a player-ridden warg's rage finds an enemy, loses it or ends, and the
  branch sleeps a second between attacks (`SleepTask(new(0, 0, 1))`, `:63`). Frequency: about once a
  second at most, for one agent (the player's mount). The file's own note says the same
  (`WargBehaviorTree.cs:40-41`).
- A cached logger would save one DryIoc resolve per such call: nanoseconds, a few times a minute.
- Profiler row that would show otherwise: `[TickSummary] top=` naming `BehaviorTreeMissionLogic` with a
  high per-call time in a warg battle.

## Considered and rejected

- Hero Auras pulse (`HeroAuraPulse.cs:50-81`): one `List<HeroAura>` and one key string per reached
  soldier per pulse, at 1 pulse per second (`hero_auras` config `pulseSeconds: 1.0`). Tens to hundreds of
  small objects a second; not worth a pool.
- Hero aura merge (`RaceAbilityEffectsMerge.cs:16-52`): allocates only while a soldier has both a live
  race ability and an aura, as documented; rare.
- Marshal's Word per-agent patches (`MarshalPacePatch.cs`, `MarshalThrowerPatch.cs`, `MarshalThrowerTimer.cs`,
  `MarshalSquareWidthPatch.cs`): one volatile read and a linear scan of a few formations, no allocation.
  The tables cannot be hashed (`MarshalThrowerTable.cs:19`: `Formation.GetHashCode` reads the team).
- Seat of Seeing patches (`Client/Features/SeatOfSeeing/Hooks/Patch140_*.cs`): one static bool off the
  Seat.
- Hidden Hosts map hooks (`PartyNameplateVM_RefreshBinding_HiddenHosts_Patch.cs`,
  `HiddenHostsPlainSightBehavior.cs:55-60` real-time throttle at 0.5 s): already cached and throttled.
- Campaign map widgets (`CampaignMusterWidget.cs:37`, `CampaignStatsPanelWidget.cs:28-63`): one small
  `BannerImageIdentifier` per frame (`Banner.BannerCode` is cached, v1.5.7 `Banner.cs:45`); the layout
  setters are change-guarded (v1.5.7 `Widget.cs:531-543`).
- BattleHud mixins: settings read every 30 refreshes, key label cached (see "Code quality context").
- Staged siege (`StagedSiegeService.cs:319-346`): reconcile throttled to `ReconcileSeconds`, log lines
  throttled to `LogSeconds`.
- Swing alternative probe (`AnimVarietyMissionBehavior.cs:38-56`, `:118-125`): would allocate one
  `AnimVarietyAgent` per human agent per frame, and its MCM switch defaults on
  (`TaomSettings.cs:2385`), but it switches itself off when no `act_taom_` swing codes exist
  (`AnimVarietyService.cs:146-171`). All 26 missions in the November logs wrote
  `[AnimVariety] no alternative groups found; swing tracking off`. A latent cost only; the MCM hint
  already states it. Revisit if alternative clips land.
- Named companions (`NamedCompanionAdapter.cs:11-92`): linear scans of `Hero.AllAliveHeroes` per call,
  but the caller runs once a day (`OathsOfServiceCampaignBehavior.cs:58-66`).
- Lord retinue (`LordRetinueBehavior.cs:43-64`, `LordRetinueWorld.cs:56-89`): daily per lord party at
  rest, over the clan's heroes only.
- Eye of Sauron hourly and frame listeners (`EyeOfSauronBehavior.cs:59-71`): return at once unless a
  hunt is running.
- Watchful Holds hourly party listener (`WatchfulHoldsBehavior.cs:40-49`): filters to lord and main
  parties first.
- Temporary roster-loss diagnostic (`TroopRoster_AddToCountsAtIndex_Patch.cs:38-42`): a sign check and
  one reference compare per world roster write; a stack trace only for main-party removals. The cost is
  small; whether the temporary diagnostic should go is the arch lane's call (removal checklist at
  `docs/reference/harmony-patch-registry.md:1703`).
- TAOM's own re-reads of the `Monsters` and `SPCultures` merges at each game load
  (`MonsterSizeCatalogAdapter.cs:46-49`, `ObjectManagerAdapter.cs:104-110`): measured 9 to 10 ms and
  96 to 119 ms per load (`[XmlMerge] ... reason=skip-validation` lines, two logs). Removing them would
  need a capture hook in the merge patch for about 0.1 s per load.
- The battle HUD ring's per-frame materials (`TaomBattleHudRingWidget.cs:379-395`): pooled by the engine
  (v1.5.7 `TwoDimensionDrawContext.cs:55-60`, reset per frame at `:46-53`), so no garbage.

## What I did not cover

- The skip-list areas (HeroDefence, FieldBallista, CoopInterop, FordsOfWar, LongDefeat, the
  single-owner files), except the Fords measurement in PERF-02, which I did not trace to code.
- Code changed before 2026-10-04 and not touched since: left to the October run's plans 028 to 042.
- No per-behaviour or campaign-map number exists on disk (PERF-01), so every cost ranking above is by
  construction and call frequency, not by measured time. I could not launch the game.
- I read the hot targets among the 229 patched files, not all of them: UI view-model patches on screens
  that open on a click (inventory, party, clan, encyclopedia, kingdom decisions, character creation)
  were skipped as not per-frame. `EncyclopediaListScreenWidget.OnUpdate` calls `Model.Synchronize()` per
  frame (`EncyclopediaListWidgets.cs:21`); not read further, the screen is open only on demand.
- `Main/Features/ShadowfaxScreens`, `WarChronicle`, `Realms` client pages, `NotableServices`,
  `HardQuestions`, `KeepWardens`, `RedArrow`, `InnRounds` and the other new campaign features were
  checked only through their tick listener registrations and the world-scan grep
  (`scratch/lane-perf/worldscans.txt`, 86 lines), not read in full.
- The Python tools, build and test scripts: other lanes own them.
- GitHub issue states were not queried.
