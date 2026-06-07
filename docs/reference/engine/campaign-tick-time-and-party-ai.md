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
out to every event receiver (:1256) — this is the dispatch TAOM's CastleRecruitment Patch42 postfixes.

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
rate or fast-forward), so behavior is deterministic in game-time, not wall-time. Staggering party-AI ticks
(`TickPartialHourlyAi`) spreads the cost of thousands of parties across frames. `AiBehavior` + `DefaultBehaviorNeedsUpdate`
is a lazy recompute: parties only re-decide when something changed, not every frame.

## TAOM relevance + gotchas
- **Every TAOM periodic behavior is driven here** (Phase 9): CultureConversion daily completion, CastleRecruitment
  daily maintenance + `HourlyTickParty` postfix, CultureMarketplace `DailyTickSettlement`, SpecialResources,
  BanditManagement — all fire on this clock crossing day/hour boundaries.
- **CastleRecruitment Patch42** sits exactly on this loop: a postfix on `RecruitmentCampaignBehavior.HourlyTickParty`
  (the per-party hourly dispatch, :1256) + transpilers on `AiVisitSettlementBehavior.AiHourlyTick` (the party-AI
  decision that routes AI lords to settlements — the `MobilePartyAi` loop).
- **`CampaignTime` is the unit for all TAOM deadlines**: Messengers travel days, Siege-defense deadline,
  CultureConversion `RequiredHoldDays`. Build with `CampaignTime.Now + CampaignTime.Days(n)`, compare via operators —
  **never raw `_numTicks`**. It's a `struct` (cheap by-value).
- **Periodic events fire on game-time boundaries, not real-time** — a `DailyTickEvent` handler runs once per campaign
  day even at FastForward; don't assume frame cadence.
- **Party-AI ticks are staggered** (`TickPartialHourlyAi`) — not every party every frame; a behavior that scans "all
  parties" each frame is wrong/expensive. React to the per-party tick instead.
- **Don't fight `DefaultBehaviorNeedsUpdate`** — setting `DefaultBehavior`/`TargetSettlement` every frame thrashes the
  AI; set once and let the engine recompute on its next AI tick.
- **`CampaignVec2` targets lazy-resolve via the map scene** — prefer raw accessors in editor mode
  (`feedback_campaign_coupled_property_in_editor`).

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
