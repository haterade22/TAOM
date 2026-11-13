# Fire and Tribute

## Overview

Once the War of the Ring is in Full War, raiding hosts ride out of their homelands to burn the villages of the strongest enemy realm. Orcs of Gundabad, Easterlings of Rhûn, Haradrim, Dunlendings and the Blacklocks of Erebor (a dwarf clan fallen into shadow, so a Shadow host) raid the Free Peoples; outlaw bands of Gondor and Mirkwood strike back at the Shadow; the Corsair Host of Umbar raids either side, never a Neutral realm. A host engages weaker parties of its target realm it meets, raids villages until its quota is done, and goes home. This is slices 1 and 2 of #870: the hosts, and the tribute envoy that asks the player's clan to pay for its villages.

## Why This Exists

- **Vanilla behavior:** bandits are small, scattered and local. Nothing sends a war host at a realm.
- **TAOM requirement:** the scripted wars of the War of the Ring need a threat that is not a kingdom declaring war, and that bites the strongest realm of the opposed side.
- **Without this feature:** the late campaign has no pressure beyond the scripted wars.

## What the player sees

- A red rumour with the attack horn when a host rides: "Riders bring word: {HOST} has taken the field near {TOWN}, and villages will burn." (`taom_fat_spawned`).
- Neutral lines when a host turns home (`taom_fat_going_home`), when it reaches home and scatters (`taom_fat_went_home`), and when it is broken (`taom_fat_broken`).
- A map party named for its host (for example "The Host of Gundabad") that raids villages of one kingdom. Looted villages follow vanilla's raid rules. Every host name is a singular collective, so the messages read right ("The Free Company of Anórien is broken"), and none repeats the clan's ordinary bandit name.
- **The tribute envoy** (slice 2). A host that rides against the player's kingdom sends an envoy a day or two later (24 to 48 hours by default), once the player's clan holds a village, the player is not enlisted, the host is out of battle and the map is free. It is a popup titled "{HOST} Demands Tribute" with one choice to pick (no cancel button) that names the host, the villages and the price, and offers Pay, Haggle (from four price units, once), Ask for time to decide, Send the envoy away and Seize the envoy. A line tells the result. See "Tribute envoy" below.

## The eight hosts

| Host | Side | Clan | Name | Anchors (towns and castles of its own side) |
|---|---|---|---|---|
| `gundabad` | Evil | `gundabad_raiders` | The Host of Gundabad | `town_G1`, `castle_G4`, `town_G2` |
| `rhun` | Evil | `rhun_raiders` | The Easterling Host of Rhûn | `town_RU1`, `town_RU3`, `town_RU5` |
| `harad` | Evil | `harad_raiders` | The Warhost of Harad | `town_A3`, `town_A5`, `town_A4` |
| `dunland` | Evil | `dunland_raiders` | The Dunlending Host | `town_EN3`, `town_EN1`, `town_EN2` |
| `erebor` | Evil | `erebor_warriors` | The Blacklock Warband | `town_RU4`, `town_RU5`, `town_RU7` |
| `umbar` | Neutral | `umbar_corsairs` | The Corsair Host of Umbar | `town_U1`, `town_U2`, `castle_U7` |
| `gondor` | Free | `gondor_soldiers` | The Free Company of Anórien | `town_EW1`, `castle_EW4`, `town_EW7` |
| `mirkwood` | Free | `mirkwood_stalkers` | The Stalker Band of Mirkwood | `town_M1`, `town_M2`, `castle_M3` |

The Blacklocks muster beside Rhûn's fiefs (their hideouts lie there), not in the halls of Erebor, which belong to the Free realm they raid; the Free Company musters on the Ithilien front. The data test `EveryFreeAndEvilHosts_AnchorsLieInRealmsOfItsOwnSide` (LiveInstall) resolves each anchor's authored culture through `execution/alignment.json` and fails if a Free or Shadow host would appear in an enemy realm.

## Rules

| Rule | Detail |
|---|---|
| Window | The War of the Ring in Full War or after it ended, from `firstHostDelayDays` (45) after the later of Full War's effective day and the day this save first saw it. With the War of the Ring off: from `warlessStartDay` (89). |
| Roll | Each day, for each side with no host and no cooldown: `spawnChance` (5%). At most one host per side, so at most three. |
| Cooldown | `cooldownDays` (120) per side, from the day its host is broken, goes home or is lost, and from the day a co-op session releases it. Removing a host because a switch went off starts none. |
| Host choice | The side's hosts in a random rotation; the first with a target, a clan, a hideout and an anchor goes. An anchor counts only while its current owner is a kingdom that resolves to the host's own side (`IRaidHostAdapter.TryGetAnchorOwner`, then `IAlignmentService.ResolveSide`): one held by a kingdom of another side, or by a faction with no kingdom (a rebel clan, whose parties the own-side rule does not hold back), is skipped as `AnchorHeldByOtherSide` and the next anchor is tried (#1032); an owner the game cannot read leaves the anchor usable. A host that cannot go is skipped with one log line. |
| Broken line | The `was broken after N/M raids` line names the destroyer the engine reports, the leader party of the side the host fought (usually the winner): `by the player's party`, `by empire_w, its own side` (an AI party of a kingdom on the host's side, the case the own-side rule should prevent), `by empire_w, side Free`, `by town_G1_rebel_clan, no kingdom`, or `by an unknown party` when the engine names none (a disband or a removal). The ids are the map faction's (#1034). |
| Target | The first of the host's `preferredTargets` that qualifies, else the strongest, ties by ordinal id. Only living kingdoms of the host's `targetSides` that own a raidable village (`FireAndTributeMath.KingdomsWithRaidable`). |
| Size | `target strength x strengthShare` split over the troops by share with `HuntMath.PlanTopUp`, lifted to `minTroops` and capped at `maxTroops`. |
| Quota | `clamp(round(target strength / strengthPerRaid) + rand(quotaExtraMin..quotaExtraMax), quotaMin, quotaMax)`. |
| Steering | Once an hour, over every live host (one per side); a host whose step throws is logged and the others still steer. A host in a map event is left alone. Otherwise it engages the nearest party of the target kingdom within `engageRadius` that is not in a battle or settlement or attached to an army, has at most `engageStrengthRatio` of its strength and is not faster; else it keeps a still-valid raid order or picks one of the `nearestVillages` closest raidable villages at random (owner is the target kingdom, normal state, finite hearth at least `minHearth`). |
| Own side | Parties of the host's own side leave it alone, and it leaves them alone. The host's clan is an outlaw clan at war with every kingdom, and the host musters in its own side's realms, so before this rule those lords broke every host before its first raid (all 7 hosts of a 2026-10-26 run, 0 raids). `TaomMobilePartyAIModel` answers false to `ShouldConsiderAttacking` and `ShouldConsiderAvoiding` when one party is the host and the other's map faction is a kingdom that `IAlignmentService.ResolveSide` puts on the host's side: lords, armies, patrols, mercenaries in its service, and its caravans and villagers, which therefore no longer flee the host either. The side function is the target picker's, so a Neutral host's own side is every Neutral-resolving kingdom (`umbar`, `battania` (Khand), `abanissa`, `shaghana`, `cardolan`, `rhudaur`, and any other kingdom whose id and culture both resolve Neutral). The model reads an immutable map of host party id to side, which the service replaces on the main thread whenever the host list changes (spawn, every removal, load, reset, co-op release); a kingdom's side is resolved when asked. Only the host is spared: the clan's ordinary bandit parties stay at war as in vanilla, the stance is unchanged, and the rule is inert in co-op, where every host is released. **Not covered** (the rule is AI initiative only): the player's battles pull in every nearby lord at peace with the player (`DefaultEncounterModel.FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter`), so own-side lords join a fight between the host and a player of its target realm; a village near such a fight is stamped with the host as its last attacker, and own-side lords may then defend it against the host. TAOM's Refuge raid skips a host altogether (`IRaidHostOwnSideQuery.IsHost`, #1033), and a host skips an anchor held by another side or by a faction with no kingdom whenever the owner can be read (see Host choice). |
| Engage strength | The party's strength is its army's whole strength when it leads an army (vanilla's own rule, `DefaultMobilePartyAIModel`: a battle with the leader pulls the army in), else its own roster's. Parties the engine ignores (`ShouldBeIgnored`, which `CanPartyInteract` refuses) and any party in a siege camp (vanilla weighs a besieger by its whole camp) are never candidates. |
| Ending | It turns home on its quota, after `maxHostDays`, or when no raidable village is left. A returning host is dismissed within `homeRadius` of its hideout (or where it stands after twice `maxHostDays`). |
| Counting | A raid counts when `VillageStateChanged` reports the Looted state with the host as raider. The event fires inside the finishing raid, so the count is recorded and the next hourly step acts. |
| Switched off | A host whose side (or the master switch) is off is despawned at its next hourly step. While it is in a battle the engine will not remove it, so its record and pin stay and the next hour tries again, as a returning host's dismissal does; only a removed or lost party drops the record. Toggling a side off and on during a battle therefore cannot produce a second host. |
| Sizing | A host is built with no party template, so bandit scaling (Patch39) and the bandit density settings do not size it. It counts as one party in its clan's `WarPartyComponents`, the count vanilla's `SpawnBanditsAroundHideout` subtracts from the clan's quota, so it takes one slot of its clan's vanilla spawn quota. |
| Budget (proposed) | The hourly step is under 1 ms per campaign hour for all hosts together, the budget Watchful Holds keeps. `maxHourMs` in the daily debug line is its measure. The daily roll and the spawn are not timed. |

## Tribute envoy

| Rule | Detail |
|---|---|
| Schedule | When a host is created and the player's clan belongs to its target kingdom, its envoy is due `rand(envoyMinDelayHours..envoyMaxDelayHours)` (24 to 48 by default) campaign hours later. Nothing is scheduled with the "Tribute Envoys" switch off, an unreadable clock, or a player outside that kingdom. One envoy per host, and it lives on the host's record, so a host that goes home or breaks takes it along. |
| Shown | From the due hour the hourly step shows the popup once nothing blocks it, else it tries again next hour and nothing is lost. A host whose side or the master switch is off is skipped (its record can linger while the engine will not remove it). Blockers, checked in this order: no readable player, the player's clan is in another kingdom than the host's target, the player is enlisted (`IEnlistmentStateQuery.IsEnlisted`), the clan holds no village (the count comes with the player reading, so waiting costs no scan of the map's villages), the host is in a map event (it may be raiding one of the clan's villages), the map is not free (`MapPopupGate.IsMapFree`: a main party, no encounter, no menu, no inquiry open). A host that has turned for home settles its envoy without asking. At most one popup is open at a time, because the map is not free while it is; a second due envoy waits. A popup that closes without an answer (a load, the engine hiding the inquiry) runs no callback and comes back next hour. The click handler holds the host's record and ignores the click unless that record is still in the live state, so a click on a popup from before a load, even of the same save, does nothing. |
| Price | `units = max(1, (villages + 1) / 2)` with integer division (1 or 2 villages: 1 unit, 3 or 4: 2, 7 or 8: 4), `price = units x envoyUnitPrice x (1 + envoys seized)`, capped at `int.MaxValue`. The villages are those bound to the fiefs the clan holds (`Clan.Fiefs`, then `Town.Villages`, summed in `TributeEnvoyAdapter`). The first quote stores the village count and the price; a delay keeps both. The popup and the console dry run share one quote expression. |
| Pay | Takes the price through `ICareerPurseAdapter` (the one debit path). The payment takes effect at once: the engine's party AI is told to leave the player's party alone for twice `maxHostDays` (`MobilePartyAi.SetDoNotAttackMainParty`, the call vanilla's safe-passage bribe makes, through `IRaidHostAdapter.SpareMainParty`), and the host is steered again outside the hourly step, as a spawn is, so an engage order on the main party or a raid order on a village now spared is replaced in that moment. The host then raids no village of the player's clan and never engages the main party or the leader of an army that carries it; other parties of the clan are still engaged. With too little gold, or no readable clan, nothing is taken, a line says so and the popup returns. |
| Haggle | Offered from 4 units, once per host. One roll: `NextDouble() < clamp(haggleBaseChance + haggleChancePerCharm x Charm, haggleMinChance, haggleMaxChance)`. A win cuts the price to `ceil(0.75 x price)`. Either way the popup returns with the price and without the haggle. |
| Ask for time | The envoy returns `envoyDelayDays` (7 by default) days later at the same price. Nothing is spared meanwhile. |
| Send away | The envoy leaves; the host raids as before. |
| Seize | The envoy is settled, `envoys seized` goes up (every later tribute costs one more full price), and the side's grudge is set to the player's kingdom: the grudge is per side, so the next host of that side (all five Shadow hosts share one) rides against it first. |
| Grudge | `Grudges[side] = kingdom`. The side's next host picks that kingdom first, then its `preferredTargets`, then the strongest. It is spent by the host that rides, whether or not that host could use it (the target must still be one of its `targetSides` with a raidable village). A dry run of the console spawn shows it and spends nothing. |
| Spared | Stored as the clan id on the host record. `FireAndTributeMath.IsRaidable` and `NearestRaidable` skip every village the clan owns, so a standing raid order on one is replaced at payment (see Pay). A raid already under way (the host is in a map event) finishes first. |
| Single-player | The schedule, the popup and the click all stand down with a co-op session, an unprobed co-op mod or a dedicated server, like the hosts. |

## Architecture

```
fire_and_tribute.json   MCM (5 switches)
        |                    |
FireAndTributeConfigProvider  FireAndTributeSettingsProvider
        \                   /
         FireAndTributeService --- FireAndTributeMath (pure)
          /      |       |       \          \
 Behavior  IRaidHostAdapter  IFireAndTributeNoticeAdapter  ITributeEnvoyAdapter, IEnvoyPresenterAdapter
 (5 events, (spawn, roster,   (message, horn)               (player clan/kingdom/gold/Charm, payment;
  one key)   pin, orders,                                    popup, result lines, map-free test)
             villages)
```

- A feature module (`FireAndTributeModule`, one line in `Main/Composition/FeatureModules.cs`; `Main/IoC.cs` and `Main/SubModule.cs` are untouched). It owns save data, so a fault fails the campaign closed.
- A host is a party of an outlaw clan from `characters/clans.xml`, created with `BanditPartyComponent.CreateBanditParty` and the nearest hideout of the clan's culture as its home. A null home would crash vanilla's per-hideout bandit count, so a host is never created without one. Its roster is filled after creation and its AI is pinned (`SetDoNotMakeNewDecisions(true)`): vanilla's hourly think never re-plans it, so its orders are the service's, while the engine's initiative still applies around the settlement it targets. It appears at a point the engine finds near the anchor's gate (the gate itself when it finds none), which is then retried 15 times around that point for one out of the player's sight (`CreatureBandParties.OutOfPlayerSight`, shared with the creature bands); it can land up to about twice the spawn radius away, and a point in sight is kept when every retry fails, so a host can appear in sight.
- The service sees parties as ids; the adapter resolves them against the live campaign on every call and keeps no state: no `Campaign`, no `Village` (a village reaches the whole campaign graph). It hands the service the village readings (`ReadVillages`, one `VillageReading` per village with its id, position, owner kingdom, state and hearth), and `FireAndTributeMath` holds the raidable rule and the nearest-village selection. An IL test pins that the adapter holds no `Campaign` or `Village`, directly or through another type.
- Reuse: `HuntMath.PlanTopUp`, `IKingdomWarSnapshotAdapter` (strength and `GetNowHours`, from the War Chronicle module), `IAlignmentService.ResolveSide`, `IWarOfTheRingService`, `IRandomSource`, `IEnlistmentStateQuery`, `ICareerPurseAdapter` (gold read and debit), `MapPopupGate.IsMapFree`.
- The service is split by concern into partial files (ADR-003 amendment): `FireAndTributeService.cs` (fields, constructor, lifecycle, status), `.Spawn.cs`, `.Steering.cs` and `.Envoy.cs`. They share the saved record and the transient counters.

## Configuration

### Config file: `Main/_Module/ModuleData/fire_and_tribute/fire_and_tribute.json`

Read once per game session: an edit needs a full restart. A missing file gives the compiled tuning and no hosts with a warning, a parse failure the same with an error. An invalid tuning field reverts alone to its default (an ordered pair reverts together), an unknown key is warned about and ignored, and an invalid host is skipped with a warning. Every float goes through `FiniteFloatValidator` first.

| Field | Shipped | Valid range |
|---|---|---|
| `firstHostDelayDays` | 45 | 0 to 3650 |
| `spawnChance` | 0.05 | 0 to 1 |
| `cooldownDays` | 120 | 0 to 3650 |
| `warlessStartDay` | 89 | 0 to 3650 |
| `minTroops` / `maxTroops` | 80 / 200 | 1 to 2000, min at most max |
| `strengthShare` | 0.012 | above 0, at most 1 |
| `strengthPerRaid` | 2000 | above 0, at most 10000000 |
| `quotaMin` / `quotaMax` | 6 / 15 | 1 to 100, min at most max |
| `quotaExtraMin` / `quotaExtraMax` | 0 / 3 | 0 to 100, min at most max |
| `minHearth` | 150 | 0 to 10000 |
| `nearestVillages` | 3 | 1 to 10 |
| `engageRadius` | 8 | above 0, at most 60 |
| `engageStrengthRatio` | 0.8 | above 0, at most 5 |
| `maxHostDays` | 30 | 1 to 365 |
| `homeRadius` | 4 | above 0, at most 50 |
| `spawnRadiusDays` | 0.25 | above 0, at most 3 |
| `envoyUnitPrice` | 500 | 1 to 1,000,000 |
| `envoyMinDelayHours` / `envoyMaxDelayHours` | 24 / 48 | 1 to 720, min at most max |
| `envoyDelayDays` | 7 | 1 to 60 |
| `haggleBaseChance` | 0.20 | 0 to 1 |
| `haggleChancePerCharm` | 0.002 | 0 to 1 |
| `haggleMinChance` / `haggleMaxChance` | 0.10 / 0.70 | 0 to 1, min at most max |

Each host has `id`, `side` (`Free`, `Neutral` or `Evil`), `clan` (one of the eight outlaw clans), `name` (`{=taom_fat_host_<id>}` plus English text, the key carrying its own id), `anchors` (town or castle ids on the TAOM map, in realms of the host's own side; at most 12), `targetSides` (never `Neutral`, never its own side), optional `preferredTargets` (kingdom ids, at most 12) and `troops` (`id` and `share`, shares summing to 1, at most 12). The file is read as UTF-8. The troops of each shipped host are the stacks of its clan's `*_raider_party_template` in `taom_partyTemplates.xml`.

### Why these defaults

Kingdom strength (`Kingdom.CurrentTotalStrength`, the `str` key of the `[WarLedger]` lines in two real runs to day 592, 17,989 kingdom-days) has the 5th, 25th, 50th, 75th and 95th percentiles 2.8k, 7.2k, 11.2k, 17.3k and 28.3k. At `strengthShare` 0.012 the host's strength is 33, 86, 135, 208 and 340; troop power for tier 2 to 4 is 0.96, 1.30 and 1.68, so a median host is about 100 men and everything below the 25th percentile is lifted to 80, everything past the 75th is capped at 200. At `strengthPerRaid` 2000 the quota is 1, 4, 6, 9 and 14 raids before the floor and the extra, so 6 to 15 in practice. The troop mix and village `hearth` thresholds are not measured: balance runs decide them.

The envoy numbers are first guesses too, not measured. At 500 denars a unit a clan with 3 villages pays 1,000 and one with 7 pays 2,000, and each seized envoy adds one full price. The haggle chance is 20% at Charm 0, 40% at Charm 100 and the 70% cap from Charm 250, so Charm decides it without ever making it certain; the 10% floor never binds at the defaults (Charm 0 already gives 20%, and a negative Charm counts as 0); it only keeps a configured base chance below 10% from closing the haggle. A balance run with real clan holdings decides the price.

### MCM settings

Group "World/Fire and Tribute" in `Main/Features/TaomSettings.cs`. All five apply at once, no restart, and are counted in the co-op settings fingerprint.

| Setting | Property | Default |
|---|---|---|
| Enable Fire and Tribute | `EnableFireAndTribute` | on |
| Free Peoples Hosts | `FireAndTributeFreeHosts` | on |
| Neutral Hosts | `FireAndTributeNeutralHosts` | on |
| Shadow Hosts | `FireAndTributeEvilHosts` | on |
| Tribute Envoys | `FireAndTributeEnvoys` | on |

A host alive when its switch goes off is despawned at its next hourly step, or at the first hour its battle is over if it is fighting then. The Free switch covers the outlaw bands of Gondor and Mirkwood, the Shadow switch Gundabad, Rhûn, Harad, Dunland and the Blacklocks of Erebor, the Neutral switch the Corsair Host of Umbar. "Tribute Envoys" off schedules and shows no envoy (one already scheduled waits, and an open popup can still be answered); the hosts themselves are unaffected.

## Co-op, save data and IoC

- **Singleplayer only.** The spawn, the steering and the console spawn stand down while a co-op session is live, while a co-op mod TAOM cannot probe is loaded, or on a dedicated server. The village and destroy handlers are not gated and do nothing there, because the records are emptied when a session goes live. A host restored into such a session has its pin released and its record dropped, never despawned, and its side rests for `cooldownDays` so the session that ends cannot be followed by a second host at once. See the `FireAndTributeBehavior` row in [coop-interop.md](coop-interop.md).
- **Save data:** one `SyncData` key, `_taom_fire_and_tribute_v1`, a JSON string: the first-seen day of Full War, each side's cooldown, and each live host (party id, host id, side, target, start day, raids, quota, returning). Slice 2 added keys without a version bump, written only when they hold something: per host `due` (campaign hour), `done`, `vil` and `price` (the stored quote), `hag` (haggled) and `sp` (the spared clan id), and at the root `grudge` (side to kingdom id) and `seized`. A slice 1 save has none of them and reads as no envoy, no grudge, none seized, and writes back byte for byte as it was. A bad envoy key resets that key alone with a warning and never drops the host row. A delayed envoy has no field of its own: it is `due` moved a week on. Nothing about the popup is saved or kept in memory: a popup open at a save is gone after the load and comes back at the next hourly step, and a click on one from before a load is ignored because its record is no longer in the state. Parsing is forgiving per section and per row; a save from before the feature stays reset. Side names parse by name through `EnumNames.TryParse` (any case, never a number). A dropped host row, or a record dropped for an unknown version, leaves its party on the map still pinned (the engine saves `DoNotMakeNewDecisions`); the warning in the log says so, and any future version bump must migrate v1 rows, never reset them. A slice 1 build that loads a slice 2 save ignores the new keys.
- **Session reset:** the behavior's constructor calls `ResetForNewSession`, and the module builds the behavior inline for every campaign start.

## Diagnostics and console

A daily debug line: `[FireAndTribute] day N: hosts=..., spawned=..., raids=..., maxHourMs=...`. `maxHourMs` is the slowest hourly step of the day.

| Command | Tier | Use |
|---|---|---|
| `taom.print_fire_and_tribute` | A | Switches, the spawn window (the one the next daily roll would use, noting a Full War not yet seen without recording it), the envoy switch and seized count, any grudges, each side's host and cooldown with its envoy (due hour and countdown, stored quote, haggled, settled, spared clan), today's counters |
| `taom.spawn_raid_host <free\|neutral\|evil> [confirm]` | C | Dry run without `confirm` (who rides, from where, against whom, men, quota). With `confirm` it ignores the phase, the chance and the cooldown; the switches, single-player and one host per side still hold |
| `taom.set_envoy_due [confirm]` | C | Sets a live host's envoy due now. Dry run without `confirm` (the quote, and what blocks it: another kingdom, enlisted, no village, the host in a battle, map busy, which an open popup also causes). With `confirm` it sets the envoy due at once and shows the popup if nothing blocks it, else reports the blocker and leaves it to the hourly retry. A settled envoy, a host going home, a host whose side is switched off, the switch and single-player refuse it |

## Key files

| File | Purpose |
|---|---|
| `Main/Features/FireAndTribute/FireAndTributeService*.cs` | Every rule: lifecycle, spawn, steering, events, and (`.Envoy.cs`) the tribute envoy |
| `Main/Features/FireAndTribute/FireAndTributeService.OwnSide.cs`, `IRaidHostOwnSideQuery.cs` | The own-side snapshot the party AI model reads (`Main/Features/UnlookedForAid/Models/TaomMobilePartyAIModel.cs`) |
| `Main/Features/FireAndTribute/FireAndTributeMath.cs` | Target picker (grudge first), raidable-village rule and selection (sparing a clan), size, quota, spawn window, cooldown, tribute units, price, haggle chance and cut, due hour |
| `Main/Features/FireAndTribute/FireAndTributeConfig*.cs` | Config model and validating loader |
| `Main/Features/FireAndTribute/FireAndTributeSaveCodec.cs`, `FireAndTributeState.cs` | The save record |
| `Main/Features/FireAndTribute/Hooks/FireAndTributeBehavior.cs` | Thin router and SyncData |
| `Main/Features/FireAndTribute/Cheats/FireAndTributeCheats.cs` | The three console commands |
| `Main/Adapters/RaidHostAdapter.cs`, `IRaidHostAdapter.cs`, `RaidHostTypes.cs` | Spawn, roster, pin, orders, villages, despawn |
| `Main/Adapters/FireAndTributeNoticeAdapter.cs` | The messages and the horn |
| `Main/Adapters/TributeEnvoyAdapter.cs`, `ITributeEnvoyAdapter.cs`, `EnvoyPresenterAdapter.cs`, `IEnvoyPresenterAdapter.cs`, `EnvoyTypes.cs` | The player reading (clan, kingdom, gold, Charm), the payment, the popup and the result lines |
| `Main/_Module/ModuleData/fire_and_tribute/fire_and_tribute.json` | Tuning and the eight hosts |

## Tests

MSTest with NSubstitute and a hand-written engine fake, in `TAOM.Tests/Features/FireAndTribute/`: config validation (one test per rule), the data against ModuleData and the live map, the pure math, the save codec, the service (spawn gates and timers, every steering branch, event counting, every end path, co-op stand-down), the console parsers, the session reset and wiring, and IL pins on the adapters. Slice 2 adds the envoy math, the save codec (a slice 1 save, every bad key), the envoy service (schedule, every blocker, each option, the grudge picker and its spending, the spared clan in raid picking and engaging, a payment's immediate effect on the host, the timers, the single-player gate, the console command and the status lines), every envoy config rule, and the MCM provider. `FireAndTributeOwnSideTests` pins the own-side snapshot: each side, the host clan's other parties, every end path (broken, home, retired, lost), reset, load, co-op release and a kingdom resolved after the spawn; `FireAndTributeWiringTests` pins that the model's query is the behavior's own service instance. The review follow-ups add tests for anchors held by another side or a rebel clan (#1032), the Refuge raid's host skip (#1033), every form of the broken line (#1034), and `RacePersistenceWiringTests` with the race reset tests (#1035).

## Still owed

- **In game:** force each side from the console, watch a host raid, fight it, save and load mid-raid, switch the feature off mid-host, and check the destroy path of a host killed in a battle.
- **Balance:** two fast-forward runs to day 200 from one save, hosts on and off, compared with `tools/analyze_war_ledger.py`.
- **Budget check from `maxHourMs`:** read the slowest hourly step of the day from the daily debug line over a long run and compare it with the proposed 1 ms budget.
- **Translation (done 2026-11-12, #763):** the 21 `taom_fat_envoy_*` keys (title, body, five options with hints, answer button, seven result lines) are registered in `taom_module_strings.xml` and translated in the 12 language files. The five slice 1 message keys and the nine `taom_fat_host_*` names are translated in the same pass.
- **In game (slice 2).** Setup: join the target realm with `campaign.join_kingdom`, give the clan villages with `campaign.give_settlement_to_player` (7 or more villages gives 4 price units, which is what shows Haggle), send a host with `taom.spawn_raid_host evil confirm`, then `taom.set_envoy_due confirm`. Check: the look of the popup (title "{HOST} Demands Tribute", five options, disabled Pay with its purse hint, no cancel button, the game pauses); the popup reopening after Haggle; each option in turn; pay and watch the host drop an engage order on the main party or a raid on one of the clan's villages in that same moment, then skip them; seize and watch the side's next host choose the player's kingdom; Ask for time and the return; save and load after Seize and after Ask for time (the grudge and the due hour survive), and with a popup open (it returns at the next hour); an enlisted player; a busy map (menu, encounter); a host in a battle; a second due envoy behind the first.
- **Balance:** the unit price, the haggle numbers and the 24 to 48 hour default wait are first guesses (see "Why these defaults").
- **In game (own side).** Fast-forward a campaign past day 89 (Full War on day 44, plus the 45-day `firstHostDelayDays`) with hosts on, then read the log: hosts should log raids (`went home after N/M raids` or raids counted in the daily line) instead of `was broken after 0/N raids`, and no own-side lord should be seen chasing a host on the map.
- **In game (follow-ups).** Capture one of a side's anchors with the other side, then `taom.spawn_raid_host <side> confirm`: the host should muster at another anchor (#1032). With refuge raids on, park a refuge near a host: it should not be raided by it (#1033). Read the broken lines of the next run for their destroyers (#1034).

## Changelog

- 2026-10-20: slice 1, the raiding hosts (#870).
- 2026-10-21: review fixes: Blacklocks and the Gondor host re-anchored, singular host names, the `regions` key renamed `anchors`, army leaders weighed by their army, ignored parties and non-leader besiegers skipped, the village cache removed (villages read live, the raidable rule moved to the math), retirement keeps a fighting host tracked, the grudge removed until slice 2.
- 2026-10-21: slice 2, the tribute envoy (#870): schedule, popup, Pay, Haggle, Ask for time, Send away, Seize, the grudge (state, save section and target preference) and the spared clan.
- 2026-10-21: slice 2 review fixes: a payment steers the host again at once and calls `SetDoNotAttackMainParty`; a spared host also leaves the leader of the player's army alone; the envoy skips a host whose side is off and waits while its host is in a battle; the open-popup latch and the session counter are gone (the map-free test carries it, and a click holds its record); the clan's villages are summed over its fiefs in the player reading; one quote expression; the console command is `taom.set_envoy_due`; the MCM label is "Tribute Envoys"; the texts say "side", open with the host and pluralise days.
- 2026-10-27: the own side leaves its host alone (`TaomMobilePartyAIModel`, `IRaidHostOwnSideQuery`), after a run where all seven hosts were broken by their own side before a raid.
- 2026-10-27: review follow-ups: anchors held by the other side are skipped at spawn (#1032), Refuge raids skip hosts (#1033), the broken line names the destroyer and its side (#1034).

## GitHub Issue

- **Issue:** #870, Fire and Tribute: raiding hosts for Good, Neutral and Evil, with a tribute envoy
- **Status:** Shipped on `bannerlord-1.5.x` (slice 1 `21e0807f9`, slice 2 the envoy `c3246f607`, v2.0.35). Issue open: the in-game checks, balance runs and the MapPerf budget check are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/dev-console.md](./dev-console.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
