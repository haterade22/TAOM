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
  campaign day even at FastForward, but not across a load. On every load `Campaign.CreateCampaignEvents` (v1.5.5
  `Campaign.cs:1022-1030`, run from `OnDataLoadFinished`) gives the daily event an initial wait equal to the part of
  the day already elapsed, and `MBCampaignEvent` schedules it at `Now + InitialWait` (`MBCampaignEvent.cs:32`). A save
  made at fraction f of elapsed day S therefore next ticks at S + 2f. Day S runs again when f is under a half (09:00
  to 21:00 on the game clock, since elapsed days count from `CampaignStartTime`, 09:00), later ticks keep that offset,
  and repeated reloads can skip a day. Only the hourly event follows that S + 2f shape, on the hour
  (`:1032-1037`). The quarter-hourly event (`:1039-1045`) takes the elapsed fraction g of the current quarter but
  counts it in hours, so its first wait is 4g quarters, up to four periods: it next fires at Q + 5g quarters, runs
  quarter Q again only when g is under a fifth, and can skip up to four ticks. With no remainder (a save exactly on
  a boundary) the hourly and quarter-hourly first waits are half a period (0.5 and 0.125 hours), while the daily
  wait is a full day. Key once-a-day work
  on a saved last-run day, as `WarChronicleDailyGate` does ([war-chronicle.md](../../features/war-chronicle.md) "Once
  per day"). This is read from the managed source; the in-game check is owed (#793).
- **Party-AI ticks are staggered** (`TickPartialHourlyAi`) — not every party every frame; a behavior that scans "all
  parties" each frame is wrong/expensive. React to the per-party tick instead.
- **Don't fight `DefaultBehaviorNeedsUpdate`** — setting `DefaultBehavior`/`TargetSettlement` every frame thrashes the
  AI; set once and let the engine recompute on its next AI tick.
- **`CampaignVec2` targets lazy-resolve via the map scene** — prefer raw accessors in editor mode
  (`feedback_campaign_coupled_property_in_editor`).

## Measured costs on TAOM's map [yotthani]

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

## Siege relief, defend and sally rules, and the campaign band [yotthani]

Source for this section: yotthani, repo `yotthani/bannerlord`, at 6265f4a2: `docs/balance-sim/campaign.md`,
`docs/balance-sim/relief-joiners-measured.md`, `docs/balance-sim/taom-baseline-2026-10-18.md`,
`docs/balance-sim/campaign-positions.md`, `docs/superpowers/specs/2026-10-18-armory-autoresolve-design.md` and
`HoN/CampaignProbe/README.md`. The rules are yotthani's decompile reading of Bannerlord 1.5.5 ("Conversion/1.5.5"), so every
`file.cs` line number below is a 1.5.5 line, not re-read on 1.5.6. The counts are from CampaignProbe runs of TAOM 2.0.34 on
Bannerlord 1.5.5 (v1.5.5.124170). TAOM is now on Bannerlord 1.5.6 and v2.0.36, and nothing here has been re-measured.

### Relief, defend and sally

- **Relief comes from the own realm only** (decompile reading plus measurement, 8 pure-TAOM runs). yotthani's words:
  "relief is by the own realm, allies play no part" (`campaign.md`:817). The measurement: 99.9 % of the parties in the
  817 relief and joined-assault battles had the culture of their leader, and 423 of 425 relief battles were opened by a
  party of the owner's kingdom. yotthani's code reading is that the defender mission runs over the party's own map faction
  (`AiMilitaryBehavior.cs` 121-175, 157-160). The source adds that TAOM's `AiBehaviorHook.cs` (136-145,
  `GetSelfAndAlliedFactions`) adds allied factions; no file of that name exists in TAOM at 0c5d81408 (checked: `git ls-files`
  and a grep of `Main/`), so that clause is not about the current tree.
- **The defend test pools every nearby defender** (decompile reading; `relief-joiners-measured.md`:126 and
  `campaign.md`:817). Every `DefendSettlement` party for the fort within `SettlementDefendingNearbyPartyCheckRadius`
  (3 x 0.8 x 3 = 7.2 map units, `DefaultMobilePartyAIModel.cs` 30-32) of the camp leader is summed against the whole camp
  (`MobilePartyAi.cs` 1184-1224). A party then engages when its own army plus the allies at the waiting position exceed
  **1.2 x** the camp (1243-1247), or, when the strongest of them is already in a map event, once all defenders near the camp
  together exceed **1.0 x** (`campaign.md`:817; `relief-joiners-measured.md` words this as "without that test", 1248).
- **The relief arrives as a wave, and the first joiner is weak** (measured, 16 pure-TAOM runs of 2.0.34, 922 joined
  assaults). The first joiner alone has a median of 0.16 of the camp's men (`relief-joiners-measured.md`:80; men, which the source keeps apart from strength, :142), and yotthani's reading of the code is that it is weak "because it is the first of a wave" (:126); the pooled joiner
  force has a median of 1.33 camps after 1 hour and 1.59 after 24 hours, and 799 of the 922 joined assaults end
  `assault_repelled` ("A joined assault is how the game repels an assault", `relief-joiners-measured.md`:109). yotthani's
  own limit: the join path is inferred by the probe's listener and the think-cycle target is not recorded, so the code lines "are
  the readable rules, not a trace of the real call" (`:143`).
- **Timing and reach** (decompile reading). The party re-thinks every 6 hours, hourly for armies and leaders
  (`AiPartyThinkBehavior.cs` 56-66; the same cadence as the gotcha above). The defend score is
  `min(2, (own + committed) / enemy near the gate)` x distance x 1.75 (`DefaultTargetScoreCalculatingModel.cs` 142-215,
  297-301), and nothing farther than 8.02 travel days scores (`AiMilitaryBehavior.cs` 351-427). TAOM's own knobs on that
  score are `ArmyDefenderPriority` 1.6, `ArmyCommitmentMultiplier` 4.0 and `ArmyPriorityBoost` 3.0
  (`Main/Features/TaomSettings.cs`:516, 486, 491) [Certain, read at 0c5d81408].
- **Sally** (decompile reading plus 8 pure-TAOM runs, 94 real sallies). `SallyOutsCampaignBehavior.HourlyTickSettlement`
  checks every fourth game hour (`:44`), or every hour while the camp leader fights outside the walls. The garrison goes out
  when garrison plus parties inside plus friends near the camp leader exceed **2.0 x** the enemy there (`:90-95`), and a
  relief battle that the garrison joins uses **1.5 x** (`:96-108`). Measured: 11.8 real sallies a run, the garrison beaten
  in 51 % (`campaign.md`:774-778). Trap in the data: the AutoResolve log files a relief battle that the garrison joined
  under `SallyOut`, so a count taken from that log (18 a run) overstates sallies.
- **A camp leader that is in a map event cannot start the assault** (decompile reading, 1.5.5):
  `EncounterManager.cs:38` returns for a party with a map event side (commit e83fbae4 body).
- **Siege engines cause no troop casualties** (decompile reading, with a measurement that fits it). yotthani: "The engines of a
  siege hit walls and engines only (`SiegeEvent.cs:1150-1180`) ... there is no bombardment casualty among troops", and
  every casualty of a map event comes from `MapEvent.SimulateSingleTroopHit` (`MapEvent.cs:745-766`), one `SimulateHit` per
  tick, in a campaign without NavalDLC (`campaign.md`:967). The wall hit points were above 0 in all 3,031 siege-type events of
  16 runs. The source's own hedge: "the logs cannot show it directly".
- **Auto-resolve hit formula** (decompile reading, 1.5.5 lines; `armory-autoresolve-design.md`:41-43). `SimulateHit`
  (`DefaultCombatSimulationModel.cs` 17-29) computes `(int)((0.5 + 0.5 * rand) * 40 * Pow(Pstriker / Pstruck, 0.7) * advantage)`,
  then adds perk effects (mobile parties only) and the morale factor `AddFactor((min(ms-50,0) - max(mv-50,0)) * 0.005)`. The
  damage is a casualty probability for a regular troop, `RandomInt(MaxHitPoints) < damage`
  (`MapEventSide.cs` 923-975). The formula and these lines are not re-read on 1.5.6; only the signature is, for TAOM's override ([external-mod-couplings.md](../external-mod-couplings.md), the `TaomCombatSimulationModel` row).

### Probe README facts (engine, Conversion/1.5.5)

yotthani's CampaignProbe README (`HoN/CampaignProbe/README.md`@6265f4a2) records four engine facts. All are yotthani's reading
or yotthani's run-to-run observation on 1.5.5.

- **The daily tick is not at the clock's midnight.** README:275: "the daily tick is a periodic event whose phase is not the
  clock's midnight"; the probe's rows now count the hour since the row's day began. This matches the load-shift analysis above.
- **A siege ends before the game changes the owner.** README:283-289: the assault component removes the besiegers in
  `OnBeforeMapEventFinalize`, then the owner changes, so the probe holds a finished siege for one game hour and patches a
  capture within that hour. The end reason is not stored (`SiegeEvent._isBesiegerDefeated` is private and "only covers
  battle defeats"). A `ByKingDecision` owner change inside a kingdom is not a capture: under the probe's earlier rule run 7 filed the only `captured` row
  from such a change and all 84 real captures as `besiegers_left` (re-derived: about 85 captured of 207 rows, README:289); the fixed rule counts `BySiege` to the besieger's own faction, and a TAOM reader should filter the same way. TAOM's reading, not
  checked against `FiefGranting`: code that reads "who captured" inside `OnSiegeEventEnded` would still see the old owner.
- **Garrison recruiting gates** (README:203): moved to [settlement-economy-food-prosperity.md](settlement-economy-food-prosperity.md) "Clan wage payment and garrison recruitment", beside TAOM's own reading of the same code.
- **Listeners run newest first.** README:205: "the probe's listeners run before the game's (newest first)". TAOM verified:
  `MbEvent.AddNonSerializedListener` puts the new record at the head of the list (v1.5.5 `MbEvent.cs`:24-30; v1.4.8 the
  same; the v1.5.6 file is not in the partial cache) [Certain]. A listener added after the engine's own therefore runs
  before it on the same event.

A negative result from yotthani's side: a cache for `DefaultDiplomacyModel.GetScoreOfDeclaringWar` saved nothing, at about 12
us a call. The numbers and the shadow test are in [diplomacy.md](../../features/diplomacy.md) "A war-score cache was
rejected". yotthani's engine-team write-up (headed Bannerlord 1.5.5, while it says "Most timings were taken on 1.5.3 and 1.5.4" and "Not every number was re-measured on 1.5.5") states it as "only ~12 µs per call; a cache saved nothing worth
having", and adds that "Periodic campaign ticks are already spread well; the remaining outliers are rare single events
(20-60 ms)" (MithrilForge `docs/engine/for-taleworlds/bannerlord-engine-findings.html`@d09f44a4, section "What we checked
and found no problem in"; the scope is the author's save and mod list).

### The campaign band: 150 days, TAOM 2.0.34, Bannerlord 1.5.5 [yotthani]

Measured by yotthani's unattended CampaignProbe on TAOM 2.0.34, Bannerlord v1.5.5.124170, first culture `empire`, speed 15,
150 days (`taom-baseline-2026-10-18.md`:10, 14). Each row is a mean over the runs named; the run counts differ by table.

| Measure at day 150 unless stated | Value | Runs | Source line |
|---|---|---:|---|
| Free men per 100 evil men | 46.7 (s 3.2, range 42.0 to 51.0) | 8 | `taom-baseline-2026-10-18.md`:28 |
| Fortresses free / evil / neutral | 57.1 / 119.6 / 44.2 | 8 | `:38` |
| Conquests by siege, days 1 to 150 | 78.6 (s 7.5, 67 to 90) | 8 | `:44` |
| Lord-against-lord field battles a day | 9.5 (s 0.7) | 8 | `:45` |
| All battles started a day, bandits included | 102.1 (s 2.3) | 8 | `:46` |
| Gold spent on upgrades, free / evil / neutral (M) | 11.6 / 12.9 / 3.0 | 8 | `:73` |
| Sieges begun a run | 192.9 | 22 (8 of 18 Oct, 14 of 21 Oct) | `campaign-positions.md`:47, runs named at :9 |
| Sieges with any relief contact | 60.8 % (60.7 % in 16 runs) | 8, 16 | `relief-joiners-measured.md`:29 |
| Real sallies a run, garrison beaten in | 11.8, 51 % | 8 | `campaign.md`:774 |

The source's caveats, kept: a single run is not comparable (one outlier run reached 59.9 men per 100 evil, "far outside the 8 shipped
night runs", `app-0.2-acceptance.md`:13); the 16-run conquest mean is 78.4 (`relief-joiners-measured.md`:33). The source also records
that the 8 runs finished by themselves, "8 of 8 attempts, no crash, no stall", at 5.4 s per campaign day, 863 to 869 s a run
(`taom-baseline-2026-10-18.md`:14).

**Do not use the band as a regression target for 2.0.36 without a re-run.** TAOM commit 2410aec51 ("no wars before the War of
the Ring", 2026-10-18 17:07 -0500) set every start relationship to peace and vetoes AI wars before phase 1 on day 30
[Certain: `git log`, and `Main/_Module/ModuleData/spkingdoms.xslt` at 0c5d81408, read by the verifier]. The 8 baseline runs
started 18.10. 01:41 to 03:38 by the source's log (time zone not stated), before that commit. The 22-run row adds 14 runs of 21 October, and the 16-run figures are those 14 plus two of 20 October (`campaign-positions.md`:9, `relief-joiners-measured.md`:7); the source does not say whether those builds held the commit. TAOM's inference [Likely]: they did not, because the same 22 runs show the game's Shaghana and Umbar war declared at a median day 17 (`campaign.md`:1097), and since 2410aec51 no AI war starts before day 30. Conquests, sieges, relief contact and the men ratio all
depend on the wars, so they may differ on 2.0.36. Bannerlord 1.5.6 is a second reason.

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

- [docs/ai-includes/orientation.md](../../ai-includes/orientation.md)
- [docs/features/butter-lib-distance-matrix.md](../../features/butter-lib-distance-matrix.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)
- [docs/reference/engine/campaignevents-and-campaignbehavior.md](./campaignevents-and-campaignbehavior.md)
- [docs/reference/engine/issue-and-quest-system.md](./issue-and-quest-system.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](./settlement-economy-food-prosperity.md)

<!-- backlinks-end -->
