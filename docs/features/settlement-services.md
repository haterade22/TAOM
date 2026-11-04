# Watch-Silver and Tidings of the Lists

## Overview

Two small settlement services (#859). **Watch-Silver:** talking to the governor of a town or castle your clan owns, inside that fief, you can offer silver for the watch. The governor names the fief's loyalty and offers up to three amounts (+5, +10 and +20 loyalty, clipped so loyalty never passes 50), each with its price in denars, and paying raises the loyalty at once. **Tidings of the Lists:** a new option in a town's arena menu, "Ask after tournaments elsewhere" with the fee (20 and the coin icon), names the three nearest towns that hold a tournament now and are not at war with your realm. Single-player only, two MCM switches, nothing saved by TAOM, no Harmony, no reflection and no prefab.

## Why This Exists

- **Vanilla behavior:** a fief's loyalty moves only by the daily sum of nine factors (food, owner culture, policies, projects, issues, security, notable relations, governor perks and the drift `-0.1 * (loyalty - 50)`, all in `DefaultSettlementLoyaltyModel`), by TAOM's own culture feats on top of them, and by events. A governor talks about the fief but has no way to spend money on it. The arena menu offers only the tournament itself (plus practice and the leaderboard), and a tournament elsewhere is found by travelling.
- **TAOM requirement:** a freshly taken town of another culture takes a loyalty penalty every day under [Revolt Tuning](revolt-tuning.md) and settles near 40, dipping toward the rebellion band when security or food is bad. The player needs a way to spend gold to steady it, and a gold sink that is not a loyalty pump.
- **Without this feature:** the only answer to a sliding town is to wait for the drift, or to lose it to a rebellion.

## Architecture

### Design Challenge

There was no loyalty writer, no governor dialogue, no revolt service and no tournament locator in TAOM. The dialogue condition of vanilla's governor line requires `CurrentSettlement.IsTown`; this feature widens that to castles, so every read and write downstream was checked to be castle-safe (`Town.Loyalty`, `Settlement.Militia`; no `.Village` dereference). Both pieces write shared world state (a fief's loyalty, the player's gold), so both stand down outside single-player.

### Solution Approach

One module (`SettlementServicesModule`), two pure services, one adapter and two campaign behaviors, each built with `new` per campaign.

- `WatchSilverService` has one check used by the offer condition, the clickable conditions and `Apply`: switch off, not single-player, not eligible, a non-finite number, at the ceiling. `Apply` re-reads every gate, re-prices, then takes the gold first and sets the loyalty second, because the debit is the step that can come up short and the setter cannot. A throwing loyalty write refunds the gold and reports a problem. The non-finite-number log line is written once per fief per game session (the service is a singleton, and the latch holds settlement ids only).
- `TidingsService` filters the adapter's survey (not the current town, not at war with the player, a finite distance above zero and below the cutoff; a distance of 0 is a failed path search), sorts nearest first then by id, takes three, and charges the fee only when there is something to name. Each behavior asks the service whether its line is shown (`WatchSilverQuote.IsOffered`, `TidingsService.IsShown`) rather than deciding inline.
- `PlayerPurse.TryCharge` is the one way both services take gold. A charge succeeds only when the purse lost exactly the price and nothing threw. Otherwise it fails and gives back what the purse lost, measured on the purse itself, because `GiveGoldAction` debits before it dispatches the trade event, so a throw can arrive after the gold is gone.
- `WatchSilverDialogBehavior` adds the lines on `OnSessionLaunchedEvent` and clears the stored outcome on `ConversationEnded`. `TidingsMenuBehavior` adds the option on `OnAfterSessionLaunchedEvent`, so the arena menu is fully built and the position of `town_arena_back` in its options is final. A result the player cannot act on (a problem, or a switch, co-op or disguise gate that closed since the menu was built) shows a neutral refusal; a purse that fell under the fee shows vanilla's "You don't have enough gold."
- Every engine callback (condition, clickable condition, consequence) runs behind the shared `ScreenFaultLog` (`Main/Core/Logging`, also used by Namegiving and the Reeve's Ledger): one log line per callback for the life of its behavior, which is one campaign. Every throw also goes to `Outcomes.Swallowed`: one `[Swallowed]` warning per site for the process, with the repeats counted in the `[Session]` summary (#1071, `cb4eeaa3d`).

### Component Diagram

```
TaomSettings.EnableWatchSilver / EnableTidingsOfTheLists
        |
SettlementServicesSettingsProvider
        |
WatchSilverService      TidingsService
        \                /
         PlayerPurse (the gold)
        \                /
      ISettlementServicesAdapter  (loyalty, militia, tournaments, gold)
        |
WatchSilverDialogBehavior (lines)   TidingsMenuBehavior (arena option)
```

### The numbers

Constants in code, no JSON.

| Rule | Value |
|------|-------|
| Ceiling | loyalty 50 (the drift medium: silver steadies a fief and never buys the tax boost at 75) |
| Offers | +5, +10, +20 points, each clipped to `floor(50 - loyalty)`; a clipped row equal to the one before it is not shown |
| Price | `ceil(points * max(militia, 50) * 4)` denars, clamped to `[1, int.MaxValue]` |
| Tidings fee | 20 denars, charged only when at least one town is listed |
| Tidings list | the 3 nearest by land travel, below `MapDiagonal * 2` |

Worked example: militia 120 at loyalty 32 gives +5 for 2,400, +10 for 4,800 and +18 (clipped) for 8,640.

## Configuration

Two MCM switches, both default on, both applied at the next conversation or menu open.

| Setting | Group | Effect |
|---------|-------|--------|
| `EnableWatchSilver` | Fief Management/Watch-Silver (GroupOrder 5) | the governor's silver line |
| `EnableTidingsOfTheLists` | Tournaments (Order 4) | the arena option |

Both are `PlayerLocal` in `CoopSettingsRelevance` and so excluded from the co-op fingerprint: each switch only gates a player's click, and every write is behind `CoopSessionPolicy.IsSinglePlayerAuthority`, so two peers with different values never compute differently. The total of MCM settings rises by 2 (see [coop-interop.md](coop-interop.md)). Switching a switch off leaves nothing to restore, since TAOM wrote nothing that persists as TAOM state. The Tidings switch also appears in the Tournaments table of [tournament-rewards.md](tournament-rewards.md).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SettlementServices/SettlementServicesModule.cs` | module: services, adapter, two behaviors, `OwnsSaveData` false |
| `Main/Features/SettlementServices/WatchSilverService.cs`, `WatchSilverTypes.cs` | the Watch-Silver rules and their value types |
| `Main/Features/SettlementServices/TidingsService.cs`, `TidingsTypes.cs` | the Tidings rules and their value types |
| `Main/Features/SettlementServices/PlayerPurse.cs` | the shared gold charge with its refund rule |
| `Main/Features/SettlementServices/SettlementServicesSettingsProvider.cs` | the two switches, with the lazy settings accessor |
| `Main/Features/SettlementServices/Hooks/WatchSilverDialogBehavior.cs`, `WatchSilverDialogLines.cs` | the conversation lines (priority 111) |
| `Main/Features/SettlementServices/Hooks/TidingsMenuBehavior.cs` | the arena option |
| `Main/Features/SettlementServices/Hooks/SettlementServicesTexts.cs` | every `TextObject` |
| `Main/Core/Logging/ScreenFaultLog.cs` | the shared guard around every engine-called member |
| `Main/Adapters/ISettlementServicesAdapter.cs`, `SettlementServicesAdapter.cs` | the engine side |
| `Main/Features/TaomSettings.cs`, `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | the switches and their co-op class |
| `Main/Composition/FeatureModules.cs` | the one module line |

## Dependencies

- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the single-player predicate `CoopSessionPolicy.IsSinglePlayerAuthority` (no live session, no deferral to a host, no dedicated server, and `IsAuthority`), shared with From the Ashes, Namegiving and the other world-writing services.
- `ISettlementServicesAdapter` (Adapters): `Hero.OneToOneConversationHero.GovernorOf`, `Town.Loyalty`, `Settlement.Militia`, `Town.AllTowns` with `TournamentManager.GetTournamentGame`, `MapDistanceModel.GetDistance` (Default navigation) and `GiveGoldAction`.
- [Revolt Tuning](revolt-tuning.md): context only. It does not change; the ceiling of 50 sits at the drift medium it works against.
- Culture Conversion (optional stable-loyalty gate, `CultureConversionRequireStableLoyalty`, off by default, minimum 50): a payment can land a fief exactly on 50, which meets that gate. The cap of 50 stays by decision; this is documented, not guarded.
- `ScreenFaultLog` (Core/Logging): shared with Namegiving and the Reeve's Ledger.

## Tests

- `TAOM.Tests/Features/SettlementServices/WatchSilverServiceTests.cs`: every gate, clipping, the price rule (floor, huge militia), short gold, a charge that throws after the debit (the other receipt cases live in `PlayerPurseTests`), call order, the offered-block rule, the once-per-fief log.
- `TAOM.Tests/Features/SettlementServices/TidingsServiceTests.cs`: each hidden case, the filter (zero, NaN and cutoff distances), the order and tiebreak, the cap, the fee and its refund, the shown-block rule.
- `TAOM.Tests/Features/SettlementServices/PlayerPurseTests.cs`: a full charge, a short or empty receipt, a throw before and after the debit, a refund that throws.
- `TAOM.Tests/Features/SettlementServices/SettlementServicesSettingsProviderTests.cs`, plus rows in `CampaignHotPathSettingsProvidersTests`: defaults, attributes, hints, co-op class.
- `TAOM.Tests/Features/SettlementServices/SettlementServicesWiringTests.cs`: module line, empty save footprint, subscriptions, outcome clearing, the shared fault log, the arena option's index lookup, Hooks files under 150 lines.
- `TAOM.Tests/Features/SettlementServices/WatchSilverDialogLinesPinTests.cs`: the token chain, every reply back to `lord_pretalk`, priority 111 with the paid-out reply above the unconditional refusal.
- `TAOM.Tests/Features/SettlementServices/SettlementServicesAdapterPinTests.cs`: source pins for the gold check, the absolute write, the eligibility mapping, the towns-only walk and the writes the feature must not make.
- `TAOM.Tests/Features/SettlementServices/SettlementServicesTextsTests.cs` (`RequiresGame`): the English of each text and the reused vanilla key.

## How to Change a Value

1. Edit the constants in `WatchSilverService` (`Ceiling`, the tier points, `GoldPerMilitiaPerPoint`, `MilitiaFloor`) or `TidingsService` (`Fee`, `MaxListed`).
2. Update the worked example and "The numbers" table above, the feature-map row, and the tests that name the numbers.

## Known Limits

- The "rebellious" label on a town clears at the first daily tick after you leave the town (the engine skips the town you stand in), not when the silver is paid. Loyalty itself rises at once.
- Militia sizes in TAOM's world are unmeasured, so the price rule is untuned (in-game check 6).
- Hostile towns are not listed, by decision; there are no map markers (they would be saved `VisualTrackerManager` state needing a removal story).
- No cooldown and no saved state: the ceiling and the daily drift already cap what silver can buy.
- Tidings uses default (land) navigation only, since TAOM ships no naval DLC.

### In-game checklist (owed)

All of this is unseen in game.

1. A companion governing an owned town, met in the keep: the line shows; the tiers and their prices; an unaffordable row is greyed with the vanilla reason; paying shows the "gold removed" line, the town overview shows the new loyalty, the governor confirms it, and the talk returns to the main options.
2. The same in a castle.
3. A fief of another clan in the player's kingdom, and a governor met outside his fief: no line.
4. Loyalty above 49: the line is greyed with its reason. A loyalty a little above 47 (47.1 to 47.9): a single +2 row.
5. A rebellious town: loyalty rises at once; the rebellious label clears at the first daily tick after you leave the town.
6. Note the militia and the three prices in two towns and a castle, as input for tuning the price rule.
7. Arena: the option sits above "Back to town center" and shows the fee; a list of up to three, nearest first, with no hostile town, one "Town (Realm)" per line under the header; none means free; under 20 denars means greyed.
8. Asking shows vanilla's "gold removed" line for the 20 denar fee.
9. "Never mind." in the governor's tiers returns to the governor's options.
10. A refusal (a switch, co-op or disguise gate that closed since the menu was built): the line "The arena master has no answer for you now." and nothing charged.
11. Disguised in a hostile town's arena: the option is hidden.
12. Save, load and a new campaign: one copy of each line and option.
13. Either switch off: its line or option is gone at the next conversation or menu open.
14. Another language: the strings render and no raw key shows.

## Changelog

- 2026-10-21: first build on feat/settlements-buildings (#859). Not yet seen in game.
- 2026-10-23: deep-review fixes: the shared fault log, a gold charge that measures and refunds a throw after the debit, once-per-fief logging, the shown-block rules moved into the services, dead data and the town gate removed, a neutral Tidings refusal.

## GitHub Issue

- **Issue:** [#859](https://github.com/haterade22/TAOM/issues/859) Settlement services: governor militia bribe and nearby tournament inquiry
- **Status:** Shipped on `bannerlord-1.5.x` in `2057ad5d9` (v2.0.35). Issue open: the in-game check is owed; the paid
  translation of the twelve new strings (`taom_wsilver_*`, `taom_tidings_*`) is tracked in [#931](https://github.com/haterade22/TAOM/issues/931).
