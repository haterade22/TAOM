# Armour Acquisition

## Overview

Markets and battle loot hand out light and medium armour freely. Heavy, elite and lord pieces are sold
only by a town whose armoury allows them (its Barracks level: heavy at 1, elite at 2, lord at 3). Lord
kit can also be forged at a level 3 armoury, or earned through a quest or a rare event, and seventeen
named hero weapons and shields never change hands. At any town's armoury the player can upgrade a
carried piece into the next class of its line for gold, two metals and, for lord kit, the kingdom's
special resource. An artisan's "Armourer's Commission" trades a heavy chest of the town's people for steel.

KEYforce proposed the design; Mike set the decisions on 2026-09-27 and revised them in the deep review
round the same day (below). Target release: TAOM 2.0.32.

## Why This Exists

- **Vanilla behaviour:** every town runs a hidden `artisans` workshop that turns leather and iron into
  light, medium, heavy and ultra armour (about 0.8, 0.3, 0.15 and 0.075 pieces a day), preferring the
  town's culture and falling back to any culture's; a defeated troop can drop the merchandise it wears;
  a tournament with four or more hero entrants awards a culture item of engine tier 4 and up. TAOM's
  CultureMarketplace added six more culture items per town per day with no filter at all: lord kit and
  Andúril were on sale.
- **TAOM requirement:** heavy, elite and lord armour is earned or bought where a proper armoury stands,
  not off any stall; the upgrade at the armoury is the path from what a player loots to what an elite
  soldier wears.
- **Without this feature:** a player buys lord kit anywhere on day one, and every named weapon with a
  culture tag can turn up in a market or a prize pool.

## Decisions (Mike, 2026-09-27)

| Question | Decision |
|---|---|
| Market thresholds | A town's armoury level: heavy at 1, elite and heavy at 2, lord and all at 3 (the review round: lord kit is sold at 3, as KEYforce proposed) |
| KEYforce's "armoury or some building is tier X-Y-Z" | The Barracks level. Every live town starts at 1 or more (19 at 1, 33 at 2, 26 at 3), so heavy is on sale in every town, elite in 59 of 78, lord kit in 26 |
| Loot of a heavy, elite, lord or named piece | Never drops; the loot roll runs over the troop's other gear |
| Lord kit | Sold at a level 3 armoury, forged there for the best metal (thamaskene) plus the special resource, the "Lord's Harness" quest, and the "Lord's Harness Unclaimed" event |
| Named items | All seventeen hero weapons and shields are never sold, looted or awarded until Mike sets each one's route (table below) |
| Cultures with no armour of their own | Draw on a related culture's armour for their markets and their lord kit: the Armourer's Commission mapping |
| Market sweep | Daily, per town, not once per save |
| The Lord's Harness | One quest, three deeds in any order |
| The Animalia moose | Guaranteed Mirkwood stock, like the elk; the rule that keeps XML non-merchandise items out of markets stays |
| Upgrade links | Stay inside one kingdom's kit line |
| Player under-geared against AI lords | Accepted for now (AI lords never shop; their gear comes from templates) |
| Field Commission copying a troop's elite kit | Accepted as earned |
| The upgraded piece's quality modifier | Kept |

## Architecture

### Design Challenge

Armour reaches the player through at least five engine channels (workshops, loot, tournament prizes,
map-event plunder, hideout loot) plus TAOM's CultureMarketplace. Patching each would be five fragile
seams. And a quarter of the Armory's armour (747 of 2,860 pieces: all of Dale, Harad and Mirkwood,
nearly all of Rohan and Rivendell) carries no light/medium/heavy/elite/lord token in its id, while the
engine's own ItemCategory is an absolute tier that ignores TAOM's per-kingdom caps (#583).

### Solution Approach

1. **One class per piece, generated.** `tools/generate_armour_classes.py` classes every character armour
   piece in the live Armory and writes `armour_classes.xml`: named hero kit
   (`rebalance_armor.is_excluded`) first, then the `_lord` token (a lord variant is a lord variant
   whoever wears it), then `_civ` kit, then `derive_armor_tiers`' tier (the lowest battle wearer's band,
   else the id token), then the band whose kingdom-cap target is nearest the piece's primary stat. Each
   row also names the piece the armoury upgrades it into: the nearest class above it with a candidate,
   in the piece's own tokened line first (`fix_armour_mesh_ladder.split_id`), then among same-slot pieces
   of the same kit line (`rebalance_armor.kingdom_key`, else the Armory folder: the mordor folder holds
   the orc, Uruk and Black Númenórean kits) sharing the longest id prefix; lord is a target only from
   elite. Armour the table does not list (vanilla pieces, pieces added since the last run) is classed by
   engine tier at runtime.
2. **The engine's own switch.** `ItemObject.NotMerchandise` keeps an item out of workshop production
   (`WorkshopsCampaignBehavior.IsProducable`), battle loot (`DefaultBattleRewardModel`), the regular
   tournament pool and TAOM's `BuildPrizePool`, map-event plunder and hideout loot. It is
   `{ get; private set; }`, set only from XML, so `ArmourGateService.ApplyGating` flips it through the
   private setter for every heavy, elite, lord and named piece, from `SubModule.OnGameInitializationFinished`
   on every campaign init (items reload from XML with each game; a custom battle or the editor is left
   alone). That hook runs after the items load (`Campaign.InitializeDefaultCampaignObjects`) and before a
   new game's workshops fill their item cache (`OnNewGameCreatedPartialFollowUp`). Nothing is saved; the
   MCM toggle applies from the next game load. An init that fails partway restores what it flipped.
3. **The armoury gate on markets.** CultureMarketplace is the one TAOM inflow the flag does not reach. Its
   daily draw asks `IMarketplaceStockGate.ForTown(townId)` (owned by CultureMarketplace, implemented by
   `ArmourMarketplaceGate`) for that town's eligibility test: heavy pieces need armoury level 1, elite 2,
   lord 3, named pieces never qualify, and an item whose XML marks it non-merchandise never does (the
   ranged ladders and starter kits were leaking). A town's level is its Barracks level plus a visiting
   master armourer's bonus, capped at 3 (`ArmouryLevelService.GetTownLevel`). A culture with no armour of
   its own draws on another's: `<Culture id="lindon" armour_from="rivendell" />` in
   `culture_marketplace_config.xml` merges the donor's character armour into its pool. A **daily sweep**
   (`DailyTickSettlementEvent`) takes out of each town's market the gated pieces its armoury does not
   allow today: stock from before the gate, stock a visiting armourer allowed after he leaves, AI lords
   selling old loot, pieces the player sells.
4. **The armoury bench.** "Visit the armoury" in every town menu lists the carried pieces that have a next
   piece, priced (flat gold plus a share of the value gained, the recipe's metals, the special resource
   for lord kit), greyed with the reason when the armoury level, gold, metals or treasure fall short. The
   offer and the upgrade judge the price by one rule; the piece is taken before anything is charged and
   handed back if the resource spend is refused.
5. **Lord kit routes.** "The Lord's Harness" is one `CareerQuest` definition, `taom_lords_harness`, whose
   `career_id` names no career (the career behaviour never offers it): take two enemy lords captive, raise
   clan renown to 900 and win 12 battles, in any order. `LordHarnessQuestBehavior` offers it at an armoury
   of the lord level in a town of the player's own culture to a player who owns an elite piece, carried
   or worn; when it completes, its owner (even after a Player Switcher change) claims one lord piece at
   such an armoury. The event, "A Lord's Harness Unclaimed", is a TAOM inquiry after a battle won against
   lords (`LordHarnessService.RollHarnessFind`), not a vanilla `IncidentManager` incident, whose saved
   cooldown dictionary would carry TAOM objects in the save. Both hand out class-table lord kit of the
   right culture (never a vanilla piece): the culture's lord pieces, else its elite pieces, else any
   culture's lord pieces, a culture with no armour using its `armour_from` donor.
6. **Co-op.** World changes (the sweep, visiting armourers) run on the campaign's authority; the bench
   turns a co-op guest away before charging (the EliteEmissary "pay-real-get-phantom" finding); the
   quest and the event skip a guest and a dedicated server.

### Component Diagram

```
armour_classes.xml (generated)   armour_acquisition_config.xml   culture_marketplace_config.xml
            \                          /                           (armour_from)
   ArmourClassTableProvider   ArmourAcquisitionConfigProvider           |
                  \                  /                        CultureMarketplaceConfigProvider
                  ArmourGateService ---- IArmourItemCatalogAdapter (NotMerchandise setter)
                  /        |        \                                    |
ArmourMarketplaceGate  ArmouryUpgradeService  LordHarnessService --------+
 (CultureMarketplace)       |                        |
   ArmouryLevelService  ArmouryPresenter     LordHarnessQuestBehavior / LordHarnessEventBehavior
   (IArmouryTownAdapter)    |
                   ArmouryMenuBehavior       ArmourAcquisitionCampaignBehavior (state, daily sweep, visits)
```

## Configuration

### Generated: `Main/_Module/ModuleData/armour_acquisition/armour_classes.xml`

One `<Item id class next>` row per piece. Never hand-edit: `python tools/generate_armour_classes.py --apply`.
On 2026-09-27: 2,860 pieces, light 419, medium 400, heavy 725, elite 1,031, lord 169, named 116; 2,071 of
the 2,575 upgradable pieces have an upgrade target inside their kit line (the rest are mostly elite
pieces with no lord sibling).

### Hand-edited: `Main/_Module/ModuleData/armour_acquisition/armour_acquisition_config.xml`

| Element | Field | Current | Meaning |
|---|---|---|---|
| `<ArmourAcquisition>` | `enabled` | true | The master switch when MCM is not loaded; with MCM, its toggle decides |
| `<Gate>` | `heavy` / `elite` / `lord` | 1 / 2 / 3 | Armoury level a class needs, to be sold and to be upgraded into (0 to 3, in order) |
| `<Upgrade target="medium">` | gold, value_share, materials | 150, 0.25, 3 wrought iron + 2 crude iron | |
| `<Upgrade target="heavy">` | | 500, 0.35, 3 steel + 4 iron | |
| `<Upgrade target="elite">` | | 1,500, 0.5, 4 fine steel + 3 steel | |
| `<Upgrade target="lord">` | + special_resource | 3,000, 0.5, 6 thamaskene, 150 resource | |
| `<NamedWeapons>` | `<Item id>` | the seventeen in "Named items" below | Never sold, looted or awarded |
| `<LordEvent>` | chance, cooldown_days, leave_relation | 0.08, 90, 5 | |
| `<VisitingArmourer>` | chance_per_day, duration_days, level_bonus | 0.04, 7, 1 | A visit goes only to a town whose Barracks is below 3 |
| `<LordHarness>` | offer_cooldown_days | 30 | Days before a declined quest is offered again |

Validated at load (ranges, NaN, gate order, class names); a bad value reverts to its compiled default with
a warning. Reuse.Singleton: a change needs a full restart. The metal prices behind the recipes are
vanilla's (`DefaultItems.cs`): crude iron 20, wrought iron 30, iron 60, steel 100, fine steel 160,
thamaskene 260. Special resources earn about 14 per battle won, so 150 is roughly ten victories.

### `culture_marketplace_config.xml`

Nine `<Culture id armour_from>` rows, the Armourer's Commission mapping: Lindon and Lórien draw on
Rivendell, Abanissa and Shaghana on Harad (`aserai`), Khand (`battania`) on Rhûn (`khuzait`), and the
three orc cultures and Umbar on Mordor. Those cultures own 22 of the 78 towns. The Animalia moose is
routed to Mirkwood with `min_stock="1"`.

### MCM: "Armour Acquisition"

`Enable Armour Acquisition`, `Lord's Harness After Battle`, `Visiting Master Armourers` (all on). The master
switch applies from the next game load; the two sub-toggles are read live, and matter only while that
game's gate is on.

### Data rows elsewhere

- `lotr_issues/taom_lotr_issues.xml`: 18 `lotr_armourer_commission_*` DeliverGoods rows (Artisan giver,
  4 steel plus up to 12 by difficulty), one per culture group, each rewarding a heavy (Umbar and
  Mirkwood: elite) chest.
- `career_system/taom_career_quests.xml`: `taom_lords_harness`, the one quest (above).

## Named items (Mike decides each)

All live in the Armory (`LOTRLOME_items/LOTRAOM_weapons.xml`; the two shields in `LOTRAOM_shields.xml`)
and are never sold, looted or awarded until a route is set.

| Item | Id | Route (to decide) |
|---|---|---|
| Andúril (carried by Amrothos, the Gondor lord `lord_1_9_3`) | `anduril` | |
| Strider's sword | `strider_sword` | |
| Théoden's sword | `theoden_sword` | |
| Sauron's mace | `wm_sauron_mace` | |
| Glamdring | `glamdring_sword` | |
| Éomer's sword | `eomer_sword` | |
| Éowyn's sword | `eowyn_sword` | |
| The Witch-king's sword | `witchking_sword` | |
| A Nazgûl's sword | `nazgul_sword` | |
| Boromir's sword | `wm_gondor_boromir_sword` | |
| Faramir's sword | `wm_gondor_faramir_sword` | |
| Legolas's sword | `wm_legolas_sword` | |
| Thranduil's sword | `wm_thranduil_sword` | |
| Dáin's hammer | `sm_dwarf_dain_hammer_a` | |
| Dáin's axe | `sm_dwarf_dain_axe_a` | |
| Boromir's shield | `wm_boromir_shield` | |
| Théoden's shield | `wm_theoden_shield` | |

A route can be a CareerQuest `GrantItem` reward or a LotrIssues `reward_item`, both data-only.

## Key Files

| File | Purpose |
|---|---|
| `tools/generate_armour_classes.py` | The class table generator and its drift check |
| `Main/Features/ArmourAcquisition/ArmourGateService.cs`, `ArmourGatePlanner.cs` | Classes and the NotMerchandise gate |
| `Main/Features/ArmourAcquisition/ArmourAcquisitionConfigProvider.cs`, `ArmourClassTableProvider.cs`, `ArmourAcquisitionSettingsProvider.cs` | Config, class table and MCM, validated |
| `Main/Features/ArmourAcquisition/Domain/*` | Classes, config, records, upgrade offers |
| `Main/Features/ArmourAcquisition/ArmourMarketplaceGate.cs` | CultureMarketplace's per-town stock gate |
| `Main/Features/ArmourAcquisition/ArmouryLevelService.cs`, `VisitingArmourerService.cs` | Town armoury level |
| `Main/Features/ArmourAcquisition/ArmourStockSweepService.cs` | The daily market sweep |
| `Main/Features/ArmourAcquisition/ArmouryUpgradeService.cs` | Upgrade offers and execution |
| `Main/Features/ArmourAcquisition/LordHarnessService.cs`, `LordHarnessEventPolicy.cs` | Lord kit: quest, claim, event, which pieces |
| `Main/Features/ArmourAcquisition/ArmourAcquisitionState.cs` | Campaign state (SyncData) |
| `Main/Features/ArmourAcquisition/Hooks/*` | Behaviors, presenter, texts |
| `Main/Features/ArmourAcquisition/ArmourAcquisitionIoC.cs`, `ArmourAcquisitionModule.cs` | Registration, feature module |
| `Main/Adapters/ArmourItemCatalogAdapter.cs`, `ArmouryTownAdapter.cs`, `ArmouryPlayerAdapter.cs` | Engine boundaries, keyed by id |
| `Main/Features/CultureMarketplace/IMarketplaceStockGate.cs`, `CultureItemPoolService.cs` (armour_from) | The gate and the pool merge |
| `Main/Features/SpecialResources/ISpecialResourceSpender.cs` | The narrow, checked resource spend |

## Dependencies

- CultureMarketplace (consumer of the stock gate, owner of the `armour_from` map), SpecialResources
  (`ISpecialResourceSpender`), CareerSystem (`ICareerQuestService`, the `CareerQuest` shell), LotrIssues
  (data rows only), CoopInterop (`ICoopSessionProvider`, `IDedicatedServerProvider`), `ITownRosterAdapter`.
- `CareerQuestCampaignBehavior` now blocks a new career quest only while a quest of the player's current
  career runs, so the harness quest never holds career quests back (and a quest from a previous career
  no longer blocks the new one).

## Tests

`TAOM.Tests/Features/ArmourAcquisition/`: the config, class-table and settings providers, the planner and
gate service, state round-trip and session reset (`RequiresGame`), visiting armourers, armoury level, the
market gate, the daily sweep per town, the upgrade service, the harness service (offer, owner-keyed
completion, claim, lord kit choices, the event roll) and event policy, wiring (the module, the SubModule
call, a container that builds all four behaviors: `RequiresGame`), and shipped data through the real
providers. Elsewhere: `SpecialResourceSpenderTests`, `CultureMarketplaceArmourDonorTests` (the pool merge
and its config), the CultureMarketplace injection tests (the per-town filter), the Animalia moose route,
`LotrIssueConfigProviderTests` (61 shipped issues), `ReflectionSiteBindingTests` (`set_NotMerchandise`).
Python: `tools/tests/test_generate_armour_classes.py`, and `ArmourClassTableDriftTests` and
`ArmourAcquisitionRefTests` in `test_validate_moduledata.py`.

## How to regenerate after an Armory art drop

1. `python tools/generate_armour_classes.py` (dry run: the class counts and the drift).
2. `python tools/generate_armour_classes.py --apply`, then commit the table with the art-drop work.
3. `python tools/validate_moduledata.py` stops warning `ARMOUR_CLASS_TABLE_DRIFT`, and reports
   `ARMOUR_ACQUISITION_REF` for any commission culture or reward, named item, upgrade metal or
   `armour_from` culture that no longer resolves.

Run it after any troop roster change that moves a piece's lowest wearer across a band, too.

## Known limitations

- **The vanilla elite prize fallback:** TAOM's prize pool falls back to vanilla's 31 fixed elite ids when
  a culture's pool of engine tier 4 and up is empty, and that list does not check the flag. How often it
  fires is unmeasured.
- **The player's own pieces:** the flag also keeps the player's gated pieces from being plundered when
  the player's party is defeated, and from the 15% given up on "try to get away".
- **Saved prizes:** a tournament running when the gate first applies keeps its saved prize.
- **Enlistment:** rank kits are issued with no gate check (each rank is re-earned by service).
- **Start kits:** the `starter_*` twins have no class row, so the armoury cannot upgrade them; they are
  non-merchandise, so the gate does not need them.
- **With the master switch off:** the Armourer's Commission quests still appear (their chests are then
  ordinary merchandise), the career-quest blocking change above still applies, and the `armour_from` pool
  merge still runs, so the nine receiving cultures' markets carry their donor's armour of every class.
- **Mount barding** is not gated (only character armour is). AI caravans never buy armour (their buy
  value is 0 for any category that is neither a trade good nor an animal), so they carry none between
  towns.

## Owed

- **Translation:** 58 new keys are registered and seeded with English in all 12 languages. The paid
  translator run is Mike's call (`translate_with_claude.py --module TAOM --sync-ids --apply`, about $0.12 a
  language); until then `NoTranslatedString_MixesWritingSystems` flags the English rows in CN, JP, KO
  and RU.
- **GitHub issue:** none filed yet; the repo is public, so filing is Mike's call.
- **In-game checklist:** a new campaign's markets hold heavy pieces everywhere, elite pieces only in
  Barracks 2 and 3 towns, lord kit only in Barracks 3 towns, and no named item anywhere; a Lindon or orc
  town stocks its donor's armour; a Mirkwood town always has the moose; a won battle drops no gated piece;
  "Visit the armoury" upgrades a medium piece with steel and keeps its modifier; the lord forge takes
  thamaskene and the resource; the Lord's Harness offer, its three deeds (renown already over 900 shows
  as done), and the claim; the event's two choices, "Take it" with the loot screen open included; an
  Armourer's Commission turn-in (the chest arrives); a visiting armourer's message and level bonus, and
  the elite stock leaving the day after he goes; an old save's markets clear of gated stock within a day.

## GitHub Issue

None yet (see Owed).

## Changelog

- 2026-09-27: built (KEYforce's design, Mike's decisions): the class table and its generator, the
  NotMerchandise gate, the market gate and sweep, the armoury bench, the Lord's Harness, the event,
  visiting armourers, the Armourer's Commission rows.
- 2026-09-27, deep review round: engine types out of the services (town ids, ADR-007); lord kit sold at
  level 3; all seventeen hero items named; the `armour_from` culture map for markets and lord kit; the
  daily sweep; the Lord's Harness as one quest; the moose routed; upgrade links kept inside one kit
  line; visiting armourers only where they raise the level; the gate campaign-only and restored on
  failure; the `ARMOUR_ACQUISITION_REF` gate; wiring tests.
