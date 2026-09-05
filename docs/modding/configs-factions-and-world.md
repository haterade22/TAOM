# Configs: factions and the world

## What this file is

This chapter covers thirteen configuration files, not one: the small JSON and XML files carrying
TAOM's faction-level and world-level decisions (who is on whose side, who fights whom and when,
which fronts an AI army defends, who stands guard in a town, what an emissary sells, which items
reach which market, what the faction-select screen shows, who carries a banner, who radiates dread,
who cannot be captured, how fast a messenger travels). None is a TaleWorlds format, so none appears
in `SubModule.xml` as an `<XmlName>` row; each is read by path by a TAOM provider class under
`Main/Features/`. That difference matters more than it sounds: a TaleWorlds XML reloads when the
campaign does, and every file in this chapter does not.

## Where it lives and how it is registered

All thirteen files live under `Main/_Module/ModuleData/`. None is registered in
`Main/_Module/SubModule.xml`; each provider builds its own path from `IPathService.ModuleDataPath`
and opens the file directly, so there is no `<XmlName id>` row to add when you create one.

| File (under `Main/_Module/ModuleData/`) | Root or top level | Provider | Registered in |
|---|---|---|---|
| `execution/alignment.json` | flat object, id to side | `Main/Features/Execution/AlignmentConfigProvider.cs` | `Main/Features/Execution/ExecutionIoC.cs:10` |
| `diplomacy/diplomacy.json` | `relationships` array | `Main/Features/Diplomacy/DiplomacyConfigProvider.cs` | `Main/Features/Diplomacy/DiplomacyIoC.cs:12` |
| `diplomacy/war_of_the_ring.json` | object | `Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs` | `Main/Features/Diplomacy/DiplomacyIoC.cs:15` |
| `configs/army_targeting.json` | object | `Main/Features/ArmyTargeting/ArmyTargetingConfigProvider.cs` | `Main/Features/ArmyTargeting/ArmyTargetingIoC.cs:11` |
| `settlement_guards/settlement_guards_config.xml` | `<SettlementGuards>` | `Main/Features/SettlementGuards/SettlementGuardConfigProvider.cs` | `Main/Features/SettlementGuards/SettlementGuardsIoC.cs:9` |
| `elite_emissary/elite_emissary_config.xml` | `<EliteEmissary>` | `Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs` | `Main/Features/EliteEmissary/EliteEmissaryIoC.cs:10` |
| `culture_marketplace/culture_marketplace_config.xml` | `<CultureMarketplaceConfig>` | `Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs` | `Main/Features/CultureMarketplace/CultureMarketplaceIoC.cs:14` |
| `factionmap/factions.json` | object, faction id to entry | `Main/Features/FactionMap/FactionConfigProvider.cs` | `Main/Features/FactionMap/FactionMapIoC.cs:13` |
| `factionmap/regions.json` | object, region id to entry | same provider | same |
| `banner_bearers/banner_bearers_config.json` | object | `Main/Features/BannerBearers/BannerBearerConfigProvider.cs` | `Main/Features/BannerBearers/BannerBearersIoC.cs:9` |
| `uncapturable_heroes/uncapturable_heroes_config.json` | object | `Main/Features/UncapturableHeroes/UncapturableHeroesConfigProvider.cs` | `Main/Features/UncapturableHeroes/UncapturableHeroesIoC.cs:13` |
| `dread_aura/dread_aura_config.json` | object | `Main/Features/DreadAura/DreadAuraConfigProvider.cs` | `Main/Features/DreadAura/DreadAuraIoC.cs:9` |
| `messengers/messenger_config.json` | object | `Main/Features/Messengers/MessengerConfigProvider.cs` | `Main/Features/Messengers/MessengerIoC.cs:10` |

**All 12 providers are registered `Reuse.Singleton`** into a container built once at module load, so
every file here is read once per Bannerlord process.
<!-- measured: rg -n "Register<I(ArmyTargeting|Alignment|BannerBearer|DreadAura|Messenger|CultureMarketplace|UncapturableHeroes|Diplomacy|WarOfTheRing|SettlementGuard|EliteEmissary|Faction)ConfigProvider, [A-Za-z]+>\(Reuse.Singleton\)" Main/ | wc -l 2026-09-05 -->
Editing any of them takes a **full application restart**. A save reload does not do it, and neither
does starting a new campaign.

Two of these files point at content that ships in a dependency module: `banner_bearers` race ids
come from `LOTRLOME_Armory/ModuleData/skins.xml`, and the routed mounts in `culture_marketplace`
come from `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`. This file lives in the
game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator
gate with any fix. The settlement ids used by `elite_emissary` and `army_targeting` are likewise
owned by `TAOM_Map/ModuleData/settlements.xml`.

## Attributes

### The kingdom-keyed files

<!-- engine-ref type="TAOM.Features.Execution.AlignmentService" file="Main/Features/Execution/AlignmentService.cs" lines="9-53" -->

`execution/alignment.json` has no fixed key set. Every key is a faction id and every value is one of
`free`, `evil`, `neutral`.

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| any id | string | no | `Neutral` | Side for that id. Parsed case-insensitively; an unrecognised value logs a warning and becomes `Neutral` | `AlignmentService.cs:18-27` |

The dictionary answers **both** `GetKingdomSide` and `GetCultureSide` from the same flat map
(`AlignmentService.cs:32-34`), which is why the shipped file carries **24 keys**: the 22 live
kingdom StringIds plus `gondor` and `mordor`, the two cultures whose kingdom carries a vanilla id
(`empire_w` is Gondor, `empire_s` is Mordor). It is not a kingdom-only map, and the "16 keys" and
"22 keys" figures in [alignment-aware-execution.md](../features/alignment-aware-execution.md) and
[alignment-recruitment.md](../features/alignment-recruitment.md) are both stale.
<!-- measured: python -c "import json,collections;d=json.load(open('Main/_Module/ModuleData/execution/alignment.json'));print(len(d),collections.Counter(d.values()))" 2026-09-05 -->

<!-- engine-ref type="TAOM.Features.Diplomacy.Models.KingdomRelationship" file="Main/Features/Diplomacy/Models/KingdomRelationship.cs" lines="5-7" -->

`diplomacy/diplomacy.json` is one key, `relationships`, holding an array of pair rows.

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `kingdomA` | string | yes | `""` | One side of the pair; order does not matter | `KingdomRelationship.cs:5` |
| `kingdomB` | string | yes | `""` | The other side | `KingdomRelationship.cs:6` |
| `tier` | enum | yes | `Neutral` | `Permanent`, `Natural`, `Neutral` or `Hostile` | `KingdomRelationship.cs:7`, `AllianceTier.cs:3-9` |

A pair with no row resolves `AllianceTier.Neutral` (`DiplomacyService.cs:46`), so omission is silent.

<!-- engine-ref type="TAOM.Features.Diplomacy.Models.WarOfTheRingConfig" file="Main/Features/Diplomacy/Models/WarOfTheRingConfig.cs" lines="5-34" -->

| Attribute (`war_of_the_ring.json`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `enabled` | bool | no | `true` | Master switch for the scripted escalation | `WarOfTheRingConfig.cs:7` |
| `phase1` | object | no | a `PhaseConfig` with day 30 | Phase 1 block | `WarOfTheRingConfig.cs:8` |
| `phase2` | object | no | a `PhaseConfig` with day 44 | Phase 2 block. The compiled default is deliberately later than Phase 1's | `WarOfTheRingConfig.cs:9-12` |
| `testMode` | object | no | disabled, days 1 and 3 | Short trigger days for testing | `WarOfTheRingConfig.cs:13`, `:31-34` |

<!-- engine-ref type="TAOM.Features.ArmyTargeting.ArmyTargetingConfig" file="Main/Features/ArmyTargeting/ArmyTargetingConfig.cs" lines="11-60" -->

| Attribute (`configs/army_targeting.json`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Theaters` | string[] | no | empty | The closed set of legal theater names. A membership entry naming anything else is skipped with a warning | `ArmyTargetingConfig.cs:11` |
| `KingdomTheaters` | map of string to string[] | no | empty | Kingdom StringId to its ordered fronts. The first entry is that kingdom's primary front; an empty list marks a deliberately passive kingdom | `ArmyTargetingConfig.cs:23` |
| `FactionPriorityTargets` | map of string to string[] | no | empty | Ordered settlement targets. Earlier entries score higher, decaying across the list | `ArmyTargetingConfig.cs:31` |
| `FactionAggressionMultipliers` | map of string to float | no | empty | Inflates `ourStrength` before vanilla's 2x defender gate | `ArmyTargetingConfig.cs:33` |
| `BorderRescueRadiusInTownGaps` | float | no | `3.2` | Bounds `Patch22`'s rescue only | `ArmyTargetingConfig.cs:47` |
| `PrimaryTheaterWeight` | float | no | `1.25` | Weight for the first listed theater | `ArmyTargetingConfig.cs:50` |
| `SecondaryTheaterWeight` | float | no | `1.0` | Weight for later listed theaters | `ArmyTargetingConfig.cs:53` |
| `ForeignTheaterWeight` | float | no | `0.35` | Weight for a theater the kingdom does not list. Must be above zero | `ArmyTargetingConfig.cs:60` |

Ordering is enforced on load: foreign at or below secondary, secondary at or below primary
([army-targeting.md:134](../features/army-targeting.md)). A kingdom absent from `KingdomTheaters`
weights neutral, which is what keeps player-founded and rebel kingdoms working. The size of the
priority boost is not in this file: it is the MCM knob `ArmyPriorityBoost`, default 3.0 and clamped
to 1.0 through 5.0, read live on every call (`Main/Features/ArmyTargeting/ArmyTargetingSettingsProvider.cs:29`,
`:51-52`). The boost decays linearly from that value to 1.0 across the list
(`Main/Features/ArmyTargeting/ArmyTargetingService.cs:74`).

### The settlement-keyed files

<!-- engine-ref type="TAOM.Features.SettlementGuards.SettlementGuardConfigProvider" file="Main/Features/SettlementGuards/SettlementGuardConfigProvider.cs" lines="78-163" -->

| Attribute (`settlement_guards_config.xml`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Settlement>@id` | string | yes | entry skipped | Settlement StringId; highest-priority pool | `SettlementGuardConfigProvider.cs:83-87` |
| `<Clan>@id` | string | yes | entry skipped | Clan StringId; second-priority pool | `SettlementGuardConfigProvider.cs:89-95` |
| `<Culture>@id` | string | yes | entry skipped | Culture StringId; lowest-priority pool | `SettlementGuardConfigProvider.cs:97-103` |

<!-- engine-ref type="TAOM.Features.EliteEmissary.EliteEmissaryConfigProvider" file="Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs" lines="72-146" -->

| Attribute (`elite_emissary_config.xml`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<EliteEmissary>@enabled` | bool | no | `true` | Master switch for the emissary dialogue | `EliteEmissaryConfigProvider.cs:72` |

<!-- engine-ref type="TAOM.Features.CultureMarketplace.Domain.RoutedItem" file="Main/Features/CultureMarketplace/Domain/RoutedItem.cs" lines="13-15" -->

`culture_marketplace_config.xml` is optional. When it is absent or empty the pools are derived from
`MBObjectManager` ([culture-marketplace.md:62](../features/culture-marketplace.md)). The shipped
file carries **no `<Culture>` blocks at all**: it is one `<Routing>` section with 14 `<Item>` rows.
<!-- measured: python -c "import xml.etree.ElementTree as ET,collections;r=ET.parse('Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml').getroot();print(len(r.findall('Culture')),len(r.findall('Routing')),collections.Counter(e.tag for e in r.iter()))" 2026-09-05 -->

### The battlefield and presentation files

<!-- engine-ref type="TAOM.Features.BannerBearers.Domain.BannerBearerConfig" file="Main/Features/BannerBearers/Domain/BannerBearerConfig.cs" lines="10-115" -->

| Attribute (`banner_bearers_config.json`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Enabled` | bool | no | `true` | Off is exact vanilla behaviour | `BannerBearerConfig.cs:10` |
| `MinimumFormationTroopCount` | int | no | `4` | Formation size floor before a bearer is assigned; valid 2 to 100 | `BannerBearerConfig.cs:16` |
| `MaxBearersPerFormation` | int | no | `6` | Ceiling; 6 is the engine's arrangement-table size | `BannerBearerConfig.cs:21` |
| `AllowedFormationGroups` | string[] | no | `["Infantry"]` | Which formation classes may carry a banner | `BannerBearerConfig.cs:29` |
| `InfantryBannerPerSoldiers` | int | no | `10` | One banner per N soldiers; `0` disables the class | `BannerBearerConfig.cs:34` |
| `RangedBannerPerSoldiers` | int | no | `25` | Inert while `Ranged` is not allowed | `BannerBearerConfig.cs:35` |
| `CavalryBannerPerSoldiers` | int | no | `15` | Inert while `Cavalry` is not allowed | `BannerBearerConfig.cs:36` |
| `HorseArcherBannerPerSoldiers` | int | no | `15` | Inert while `HorseArcher` is not allowed | `BannerBearerConfig.cs:37` |
| `OtherBannerPerSoldiers` | int | no | `25` | Any class outside the four above | `BannerBearerConfig.cs:38` |
| `ExcludedRaces` | string[] | no | 5 compiled ids | Race ids that never carry a banner | `BannerBearerConfig.cs:43` |
| `CultureBanners` | map of string to string | no | 28 compiled rows | Culture StringId to banner `ItemObject` id; `""` means no banner | `BannerBearerConfig.cs:62` |
| `DefaultBannerItemId` | string | no | `""` | Fallback for an unmapped culture. Empty on purpose, so the fallback fails closed | `BannerBearerConfig.cs:115` |

Keys prefixed `_comment` are notes in the shipped file and are ignored by the deserializer.

<!-- engine-ref type="TAOM.Features.DreadAura.Domain.DreadAuraConfig" file="Main/Features/DreadAura/Domain/DreadAuraConfig.cs" lines="14-81" -->

| Attribute (`dread_aura_config.json`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `enabled` | bool | no | `true` | Master switch, overridden live by MCM | `DreadAuraConfig.cs:14` |
| `pulseIntervalSeconds` | float | no | `0.25` | Seconds between drain pulses; valid 0.1 to 5.0 | `DreadAuraConfig.cs:18` |
| `maxSourcesPerFrame` | int | no | `2` | Dread sources evaluated per frame; valid 1 to 16 | `DreadAuraConfig.cs:23` |
| `fearVoiceChancePerPulse` | float | no | `0.02` | Chance of a fear voice line per pulse | `DreadAuraConfig.cs:27` |
| `heroSets` | string[] | no | `["nazgul_nine"]` | Named lore groups that radiate dread | `DreadAuraConfig.cs:37` |
| `heroIds` | string[] | no | `["lord_1_17"]` | Individual hero StringIds | `DreadAuraConfig.cs:42` |
| `races` | string[] | no | `["sauron"]` | FaceGen race ids that radiate dread | `DreadAuraConfig.cs:46` |
| `profile` | object | no | radius 12, inner 4, rate 5, floor 0 | The drain shape | `DreadAuraConfig.cs:48`, `:64-81` |
| `raceResist` | map of string to float | no | elf 0.4, dwarf 0.5 | Per-race resistance; a value above 1 is dropped, not clamped | `DreadAuraConfig.cs:54` |

<!-- engine-ref type="TAOM.Features.UncapturableHeroes.Domain.UncapturableHeroesConfig" file="Main/Features/UncapturableHeroes/Domain/UncapturableHeroesConfig.cs" lines="14-48" -->

| Attribute (`uncapturable_heroes_config.json`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `enabled` | bool | no | `true` | Master switch, overridden by the MCM toggle | `UncapturableHeroesConfig.cs:14` |
| `heroSets` | string[] | no | `["nazgul_nine"]` | Include by named lore group; unknown names are skipped with a warning | `UncapturableHeroesConfig.cs:27` |
| `heroIds` | string[] | no | `["lord_1_17"]` | Include by individual hero StringId | `UncapturableHeroesConfig.cs:32` |
| `uncapturableRaces` | string[] | no | `["sauron"]` | The rule: any hero of a named FaceGen race | `UncapturableHeroesConfig.cs:39` |
| `excludeHeroIds` | string[] | no | empty | Evaluated first; beats the rule and both include lists | `UncapturableHeroesConfig.cs:43` |
| `announceEscape` | bool | no | `true` | Write the campaign message-feed line on an escape | `UncapturableHeroesConfig.cs:48` |

<!-- engine-ref type="TAOM.Features.Messengers.MessengerConfig" file="Main/Features/Messengers/MessengerConfig.cs" lines="5-6" -->

| Attribute (`messenger_config.json`) | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `accidentChancePerHour` | float | no | `0.002` | Per-hour ambush probability; valid 0.0 to 1.0 | `MessengerConfig.cs:5` |
| `travelSpeedMultiplier` | float | no | `1.0` | Multiplies per-hour travel speed; valid 0.1 to 10.0 | `MessengerConfig.cs:6` |

### Coverage: what a new kingdom id must gain

<!-- engine-ref type="TAOM.Tests.Features.ArmyTargeting.WarTheaterConfigInvariantsTests" file="TAOM.Tests/Features/ArmyTargeting/WarTheaterConfigInvariantsTests.cs" lines="62-145" -->

The culture checklist in [culture-playability-wiring.md](../features/culture-playability-wiring.md)
covers cultures only. This is the kingdom-side list, and only one row is enforced by a test that
fails on a missing id.

| File | Must the new kingdom id appear? | Enforced by |
|---|---|---|
| `configs/army_targeting.json` `KingdomTheaters` | Yes, even an empty list to mark it passive | `WarTheaterConfigInvariantsTests.EveryKingdom_HasATheaterDecisionRecorded` (`WarTheaterConfigInvariantsTests.cs:131-145`) |
| `execution/alignment.json` | Yes; absent resolves `Neutral` | Nothing. `ShippedAlignmentConfigTests.cs:125-151` pins 14 named culture rows only |
| `diplomacy/diplomacy.json` | Yes, one row per opponent; absent pairs resolve `Neutral` | Nothing reads the shipped file |
| `diplomacy/war_of_the_ring.json` | Only if it takes part in a scripted declaration | `WarOfTheRingShippedConfigTests.cs:51-105` pins days 30 and 44 and the two Phase 1 wars |
| `factionmap/factions.json` and `regions.json` | Only if it is player-selectable | `FactionMapDataTests.cs:28-141` |
| `banner_bearers/banner_bearers_config.json` `CultureBanners` | Keyed by **culture**, not kingdom | `ShippedBannerBearerConfigTests.cs:104-125` pins 20 cultures |
| `settlement_guards/settlement_guards_config.xml` | Optional | `ConfigIdValidationTests.cs:120-183` validates the ids that are present |

The kingdom set the first test reads is derived from data, not hardcoded: 14 `<Kingdom>` rows in
`Main/_Module/ModuleData/taom_spkingdoms.xml` plus 8 vanilla kingdoms retagged by
`Main/_Module/ModuleData/spkingdoms.xslt`, so **22** ids.
<!-- measured: python -c "import xml.etree.ElementTree as ET,re;k=set(x.get('id') for x in ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot().findall('Kingdom'));k|=set(re.findall(r\"Kingdom\[@id='([a-z_]+)'\]\",open('Main/_Module/ModuleData/spkingdoms.xslt').read()));print(len(k))" 2026-09-05 -->
Note that `kingdom-creation.md` spells the first file `TAOM_spkingdoms.xml`; the file on disk is all
lowercase, and NTFS hides the difference until something case-sensitive reads it.

## Child elements

<!-- engine-ref type="TAOM.Features.SettlementGuards.SettlementGuardConfigProvider" file="Main/Features/SettlementGuards/SettlementGuardConfigProvider.cs" lines="105-163" -->

| Child element (`settlement_guards_config.xml`) | Attributes | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Guards>` | none | yes inside a pool | pool is empty | Wraps the weighted guard rows | `SettlementGuardConfigProvider.cs:129` |
| `<Guard>` | `troop`, `weight`, `spawn_points` | `troop` yes | `weight` 1, `spawn_points` any | One weighted candidate; `spawn_points` is a comma-separated filter | `SettlementGuardConfigProvider.cs:135-155` |
| `<PrisonGuard>` | `troop` | no | culture default | Overrides the culture's prison guard | `SettlementGuardConfigProvider.cs:159` |
| `<Spears>` | none | no | no override | Wraps the per-culture spear overrides | `SettlementGuardConfigProvider.cs:105` |
| `<Spear>` | `culture`, `item` | both | row skipped | Spear item id for guards at a spear spawn point | `SettlementGuardConfigProvider.cs:110-111` |

`weight` is relative **within the matched pool only**, after the spawn-point filter has run: the
service sums the surviving candidates' weights and rolls once against that total
(`SettlementGuardService.cs:97-113`). It is not a chance out of 100, and a weight in one settlement
block says nothing about another. Shipped example: Minas Tirith's four rows are 3, 2, 3 and 2, so
the fountain guard draws 3 of every 10 picks at the spawn points it lists.

<!-- engine-ref type="TAOM.Features.EliteEmissary.Domain.EliteEmissaryConfig" file="Main/Features/EliteEmissary/Domain/EliteEmissaryConfig.cs" lines="10-16" -->

| Child element (`elite_emissary_config.xml`) | Attributes | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<KeySettlements>/<Settlement>` | `id` | yes | row skipped | Where the emissary appears; validated against live settlements at session launch | `EliteEmissaryConfig.cs:13` |
| `<CultureOffers>/<Culture>` | `id` | yes | list dropped and warned | Keyed by the settlement's **current owner** culture, so conquest flips the offerings | `EliteEmissaryConfig.cs:16` |
| `<CultureOffers>/<Culture>/<Troop>` | `id` | yes | row dropped and warned | An offered troop. A troop with no `merchant_cost` row is dropped at load | `EliteEmissaryConfigProvider.cs:145` |

`merchant_cost` is the one-time emissary purchase price in the owner faction's special resource, and
it lives on the `<Troop>` rows of `special_resources/troop_resource_costs.xml`, not here. It is a
fourth cost attribute alongside `upgrade_cost`, `recruit_cost` and `daily_upkeep`, and
[special-resources.md](../features/special-resources.md) omits it: 70 of the file's 77 `<Troop>`
rows carry one.
<!-- measured: python -c "import xml.etree.ElementTree as ET,collections;rows=ET.parse('Main/_Module/ModuleData/special_resources/troop_resource_costs.xml').getroot().findall('.//Troop');c=collections.Counter();[c.update(x.attrib) for x in rows];print(len(rows),dict(c))" 2026-09-05 -->
The full cost table is in [configs-balance](configs-balance.md); price bands are in
[elite-emissary.md:130-134](../features/elite-emissary.md).

<!-- engine-ref type="TAOM.Features.CultureMarketplace.Domain.RoutedItem" file="Main/Features/CultureMarketplace/Domain/RoutedItem.cs" lines="13-15" -->

| Child element (`culture_marketplace_config.xml`) | Attributes | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Culture>/<Blacklist>/<Item>` | `id` | yes | not excluded | Keeps an item out of that culture's pool | [culture-marketplace.md:68](../features/culture-marketplace.md) |
| `<Culture>/<Boost>/<Item>` | `id`, `weight` | `id` yes | weight 1.0 | Draw weight in 0 to 1000; a bad value reverts to 1.0 with a warning | [culture-marketplace.md:69-70](../features/culture-marketplace.md) |
| `<Routing>/<Item>` | `id`, `cultures`, `min_stock` | `id`, `cultures` | `min_stock` 0 | The item ignores its own `culture=` and appears only in the listed cultures' pools | `RoutedItem.cs:13-15` |

`min_stock` bypasses the per-town roster cap, which is the only reason the ten Erebor war-ram rows
exist: routing an already-Erebor item to Erebor changes no pool, it just buys the stock floor
(the file's own comment at `culture_marketplace_config.xml:44-50`).

<!-- engine-ref type="TAOM.Features.FactionMap.FactionConfigProvider" file="Main/Features/FactionMap/FactionConfigProvider.cs" lines="1-119" -->

Each `factions.json` entry uses 14 fields, all of them present on every shipped entry.
<!-- measured: python -c "import json;f=json.load(open('Main/_Module/ModuleData/factionmap/factions.json'));k=set();[k.update(v) for v in f.values()];print(len(f),sorted(k))" 2026-09-05 -->
`name`, `description`, `traits`, `bonuses[].text`, `perks[].name`, `perks[].description`,
`special_units[].name`, `special_units[].description`, `strengths` and `weaknesses` are player-facing
and must be wrapped `{=KEY}default`; `color`, `game_faction`, `side`, `image`, `playable` and
`difficulty` are raw. The field meanings are in
[faction-map.md:71-101](../features/faction-map.md). `regions.json` entries carry `faction`,
`norm_bbox` and `capital_pos`.

## Worked example

The highest-priority guard pool in the shipped file, Minas Tirith:

<!-- example file="Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml" id="town_EW1" -->
```xml
  <Settlement id="town_EW1">
    <Guards>
      <Guard troop="gondor_mt_fountain_guard" weight="3" spawn_points="sp_guard_castle,sp_guard_with_spear" />
      <Guard troop="gondor_mt_captain" weight="2" spawn_points="sp_guard_castle" />
      <Guard troop="gondor_mt_sergeant" weight="3" spawn_points="sp_guard,sp_guard_patrol" />
      <Guard troop="gondor_mt_veteran" weight="2" spawn_points="sp_guard,sp_guard_patrol" />
    </Guards>
  </Settlement>
```

1. **`troop`** is the first thing to change: it is an `NPCCharacter` id from a troops XML, and a
   wrong id gets you vanilla's own garrison pick with no error. Ids live in [troops](troops.md).
2. **`weight`** rebalances who you see without touching the roster. Only the ratio inside this one
   `<Guards>` block matters.
3. **`spawn_points`** decides where a troop can stand. `sp_guard_castle` is not prosperity-scaled,
   the other four are, so a captain listed only at `sp_guard_castle` appears in a poor town and a
   sergeant on `sp_guard` may not.

The first eight lines of the alignment map, which is what a new faction's side edit looks like:

<!-- excerpt file="Main/_Module/ModuleData/execution/alignment.json" -->
```json
{
  "empire_w": "free",
  "gondor": "free",
  "mordor": "evil",
  "empire": "evil",
  "vlandia": "free",
  "erebor": "free",
  "sturgia": "free",
```

`empire_w` is Gondor's kingdom and `gondor` its culture; `empire` is Dunland and `vlandia` is Rohan.
Writing `rohan` or `dunland` produces a key nothing ever looks up, and the faction quietly resolves
`Neutral`.

## Recipes: Add / Modify / Delete

Each of these is data only unless it introduces a key the provider does not already have. A new key
always needs a matching property on the config class, which is a code change.

### Add

**A guard to a settlement's pool.**

1. Open `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml`.
2. Find the `<Settlement id="...">` block, or add one at the level of its siblings if the settlement
   has none. Settlement ids come from `TAOM_Map/ModuleData/settlements.xml`.
3. Add `<Guard troop="..." weight="N" spawn_points="..." />` inside its `<Guards>`. Leave
   `spawn_points` off to let the troop appear at any of the five spawn types.
4. Confirm the troop id resolves and its race is not `cave_troll`, which is scrubbed silently
   (`Main/Features/SettlementGuards/SettlementGuardService.cs:15-16`).

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

**A priority target for a kingdom.**

1. Open `Main/_Module/ModuleData/configs/army_targeting.json`.
2. Under `FactionPriorityTargets`, add the settlement id to that kingdom's array. Position is the
   priority: earlier scores higher, and the boost decays across the whole list, so inserting near
   the top flattens the decay for everything below it.
3. If the kingdom has no array yet, add one keyed by its kingdom StringId, not its lore name.

Check: `dotnet test TAOM.Tests --filter WarTheaterConfigInvariants -p:DisableModuleCopy=true -p:ModuleId=`
Takes effect: full game restart
Code: No code changes needed

**An alignment key for a new faction.**

1. Open `Main/_Module/ModuleData/execution/alignment.json`.
2. Add `"<id>": "free"`, `"evil"` or `"neutral"`. Add the kingdom StringId; add the culture
   StringId as well only when the two differ, as they do for Gondor and Mordor.
3. Work through the coverage table above: `army_targeting.json` is the one that fails a test if you
   forget it, `diplomacy.json` is the one nothing checks.

Check: `python -m json.tool Main/_Module/ModuleData/execution/alignment.json`
Takes effect: full game restart
Code: No code changes needed

### Modify

**A number in any of the thirteen files.**

1. Edit the value in place. Every provider validates per field, reverts an out-of-range value to the
   compiled default, and logs both a per-field warning and one summary warning
   ([banner-bearers.md:133](../features/banner-bearers.md)); the compiled defaults are the ones in
   the Attributes tables above, not whatever is in the file.
2. Check the feature's MCM group before you retune. Where a knob exists in both places the MCM value
   usually wins at runtime and is stored per player under
   `Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/TAOM/`, so a JSON change
   reaches fresh installs only ([war-of-the-ring.md:132-134](../features/war-of-the-ring.md)).
3. Relaunch the executable. Nothing here reloads on save load.

Check: `python tools/lint_docs.py --fail-on-drift`
Takes effect: full game restart
Code: No code changes needed

**A faction's entry on the selection screen.**

1. Edit `Main/_Module/ModuleData/factionmap/factions.json`, keeping the `{=KEY}` prefix on every
   player-facing string.
2. Run `python tools/harvest_factionmap_strings.py` to refresh the harvested block in
   `taom_module_strings.xml`. It is idempotent.
3. Translate: `python tools/translate_with_claude.py --lang <LANG> --module TAOM --apply` per
   language. See [strings-and-localization](strings-and-localization.md).

Check: `dotnet test TAOM.Tests --filter FactionMap -p:DisableModuleCopy=true -p:ModuleId=`
Takes effect: full game restart
Code: No code changes needed

### Delete

**A row, a key or a block.**

1. Remove the element or key. Understand the fallback first, because every one of these files fails
   quiet: a missing `alignment.json` key resolves `Neutral`
   (`Main/Features/Execution/AlignmentService.cs:36-41`); a missing `diplomacy.json` pair resolves
   `AllianceTier.Neutral` (`Main/Features/Diplomacy/DiplomacyService.cs:46`); a deleted
   `<Settlement>` guard block drops that town to its clan block, then its culture block, then to
   vanilla's own garrison pick (`Main/Features/SettlementGuards/SettlementGuardService.cs:46-52`);
   an unmapped culture in `CultureBanners` gets no banner, because `DefaultBannerItemId` ships empty
   on purpose.
2. Deleting from `KingdomTheaters` is the one case that breaks a test rather than going quiet. Set
   the value to an empty array instead to mark the kingdom deliberately passive.
3. Deleting a `FactionPriorityTargets` entry re-steepens the decay for every survivor in that list,
   so re-read the remaining order rather than assuming it is unchanged.

Check: `dotnet test TAOM.Tests --filter WarTheaterConfigInvariants -p:DisableModuleCopy=true -p:ModuleId=`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **Nothing here reloads without relaunching the game.** All 12 providers are `Reuse.Singleton` in a
  container built at module load, so a save reload and a new campaign both read the cached copy.
  This is the single most common wasted hour on these files (`Main/Features/Execution/ExecutionIoC.cs:10`).
- **`alignment.json` is not a kingdom-only map.** One flat dictionary answers `GetKingdomSide` and
  `GetCultureSide`, which is why it needs `gondor` and `mordor` on top of the 22 kingdom ids
  (`Main/Features/Execution/AlignmentService.cs:32-34`).
- **A `Neutral` faction is treated as everyone's enemy, not as nobody's.** `AreEnemyAlignments`
  returns true whenever either side is `Neutral`, and `AreSameAlignment` returns false, so a typo
  that drops an id out of the map is not a no-op (`Main/Features/Execution/AlignmentService.cs:44-53`).
- **Lore names are dead keys.** `empire` is Dunland, `vlandia` is Rohan, `empire_w` is Gondor,
  `empire_s` is Mordor, `sturgia` is Dale, `aserai` is Harad, `khuzait` is Rhun, `battania` is Khand
  (`Main/_Module/ModuleData/spkingdoms.xslt:15-234`). The tables in
  `alignment-aware-execution.md:228-248` and `execution.md:74-94` have `empire` and `vlandia`
  swapped; do not copy them.
- **`diplomacy.md`'s tier counts are stale.** It says 5 permanent, 11 natural, 10 permanent, 33
  hostile; the shipped file holds 130 rows, 38 `Permanent`, 24 `Natural`, 7 `Neutral`, 61 `Hostile`
  ([diplomacy.md:92](../features/diplomacy.md)).
- **A `cave_troll`-race troop named in a guard pool never appears.** The exclusion is a hardcoded
  invariant, not config, and the rejection is a one-line warning
  (`Main/Features/SettlementGuards/SettlementGuardService.cs:12-16`). Banner bearers exclude five
  races the same way, from the config side this time: `cave_troll`, `hill_troll`, `nazghul`,
  `saruman`, `sauron`. Picking one of those races for a new troop costs it both roles with no error
  ([settlement-guards.md:73-99](../features/settlement-guards.md)).
- **`spawn_points` that matches nothing falls back to the whole pool.** A typo in a spawn tag does
  not empty the pool, it widens it, so a castle-only captain starts walking patrol routes
  (`Main/Features/SettlementGuards/SettlementGuardService.cs:54-56`).
- **The guard fallback chain stops at the first match, not the first useful match.** A
  `<Settlement>` block wins over the culture block even when its rows are all filtered out
  (`Main/Features/SettlementGuards/SettlementGuardService.cs:46-52`).
- **An emissary troop with no `merchant_cost` row is dropped at load, not priced at zero**
  (`Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml:5-6`).
- **`culture_marketplace_config.xml` ships with no `<Culture>` blocks.** The blacklist and boost
  sections documented in [culture-marketplace.md:60-82](../features/culture-marketplace.md) are
  supported but unused, so there is no shipped example to copy from; the header comment inside the
  file is the reference.
- **`regions.json` carries one key that is not a faction**, `map_boundary`, so a script that treats
  the two files as parallel maps will trip on it (measured, command below).
- **Dread aura race names and hero ids are not validated at load**, because the FaceGen registry is
  not populated yet; a bad name is skipped on first resolve
  ([dread-aura.md:230-233](../features/dread-aura.md)).
- **`Patch28_SettlementGuards` registers two manual patches with no category attribute**, so
  grepping `HarmonyPatchCategory` will not show them
  ([harmony-patch-registry.md:5](../reference/harmony-patch-registry.md)).
- **Line numbers quoted from `docs/reference/engine/` were captured against an older engine dump.**
  Re-read one against the dump this handbook cites before trusting it.

## Numbers in this chapter

All measured 2026-09-05 from the repo working tree at `Main/_Module/ModuleData/`.

| Number | Command |
|---|---|
| `alignment.json`: 24 keys, 11 evil, 9 free, 4 neutral | `python -c "import json,collections;d=json.load(open('Main/_Module/ModuleData/execution/alignment.json'));print(len(d),collections.Counter(d.values()))"` |
| 22 live kingdom ids (14 declared, 8 retagged by XSLT); the only alignment keys that are not kingdom ids are `gondor` and `mordor` | `python -c "import xml.etree.ElementTree as ET,re,json;k=set(x.get('id') for x in ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot().findall('Kingdom'));k\|=set(re.findall(r\"Kingdom\[@id='([a-z_]+)'\]\",open('Main/_Module/ModuleData/spkingdoms.xslt').read()));a=json.load(open('Main/_Module/ModuleData/execution/alignment.json'));print(len(k),sorted(set(a)-k))"` |
| `diplomacy.json`: 130 rows, 38 Permanent, 24 Natural, 7 Neutral, 61 Hostile | `python -c "import json,collections;print(collections.Counter(r['tier'] for r in json.load(open('Main/_Module/ModuleData/diplomacy/diplomacy.json'))['relationships']))"` |
| `army_targeting.json`: 4 theaters, 22 `KingdomTheaters` keys (2 empty), 9 `FactionPriorityTargets`, 5 `FactionAggressionMultipliers` | `python -c "import json;a=json.load(open('Main/_Module/ModuleData/configs/army_targeting.json'));print(len(a['Theaters']),len(a['KingdomTheaters']),len(a['FactionPriorityTargets']),len(a['FactionAggressionMultipliers']),[k for k,v in a['KingdomTheaters'].items() if not v])"` |
| `settlement_guards_config.xml`: 16 `<Settlement>`, 0 `<Clan>`, 1 `<Culture>`, 38 `<Guard>` (37 with `spawn_points`), 16 `<Spear>` | `python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml').getroot();g=r.findall('.//Guard');print(len(r.findall('Settlement')),len(r.findall('Clan')),len(r.findall('Culture')),len(g),sum(1 for x in g if 'spawn_points' in x.attrib),len(r.findall('.//Spear')))"` |
| `elite_emissary_config.xml`: 11 key settlements, 11 culture offer lists, 70 `<Troop>` rows | `python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml').getroot();print(len(r.findall('.//KeySettlements/Settlement')),len(r.findall('.//CultureOffers/Culture')),len(r.findall('.//Troop')))"` |
| `troop_resource_costs.xml`: 77 `<Troop>` rows, 70 with `merchant_cost` | `python -c "import xml.etree.ElementTree as ET,collections;rows=ET.parse('Main/_Module/ModuleData/special_resources/troop_resource_costs.xml').getroot().findall('.//Troop');c=collections.Counter();[c.update(x.attrib) for x in rows];print(len(rows),dict(c))"` |
| `culture_marketplace_config.xml`: 0 `<Culture>` blocks, 1 `<Routing>`, 14 `<Item>` rows | `python -c "import xml.etree.ElementTree as ET,collections;r=ET.parse('Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml').getroot();print(len(r.findall('Culture')),len(r.findall('Routing')),collections.Counter(e.tag for e in r.iter())['Item'])"` |
| `factions.json`: 45 factions, 20 playable, 17 evil, 15 free, 13 neutral, 14 fields per entry; `regions.json` 46 entries, the extra key being `map_boundary` | `python -c "import json,collections;f=json.load(open('Main/_Module/ModuleData/factionmap/factions.json'));r=json.load(open('Main/_Module/ModuleData/factionmap/regions.json'));k=set();[k.update(v) for v in f.values()];print(len(f),sum(1 for v in f.values() if v['playable']),collections.Counter(v['side'] for v in f.values()),len(k),len(r),sorted(set(r)-set(f)))"` |
| `banner_bearers_config.json`: 28 `CultureBanners`, 5 `ExcludedRaces`, `DefaultBannerItemId` empty | `python -c "import json;b=json.load(open('Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json'));print(len(b['CultureBanners']),b['ExcludedRaces'],repr(b['DefaultBannerItemId']))"` |
| `dread_aura_config.json`: 1 hero set, 1 hero id, 1 race, 2 `raceResist` rows | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/dread_aura/dread_aura_config.json'));print(len(d['heroSets']),len(d['heroIds']),len(d['races']),len(d['raceResist']))"` |
| `uncapturable_heroes_config.json`: 13 keys, 7 of them `_comment_*`, 6 real | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/uncapturable_heroes/uncapturable_heroes_config.json'));print(len(d),sum(1 for k in d if k.startswith('_comment')))"` |
| 12 of 12 config providers registered `Reuse.Singleton` | `rg -n "Register<I(ArmyTargeting\|Alignment\|BannerBearer\|DreadAura\|Messenger\|CultureMarketplace\|UncapturableHeroes\|Diplomacy\|WarOfTheRing\|SettlementGuard\|EliteEmissary\|Faction)ConfigProvider, [A-Za-z]+>\(Reuse.Singleton\)" Main/ \| wc -l` |

## Read next

- Sides and war: [execution.md](../features/execution.md),
  [alignment-aware-execution.md](../features/alignment-aware-execution.md),
  [diplomacy.md](../features/diplomacy.md), [war-of-the-ring.md](../features/war-of-the-ring.md),
  [army-targeting.md](../features/army-targeting.md)
- Settlements and markets: [settlement-guards.md](../features/settlement-guards.md),
  [elite-emissary.md](../features/elite-emissary.md),
  [special-resources.md](../features/special-resources.md),
  [culture-marketplace.md](../features/culture-marketplace.md)
- Presentation and battlefield: [faction-map.md](../features/faction-map.md),
  [banner-bearers.md](../features/banner-bearers.md),
  [uncapturable-heroes.md](../features/uncapturable-heroes.md),
  [dread-aura.md](../features/dread-aura.md), [messengers.md](../features/messengers.md)
- Wiring a new faction: [culture-playability-wiring.md](../features/culture-playability-wiring.md),
  [harmony-patch-registry.md](../reference/harmony-patch-registry.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](./configs-balance.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
