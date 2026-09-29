# Armour Acquisition

## Overview

Markets and battle loot hand out light and medium armour freely. Heavy, elite and lord pieces are sold
only by a town whose armoury allows them (its Barracks level: heavy at 1, elite at 2, lord at 3). Lord
kit can also be forged at a level 3 armoury, found after a battle won against lords, or earned piece by
piece on **the lord's gear ladder**: six quests, one per slot (hands, legs, shoulders, head, body,
weapon), each done by the hero's own deeds or by handing an armourer the culture's lord's materials. The
twenty-seven named hero weapons and shields are never sold or looted; the named weapons are the weapon
rung's reward for their cultures. At any town's armoury the player can upgrade a carried piece into the
next class of its line for gold, two metals and, for lord kit, the kingdom's special resource. An
artisan's "Armourer's Commission" trades a heavy chest of the town's people for steel, and a village
headman's "Deep Seam" pays lord's materials for timber.

KEYforce proposed the design; Mike set the decisions on 2026-09-27, revised them in the deep review
round the same day, and designed the ladder on 2026-09-28 (#693, below). Target release: TAOM 2.0.33
(2.0.32 shipped without it).

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
| Lord kit | Sold at a level 3 armoury, forged there for the best metal (thamaskene) plus the special resource, the lord's gear ladder (below), and the "Lord's Harness Unclaimed" event |
| Named items | All seventeen hero weapons and shields are never sold or looted, and ten more since 2026-09-28 (Tuor's heirloom axes, Galadriel's sword, the seven Noldor swords); the ladder's weapon rung awards the named weapons (table below) |
| Cultures with no armour of their own | Draw on a related culture's armour for their markets and their lord kit: the Armourer's Commission mapping |
| Market sweep | Daily, per town, not once per save |
| The Animalia moose | Guaranteed Mirkwood stock, like the elk; the rule that keeps XML non-merchandise items out of markets stays |
| Upgrade links | Stay inside one kingdom's kit line |
| Player under-geared against AI lords | Accepted for now (AI lords never shop; their gear comes from templates) |
| Field Commission copying a troop's elite kit | Accepted as earned |
| The upgraded piece's quality modifier | Kept |

### The lord's gear ladder (Mike, 2026-09-28, #693)

Mike asked for the lord's gear to be earned piece by piece, the way the career gear quests of The Old
Realms work (the design follows his description; TOR's code is comparison-only and nothing here derives
from it).

| Question | Decision |
|---|---|
| Structure | Six quests, one per slot, climbed in order: hands, legs, shoulders, head, body, weapon. It replaces the one-quest Lord's Harness |
| Deeds | The hero's own kills, battles won, enemy lords captured |
| The other route | Bring the armourer N of the culture's lord's materials instead of the deeds |
| Reward | The culture's lord piece for the slot, else its best elite piece there |
| Weapon rung | The culture's named weapons where it has them, else a per-culture pick in config |
| Materials | New items, one per culture that owns armour (13); a culture without armour uses its donor's |
| Material sources | Battle drops of the hero's own culture's material, and new notable quests per culture. No smelting |

Defaults set here, for Mike to overrule: a knockout counts as struck down (`count_knockouts`); a rung's
piece is claimed at an armoury of the lord level, the player choosing among the culture's lord pieces for
the slot; with no lord or elite piece in a slot (the Haradrim head and body), the culture's best heavy
piece, and with nothing at all, any culture's lord pieces for the slot; the materials complete a rung
whether or not its quest was taken up.

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
5. **The lord's gear ladder** (#693, `LordsLadderService`). Six `CareerQuest` definitions,
   `taom_lords_gear_<slot>`, each with a `career_id` naming no career (the career behaviour never offers
   one). The armoury lists the current rung (`LadderPresenter`, above the upgrades): take up its quest,
   hand over its lord's materials, or, once it is done, claim its piece at an armoury of the lord level.
   A rung's deeds are the hero's own kills (the new `HeroKills` objective), battles won and lords
   captured. `HeroKillCounterMissionLogic` adds each kill to `HeroKillTally` as it happens, in any mission
   opened while the player's encounter holds a map event (a field battle, a siege, a sally out, a
   hideout, a raid, a naval battle, a duel, a town's alley fight that starts a battle) and in no other (an
   arena, a tournament, a plain settlement scene, a custom battle); `LordsLadderBehavior` credits the
   tally to the owner's running rung quest through `CareerQuest.AddProgress` when the battle ends, then
   rolls the lord's material find. A rung readies for the quest's owner (even after a Player Switcher
   change) when its quest completes, or at once when the materials are handed over (which also completes
   a running quest); a rung with no piece to claim offers no hand-in. The claim offers the culture's lord
   pieces for the slot, else its single best elite piece there, else its best heavy one, else any
   culture's lord pieces for the slot; the weapon rung offers the culture's configured weapons that are
   loaded. A culture with no armour of its own uses its `armour_from` donor's pieces, weapons and
   materials. The save keeps which slots each hero has claimed and which are done, one bit per slot each
   (the `LadderSlot` values are pinned), so a reordered or shortened ladder resumes every hero at their
   first unclaimed rung, and a done rung stays done in its own slot.
6. **The event.** "A Lord's Harness Unclaimed" is a TAOM inquiry after a battle won against lords
   (`LordHarnessService.RollHarnessFind`), not a vanilla `IncidentManager` incident, whose saved cooldown
   dictionary would carry TAOM objects in the save. It hands out class-table lord kit of the right
   culture (never a vanilla piece): the culture's lord pieces, else its elite pieces, else any culture's
   lord pieces, a culture with no armour using its `armour_from` donor.
7. **Lord's materials.** Thirteen TAOM items (`taom_lords_materials.xml`, TAOM's only item file), `Goods`
   in the `unassigned` category, non-merchandise, with no culture and a value of 500: no workshop makes
   them, no caravan buys them, and neither the loot model, plunder, the hideout pool nor
   CultureMarketplace hands them out (the engine facts below). They come from battles won, while a rung
   neither claimed nor done remains, and from "The Deep Seam", a village headman's `LotrIssues` row per
   culture that pays five for timber (`reward_count`): a headman of that culture, or of one that draws on
   it, offers it only to a player of the same group (`for_player_culture`), and an untaken offer is
   withdrawn once the player's culture stops matching (a new campaign creates its first issues before
   character creation).
8. **Co-op.** World changes (the sweep, visiting armourers) run on the campaign's authority; the bench and
   the ladder's rows turn a co-op guest away before anything is charged (the EliteEmissary
   "pay-real-get-phantom" finding); the ladder's battle hand-off and the event skip a guest and a
   dedicated server.

### Engine facts the ladder rests on (v1.5.3, verified 2026-09-28)

- **Kills.** `Mission.OnAgentRemoved` (Mission.cs:3006-3032) raises the killer's `KillCount` for any
  removal across teams, knockouts included, and nulls `Mission.MainAgent` only after every behavior has
  seen the removal: the counter caches the hero's agent once, from `OnMissionTick`, instead of reading
  `MainAgent` late, which also keeps a soldier the player takes control of after the hero falls from
  counting as the hero. Singleplayer can deliver agent removals off the main thread (#634), so the
  counter checks the killer's identity on managed fields first, runs the native enmity check only for the
  hero's own kills (never on a cached agent the engine may have deleted), and adds with `Interlocked`. A
  mount's blow is credited to its rider, as vanilla's `BattleAgentLogic.OnScoreHit` does. Any mission
  opened while the encounter holds a map event counts (`PlayerEncounter.Battle`; the rival-gang alley
  fight calls `StartBattle`, SandBox `RivalGangMovingInIssueBehavior.cs:1030-1041`); a custom battle has
  no `Campaign.Current`.
- **The hand-off.** `PlayerEncounter.DoApplyMapEventResults` (PlayerEncounter.cs:1346-1351) fires
  `OnPlayerBattleEnd` before `CalculateAndCommitMapEventResults` builds the loot. The loot roster is
  public (`RosterToReceiveLootItems`, PlayerEncounter.cs:308, the roster the loot screen opens), but a
  find goes straight into the party inventory with a message instead, so it cannot be left behind on the
  loot screen. A battle the hero leaves before it ends raises no battle end for the player: its kills are
  credited at the next hourly tick, with no find.
- **Items.** An `ItemCategory` declared in XML gets its id and nothing else (Game.cs:314 registers the
  type; it has no deserializer of its own and `MBObjectBase.Deserialize` sets only the id), so no TAOM
  category could carry trade or demand settings; the rows use `unassigned` (DefaultItemCategories.cs:341,
  411), which is no trade good and has no base demand. A category with no base demand still gets 1% of a
  town's prosperity as daily demand (DefaultSettlementEconomyModel.cs:68-71), so a town slowly consumes a
  stack sold to it, and a category that is no trade good prices between 0.8 and 1.3 times the item's value
  (DefaultTradeItemPriceFactorModel.cs:171-175). `Type="Goods"` alone makes an item a trade good
  (`ItemObject.IsTradeGood`, ItemObject.cs:244), whatever its category. The hideout's night loot pool,
  built at session launch, keeps a trade good only if its theoretical market value (ten times its value,
  DefaultTradeItemPriceFactorModel.cs:191-198) plus one is at most 4,750 (HideoutCampaignBehavior.cs:
  161-172), and its first loop skips no non-merchandise item (lines 124-141): a value of 475 or more keeps
  a material out. Vanilla's non-food goods carry only the `Civilian` flag in XML (pottery, linen, leather
  and velvet in SandBoxCore's `items/horses_and_others.xml`), so the material rows carry that flag and no
  component.
- **Icons.** An item's inventory icon is a render of its mesh (`ItemImageIdentifierVM`), never a 2D
  sprite, so each material has its own bar. Vanilla's smithing materials share one material
  (`crafting_materials`: `pbr_metallic`, a 512 `crafting_mat_d/_n/_s` atlas in `core_game.tpac`), each
  mesh sampling its own patch. The thirteen bars (`lord_material_01` to `13`, LOTRLOME_Armory
  `Assets/smithing/lord_materials_geo.tpac`) are vanilla's `thamaskene_steel` cloned with
  `tools/tpac_clone_metamesh.py` onto materials `lord_material_m_01` to `13`, whose 1K `_d` recolours the
  atlas's thamaskene patches per culture over one shared `_n` and `_s` (Mike, 2026-09-28).
- **Retiring a quest.** A `CareerQuest` whose definition is gone from the XML at load becomes a silent
  zombie (`InitializeQuestOnGameLoad` finds no definition, `RegisterEvents` returns early, nothing logs):
  a shipped quest definition is never deleted. The one-quest Lord's Harness shipped in no build (no
  deployed `TAOM.dll` carried it), so the ladder replaced it outright.

### Component Diagram

```
armour_classes.xml (generated)   armour_acquisition_config.xml   culture_marketplace_config.xml
            \                          /                           (armour_from)
   ArmourClassTableProvider   ArmourAcquisitionConfigProvider           |
                  \                  /                        CultureMarketplaceConfigProvider
                  ArmourGateService ---- IArmourItemCatalogAdapter (NotMerchandise setter)
                  /        |        \                                    |
ArmourMarketplaceGate  ArmouryUpgradeService  LordsLadderService / LordHarnessService (event)
 (CultureMarketplace)       |                    |                |
   ArmouryLevelService  ArmouryPresenter --- LadderPresenter   LordsLadderBehavior   LordHarnessEventBehavior
   (IArmouryTownAdapter)    |                (CareerQuest via LadderQuests)   ^
                   ArmouryMenuBehavior       HeroKillCounterMissionLogic --> HeroKillTally
                   ArmourAcquisitionCampaignBehavior (state, daily sweep, visits)
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
| `<NamedWeapons>` | `<Item id>` | the twenty-seven in "Named items" below | Never sold or looted; the weapon rung awards the twenty-five weapons |
| `<LordEvent>` | chance, cooldown_days, leave_relation | 0.08, 90, 5 | |
| `<VisitingArmourer>` | chance_per_day, duration_days, level_bonus | 0.04, 7, 1 | A visit goes only to a town whose Barracks is below 3 |
| `<LordsLadder>` | count_knockouts; `<Step slot quest materials>` | true; hands 10, legs 15, shoulders 20, head 30, body 40, weapon 60 | The rungs in climbing order, each slot once, each on its own career quest; materials 1 to 999 |
| `<LordsMaterials>` | base_chance, chance_per_ten_kills, max_chance, min_units, max_units; `<Material culture item>` | 0.1, 0.01, 0.6, 1, 3; 13 cultures | The find after a battle won: 10%, plus 1 point per ten enemies the hero struck down in it, at most 60%, for 1 to 3 units |
| `<LadderWeapons>` | `<Weapon culture item>` | 39 rows, 14 cultures | The weapon rung's choices per culture, in order |

The rung quests' deeds live in `taom_career_quests.xml` (Mike's placeholders, 2026-09-28):

| Rung | Kills | Battles won | Lords captured | Or materials |
|---|---|---|---|---|
| Hands (the Lord's Gauntlets) | 100 | 10 | 0 | 10 |
| Legs (the Lord's Greaves) | 200 | 20 | 0 | 15 |
| Shoulders (the Lord's Mantle) | 300 | 35 | 0 | 20 |
| Head (the Lord's Helm) | 400 | 50 | 1 | 30 |
| Body (the Lord's Harness) | 500 | 75 | 3 | 40 |
| Weapon (the Lord's Weapon) | 1,000 | 100 | 5 | 60 |

Weapon picks: Gondor, the Rohirrim, Mordor, Mirkwood, Erebor and Rivendell offer their named weapons
(Glamdring is tagged Gondor's in the Armory; Rivendell's are the seven Noldor swords and Tuor's two
heirloom axes, which carry no culture), and Lórien its own, Galadriel's sword, while Lindon still draws
on Rivendell's. The other seven cultures offer a pick from their own Armory weapons, placeholders for
Mike or KEYforce, taken where the culture has any from the weapons no troop, lord or player kit
carries: Dale a halberd and a war spear; the Easterlings two two-handed swords; the Haradrim a sword and a spear; Dunland an axe and a
spear; Dol Guldur its one such weapon, a halberd, and a two-handed mace one of its troops carries;
Isengard (a two-handed sword and axe) and Gundabad (a sword and a mace) have no such weapon, so theirs
are troop weapons.

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
game's gate is on. The ladder has no toggle of its own: it runs while the gate is on.

### Data rows elsewhere

- `lotr_issues/taom_lotr_issues.xml`: 18 `lotr_armourer_commission_*` DeliverGoods rows (Artisan giver,
  4 steel plus up to 12 by difficulty), one per culture group, each rewarding a heavy (Umbar and
  Mirkwood: elite) chest; and 13 `lotr_deep_seam_*` rows, "The Deep Seam" (Headman giver, 6 hardwood
  plus up to 18 by difficulty), one per culture that owns armour and shared by the cultures that draw on
  it, each paying five of the culture's lord's material (`reward_count="5"`).
- `career_system/taom_career_quests.xml`: the six `taom_lords_gear_*` rung quests (above).
- `armour_acquisition/taom_lords_materials.xml`: the thirteen lord's materials, registered as an `Items`
  node in `SubModule.xml` (campaign game types only). Names and art are placeholders: Númenórean Steel,
  Eorling Steel, Erebor Dwarf-steel, Dale-forged Steel, Noldorin Silver-steel, Greenwood Silver, Barad-dûr
  Black Iron, Isengard Forge-iron, Dol Guldur Shadow-iron, Gundabad Orc-steel, Rhûnic Gilt-bronze,
  Haradric Bronze, Dunland Bog-iron.

## Named items

All live in the Armory (`LOTRLOME_items/LOTRAOM_weapons.xml`; the two shields in `LOTRAOM_shields.xml`)
and are never sold or looted. Mike named the first seventeen on 2026-09-27 and ten more on 2026-09-28,
after Tuor's heirloom axe turned up for sale in play. The ladder's weapon rung is the named weapons'
route (Mike, 2026-09-28: "named weapons, then a pick"): a hero of the weapon's culture, or of a culture
that draws on it, chooses one at the top of the ladder. The two shields have no route yet.

| Item | Id | Culture | Route |
|---|---|---|---|
| Andúril (carried by Amrothos, the Gondor lord `lord_1_9_3`) | `anduril` | Gondor | Weapon rung |
| Strider's sword | `strider_sword` | Gondor | Weapon rung |
| Glamdring | `glamdring_sword` | Gondor | Weapon rung |
| Boromir's sword | `wm_gondor_boromir_sword` | Gondor | Weapon rung |
| Faramir's sword | `wm_gondor_faramir_sword` | Gondor | Weapon rung |
| Théoden's sword | `theoden_sword` | Rohirrim | Weapon rung |
| Éomer's sword | `eomer_sword` | Rohirrim | Weapon rung |
| Éowyn's sword | `eowyn_sword` | Rohirrim | Weapon rung |
| Sauron's mace | `wm_sauron_mace` | Mordor | Weapon rung (also Umbar and the orc cultures, through their donor) |
| The Witch-king's sword | `witchking_sword` | Mordor | Weapon rung (likewise) |
| A Nazgûl's sword | `nazgul_sword` | Mordor | Weapon rung (likewise) |
| Legolas's sword | `wm_legolas_sword` | Mirkwood | Weapon rung |
| Thranduil's sword | `wm_thranduil_sword` | Mirkwood | Weapon rung |
| Dáin's hammer | `sm_dwarf_dain_hammer_a` | Erebor | Weapon rung |
| Dáin's axe | `sm_dwarf_dain_axe_a` | Erebor | Weapon rung |
| Fingon's sword | `wm_fingon_sword` | Rivendell | Weapon rung (also Lindon, through its donor) |
| Túrin's sword | `wm_turin_sword` | Rivendell | Weapon rung (likewise) |
| Celegorm's sword | `wm_celegorm_sword` | Rivendell | Weapon rung (likewise) |
| Finwë's sword | `wm_finwe_sword` | Rivendell | Weapon rung (likewise) |
| Ingwë's sword | `wm_ingwe_sword` | Rivendell | Weapon rung (likewise) |
| Finarfin's sword | `wm_finarin_sword` | Rivendell | Weapon rung (likewise) |
| Voronwë's sword | `wm_voronwe_sword` | Rivendell | Weapon rung (likewise) |
| Tuor's one-handed axe, an heirloom | `wm_tuors_axe_1h` | None | Rivendell's weapon rung (Mike, 2026-09-28) |
| Tuor's two-handed axe, an heirloom | `wm_tuors_axe` | None | Rivendell's weapon rung |
| Galadriel's sword | `wm_galadriel_sword` | Lórien | Lórien's own weapon rung row |
| Boromir's shield | `wm_boromir_shield` | Gondor | None yet |
| Théoden's shield | `wm_theoden_shield` | Rohirrim | None yet |

Another route can be a CareerQuest `GrantItem` reward or a LotrIssues `reward_item`, both data-only.

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
| `Main/Features/ArmourAcquisition/LordsLadderService.cs`, `Domain/LordsLadder.cs` | The ladder: rungs, hand-in, rewards, claim, material finds |
| `Main/Features/ArmourAcquisition/HeroKillTally.cs`, `Hooks/HeroKillCounterMissionLogic.cs` | The hero's kills, from the mission to the campaign |
| `Main/Features/ArmourAcquisition/Hooks/LordsLadderBehavior.cs`, `LadderPresenter.cs`, `LadderQuests.cs` | The ladder's boundary: battle hand-off, armoury rows, the rung quests |
| `Main/Features/ArmourAcquisition/LordHarnessService.cs`, `LordHarnessEventPolicy.cs` | The event: the find and which pieces |
| `Main/Features/ArmourAcquisition/ArmourAcquisitionState.cs` | Campaign state (SyncData): rungs claimed, rung ready, event cooldowns, visits |
| `Main/Features/ArmourAcquisition/Hooks/*` | Behaviors, presenters, texts |
| `Main/Features/CareerSystem/Quests/CareerQuest.cs` (`AddProgress`), `Domain/CareerQuestObjectiveDefinition.cs` (`HeroKills`) | The shell's feed for progress no campaign event carries |
| `Main/Features/LotrIssues/*` (`reward_count`) | An issue reward of several items |
| `Main/_Module/ModuleData/armour_acquisition/taom_lords_materials.xml` | The lord's materials |
| `Main/Features/ArmourAcquisition/ArmourAcquisitionIoC.cs`, `ArmourAcquisitionModule.cs` | Registration, feature module |
| `Main/Adapters/ArmourItemCatalogAdapter.cs`, `ArmouryTownAdapter.cs`, `ArmouryPlayerAdapter.cs` | Engine boundaries, keyed by id |
| `Main/Features/CultureMarketplace/IMarketplaceStockGate.cs`, `CultureItemPoolService.cs` (armour_from) | The gate and the pool merge |
| `Main/Features/SpecialResources/ISpecialResourceSpender.cs` | The narrow, checked resource spend |

## Dependencies

- CultureMarketplace (consumer of the stock gate, owner of the `armour_from` map), SpecialResources
  (`ISpecialResourceSpender`), CareerSystem (`ICareerQuestService`, the `CareerQuest` shell and its
  `HeroKills` objective), LotrIssues (data rows and `reward_count`), CoopInterop (`ICoopSessionProvider`,
  `IDedicatedServerProvider`), `ITownRosterAdapter`, the feature-module runner (the kill counter is a
  `MissionBehaviorDecl`).
- `CareerQuestCampaignBehavior` blocks a new career quest only while a quest of the player's current
  career runs, so a ladder rung never holds career quests back (and a quest from a previous career no
  longer blocks the new one).

## Tests

`TAOM.Tests/Features/ArmourAcquisition/`: the config (the ladder's sections included), class-table and
settings providers, the planner and gate service (pieces by slot), state round-trip and session reset
(`RequiresGame`), visiting armourers, armoury level, the market gate, the daily sweep per town, the
upgrade service, the ladder service (`LordsLadderServiceTests`: rungs, owner-keyed readiness, the
hand-in, rewards and their fallbacks, the claim, material finds, the claimed-slot mask with a rung
dropped from the config), the kill tally, its rule and concurrent adds (`HeroKillTallyTests`), the
ladder's domain rules (slot names only, never numerals or comma lists), the state's mask range, the
event service and policy, wiring (the module with its four behaviors, the kill counter's
`MissionBehaviorDecl` built through its factory, the SubModule call, a container that builds them all:
`RequiresGame`), and shipped data through the real providers (every rung's quest, `HeroKills` only in
rung quests, the compiled ladder mirroring the shipped file, the materials' economy attributes, the Deep
Seam's culture map and player filter, a material and weapon picks for all thirteen cultures).
Elsewhere: `SpecialResourceSpenderTests`, `CultureMarketplaceArmourDonorTests` (the pool merge and its
config), the CultureMarketplace injection tests (the per-town filter), the Animalia moose route,
`CareerQuestConfigProviderTests` and `CareerQuestServiceTests` (`HeroKills`),
`LotrIssueConfigProviderTests` (74 shipped issues, `reward_count`, `for_player_culture`),
`LotrIssueServiceTests` (`OffersTo`), `ReflectionSiteBindingTests` (`set_NotMerchandise`). Python:
`tools/tests/test_generate_armour_classes.py`, and in `test_validate_moduledata.py`
`ArmourClassTableDriftTests`, `ArmourAcquisitionRefTests` (the ladder's references included) and
`BuildRegistriesTests` (the item registry counts only `<Items>` documents). The kill counter itself runs
only in a mission: its rule is `HeroKillTally.Counts`, tested; the rest is owed in game.

## Performance

The kill counter is added to every mission but does nothing outside an eligible one. In a campaign
battle it caches the hero's agent once, then answers each agent removal with managed identity checks;
the native enmity check and one `Interlocked` add run only for the hero's own kills. The find rolls once
per battle, and the hourly fallback is one `Interlocked.Exchange`. The ladder's armoury rows are built
only when the menu opens.

## How to regenerate after an Armory art drop

1. `python tools/generate_armour_classes.py` (dry run: the class counts and the drift).
2. `python tools/generate_armour_classes.py --apply`, then commit the table with the art-drop work.
3. `python tools/validate_moduledata.py` stops warning `ARMOUR_CLASS_TABLE_DRIFT`, and reports
   `ARMOUR_ACQUISITION_REF` for any commission culture or reward, named item, upgrade metal, `armour_from`
   culture, ladder weapon pick, lord's material or rung quest that no longer resolves.

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
  ordinary merchandise), "The Deep Seam" still pays lord's materials that nothing spends until the switch
  is on again, the career-quest blocking change above still applies, and the `armour_from` pool merge
  still runs, so the nine receiving cultures' markets carry their donor's armour of every class. The kill
  counter counts nothing, and a rung quest that still completes readies its rung without the message.
- **Mount barding** is not gated (only character armour is). AI caravans never buy armour (their buy
  value is 0 for any category that is neither a trade good nor an animal), so they carry none between
  towns.
- **The ladder's kills:** a trample by the hero's horse counts only if the engine credits the blow to the
  rider (native collision code, unverified); damage over time and falls count only when the engine
  attributes them to the hero's last blow. Kills in a battle the hero leaves early are credited an hour
  later and roll no find. The quest journal shows the kills once the battle ends, not during it.
- **The hand-off's timing** (UNVERIFIED): a battle's kills wait in the tally until the battle ends or
  the next hourly tick. An hourly tick between the mission's end and the battle's end would credit them
  first, and the find would then roll at the base chance; kills from a battle left early count toward
  the next battle's find if it ends within the hour. An encounter menu stops time, so neither should be
  common.
- **Editing the ladder mid-campaign:** reordering or removing a `<Step>` resumes every hero at their
  first unclaimed rung, and a done rung keeps its readiness in its own slot, so no other rung inherits it;
  a rung quest already running for a rung that is no longer current gets no more credit (only the current
  rung's quest is fed).
- **Tuor's two axes** carry no localization key in the Armory (`name="[Heirloom] Tuor's ..."`), so their
  names read in English in every language until the Armory gives them one.
- **The ladder's pieces** come with no quality modifier. The weapon rung of Umbar and the orc cultures
  offers Mordor's named weapons (their `armour_from` donor), Sauron's mace among them, until a culture
  row of its own is added.
- **Lord's materials leave through trade:** they sell to a town at the non-trade-good price band, about
  0.8 to 1.3 times their value of 500, and the stack sits in that market until the town's small daily
  demand consumes it; the vanilla "Trade Proposal" incident can pick a stack of them and pay two or three
  times the value (the player's choice, IncidentsCampaignBehaviour.cs:1168-1224, 3352-3355); trade
  rumours list them; QuickActions' "Sell Low Value" sells them when its threshold is set at or above
  their price (the default is 100).

## Owed

- **Translation:** 102 new keys (88 module, 14 issue) are registered and seeded with English in all 12
  languages. The paid translator run is Mike's call (`translate_with_claude.py --module TAOM --sync-ids
  --apply`); until then `NoTranslatedString_MixesWritingSystems` flags the English rows in CN, JP, KO and RU.
- **Names and art:** the thirteen material names and the weapon picks of the seven cultures without named
  weapons are placeholders for Mike or KEYforce. The bars, their materials and textures live only in the
  live Armory (unversioned): the Kit must save the Armory once so the game loads the new package, and an
  in-game look is owed.
- **GitHub issue:** the base feature has none; filing it is Mike's call (the repo is public).
- **Mike's rulings:** the builder's defaults in "The lord's gear ladder" above, plus a rung with no piece
  offering no hand-in, drops stopping after the last rung, a material's value of 500 (it keeps them out
  of the hideout's night loot), and the Deep Seam's price and player-culture filter; the rung numbers and
  the drop curve after play; a route for the two shields; and whether the seven cultures' weapon picks
  join `<NamedWeapons>` (never sold or looted, as the named weapons are) or stay merchandise that troops
  of Isengard, Gundabad and Dol Guldur already carry.
- **In-game checklist:** a new campaign's markets hold heavy pieces everywhere, elite pieces only in
  Barracks 2 and 3 towns, lord kit only in Barracks 3 towns, and no named item anywhere; a Lindon or orc
  town stocks its donor's armour; a Mirkwood town always has the moose; a won battle drops no gated piece;
  "Visit the armoury" upgrades a medium piece with steel and keeps its modifier; the lord forge takes
  thamaskene and the resource; the event's two choices, "Take it" with the loot screen open included; an
  Armourer's Commission turn-in (the chest arrives); a visiting armourer's message and level bonus, and
  the elite stock leaving the day after he goes; an old save's markets clear of gated stock within a day.
  The ladder: the armoury lists the hands rung; taking it up starts the quest; a field battle's kills show
  in the journal when it ends (and a hideout's, and a siege's); a knockout counts; a horse's trample
  (does it?); a won battle sometimes yields the hero's culture's material, more often with more kills;
  handing over ten materials readies the rung and completes the running quest; the claim needs a level 3
  armoury and offers the slot's pieces; the next rung follows; the weapon rung offers the named weapons
  (Gondor) and the picks (Dale); a Lindon hero climbs on Rivendell's kit and material; a Deep Seam turn-in
  pays five materials; materials never appear in a workshop, a caravan or a hideout's night loot. The
  deeds route end to end: one rung done by kills, battles and captures alone, readied by its quest, then
  claimed. The fallbacks: a Haradrim head or body rung offers the best heavy piece; an orc culture's weapon
  rung offers Mordor's weapons. A save and reload mid-ladder keeps the claimed rungs, a ready rung and a
  running quest's progress. No kills count in an arena, a tournament, a plain town scene or a custom
  battle; a battle left early credits its kills within the hour and yields no find. A Player Switcher change
  mid-rung: the new hero can take up their own rung while the old hero's quest still runs. With the master switch
  off, the town menu offers no armoury and battles count nothing; a co-op guest is turned away at the
  armoury and gets no finds.

## GitHub Issue

The lord's gear ladder: #693. The base feature: none yet (see Owed).

## Changelog

- 2026-09-27: built (KEYforce's design, Mike's decisions): the class table and its generator, the
  NotMerchandise gate, the market gate and sweep, the armoury bench, the Lord's Harness, the event,
  visiting armourers, the Armourer's Commission rows.
- 2026-09-27, deep review round: engine types out of the services (town ids, ADR-007); lord kit sold at
  level 3; all seventeen hero items named; the `armour_from` culture map for markets and lord kit; the
  daily sweep; the Lord's Harness as one quest; the moose routed; upgrade links kept inside one kit
  line; visiting armourers only where they raise the level; the gate campaign-only and restored on
  failure; the `ARMOUR_ACQUISITION_REF` gate; wiring tests.
- 2026-09-28: the lord's gear ladder (#693) replaced the one-quest Lord's Harness, which never shipped:
  six rung quests on the hero's own kills (the `HeroKills` objective and its mission counter), battles and
  captives, or lord's materials; thirteen material items, their battle finds and "The Deep Seam" issue
  rows (`reward_count`); the weapon rung's picks; the ladder's rows at the armoury; the gate extended to
  the ladder's references.
- 2026-09-28: ten more named weapons (Mike): Tuor's two heirloom axes, Galadriel's sword and the seven
  Noldor swords; Rivendell's weapon rung offers the swords and Tuor's axes, Lórien's Galadriel's sword.
