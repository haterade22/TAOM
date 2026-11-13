# Far Watch

## Overview

Far Watch sends a scout out from the raised field camp. The camp panel gains one option, **Send a scout** (**Ride out to scout** when the player travels alone; **Await your scout**, or **Continue the ride** for a solo ride, while a ride runs). A ride either sweeps the surroundings or looks over a nearby town or castle. A surroundings ride raises the party's sight while it runs, hands the player a report of the hosts in sight when the scout returns, and leaves a wider sight behind it for a day or two. A settlement ride lowers sight while the scout is away and reports the garrison, the militia and each lord party inside. Since #847.

## Why This Exists

- **Vanilla behavior:** sight is a number on the party (`MobileParty.SeeingRange`, base 12 by day and 6 by night, widened by the Scouting skill and a few perks, hard-capped at 60). The only way to learn more about the world is to ride toward it.
- **TAOM requirement:** the camp feature (Field Camp, #506) holds the party still, and Hidden Hosts (#846) now blurs troop counts with distance. A standing camp needs a way to look around without breaking camp, and Scouting needs a use that is more than a passive number.
- **Without this feature:** a lookout widens sight by a fixed share and nothing else; the player cannot choose what to learn, cannot send somebody to a town, and a strong scout wastes his skill against the 60-unit cap.

## Player flow

1. With a raised camp of any type, the camp panel (`taom_fc_camp`) shows the option below Host Review and above Leave. It is greyed with the reason while the camp is still being raised, no scout is named, the scout is wounded, the scout is resting (with the time left), an encounter or battle is live, or the campaign is shared. It is hidden only when the Field Camp feature is off and no ride runs. The panel is refreshed in place when a rest or a ride ends while it is open, because a menu runs an option's condition only when it is built or refreshed (the hourly tick calls `IGameMenuAdapter.RefreshCurrent`).
2. **Who rides:** the hero in the party's Scout role (never the leader by fallback), if he is in the party, alive, not a prisoner and not the main hero. A party of only the main hero rides out himself.
3. The option opens the pick, a plain menu (time stands while choosing): **Sweep the surroundings**, up to 8 towns and castles within 50 map units of the party (gate to gate), nearest first, each with its compass direction, and **Back to camp**. The player clan's own fiefs are not offered.
4. A choice starts the ride and opens the wait menu (progress bar and hours, time runs) naming the scout and the target, with one option, **Call {SCOUT} back** (**Ride back to camp** for a solo ride).
   - Length: a surroundings ride takes 8 hours less 4 for a full 300 Scouting (`8 - 4 x skill/300`, 4 to 8 hours). A settlement ride takes 70 percent of that (2.8 to 5.6 hours).
5. While a **surroundings** ride runs, the party's sight rises in a straight line to +50 percent of its base. While a **settlement** ride runs, sight is 35 percent of base lower and Hidden Hosts reads the party's Scouting as 0 for how far digits reach.
6. On time the wait ends, the player lands on the camp panel (a wait menu, so time keeps running) and a report opens in a paused inquiry:
   - **Surroundings:** up to 6 hosts (army leaders first, then lords, then the rest, nearest first). Each row is `{NAME}: {COUNT} ({KIND}), {DIRECTION}` (`{NAME}` is the name the party's map plate prints: the army's for a leader, else the lord's, else the party's own; an army leader's kind counts every attached party): the count as the scout now knows it (the Hidden Hosts mask, or the exact number with the fog off), what the host is mostly made of (foot, archers, horse, horse archers), and a compass word. Every host in sight that is not exempt (not a villager, caravan, party attached to an army, party inside a settlement, the player's own side, or a party the engine hides; an army member that has not yet attached still moves alone and counts) gains 1 known digit (2 at Scouting 150 or more); the 6 listed also keep their names past the horizon until the raised sight is over. The last line, "Your party sees farther for about {TIME}", is the hours the raised sight holds at full strength, 24 to 48 (`24 x (1 + skill/300)`), and then fades over half as long again.
   - **Settlement:** "{SETTLEMENT} is held by {COUNT} men:" with the total (exact), then the garrison (its count and what it is mostly made of), the militia (a count only) and each lord party inside by name (count and what it is mostly made of). A party with no healthy men is not listed. Hidden Hosts remembers that settlement's defenders exactly from that hour.
   The scout earns `150 + 0.5 x skill` raw Scouting XP (learning rate and focus apply, `HeroDeveloper.AddSkillXp`) and rests 8 hours.
7. **Call back, any encounter with the camp (a battle or a peaceful meeting), the camp being struck or Field Camp being switched off, the scout leaving the party, or a shared campaign** end the ride early: one message, no report, no XP, the 8-hour rest. A surroundings ride keeps the sight it had reached, which then holds and fades on the normal schedule.

### Constants

| Constant | Value | Where |
|---|---|---|
| Surroundings length | `8 - 4 x clamp(skill, 0, 300)/300` hours | `FarWatchMath.RideHours` |
| Settlement length | 0.7 of that | same |
| Sight ramp (surroundings ride) | `0.5 x` fraction of the ride done | `FarWatchMath.Ramp` |
| Settlement penalty | `-0.35` of base sight while the ride runs | `FarWatchMath.SettlementPenalty` |
| Kept sight | level 0.5, holds `24 x (1 + clamp(skill, 0, 300)/300)` h, fades linearly over half the hold | `HoldHours`, `BuffAt` |
| Named hosts keep names until | return hour + hold + hold/2, or the end of the fade of a longer full-strength buff already held (a weaker scout's return never shortens it: it is kept as it is and the report says the hours left) | `FarWatchService.RaiseSight` |
| Digit stages added by a report | 1; 2 from Scouting 150 | `FarWatchMath.Stages` |
| XP | `150 + 0.5 x clamp(skill, 0, 1000)` raw | `FarWatchMath.Xp` |
| Rest after every ride | 8 hours | `FarWatchMath.RestHours` |
| Pick | at most 8 settlements within 50 units, nearest first | `MaxSites`, `SiteRadius` |
| Report rows | at most 6 | `MaxRows` |
| Compass | 8 sectors of 45 degrees, +Y north, a boundary belongs to the next sector clockwise | `CompassSector` |

All of them are constants. There is **no MCM setting**: FieldCamp's master toggle covers the feature, and later sliders would each need the provider rules (NaN, range, sign, warning, summary, hot-path gate).

## Architecture

### Design Challenge

- The sight number is capped inside vanilla (`DefaultMapVisibilityModel.GetPartySpottingRange` ends in `LimitMax(MaximumSeeingRange())`, 60), so a factor added after the base call is still clamped. From about Scouting 250 with perks, or with a strong lookout, the cap eats part or all of the buff. See "The seam".
- A ride is timed work, so it needs a wait menu, with every trap of the field camp: the condition must be the literal `true` (#567), only an isLeave option gets `EndWait` before its consequence, and an exit needs a landing.
- The ride can be pre-empted from outside its menu (an attack on the camp, a load, a forced exit), so the state lives in a store keyed to absolute campaign hours and every path that ends it goes through the service.
- Hidden Hosts must change in three small ways (scout away, named past the horizon, the raise and the settlement stamp), without a second copy of any fog rule and without Hidden Hosts depending on this feature.

### Solution Approach

A feature module (`FarWatchModule`, one line in `FeatureModules.cs`, listed after Host Review). No Harmony patch, no `IoC.cs` or `SubModule.cs` edit.

- `FarWatchService` holds every rule. Plain data in and out; engine reads go through `IFarWatchWorldAdapter`, the digit and memory rules through `HiddenHostsService`. The report half is `FarWatchService.Sweep.cs`.
- `FarWatchStore` is the plain state (the ride, the kept sight, the rest, the named hosts) and implements `IScoutIntelQuery`, the read-only seam Hidden Hosts consumes.
- `FarWatchSightContributor` is the boundary of the sight seam: an `IPartySpottingContributor`, registered beside the lookout.
- The hooks (`Hooks/`) are thin: the behavior, the camp option, the pick menu, the wait menu, the reconcile (hourly look, encounter and map-event listeners), the landing, the presenter and the two text classes. Every hook file is under 150 lines.
- The adapter (`FarWatchWorldAdapter`) reports facts only and keeps no engine object. The notifier (`FarWatchNotifier`) is `InformationManager.ShowInquiry(pause: true)` and `DisplayMessage`.

### The seam: scout sight through `IPartySpottingContributor`

The existing seam took one member (`GetSpottingRangeBonusFactor`) and ignored negative or non-finite values, and its factors could never lift the cap. It now has a second member, `GetSpottingRangeCapBonus(MobileParty)`, and the model's contributor loop moved into `SpottingContributorComposer` (`Main/Core/Domain`). The rules:

| Rule | Value |
|---|---|
| Factors from every contributor share one sum | no upper cutoff, as the lookout always had |
| A negative factor is a penalty | all penalties together floor at `-0.5` |
| NaN and infinite values | add nothing |
| A sum that overflows to infinity | becomes 0 |
| Cap bonus | the largest bonus offered, never the sum, at most `MaxCapBonus` = `0.29` (60 to 77.4); the scout asks for 0.5 and is clamped |
| Model body | `base`, career passives, then `Apply`: `AddFactor(sum)` and a second `LimitMax(MaximumSeeingRange() x (1 + capBonus), label)` |

With no cap bonus the second `LimitMax` writes the same 60 vanilla wrote. The label is vanilla's own `{=6qv6Hdww}Limit` only when descriptions are asked for, so the tooltip line keeps its name; `Compose` allocates nothing per query and `Apply` allocates the label text only for a described number (both pinned). The composer adds no line of its own to a described number: the lookout never had one (the old loop added its factor without a description) and the interface carries no label to print, so a described number shows vanilla's lines, the career passives' and the cap. The lookout returns 0 for the cap member; its behaviour through the composer is pinned by the `Apply` rows of `SpottingContributorComposerTests`, written and run green against the old loop first.

**Contributors today:** the lookout (`+0.2 + Scouting/200`), the scout (`+0.5` and `-0.35`) and Under the Eaves' forest penalty (`-0.3` for the main party in forest outside a settlement, half of that with Keen Sight, [under-the-eaves.md](under-the-eaves.md)). The scout's settlement penalty and the forest penalty share the `-0.5` floor: `-0.35` and `-0.3` together are `-0.65`, floored to `-0.5`, which is 9 from a base of 18 (`SpottingContributorComposerTests` pins it). Forest asks for no cap bonus.

**Factors are shares of base sight, not of the total.** The scout's `+0.5` and `-0.35` add to the same `SumOfFactors` as the lookout and the perks. Example: scout 150 with a lookout (+0.7) on a settlement ride: `21 x (1 + 0.2 + 0.7 - 0.35) = 32.6`, against 39.9 without the ride, so the ride costs 18 percent of the total, not 35.

**Where the cap binds** (base 12 by day plus 0.06 per Scouting point, perk factors from `DefaultPerks`):

| Case | Base | Factors | Without scout | With +0.5 buff |
|---|---|---|---|---|
| Scout 150, Day Traveler and Water Diviner, no lookout | 21 | 0.2 | 25.2 | 35.7 |
| Scout 150, same, lookout, leader Scouting 100 (+0.7) | 21 | 0.9 | 39.9 | 50.4 |
| Scout 300, all five day perks (0.65), no lookout | 30 | 0.65 | 49.5 | 64.5, capped to 60 without the raise, 64.5 with it |
| Scout 300, all perks, lookout, leader Scouting 300 (+1.7) | 30 | 2.35 | 100.5, already 60 | 77.4 with the raise (the ceiling) |

**What a higher cap costs, and what the ceiling of 77.4 is.** `MobileParty.SeeingRange` recomputes through the model on every read. Its readers: the visibility pass on the map every frame (`CampaignTickCacheDataStore.UpdateVisibilitiesBasedOnPoint` searches the locator grid with `radius = seeingRange` and runs the party and settlement checks per hit), the map ambient sound (`MapScreen`), the deserter spawn keep-out (`DesertersCampaignBehavior`, line 226) and the Extortion by Deserters issue, the bandit spawn keep-out (`BanditSpawnCampaignBehavior` retries a spawn point that falls inside the player's range), minor-faction recruitment out of sight (`RecruitmentCampaignBehavior`, line 307), the parley distance (`DefaultEncounterModel.PlayerParleyDistance`, line 38; read for a settlement and, from v1.5.6, for a lord party in `CanMainHeroDoParleyWithParty`, where Hidden Hosts refuses a host it masks, #1037), hideout spotting at 1.2 times the range (`DefaultMapVisibilityModel.GetHideoutSpottingDistance`), the Scout Enemy Garrisons quest (a settlement counts as scouted while it is within the range), and the party-item and map-info view models. More parties become `IsVisible`, so more nameplates draw (each pays the Hidden Hosts prefix and finalizer). The engine's locator grid is 32 by 32 cells of 5 map units. `LocatorGrid.GetBoundaries` takes a span of `floor((x + r) / 5) - floor((x - r) / 5)` cells per axis, caps it at 31, and wraps the first cell by modulo, so past r of about 75 most positions walk a whole axis, and a full walk visits nearly every party. There is **no step** in that cost: it grows smoothly with the radius, and vanilla already searches the same grid at `SeeingRange + 25` (85 at the 60 cap) every frame for the footstep sounds (`MapScreen.TickStepSounds`). `SpottingContributorComposer.MaxCapBonus` is therefore `0.29` (60 x 1.29 = 77.4): a judgement that bounds the extra per-frame cost of a raised sight, one clamp in the composer for every contributor, not a measured threshold. **An in-game A/B of frame time at 60 and at 77.4 is owed.** The facts are pinned against the installed engine in `FarWatchEngineFactsTests`. Sight below the cap still gets the scout's whole +50 percent. (The sweep's own one-off search uses 1.01 times the range, once per returned ride, not per frame.) The raise is bounded to the buff, to the main party and to that ceiling. `MaximumSeeingRange()` has exactly one call site in `TaleWorlds.CampaignSystem.dll` (`DefaultMapVisibilityModel.GetPartySpottingRange`; the pin scans that assembly's method bodies, not the other modules').

### Hidden Hosts

Everything goes through `HiddenHostsService`; no second copy of a fog rule. The service now takes an `IScoutIntelQuery` (a test double, `NoScoutIntel`, stands in for it in the Hidden Hosts tests; it is never shipped).

1. **Scout away, reach only.** While a settlement ride runs, `FogSkill` reads Scouting as 0 at the five `ScoutMultiplier` sites (nameplate, party, settlement and battle tooltips, the settlement party row) and in the live reading of `RaiseByScout`. How long a count is held and how fast it fades keep the real skill, and the adapter keeps reporting the raw one.
2. **No scout-report multiplier in the fog stack.** The thresholds are fractions of `SeeingRange`, so the raised sight already scales every threshold and the horizon in absolute distance; a fog multiplier would apply the buff twice.
3. **Named past the horizon.** A host the last report listed keeps its name, its encyclopedia hotkey and its tooltip title past the horizon, not its orders (those read "Orders unknown.", #848), while its count stays `...` (decision Q5 of Hidden Hosts unchanged). One predicate, `IsNameHidden`: at the horizon and not named. `NameplateIntel.HidesName`, `TooltipVerdict.Named` and `TooltipFrame.RenamesTitle` follow it. `IsNamedByScout(partyId, nowHour)` is a scan of at most 6 ids with ordinal compares, asked only at the horizon branch.
4. **`RaiseByScout(snapshot, stages)`.** The live known digits as `Evaluate` computes them (the horizon gives 0; the latch is read, never written), `raised = min(digits, max(live, remembered) + stages)`, where a remembered count counts only if it agrees with the live one (same number of digits and the same leading digits, `HiddenHostsMath.Agrees`: a row from another size of host is ignored), written to the memory so the nameplate later shows `~12?`. It returns the text to print. No Scouting XP (the ride pays its own). With the fog off, no clock, or an own-side or engine-hidden host, it returns the whole number and writes nothing.
5. **`RecordScoutedSettlement(snapshot)`.** The exact defenders, with the same gates as the stamp of a settlement the player is in (toggle, usable clock, own side) but for any settlement; the row starts on the out-of-sight clock (stamp plus hold), and the hourly poll moves it back to the sight clock if the settlement is inspected.

**A known limit, small.** The report reads the intel adapter's half-second caches (the main party's seeing range and each party's spotting ratio). A surroundings ride raises sight in a straight line, so by the time it returns the range is already at its full height and the cache lags it by at most half a second of nothing; the digit reach of the report is the new range's. The one place a stale range could show is the first half second after a settlement ride ends (its penalty lifts), where the nameplate reads the old range until its next refresh. Not measured.

**Hidden Hosts off:** the report shows exact counts and writes nothing; sight changes still apply (they are Field Camp effects). **Field Camp off:** the camp option hides unless a ride runs ("Await" stays so the scout can be called back), the contributor returns 0, and a running ride ends as a struck camp (the camp line, no report, no XP, the rest starts) at the next hourly look or when its wait menu finishes, whichever comes first; both paths ask the same predicate (`FarWatchService.FieldCampOff`), so the sight and the scout-away rule stop together and no report promises a sight that is not there.

### Who rides, absence, wounds, rest, XP

- The scout is read from the Scout **role** (`MobileParty.GetRoleHolder(PartyRole.Scout)`), never from `EffectiveScout`, which falls back to the leader and would turn the player into the scout silently.
- **Absence is a flag, not a roster change.** Removing the companion strips his role (`Hero.SetPartyBelongedTo` calls `RemoveAllPartyRolesOfHero`, pinned), leaves a partyless hero in the world for hours, and would need a re-add before a battle that `MapEventStarted` reports only after the sides are built. So the engine still counts him: he fights if the camp is attacked (the ride ends then: he rode back), and vanilla's Scout perks still read him as `EffectiveScout`. The `-35 percent` and the skill-0 fog stand for his absence. A consequence: Hidden Hosts' own live-reading XP (`HiddenHostsCampaignAdapter.GrantScoutingXp`) can still credit him while away.
- **Wounded** blocks the start. A wound mid-ride can only come from a battle, which ends the ride.
- **No hidden solo rider** on the map (decision Q3): hiding needs a patch on the party visual, is cosmetic, and would suggest the party cannot be found, which is false.
- **XP** is written once, on a full return, to the stored hero, only when `IsAuthority` and `CoopSessionPolicy.MayWriteSaveBackedState(IsCoopClient)`. The ride is cleared from the store before the write, so a throw cannot leave a ride that pays again.

### What ends the wait

| Path | Detected by | Effect |
|---|---|---|
| Time is up | engine `Progress >= 1` runs the wait consequence | complete, land, report |
| Call back | the isLeave option (the engine ends the wait first) | end, message, land |
| The camp is attacked or met (battle or peaceful meeting) | `OnPartyEncounterEvent` listener (raised at the end of `EncounterManager.StartPartyEncounter`, for every encounter in the world; it gates on a running ride, then on the main party being attacker or defender), and the `MapEventStarted` listener when a battle follows | end, message, no landing (the engine is opening its own menu) |
| An encounter or map event the listeners missed | hourly look | end, message; no menu switch |
| Camp struck or Field Camp off, scout gone, shared campaign | hourly look (Field Camp off also when the wait menu completes) | end, message; no menu switch |
| Due while the player is elsewhere or still in the wait menu | hourly look | complete; the report opens over whatever is current; no menu switch |
| Load inside the wait | `MapState.OnLoadingFinished` restarts the wait with time paused | `OnInit` resumes from the stored start |

**One exit path.** The hourly look never switches a menu. When it ends or completes a ride while the wait menu is current, the wait menu's own tick sees no ride and lands the player through `FarWatchLanding` (the tick runs every frame of the map state; while the paused report inquiry is open the game state manager runs the idle tick instead, which never ticks the menu, so the landing happens when the inquiry closes). The hourly cases accept up to an hour of latency. `Complete` is idempotent (no ride, no work), so the engine's completion and the hourly look cannot both pay.

### The landing

Every exit lands through `FarWatchLanding.Land`: the engine's own choice first (`GenericStateMenuId`, when it is neither of the scout's menus), then the camp panel while the camp stands (a wait menu, so time keeps running), else the map. A camp is never inside a settlement (FieldCamp folds it on entry); if the party is in one anyway, the landing leaves by the map with a warning rather than opening the camp panel.

### The camp menu order

The option is added from `OnAfterSessionLaunched`. FieldCamp builds the panel in `OnSessionLaunched` (forage 0, fortify 1, supplies 2, break 3, Leave appended), Refuge inserts at 4, and listeners of one event run newest first, so camp work (#860), listed after this module, inserts at 5 first, this module's listener inserts at 5 next and pushes it down, and Host Review, listed before this module, inserts last and pushes both down: **Refuge 4, Review 5, Scout 6, Work 7, Leave 8**. `GameMenu.AddOption` inserts only when `Count >= index`, so Leave stays last. `FarWatchWiringTests` composes the order from the indexes in the sources, the module order and the two engine rules (pinned in `FarWatchEngineFactsTests`).

### Co-op

Single-player only, as From the Ashes and Host Review: the start gate is `CoopSessionPolicy.IsSinglePlayerAuthority` (no live session, no deferral to a host, no dedicated server, and `IsAuthority`). A ride carried into a co-op session ends at the next hourly look with a message, no report and no XP, unless the wait menu's own completion comes first: that completion does not check the session, so it reports and raises the sight like any other and pays the XP only where the authority gate allows (`IsAuthority` and `MayWriteSaveBackedState(IsCoopClient)`). A saved kept sight loaded into a session affects only that peer's own map sight, as the lookout does. See [coop-interop.md](coop-interop.md).

### Component Diagram

```
Field Camp panel (taom_fc_camp, index 5)
        |
  FarWatchCampOption --- FarWatchPickMenu --- FarWatchWaitMenu --- FarWatchLanding
        |                      |                    |                    |
        +---------- FarWatchService (rules) --------+---- FarWatchPresenter / texts
                   /      |        \
      FarWatchStore  IFarWatchWorldAdapter  HiddenHostsService
      (plain state,   (engine facts,        (RaiseByScout, RecordScoutedSettlement,
       IScoutIntelQuery) XP write)           FogSkill, IsNameHidden)
            |
  FarWatchSightContributor -> SpottingContributorComposer -> TaomMapVisibilityModel.GetPartySpottingRange
HourlyTickEvent (-> camp panel refresh), OnPartyEncounterEvent, MapEventStarted -> FarWatchReconcile -> FarWatchService
```

## Configuration

None. Every number is a constant in `FarWatchMath` (the table above). The Field Camp master toggle (`ICampSettingsProvider.Enabled`) gates the option, the contributor and the sight effects. Hidden Hosts' own toggle changes only what the report says.

## Data and save

`FarWatchStore` holds no engine type and no `CampaignTime`. `FarWatchBehavior.SyncData` writes sixteen primitive keys and loads into a fresh `FarWatchRecord`, which `FarWatchStore.LoadFrom` validates part by part (an invalid part is dropped with one warning; the store never throws):

| Part | Keys | Dropped when |
|---|---|---|
| The ride | `_taomFarWatchKind`, `Hero`, `Name`, `Self`, `Target`, `TargetName`, `Start`, `Hours`, `Skill` | kind not 0 to 2, no hero, a settlement ride with no target, start not a finite hour, length not in (0, 8]; skill is clamped to 0..1000 |
| The kept sight | `_taomFarWatchBuff`, `BuffStart`, `BuffHold` | level not in [0, 0.5], start or hold not usable, hold above 48 |
| The rest | `_taomFarWatchRest`, `RestName` (who rode, for the rest text) | not a finite hour at or above 0 (then no rest and no name) |
| The named hosts | `_taomFarWatchNamed` (a `List<string>`), `NamedUntil` | until-hour unusable (at most 6 ids kept, blanks skipped) |

A missing key keeps the local default, so a save from before the feature loads as no ride, no sight. The names of the hero and the target are stored so a message can name a scout who has since died or left (a departure from the first design, which stored 13 keys; the 16th, `RestName`, keeps the rest text true when another scout is named while the first rests).

**Session reset.** The behavior is built with `new` in the module's factory, which the runner calls once per campaign start; its constructor clears the process-lifetime store, before any event and before a load's `SyncData` (the `#764` rule). No `OnGameLoaded` handler exists: a throw there loops the loading screen.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/FarWatch/FarWatchModule.cs` | registrations, the per-campaign behavior declaration, `OwnsSaveData` |
| `Main/Features/FarWatch/FarWatchService.cs`, `.Sweep.cs` | every rule; the sweep and the settlement report |
| `Main/Features/FarWatch/FarWatchStore.cs` | plain state, `IScoutIntelQuery`, the validated record |
| `Main/Features/FarWatch/FarWatchMath.cs` | constants and pure formulas |
| `Main/Features/FarWatch/FarWatchTypes.cs` | offers, progress, turns, reports |
| `Main/Features/FarWatch/FarWatchSightContributor.cs` | the sight seam boundary |
| `Main/Features/FarWatch/Hooks/` | behavior, camp option, pick, wait, reconcile, landing, presenter, texts, report text |
| `Main/Adapters/IFarWatchWorldAdapter.cs`, `FarWatchWorldAdapter.cs`, `ScoutCampFacts.cs`, `ScoutSighting.cs`, `ScoutSite.cs`, `ScoutSiteReport.cs` | engine facts, the XP write, the fact records |
| `Main/Adapters/IFarWatchNotifier.cs`, `FarWatchNotifier.cs` | the paused report inquiry and the one-line message |
| `Main/Adapters/SettlementDefenders.cs` | `IsDefender`: the one predicate behind the tooltip total and the breakdown |
| `Main/Core/Domain/IPartySpottingContributor.cs`, `SpottingContributorComposer.cs`, `IScoutIntelQuery.cs` | the seam |
| `Main/Features/CareerSystem/Models/TaomMapVisibilityModel.cs` | delegates to the composer |
| `Main/Features/FieldCamp/LookoutSpottingContributor.cs` | `GetSpottingRangeCapBonus => 0` |
| `Main/Features/HiddenHosts/HiddenHostsService*.cs`, `NameplateIntel.cs`, `TooltipVerdict.cs`, `TooltipFrame.cs`, `VanillaBinds.cs` | the Hidden Hosts changes |
| `Main/_Module/ModuleData/taom_module_strings.xml` | the 46 `taom_fwatch_*` keys under `<!-- FarWatch -->` |

## Dependencies

- `IFarWatchWorldAdapter` (Adapters) over the clock, the camp and party facts, the hero lookup, the locator scan, the fiefs, a settlement's defenders and `Hero.AddSkillXp`.
- `IPartyIntelAdapter` (Adapters, Hidden Hosts) for the fog facts of each party and the snapshot of a scouted settlement.
- `HiddenHostsService` for the digit raise and the settlement memory; `ICampService` and `ICampSettingsProvider` (Field Camp); `ICoopSessionProvider` and `IDedicatedServerProvider`.
- `IGameMenuAdapter` for opening menus and the engine's generic state menu.

## Tests

Counts are from the build that shipped this document. `Tests` lists the file and what it proves; the mutation runs (each mutant killed unless noted) are in the build log.

| File | Tests | What |
|---|---|---|
| `FarWatchMathTests` | 140 | lengths, ramp, hold and taper, stages, XP, compass (centres and boundaries), buckets, the kinds of an army and a plate name, composition ties, report order, the pick |
| `FarWatchStoreTests` | 50 | the state, `IsNamedByScout` expiry, every invalid part of the record, the round trip |
| `FarWatchServiceTests` | 99 | each block with its paired allowed case and the block order, who rides, the rest naming its rider, `TryStart` re-running the offer, progress, `Complete` (buff, rest, XP, idempotence, store cleared before the XP write, lost hero, co-op), `End` per reason (a short ride does not renew an older buff), `Reconcile` per cause and precedence (Field Camp off ends it), sight in every state, no allocation per query |
| `FarWatchSweepTests` | 35 | each exclusion with a paired included party (an unattached army member is a row), stages, the memory read back as `~12?`, fog off, the six and their order, the named hosts and their expiry, the settlement breakdown |
| `TAOM.Tests/Core/Domain/SpottingContributorComposerTests` | 40 | the composer rules and the 0.29 ceiling (pure rows), the model hands its number to the composer, and the `Apply` rows (`RequiresGame`) that include the lookout's number unchanged (characterisation first) |
| `HiddenHostsScoutSeamTests` | 46 | `FogSkill` at all five sites, named past the horizon (and no allocation with the real store), `RaiseByScout` (a disagreeing remembered count is not built on), `RecordScoutedSettlement` |
| `FarWatchMenuHookTests`, `FarWatchReconcileTests`, `FarWatchLandingTests`, `FarWatchSightContributorTests` | 41, 16, 9, 3 | the option (solo wording, the panel refresh), the pick, the wait (init, tick, completion and recall faults), the encounter and map-event listeners and the hourly look (no menu switch; the next wait tick lands), the landing |
| `FarWatchTextsTests` | 19 | every line renders, names pass the brace filter, glyphs verbatim, no newline in a registered default |
| `FarWatchBehaviorTests`, `FarWatchWiringTests` | 7, 27 | session reset, the 16-key round trip, module order, one construction, four events, the hourly order, the menu order, the adapter's army reads, wait-menu pins |
| `FarWatchWorldAdapterTests` | 6 | no engine reference type held by any Far Watch singleton |
| `FarWatchEngineFactsTests` | 18 | the IL and binding pins of the table below (`BindingVerification`), including the locator grid and the encounter raise |
| `HiddenHostsHarmonyPairTests` (+3), `HiddenHostsHookTests` (+1), `HiddenHostsTooltipHookTests` (+3) | 7 | the named-horizon case through real Harmony (prefix and finalizer, plus the stripped-prefix case), the encyclopedia prefix, the tooltip title and the frame |

## Engine facts (v1.5.5), with their callers

| Fact | Where | Pinned by |
|---|---|---|
| `GetPartySpottingRange` ends in `LimitMax(MaximumSeeingRange(), "Limit")`, after its last `AddFactor`; `MaximumSeeingRange()` has one call site in the dump | `DefaultMapVisibilityModel.cs:18, 32, 81` | `DefaultMapVisibilityModel_CapsTheNumberLast...`, `MaximumSeeingRange_HasExactlyOneCaller...` |
| The last `LimitMax` wins; `ResultNumber` clamps to it; `AddFactor` adds to `SumOfFactors` and ignores about 0 | `ExplainedNumber.cs:115, 149, 238, 259` | `ExplainedNumber_TheLastLimitMaxWins...` |
| `AddOption` inserts only when `Count >= index`, else appends | `GameMenu.cs:146` | `GameMenu_AddOption_InsertsOnlyWhen...` |
| `RunOnTick` ends the wait, then runs the wait consequence; `RunMenuOptionConsequence` ends the wait before an isLeave option | `GameMenu.cs:234-247, 276-284` | the two ordering pins |
| `SwitchToMenu` starts a wait menu's wait; a load into a wait menu restarts it with time stopped | `GameMenu.cs:368`, `MapState.cs:194-205` | `SwitchToMenu_StartsAWaitMenusWait`, `MapState_OnLoadingFinished...` |
| Listeners are head-inserted: the last registered runs first | `MbEvent.cs:24` | `MbEvent_ListenersAreInsertedAtTheHead...` |
| `MapEventStarted` is raised at the end of `MapEvent.Initialize`, after the parties have their sides | `MapEvent.cs:792-803` | `MapEvent_Initialize_PutsThePartiesOnTheirSides...` |
| An attack on the camp is a party encounter: `StartPartyEncounter` restarts the player encounter and ends by raising `OnPartyEncounterEvent` (`IMbEvent<PartyBase, PartyBase>`), for every encounter in the world. `GetEncounterMenu` can answer a plain `encounter_meeting` with no battle started, so `MapEventStarted` is not raised for it | `EncounterManager.cs:56-98`, `CampaignEvents.cs:1131, 2786`, `DefaultEncounterGameMenuModel.cs:192` | `EncounterManager_StartPartyEncounter_RaisesOnPartyEncounter_AsItsLastCall`, `GetEncounterMenu_CanAnswerAPlainMeeting_WithNoBattleStarted` |
| The locator grid is 32 by 32 cells of 5 units; a search's span per axis is `floor((x + r) / 5) - floor((x - r) / 5)` capped at 31 cells and the first cell wraps by modulo, so past r of about 75 most positions walk a whole axis and nearly every party; the cost grows smoothly with the radius, with no step. Vanilla's footstep sounds already search the same grid every frame at `SeeingRange + 25` | `LocatorGrid.cs` (`GetBoundaries`), `Campaign.MobilePartyLocator` | `LocatorGrid_SpanIsFloorDifferenceCappedAtWidthLessOne_AndWrapsByModulo`, `MapScreen_TickStepSounds_SearchesTheGridAtSeeingRangePlusTwentyFive`, `TheCampaignsMobilePartyLocator_IsAGridWithTheDefaultShape` |
| Leaving a party strips every role of the hero | `Hero.cs:2054` | `Hero_SetPartyBelongedTo_StripsEveryRole...` |
| `Town.AllFiefs` yields `AllTowns` then `AllCastles`; `AllTowns` holds no castles | `Town.cs:279` | `TownAllFiefs_YieldsTheTownsAndTheCastles...` |
| `OnAfterSessionStart` is raised right after `OnSessionStart` | `Campaign.cs:754-755` | (listener order composed in `FarWatchWiringTests`) |
| `FormationClass` ordinals 0 to 7 | `TaleWorlds.Core.FormationClass.cs` | `FormationClass_HasTheOrdinalsTheBucketMappingReads` |
| Members the adapter and hooks call keep their shape | `GetRoleHolder`, `IsWounded`, `AddSkillXp`, `StartFindingLocatablesAroundPosition`, `ShowInquiry`, the `GameMenu` wait members | `TheMembersTheAdapterAndHooksCall_KeepTheirShapes` |

## Localization

46 keys with the prefix `taom_fwatch_`, registered in `Main/_Module/ModuleData/taom_module_strings.xml` under `<!-- FarWatch -->` with defaults identical to the inline ones, and seeded as English into the 12 language files (`translate_with_claude.py --sync-ids --seed-only`). The rows are translated in all 12 languages (2026-11-12, #763). Names (`{SCOUT}`, `{SETTLEMENT}`, `{NAME}`) reach a template only through `FarWatchTexts.Safe` (braces removed: a hero's name may be player-typed). The counts arrive as `{COUNT}` variables so the translator's placeholder gate protects the glyphs. The kind and direction labels are nominatives that agree with nothing, and `report_row` (with the labels and the host sentence) should reach the translator together. The sight line prints only when the ride set a sight; a host with no direction is not listed; a settlement with nobody inside reads the "saw no hosts" line instead of claiming zero men.

## In-game checklist (owed)

Each step names what it expects to see.

1. **Surroundings ride:** raise a camp with a Ranger companion in the Scout role. "Send a scout", Sweep. The wait menu names the scout, the bar fills over 4 to 8 hours, sight (the map bar's sight tooltip) rises during the ride. On return the camp panel and a paused report with up to 6 hosts and the sight line.
2. **Settlement ride:** pick a town within 50 units. Sight drops by about a third of base during the ride; on return the defenders breakdown opens. Hover the town later: `~` defenders and the "Counted ... ago" row.
3. **Call back** mid-sweep: one message, no report, sight stays wider, then fades. The option reads "Await your scout" while a ride runs and reopens the wait menu from the camp panel.
4. **An attack on the camp** during a settlement ride: the encounter menu, the battle message, no report, no XP. Then a **peaceful meeting** (a lord party that only talks): the ride ends too, with the same message, and the engine's meeting menu is not replaced.
5. **Save and load inside the wait:** time is paused on load; progress resumes from the stored start. Save and load on the pick menu: it rebuilds.
6. **Hidden Hosts on and off:** with it on, the report rows carry digits and `?`; a listed host past the horizon keeps its name and the encyclopedia key opens it; with it off, exact counts in the report.
7. **A solo player** (no companions): "Ride out to scout", and the hero is away for the length of the ride.
8. **The sight cap with a strong scout** (Scouting 300 with perks and a lookout): the sight tooltip shows the "Limit" line above 60 (up to 77.4) during the raised sight, and falls back to 60 when it ends.
9. **A second campaign in one process** starts with no ride, no sight and no rest.
10. The camp panel order: Refuge, Review the host, Send a scout, Set the men to work, Leave. With the panel open during a rest, the option's tooltip counts the hours down and turns to "Send a scout" when the rest ends, without leaving the panel.
11. **A solo player**: while the ride runs the camp option reads "Continue the ride" and the wait menu offers "Ride back to camp".
12. **Field Camp switched off** in the MCM mid-ride: the ride ends at the next hourly look with the camp line, and sight and the fog return to normal together.

## Not built

- Hiding the solo rider on the map (Q3).
- A partial report or XP for a called-back ride (Q2); the partial sight is kept.
- Villages as targets (they hold only militia), sliders, or any MCM setting.
- A scout-report multiplier in the fog stack (the sight already scales it).

## Changelog

- 2026-10-20: first version (#847): the camp option, the pick and wait menus, the sight seam (composer, cap bonus), the Hidden Hosts rules, 44 keys.
- 2026-10-20: deep-review fixes: the buff clock is no longer renewed by a short ride, army members not yet attached and an army's attached parties are counted, any encounter with the camp ends the ride, the sight cap is 77.4 (a bound on the extra per-frame search cost, not a measured threshold), a solo ride has its own two labels (46 keys), the hourly look never switches a menu, the camp panel refreshes when a rest or ride ends, Field Camp off ends a ride (also at a wait-menu completion), a weaker scout's return keeps a longer held full sight, and the cap-cost reasoning is corrected: the search cost has no step and an in-game A/B is owed.

## GitHub Issue

- **Issue:** #847 [Send a scout from camp](https://github.com/haterade22/TAOM/issues/847)
- **Status:** Open (code done; the in-game checklist and the paid translation are owed).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/field-camp.md](./field-camp.md)
- [docs/features/hidden-hosts.md](./hidden-hosts.md)
- [docs/features/host-review.md](./host-review.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/gamemodel-registry.md](../reference/gamemodel-registry.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
