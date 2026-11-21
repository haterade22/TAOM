# Battle Gear Loot

## Overview

An optional MCM switch (default off). When on, the player's body loot after a battle his side wins is the gear the beaten
enemies fought in. Each weapon, armour piece, horse and harness rolls on its own against an MCM chance (0 to 100, default 10).
No GitHub issue (the maintainer waived it for this feature).

## Why This Exists

- **Vanilla behavior:** `MapEvent.LootCasualtyCharacter` gives the player at most `RoundRandomized(share)` tries per body,
  each one item from `GetLootedItemFromTroop` (one random battle set, at most one merchandise item, often none), and drops a
  second item of the same type (`MapEvent.cs:1626-1664`, `DefaultBattleRewardModel.cs:100-162`).
- **TAOM requirement:** a player who wants the fallen enemy's real kit as loot, tuned by a chance he sets.
- **Why not a GameModel:** the model hooks give one `EquipmentElement` per call, and the engine calls them a handful of times
  per body and drops repeated types, so "every item rolls on its own" cannot be built there.

## Architecture

### Design Challenge

Rebuilding what an enemy wore: at loot time the rosters hold character counts only, the agent's kit is gone.

### Solution Approach

- **Seam:** Harmony bool prefix `Patch172_BattleGearLoot` on the private `MapEvent.LootCasualtyCharacter`. It runs once per
  body the engine already gave the player. The AI branch, winner assignment, baggage loot, gold, prisoners and the loot screen
  stay vanilla.
- **Kit:** `Equipment.GetRandomEquipmentElements(character, true, Battle)`, the call a spawned agent uses, so shape and
  modifier distribution match, and it works in simulated battles. A hero's own `BattleEquipment` is read slot by slot; the
  shared dead-hero placeholder gives no pieces.
- **Rolls:** `BattleGearLootService.PickSlots` rolls once per eligible piece in slot order through `IRandomProvider`
  (`System.Random`, so the engine's `MBRandom` sequence is not shifted). Chance 0 and 100 draw no roll.
- **Never drops:** empty slots, `NotMerchandise` items (this includes the heavy, elite, lord and named classes while Armour
  Acquisition gates them), named weapons always, banners, quest items, and mounts of the mount-locked creatures (giant spider,
  war elephant, Mumakil). With Armour Acquisition off, heavy, elite and lord pieces drop like any merchandise.
- **Share beside allies:** a body counts only when vanilla's own per-body draw (`maxLootedItemsPerBodyForMainParty`,
  `RoundRandomized(share)`) gives the player at least one try; otherwise vanilla runs and gives nothing, so allied battles
  loot as many bodies as before.
- **Enlisted:** the enlistment spoils stay on top of the gear share.
- **Hideout "send troops":** vanilla's model gives no loot there; the adapter asks the model first and keeps that.
- **Metallurgy:** the perk strips a bad modifier with its own chance, as in vanilla's loot.
- **Co-op:** `BattleGearLootService.Applies` is the switch and `CoopSessionPolicy.IsSinglePlayerAuthority`. Otherwise the prefix
  returns true and vanilla loot runs. The two settings are `PlayerLocal` in `CoopSettingsRelevance`, because they are read only
  behind that predicate, so a differing value never changes a shared simulation.
- **Off stays identical:** with the switch off the prefix returns true before reading the engine or drawing a number.
- **Failure:** an exception is logged once per battle with `[BattleGearLoot]`; vanilla's loot runs if nothing was granted yet.

## Configuration

MCM group "Battle Gear Loot" (`TaomSettings.EnableBattleGearLoot`, `BattleGearLootChancePercent`), read live, no restart.
Defaults and bounds live in `BattleGearLootSettingsProvider`.

## Key Files

| File | Role |
|---|---|
| `Main/Features/BattleGearLoot/BattleGearLootModule.cs` | Registers the service, applies the patch category at game init |
| `Main/Features/BattleGearLoot/BattleGearLootService.cs` | The switch, the single-player test and the roll |
| `Main/Features/BattleGearLoot/Domain/GearLootPolicy.cs` | The exclusion rules |
| `Main/Features/BattleGearLoot/Hooks/Patch172_BattleGearLoot.cs` | The prefix |
| `Main/Adapters/CasualtyGearAdapter.cs` | Builds the kit, grants the picked pieces |

## Dependencies

`IArmourAcquisitionConfigProvider` (the named weapon list, read from the config so it holds even when gating failed), `IRandomProvider`, `ICoopSessionProvider`, `IDedicatedServerProvider`, `IOutcomeLog`.

## Tests

`TAOM.Tests/Features/BattleGearLoot/`: service, policy, settings provider, patch, binding and wiring tests.
`CoopVetoClassificationTests` carries the `GatedForCoop` row.

## How to verify in game

The applied line is `taom.battle-gear-loot.applied` (one per battle, `chance=<n>`); a failure logs `[BattleGearLoot]`. Switch off: a won field battle loots as before. On at 100: every merchandise item of
each beaten enemy appears. On at 10: a few items per battle. Beat a lord with Armour Acquisition on: no heavy, elite, lord or
named piece. Auto-resolve a win, a siege defence won by retreat, a hideout "send troops" win (no body loot) and an enlisted win
(gear share plus the spoils) are the other cases. Beside an allied army: about as many looted bodies as before.

## Performance

One kit rebuild and at most 12 rolls per looted body, on the campaign main thread after a battle.

## Changelog

- v2.0.42 branch: added.

## GitHub Issue

None (waived).
