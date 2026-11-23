# Eye of Sauron

## Overview

Great deeds draw a hunter. A player whose battles against enemy kingdoms build up a deed score is hunted by a
named hero with a formidable host: one of the Nine with high-tier Mordor troops (Dol Guldur troops for the four of
the Nine who ride for Dol Guldur) when the player fights for the Free Peoples, or Glorfindel with a host of the
Eldar, sent by Elrond, when the player fights for the Dark. The hunter closes in while out of sight and, once within
1.5 of the player's seeing ranges, moves faster than the player, so a player who stops, turns to face him, or keeps
riding meets him in battle. Milestone M8 of #765 (War Chronicle).

## Why This Exists

- **Vanilla behavior:** no lord ever singles the player out. An AI party engages the player only when its own
  initiative score says so, and it scales that score down by the speed ratio (DefaultMobilePartyAIModel), so a
  faster player is soon left alone.
- **TAOM requirement:** the war should notice the player. Sauron's attention, or Elrond's, is lore the campaign
  never showed; a player who keeps winning should meet the enemy's champions, not only its levies.
- **Without this feature:** a strong player never meets the Nine or Glorfindel in the field unless he seeks them out.

## The maintainer's decisions (final)

1. A **Free-side** player whose deeds (won battles and sieges) draw the Eye is hunted by the Witch-king, Khamûl or
   another of the Nine with a formidable army of high-tier Mordor troops (Dol Guldur troops for a wraith of Dol
   Guldur: Khamûl and the three `lord_1_48_*` wraiths, whom `heroes.xslt` places in `clan_dolguldur_1`).
2. An **Evil-side** player is hunted by Glorfindel (`lord_R2_1`), sent by Elrond, with high-tier Elven troops.
3. Only a player whose kingdom (ruler, vassal or mercenary) is **at war with the hunter's kingdom** is hunted. A
   kingdomless player is never hunted.
4. A hunter who already leads a party has **that party** re-tasked and topped up; a free hunter gets a new party
   spawned out of sight near the player. Never a second party for one hero.
5. The hunter moves closer in steps, hour by hour, while out of sight; once within the player's visual range he
   moves faster than the player.
6. No reward beyond normal battle loot. **Single-player only** in v1.

The player is told once when a hunt starts and once when it ends (orchestrator decision: no warning phase).

Decided by the maintainer 2026-10-14:

7. **Flee keeps the hunt.** A battle ends the hunt only when the hunter took part and someone won it (the player's
   battle against him), or when the hunter's side was defeated. A player who gets away ("Try to get away" leaves the
   battle with no winner) is still hunted, and the hourly re-issue restores the engage order. A battle without the
   player that the hunter wins, or that nobody wins, leaves the hunt running.
8. **Speed floor from 1.5 S.** The in-sight speed floor applies from 1.5 times the player's seeing range inward, the
   same margin the steps keep (`SightMargin`), so a moving player cannot be shadowed between S and 1.5 S.
9. **No hunt while in another lord's army.** While the player's party is in an army another lord leads (never his
   own), no hunt starts: the start is deferred, with no cooldown, and the daily tick tries again. A running hunt
   stands off: the hunter's engage order and pin are released through the end's release path, so his own AI resumes,
   no speed floor applies and he takes no step; the hunt itself goes on (no end, no cooldown, the timeout counts).
   The first hour after the player leaves the army, the hunter is re-pinned and re-engaged once, unless his own AI
   left him busy (a battle, an army he leads or rides in, a siege, a settlement, another order's pin): then the hunt
   claims no pin and retries each hour until he is free.
10. **Bannerlord Together enabled means no hunt.** A co-op mod TAOM cannot probe stands the hunt down like a live
    co-op session, since it cannot tell host from client.
11. **The Dol Guldur kingdom rule.** A wraith whose live kingdom is `dolguldur` brings the Dol Guldur host; every
    other wraith brings the Mordor host.
12. **On by default.** `EyeOfSauronEnabled` defaults to on.

Decided by the maintainer 2026-11-23:

13. **A bribed hunter waits out the oath.** A safe passage gives the player a 5-day oath with the hunter's faction
   (`NoAttackBarterable` adds 5 days to `IFaction.NotAttackableByPlayerUntilTime`). The hunt stands off while that
   oath or the party's own shorter promise holds, then resumes, and no new hunt starts from a faction under oath. The
   oath is the faction's, so a bribe to any lord of the hunter's faction pauses the hunt too, and the hunt's timeout
   keeps counting.

## Architecture

### Design Challenge

The engine has no "hunt the player" order that survives the AI's own decisions, the speed model runs on the
parallel party tick, a party-size limit is cached per roster version, and a topped-up host is trimmed by the daily
TroopWeight shed unless its limit covers it. Every piece of state must survive a save, and a pinned party must never
be left pinned.

### Solution Approach

| Piece | What it does |
|---|---|
| Deeds | `MapEventEnded`: a player victory over a side whose faction is a `Kingdom` scores 1 (any field battle, a village raid and a forced levy of supplies or volunteers included), 2 (sally out or a battle outside a siege) or 3 (siege assault) against that kingdom's side (`AlignmentService.GetKingdomSide`; Neutral never scores). Two scores, against Evil and against Free, decay with a half-life when read. Deeds score even while kingdomless, but never on a co-op peer. |
| Start (daily) | Single-player (read live: no co-op session, no co-op mod TAOM cannot probe, no dedicated server); the player clan has a kingdom; its side is Free or Evil, from the kingdom id, else the kingdom's own culture (`ResolveSide`), so a player-founded `new_kingdom` is sided; the score against the other side reaches the threshold; no cooldown. While the player is in another lord's army the start is deferred (no cooldown; retried daily), on the console path too. |
| Hunter | Free player: the Nine in order, Witch-king (`lord_1_15`) first, Khamûl (`lord_1_48`) last. Evil player: Glorfindel only. The first in order who is alive, not a fugitive or prisoner, not marked for death, whose live faction is at war with the player's kingdom, and who can ride today is chosen. |
| Busy hunter | His party is re-tasked only when it is not in a battle, an army, a siege or a settlement, not disbanding and not pinned by anyone else; a free hunter must be able to lead a party. A busy or unfit hunter is passed over for the next in order; the hunt waits for tomorrow only when none can ride. A busy hunter never gets a second party. |
| Free hunter | Spawned with `MobilePartyHelper.SpawnLordParty` at a reachable point 1.6 to 2.5 of the player's sight away (straight line beyond 1.5; the service passes the ring), after removing him as governor and re-checking that he belongs to no party. A failed search is not retried that day (the engine has already tried 250 points), and a refused spawn stops the day's search and is logged. |
| Order | Host and grain first, then `SetPartyAiAction.GetActionForEngagingParty(hunter, MainParty)`, `CheckPartyNeedsUpdate`, `SetDoNotMakeNewDecisions(true)`. Re-issued hourly only when lost, since every call resets the party's movement state. |
| Host | Registered as the hunter's party first, and its cached size limit invalidated (`MemberRoster.UpdateVersion`: `PartySizeLimit` is cached per roster version, which `MobileParty.UpdateVersionNo` does not touch), even when the top-up adds nobody; then topped up to `HunterStrengthRatio` times the player's strength (his army's when he leads one) with the host's troops, `ceil(deficit * share / power)` each, capped at `HunterMaxTroops` men. Every day the troops are topped up again (never during a battle), and the grain is brought back to three days (during a battle too). |
| Steps (hourly) | First the end gates; then, while the player is in another lord's army, the hunter stands off (his pin released once, no re-engage, no step; the last step reads "player in another lord's army"); next, while vanilla has promised the hunter will leave the player alone (`Ai.DoNotAttackMainPartyUntil` in the future: a safe passage bribe, a lord paid off in conversation, an escape from a lost encounter; or the faction's safe-passage oath, decision 13), he stands off the same way (the last step reads "hunter promised to leave you alone"); after a stand-off the first free hour re-pins and re-engages him once, whatever his own AI chose; while his AI has left him busy (the start's busy test: a battle, an army he leads or is attached to, a siege, a settlement, disbanding, another order's pin, the promise above) the resume claims no pin, does not step, and retries the next hour. Out of sight only. The service judges the adapter's raw step state: the hunter active, not in a battle, siege, army, settlement or at sea, not visible, engaging the player; the player active, on the open map (no battle, siege, settlement, encounter or sea), no true sight, same island. Then, only beyond 1.5 S in a straight line, it rebuilds the path, walks `min(pathGap - 1.5 S - 3, StepCap)` along it, accepts the point only on the player's island with no enemy of the hunter within 3 and at least 1.5 S from the player, and moves the party. S is the player's seeing range, re-read each hour. `taom.print_eye_of_sauron` shows the last step's outcome. |
| Speed | A main-thread frame tick finds the hunter with the locator search around the player (radius 1% beyond 1.5 S, the service's `SightMargin`, since the engine's locator skips a party at exactly its radius; no allocation) and publishes `{hunter party, in band, player speed, multiplier}`; the hunter is in band at a straight-line distance of at most 1.5 S (a NaN distance or S is out). The frame tick also stands the hunter off the moment the player is in another lord's army or the sight reading shows vanilla's promise to leave the player alone, and publishes him out of band whenever the hunt does not hold his pin (standing off, or not yet re-pinned). `TaomPartySpeedModel` raises the hunter to `player speed * HunterSpeedMultiplier` (never under 1) while in band, reading only that snapshot on the worker threads. The hunter's speed cache is refreshed when the in-band state flips or the player's speed moves more than 5%. |
| Size | `TaomPartySizeModel` lifts the hunter's limit to the larger of its raw and weighted head count, before the TroopWeight line, so the size sheds (the TroopWeight shed and vanilla's size rule) never trim the host. It does not stop vanilla's wage-limit desertion (up to 20 men a day once the clan leader holds 90,000 gold or less); the daily top-up replaces those men. |
| End | The first of: a battle the hunter's side lost, or the player's battle against the hunter that someone won (`HuntBattleReader` reads the winner and the hunter's side at `MapEventEnded`; a flee, with no winner, never ends the hunt); the party destroyed, missing or under another leader; the hunter dead, captured or a fugitive; the war ending; the player kingdomless; a co-op session, an unprobeable co-op mod or a server; `HuntTimeoutDays`; the switch off; `taom.clear_eye_hunt`. The gates run at the hunter's hourly tick as well as daily, so a peace or a kingdom change ends the hunt within the hour. Every end releases the pin (dropping an engage order on the player and having the party rethink next hour) unless a stand-off already released it, clears the snapshot, refreshes the size limit and the speed and starts the cooldown. A start refused after the host was registered clears it and refreshes the size limit too. |

### Component Diagram

```
CampaignEvents (daily, hourly party, frame, MapEventEnded, MobilePartyDestroyed, session launch)
        |
  EyeOfSauronBehavior (thin router, SyncData)          TaomPartySpeedModel / TaomPartySizeModel
        |                                                         |  IEyeOfSauronHook (snapshot only)
  EyeOfSauronService (scores, gates, hunter choice, host plan, steps, snapshot, ends)  +  HuntMath (pure arithmetic)
        |
  IHunterPartyAdapter -> HunterPartyAdapter (Hero, MobileParty, NavigationHelper, popups)
```

`EyeOfSauronModule` registers the services in `IoC.Configure` and builds the behavior with `new` in its declaration's
factory, once per campaign start, so the behavior's constructor resets the singleton service before any event or
SyncData (`EyeOfSauronWiringTests` pins the shape and rejects a cached, static or resolved construction).

### Persistence

Seven `SyncData` keys (`_taom_eye_*`), one per field of `EyeOfSauronState`: both scores, the last decay day, the
cooldown end, the hunter's hero and party StringIds, and the start day. Ids are resolved against the live campaign on
every use. The engine saves the hunter's AI pin (`DoNotMakeNewDecisions`): a hunt saved while pinned ends by
releasing it, and one saved during a stand-off holds none. A load drops the in-sight state and re-registers the host;
on session launch the live pin first tells the service whether the hunt holds it, then a hunt whose party is gone, or
a hunt restored into a co-op session, ends. So a stand-off saved mid-way re-pins on resume, and neither it nor a
launch-time end releases a pin the hunt no longer owns.

### Threading

The speed and size models run on the parallel party tick as well as the main thread. They read only the immutable
snapshot (`Volatile.Read`); the multiplier is captured into it on the main thread, so neither model reads MCM,
`MainParty.Speed` or a position. The size model's weighted-count delegate is built once in its constructor, so the
per-party call allocates nothing.

### Open risks

- **Standing off frees the hunter's AI:** while the player is in another lord's army the hunt's order and pin are
  released and no floor applies, but the hunter's own AI decides for itself, as any lord's does, and may still
  choose to engage the army on its own initiative.
- **Bribes pause the hunt, and the timeout keeps counting:** each safe passage adds 5 days to the faction's oath
  (decision 13), so repeated bribes can run a hunt out (`HuntTimeoutDays`). Vanilla AI never reads the oath; the hunt does. The
  oath is cleared early when a party of that faction attacks the player or starts a siege the player is part of
  (`EncounterGameMenuBehavior.OnMapEventStarted`, `OnSiegeEventStarted`); the hunt then resumes at the next free hour,
  unless it was the hunter's own attack and that battle ends the hunt. Vanilla AI honours only the party's 32-hour
  promise, so after it the hunter's own AI may attack the player during the oath, as any bribed lord's may.
- **Uninstalling mid-hunt** leaves the hunter's saved AI pin with nothing to clear it. The timeout bounds the window in
  a game that keeps TAOM; remove TAOM only between hunts.
- **At sea:** the sea guards (`HunterAtSea`, `PlayerAtSea`) are unreachable in a supported setup. TAOM declares
  the War Sails module (`NavalDLC`) incompatible in `Main/_Module/SubModule.xml` (`<IncompatibleModules>`, see
  [advanced start options](advanced-start-options.md)), and TAOM's own sailing is parked
  ([naval travel](naval-travel.md)), so no party reaches the sea. The guards stay as a belt.
- **UNVERIFIED in the engine:** save and load in a running game, the landing point's native navmesh results, and
  live Bannerlord Together behaviour were never exercised (the reviews ran unit tests and IL checks only); the
  in-game checklist below covers the first two.
- **A co-op session that goes live mid-hunt** is noticed at the hunter's next hourly tick; until then the ungated
  frame tick keeps publishing the snapshot ([co-op](coop-interop.md)).

## Configuration

MCM group **War of the Ring/Eye of Sauron**. Defaults and slider bounds live in `EyeOfSauronSettingsProvider`; the
provider reverts a non-finite float to its default before clamping it into the slider range, and clamps the ints
(`SettingClamp`).

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `EyeOfSauronEnabled` | on | | Master switch; turning it off ends a running hunt within the hour. This is the default for a new campaign: a campaign that pinned its own choice follows that, so MCM is the live answer only for an unpinned save ([campaign-start-options.md](campaign-start-options.md)) |
| `EyeThreshold` | 10 | 1 to 100 | Decayed deed score that draws a hunt |
| `EyeHalfLifeDays` | 30 | 1 to 365 | Days for a score to halve |
| `EyeCooldownDays` | 60 | 0 to 365 | Days after a hunt ends before another can start |
| `HuntTimeoutDays` | 20 | 1 to 120 | Days before the hunter gives up |
| `HunterSpeedMultiplier` | 1.3 | 1.1 to 2.0 | Within 1.5 of the player's seeing ranges (decision 8), the hunter moves at least this many times the player's speed |
| `HunterStrengthRatio` | 1.3 | 0.5 to 3.0 | Host strength as a multiple of the player's |
| `HunterMaxTroops` | 250 | 20 to 1000 | Most men in the hunter's party after the top-up |
| `HunterStepCap` | 15 | 1 to 50 | Longest single out-of-sight step, in map units |

MCM persists each property, so a changed compiled default reaches fresh installs only.

### Hosts (`HuntTroopLists`)

| Host | Troops (share) |
|---|---|
| Mordor | `mordor_num_temple_knight` (0.25), `mordor_num_temple_guard` (0.25), `mordor_num_shadowbow` (0.20), `mordor_num_knight` (0.15), `mordor_uruk_baraddurguard` (0.15) |
| Dol Guldur (a hunter whose live kingdom is `dolguldur`: Khamûl, `lord_1_48_1`, `lord_1_48_2`, `lord_1_48_3`) | `dg_khamul_shadow_knight` (0.30), `dg_khamul_shadow_guard` (0.25), `dg_khamul_shadow_reaper` (0.20), `dg_khamul_shadow_bowman` (0.25) |
| Rivendell (Glorfindel) | `rivendell_glorfindel_guard` (0.30), `rivendell_knight_golden_flower` (0.25), `rivendell_royal_knight` (0.25), `rivendell_gondolin_battlemaster` (0.20) |

`HuntTroopListsDataTests` proves every id is a level 36 or higher soldier of its culture in `Main/_Module/ModuleData`,
and that `dolguldur` is the kingdom Khamûl rules.

## How to

### Change a host's troops

1. Edit the host's list in `Main/Features/EyeOfSauron/HuntTroopLists.cs` (`Mordor`, `DolGuldur` or `Rivendell`):
   each entry is a troop id and its share of the deficit.
2. Keep the shares summing to 1, and use only soldiers of level 36 or higher from the host's culture in
   `Main/_Module/ModuleData`.
3. Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~HuntTroopListsDataTests`;
   it checks both rules for every id. Code change only: no data file holds the lists.

### Change the hunter order

1. Reorder `HuntTroopLists.WraithOrder` (Free player) or `ElvenOrder` (Evil player). The first hunter in order who
   can ride today is chosen; one who cannot is passed over for the next.
2. `WraithOrder` must stay exactly the Nine, the Witch-king first and Khamûl last
   (`WraithOrder_IsExactlyTheNine_WitchKingFirst_KhamulLast`). A wraith's host follows his live kingdom
   (`HostFor`), so moving a wraith in the order does not change his troops.
3. Run the same filtered test, plus `EyeOfSauronServiceTests` for the order rows.

### Tune the settings

1. In game: MCM, group **War of the Ring/Eye of Sauron** (the table above). The provider reads MCM on every use,
   so a slider change applies at its next read (the speed multiplier on the next frame), with no restart or reload.
2. A slider bound is a constant in `EyeOfSauronSettingsProvider.cs`, the single source for both the clamp and the
   slider in `Main/Features/TaomSettings.cs`. A persisted MCM default never flips: MCM loads the saved json2 value
   over the compiled default, so rename the setting to change its default
   ([shader precompilation](shader-precompilation.md); orientation.md "Persisted MCM defaults").
3. Run `EyeOfSauronSettingsProviderTests` (defaults, NaN, infinity and range clamps).

## Console commands

| Command | Tier | Does |
|---|---|---|
| `taom.print_eye_of_sauron` | A | Scores, threshold, cooldown, the player's kingdom and side, and the running hunt (hunter, party, live pin and engage state, day, in sight, speeds, last hourly step) |
| `taom.add_eye_score <evil\|free> <n>` | B | Adds `n` (between -1000 and 1000) to the score against Evil or against Free; never below zero |
| `taom.start_eye_hunt [confirm]` | C | Dry run without `confirm`: who would ride, re-task or spawn, a governorship given up, the top-up. With `confirm` it starts a hunt now, ignoring the score and the cooldown; the switch, single-player, kingdom and war gates still hold, and it starts nothing while you ride in another lord's army (the dry run says so too). The party, its troops and grain, and a lost governorship remain after the hunt |
| `taom.clear_eye_hunt` | B | Ends the hunt and releases the hunter; a spawned hunter stays on the map as an ordinary lord party |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/EyeOfSauron/EyeOfSauronService.cs` | All decisions; implements `IEyeOfSauronHook` |
| `Main/Features/EyeOfSauron/HuntMath.cs` | Pure arithmetic: decay, deed weights, top-up plan, food, steps |
| `Main/Features/EyeOfSauron/EyeOfSauronTypes.cs` | End reasons, deed kinds, the battle summary, the saved record |
| `Main/Features/EyeOfSauron/IEyeOfSauronHook.cs` | The models' seam |
| `Main/Features/EyeOfSauron/EyeOfSauronBehavior.cs` | Event router and SyncData; constructor resets the service |
| `Main/Features/EyeOfSauron/EyeOfSauronModule.cs`, `EyeOfSauronIoC.cs` | Feature module and registration |
| `Main/Features/EyeOfSauron/HuntTroopLists.cs` | Hunter order and hosts |
| `Main/Features/EyeOfSauron/HuntBattleReader.cs` | `MapEvent` to a plain battle summary |
| `Main/Features/EyeOfSauron/EyeOfSauronSettingsProvider.cs`, `IEyeOfSauronSettings.cs` | Validated MCM values |
| `Main/Features/EyeOfSauron/Cheats/EyeOfSauronCheats.cs` | The four console commands |
| `Main/Features/TaomSettings.cs` | The MCM group **War of the Ring/Eye of Sauron** |
| `Main/Composition/FeatureModules.cs` | Lists `EyeOfSauronModule` |
| `Main/SubModule.cs` | Hands the one resolved `IEyeOfSauronHook` to both party models |
| `Main/Adapters/IHunterPartyAdapter.cs`, `HunterPartyAdapter.cs` | Engine boundary |
| `Main/Adapters/HunterHeroSnapshot.cs`, `HuntPartySnapshot.cs`, `HuntSightReading.cs`, `HuntStepState.cs`, `HuntPath.cs`, `HuntLanding.cs`, `HuntAnnouncement.cs` | The boundary's plain values |
| `Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs`, `TaomPartySizeModel.cs` | One seam line each |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The four popup lines: `taom_eye_hunt_free`, `taom_eye_hunt_evil`, `taom_eye_hunt_won`, `taom_eye_hunt_over` |

## Dependencies

- `IAlignmentService` (Execution): the side of a kingdom, and of the player's own by its culture.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the single-player gate.
- `AiPartySizeService.AddResultFrameBonus`: adds the host bonus in the result frame.

## Tests

- `TAOM.Tests/Features/EyeOfSauron/EyeOfSauronServiceTests.cs`: decay and NaN scores, deed weights and the Neutral,
  non-kingdom, lost-battle and co-op cases, the start gates (the console path's refusals included), a player-founded
  kingdom, hunter order and the pass-over of a busy hunter, the spawn ring and a refused spawn, the dry run, host
  sizing, the daily re-top-up and the head cap, food, every step gate and the step order, landing, the speed floor
  and size bonus with their NaN gates, the floor band's 1.5 S edges, the ends (daily and hourly) releasing the pin, a
  flee and a third-party battle keeping the hunt, the start deferred and the hunter standing off in another lord's army, a resume waiting while the hunter is busy (army leader or member included), the size limit refreshed on every membership change, a stand-off save loaded into co-op releasing nothing, reset and a field-by-field
  persistence round trip, the console's score edits and `Describe`, and a waiting hunter logged by the typed wait
  (never by the message's wording; a dry run logs nothing). Uses a real `AlignmentService`.
- `EyeOfSauronCheatsTests.cs`: the `confirm` token, the side parser and the score bounds.
- `EyeOfSauronWiringTests.cs`: the module listed once and owning its save data, no hand registration, the
  per-campaign construction pin and its negative rows, SubModule's one hook for both models, the speed seam's order,
  and the service graph resolving. `AiPartySizeOrderingTests` pins the size seam before the TroopWeight line.
- RequiresGame: `EyeOfSauronBehaviorSessionResetTests.cs` (save into a fresh behavior through an engine-faithful
  store), `EyeOfSauronSpeedFloorTests.cs` (`LimitMin` on a real `ExplainedNumber`), `HunterPartyAdapterIlTests.cs`
  (no linear party walk per frame, the widened locator radius, the army attachment read),
  `TaomPartySizeModelAllocationTests.cs` (no per-call delegate). `HunterPartyAdapterLocatorRadiusTests` (no game)
  pins the radius past the exact 1.5 S edge.
- `EyeOfSauronSettingsProviderTests.cs`: each float's default, NaN and infinity reverting to it and the range
  clamp; the int clamps; reads through the cached settings object.
- `HuntTroopListsDataTests.cs` (reads `Main/_Module/ModuleData`): every host troop a level 36 or higher soldier of its
  culture, shares summing to 1, the wraith order, Glorfindel a Rivendell lord, the host picked by hunter and live
  faction, and `dolguldur` the kingdom Khamûl rules.

## In-game test checklist

Owed in game: #765 closed on 2026-10-14 with the `triage-needs-ingame` label, which comes off once these pass. Use a Free kingdom (Gondor) unless stated.

1. **Won:** `taom.add_eye_score evil 20`, wait a day. The start popup names the hunter. Find and beat him; the
   "is beaten" line shows and `taom.print_eye_of_sauron` reports no hunt and a cooldown.
2. **Lost:** start a hunt, let the hunter catch and beat you. The hunt ends with "The hunt is over, for now".
3. **Timed out:** set Hunt Timeout to 1, start a hunt, stay in a town for two days. The hunt ends; the hunter resumes
   his own decisions within about an hour.
4. **Re-tasked hunter:** with the Witch-king leading his own party outside an army, run `taom.start_eye_hunt` and
   check that the dry run says re-tasked; then `taom.start_eye_hunt confirm`. His existing party turns toward you,
   grows to the strength ratio and no second party appears.
5. **Spawned hunter:** with the Witch-king without a party, run `taom.start_eye_hunt` (the dry run says spawned),
   then `taom.start_eye_hunt confirm`. A new party appears out of sight and closes in hour by hour; in sight it
   outpaces you. Run the Map Profiler `top=EyeOfSauronBehavior` during the hunt.
6. **Keep riding:** with a hunt running, keep riding away from the hunter once he is in sight; he closes the gap and
   brings the battle (the floor applies from 1.5 S).
7. **Save and reload mid-hunt:** save while hunted, reload. `taom.print_eye_of_sauron` shows the same hunter, party
   and day; the hunter still chases and is not shed back to its old size.
8. **Glorfindel:** join an Evil kingdom at war with Rivendell, `taom.add_eye_score free 20`. Glorfindel rides with an
   Elven host and the Elrond line shows; then `taom.clear_eye_hunt`, and the end line does not mention the Eye.
9. **Founded kingdom:** found a kingdom with Gondor or Rohan culture at war with Mordor, `taom.add_eye_score evil 20`;
   a hunt starts.
10. **Dol Guldur wraith:** a hunt by `lord_1_48_1` against a Free kingdom at war with Dol Guldur only brings the Dol
    Guldur host.
11. **Wage desertion:** a Nazgûl hunt with his clan leader under 90,000 gold keeps its head count across several days.
12. **Peace:** make peace with the hunter's kingdom mid-hunt; the hunt ends within the hour and the hunter stops
    chasing.
13. **Kingdomless:** leave your kingdom with a high score; no hunt starts, and a running one ends.
14. **Flee:** let the hunter catch you and choose "Try to get away". `taom.print_eye_of_sauron` still shows the hunt,
    and within a few hours he engages you again (vanilla may first give you up to 2 hours' grace, which the hunt
    honours).
15. **Another lord's army:** with a high score, join another lord's army; no hunt starts while you stay. Start one,
    then join an army: the hunter stops chasing at once, `taom.print_eye_of_sauron` shows the hunt with "pinned: no" and
    the last hourly step "player in another lord's army"; leave the army and within the hour he is pinned and
    chasing again.
16. **Bought off:** let the hunter catch you and pay him for safe passage. He stops chasing at once,
    `taom.print_eye_of_sauron` shows the hunt with "pinned: no" and the last hourly step "hunter promised to leave you
    alone", and he does not come back to talk. The hunt leaves him unpinned for the 5 days of the oath (until then
    your attack option on his faction reads "You have sworn not to attack"); after about 32 hours his own AI may still
    choose to attack you, as any bribed lord's may. Within the hour after the oath ends he is pinned and chasing
    again, and when he catches you, you can fight him.

## Changelog

- 2026-10-08: built (#765 M8): deeds, hunter choice, re-task or spawn, host top-up, out-of-sight steps, in-sight
  speed floor, host size seam, console, MCM.
- 2026-10-14: review fixes: a player-founded kingdom is sided by its culture; the per-frame sight read searches only
  around the player; the end gates run hourly; a busy hunter is passed over; the host is topped up daily; Bannerlord
  Together stands the hunt down; `start_eye_hunt` is tier C with a dry run; the step decisions moved into the service.
- 2026-10-14: the maintainer's decisions: a flee keeps the hunt (E06); the speed floor applies from 1.5 S (E09); no
  hunt while in another lord's army, and a running one stands off (E17). The stand-off then made real: the hunter's
  order and pin are released and the floor is off while the player is in the army, and he is re-pinned once after.
- 2026-10-14: Codex review fixes: the resume waits while the hunter's own AI left him busy (an army above all); every
  hunt membership change refreshes the cached size limit; session launch reads the live pin before any end; the
  locator search reaches just past 1.5 S.
- 2026-10-14: review follow-ups (#792): the hunt popup's map-free check reads the stored `Campaign.Current?.MainParty`;
  the computed `PartyBase.MainParty` throws while it is null.
- 2026-11-23: the hunt honours vanilla's promise not to attack the player (`Ai.DoNotAttackMainPartyUntil`: a safe
  passage bribe 32 h, a lord paid off in conversation 24 h, an escape from a lost encounter 2 h). The hourly re-engage
  overrode it, so a bought-off hunter kept catching the player in an encounter he would not fight. He now stands off
  as in another lord's army (hourly and frame tick) and is re-pinned once the promise runs out; a hunt does not start
  on a hunter who holds it.
- 2026-11-23 (decision 13): the hold also reads the faction's 5-day safe-passage oath
  (`IFaction.NotAttackableByPlayerUntilTime`), so a bribed hunter waits out the oath, not only the 32-hour promise.

## GitHub Issue

- **Issue:** #765 (War Chronicle), milestone M8; follow-ups #792 (closed)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bannerlord-together-compat.md](./bannerlord-together-compat.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/war-chronicle.md](./war-chronicle.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/gamemodel-registry.md](../reference/gamemodel-registry.md)

<!-- backlinks-end -->
