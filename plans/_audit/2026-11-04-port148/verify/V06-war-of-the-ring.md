# V06 verify: War of the Ring ends on day 4 with a Free win and a centred bar

Run 2026-11-04-port148. Role: adversarial, read-only verifier of the WOTR lens
(`plans/_audit/2026-11-04-port148/deepdive/wotr.md`). Nothing was built, tested or edited; the only file
written is this one.

Refs used: 1.4.x tip `origin/bannerlord-1.4.5` = 1b00d1acd; public build `origin/fix/public-armory-ids-145`
= 8df90444c (c5b84fb41 plus 92a30ba42, 10a52fb3d, 8df90444c); working tree = c5b84fb41; 1.5.x tip
`origin/bannerlord-1.5.x` = e45f6df2c. Engine: v1.4.8 dump `C:\Decompiled_Bannerlord\_categories_v1.4.8`.
Below, `CS\` abbreviates `C:\Decompiled_Bannerlord\_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem\`.

## Bottom line

1. On the 1.4.x line and the public build, the only on-screen "Free Peoples won" signal is the "End of the
   War" inquiry, and it needs the MCM option "Enable Victory (End the War)" switched ON. That option
   defaults to off and has done so since it was introduced (601a55c57, 2026-07-04); no older `TAOM.json`
   can carry `true`, because the property did not exist before that commit.
2. With victory on, a Free decision while the bar sits at its centre has only two code routes: an empty
   Evil roster (fits every symptom, but no shipped data or code empties ten Evil realms in four quiet days)
   or an absolute momentum lead that the ratio bar does not show (documented runaway momentum). Which one
   fired is UNVERIFIABLE without the reporter's `taom_debug` log; one log line settles it (section 4.4).
3. WOTR-01 and WOTR-02 are real defects with ready 1.5.x fixes, and those fixes chain-apply cleanly onto
   1.4.x (read-only `git apply --check`, section 4.1). But WOTR-01 does not by itself stop battles or freeze
   the bar: the lens overstated its link to the report.
4. Verdict on the lens: PARTLY-CONFIRMED. All six requested claims hold at the code level; four need
   corrections (section 5).

## 0. What players run

`git diff --stat` between c5b84fb41, 1b00d1acd and 8df90444c over `Main/Features/WarOfTheRingMomentum`,
`Main/Features/Diplomacy`, `Main/Features/Execution`, `Main/Features/TaomSettings.cs`, `Main/SubModule.cs`,
`Main/IoC.cs`, the momentum/diplomacy/execution ModuleData and the MomentumView prefabs is empty, except
three unrelated adapter files (CultureTroopIdReader, IObjectManagerAdapter, ObjectManagerAdapter) on the tip
and `VolunteerRecruitmentService.Gondor.cs` on the public build. So working-tree line numbers below are
also the 1.4.x tip's and the public build's. The kingdom files `spkingdoms.xslt` and `taom_spkingdoms.xml`
have identical blob ids on all three refs.

## 1. Claim verdicts

| Claim | Verdict | Evidence (refs as above) |
|---|---|---|
| WOTR-01 Full War never declares its Hostile-pair wars | CONFIRMED (defect); PARTLY (impact) | `WarOfTheRingService.cs:131` flips the phase before `:139-143`; both declaration loops guard on `AreAtWar` (`:152`, `:170-171`), which is `Kingdom.IsAtWarWith` (`AllianceAdapter.cs:47`); `TaomDiplomacyModel.cs:26-36` makes every Hostile pair constant war in FullWar; `CS\TaleWorlds.CampaignSystem\FactionManager.cs:150` answers "at war" on constant war before reading the stance. Phase 2 `wars` is empty (`war_of_the_ring.json:14`), so zero wars are declared. Impact correction below. |
| WOTR-02 process singleton carries FullWar or WarEnded into a second campaign | CONFIRMED | `DiplomacyIoC.cs:17` singleton; `IoC.Configure()` once in `OnSubModuleLoad` (`SubModule.cs:114`); behavior built `new` per campaign (`SubModule.cs:981`) but its only reset zeroes its own ints (`WarOfTheRingBehavior.cs:33-37`); new game never loads `SyncData` (`CS\TaleWorlds.CampaignSystem\Campaign.cs:1420-1430` vs `:1694-1695`); listeners run newest-first (`CS\TaleWorlds.CampaignSystem\MbEvent.cs:24-42`, generic `:100-111`; insertion order `CS\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignBehaviorManager.cs:32-38`); first save persists the phase (`WarOfTheRingBehavior.cs:48-53`). |
| WOTR-03 empty-roster tie goes to Free | CONFIRMED (logic); trigger not found | `MomentumVictoryService.cs:56` tests `Evil.KingdomIds.Count == 0` before the Evil branch at `:58`; unchanged on the 1.5.x tip (`:56-58`). |
| WOTR-04 absolute lead hidden by the ratio meter | CONFIRMED | decision on `(Free - Evil) / 100` (`MomentumWarState.cs:24`, `MomentumVictoryService.cs:52-57`); bar is a ratio of the positive parts (`MomentumQueryService.cs:40-47`); popup Total is the same bounded ratio (`docs/features/war-of-the-ring-momentum.md:79`); the doc itself warns "an enabled threshold-victory fires almost immediately once the player has ~5 events" (`:172`). Gate credit is realm-wide (`WarEventSnapshotAdapter.cs:64-66,152-160`), win or lose (`MomentumEventService.cs:75-76`). |
| WOTR-06 legacy MCM phase days 2 and 14 | CONFIRMED | defaults 30/45 (16f7f4e93, 2026-03-25), 2/14 (f4ee462ee, 2026-05-22), 30/44 (e273cd23f, 2026-07-30) under unchanged names; all ancestors of 8df90444c. The v2.0.15 release note told players "Existing players keep the old days" (`docs/releases/v2.0.15-discord.md:19-20`). The logged pair is JSON only (`WarOfTheRingConfigProvider.cs:39`); the service uses MCM when present (`WarOfTheRingService.cs:116-125`). |
| 601a55c57 `MomentumVictoryEnabled` default false | CONFIRMED | `git log -S MomentumVictoryEnabled` finds only 601a55c57; it adds `= false` (`TaomSettings.cs:229` today) and `"victoryEnabled": false` (`momentum.json:3`) in the same commit. The feature itself landed the day before (3408c2d94, 2026-07-03). |

### WOTR-01: what the defect does and does not do (v1.4.8)

- Does: no `DeclareWarAction`, no war notice, no history entry, and the stored stance stays whatever it was
  (Neutral for pairs at peace). The at-war cache `Kingdom._factionsAtWarWith` is rebuilt only by
  `UpdateFactionsAtWarWith` (`CS\TaleWorlds.CampaignSystem\Kingdom.cs:503-520`), whose callers are stance
  changes (`FactionManager.cs:94-98,112-116,136-140,203-207`) and vanilla's full refresh on new game, load,
  kingdom creation, clan creation and any clan changing kingdom
  (`CS\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignFactionManagerBehaviour.cs:9-50`). Until then the
  stale cache gates siege and raid targets (`CS\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiMilitaryBehavior.cs:166-174`),
  patrols (`AiPatrollingBehavior.cs:117`) and army creation (`CS\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManagementCalculationModel.cs:123`).
- Does not: stop battles. AI engagement scans nearby parties with the live `IsAtWarWith`
  (`AiEngagePartyBehavior.cs:73-81`), which already includes constant war.
- The window is short in practice: every kingdom change, mercenary contracts included, raises
  `OnClanChangedKingdom` (`CS\TaleWorlds.CampaignSystem.Actions\ChangeKingdomAction.cs:161`, called for
  `JoinAsMercenary`/`LeaveAsMercenary` at `:195-203`), which triggers the full refresh. Its length in a live
  TAOM campaign is UNVERIFIED.
- It cannot keep the bar at zero on a normal day-44 transition: the Phase 1 wars (`isengard` and `empire`
  against `vlandia`, `war_of_the_ring.json:6-7`) are real declarations made at day 30, all three realms
  enrol at Full War, and their battles, raids and armies feed momentum.

## 2. Scenario reconstruction

### 2.1 Conditions every Free-win popup needs (1.4.x code)

1. Momentum on (`MomentumEnabled`, default true, `TaomSettings.cs:219`); solo play is authority.
2. Victory on: `CheckAndApplyVictory` returns `None` at its first statement otherwise
   (`MomentumVictoryService.cs:41-42`). MCM path `Mcm?.MomentumVictoryEnabled ?? Config.VictoryEnabled`
   (`MomentumSettingsProvider.cs:27`).
3. War started and not ended (`:44-45`): the phase reached FullWar and a sweep enrolled at least one realm
   on EITHER side (`MomentumEnrollmentService.cs:35,66-71`).
4. Player gate: `RequireParticipationForVictory` off, or `MinimumPlayerEventsForVictory` reached (default 5,
   slider 0-20; `TaomSettings.cs:244,247-249`; `PlayerMomentumService.cs:38-41`).
5. `internalMomentum >= threshold` (threshold at least 1 via `Math.Max(1, ...)` on the MCM path,
   `MomentumSettingsProvider.cs:29-30`; slider 100-2000, default 500, `TaomSettings.cs:232-234`; JSON path
   validated to [1, 1000000], `MomentumConfigProvider.cs:69-74`) or `Evil.KingdomIds.Count == 0`.

At zero momentum the threshold branch is false (`0 >= 1` is false), so a decision at a truly centred bar
needs the Evil roster empty. `OnDailyTick` runs the sweep and the victory check in the same tick
(`WarOfTheRingMomentumBehavior.cs:205-209`).

### 2.2 Is there a Free-win display that does not need the victory option?

No. The only Free-win text is `taom_wotr_victory_free` (`taom_wotr_strings.xml:34`), used once, in
`ShowVictoryInquiry` (`WarOfTheRingMomentumBehavior.cs:324-341`), which is called only when
`CheckAndApplyVictory` returns an outcome (`:209-212`, `:314-317`). The bar and popup carry no victory
binding (no `Victor`/`Victory` in `Main/Features/WarOfTheRingMomentum/UI` or the MomentumView prefab). The
two silent endings show nothing: the load reconcile `EndWar` (`:177-181`, itself behind the victory option)
and the WOTR-02 `WarEnded` carry-over. War Chronicle, Long Defeat and Eye of Sauron are feature folders new
on 1.5.x (`features_new_on_15x.txt`); 1.4.x has only `WarOfTheRingMomentum`.

### 2.3 Scenario matrix

Kingdom facts used: the public build defines 22 kingdoms (8 rewritten by `spkingdoms.xslt`, 14 in
`taom_spkingdoms.xml`, parsed as XML so comments are excluded). All 22 ids are keys in `alignment.json`:
10 Evil (`empire`, `empire_s`, `aserai`, `khuzait`, `isengard`, `gundabad`, `dolguldur`, `goblin`,
`mistymountainorcs`, `bluecraig`), 8 Free, 4 Neutral. This laptop's TAOM_Map (v2.0.27) ships an empty
`<Kingdoms/>` stub, so the realm set comes from TAOM itself. No TAOM code destroys a kingdom (grep for
`DestroyKingdomAction`, `IsEliminated =`); v1.4.8 discontinues a realm only when it loses its last
settlement to an owner change (`CS\TaleWorlds.CampaignSystem.CampaignBehaviors\FactionDiscontinuationCampaignBehavior.cs:24-39,67-75`),
not on a timer.

| # | Scenario and code path | Settings and state required | Fit (day 4 / no battle / centred / Free) | Likelihood |
|---|---|---|---|---|
| S0 | Defaults, any campaign: `MomentumVictoryService.cs:41-42` returns `None` forever | none | no Free win possible | certain: the report cannot happen on defaults |
| S1 | Elimination: `:56` with an empty Evil roster, via the daily tick (`WarOfTheRingMomentumBehavior.cs:205-212`) or a `KingdomDestroyed` (`:311-317`) | victory ON, gate open (off, 0, or 5 events), all 10 Evil realms absent from the roster | all four (the bar never moves: with Evil empty every Evil event returns early, `MomentumEventService.cs:49,92,115,132`) | very low: needs a broken install or world; none found in shipped data |
| S2 | Threshold, fresh war, new campaign in a fresh process | victory ON; Full War early through Test Mode (transition on the day-3 tick, momentum war from the next tick because the momentum handler runs first) or custom days; or legacy 2/14 (Full War day 14) | Free yes; "day 4" only as "4 days of war"; a centred bar contradicts a lead of 100 to 500 internal points built in days | low |
| S3 | WOTR-02 second campaign in the same process after a campaign at Full War: Full War from day 0, vanilla at-war caches built under constant war at `OnNewGameCreated`, momentum war at session launch | victory ON; the player starts in a kingdom (E3 shows the player in `aserai` at day 4), so realm AI lords fill the gate (WOTR-04) | Free and day 4 plausible; centred only approximately with a lowered threshold (for example 100: 60,000 against 50,000 store units reads 9 of 100); "no battle" only as "none I fought" | low to moderate among players who enable victory and do not restart |
| S4 | Victory switched on mid-campaign in a long endless war: huge balanced totals keep the ratio near centre while the absolute lead already exceeds the threshold; persisted player events open the gate | victory ON, long campaign | centred, no battle needed, Free; "4 days" only as the delay after switching it on | plausible; documented as a known limitation (`docs/features/war-of-the-ring-momentum.md:172`) |
| S5 | WOTR-05 carry-over: load of a save with no momentum record after another campaign in one process keeps that campaign's rosters, momentum and player events (`CS\TaleWorlds.CampaignSystem\CampaignBehaviorDataStore.cs:86-105`; store resets only at `WarOfTheRingMomentumBehavior.cs:81`) | victory ON, a pre-2026-07-03 save | any | very low |
| S6 | Hand-edited threshold below the slider minimum: `Math.Max(1, ...)` admits 1 to 99 | edited `TAOM.json`; whether MCM clamps an out-of-range saved int on load is UNVERIFIED | centred with a tiny lead | very low |

By player type: a new campaign in a fresh process with current defaults cannot reach Full War before day
44, and cannot end it at all unless victory is on. A second campaign in the same process after a Full War
campaign starts in Full War (S3); after a campaign whose war ended it never gets a war and shows no popup
(`WarOfTheRingService.cs:75`, `MomentumEnrollmentService.cs:35`). An old `TAOM.json` saved between
2026-05-22 and 2026-07-30 moves Full War to day 14, never to day 4, and cannot switch victory on.

Ranking (all need victory ON): (1) a threshold decision on an absolute lead the bar hides, after S4 or
after an early war (S3, S2), with realm-wide gate credit; (2) S1, the only exact fit, with no trigger;
(3) S5 or S6.

## 3. E3 and E4: every War of the Ring and momentum line

E3 (`taom_debug_2026-11-01_20-00-00.log`, 223 lines):

- 7 `Loaded 24 kingdom alignments` (`AlignmentConfigProvider.cs:35`) and 8 `AlignmentService: Loaded 24 kingdom alignments` (`AlignmentService.cs:29`): the shipped 24-key map loaded, so all 22 realms resolve.
- 10 `Loaded War of the Ring config: Phase1 day 30, Phase2 day 44`: JSON values only.
- 80 `War of the Ring: Phase restored from save → Peace`, 81 `War of the Ring: Outcome restored from save → None`.
- 89 `[WarOfTheRing] WarOfTheRingBehavior registering events`.
- 148 `[WarOfTheRing] Session launched — phase restored at day 4`.

E4 (`taom_debug_2026-11-01_20-16-56.log`, 5164 lines): the same lines at 7, 8, 10, 78, 79, 87 and 147,
plus 2490 `[DEBUG] [WarOfTheRing] Peace allowed: sturgia <-> vlandia` (`PeaceActionHook.cs:37`, a Free-Free
peace allowed outside Full War).

What they show:

- Phase days: the session-launch check ran at elapsed day 4 and logged no transition. E4's AutoResolve
  records span absolute days 91081 (33 records), 91082 (132) and 91083 (60). The v1.4.8 start is
  1084 x 84 + 21 days + 9 hours = day 91077.375 (`CS\TaleWorlds.CampaignSystem.GameComponents\DefaultCampaignTimeModel.cs:7`,
  vanilla 84-day year), so two daily checks ran at elapsed 4.6 and 5.6 with no
  `War of the Ring: Transitioning to IsengardWar`. For this save the effective Phase 1 day is at least 6
  (given War of the Ring enabled): neither Test Mode (1/3) nor the legacy 2/14 pair is active. The clock
  arithmetic also shows no time-model jump (WOTR-10) in this save.
- Enrollment: zero `[Momentum]` lines, so no sweep enrolled anything, consistent with Peace. No
  `MomentumConfigProvider:` line either: that provider is lazy (`MomentumConfigProvider.cs:29`) and with MCM
  present nothing reads it before the war starts (`MomentumSettingsProvider.cs:25-41`), consistent with
  MCMv5 v5.12.1.0 loaded (E3 line 54-55).
- These sessions (save003, Marzuk, kingdom `aserai`) are not a War of the Ring end. The report has no log.

## 4. Ports, hardening, diagnostics, workaround

### 4.1 Port list in order (draft WP 113 "war-of-the-ring-fixes")

Dry-run: `git format-patch --stdout -1 <sha> -- <paths> | git apply --check -` against the working tree,
which equals the 1.4.x tip for every path involved (none is among the 52 drifted files). Chaining all six
code and test patches below in this order: 29 file diffs, exit 0.

| Step | Commit | Take | Dry-run on 1.4.x | Notes |
|---|---|---|---|---|
| 0 | a0db0558b (file only) | `TAOM.Tests/Infrastructure/RepoPaths.cs` (`RepoPath`, `ReadSource`, `StripComments`) | new file | absent on the 1.4.x tip; `WarOfTheRingWiringTests` needs it; do not cherry-pick 35007dbb8 |
| 1 | 2719e59e8 (#764) | `ResetForNewSession` in the per-campaign constructor, remove the `OnNewGameCreated` listener, SubModule comment, session-reset, service and wiring tests, RCA, lessons | code and tests clean (SubModule hunk at offset -200) | bookkeeping conflicts only |
| 2 | 9ab3952a7 | rule amendment in `.claude/rules/csharp-architecture.md` | clean | the 1.4.x rule still prescribes the `OnSessionLaunched` reset |
| 3 | d1b9fd91a (#772) | `HasDeclaredWar`, stance-guarded `DeclareWar`, `ReconcileDeclaredWars` on host session launch, config pair validation, tests incl. `CoopVetoClassificationTests` row | clean after step 1; alone it fails on `IWarOfTheRingService.cs`, `WarOfTheRingBehavior.cs`, `WarOfTheRingBehaviorSessionResetTests.cs` | v1.4.8 re-check done: link typed War under constant war (`CS\TaleWorlds.CampaignSystem.GameComponents\DefaultDiplomacyModel.cs:1082-1089`, `FactionManager.cs:78-100`); ally call-in filter `!IsAtWarWith` (`CS\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:685-710`); `IsWarAllowed` (`DiplomacyService.cs:113-120`) vetoes none of the 61 Hostile pairs (60 Free-Evil, 1 with a Neutral, 0 same side) |
| 4 | cf7486cba | doc hunks for #764/#772 only | docs | leave the Gundabad/Arthedain pair, War Chronicle rows and counts |
| 5 | 16a34027d (momentum half) | `Main/Core/Persistence/SyncDataChunker.cs`, momentum behavior and cheats, delete `MomentumSyncChunker.cs`, chunker and SyncData tests | clean (8 files) | the three edited files are byte-identical on both sides; leave all WarChronicle files; re-cite the 32,763-byte limit to v1.4.8 |
| 6 | ec2a3026f (momentum half) | `CapturesSettlement` filter and `WarOfTheRingMomentumSiegeGateTests` | clean | `BlockadeSallyOutBattle` exists in v1.4.8 (`CS\TaleWorlds.CampaignSystem.MapEvents\MapEvent.cs:36`); `SiegeCompleted` fires for a SiegeOutside attacker win (`:2088-2093`) and only Siege, SallyOut, BlockadeSallyOutBattle transfer (`CS\TaleWorlds.CampaignSystem\KingdomManager.cs:238-241`). The `WarEventSnapshotAdapter.cs` hunk fails: it needs 7f3636b2e's `IPlayerControlQuery` (WP 131) or a MainParty-side check |
| 7 | 429713746 (WotR part) | rename `Phase1TriggerDay`/`Phase2TriggerDay` to `WarPhase1TriggerDay`/`WarPhase2TriggerDay`, keep 30/44 | `TaomSettingsProvider.cs` clean; `TaomSettings.cs` fails on unrelated drift (hand-apply); `McmRenamedDefaultsTests.cs` clean as a new file | land rename and provider together or the build breaks; trim the test to the renames applied; on the tip all 14 old properties exist and 13 already carry the intended default, `ChargeAutoKnockdownWeightRatio` is 8 against 6 (a balance call, not this WP); leave the ServerSettings test edits |
| opt | 3fe86e22b | the three lessons only, each re-checked on v1.4.8 | not run | 2410aec51 itself stays OUT |

No port needed: 5e43518df's momentum hunk delegates to `IAlignmentService.ResolveSide`, which 1.4.x
already has with the same semantics (`AlignmentService.cs:36-43`).

### 4.2 Hardening 1.4.x needs that 1.5.x lacks (new code, TDD)

All four gaps are present on the 1.5.x tip e45f6df2c (`MomentumVictoryService.cs:56-58`,
`MomentumEventService.cs:197-205`, `KingdomStrengthAdapter.cs:19`, `WarOfTheRingMomentumIoC.cs:26`).

- H1 empty roster (WOTR-03): in `MomentumVictoryService.cs:56-59` elimination requires the other side
  non-empty and both-empty returns `None`. Optional and a maintainer decision: start the war only when both
  sides have a realm (`MomentumEnrollmentService.cs:66`), so a side that never enrolled cannot be
  "eliminated"; trade-off, a side destroyed before Full War then means no war instead of a Free win.
- H2 non-finite strength (WOTR-08): gate `loserSideStrength` as a positive requirement with
  `FiniteFloatValidator` (`MomentumEventService.cs:197-206`), compute `InternalMomentum` without int
  overflow (`MomentumWarState.cs:24`), refuse non-finite or negative event values
  (`MomentumSideData.cs:53-70`); one NaN test per cast (house rule).
- H3 #773 (open; WOTR-05): build `WarOfTheRingMomentumBehavior` with `new` per campaign, reset the store in
  its constructor, delete the `OnNewGameCreated` listener (`:81`), drop the container registration
  (`WarOfTheRingMomentumIoC.cs:24`); the store stays a singleton (UI, query, cheats). SubModule and IoC are
  single-owner files.
- H4 diagnostics: extend `[Momentum] War of the Ring decided` (`MomentumVictoryService.cs:64`) with roster
  sizes, the branch taken and the gate inputs; print the effective phase days and their source (MCM, JSON,
  Test Mode) on session launch (`WarOfTheRingBehavior.cs:77`).
- H5 decision, not a fix: realm-wide gate credit is deliberate on both lines (#375 comment,
  `WarEventSnapshotAdapter.cs:88-95`; ec2a3026f keeps it), so correct the MCM hint
  (`TaomSettings.cs:243`, "fought ... yourself") or change the rule, by maintainer choice.

### 4.3 How to test, and STOP conditions

- Build and test (no CI on 1.4.x since ccef172ad): `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=`,
  then `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~WarOfTheRing|FullyQualifiedName~Momentum|FullyQualifiedName~SyncDataChunker|FullyQualifiedName~McmRenamedDefaults|FullyQualifiedName~CoopVeto"`,
  then the full suite against the 1b00d1acd baseline (9150 passed, 3 skipped).
- In game: (a) load a Full War save, return to the menu, start a new campaign: expect
  `War of the Ring: phase reset to Peace at campaign start; a load restores the saved phase next` and no
  `[Momentum] ... enrolled` line before `War of the Ring: Transitioning to FullWar`; (b) Test Mode on: on
  the Full War tick expect `War of the Ring: <a> declares war on <b> (hostile tier)` lines and notices;
  (c) load an old Full War save: expect `War of the Ring: declared N missing Full War wars (#772)` once.
- STOP if: any `had no effect (unknown kingdom, or vetoed by IsWarAllowed)` warning names a shipped pair; a
  ruler player gets a burst of call-to-war popups at the transition (count them and report); the wiring test
  fails to compile without RepoPaths; the BindingVerification tests fail; any step needs a balance change.

### 4.4 What a player sends

The newest `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_<timestamp>.log` from the session where the
popup appeared (only 10 are kept, `FileLogger.cs:26,28`), plus
`Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/TAOM/` (`docs/modding/configs-factions-and-world.md:368`).
The deciding line:

`[Momentum] War of the Ring decided: FreeVictory (momentum X, threshold Y)` (`MomentumVictoryService.cs:64`)

X at or above Y: threshold route (S2 to S4). X below Y, usually 0: elimination (S1); then expect no
`[Momentum] Peace: <free> ↔ <evil>` lines (`:85`) and look for `[Momentum] <id> removed from the war (kingdom destroyed)`
(`MomentumEnrollmentService.cs:80`, also printed by the prune). Roster history:
`[Momentum] <id> enrolled on the Evil side` (`:58`; 10 expected). `[Momentum] The War of the Ring momentum tracking has begun`
(`:69`) with no `War of the Ring: Transitioning to FullWar` earlier in that campaign means WOTR-02. A popup
with no "decided" line is impossible on this code.

### 4.5 Player workaround

- For an endless war, leave "Enable Victory (End the War)" off (the default).
- If the war already ended: load a save from before the popup and switch the option off first. Switching it
  off afterwards only un-freezes the bar (`WarOfTheRingMomentumBehavior.cs:163-173`); the phase stays
  `WarEnded`, so peace is no longer blocked and no realm re-enrols.
- Quit to desktop before starting a new campaign after playing a campaign that reached Full War or ended.
- After Full War begins, save and reload once (rebuilds every realm's war list,
  `CampaignFactionManagerBehaviour.cs:26-28`).
- If the war comes at day 2/14, set the Phase 1 and Phase 2 start days to 30 and 44 (deleting the TAOM
  ModSettings folder resets every TAOM option).

## 5. Corrections to the lens

1. WOTR-01 does not explain "no battle" or a centred bar: engagement uses the live check, Phase 1 wars are
   real, and any mercenary contract change refreshes the cache. Keep it as a correctness port.
2. The Evil roster is 10 realms, not 7 (`goblin`, `mistymountainorcs`, `bluecraig` were missed).
3. Side resolution is loaded from `alignment.json` (`AlignmentConfigProvider.cs:21-43`), not a static map
   in code; E3/E4 prove the map loads (24 entries).
4. WOTR-02's bar appears at the first daily tick, not the first map frame: `OnGameLoadFinished` fires only
   on a load (`C:\Decompiled_Bannerlord\_categories_v1.4.8\Modules\SandBox\Sandbox\SandBoxGameManager.cs:123-177`).
5. WOTR-04's suggested gate restriction contradicts the recorded design (H5); the hint text is the defect.
6. WOTR-04 is the documented S4 mechanism (doc `:172`), which the lens did not cite; it ranks above S1.

## 6. Open questions

- The reporter's log and MCM file; what "4 Days" counts from (campaign start, Full War, enabling victory).
- Whether MCM clamps an out-of-range saved integer on load (S6).
- What BetterTime patches (not installed here; WOTR-10 stays UNVERIFIED).
- How long the WOTR-01 stale window lasts in a live TAOM campaign.

## 7. Recommended skills (for the orchestrator)

`/investigate` when a reporter log arrives; `/research` before H1 and H2; `/deep-review` then `/verify` on
the WP 113 port; `/humanizer` on the release note that tells players about the phase-day rename.
