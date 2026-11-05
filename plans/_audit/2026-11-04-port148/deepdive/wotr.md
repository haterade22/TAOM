# Deep dive: War of the Ring ends early (day 4, no battles, Free Kingdoms win)

Run id: 2026-11-04-port148. Lens: WOTR. Read-only investigation of the 1.4.x line (working tree = c5b84fb41;
the 52 drifted files read from origin/bannerlord-1.4.5), the v1.4.8 engine, and the 1.5.x line as a source of
leads and fixes.

Player report (brief fact 7): "ended for me after 4 Days. In that time no battle was fought and the indicator
stayed in its starting position and caused a win for the Free Kingdoms." E3/E4 log "Loaded War of the Ring
config: Phase1 day 30, Phase2 day 44".

## Findings (appended as found)

### Scope facts established first

- No WarOfTheRingMomentum or Diplomacy war file is among the 52 drifted files, and the 3 public-build
  commits (92a30ba42, 10a52fb3d, 8df90444c) touch troop data only. The war code players run equals the
  working tree at c5b84fb41.
- The ONLY player-facing "Free Peoples won" signal is `ShowVictoryInquiry(FreeVictory)` at
  `Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs:324-341` (working tree). Its only
  callers are `OnDailyTick` (`:209-216`) and `OnKingdomDestroyed` (`:314-319`), both through
  `MomentumVictoryService.CheckAndApplyVictory` (`MomentumVictoryService.cs:38-75`). No cheat ends the
  war (`Cheats/MomentumCheats.cs` is read-only).
- Every way the war can end on the 1.4.x line:
  1. Daily tick victory check (`WarOfTheRingMomentumBehavior.cs:209`), shows the inquiry.
  2. Kingdom-destroyed victory check (`:314`), shows the inquiry.
  3. Session-launch load-reconcile `EndWar(state.Victor)` (`:177-181`): no inquiry, needs a save whose
     momentum state already says ended.
  4. Process-singleton carry-over of `WarOfTheRingService.CurrentPhase == WarEnded` into a second
     campaign (see WOTR-02): the war is over before it begins, no inquiry.
- `CheckAndApplyVictory` returns FreeVictory only when ALL hold: `VictoryEnabled` (MCM
  `MomentumVictoryEnabled`, default false, `Main/Features/TaomSettings.cs:229`; JSON fallback false,
  `Main/_Module/ModuleData/momentum/momentum.json:3`; the default has been false since the property
  was added in 601a55c57, 2026-07-04), war started and not ended, the player gate
  (`RequireParticipationForVictory` default true and `MinimumPlayerEventsForVictory` default 5,
  `TaomSettings.cs:244,249`; player events are recorded only for valid battles and captures that the
  snapshot marks player-involved, win or lose, `MomentumEventService.cs:75-76,99-103`), and then
  `internalMomentum >= threshold || state.Evil.KingdomIds.Count == 0` (`MomentumVictoryService.cs:56`).
- The map meter is a RATIO, `(free - evil) * 100 / (free + evil)` over the positive parts, and reads 0
  (centre, "starting position") whenever both sides are at zero (`MomentumQueryService.cs:40-47`). The
  prefab binds `ValueInt="@Momentum"` (`Main/_Module/GUI/PreFabs/MomentumView/MomentumMapIndicator.xml:17`)
  and v1.4.8 `SliderWidget` moves the handle on value change regardless of `IsEnabled`
  (`C:\Decompiled_Bannerlord\_categories_v1.4.8\UI\TaleWorlds.GauntletUI\TaleWorlds.GauntletUI.BaseTypes\SliderWidget.cs:139-176,420-425`),
  so a frozen meter is not the explanation.
- With momentum at zero (meter centred), a Free win therefore needs the ELIMINATION branch: an empty
  Evil roster. Momentum cannot go negative in normal flow (every event value is >= 0 and
  `MomentumSideData.AddEvent` keeps trimmed contributions, `MomentumSideData.cs:53-70`).
- The E3/E4 Harad player (save003, character Marzuk) is NOT showing a War of the Ring end: both
  sessions restore `Peace`/`None` and launch "at day 4" (E3 lines 80-81, 148; E4 lines 78-79, 147), with
  no `[Momentum]` line in either log. The WOTR report has no log in hand, so the root cause below is
  derived from code.

### WOTR-01: Full War never really declares its Hostile-pair wars (no new battles, meter frozen at centre)

- **Severity:** P1 (every 1.4.x campaign that reaches Full War). **Confidence:** high (code + v1.4.8 engine).
- **Evidence (working tree c5b84fb41):** `Main/Features/Diplomacy/WarOfTheRingService.cs:131` sets
  `CurrentPhase = newPhase` BEFORE `DeclareConfiguredWars`/`DeclareHostileTierWars` (`:139-143`); both
  guard on `_allianceAdapter.AreAtWar` (`:152`, `:170-171`), and `AllianceAdapter.DeclareWar` guards
  again on `kingdomA.IsAtWarWith(kingdomB)` (`Main/Adapters/AllianceAdapter.cs:47,56`).
  `TaomDiplomacyModel.IsAtConstantWar` returns true for every Hostile pair once the phase is FullWar
  and `blockPeaceBetweenHostileTiers` is on (`Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs:26-35`;
  shipped `true` at `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json:13`).
- **Engine (v1.4.8 dump):** `Kingdom.IsAtWarWith` calls `FactionManager.IsAtWarAgainstFaction`, which
  returns true as soon as `DiplomacyModel.IsAtConstantWar` does
  (`C:\Decompiled_Bannerlord\_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:652-655`,
  `...\FactionManager.cs:144-160`). So every Phase 2 declaration is skipped: no `DeclareWarAction`, no
  `OnWarDeclared`, no war notice, and the stored stance stays Neutral. AI siege and raid target selection
  (`AiMilitaryBehavior.FindBestTargetAndItsValueForFaction`,
  `...\CampaignBehaviors.AiBehaviors\AiMilitaryBehavior.cs:117,166`) walks the cached `FactionsAtWarWith`,
  which is rebuilt only on a stance change, a load, a new game, a clan or realm creation, or any clan
  changing realm (`...\FactionManager.cs:96-139,205-207`; `...\CampaignBehaviors\CampaignFactionManagerBehaviour.cs:7-50`).
- **Mechanism:** at the day-44 transition the momentum war enrolls every realm and the meter appears,
  but the new Hostile-pair wars exist only "virtually" until the next cache refresh. AI armies do not
  pick the new enemies' towns and villages as targets, so momentum stays at 0 and the ratio meter sits
  at centre. This matches "no
  battle was fought and the indicator stayed in its starting position". How long the window lasts
  depends on the next refresh trigger (a reload, or any clan changing realm such as a mercenary
  contract); its length in a live TAOM campaign is UNVERIFIED.
- **Fix direction:** port d1b9fd91a: guard each declaration on the stored stance (`HasDeclaredWar`,
  `GetStanceWith(...)?.IsAtWar`), keep the phase flip first, add `ReconcileDeclaredWars()` on host
  session launch to repair saves already in Full War, validate the `wars` lists. Re-check every v1.5.5
  line reference in that commit against v1.4.8 (the `GetStanceWith` link-creation side effect, the ally
  call-in in `AllianceCampaignBehavior.OnWarDeclared`).
- **1.5.x:** d1b9fd91a (fix, #772; RCA `docs/reviews/rca-wotr-phase2-wars-2026-10-08.md` on that ref),
  docs cf7486cba. Shared code, IN scope.
- **Verify:** port the 1.5.x test whose `AreAtWar` follows the phase; in game, reach Full War (MCM Test
  Mode puts phase 2 at day 3) and confirm "declares war" notices and `War of the Ring: X declares war on
  Y (hostile tier)` log lines on the transition day.

### WOTR-02: The War of the Ring phase is a process singleton that a second campaign inherits

- **Severity:** P1. **Confidence:** high (code + v1.4.8 event order).
- **Evidence:** `IWarOfTheRingService` is `Reuse.Singleton` (`Main/Features/Diplomacy/DiplomacyIoC.cs:17`);
  the container is built once in `OnSubModuleLoad` (`Main/SubModule.cs:114`), while the behaviors are
  added per campaign from `OnGameStart` (`Main/SubModule.cs:727`, then `RegisterDiplomacyAndConflict`
  `:965-985`). `WarOfTheRingBehavior`'s only reset is an `OnNewGameCreated` listener that zeroes the
  behavior's OWN two ints (`Main/Features/Diplomacy/WarOfTheRingBehavior.cs:33-37`), never the service.
  A new game never loads `SyncData`, so `SetPhaseFromSave` (`:55-59`) never runs and `CurrentPhase` and
  `Outcome` keep the previous campaign's values (`WarOfTheRingService.cs:17-19`).
- **Engine (v1.4.8):** a new game runs `RegisterEvents`, then dispatches `OnNewGameCreated`, then
  `OnSessionStart` (`...\TaleWorlds.CampaignSystem\Campaign.cs:1583-1585,1624,1694-1695`). Campaign
  event listeners run newest-first (`...\TaleWorlds.CampaignSystem\MbEvent.cs:24-42`), so the momentum
  behavior (added after the WotR behavior, `SubModule.cs:981,985`) runs `SweepEnrollment` first; vanilla
  `CampaignFactionManagerBehaviour.OnNewGameCreated` also caches every realm's at-war list through the
  stale `IsAtConstantWar` (`CampaignFactionManagerBehaviour.cs:9,20-23,38-50`).
- **Mechanism:** after a campaign that reached Full War, a new campaign in the same game process starts
  IN Full War: `SweepEnrollment` passes its phase gate (`MomentumEnrollmentService.cs:35`), enrolls every
  realm and starts the momentum war on day 0 (`:66-71`); the meter is up from the first map frame; the
  Phase 1 and Phase 2 declarations never run because `CheckPhaseTransition` has nothing to transition
  (`WarOfTheRingService.cs:80-88`). After a campaign whose war ENDED, the new campaign is `WarEnded`
  from the start and never gets a war (`:75`). The first save persists the inherited phase
  (`WarOfTheRingBehavior.cs:46-53`). On shipped settings (Test Mode off, phase days 30/44), and short of
  a mid-campaign time-model change (WOTR-10), this is the only code path that puts a live War of the
  Ring meter in front of a player within the first 4 days.
- **Fix direction:** port 2719e59e8: `ResetForNewSession()` on the service, called from the
  `WarOfTheRingBehavior` constructor (built with `new` per campaign, before every campaign event and
  before `LoadBehaviorData`), plus the `WarOfTheRingWiringTests` source pin. Port the rule amendment
  9ab3952a7 (reset in the per-campaign constructor, never in `OnSessionLaunched`).
- **1.5.x:** 2719e59e8 (fix, #764; RCA `docs/reviews/rca-wotr-session-reset-2026-10-08.md`), 9ab3952a7
  (rule), cf7486cba (docs). IN scope.
- **Verify:** port `WarOfTheRingBehaviorSessionResetTests`; in game, load a day-45+ save, go back to the
  main menu, start a new campaign, and check the log shows no `[Momentum] ... enrolled` line before
  `War of the Ring: Transitioning to FullWar`.

### WOTR-03: A Free win at zero momentum is only reachable through the elimination branch, and an empty-roster tie goes to Free

- **Severity:** P2 (needs MCM victory on). **Confidence:** high for the logic, LOW for the trigger.
- **Evidence:** `MomentumVictoryService.cs:56-59`: `internalMomentum >= threshold || Evil.KingdomIds.Count == 0`
  is tested BEFORE the Evil branch, so a state with both rosters empty resolves as FreeVictory. With an
  empty Evil roster, every event handler early-returns for an Evil faction
  (`MomentumEventService.cs:49,92,115,132`), so momentum and the meter stay at 0: exactly the reported
  picture.
- **What could empty the Evil roster: not found.** The shipped roster is 7 Evil realms (`empire` =
  Dunland, `empire_s` = Mordor, `isengard`, `gundabad`, `dolguldur`, `khuzait` = Rhun, `aserai` = Harad;
  `Main/_Module/ModuleData/execution/alignment.json:4-24`, names from `Main/_Module/ModuleData/spkingdoms.xslt`).
  Side resolution is a static id map (`Main/Features/Execution/AlignmentService.cs:11-51`, one
  registration at `ExecutionIoC.cs:11`). No TAOM code destroys realms, and the v1.4.8 destroy paths
  need a realm to lose its last settlement or its ruling clan
  (`...\CampaignBehaviors\FactionDiscontinuationCampaignBehavior.cs:24-87`,
  `...\CampaignBehaviors\ClanVariablesCampaignBehavior.cs:204-260`, `...\Actions\KillCharacterAction.cs:74`).
  The trigger is UNVERIFIED; the summary names the log line that would settle it.
- **Fix direction:** none on 1.5.x. Make elimination require that the OTHER side still holds realms
  (`Free.Count > 0 && Evil.Count == 0`), return None when both are empty, and log both roster sizes in
  the "decided" line.
- **Verify:** unit tests: both rosters empty gives None; Evil empty with Free non-empty gives FreeVictory
  only when victory and the gate pass.

### WOTR-04: Once victory is on, the war is decided by a number the meter never shows, and the "player participation" gate counts AI lords

- **Severity:** P2. **Confidence:** high (code), the player-visible effect is inferred.
- **Evidence:** the decision compares the ABSOLUTE lead `(Free - Evil) / 100` with the threshold
  (`MomentumWarState.cs:24`, `MomentumVictoryService.cs:52-57`); the meter shows a RATIO over the
  positive parts (`MomentumQueryService.cs:40-47`), so a 500 lead on 5,000 against 4,500 reads 5 of 100,
  a hair off centre, at the moment the war ends. `PlayerInvolved` is the main party OR any owned party
  whose map faction is the player's realm (`Main/Adapters/WarEventSnapshotAdapter.cs:64-66,96,152-161`),
  and `ProcessBattle` records a player event whether the player's side won or lost
  (`MomentumEventService.cs:75-76`). The MCM hint promises the opposite: "the war cannot end until you
  have fought in enough momentum events yourself" (`Main/Features/TaomSettings.cs:242-244`). The 1.4.x
  doc already records that an enabled threshold victory "fires almost immediately once the player has
  ~5 events" (`docs/features/war-of-the-ring-momentum.md:172`).
- **Mechanism:** a vassal or ruler who turns on "Enable Victory" gets his 5 gate credits from his realm's
  AI lords, and the war can end within days of Full War without him fighting, with the bar near the
  middle. This is the best explanation of the report IF the player's "no battle" means "no battle I
  fought" and "starting position" means "near the middle".
- **Fix direction:** none on 1.5.x (ec2a3026f and #966 widen siege credit further; nothing fixes the
  mismatch). Show the decisive lead against the threshold in the popup when victory is on; restrict the
  gate to the player's own party or its map-event side (the `HasPlayerParty` side test from ec2a3026f);
  do the bounded-momentum rebalance the doc asks for before anyone is encouraged to enable victory.
- **Verify:** unit tests on `IsPlayerRelated` for an AI lord of the player's realm; an in-game run with
  victory on, gate 5, the player parked in a town, and the log's `[Momentum] War of the Ring decided`
  line.

### WOTR-05: The momentum state survives into a loaded campaign whose save has no momentum record (#773)

- **Severity:** P3 (needs a pre-2026-07-03 save). **Confidence:** high (code + v1.4.8 engine).
- **Evidence:** v1.4.8 `CampaignBehaviorDataStore.LoadBehaviorData` calls `SyncData` only when the save
  holds a record for the behavior (`...\TaleWorlds.CampaignSystem\CampaignBehaviorDataStore.cs:86-105`);
  a missing key leaves the ref unchanged (`:26-39`). The momentum behavior and store are container
  singletons (`Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumIoC.cs:14,24`) and the store resets
  only on `OnNewGameCreated` (`WarOfTheRingMomentumBehavior.cs:81`, `MomentumStateStore.cs:42-46`).
- **Mechanism:** after any campaign in the same process, loading a save made before the momentum feature
  existed keeps the previous campaign's rosters, momentum, ended flags and player events; with victory
  on, `OnSessionLaunched` can `EndWar(state.Victor)` with the old victor (`:177-181`), or the first daily
  tick can decide this campaign's war from the other campaign's numbers.
- **Fix direction:** build the momentum behavior with `new` per campaign and reset the store in its
  constructor (the 2719e59e8 pattern).
- **1.5.x:** none. #773 was filed by the 2719e59e8 RCA and is still open; 1.5.x keeps the singleton
  (`Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumIoC.cs:26` on that ref).
- **Verify:** a behavior test that loads a record-less store after a populated one and expects fresh state.

### WOTR-06: The logged phase days are not the effective ones; old MCM files run Phase 1 at day 2 and Full War at day 14

- **Severity:** P2 (timeline and diagnostics). **Confidence:** high (code + git history).
- **Evidence:** the "Loaded War of the Ring config: Phase1 day 30, Phase2 day 44" line prints the JSON
  (`Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs:39`), but the service takes Test Mode (1/3) or
  the MCM pair whenever MCM is present (`WarOfTheRingService.cs:114-126`, `TaomSettingsProvider.cs:5-9`;
  E3/E4 confirm MCMv5 v5.12.1.0 is loaded, E3 lines 54-55). The MCM defaults moved under unchanged names:
  30/45 (16f7f4e93, 2026-03-25), 2/14 (f4ee462ee, 2026-05-22), 30/44 (e273cd23f, 2026-07-30), and MCM
  loads a saved value by property name (the reason given in 1.5.x 429713746).
- **Mechanism:** a player whose `TAOM.json` was saved between 2026-05-22 and 2026-07-30 gets the war at
  day 2 and day 14 while the log reads 30/44. Phase days alone still cannot bring Full War to day 4
  without Test Mode, a custom value, or WOTR-02. `Session launched — phase restored at day N`
  (`WarOfTheRingBehavior.cs:77`) prints the clock but not the days it is compared against.
- **Fix direction:** port 429713746 (#1017): the renamed MCM properties and the `McmRenamedDefaultsTests`
  gate. It renames 14 settings across several features; check each default against the 1.4.x line
  before porting, and leave out the ServerSettings test edits (co-op feature new on 1.5.x). Add the
  effective phase days and their source (MCM, JSON, Test Mode) to the session-launch log line.
- **Verify:** `McmRenamedDefaultsTests`; a log from a player with an old `TAOM.json`.

### WOTR-07: A relief-force win outside the walls scores a full siege capture

- **Severity:** P3 (momentum correctness, not the zero-momentum trigger). **Confidence:** high.
- **Evidence:** v1.4.8 raises `SiegeCompleted(..., isWin: true, _mapEventType)` for an `IsSiegeOutside`
  attacker victory (`...\TaleWorlds.CampaignSystem.MapEvents\MapEvent.cs:2088-2093`); vanilla transfers
  the settlement only for `SallyOut`, `Siege` and `BlockadeSallyOutBattle`
  (`...\TaleWorlds.CampaignSystem\KingdomManager.cs:238-241`). TAOM filters on `isWin` alone
  (`WarOfTheRingMomentumBehavior.cs:258`), so it adds `siegeMomentum` 400 (x1.5 when player-related) and a
  "Settlements Captured" stat for a town nobody took.
- **Fix direction:** port the `CapturesSettlement` filter from ec2a3026f. Review its `PlayerFought` siege
  credit separately (it also edits Career, Enlistment and LotrIssues, all present on 1.4.x).
- **1.5.x:** ec2a3026f (#1015). Shared code, IN scope.
- **Verify:** port `WarOfTheRingMomentumSiegeGateTests`.

### WOTR-08: A non-finite realm strength would cast to int.MinValue and decide the war either way

- **Severity:** P3 (latent). **Confidence:** medium (math verified; trigger UNVERIFIED).
- **Evidence:** `KingdomStrengthAdapter.cs:19` returns `Kingdom.CurrentTotalStrength` unvalidated (v1.4.8 sums
  party `EstimatedStrength` from `MilitaryPowerModel.GetPowerOfParty`, `...\Clan.cs:699-708`,
  `...\Party\PartyBase.cs:857-901`). `CalculateBattleMomentum` guards only `== 0f`, then
  `(int)Math.Round(...)` (`MomentumEventService.cs:197-206`); a NaN there is `int.MinValue` on net472/x64
  (house rule, `.claude/rules/csharp-architecture.md` "float to int CASTS"). `SideMomentum += int.MinValue`
  and the int subtraction in `InternalMomentum` (`MomentumWarState.cs:24`) can overflow, and the meter
  clamps negatives to 0 (`MomentumQueryService.cs:40-41`), so the bar can sit at centre while the
  threshold test fires for either side.
- **Trigger likelihood:** low. TAOM's power model reads bounded MCM values and validated JSON
  (`Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs:19-55`,
  `BattleBalanceConfigProvider.cs:66,82`).
- **Fix direction:** none on 1.5.x (same code at `MomentumEventService.cs:197-205` on that ref). Gate the
  strength with `FiniteFloatValidator` as a positive requirement, compute `InternalMomentum` in `long`,
  and refuse a non-finite or negative event value.
- **Verify:** one NaN test per cast, per the house rule.

### WOTR-09: A corrupt chunk count throws out of the momentum SyncData

- **Severity:** P3 (corrupt saves only). **Confidence:** high.
- **Evidence:** `WarOfTheRingMomentumBehavior.cs:109-117` passes the saved count straight to
  `new List<string>(count)`: a negative count throws during `LoadBehaviorData`, an absurd one allocates.
- **Fix direction:** port the momentum half of 16a34027d (`SyncDataChunker.SyncChunks`: count below 0 or
  above 4,096 ignored with a warning, state loads fresh); its War Chronicle half is OUT (feature new on
  1.5.x).
- **1.5.x:** 16a34027d. IN scope as a robustness guard.
- **Verify:** port `WarOfTheRingMomentumBehaviorSyncDataTests`.

### WOTR-10: The phase clock trusts a start time the engine recomputes; a time-model change mid-campaign moves every phase day

- **Severity:** P3. **Confidence:** medium (engine verified; BetterTime UNVERIFIED).
- **Evidence:** the phase machine reads `CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow` on every
  call (`WarOfTheRingBehavior.cs:75,89`). In v1.4.8 that start time is rebuilt on each read from calendar
  statics (`...\GameComponents\DefaultCampaignTimeModel.cs:7`), which `CampaignTime.Initialize()` sets once
  per game start from the active time model (`...\TaleWorlds.CampaignSystem\CampaignTime.cs:178-199`,
  called at `Campaign.cs:1394`); the map clock itself is seeded once at new game (`Campaign.cs:1402`) and
  saved. The week and season lengths follow `Options.AccelerationMode` (`DefaultCampaignTimeModel.cs:23-45`),
  whose only writer is TaleWorlds' optional FastMode module (`C:\Decompiled_Bannerlord\_modules_build\FastMode__TaleWorlds.CampaignSystem.FastMode.cs:54`),
  not in any player's mod list. Vanilla uses the same idiom (`Campaign.cs:787`, `SaveHandler.cs:197`,
  `KingdomDecisionProposalBehavior.cs:69`), so TAOM is not worse than the engine here.
- **Mechanism:** if a mod that replaces the time model or its calendar units is added or removed after a
  campaign starts, the computed start jumps and the elapsed-day count jumps with it; a large positive
  jump runs Peace, IsengardWar and FullWar inside one `CheckPhaseTransition` call on the next session
  launch (`WarOfTheRingService.cs:80-88`), putting the meter up at once. E3/E4 show a sane clock (day 4
  at session launch, lines 148/147) and no BetterTime in that save's module list (E3 line 26); E1/E2 load
  BetterTime last (rgl_log_22112 line 29). What BetterTime patches is UNVERIFIED: it is not installed on
  this laptop.
- **Fix direction:** none on 1.5.x (5ba8d6c82 guards a broken clock in the War Chronicle only, a feature
  new on 1.5.x, so OUT). If it is ever needed, persist the campaign's own start ticks in the WotR record
  and log the effective day pair beside the clock (see WOTR-06).
- **Verify:** decompile `BetterTime.dll` for a `CampaignTimeModel` subclass or patches on `CampaignTime`,
  `MapTimeTracker` or `DefaultCampaignTimeModel`.

## Summary

**Bottom line.** Nothing in the 1.4.x code ends the War of the Ring on a timer. Every "Free Peoples won"
outcome goes through `MomentumVictoryService.CheckAndApplyVictory`, and that needs the player to have
switched MCM "Enable Victory (End the War)" on (default off since the property was added). The code
fully explains two of the three symptoms with defects that 1.5.x has already fixed: Full War begins
with none of its wars really declared, so no new battles happen and the meter stays at centre (WOTR-01,
port d1b9fd91a); and a second campaign in one game session inherits the War of the Ring phase, which
puts a live meter in front of the player from day 0 (WOTR-02, port 2719e59e8 and the rule 9ab3952a7).
The exact trigger of the Free decision is not provable without the reporter's log.

**Q1, every path that ends the war.** Daily-tick victory (`WarOfTheRingMomentumBehavior.cs:209`),
kingdom-destroyed victory (`:314`), session-launch reconcile (`:177-181`, no popup) and the
singleton carry-over of `WarEnded` into a new campaign (WOTR-02, no popup). The config is Phase 1 day 30
and Phase 2 day 44 from JSON, but MCM, Test Mode (days 1/3) or a pre-2026-07-30 saved MCM pair (days
2/14) can override it while the log still prints 30/44 (WOTR-06). The war has no start clock and no
timeout; event decay runs on absolute campaign hours. With zero momentum, the only Free route is the
elimination branch with an empty Evil roster (WOTR-03); if "zero" is approximate, the threshold branch
fires on an absolute lead that the ratio meter barely shows, with the participation gate filled by the
player's own realm's AI lords (WOTR-04).

**Q2.** No start time is captured as default or zero; the phase clock is the engine's own idiom
(WOTR-10). Load resets: WOTR-02 (phase) and WOTR-05 (momentum, old saves only, #773 still open on
1.5.x). BetterTime: UNVERIFIED (not installed here). Division by zero is guarded; a NaN realm strength
is not (WOTR-08, latent). Default winner: an empty-on-both-sides roster resolves as Free (WOTR-03).

**Q3, 1.5.x commits.** Port (shared fixes): d1b9fd91a (#772), 2719e59e8 (#764), 9ab3952a7 (rule),
cf7486cba (docs), ec2a3026f (`CapturesSettlement` filter, plus its siege-credit change after review,
WOTR-07), 16a34027d (momentum count guard only, WOTR-09), 429713746 (#1017, after checking each of the
14 defaults on 1.4.x, WOTR-06). No port needed: 5e43518df's momentum hunk is a refactor with the same
behavior (its alignment fixes to other features belong to another lens; its minor-clan changes are
content). OUT (1.5.x-only features or structure): 84f5e63a9, 7e8451555, bad32ad08 (War Chronicle and
the momentum award; their `GetEffectivePhaseDay` accessor could serve the WOTR-06 log line), 2410aec51
(a new rule, no wars before the War of the Ring), b02467744 (Arthedain content), a15563a3c (TAOM.Client
move), and the four task-named commits 5ba8d6c82, 5479109bf, 5f80f95c9, f0b5eddf9 (all War Chronicle).

**Q4, ranked hypotheses** (all need "Enable Victory" on):
1. Threshold victory under WOTR-04, after Full War arrived via the normal transition or WOTR-02: fits
   "4 days" and "Free win"; fits "centred meter" and "no battle" only loosely.
2. Elimination with an empty Evil roster (WOTR-03): fits all three symptoms exactly, but no shipped data
   or code path empties seven Evil realms in four days.
3. A non-finite strength cast (WOTR-08) or foreign state from a record-less load (WOTR-05): possible,
   unlikely.

**What tells them apart:** the reporter's `taom_debug` log for the session where the war ended. The line
`[Momentum] War of the Ring decided: FreeVictory (momentum X, threshold Y)` (`MomentumVictoryService.cs:64`)
settles it: X at or above Y means 1, X near 0 means 2, X in the millions means the cast in 3. No
`[Momentum] Peace:` lines after it (`:85`) means the Evil roster was empty. `[Momentum] ... enrolled on the
Evil side` and `removed from the war` (`MomentumEnrollmentService.cs:58,80`) give the roster history.
`[Momentum] The War of the Ring momentum tracking has begun` (`:69`) with no `War of the Ring:
Transitioning to FullWar` (`WarOfTheRingService.cs:130`) in that campaign means WOTR-02. No `declares war
on ... (hostile tier)` lines (`:173`) on the transition day confirms WOTR-01. Beyond the log: the
reporter's MCM settings for TAOM (Id and folder `TAOM`, `TaomSettings.cs:13-16`) and the save's momentum
record (`tools/repair_sav_strings.py <save>` diagnoses without writing; whether it or `tools/inspect_sav.py`
prints the momentum JSON is UNVERIFIED).

**Player workarounds (until the ports land).** Leave "Enable Victory (End the War)" off for an endless
war. Quit to desktop before starting a new campaign after playing or loading one that reached Full
War. Once Full War begins, save and reload once: the load rebuilds every realm's war list
(`CampaignFactionManagerBehaviour.cs:25-28`), so AI armies go after the new enemies. If Full War comes at
day 14, set the MCM Phase 1 and Phase 2 start days back to 30 and 44.

**Recommended skills for the orchestrator:** `/research` before porting d1b9fd91a (its engine notes cite
v1.5.5 lines), `/investigate` once a reporter log arrives, `/deep-review` on the port.

