# Inn Rounds

## Overview

Three lines on a town tavern keeper's talk (`tavernkeeper_talk`). "Stand a round for my whole company" costs the town's price of the local drink for every man in the party and raises the party's morale. "A round for everyone in the house, on me" costs the same for everyone standing in the tavern and raises the player's clan renown. "Point me to the holds near here" marks the nearest towns and castles on the map, by the same rules as a notable's line. Both rounds also give a little Charm experience, and the gold goes to the town. Single player only. Issue #901.

No Harmony patch and no GameModel: one dialog behavior over a service graph, and it saves the day and count of the rounds stood.

## Why This Exists

- **Vanilla behavior:** a tavern keeper offers quests, companions for hire and information about the town's owner. The tavern maid sells the men one meal (+2 morale, once a week).
- **TAOM requirement:** the tavern is where a war band drinks. Standing a round should lift a tired party and make a name, and a keeper who hears every traveller should be able to say where the strongholds stand.
- **Without this feature:** the only morale a tavern sells is the maid's meal for the men (+2, once a week), and nothing in a tavern raises a clan's renown. The map stays dark until the player has travelled it.

## Decisions (Mike, 2026-10-26)

1. **Reveal reuse:** the reveal's rules were moved out of `NotableServicesService` into `HoldsRevealService` ([notable-services.md](notable-services.md)). It takes a `RevealAsk` (settlement, at war with the player, relation) and a payment delegate. The price, count, diagonal fraction and minimum relation stay in `notable_services.json`.
2. **The keeper's relation** is the player's relation with the leader of the clan that owns the town (`Hero.GetRelationWithPlayer`). A town with no owner counts as 0.
3. **Payment:** the reveal gold and the round gold go to the town (`GiveGoldAction.ApplyForCharacterToSettlement`), checked by the change in the purse as `PayNotable` does.
4. **Text:** the reveal line is Notable Services' own (`NotableServicesTexts.RevealLine`) and its notice is `taom_ns_holds_notice`. Four keeper replies are new: done, war, cold, nothing new.
5. **Daily cap:** `maxRoundsPerDay` rounds, party and house together, and the count starts over on a new campaign day. Both round lines are greyed with a reason once it is used up. The reveal is not counted.
6. **Drinks:** `cultureDrinks` maps a culture id to `beer` or `wine`, the only two drink goods in SandBoxCore (`horses_and_others.xml`). A culture that is not listed pours beer. Each culture has its own name for its drink, a game text `str_taom_round_drink` with the culture id as the variation (the Bandit Voices pattern), and a culture with no row says "ale".
7. **Names:** the feature is Inn Rounds, keys are `taom_round_*`.

## Architecture

### Design Challenge

- The tavern keeper is not a hero (`Occupation.Tavernkeeper` on a `CharacterObject`), so the adapter reads the conversation's character and the town from the main hero's current settlement.
- Vanilla also seats a tavern keeper in the town center by day (`TavernEmployeesCampaignBehavior`), and some TAOM center scenes have the spawn point. The adapter therefore counts the room only while the conversation runs in the tavern location (`CampaignMission.Current.Location`, the test vanilla's board games make) and answers 0 anywhere else, so the house line hides at a center keeper. The count includes the keeper and the staff.
- Vanilla's own price line sets a global `PRICE` variable. Each of these lines carries its own whole-line variable (`TAOM_ROUND_PARTY_LINE`, `TAOM_ROUND_HOUSE_LINE`, `TAOM_ROUND_REVEAL_LINE`) and never reads or writes that one.
- `MobileParty.RecentEventsMorale` clamps to [-100, 100], so the adapter reads the change back and the notice shows that change in the party's recent-events morale. It is not the morale bar the player sees: the bar is clamped again and scaled by the leader's Generosity, so a party near 100 sees little change.
- The engine credits Charm experience at the ask times the hero's XP multiplier and the skill's learning rate, so the adapter reads the developer's total before and after (`IHeroSkillXpAdapter.AddSkillXpLanded`) and a second notice shows the experience that landed, or none when none did.
- A failed reveal answers with vanilla's "Anything else?" straight back to `tavernkeeper_talk`, as vanilla's own turndown line does; sending it to `tavernkeeper_pretalk` would say it twice.
- The day's count is campaign state, so it lives in a `RoundLedger` owned by the behavior (built with `new` for every campaign) and saved in `SyncData`. A container singleton would carry one campaign's count into the next. The count is saved with its day, so a save from earlier on the same day brings back that day's count, never zero.

### Solution Approach

`InnRoundsService` owns every rule over snapshots and ids. `IInnRoundsAdapter` is the engine boundary for the keeper and town, the day, the drink price, the two headcounts, the purse, the town's coffers, the morale and the notices. Charm experience, renown, the holds on the map and the random numbers come from the shared adapters (`IHeroSkillXpAdapter`, `IHeroRenownAdapter`, `IMapRevealAdapter`, `IRandomSource`). `InnRoundsDialogBehavior` is thin (ADR-002). The conditions never write; each consequence re-checks every gate.

The price of a head is the town market's price of the culture's drink as a buyer (`TownMarketData.GetPrice`), capped at `maxPricePerHead`. A missing item, market or town uses `fallbackPricePerHead`. A party round costs `TotalManCount` times that, a house round the tavern headcount times that.

### Component Diagram

```
inn_rounds.json -> InnRoundsConfigProvider (validated, cached)
                                  |
InnRoundsDialogBehavior -> InnRoundsService (price, cap, effects, gate)
(lines at priority 111,       |
 RoundLedger, SyncData)       +--> IInnRoundsAdapter (keeper, price, headcounts, purse, town, morale, notices)
                              +--> HoldsRevealService --> IMapRevealAdapter
                              +--> IHeroSkillXpAdapter, IHeroRenownAdapter, IRandomSource
                              +--> IMessengerConversationState, ICoopSessionProvider, IDedicatedServerProvider
```

## What the player sees

Three options sit on `tavernkeeper_talk` at priority 111 (vanilla is 100), each showing its price. They show for a tavern keeper in a town, in single player, outside a messenger conversation and with the MCM switch on.

- **Party round:** price is the party's `TotalManCount` times the price of a head. Recent-events morale rises by a random amount from `moraleMin` to `moraleMax`, times the MCM morale scale, and the notice shows the change that landed. Charm experience is asked from `charmXpMin` to `charmXpMax`, and a second notice shows what landed after the engine's multipliers. The keeper then says vanilla's "Anything else?".
- **House round:** price is the tavern headcount times the price of a head. The line is hidden when the headcount is 0, which includes a talk with the keeper who stands in the town center by day. Renown is from `renownMin` to `renownMax`, times the MCM renown scale, rounded to a whole number and given through `GainRenownAction`. Charm experience as above.
- **Point out the holds:** the price is `revealPrice` from `notable_services.json`. At war with the town's realm the keeper refuses with no charge, as when the owner's relation is below `revealMinRelation`, and when every hold in reach is already marked. Otherwise the gold goes to the town and the nearest holds are marked.
- **Not enough gold:** the line is greyed with vanilla's "You don't have enough gold." (by key, no row of ours).
- **Daily cap:** both round lines are greyed with "Enough rounds have been poured for one day." The cap is named ahead of the purse.

## Configuration

### Config File: `Main/_Module/ModuleData/inn_rounds/inn_rounds.json`

Every number is a finite float. A NaN, infinite or out-of-range value reverts to its default with a warning, and one summary warning follows; a minimum above its maximum reverts both; a missing or unparseable file uses the defaults. Cached for the process, so an edit needs a restart. A test pins the shipped file to the compiled defaults.

| Key | Default | Range | What it does |
|---|---|---|---|
| `maxRoundsPerDay` | 3 | 1..20 | Rounds (party and house together) per campaign day |
| `maxPricePerHead` | 35 | 1..1000 | Ceiling on the price of one drink |
| `fallbackPricePerHead` | 27 | 1..1000 | Price when the market cannot be asked |
| `moraleMin`, `moraleMax` | 20, 40 | 0..100, min at most max | Morale a party round gives, before the scale |
| `renownMin`, `renownMax` | 1, 3 | 0..50, min at most max | Renown a house round gives, before the scale |
| `charmXpMin`, `charmXpMax` | 20, 60 | 0..1000, min at most max | Charm experience a round gives |
| `cultureDrinks` | 30 rows | `beer` or `wine` | The drink each culture id pours; an unlisted culture pours beer |

The 30 rows are the 24 main cultures of `taom_spcultures.xml` and the six renamed vanilla ones (`empire`, `vlandia`, `battania`, `khuzait`, `aserai`, `sturgia`). A test pins that every one has a row here and a name in `taom_module_strings.xml`. Ered Luin pours beer and names it "blue-mountain stout".

### MCM

`World / Inn Rounds` (`GroupOrder = 79`, free when checked 2026-10-26): **Enable Inn Rounds** (default on), **Inn Round Morale Scale** and **Inn Round Renown Scale** (0 to 3, default 1; a value that is not a finite number in that range reads as 1). All three are `PlayerLocal` in `CoopSettingsRelevance`: they only shape a player's click, and every write is behind the single-player predicate, so peers with different values never compute differently.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/InnRounds/InnRoundsService.cs` | Every rule: gate, price, cap, effects, reveal |
| `Main/Features/InnRounds/InnRoundsModule.cs` | Feature module, owns save data, behavior built with `new` per campaign |
| `Main/Features/InnRounds/InnRoundsIoC.cs` | DryIoc registration (none of the shared pieces) |
| `Main/Features/InnRounds/InnRoundsConfig.cs`, `InnRoundsConfigProvider.cs` | Validated JSON config |
| `Main/Features/InnRounds/InnRoundsSettingsProvider.cs` | MCM switch and the two scales over their defaults |
| `Main/Features/InnRounds/Domain/` | `TavernSnapshot`, `RoundQuote`, `RoundKind`, `RoundLedger` |
| `Main/Features/InnRounds/Hooks/` | Dialog behavior, dialog lines, number-carrying texts |
| `Main/Features/NotableServices/HoldsRevealService.cs` | The shared reveal rules |
| `Main/Adapters/IInnRoundsAdapter.cs`, `InnRoundsAdapter.cs` | Engine boundary |
| `Main/_Module/ModuleData/inn_rounds/inn_rounds.json` | Configuration data |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The 30 drink names (`str_taom_round_drink.<cultureId>`) and the round texts |

## Dependencies

- `HoldsRevealService` (NotableServices): the reveal; `IMapRevealAdapter` (Hard Questions registers it).
- `IHeroSkillXpAdapter`, `IHeroRenownAdapter` (Enlistment registers them), `IRandomSource` (Initial Child Generation registers it).
- `IMessengerConversationState` (Messengers), `ICoopSessionProvider`, `IDedicatedServerProvider` (Co-op Interop): the gates.
- `ScreenFaultLog` (Core): every engine-called member of the behavior runs behind it.

## Tests

- `TAOM.Tests/Features/InnRounds/InnRoundsServiceTests.cs`: the price (market, cap, fallback, drink), both quotes, the shared daily cap and its reset, the effects and the scales, a failed payment, the reveal, and each gate closing each line.
- `InnRoundsConfigProviderTests.cs`: every field against NaN, both infinities, below and above range, the bounds, the min and max pairs, the drink map and the shipped JSON.
- `InnRoundsSettingsProviderTests.cs`: the defaults, the scales against a bad value, the settings group, each MCM hint quoting its compiled default and naming no gender.
- `InnRoundsTextsTests.cs` (`RequiresGame`): the two priced lines render their price and the cap reason is a full sentence.
- `InnRoundsBehaviorTests.cs` (`RequiresGame`): the behavior over the real service, the cap reasons and the `SyncData` round trips, including a load earlier on the same day.
- `InnRoundsWiringTests.cs`: the module line, nothing in `IoC.cs` or `SubModule.cs`, save data owned, the behavior new for each campaign, the container resolving the service, unique line variables and source pins that no condition writes.
- `InnRoundsBindingTests.cs` (`RequiresGame`): `tavernkeeper_talk` and `tavernkeeper_pretalk` in the installed engine, the engine calls the adapter makes and the two drink goods. The pins on the adapters' own source (the town payment, the market price, the tavern count, the morale and skill read-backs) are in `InnRoundsWiringTests.cs`, which needs no game.
- `InnRoundsDataTests.cs`: a drink row and a name for every main culture, every `{=taom_round_*}` key registered and referenced, and the English free of gendered words and the listener's peoples.
- A campaign behavior declared by a feature module has its co-op row in [coop-interop.md](coop-interop.md).

## How to verify in game

**Preconditions:** a campaign in single player with TAOM only (no co-op mod), the MCM switch on, the party in a town.

1. Talk to the tavern keeper: three new lines show, each with a price. Check the party round costs your party size times a drink price (35 at most a head).
2. Stand a party round: the gold moves to the town, one notice shows the change in recent-events morale and a second the Charm experience that landed.
3. Stand a house round in the tavern: renown rises and a notice shows the Charm experience that landed. Talk to a keeper in the town center by day: the house line is not offered.
4. Stand a third round, then look at the lines: both are greyed with the daily reason. Wait a day: they work again.
5. Save after two rounds, stand a third, reload: two are counted.
6. Point out the holds at peace: gold goes to the town, holds are marked. At war: refused, no charge.
7. Drop your gold below a price: that line is greyed with "You don't have enough gold."
8. A messenger conversation shows none of the lines.
9. Check a Gondor town (wine) and a Dunland town (beer) name their drinks.

## Known limits

- **The tavern headcount includes the keeper and the staff.** A house round is therefore on offer whenever the player talks to the keeper in the tavern, and never at a center keeper.
- **The price cap applies in most towns.** Beer is worth 50 and wine 90, so most towns charge the cap, 35 a head.
- **In the player's own town the owner relation is 0** (the player's relation with self), so a `revealMinRelation` above 0 would make the player's own keeper refuse.
- **A missing `inn_rounds.json` prices wine towns as beer,** because the culture map is empty and an unlisted culture pours beer.
- **The morale notice is not the morale bar.** See the design notes above.
- **Only beer and wine exist as drink goods.** Every culture's drink is priced as one of the two, whatever its name.
- **The price follows the player's trade perks,** because the market is asked with the main party as the buyer.

## Translation

The 12 language files hold English seed rows only. The paid translation is owed in backlog #763.

## Changelog

- 2026-10-26: wave 1 review fixes: the house round counts only the tavern the player stands in, the Charm notice shows the experience that landed, the morale notice is worded as the recent-events change, the failed reveal no longer says "Anything else?" twice, and the single-player gate is `CoopSessionPolicy.IsSinglePlayerAuthority`.
- 2026-10-26: first version (#901). The reveal's rules move into `HoldsRevealService`.

## GitHub Issue

- **Issue:** #901, Inn Rounds
- **Status:** Open
