# Faction Balance (War of the Ring)

## Overview

A set of data and tuning changes from #1283, after a player reported that Isengard is weak, Dunland fights Gondor in every campaign, Mordor takes the Osgiliath forts and then stalls, and Gondor shows 7,000 to 10,000 more strength than its enemies. Each change sits in the feature that owns its data; this page lists them together with the evidence behind each and what is still owed.

## Why This Exists

- **Vanilla behavior:** party slots come from clan tier, auto-resolve weighs troop power at `^0.7` per strike, and an army that takes a fortress leaves after 10 hours.
- **TAOM requirement:** the evil realms must be able to wage the War of the Ring: Isengard against Rohan, Mordor against Gondor, without Dunland drawn into a war it has no reason to fight.
- **Without these changes:** Dunland split its armies across a Gondor front; Mordor's wounded died more often than any culture's; Isengard fielded 24 parties against Rohan's 31; Gondor's elite troops won auto-resolved battles against much larger orc hosts; Mordor's armies marched off the forts they took.

## Architecture

### Design Challenge

Most of the gap lives in engine rules that a data file cannot see: party slots follow clan tier, not lord count; auto-resolve reads troop level, mount and hit points, not gear; a Hostile row in `diplomacy.json` forces a war that Full War then blocks from ending. An outside handoff proposed nine fixes, and several rested on wrong readings of these rules (the review is in [rca-faction-balance-data-2026-11-14.md](../reviews/rca-faction-balance-data-2026-11-14.md)).

### Solution Approach

| Change | Where | Doc |
|---|---|---|
| Dunland is no longer Hostile to Gondor | `diplomacy/diplomacy.json` | [diplomacy.md](diplomacy.md) |
| Mordor's -0.2 survival row removed; Mirkwood gets Rivendell's 0.4 | `configs/battle_balance_config.json`, `BattleBalanceConfig.cs` | [battle-balance.md](battle-balance.md) |
| Mordor's armies hold a captured fort (Hold the Walls by realm) | `CapturedFortHold.Factions` in `configs/army_targeting.json` | [hold-the-walls.md](hold-the-walls.md) |
| Isengard's seven tier-3 clans raised to tier 5: 24 party slots to 31 | `characters/clans.xml` | this page |
| Auto-resolve power ratio exponent 0.7 to 0.5: numbers outweigh quality | `AutoResolve.PowerRatioExponent` in `configs/battle_balance_config.json` | [battle-balance.md](battle-balance.md) "Auto-resolve: numbers over quality" |

### Isengard party slots

`DefaultClanTierModel.GetPartyLimitForTier` gives a non-minor clan 1 party below tier 3, 2 for tiers 3 and 4, and 3 from tier 5; `TaomClanTierModel` changes only the companion limit. Isengard had 11 clans: tiers 6, 5, 4 and 4, and seven at tier 3, which is 24 slots. Rohan has 31, Gondor 26, Mordor 24, Dunland 17. Clans `clan_isengard_4` to `_9` and `_11` go to tier 5, and each has at least three adult heroes to lead its three parties (`clan_isengard_4` has four), so the change gives 31 slots. More lords in the old clans would add no party.

A tier also adds 15 to each lord's party size and 25 to the clan leader's (`DefaultPartySizeLimitModel`, `GetTierEffectInternal`), so these clans field larger parties as well: +30 per lord and +50 per clan leader. The tier also raises what each clan starts with: renown from 350 to 679 at tier 3 to 2,350 to 4,629 at tier 5, and influence by about 100 per clan (`DefaultClanTierModel.CalculateInitialRenown`, `CalculateInitialInfluence`). AI lords spend influence to call armies, so part of any Isengard gain in the run comes from that, not from the slots. A clan with exactly three adults now has no spare hero for a Lord Retinue (`LordRetinueWorld`). The XML tier is read when a campaign starts (`Clan` deserialization, `Tier` from the `tier` attribute) and only renown can raise it later, so the change reaches new campaigns only. `clan_isengard_2` is tier 5 with two adult heroes, so one of its slots stays empty; that is unchanged.

## Configuration

See the linked feature docs for each file. The two new fields:

| Field | File | Shipped | Description |
|---|---|---|---|
| `CapturedFortHold.Factions` | `army_targeting.json` | `["empire_s"]` | Realms whose armies hold a captured fort whatever their side |
| `AutoResolve.PowerRatioExponent` | `battle_balance_config.json` | 0.5 | Exponent of the power ratio in a simulated strike (vanilla 0.7) |

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/characters/clans.xml` | Isengard clan tiers |
| `Main/Features/ArmyTargeting/CapturedFortHoldConfig.cs` | `Factions`, `HoldsArmy` |
| `Main/Features/BattleBalance/AutoResolveQualityMath.cs` | The strike damage factor |
| `Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs` | Applies it in `SimulateHit` |

## Tests

- `TAOM.Tests/Core/IsengardPartySlotTests.cs`: Isengard's 11 clans give 31 slots by the engine's tier rule.
- `TAOM.Tests/Features/ArmyTargeting/HoldTheWalls/CapturedFortHoldConfigTests.cs` and `HoldTheWallsStartTests.cs`: `Factions` validation and a listed realm holding while an unlisted Evil realm does not.
- `TAOM.Tests/Features/ArmyTargeting/WarTheaterConfigInvariantsTests.cs`: every shipped `Factions` id is a kingdom, and Mordor is listed.
- `TAOM.Tests/Features/BattleBalance/AutoResolveQualityMathTests.cs`, `BattleBalanceConfigProviderValidationTests.cs`, `ShippedBattleBalanceConfigTests.cs`, `TaomCombatSimulationModelTests.cs`: the factor, its validation, the shipped value and the wiring.
- `TAOM.Tests/Features/Diplomacy/DiplomacyShippedConfigTests.cs`: Dunland is not Hostile to Gondor; every row's ids are kingdoms.

## How to verify in game

The [scenario autorun](scenario-autorun.md) plays the run with nobody at the keyboard. It starts the game unattended, so it needs Mike's word, and a first run is `--days 3`.

1. Run `python tools/run_scenario.py --days 150`. It deploys the checkout, starts a new campaign (the clan tiers load only then), keeps your party out of the wars and quits at day 150.
2. Read `ledger.txt` in the run folder under `E:\Temp\scenario-runs\`. The runner writes it with `analyze_war_ledger.py`.
3. For a person's run instead, run `./build.ps1`, start a new campaign, keep your party out of the wars, and on days 45, 60, 100 and 150 record these items:
   - who holds East Osgiliath (`town_EW3`), Cair Andros (`castle_EW4`), Helm's Deep and Edoras;
   - Dunland's wars;
   - Isengard's party count;
   - each realm's strength in the kingdom list;
   - how many lords died, and on which side.
4. Run `python tools/analyze_war_ledger.py` on the session log. It reads the `[WarLedger]` lines.
5. Grep the log for `[HoldTheWalls]` lines with a Mordor leader, `War of the Ring: empire` and `[Diplomacy] War declaration blocked`.

If Rivendell or Arthedain calls a war between Gondor and Dunland after day 44, open a follow-up. The exponent also applies to your own Send troops, so a player battle is a second check.

## Still owed

- The in-game run above (#1283, label `triage-needs-ingame`).
- A Codex review of 5101f151b and of these changes: the account's usage limit blocks Codex until 2026-11-20.
- Isengard in `CapturedFortHold.Factions`: decide from the run.
- A gear (armour) factor in auto-resolve: considered and not added, because Gondor has the heaviest armour and the factor would widen the lead #1283 reported.

## Changelog

- 2026-11-15: Page created. Mordor holds captured forts, Isengard's seven tier-3 clans raised to tier 5, auto-resolve exponent 0.5 (#1283).
- 2026-11-14: Dunland-Gondor Hostile row removed, Mordor survival row removed, Mirkwood survival row added (#1283, 5832fc5c6).

## GitHub Issue

- **Issue:** #1283
- **Status:** Open: the in-game run is owed.
