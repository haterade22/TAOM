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
| Harad's eight tier-3 and tier-4 clans raised to tier 5: 19 party slots to 27 | `spclans.xslt` | this page, "Harad party slots" |
| Auto-resolve power ratio exponent 0.7 to 0.5: numbers outweigh quality | `AutoResolve.PowerRatioExponent` in `configs/battle_balance_config.json` | [battle-balance.md](battle-balance.md) "Auto-resolve: numbers over quality" |
| Blue Craig's defenders get +10% advantage in an auto-resolved siege of its own fortification, and survival 0.3 | `SiegeDefence.DefenderAdvantageBonuses` and `CulturalSurvivalBonuses` in `configs/battle_balance_config.json` | [battle-balance.md](battle-balance.md) "Siege defence by culture" |
| Blue Craig's garrisons cost no wages or food, take no starvation wounds, and grow by 2 free troops a day | `SiegeDefence.UpkeepFreeGarrisons` and `DailyGarrisonGrowth`, new `TaomSettlementGarrisonModel` | [battle-balance.md](battle-balance.md) "Garrison upkeep by realm" |

### Isengard party slots

`DefaultClanTierModel.GetPartyLimitForTier` gives a non-minor clan 1 party below tier 3, 2 for tiers 3 and 4, and 3 from tier 5; `TaomClanTierModel` changes only the companion limit. Isengard had 11 clans: tiers 6, 5, 4 and 4, and seven at tier 3, which is 24 slots. Rohan has 31, Gondor 26, Mordor 24, Dunland 17. Clans `clan_isengard_4` to `_9` and `_11` go to tier 5, and each has at least three adult heroes to lead its three parties (`clan_isengard_4` has four), so the change gives 31 slots. More lords in the old clans would add no party.

A tier also adds 15 to each lord's party size and 25 to the clan leader's (`DefaultPartySizeLimitModel`, `GetTierEffectInternal`), so these clans field larger parties as well: +30 per lord and +50 per clan leader. The tier also raises what each clan starts with: renown from 350 to 679 at tier 3 to 2,350 to 4,629 at tier 5, and influence by about 100 per clan (`DefaultClanTierModel.CalculateInitialRenown`, `CalculateInitialInfluence`). AI lords spend influence to call armies, so part of any Isengard gain in the run comes from that, not from the slots. A clan with exactly three adults now has no spare hero for a Lord Retinue (`LordRetinueWorld`). The XML tier is read when a campaign starts (`Clan` deserialization, `Tier` from the `tier` attribute) and only renown can raise it later, so the change reaches new campaigns only. `clan_isengard_2` is tier 5 with two adult heroes, so one of its slots stays empty; that is unchanged.

### Harad party slots

Harad's clans are the vanilla `clan_aserai_1` to `_9`, and `spclans.xslt` sets each one's tier over the SandBox value. They were at tiers 5, 4, 4, 4, 4, 3, 3, 3 and 4, which is 19 slots; clans 2 to 9 now sit at tier 5, which is 27. Each has three to seven living adult heroes (`clan_aserai_8` has exactly three), so every slot has a leader. The Isengard paragraph above holds for these clans too: larger parties (+15 per tier for a lord, +25 for the clan leader), starting renown of 2,350 or more with the influence it brings, more weight in the engine's war scoring (`FactionHelper.FindPotentialStrength` sums tier times 100 per clan), and new campaigns only. Part of any Harad gain in a run therefore comes from those, not from the slots, and `clan_aserai_8` has no spare hero for a Lord Retinue.

## Configuration

See the linked feature docs for each file. The two new fields:

| Field | File | Shipped | Description |
|---|---|---|---|
| `CapturedFortHold.Factions` | `army_targeting.json` | `["empire_s"]` | Realms whose armies hold a captured fort whatever their side |
| `AutoResolve.PowerRatioExponent` | `battle_balance_config.json` | 0.5 | Exponent of the power ratio in a simulated strike (vanilla 0.7) |
| `SiegeDefence.DefenderAdvantageBonuses` | `battle_balance_config.json` | `bluecraig` 0.1 | Factor on the defenders' advantage in an auto-resolved siege of a fortification that culture owns |
| `SiegeDefence.UpkeepFreeGarrisons` | `battle_balance_config.json` | `bluecraig` true | The realm's garrisons cost no wages, eat no food and take no starvation wounds |
| `SiegeDefence.DailyGarrisonGrowth` | `battle_balance_config.json` | `bluecraig` 2 | Free basic troops each fortification of the realm gains a day |

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/characters/clans.xml` | Isengard clan tiers |
| `Main/_Module/ModuleData/spclans.xslt` | Harad clan tiers (`clan_aserai_1` to `_9`) |
| `Main/Features/ArmyTargeting/CapturedFortHoldConfig.cs` | `Factions`, `HoldsArmy` |
| `Main/Features/BattleBalance/AutoResolveQualityMath.cs` | The strike damage factor |
| `Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs` | Applies it in `SimulateHit` |

## Tests

- `TAOM.Tests/Core/IsengardPartySlotTests.cs`: Isengard's 11 clans give 31 slots by the engine's tier rule.
- `TAOM.Tests/Core/HaradPartySlotTests.cs`: Harad's nine clans in `spclans.xslt` give 27 slots by the same rule.
- `TAOM.Tests/Features/ArmyTargeting/HoldTheWalls/CapturedFortHoldConfigTests.cs` and `HoldTheWallsStartTests.cs`: `Factions` validation and a listed realm holding while an unlisted Evil realm does not.
- `TAOM.Tests/Features/ArmyTargeting/WarTheaterConfigInvariantsTests.cs`: every shipped `Factions` id is a kingdom, and Mordor is listed.
- `TAOM.Tests/Features/BattleBalance/AutoResolveQualityMathTests.cs`, `BattleBalanceConfigProviderValidationTests.cs`, `ShippedBattleBalanceConfigTests.cs`, `TaomCombatSimulationModelTests.cs`: the factor, its validation, the shipped value and the wiring.
- `TAOM.Tests/Features/Diplomacy/DiplomacyShippedConfigTests.cs`: Dunland is not Hostile to Gondor; every row's ids are kingdoms.

## How to verify in game

The [scenario autorun](scenario-autorun.md) plays the run with nobody at the keyboard. It starts the game unattended, so it needs Mike's word, and a first run is `--days 3`.

1. Deploy the checkout with `pwsh ./build.ps1`; the launcher does not build. Then run `python tools/run_scenario.py --days 150`. It starts a new campaign (the clan tiers load only then), keeps your party out of the wars and quits at day 150.
2. Read `ledger.txt` in the run folder under `E:\Temp\scenario-runs\`. The runner writes it with `analyze_war_ledger.py`.
3. For a person's run instead, run `./build.ps1`, start a new campaign, keep your party out of the wars, and on days 45, 60, 100 and 150 record these items:
   - who holds East Osgiliath (`town_EW3`), Cair Andros (`castle_EW4`), Helm's Deep and Edoras;
   - Dunland's wars;
   - Isengard's party count;
   - each realm's strength in the kingdom list;
   - how many lords died, and on which side.
4. Run `python tools/analyze_war_ledger.py` on the session log. It reads the `[WarLedger]` lines.
5. Grep the log for `[HoldTheWalls]` lines with a Mordor leader, `War of the Ring: empire` and `[Diplomacy] War declaration blocked`.
6. Grep the log for `[Diplomacy] War declaration allowed:` lines that hold both `empire_w` and `empire`, in either order. The word in brackets is the engine path: `CausedByKingdomDecision` is an AI war vote, `CausedByCallToWarAgreement` a call to war (the called realm comes first), and `Default` a scripted or TAOM war such as the War of the Ring or War Pledge. For a call to war, the `[Diplomacy] Call to war started: <caller> calls <called> against <target>` line before it names the realm that made the call; a started call with no declaration line after it is a war TAOM refused. Pair the lines by their ids: another realm's line can fall between two lines of one war.

If Rivendell or Arthedain calls a war between Gondor and Dunland after day 44, open a follow-up. The exponent also applies to your own Send troops, so a player battle is a second check.

## The first 150-day run

Run `E:\Temp\scenario-runs\20261115-163251` (build 879c63d15, which also holds the #1330 skill curve and the #1336 crush-through
change) passed 150 days. The `[WarLedger]` `str` is `Kingdom.CurrentTotalStrength`, the sum of each clan's war parties and garrisons,
valued by head count and troop tier only (`KingdomWarSnapshotAdapter.cs:77`), so gear and skills do not move it.

- Mordor stormed Cair Andros on day 23, lost it on day 51, and stormed it again on day 58. It held it to day 150. East Osgiliath was
  never assaulted.
- Isengard went from 3 to 8 castles, and Rohan went from 7 to 0. Isengard's strength stayed between 6,278 and 7,566.
- Dunland and Gondor made peace on day 134, so they were at war before it, though TAOM blocked the scripted declaration on day 1. TAOM
  logged only the declarations it blocked; each declaration it allows now writes `[Diplomacy] War declaration allowed: <a> -> <b>
  (<DeclareWarDetail>)`, which names the engine path.
- Gondor's lead over Mordor grew from 8,014 to 15,231. Gondor gained a fief on 13 days, and Harad lost one on 11 of them: Harad fell
  from 11 castles to 3. Harad had 19 party slots, so its nine clans now sit at tier 5, each with at least three adult heroes to lead
  (27 slots). An estimate from the party templates puts most of Gondor's day-1 lead in its troop tiers (mean tier 4.58 against Mordor's 3.26) and its 27 fiefs, which this change does not touch; the garrison share is UNVERIFIED.

## The second 150-day run

Run `E:\Temp\scenario-runs\20261115-172417` (build `879c63d15.dirty`, built 2026-11-15 23:23 UTC, with the Harad tiers and both war
lines) passed 150 of 150 days with a clean unload. It met the three targets the first run set:

| Target | First run | Second run |
|---|---|---|
| The log names how Dunland and Gondor went to war | no line | `Call to war started: mistymountainorcs calls empire against empire_w`, then `War declaration allowed: empire -> empire_w (CausedByCallToWarAgreement)`, day 2 |
| Harad holds more than 3 castles on day 150 | 3 (4 towns) | 16 (6 towns) |
| The Gondor to Mordor strength gap ends below 15,231 | 8,014 on day 1, 15,231 on day 150 | 8,674 on day 1, 6,108 on day 150 |

In the second run, Gondor went from 11 towns and 16 castles to 9 and 9 (the first run ended at 12 and 19); Mordor from 6 and 8 to 7 and 11. Isengard ended with 1 town and 4 castles, after
2 and 7 on day 100. Each figure is one campaign, so a single run cannot separate the tier change from chance.

Dunland's war with Gondor comes from its alliance: the Misty Mountain Orcs call Dunland in against Gondor. On the maintainer's decision
(2026-11-15), Dunland now refuses every call to war against Gondor: the `callToWarRefusals` list in `diplomacy.json`
([diplomacy.md](diplomacy.md)).

## The third 150-day run

Run `E:\Temp\scenario-runs\20261115-193557` (build `2a48c3d99.dirty`, built 2026-11-16 01:35 UTC, with Dunland's call-to-war
refusal) passed 150 of 150 days with a clean unload. Dunland refused three calls to war against Gondor (`Call to war refused: empire
will not join a war against empire_w`), and the log has no war declaration between the two realms.

| Day 150 | Second run | Third run |
|---|---|---|
| Gondor towns/castles, strength | 9/9, 21,631 | 11/15, 23,883 |
| Mordor towns/castles, strength | 7/11, 15,523 | 6/10, 13,734 |
| Gondor to Mordor gap (day 1 gap) | 6,108 (8,674) | 10,149 (6,466) |
| Harad towns/castles | 6/16 | 5/10 |
| Dunland towns/castles | 3/6 | 4/6 |

The gap stayed below the first run's 15,231 but grew from day 1, where the second run's shrank. The ledger's capital target failed:
Blue Craig lost its capital on day 75 and was destroyed on day 99. In the second run Blue Craig lived with 1 town. Each run is one
campaign, so these runs cannot say whether Dunland's peace with Gondor freed Gondor to fight Blue Craig.

## The fourth 150-day run

Run `E:\Temp\scenario-runs\20261116-100801` (trunk `9dc562c62`, built 2026-11-16 16:01 UTC, with the refusal and the #1350 and #1381
dialogue fixes) passed 150 of 150 days with a clean unload. Dunland again refused three calls to war against Gondor, and the log has no
war declaration between the two realms.

| Day 150 | Second run | Third run | Fourth run |
|---|---|---|---|
| Gondor towns/castles, strength | 9/9, 21,631 | 11/15, 23,883 | 10/15, 24,060 |
| Mordor towns/castles, strength | 7/11, 15,523 | 6/10, 13,734 | 7/10, 15,468 |
| Gondor to Mordor gap (day 1 gap) | 6,108 (8,674) | 10,149 (6,466) | 8,592 (8,646) |
| Harad towns/castles | 6/16 | 5/10 | 5/9 |
| Dunland towns/castles | 3/6 | 4/6 | 4/6 |
| Blue Craig capital lost, destroyed | no, no | day 75, day 99 | day 53, day 122 |

The free share stayed within 0.4 to 0.6 on all 150 days (maximum deviation 0.042, no lead change), and the gap held level with day 1.
The capital target failed again, earlier: Blue Craig lost its capital on day 53 and was destroyed on day 122. In the second, third and
fourth runs, ten free realms declared war on Blue Craig on day 2 (Gondor, Dale, Rohan, Erebor, Lorien, Rivendell, Mirkwood, and Lindon
called in by Rivendell; Blue Craig declared on Arthedain and Ered Luin), so its enemies did not change between runs. Blue Craig fell in
both runs with Dunland's refusal and lived in the one run without it. Three campaigns cannot prove the cause, but the pattern fits
Gondor having no Dunland front.

## The fifth 150-day run

Run `E:\Temp\scenario-runs\20261116-104744` (trunk `ab43768ce`, built 2026-11-16 16:47 UTC, with Blue Craig Hostile only to its
neighbours) passed 150 of 150 days with a clean unload. Blue Craig started with four wars instead of ten: Rivendell, Arthedain and Ered
Luin by its Hostile rows, and Lindon, which Arthedain called in. Dunland refused two calls to war against Gondor and stayed at peace
with it.

| Day 150 | Fourth run | Fifth run |
|---|---|---|
| Gondor towns/castles, strength | 10/15, 24,060 | 10/13, 24,910 |
| Mordor towns/castles, strength | 7/10, 15,468 | 9/11, 16,883 |
| Gondor to Mordor gap (day 1 gap) | 8,592 (8,646) | 8,027 (6,206) |
| Rohan towns/castles | 5/3 | 3/1 |
| Arthedain towns/castles | 2/6 | 0/5 |
| Blue Craig capital lost, destroyed | day 53, day 122 | day 73, day 113 |

The free share stayed within 0.4 to 0.6 on all 150 days (maximum deviation 0.033). Blue Craig still fell: it lost its capital 20 days
later than in the fourth run and was destroyed 9 days earlier. Its four neighbours alone took it, so the six distant wars were not
what decided its fate; Blue Craig sits on a closed land-navigation island and can reach no other realm (`WarTheaterAndReachTests`),
so it cannot carry the war to anyone. Arthedain also lost its capital, on day 78, and ended with no town. Rohan fell from 7 towns and
7 castles to 3 and 1.

## The sixth 150-day run

Run `E:\Temp\scenario-runs\20261116-114451` (trunk `3d2c1fb27`, built 2026-11-16 17:44 UTC, with Blue Craig's siege defence bonus
0.1 and survival bonus 0.3) passed 150 of 150 days with a clean unload. Blue Craig had the same four enemies as in the fifth run:
Arthedain and Ered Luin, on which it declared war, Rivendell, and Lindon, which a call to war brought in. Dunland refused one call to
war against Gondor and stayed at peace with it.

| Day 150 | Fifth run | Sixth run |
|---|---|---|
| Gondor towns/castles, strength | 10/13, 24,910 | 11/17, 27,782 |
| Mordor towns/castles, strength | 9/11, 16,883 | 6/9, 13,420 |
| Gondor to Mordor gap (day 1 gap) | 8,027 (6,206) | 14,362 (8,336) |
| Rohan towns/castles | 3/1 | 7/6 |
| Arthedain towns/castles | 0/5 | 2/6 |
| Blue Craig capital lost, destroyed | day 73, day 113 | day 54, day 140 |

The free share stayed within 0.4 to 0.6 on all 150 days (mean deviation 0.044, maximum 0.074 on day 149). Blue Craig lost three of its
four castles by day 30, then its capital on day 54 and its last castle on day 74. Its last town held alone from day 74
to day 140, 27 days longer than the realm lasted in the fifth run. The capital, however, fell
19 days earlier than in the fifth run. With one run per change, part of both differences is chance. No capital other than Blue Craig's
fell. Gondor ended 14,362 strength ahead of Mordor, the widest gap of the last three runs, and Mordor held 6 towns and 9 castles.

## The 300-day runs

Five runs of 300 days, each with a clean unload, tested Blue Craig's free garrisons (#1283). Runs 1 to 3 changed only
`DailyGarrisonGrowth`; runs A and B also raised `DefenderAdvantageBonuses` from 0.1 to 0.4 in the deployed config, which was
restored after each test. Runs 2, 3, A and B logged the new `t=garrison` ledger line.

| Run (`E:\Temp\scenario-runs\...`) | Build | Growth, defence | Blue Craig capital lost, destroyed | Lindon joined by | Sieges of Blue Craig held by | Gondor to Mordor gap, day 300 |
|---|---|---|---|---|---|---|
| 1 (`20261116-135428`) | `829b047d0` | 2, 0.1 | day 55, day 88 | call to war | Lindon 7 | 12,693 |
| 2 (`20261116-154124`) | `9e7c0178e` | 2, 0.1 | held, day 251 | kingdom decision | Ered Luin 11 | 808 |
| 3 (`20261116-162421`) | `9e7c0178e` | 3, 0.1 | day 58, day 94 | call to war | Lindon 4, Ered Luin 2 | 15,069 |
| A (`20261116-165321`) | `9e7c0178e` | 2, 0.4 | day 61, day 112 | call to war | Lindon 6, Ered Luin 1 | 15,205 |
| B (`20261116-172137`) | `9e7c0178e` | 2, 0.4 | day 85, day 129 | call to war | Ered Luin 5, Lindon 4 | 18,786 |

"Sieges held by" counts the `[HoldTheWalls]` lines of lords holding a Blue Craig fortification, by the lord id's realm prefix.

The free garrisons work: Blue Craig's garrisons grew from about 200 men per fortification on day 1 to 400 to 650, and still fell.
Neither lever moved the outcome beyond chance: the two runs at the shipped values ended 160 days apart, and growth 3 and defence
0.4 did no better than growth 2 and defence 0.1. In every run Blue Craig had the same four enemies: it declared war on Arthedain and
Ered Luin, Rivendell declared war on it, and Lindon joined. Blue Craig lasted to day 251 only in run 2, where Lindon joined by its
own decision and no Lindon lord besieged a Blue Craig fortification. With five runs this is a correlation, not a proof.

Other capitals lost: Gondor's on day 15 in run 2 (it never lost one in the other runs), and late losses for Dale, Harad, Rohan
and Arthedain. The Gondor to Mordor gap on day 300 was 12,693 to 18,786 in four runs and 808 in run 2.

## Still owed

- Blue Craig still falls before the maintainer's target of about day 300 (2026-11-16): days 88 to 251 in five 300-day runs. Garrison growth and the siege defence bonus did not move it beyond chance; the runs point at Lindon's lords, who join through a call to war. The next lever was diplomacy; the maintainer chose to keep the current values and end the tuning (2026-11-16, #1283).
- Gondor lost its capital on day 15 in one 300-day run. Watch it in the next runs (#1283).
- The Gondor to Mordor gap widened to 14,362 in the sixth run (8,027 in the fifth), and was 12,693 to 18,786 on day 300 in four of five 300-day runs. Watch it before any change (#1283).
- Gondor's survival row (0.3, shared with Arthedain) and its tier 7+ party template stacks: not changed, because the second run
  closed the gap. Revisit only if a later run shows the gap again.
- A Codex review of 5101f151b and of these changes: the account's usage limit blocks Codex until 2026-11-20.
- Isengard in `CapturedFortHold.Factions`: decide from the run.
- A gear (armour) factor in auto-resolve: considered and not added, because Gondor has the heaviest armour and the factor would widen the lead #1283 reported.

## Changelog

- 2026-11-16: Blue Craig tuning ends: the maintainer keeps growth 2 and defence 0.1, and Blue Craig may fall before day 300 (#1283).
- 2026-11-16: Five 300-day runs recorded; Blue Craig falls on day 88 to 251, and garrison growth and the siege defence bonus do not change that (#1283).
- 2026-11-16: Blue Craig's garrisons cost no wages or food, take no starvation wounds, and grow by 2 free troops a day (#1283).
- 2026-11-16: Sixth 150-day run recorded; Blue Craig, with its siege defence bonus, lasts to day 140 but loses its capital on day 54 (#1283).
- 2026-11-16: Blue Craig gets a siege defence bonus of 0.1 and the survival bonus 0.3 (#1283).
- 2026-11-16: Fifth 150-day run recorded; Blue Craig still falls with four wars (#1283).
- 2026-11-16: Blue Craig keeps Hostile rows only with its Eriador neighbours (#1283).
- 2026-11-16: Fourth 150-day run recorded; Blue Craig fell again (#1283).
- 2026-11-15: First 150-day run recorded; Harad's clans raised to tier 5; allowed war declarations logged (#1283).
- 2026-11-15: Page created. Mordor holds captured forts, Isengard's seven tier-3 clans raised to tier 5, auto-resolve exponent 0.5 (#1283).
- 2026-11-14: Dunland-Gondor Hostile row removed, Mordor survival row removed, Mirkwood survival row added (#1283, 5832fc5c6).

## GitHub Issue

- **Issue:** #1283
- **Status:** Open: the in-game run is owed.
