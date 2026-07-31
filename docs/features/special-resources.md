# Special Resources

**Status:** Verified in-game (2026-04-14). Gondor Caster resource showing on map bar with rich tooltip (amount, tier, daily breakdown, per-event rates). Icon sprite loading correctly.

## Overview

Per-kingdom special resource system where all 18 TAOM kingdoms have a unique secondary currency required to recruit and maintain elite troops. 11 unique resources mapped to faction groups — shared balance within each group. Resources are earned through combat, displayed in the map bar, enforced in the party screen with pending transaction support, and trigger troop desertion when depleted.

## Why This Exists

- **Vanilla behavior:** All troop upgrades cost only gold and XP — no faction flavor
- **TAOM requirement:** Elite troops should feel expensive and faction-specific
- **Without this feature:** Elite armies are trivially affordable, reducing faction identity and strategic tension

## Resources

| Resource | Kingdoms | Theme |
|----------|----------|-------|
| War Spoils | Mordor, Isengard, Gundabad, Dol Guldur | Orc plunder from battles |
| Gems | Erebor | Dwarven mining wealth |
| Caster | Gondor | Silver coin currency |
| Marks | Rohan | Horse-lord currency |
| Elven Wine | Rivendell, Lothlorien, Mirkwood | Elven trade goods |
| Lake Fish | Dale | Laketown trade |
| War Drums | Harad, Shaghana, Abanissa | Tribal war currency |
| Tribal Relics | Khand | Sacred artifacts |
| Dunlending Ale | Dunland | Clan tribute |
| Plunder | Umbar | Corsair loot |
| War Banners | Rhun | Easterling standards |

Factions sharing a resource share the same balance (e.g., switching from Mordor to Isengard keeps your War Spoils).

## Architecture

### Design Challenge

Bannerlord has no concept of per-faction resources beyond gold and influence. The party screen upgrade flow is hardcoded to check only gold costs.

### Solution

- **XML-driven config:** Resource definitions with nested `<Kingdom>` and `<Culture>` child elements for many-to-one mappings
- **Culture fallback:** Resolves via kingdom first, then culture — supports kingdomless players
- **CampaignBehavior:** Hooks 8 events (DailyTick, MapEventEnded, RaidCompleted, PrisonerTaken, TournamentFinished, HideoutCompleted, NewGameCreated, SessionLaunched)
- **Harmony Patch26:** 3 patches — InitializeUpgrades (grey out + hint), AddCommand prefix (clamp count), UpgradeTroop postfix (queue spend)
- **Pending transaction:** Upgrades queue during party screen, commit on close, revert on cancel
- **Desertion:** At 0 balance, 10% of each upkeep-troop type deserts daily (min 1 per type)
- **Notifications:** Green chat for earnings, yellow deficit warning (only when the next daily tick's projected balance would fall to ≤ 0 — i.e. one day before desertion; a low-but-stable balance is silent), center-screen desertion alert
- **SyncData persistence:** Composite `heroId:resourceId` keys, cap enforcement on load
- **Career passive integration:** `SpecialResourceGain` scales daily earning, `SpecialResourceUpkeepModifier` reduces upkeep, `SpecialResourceUpgradeCostModifier` reduces upgrade cost — all wired through `ICareerPassiveService`
- **Resource tiers:** Optional `<Tiers>` XML element defines threshold-based progression (pilot: Gems with 3 tiers). `GetCurrentTier()` resolves highest tier where balance >= threshold. Map bar shows tier name when active.
- **Map bar display:** Uses `[DataSourceProperty]` bindings on mixin + `PrefabExtensionInsertPatch` — does NOT add to `SecondaryInfoItems` (causes vanilla IndexOutOfRange crash). See [gui-sprite-system.md](gui-sprite-system.md).
- **Comprehensive logging:** `[SpecRes]` prefix throughout all components

### Component Diagram

```
special_resources_config.xml + troop_resource_costs.xml
        |
  SpecialResourceConfigProvider (loads + caches XML, multi-key indexes)
        |
  SpecialResourceService (resolve, earn, spend, validate, daily tick, desertion)
       / \         \          \
      /   \         \          \
Behavior  Patch26    MapBarMixin  SpriteWidget  ICareerPassiveService
(events)  (party UI) (map bar)   (dynamic icon) (career modifier)
                                                      |
                                              ResourceTier (domain)
                                              GetCurrentTier (service)
```

## Configuration

### Resource Definitions: `Main/_Module/ModuleData/special_resources/special_resources_config.xml`

```xml
<Resource id="war_spoils" display_name="War Spoils" icon_sprite="taom_war_spoils_icon"
  cap="10000" starting_amount="0" daily_per_town="0.2"
  per_battle_victory_base="14" per_raid="12" per_siege_victory="20"
  per_prisoner="2" per_tournament_win="3" per_hideout_clear="8">
  <Kingdom id="empire_s" />
  <Kingdom id="isengard" />
  <Culture id="mordor" />
  <Culture id="isengard" />
</Resource>
```

Earning rates are now differentiated per faction identity:
- **Aggressive factions** (Mordor, Harad): high battle/raid, low daily
- **Mining/trade factions** (Erebor, Dale): high daily, low battle
- **Honor factions** (Rohan): high tournament, zero raid
- **Peaceful factions** (Elves): high daily, zero raid

### Resource Tiers (Optional): `<Tiers>` element inside `<Resource>`

```xml
<Resource id="gems" ...>
  <Tiers>
    <Tier level="1" name="Apprentice Miner" threshold="100"
          description="Dwarven mining efficiency improves." />
    <Tier level="2" name="Journeyman Smith" threshold="250"
          description="Erebor's forges burn bright." />
    <Tier level="3" name="Master of the Treasury" threshold="400"
          description="The wealth of Erebor flows." />
  </Tiers>
</Resource>
```

Tiers are sorted by threshold at parse time. `GetCurrentTier()` reverse-walks to find the highest met threshold. Resources without `<Tiers>` have an empty list (backward compatible).

Multiple `<Kingdom>` and `<Culture>` child elements map to the same resource (many-to-one).

### Troop Costs: `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`

```xml
<!-- Upgrade target: charged on the party-screen upgrade (Patch26) -->
<Troop id="mordor_uruk_darkblade" resource_id="war_spoils" upgrade_cost="2" daily_upkeep="0.1" />
<!-- Recruitable volunteer: charged at recruitment (Patch51), not upgrade -->
<Troop id="harad_elephant_rider" resource_id="war_drums" recruit_cost="50" daily_upkeep="10" />
<Troop id="taom_spider_creature" resource_id="war_spoils" recruit_cost="5" daily_upkeep="1" />
```

Three cost fields, any combination allowed per troop:

| Field | When charged | Path |
|-------|-------------|------|
| `upgrade_cost` | Party-screen upgrade into this troop | Patch26 (`PartyScreenLogic.UpgradeTroop`) |
| `recruit_cost` | Recruited as a volunteer (one-time) | Patch51 gate + `OnUnitRecruitedEvent` charge |
| `daily_upkeep` | Every daily tick the troop is in the party | `OnDailyTickHero` → `GetDailyUpkeep` |

`recruit_cost` exists because the elephant/spider are **volunteer recruits, not upgrade targets** — nothing
upgrades into them, so `upgrade_cost` would never fire. It is kept distinct from `upgrade_cost` so a troop
that is both can't be double-charged. The **charged resource is always the player's resolved resource**
(`ResolveResource(kingdom, culture)`); the `resource_id` attribute is documentation only. Fully data-driven:
giving any troop a `recruit_cost` gates + charges it with no code change.

**Recruit gate (Patch51_RecruitmentResourceGate):** a postfix on the private `RecruitmentVM.RefreshPartyProperties`
disables the Done button (with a `{=taom_recruit_needs_resource}` "Requires N <Resource>" hint) when the cart
holds an unaffordable troop — mirroring vanilla's gold gate, only ever forcing the flag false. The matching
deduction is on `OnUnitRecruitedEvent` (player-only; the AI/generic recruit path fires `OnTroopRecruited`
instead, so AI lords are never charged).

### Current Values (all resources)

- Cap: 10000, Starting: 0
- Daily per town: +0.5, Battle: +10 (x enemy ratio 0.5-2x), Raid: +8, Siege: +15, Prisoner: +1, Tournament: +5, Hideout: +6
- 12 Mordor elite troops costed (other factions pending)

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SpecialResources/ISpecialResourceService.cs` | Service interface + TroopUpkeepInfo + TroopDesertionEntry |
| `Main/Features/SpecialResources/SpecialResourceService.cs` | Core logic (resolve, earn, spend, desertion, session) |
| `Main/Features/SpecialResources/ISpecialResourceStorageService.cs` | Storage interface |
| `Main/Features/SpecialResources/SpecialResourceStorageService.cs` | Composite-key dict persistence |
| `Main/Features/SpecialResources/ISpecialResourceConfigProvider.cs` | Config interface (GetByKingdomId, GetByCultureId) |
| `Main/Features/SpecialResources/SpecialResourceConfigProvider.cs` | XML loader with multi-key indexing |
| `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` | CampaignBehavior (8 events, desertion, notifications) |
| `Main/Features/SpecialResources/SpecialResourcesIoC.cs` | DryIoc registrations |
| `Main/Features/SpecialResources/Domain/SpecialResource.cs` | Resource definition (KingdomIds/CultureIds lists) |
| `Main/Features/SpecialResources/Domain/TroopResourceCostEntry.cs` | Per-troop cost record |
| `Main/Features/SpecialResources/Hooks/PartyCharacterVM_InitializeUpgrades_Patch.cs` | Grey out upgrades, show cost hint |
| `Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs` | Prefix: clamp count before execution |
| `Main/Features/SpecialResources/Hooks/PartyScreenLogic_UpgradeTroop_Patch.cs` | Postfix: queue resource spend |
| `Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs` | Upgrade hook interface |
| `Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs` | Upgrade hook implementation |
| `Main/Features/SpecialResources/Hooks/IOnRecruitmentResourceGate.cs` | Recruit gate hook interface |
| `Main/Features/SpecialResources/Hooks/RecruitmentResourceGateHook.cs` | Recruit gate hook implementation |
| `Main/Features/SpecialResources/Hooks/RecruitmentVM_RecruitGate_Patch.cs` | Patch51: block Done button when recruit cost unaffordable |
| `Main/Features/SpecialResources/Cheats/SpecialResourceCheats.cs` | `taom.add_special_resources` console command |
| `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` | Map bar UIExtenderEx mixin |
| `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs` | Dynamic icon sprite (extends IconBrushWidget) |
| `Main/Features/SpecialResources/UI/SpecialResourcePrefab.cs` | PrefabExtension: swap widget in BottomInfoBar |
| `Main/_Module/ModuleData/special_resources/special_resources_config.xml` | 11 resource definitions |
| `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | Mordor troop costs |

## Dependencies

- `IPathService` (Core) — module data path resolution
- `IModLogger` (Core) — logging (`[SpecRes]` prefix)
- UIExtenderEx — map bar mixin + prefab extension
- Harmony 2.x — Patch26_SpecialResources (3 patches)

## Tests

- `SpecialResourceServiceTests.cs` — 60 tests (resolve, earn, spend, validate, daily tick, projected-net deficit warning, pending transaction, desertion, edge cases)
- `SpecialResourceStorageServiceTests.cs` — 11 tests (get/set/add, clamp, multi-hero, multi-resource, restore-null)
- `SpecialResourceServiceGrantTests.cs` — 9 tests for `GrantAmount` (cap clamp, floor at 0, already-at-cap, unresolved kingdom/culture, NaN/Infinity rejection, grant during an open party-screen session) against a real storage instance
- `SpecialResourceCheatsFormatTests.cs` — 6 tests for the console echo, including a legacy balance above a lowered cap
- `TAOM.Tests/Features/DevConsole/ConsoleCommandBindingTests.cs` — 5 tests pinning the engine reflection contract for every attributed TAOM console command (assembly-wide; see [dev-console.md](dev-console.md))

## Cheat Command

`taom.add_special_resources [amount]` — the Special Resources counterpart to vanilla's
`campaign.add_gold_to_hero`. Requires cheat mode (`cheat_mode = 1` in
`Documents/Mount and Blade II Bannerlord/Configs/engine_config.txt`); the in-game console opens with
<kbd>Alt</kbd>+<kbd>~</kbd>.

| Input | Effect |
|-------|--------|
| `taom.add_special_resources` | +1000 to whichever resource your kingdom/culture resolves to |
| `taom.add_special_resources 500` | +500 |
| `taom.add_special_resources -300` | −300, floored at 0 (drive it to 0 to exercise the desertion path) |
| `taom.add_special_resources help` | Usage text |

The grant targets the *resolved* resource only — there is no resource-id argument, because
`ResolveResource(kingdom, culture)` is the single thing the player's UI, upgrade gate, and recruit
gate all read. It clamps to that resource's `cap` exactly like every legitimate earn path
(`AddCapped`), and the console echoes the real before→after so a clamp is never silent.

`SpecialResourceCheats.AddSpecialResources` is a thin entry point: it validates the console text and
delegates to `ISpecialResourceService.GrantAmount`. The console echo is built by `FormatResult`, kept
`internal` so its branches are testable without a running campaign. The cheat gate, the help branch
and the exception guard live in `TaomConsole.RunInCampaign` with every other `taom.*` command, and
`NaN` / `Infinity` rejection (which `float.TryParse` otherwise accepts) is handled by
`DevConsoleArgs.TryParseAmount`.

**Adding another TAOM console command:** read [dev-console.md](dev-console.md) first. It owns the
engine contract — the unguarded `Delegate.CreateDelegate` in the discovery loop, the silent
duplicate-name drop, the naming convention, the risk tiers, and the unresolved discovery-timing
question. Do not duplicate any of that here.

## How to Add a New Kingdom's Resource

1. Add a `<Resource>` element with `<Kingdom>` and `<Culture>` children to `special_resources_config.xml`
2. Or add `<Kingdom>`/`<Culture>` children to an existing resource for shared balance
3. Add `<Troop>` rows to `troop_resource_costs.xml` for T6+ troops
4. Add a 33x33 PNG icon to `Main/_Module/GUI/SpriteParts/ui_taom/MapBar/`
5. No C# changes needed — fully data-driven

## How to Tune Earning Rates

Edit attributes on the `<Resource>` element. Each resource can have independent rates. Current values are identical across all 11 resources.

## Desertion Mechanics

- Triggers daily when resource balance is 0 and party has upkeep-costing troops
- 10% of each troop type deserts per day (minimum 1 per type)
- Center-screen notification: "X elite troops deserted — your [Resource] are depleted!"
- Uses vanilla `TroopRoster.AddToCounts(character, -count)` for roster removal

## Performance

- IoC.Resolve cached in MapBarMixin constructor (not per-refresh)
- Config provider lazy-loaded with dictionary indexes
- No LINQ in hot paths — direct enumeration loops
- SpriteWidget caches resolved sprite (loads once, not per-frame)
- String formatting only when amount changes (cached `_lastAmount`)
- Comprehensive logging uses `LogDebug` for high-frequency paths, `LogInfo` for events

## Changelog

- 2026-07-30 — Added the `taom.add_special_resources` console cheat (TAOM's first console command) plus `ISpecialResourceService.GrantAmount`, the only arbitrary-amount grant path in the feature.
- 2026-06-19 — Gate the war elephant + spider behind recruit cost + daily upkeep via a new `recruit_cost` XML field and `Patch51_RecruitmentResourceGate` (block Done button) + `OnUnitRecruitedEvent` charge.
- 2026-06-01 — Deficit warning now fires only when the next tick's projected net would push the balance to ≤ 0 (`GetProjectedDailyNet` shared with the real tick math), replacing the low-but-stable `< Cap*0.1` warning.
- 2026-05-14 — R1-reset of resource state, added desertion grace, and per-resource seeding (closes deferred #133).
- 2026-05-13 — Fixed SyncData per-resource cap clamp + screen-event leak + NaN ParseFloat (#133), and made `QueueUpgradeSpend` debit the career-discounted effective cost with regression tests (#174, #194).
- 2026-05-04 — Deduped the hot-path `ResolveResource` DEBUG log spam by `(kingdom, culture)` key.
- 2026-04-14 — Corrected the Gondor resource display name from "Caster" to "Castar".
- 2026-04-08 — Initial Per-Kingdom Special Resource System (#73): 11 resources across 18 kingdoms, earning/spending/desertion/map-bar/SyncData; plus Codex adversarial review fixes (#72) including the `mordor`→`empire_s` ship-blocker and the transactional upgrade-spend pattern.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/elite-emissary.md](./elite-emissary.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
