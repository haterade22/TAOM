# Field Camp

Pitch a visible camp on the campaign map (#506): field and fortified camps with a morale bonus and
grain foraging, terrain-gated ambushes and lookouts, a timed raise with an on-map progress bar, and
authored 3D encampment visuals. Port of the commissioned yotthani FieldCamp module (Bannerlord
1.4.5, behaviourally ported 2026-08-22); provenance:
[provenance-register.md](../reference/provenance-register.md) "Yotthani commissioned modules".

## Architecture

```
Make Camp button (TaomFieldCampOverlay, MapView + GauntletLayer, never IsFocusLayer)
   -> ICampMenuActivationQuery guards -> IGameMenuAdapter.Activate("taom_field_camp_menu")
taom_field_camp_menu (choose type) / taom_fc_camp (manage: forage, fortify, supplies, break)
   -> ICampService state machine (camp book, SyncData "_taomFieldCamps")
        terrain policy  -> ICampTerrainService (pure; every 1.4.8 TerrainType explicitly mapped)
        ambush odds     -> ICampAmbushService (pure)
        visuals         -> ICampVisualService (core null; TAOM.Client CampVisualService) -> CampLayoutBuilder (tpac prefab, vanilla fallback)
        supplies        -> ISupplyOrderService.CancelCampOrders on break; SupplyOrderScreens.Open from menu
        lookout range   -> LookoutSpottingContributor -> TaomMapVisibilityModel contributor seam
        nameplate icon  -> Patch74 postfix (PatchShield-excluded namespace: never-throw body)
```

Camp raise progress derives from `EstablishedAt + BuildHours` (nothing stored), so mid-build saves
resume exactly. Ambush and lookout raise in a quarter of the configured hours; fortifying a ready
field camp starts a real second raise at double the setup hours. The raise completing announces
`{=taom_fcamp_established}`; breaking announces `{=taom_fcamp_broken}`.

The port's FieldCamp localization keys use the `taom_fcamp_` prefix (the camp work, rest healing and start-in-camp keys of #860 use
`taom_camp_`, a different prefix, so they cannot collide with either that or FieldCommission's). The port initially claimed
`taom_fc_`, which was already FieldCommission's registered prefix (10 colliding ids, round-B
dataflow finding); the 58 keys were renamed mechanically, same suffixes. The game-menu and
menu-option ids (`taom_fc_camp`, `taom_fc_ambush`, ...) are NOT localization keys and keep the
short prefix.

## Key decisions

- **The lookout does NOT register a MapVisibilityModel.** The engine holds one slot and
  CareerSystem's `TaomMapVisibilityModel` owns it; the +20% + LEADER's Scouting/200 sight bonus
  (leader-based per the source, consistent with every other camp skill read) rides the
  `IPartySpottingContributor` seam added for exactly this collision. The contributor returns 0
  while the master toggle is off.
- **Ambush checks are throttled** to a half-hour game-time accumulator with a straight-line
  distance prefilter. The source ran pathfinding per hostile per frame while an ambush was armed.
  The prefilter runs INSIDE the campaign-boundary enumeration, before any candidate allocation,
  and party names are never rendered during the scan; only the winning candidate's name resolves
  (lazily, from its engine handle) when the inquiry is shown (round-B scan-cost finding).
- **The ambush payoff is the source's battle flow**: on trigger the camp is spent and a two-button
  inquiry offers the strike (`{=taom_fcamp_ambush_ok}` on a successful roll,
  `{=taom_fcamp_ambush_fail}` when spotted, either way the player may attack). Confirming starts a
  real battle via `StartBattleAction.ApplyStartBattle`, guarded on BOTH parties being free of a
  map event; only a successful roll softens the target first (recent-events morale halved +
  disorganized). No new scan runs while the inquiry is open. Both map-context inquiries carry the
  source's `pauseGameActiveState: true` (ambush also `prioritize: true`, its `ShowInquiry(data,
  true, true)`): without the pause the world kept marching under the modal (round-B fidelity
  finding).
- **The trigger chance uses the PLAYER's spotting range** (source formula): a wider sight radius
  erodes the concealment term. `ICampAmbushService.TriggerChance(playerSpottingRange, ...)` is
  named for it so nobody re-reads the first argument as a candidate distance. In forest the player's sight is shorter
  ([under-the-eaves.md](under-the-eaves.md)), so the ambush reach narrows and the chance rises (day at Scouting 0: reach 10 to 8.4,
  chance 0.50 to 0.548; night: 6 to 4.2, 0.62 to 0.674).
- **Fortify is a real second raise** (source behaviour): double the setup hours, effects paused
  until the palisade stands. The one fix kept over the source: the foraging flag, accumulator and
  tally survive the upgrade instead of being silently wiped.
- **Terrain tables are the source's, decoded exactly.** Its compiled relative switches were
  decoded against the real 1.4.8 `TerrainType` values (identical to its 1.4.5 target). Every
  member is explicitly classified with default-deny arms, so a future engine terrain cannot
  silently allow an ambush or inherit the source's 0.5 forage default unreviewed. One oddity kept
  for parity: terrain the source never named (Cliff, the restriction faces) forages at its 0.5
  default.
- **Move-away handling**: while camped, a move order holds the party and asks to break camp
  (confirm inquiry with a reentry latch). The FULL order is captured before the hold
  (settlement / engage / escort target + navigation type, not just the point), so "break camp and
  move" still enters the town or presses the attack instead of walking to a stale position.
- **Session reset**: the camp book lives in a process-lifetime singleton and SyncData only runs
  when a save record exists, so the behavior tracks whether a LOADING SyncData ran and calls
  `ICampService.ResetForNewSession()` from OnSessionLaunched / OnGameLoaded otherwise (fresh
  campaign, or a pre-feature save). The reset latches `_syncedThisSession` so a stray later call
  cannot wipe a camp pitched right after launch (the Refuge/SupplyLines shape). `LoadFrom` also
  clears every transient (ambush scan clock, move latch, inquiry latch) and drops null book rows,
  logging once.
- **SyncData branches on direction** (the SupplyLines shape): the save half is SaveInto + SyncData
  only, because `LoadFrom` has load-only semantics and running it on every autosave wiped live
  latches and the scan clock (round-B). The load half nulls the local first, because the engine's
  `BehaviorSaveData.SyncData` leaves the ref UNCHANGED on a missing key; pre-seeding from the live
  singleton would silently keep the previous session's book on a key miss (Codex round-2 #2). A
  missing key therefore loads as an empty book.
- **Player capture breaks the camp cleanly** (visuals removed, camp-placed supply orders
  cancelled) from either tick, before any other camp work.

## Configuration

MCM group **Field Camps** (GroupOrder 46): master toggle, `CampSetupHours` (4),
`CampMoralePerHour` (1, x2 fortified), `CampForagePerTroopFactor` (0.1), `CampMaxAmbushRange` (10),
`CampBaseAmbushChance` (0.5), `CampMinTownDistance` (10, ambush/lookout exempt),
`CampFortifiedUpgradeCost` (500), and the three camp work knobs `CampActivityHours` (10), `CampActivityYieldPerTenMen` (10) and `CampActivityYieldCap` (30), `CampRestHealRegularsPerDay` (15), `CampRestHealHeroesPerDay` (19) and `CampStartInCamp` (off). All validated in `CampSettingsProvider` (which takes the MCM object once and reads
through it, since `Enabled` is read every campaign frame while a camp stands, #746); all coop
simulation-relevant except the camp work, rest healing and start-in-camp knobs, which are player-local (the work is single-player only). Master toggle off hides the button and menus and stops the gameplay effects
(morale, forage, ambush scans, the lookout sight bonus); the state-PROTECTING paths stay live so a
standing camp is never trapped: the settlement-entry fold, the captivity break, and the move guard
(whose inquiry is the remaining way to break the camp while the button is hidden) all keep
running.

## Assets

`Main/_Module/AssetPackages/fieldcamp_camp_a.tpac` + `fieldcamp_palisade_ring.tpac`: the FIRST
AssetPackages content in this repo (commissioned art, provenance-cleared). `CopyModule` deploys
`_Module` verbatim, so no csproj change was needed. `CampLayoutBuilder` falls back to the source's
procedural vanilla-mesh layouts (siege-camp tents, barricade rings) when a mesh is missing, so a
stripped install degrades instead of breaking; the fallback is exercised by renaming the tpac away
(smoke checklist).

**Known limitation (2026-09-29): the prefabs render nowhere, so every camp shows the fallback.** The four packages
(these two and Refuge's `refuge_camp_a` and `refuge_palisade_ring`) were built outside the Modding Kit with yotthani's
MithrilForge. The editor-built releases ship only `pack0.tpac`, which holds none of them, and the dev install loads
TAOM's loose `Assets/` tree instead of `AssetPackages/`. Where they should live is an open decision
([tpac-static-prop-authoring.md](../reference/tpac-static-prop-authoring.md)). The in-game check is the debug log:
`[FieldCamp] placed 1 Field camp entities` means the prefab stood; a larger count means the fallback.
`python -m unittest tools.tests.test_prefab_asset_packages` keeps the packages and the code's prefab names in step.

## Key files

| File | Purpose |
|---|---|
| `Domain/CampState.cs` + `FieldCampSaveDefiner.cs` | Persisted camp record; definer base 726901101 |
| `CampService.cs` | State machine; campaign statics behind protected virtuals (CampServiceTests). Also declares `SettlementSite` and `FortificationSearch`, the nearest town-or-castle rule RefugeService shares for its keep-outs (FortificationSearchTests) |
| `CampTerrainService.cs` / `CampAmbushService.cs` | Pure policy and odds |
| `Client/Features/FieldCamp/CampVisualService.cs` + `Visuals/CampLayoutBuilder.cs` | Entities, wind ticker (500 ms throttle), fallback chain. TAOM.Client (ADR-013); core registers `NullCampVisualService`, whose `Show` reports not ready |
| `LookoutSpottingContributor.cs` | The sight bonus through the model seam (its cap member returns 0; Far Watch's scout is the second contributor, see [far-watch.md](far-watch.md) "The seam"; Under the Eaves' forest penalty is the third, see [under-the-eaves.md](under-the-eaves.md)) |
| `Hooks/FieldCampCampaignBehavior.cs` + `FieldCampMenuController.cs` | Events, SyncData, menus (index 4 on both menus reserved for Refuge; index 5 on the camp sub-menu is shared by Host Review, Far Watch and camp work, all before Leave: the camp sub-menu's options end up Forage, Fortify, Supplies, Break, Refuge, Review the host, Send a scout, Set the men to work, Leave, see [host-review.md](host-review.md), [far-watch.md](far-watch.md) and "Camp work" below) |
| `Activities/` | Camp work, rest healing and start in camp (#860): `CampActivityRules` (pure numbers and the one hunting table), `CampActivityService` (decisions), `ICampWorkAdapter` / `CampWorkAdapter` (engine reads and writes), `CampWorkTypes.cs` (the six value types), `CampRestHealing` (when a party rests, and the one `Grant` the healing model asks), `CampStartInCamp` (the new-campaign hook), `CampActivitiesModule`, and the shared `CoopSessionPolicy.IsSinglePlayerAuthority` that the service, `CampRestHealing` and `CampStartInCamp` all call; `Activities/Hooks/` holds the behavior, the camp option and pick menu, the wait menu, the landing and the texts |
| `Hooks/PartyNameplateCampIconPatch.cs` | Patch74 thin guarded body (registry entry has the never-throw contract) |
| `Hooks/CampNameplateIconPresenter.cs` | Patch74's widget half: icon creation, sprite memo (same never-throw posture) |
| `UI/FieldCampOverlayVM.cs` (core) + `Client/Features/FieldCamp/UI/FieldCampMapView.cs` (TAOM.Client) | Overlay button + status panel, 4 Hz refresh. The behavior attaches the view through `IFieldCampOverlayAdapter` (`FieldCampCampaignBehavior.Overlay`, set by `FieldCampIoC.InitializePatchStatics` after the client composition; core registers a null adapter) |
| `Main/_Module/GUI/PreFabs/FieldCamp/TaomFieldCampOverlay.xml` | Ported prefab; brushes are grep-verified vanilla (`Popup.Description.Text`, `Popup.Button.Text`, `Popup.Frame`, `ButtonBrush2`); the source's root `Frame1.Broken` was a phantom too (only `.Left`/`.Right` exist), swapped for the real `Popup.Frame` in round C |

## Menus and time: establish lands on the map, the camp sub-menu is a WAIT menu (field-tested 2026-08-23/25)

A standard game menu stops campaign time unconditionally and deadens the time controls (vanilla
behavior, not a bug), and MapState persists the open menu id into the save. The source module
switched into its camp sub-menu right after establishing, which showed "Raising 0%" in a panel
where the raise could never progress; the very first field camp ever established read as a
frozen game, and saving there made every load resume "frozen" too. Two deliberate deviations
from the source close that class:

- **Establish exits to the map** (`FieldCampMenuController.Establish`): the overlay narrates the
  raise and Camp Options reopens the menu on demand. Verified in-game 2026-08-25: the first camp
  on the fix build raised to completion on a new campaign.
- **The camp sub-menu is a wait menu** (`AddWaitGameMenu`, `WaitMenuHideProgressAndHoursOption`;
  the Enlistment service menu is the precedent): time runs while the panel is open, and the tick
  re-renders the raise percent and forage grain. The forage toggle refreshes in place through
  `IGameMenuAdapter.RefreshCurrent()` (MenuContext.Refresh) instead of SwitchTo(same id), which
  would re-initialise the wait. Break camp exits to the map. Leave stays the only isLeave option
  because the engine calls EndWait BEFORE an isLeave consequence (the Enlistment trap).
- **The wait menu's condition delegate is `true`** (#567, 2026-09-12).
  `GameMenu.GetMenuOptionConditionsHold` ANDs that delegate with every option's own condition,
  Leave included: it is a visibility gate over the whole panel, not a menu-validity hook, and
  nothing exits the menu when it turns false. The first wait-menu version gated it on
  `PlayerCamp`. Founding a refuge (an option inserted on this same menu) breaks the camp and
  returns here from the garrison deposit screen, so the panel rendered zero options and no exit,
  and `MapState` persisted the open menu into the save (every release from v2.0.24 to v2.0.28).
  Any action that removes the camp from inside the sub-menu must also land on the map, as Break
  camp and refuge founding now do; the `true` condition is the belt that keeps Leave visible if
  one ever does not. `FieldCampWiringTests` forbids gating the condition on the camp.

Field diagnostics that came out of the incident (DevConsole feature, taom.* commands):
`taom.time_status` dumps every state that can freeze campaign time (menu context, time lock,
engine pause, waiting flag, encounter); `taom.rescue_time` exits a stuck menu context, releases
the lock, unpauses the engine and force-resumes time. Both live in
`Main/Features/DevConsole/Cheats/TimeControlCheats.cs`.

## Camp work (#860)

**What the player sees.** On a raised field or fortified camp, the camp panel has "Set the men to work". It opens a plain menu with
three choices: hunt for game, fell timber, fish the waters. A choice that cannot run is shown greyed with the reason as its tooltip
(no game on this ground, only a forest has timber, no water close enough, the party is short of tools, and so on). Choosing one opens a
timed wait menu with a progress bar and one button, "Stop work". When the bar fills the men come home, the goods go into the party's
stores, a few lines in the message log say what was brought in, and the player lands on the campaign map. While a work runs, the camp
panel's option reads "Return to the work" and opens the wait menu again; it is greyed with a reason whenever a global block holds
(a shared campaign, an encounter, a camp raised again after Fortify, a Far Watch ride out) and hidden while the master toggle is off.

**Rules.**

- **What counts as a work camp:** a ready Field or Fortified camp. An ambush or lookout camp stays small and hidden and never works.
- **Goods:** hunting brings in meat, timber brings in hardwood (forest only), fishing brings in fish. Yield is `min(cap, round(perTen
  x healthy men / 10 x ground factor))`, defaults 10 per ten men and a cap of 30. The ground factor for hunting is 1.0 in forest, 0.7 on
  plain, steppe and swamp, 0.5 on mountain, canyon, snow and farmland (`RuralArea`, farmland round a village: some game, thinner than open country),
  0.2 in desert and dune, and 0 on water, shores, crossings and anything the table does not name. Fishing needs water within 4 map
  units (sampled at the eight compass points at 2 and 4 units) and then counts as 1.0 on any ground. Every terrain the engine defines
  is named in `CampActivityRules`, and a guard test walks the enum.
- **Men and tools:** every healthy man in the party works. Up to ten men work with their own hands; above ten, one tool per ten men
  started is needed. The party must hold them when the work starts (the choices grey otherwise) and they are used up when it ends, up
  to what is still held. Meat and fish are food; hardwood and tools are the vanilla trade goods.
- **Morale is a cost:** a long work wears on the men. At the end the party loses one morale per full ten men above ten, less ten
  percent per clan tier, never below 0 (a tier-0 clan with 60 men loses 5). The log line reads "The long work wears on the men
  (morale -N)". Ten men or fewer lose nothing. It is never a gain.
- **Injury:** one roll when the work passes its midpoint, a 10 percent chance. A hit wounds about 5 percent of the healthy regulars,
  at least one man. A hit draws evenly from all the healthy regulars (the engine's own random draw) and never from a hero. A saved
  flag stops a second roll after a reload.
- **Stop work discards the progress** and pays nothing. A broken camp takes the work with it, since the work lives in the camp record.
- **No gold.** Nothing is bought or sold.
- **The master toggle** ends a work the way Far Watch ends a ride when Field Camps are switched off. With the toggle off no work
  starts (the option is hidden and the offers refuse), and a saved work is cleared at the wait's next call, pays nothing and lets the
  wait land on the map; a completion that arrives first pays nothing either.
- **Single-player only.** The gate is `CoopSessionPolicy.IsSinglePlayerAuthority`; a shared campaign greys the option. A work already running when a
  shared campaign begins ends at the wait's next tick, before any injury roll: it is cleared, nothing is paid, the wait lands on the
  map, and one message says camp work is not available in a shared campaign. A completion that arrives first drops the work silently.
  A Far Watch ride out greys the work option too ([far-watch.md](far-watch.md)).

**How it works.** The work is four fields on `CampState` (108 Activity, 109 hours done, 110 target hours, 111 injury rolled), saved
under the existing `_taomFieldCamps` key; an old save reads as no work. The target hours are copied from the setting when the work
starts, so changing the setting mid-work moves nothing. Progress is the hours this wait's tick has added, never a start time: the
engine gives the tick only the time that passed while this wait was active, so time in an interrupting encounter or on the map does
not count, and a start time would count it. The wait menu follows the camp panel's rules above:
the condition delegate is the literal `true`, Stop is the only isLeave option, and when the bar reaches 1 the engine ends the wait and
runs the completion. A load mid-work reopens the menu and its init reads the saved hours. An encounter that interrupts the wait keeps the
progress. Every exit lands through `CampWorkLanding`: the engine's own choice first (an encounter menu), else the map, never the camp
panel. Engine callbacks run through `ScreenFaultLog`; a fault drops the work and lands, and the once-per-site log latch only decides
whether a line is written, never whether the player is freed. `CampService.LoadFrom` clears a saved work with an unknown kind or
hours that are not finite, logging one warning.

**Settings** (MCM group Field Camps, validated in `CampSettingsProvider`, player-local for co-op): `CampActivityHours` 1 to 48 (10),
`CampActivityYieldPerTenMen` 0 to 50 (10), `CampActivityYieldCap` 0 to 200 (30).

**Text.** Player text is `{=taom_camp_*}`. Most of it is in `CampWorkTexts` (work) and `CampRestStartTexts` (rest healing and start
in camp); the menu labels are keyed literals in `CampWorkMenu` and `CampWorkWaitMenu`, and the healing line's label is in
`CampRestModelLine`. The
English rows are in `Main/_Module/ModuleData/taom_module_strings.xml`; the twelve language files under
`Main/_Module/ModuleData/Languages/` (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR) carry the same English text until the next
translation run.

**In-game checklist.**

1. Raise a field camp in forest with 20 or more men and a few tools. "Set the men to work" is enabled; hunt, timber and fish are
   listed (fish greyed if no water is near).
2. Start a hunt. The wait menu shows with a progress bar; time runs; the bar fills over about 10 hours and you land on the map with the
   meat, a morale line and a tools line in the log.
3. Start a work and save mid-way. Load: the wait menu reopens at the same fraction and finishes.
4. Press "Stop work": you land on the map, nothing is paid, and the option reads "Set the men to work" again.
5. Start a work, then meet a hostile party: the encounter takes over; after it, the camp option reads "Return to the work" and the
   progress is kept. Fortify the camp meanwhile: the option is greyed ("Raise a field or fortified camp first") until the raise ends.
6. Camp on a plain: timber is greyed ("Only a forest has timber worth felling"). In a desert: hunting yields 2 of 10.
7. With more than ten men and no tools, the choices are greyed and the tooltip says how many tools are needed.
8. Start a Far Watch ride: the work option is greyed while the scout is out.
9. Over a long work, an injury line appears at most once, near the midpoint, and wounded men show in the party screen.

## Rest healing (#860)

**What the player sees.** While the camp panel (Camp Options, then Manage camp) is open on a raised camp, the panel's status line adds
a second line: "Resting here heals N more wounded troops a day, and the heroes N more hit points a day." The line is on the camp panel
only, not on the base menu where the camp type is chosen. Wounded troops and heroes then recover faster than on the march or in
a stationary party with the panel closed. The healing roll shows a line "Resting in camp" in the party's healing tooltip.

**Rules.**

- **Only while resting.** The party rests when the player's camp stands, its raise is done, `taom_fc_camp` is the open menu, the camp
  feature is on and the campaign is single-player. The work menus, the pick and the wait, are not the camp panel, so a party at work
  does not rest. Closing the panel on the map ends the bonus at once; the next healing roll sees the new state.
- **Rates.** `CampRestHealRegularsPerDay` (15) for wounded troops, `CampRestHealHeroesPerDay` (19) for heroes' hit points, both 0 to 50,
  each a base amount added to vanilla's healing, which the usual healing percentages (skill and perk factors) raise too, not a flat
  extra on top of the total. A rate that is not a positive finite number adds nothing. Setting both to 0 turns rest healing fully
  off: the party is not resting, so there is no line and no lodging perk either.
- **Any raised camp rests,** Field, Fortified, Ambush or Lookout; only a camp still being raised does not.
- **No tick of its own.** `TaomPartyHealingModel` already owns the healing slot, and vanilla heals the main party every hour through
  it. `GetDailyHealingForRegulars` is overridden and `GetDailyHealingHpForHeroes` extended; both act only for `PartyBase.MainParty`,
  never for prisoners, and never for a starving party (vanilla's own starving case stands).
- **Lodging perks.** A Good Day's Rest (Athletics) and Good Lodging (Medicine, `GoodLogdings` in the engine) count in camp, only
  while resting. Vanilla applies them solely inside a settlement (`DefaultPartyHealingModel`, the `CurrentSettlement != null` branch in
  both methods), so the camp applies them through `PerkHelper.AddPerkBonusForParty` with vanilla's own flags, and only when the
  party is not in a settlement, so a perk is never counted twice and no value is guessed. Triage Tent is not repeated: vanilla gives it
  to any party that is not moving, camp or not.
- **Single-player only,** the shared predicate (`CoopSessionPolicy.IsSinglePlayerAuthority`). In a shared campaign the bonus is off and the panel shows no resting line.

**Wiring.** `CampRestHealing` (in `Activities/`, registered by `CampActivitiesModule`) decides; the model only reads the engine and
applies the answer. Each override passes vanilla's number to `CampRestModelLine.Apply` (in `Activities/Hooks/`), which reads four engine facts (is this the main party, are these prisoners, is the party
starving, is it in a settlement) and passes them to `CampRestHealing.Grant`, which returns the bonus and whether the two lodging perks follow; the
helper then adds the "Resting in camp" line and calls vanilla's perk helper twice. The main party is read as `Campaign.Current?.MainParty?.Party`,
so a roll with no campaign cannot throw. The model takes `CampRestHealing` as a required fifth constructor parameter, and `SubModule.cs`
passes `IoC.Resolve<Features.FieldCamp.Activities.CampRestHealing>()` (pinned by `CampRestHealingTests.SubModule_PassesTheCampRestHealingToTheModel`).
The camp panel asks `StatusRates()` through the static `FieldCampMenuController.RestStatus`, which the module sets from `InitializeStatics`
(`CampActivitiesWiringTests.InitializeStatics_GivesTheCampPanelTheContainersRestHealing`); the controller builds the text.

**In-game checklist.**

1. Wound some troops and a hero, raise a camp, leave the panel closed for a day, note the healing in the party screen.
2. Open Camp Options, then Manage camp, and wait a day: the status line mentions resting, the party tooltip shows "Resting in camp", and healing is faster.
3. Start a hunt: the bonus stops while the work menu is open.
4. With A Good Day's Rest or Good Lodging, the party tooltip lists the perk only while the panel is open, and not twice.
5. Turn the camp feature off: no resting line, no bonus. Set both rates to 0: the same, and no lodging perk in the tooltip.
6. Open the camp type menu (Camp Options before a camp stands): no resting line there.

## Start in camp (#860)

**What the player sees.** With "Start New Campaigns In Camp" on (default off), a new campaign begins with a field camp raised at
the starting spot, as if the player had pressed Make Camp on the first moment on the map. If the ground does not allow it, one message
names the reason ("Your campaign begins without a camp: the party stands inside a settlement.") and play goes on as usual.

**Rules.**

- **The hook.** `CharacterCreationState.FinalizeCharacterCreationState` in `TaleWorlds.CampaignSystem` pushes the map state, then
  calls `CampaignEventDispatcher.Instance.OnCharacterCreationIsOver()`, and the dispatcher invokes `OnCharacterCreationIsOverEvent`
  once for each index 0 to 9, ten times in a row. The sandbox new-game path (`SandBoxGameManager`) creates that state, so TAOM's
  campaigns raise the event, and a loaded save never does. `CampStartInCamp` arms on the first call and ignores the other nine. The
  fallback, `TaomCharacterCreationContentHandler.OnCharacterCreationFinalize`, was not needed.
- **The tick.** Arming only sets a flag. The first map `TickEvent` after it makes the one attempt: `CanEstablish(Field, ignoreTownDistance:
  true)`, then `Establish(Field, ignoreTownDistance: true)`, which holds the party on the map. Any block (inside a settlement, moving,
  a Refuge keeping out) shows one message and gives up; the master camp toggle being off gives up silently. There is no retry.
- **Start in camp skips the town distance.** `CampMinTownDistance` is skipped by the start attempt and by nothing else: every default
  culture start stands 0.80 to 7.13 map units from its town gate (measured against the live `TAOM_Map` `settlements.xml` on 2026-10-24, all 25
  starts in `cultures.json`), so the 10-unit rule would block every one. The seam is
  one optional parameter on `CanEstablish` and `Establish`; every other block still applies, and the Make Camp button keeps the rule.
- **Single-player only,** and the hook object is built fresh in the behavior's factory for every campaign, so a second campaign in one
  game session arms again.
- **The log line for the smoke test:** `[FieldCamp] start in camp: established a Field camp on the first map tick, armed by
  CampaignEvents.OnCharacterCreationIsOverEvent`, or `gave up (<reason>)` when blocked.

**In-game checklist.**

1. Turn the setting on, start a new campaign with any culture: after character creation a camp is raised even though a town is
   within a few units, the party is held, and the log has the "established" line.
2. Start one where something else blocks it (for example a party that is already moving): one message with the reason, no camp, no
   second message.
3. Load a save: no camp appears and no message shows.
4. Start a second new campaign in the same game session: the camp is raised again.
5. Setting off: nothing changes.

## Co-op (#966)

A co-op guest gets no camp. `ICampMenuActivationQuery.DefersToCoopHost` (`MapScreenCampMenuActivationQuery.cs:38`) returns `ICoopSessionProvider.ShouldDeferToHost`; `FieldCampOverlayVM` hides the camp button (`FieldCampOverlayVM.cs:176`) and `ExecuteOpenCampMenu` refuses (`:224`, through `MayOpenCampMenu`). A player host and a solo player keep the camp. Cost: under BannerlordTogether or another co-op mod TAOM cannot probe, or after a failed Coop role bind, `ShouldDeferToHost` is true on every peer, solo play included, so the button hides there too. See [coop-interop.md](coop-interop.md).

## Traps

- The Refuge insertion point is **index 4 on BOTH menus**; do not take it. Host Review, Far Watch and camp work all insert at **index 5** on the camp sub-menu, before Leave ([host-review.md](host-review.md), [far-watch.md](far-watch.md)). All three add from `OnAfterSessionLaunched`; listeners of one event run newest first, so the module listed last (camp work, after Far Watch, which is after Host Review in `FeatureModules.cs`) inserts first, Far Watch pushes it down, and Host Review pushes both down. Reorder the modules and the options swap; `FarWatchWiringTests` pins the order (Refuge 4, Review 5, Scout 6, Work 7, Leave 8) and the module positions.
- `CampVisualService` is a process-lifetime singleton: session teardown must call `ClearAll`
  (map-view finalize + game-over handler both do; both idempotent).
- A hand-corrupted save with NaN `BuildHours` renders that camp permanently un-ready; the only
  write sites (Establish/Fortify) sanitize, so this needs deliberate save editing to reach.
- Banner-cloth wind is ticked from `Show`/`IsShown` polling; `CampService.FrameTick` polls
  `IsShown` every pass while the visual stands, and that poll IS the steady-state wind driver: if
  it is ever removed, flags go limp shortly after placement (visuals builder note).
- The behavior's `_syncedThisSession` flag is set by a loading SyncData or by the one-shot reset
  itself (the latch). Do not "simplify" the OnSessionLaunched / OnGameLoaded reset checks away:
  without them a fresh campaign inherits the previous campaign's camp book from the
  process-lifetime singleton and saves it.
- `ICampOverlayContributor` consumers all contain per-contributor (and per-`Lazy` factory)
  exceptions: `CampService.FirstContributorBlockReason` feeds a GameMenuOption condition whose
  engine dispatch is unguarded, so a throwing contributor there is a menu-open crash, not a
  degraded label. Keep the catch when adding consumers.
- `CampLayoutBuilder` placement helpers remove their half-built `GameEntity` in the catch block:
  it is not in `outEntities` yet when a native call throws, so nothing else can ever release it.

## Owed

- In-game smoke per #506 checklist (all four types, fortify re-raise, forage, ambush inquiry +
  battle, save/load mid-build, toggle-off break-via-move-guard, capture-while-camped, tpac render
  + fallback).
- In-game smoke for #860: the three checklists above ("Camp work", "Rest healing", "Start in camp"), none run in game yet.
- 12-language TRANSLATION run for the `{=taom_fcamp_*}` keys (backlog issue #508). Registration
  is done: batch-2 integration purged every `taom_fc_` row from `taom_module_strings.xml`,
  registered all 58 `taom_fcamp_` keys, and rebuilt the 12 language files with English fallbacks;
  FieldCommission's `taom_fc_` rows live untouched in `taom_enlistment_strings.xml`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/from-the-ashes.md](./from-the-ashes.md)
- [docs/features/wanderer-allegiance.md](./wanderer-allegiance.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
