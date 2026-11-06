# Bannerlord campaign heartbeat — Campaign.Tick / CampaignTime / MobilePartyAi (Phase 19)

> **One process, traced from the decompile** (`TaleWorlds.CampaignSystem`, v1.4.5): the campaign-map "frame" — the
> clock that advances `CampaignTime`, the per-frame `Campaign.Tick` that fires the Phase-9 periodic events, and the
> party AI that decides where each `MobileParty` goes (until it collides into a Phase-17 encounter). This is the
> *heartbeat* that drives every TAOM `DailyTick`/`HourlyTick` behavior. It completes the campaign layer (Phases 9,16,17)
> the way Phases 13-15 completed the mission layer. Part of the phased engine study.

## WHAT it is

The campaign map runs a loop each rendered frame: the **clock** (`MapTimeTracker`) advances **`CampaignTime`** at the
player's chosen speed (`TimeControlMode`); **`Campaign.Tick`** dispatches the periodic events (hourly/daily/weekly — the
Phase-9 bus), advances active `MapEvent`s, runs **party AI** (each `MobileParty` scores `AiBehavior`s and moves toward a
target), and runs **`EncounterManager`** which turns a party-collision into a Phase-17 encounter. The whole campaign
*is* this loop.

## HOW it works

### The clock — `CampaignTime` (struct, CampaignTime.cs:11)
A value type wrapping **`_numTicks`** (a `long`, :56). **`CampaignTime.Now => Campaign.Current.MapTimeTracker.Now`**
(:70); `CurrentHourInDay` (:93); conversions `ToHours`/`ToDays`/`ToWeeks`/`ToSeasons` (:138-144); static span factories
`CampaignTime.Days(n)`/`Hours(n)`/`Weeks(n)`. A point *or* span in campaign time, comparable via `IComparable`.

### Time control — `Campaign` (Campaign.cs)
`Campaign.Current` (:376) — the campaign singleton. **`TimeControlMode`** (`CampaignTimeControlMode`:
Stop/Play/UnstoppablePlay/FastForward, :353) is the player's speed. **`RealTick(realDt)`** (:885) → `TickMapTime(realDt)`
(:840) → **`MapTimeTracker.Tick(4320f * num)`** (:874) — advances campaign time by real-dt scaled by the speed mode (so
1 real second = N campaign minutes depending on Play vs FastForward; `_dt==0` when paused).

### The tick — `Campaign.Tick()` (Campaign.cs:954)
Each frame (when `_dt>0` or warming up):
```
CampaignEventDispatcher.Instance.Tick(_dt);                  // raises TickEvent
_campaignPeriodicEventManager.OnTick(_dt);                   // crosses hour/day boundaries → HourlyTick/DailyTick/Weekly (Phase 9)
_campaignPeriodicEventManager.MobilePartyHourlyTick();       // per-party hourly
MapEventManager.Tick();                                      // advance active MapEvents (Phase 17)
... EncounterManager.Tick(_dt);                              // detect collisions → StartPartyEncounter (Phase 17)
_campaignPeriodicEventManager.TickPartialHourlyAi();         // STAGGERED party-AI ticks
```
`CampaignEventDispatcher.HourlyTickParty(mobileParty)` (CampaignEventDispatcher.cs:1251) fans each party's hourly tick
out to every event receiver (:1256); this is the dispatch TAOM's CastleRecruitment listens to (a `HourlyTickPartyEvent` listener).

### Party AI — `MobilePartyAi` (MobilePartyAi.cs:17)
`Ai.Tick(dt)` (:311): if **`DefaultBehaviorNeedsUpdate`** (:101) → recompute. `CheckPartyNeedsUpdate` (:468) scores
candidate `AiBehavior`s and picks `bestAiBehavior` (:479), setting the party's goal. On `MobileParty`:
**`DefaultBehavior`** (`AiBehavior` — Hold/GoToSettlement/EngageParty/DefendSettlement/Raid/Besiege…, :724),
`ShortTermBehavior` (:419), **`TargetSettlement`** (:741); setting any flips `DefaultBehaviorNeedsUpdate` (:735) so the
engine re-decides next AI tick. The chosen behavior moves the party across the map (`AiBehaviorTarget` is a
`CampaignVec2`, :341) until it reaches a target → `EncounterManager` → encounter.

### The loop closes
```
RealTick → MapTimeTracker advances CampaignTime (at TimeControlMode speed)
  → Campaign.Tick → { periodic events fire (Phase 9 Hourly/Daily/Weekly)
                      party AI decides + moves (MobilePartyAi → DefaultBehavior/TargetSettlement)
                      EncounterManager detects collision → StartPartyEncounter (Phase 17) → MapEvent → Mission }
  → (repeat next frame)
```

## WHY it's shaped this way

Scaling a single `MapTimeTracker` by `TimeControlMode` lets the *same* simulation run at any speed (and pause) without
changing the logic — periodic events fire on **clock-boundary crossings** (once per campaign day, regardless of frame
rate or fast-forward; a load can repeat a day, see the gotchas below), so behavior is deterministic in game-time, not wall-time. Staggering party-AI ticks
(`TickPartialHourlyAi`) spreads the cost of thousands of parties across frames. `AiBehavior` + `DefaultBehaviorNeedsUpdate`
is a lazy recompute: parties only re-decide when something changed, not every frame.

## TAOM relevance + gotchas
- **Every TAOM periodic behavior is driven here** (Phase 9): CultureConversion daily completion, CastleRecruitment
  daily maintenance + `HourlyTickPartyEvent` listener, CultureMarketplace `DailyTickSettlement`, SpecialResources,
  BanditManagement — all fire on this clock crossing day/hour boundaries.
- **CastleRecruitment** sits exactly on this loop: a `HourlyTickPartyEvent` listener in `CastleRecruitmentBehavior`
  (the per-party hourly dispatch, :1256; it invokes vanilla's private `CheckRecruiting` for AI parties in a castle)
  and the Patch42 transpiler on `AiVisitSettlementBehavior.AiHourlyTick` (the party-AI decision that routes AI lords
  to settlements, raised by AiPartyThinkBehavior on TickPartialHourlyAiEvent, every 6 hours by default, 1 or 3 for
  armies and rethinks).
- **`CampaignTime` is the unit for all TAOM deadlines**: Messengers travel days, Siege-defense deadline,
  CultureConversion `RequiredHoldDays`. Build with `CampaignTime.Now + CampaignTime.Days(n)`, compare via operators —
  **never raw `_numTicks`**. It's a `struct` (cheap by-value).
- **Periodic events fire on game-time boundaries, not real-time**: a `DailyTickEvent` handler runs once per elapsed
  campaign day even at FastForward, but not across a load. On every load `Campaign.CreateCampaignEvents` (v1.4.8
  `Campaign.cs:1002-1026`, run from `OnDataLoadFinished` at `:724`) gives the daily event an initial wait equal to
  the part of the day already elapsed (`:1004-1010`), and `MBCampaignEvent` schedules it at `Now + InitialWait`
  (`MBCampaignEvent.cs:32`). A save made at fraction f of elapsed day S therefore next ticks at S + 2f. Day S runs
  again when f is under a half (09:00 to 21:00 on the game clock, since elapsed days count from `CampaignStartTime`,
  09:00, `DefaultCampaignTimeModel.cs:7`), later ticks keep that offset, and repeated reloads can skip a day. Only
  the hourly event follows that S + 2f shape, on the hour (`:1012-1018`). The quarter-hourly event (`:1019-1025`)
  takes the elapsed fraction g of the current quarter but counts it in hours, so its first wait is 4g quarters, up
  to four periods: it next fires at Q + 5g quarters, runs quarter Q again only when g is under a fifth, and can skip
  up to four ticks. With no remainder (a save exactly on a boundary) the hourly and quarter-hourly first waits are
  half a period (0.5 and 0.125 hours), while the daily wait is a full day. Key once-a-day work on a saved last-run
  day: store the last elapsed day the handler ran with its state and skip a repeat. TAOM's daily listeners are not
  audited for this yet (#793). Read from the managed source; the in-game check is owed (#793).
- **Party-AI ticks are staggered** (`TickPartialHourlyAi`) — not every party every frame; a behavior that scans "all
  parties" each frame is wrong/expensive. React to the per-party tick instead.
- **Don't fight `DefaultBehaviorNeedsUpdate`** — setting `DefaultBehavior`/`TargetSettlement` every frame thrashes the
  AI; set once and let the engine recompute on its next AI tick.
- **`CampaignVec2` targets lazy-resolve via the map scene** — prefer raw accessors in editor mode
  (`feedback_campaign_coupled_property_in_editor`).
- **Listeners run newest first** (v1.4.8 taom-src `MbEvent.cs:24-30`, `:32-44`): `AddNonSerializedListener` puts each new
  record at the head of the list and `Invoke` walks from the head, so a listener added after the engine's own runs
  BEFORE it on the same event. (yotthani's CampaignProbe README observed the same on 1.5.5.)

## Measured costs on TAOM's map [yotthani]

**On the 1.4.x line (plan 144):** measured on 1.5.3, not re-measured on v1.4.8. This line has the NameplateCull (plan 120) and the ButterLib distance matrix switch (plan 118) the bullets name.

yotthani's first campaign-map run on a real save with the full mod list (2026-10-02 21:08, 236 s measured, 1,945 to
2,524 parties, 1,002 settlements in yotthani's count of that session; a foreign test run kept one core at 18.6 % load
at the start). All figures: yotthani, MithrilForge `docs/engine/perf.md`@0a2f4761, measured in game, v1.5.3; v1.5.5:
not re-checked; the version of TAOM loaded is not stated.

- **Stutter is single long frames, not a low average.** The averages were fine: 106 FPS with time stopped and the
  camera moving, 86 in Play, 72 in fast forward. The run held 154 outlier frames that added up to 13 s (lines 858-867).
- **A save costs about 4 s of still frame.** Two stalls of 2.8 s and 2.6 s sat in `MapState.OnTick` around
  `onBeforeSaveEvent`, and each was followed by one more frame of about 1.2 s with no managed time in it (lines
  869-871).
- **Settlement nameplates were the largest steady cost.** `GauntletMapSettlementNameplateView.OnMapScreenUpdate` took
  4.0 ms a frame in fast forward (29 % of the frame), 0.9 ms with time stopped and 5.9 ms inside the outlier frames.
  yotthani's code reading: it updates every nameplate every frame, a `TWParallel.For` over all of them and then
  `RefreshBindValues()` for each in turn, whether or not the settlement is on screen (lines 880-885). TAOM's answer
  is [nameplate-cull.md](../../features/nameplate-cull.md); with `nameplate-cull` on, yotthani's later run measured
  0.17 ms (line 1337).
- **Event handlers cost 142 ms per campaign hour, 97 % of it vanilla and 3 % TAOM** ("our mods not measurable"),
  which is about 245 ms per real second in fast forward at 1.7 campaign hours a second. The largest were
  `AiPartyThinkBehavior.PartyHourlyAiTick` at 32 ms an hour (2,500 calls), `AiVisitSettlementBehavior.AiHourlyTick` at
  16, `PoliticalStagnationAndBorderIncidentCampaignBehavior.HourlyTickSettlement` at 9.6, `AiEngagePartyBehavior` at
  6.5, `NotablesCampaignBehavior.DailyTickHero` at 5.9 and `AiMilitaryBehavior` at 5.6 (lines 887-890). After
  yotthani's tuning levers (`nameplate-cull`, `butterlib-lazy-distance`, server GC) the total was 62 ms an hour, 94 %
  vanilla and 6 % TAOM, at 118 FPS in fast forward (lines 1333-1338, 2026-10-04). The TAOM share rose from 3 % to 6 %
  while the total fell; by arithmetic that is about 4.3 ms and about 3.7 ms an hour.
- **Step sounds were refuted at the call site.** The first suspicion, `MapScreen.TickStepSounds` calling the native
  step sound for every visible party each frame, was read from code (lines 824-827) and then measured at about 0.2 ms
  a second (line 892). If the sound stutters, yotthani says the cause is native (FMOD) and only a profile would show it.
- **The "before" numbers include ButterLib's rebuild, which TAOM removes.** The run above was taken with the user's full
  load list (line 853), which includes ButterLib; that its Distance Matrix was on is TAOM's inference from the user
  setting yotthani read the next day ("Distance Matrix Enabled" `true`, line 1211). In that later run (2026-10-03
  23:39) yotthani found that the six worst
  `MapEventManager.Tick` frames (229 to 292 ms) were all siege ends running ButterLib's
  `GeopoliticsBehavior.UpdateOnSettlementOwnerChanged`: 13 calls, 2,250 ms, 173 ms on average, up to 281 ms in one
  frame (lines 1205-1208). TAOM switches that subsystem off, so figures taken with it running overstate what a TAOM
  player sees; see [butter-lib-distance-matrix.md](../../features/butter-lib-distance-matrix.md).

## The native boundary
**None** — the campaign heartbeat is **entirely managed** (clock, tick dispatch, periodic events, party AI are all C#).
The only engine touch is the **map scene** for party positions (`CampaignVec2` → native map mesh, lazy-resolved). This
is the campaign layer (Phase 16) running; it hands off to native only at the Phase-17 `MissionState.OpenNew` seam when a
battle the player watches actually loads.

## Evidence (file:line, v1.4.5)
- `Campaign.cs`:376 (`static Current`), :349 (`MapTimeTracker`), :353 (`TimeControlMode`), :885 (`RealTick`), :840 (`TickMapTime`), :874 (`MapTimeTracker.Tick(4320f*num)`), :954 (`Tick` → `CampaignEventDispatcher.Tick`/`_campaignPeriodicEventManager.OnTick`/`MapEventManager.Tick`/`EncounterManager.Tick`/`TickPartialHourlyAi`).
- `CampaignTime.cs`:11 (`struct : IComparable`), :56 (`_numTicks`), :70 (`Now`), :93 (`CurrentHourInDay`), :138-144 (`ToHours`..`ToSeasons`).
- `CampaignEventDispatcher.cs`:1251 (`HourlyTickParty` → :1256 fan-out).
- `MobilePartyAi.cs`:17 (`class`), :101 (`DefaultBehaviorNeedsUpdate`), :311 (`Tick`), :468 (`CheckPartyNeedsUpdate`), :479 (`bestAiBehavior`). `MobileParty.cs`:724 (`DefaultBehavior`), :419 (`ShortTermBehavior`), :741 (`TargetSettlement`), :341 (`AiBehaviorTarget` CampaignVec2).
- Linked: campaignevents-and-campaignbehavior.md (Phase 9, the events fired here), campaign-object-graph.md (Phase 16, the parties moved), campaign-to-mission-bridge.md (Phase 17, the encounter this produces). Gotcha: `feedback_campaign_coupled_property_in_editor`; CastleRecruitment Patch42 (CLAUDE.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)
- [docs/reference/engine/issue-and-quest-system.md](./issue-and-quest-system.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](./settlement-economy-food-prosperity.md)

<!-- backlinks-end -->
