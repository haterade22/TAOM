# Special Resources

**Status:** Shipped. The 2026-04 core was verified in-game (Gondor Castar on the map bar with tooltip and icon). The 2026-09-11 outflow work (#558: one daily breakdown behind every surface, four outflow toasts, per-troop tooltip rows, the zero-upkeep desertion fix, the Black Numenorean rescale, and the three Codex fixes of review 95) is unit-tested and reviewed but still awaits its in-game smoke; the 20 new strings are English in all 12 languages until the translator runs. #563 tracks the ungated prisoner recruit path.

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
| Castar | Gondor | Numenorean silver coin (the resource id stays `caster`) |
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
- **CampaignBehavior:** Hooks 12 events (SessionLaunched, DailyTickHero, MapEventEnded, RaidCompleted, PrisonerTaken, NewGameCreated, CharacterCreationIsOver, GameLoaded, TournamentFinished, HideoutBattleCompleted, UnitRecruited, GameOver) plus `ScreenManager.OnPushScreen` for the party-screen session
- **Earn policy:** `SpecialResourceEarnPolicy` — participation (not command) decides a battle payout, and a dedicated server credits nobody. See [Earning Rules](#earning-rules)
- **Harmony Patch26:** 3 patches — InitializeUpgrades (grey out + hint), AddCommand prefix (clamp count), UpgradeTroop postfix (queue spend)
- **Pending transaction:** Upgrades queue during party screen, commit on close, revert on cancel
- **Desertion:** At 0 balance, 10% of each upkeep-troop type deserts daily (min 1 per type). "Upkeep troop" means a troop whose cost row carries a `daily_upkeep` greater than zero; a row with only a `merchant_cost` (the Elite Emissary's 50 offers, many of them ordinary tree troops such as the Erebor Royal Warden) is not one and never deserts (#558)
- **One daily breakdown:** `ISpecialResourceService.GetDailyBreakdown` returns earning (career gain applied), upkeep (career upkeep modifier applied), net and one `TroopUpkeepLine` per upkeep troop type. The daily tick applies its net; the tooltip, the daily message and the console dump render the same object, so none of them can drift from the deduction. `PartyUpkeepReader` reads the party roster and town count off the engine objects for all three callers (#558)
- **Notifications:** Green chat for earnings. Every outflow speaks too (#558), built by the pure `SpecialResourceMessages` helper with every number in a slot: a daily line (`War Spoils: +0.4 income, -3.8 upkeep (12 left)`, yellow when the net is negative) on any day the party holds upkeep troops; a red overdraft line when the bill exceeded the balance and the floor at zero absorbed the rest; a yellow spend line when a party-screen session commits upgrades; a yellow charge line on a recruit with a `recruit_cost` (one line per unit, because the engine raises `OnUnitRecruitedEvent` once per recruited prisoner or volunteer). The spend and charge lines report what actually left the wallet, measured before and after the write: the storage floors at zero and the party screen's prisoner recruit is not gated by `recruit_cost` (#563), so the nominal cost can exceed the balance. The existing yellow one-day-ahead deficit warning (now, like the map-bar flag and desertion itself, only when the party holds upkeep troops) (fires only when the next tick would push the balance to zero or below, which is the desertion threshold) and the centre-screen desertion alert are unchanged
- **SyncData persistence:** Composite `heroId:resourceId` keys. The load path repairs a non-finite entry to zero (#558) and does NOT clamp to the cap: a per-player cap applied to every key once shrank other resources' balances (#133), so `ClampAll` is no longer called. The load reads into a null local, not the live dictionary: the engine leaves the ref unchanged when the key is missing, so a behavior record without the balances key loads empty storage instead of the previous campaign's balances (defensive: every TAOM build has written the key). **Open gap:** a save older than SpecialResources (before 2026-04-07) has no record for this behavior, the engine's `LoadBehaviorData` never calls its SyncData, and loaded after another campaign in the same process it still inherits that campaign's balances; the `_syncedThisSession` reset at the top of `OnGameLoaded` (the FiefGranting and FieldCamp shape) awaits Mike's call. The storage is a process-lifetime singleton that a new game never loads into, so `OnNewGameCreated` wipes it (`RestoreData(null)`) along with the service's session state, before the character-creation finalize seeds the new hero (plan 001)
- **Career passive integration:** `SpecialResourceGain` scales daily earning, `SpecialResourceUpkeepModifier` reduces upkeep, `SpecialResourceUpgradeCostModifier` reduces upgrade cost — all wired through `ICareerPassiveService`
- **Resource tiers:** Optional `<Tiers>` XML element defines threshold-based progression (pilot: Gems with 3 tiers). `GetCurrentTier()` resolves highest tier where balance >= threshold. Map bar shows tier name when active.
- **Map bar display:** `SpecialResourceMapBarMixin` adds one `MapInfoItemVM` to `SecondaryInfoItems` (dormant ownership hazard, see `.claude/rules/gui-ui.md`; the old IndexOutOfRange claim did not reproduce on v1.4.8). The tooltip renders the daily breakdown: title with balance and cap, tier or next tier, `Daily change` rundown with income (town count), elite upkeep (troop-type count) and one row per troop type in the extended (Alt) view, net, `Depleted in N days` while the balance shrinks (no countdown when the net is below the balance's float resolution, since the stored value would never move), or, at zero with a net at or below zero, a notice that elite troops desert each day while the resource stays at zero (the tick adds the net before it tests the balance, so at zero with income covering upkeep nothing deserts and no notice shows); then the per-event rates. Every label is a `{=taom_res_tt_*}` key rendered through `TextObject.ToString()` because `TooltipProperty` accepts strings only. `HasWarning` lights when the party holds upkeep troops and `balance + net <= 0`, the desertion trigger, one day ahead; vanilla lights gold the same way (`MapInfoVM.UpdatePlayerInfo`). Until 2026-09-11 the tooltip computed upkeep from an EMPTY troop list, so it never showed an upkeep line (#558). See [gui-sprite-system.md](gui-sprite-system.md).
- **Comprehensive logging:** `[SpecRes]` prefix throughout all components

### Component Diagram

```
special_resources_config.xml + troop_resource_costs.xml
        |
  SpecialResourceConfigProvider (loads + caches XML, multi-key indexes)
        |
  SpecialResourceService (resolve, earn, spend, GetDailyBreakdown, daily tick, desertion)
       / \         \          \              \
      /   \         \          \              \
Behavior  Patch26    MapBarMixin  SpriteWidget  Cheats (taom.print/add_special_resources)
(events)  (party UI) (map bar)   (dynamic icon)
   |                    |                          ICareerPassiveService (gain, upkeep, upgrade cost)
   +-- PartyUpkeepReader (roster + town count, shared by behavior, mixin and console)
   +-- SpecialResourceMessages (the four outflow toasts, pure)
                                              DailyResourceBreakdown / TroopUpkeepLine (domain)
                                              ResourceTier (domain), GetCurrentTier (service)
```

## Configuration

### Resource Definitions: `Main/_Module/ModuleData/special_resources/special_resources_config.xml`

```xml
<!-- excerpt: the shipped row also lists gundabad and dolguldur as kingdom and culture -->
<Resource id="war_spoils" display_name="War Spoils" icon_sprite="SpecialResources\taom_war_spoils_icon"
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
<!-- Its upgrade rungs (#616): the climb costs War Spoils, the upkeep stays the creature rate -->
<Troop id="taom_spider_rider_brown" resource_id="war_spoils" upgrade_cost="2" daily_upkeep="1" />
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
instead, so AI lords are never charged). **The greyed button is not the gate.** v1.5.3's
`GauntletMenuRecruitVolunteersView.OnFrameTick` sends the Confirm hotkey straight to
`RecruitmentVM.ExecuteDone`, which never reads `IsDoneEnabled`, and `OnDone` rechecks gold only, so until
2026-09-15 the hotkey recruited an unaffordable cart and the charge floored at the balance (Codex review
112, #600). `RecruitmentVM_ExecuteDone_Patch`, in the same category, re-runs the cart verdict as a prefix
on `ExecuteDone` and skips the commit with the same line in red. `ExecuteDone` is the one public entry
the button, the hotkey and the over-limit inquiry pass through; the quit path calls `ExecuteReset` first,
so its cart is empty. Both patches fold the cart through the pure `RecruitCartGrouping`.

### Encyclopedia badge (#590)

The encyclopedia troop tree (Home > Troops > a troop) marks every troop whose row carries an
`upgrade_cost`, a `recruit_cost` or a `daily_upkeep` above zero with that troop's resource icon in the
bottom-right corner of its card (27 rows on 2026-09-13, 43 since the Gondor wiring of 2026-09-15, #600). A row that only carries a `merchant_cost` is
an ordinary tree troop the Elite Emissary sells and shows nothing. Hovering the badge lists the
resource, then Upgrade, Recruit, Upkeep per day and Elite Emissary price where set, and adds a line
naming the player's own resource when it is not the troop's, because those three charges land in the
PLAYER's resolved resource whatever the troop's faction.

How it is wired: TAOM's clone of `EncyclopediaUnitTreeNodeItem.xml` carries the badge widget beside
the vanilla tier and type icons, bound to `EncyclopediaUnitBadgeMixin`, a UIExtenderEx mixin on
`EncyclopediaUnitVM` (the VM the card binds as `{Unit}`). Neither tree VM exposes the troop, so the
mixin reads the private `_character` field once at construction (registered in
`docs/reference/taleworlds-api-snapshot/reflection-sites.md`, gated by `ReflectionSiteBindingTests`).
The icon comes from the row's `resource_id` through `ISpecialResourceConfigProvider.GetById`, which
makes that attribute load-bearing for the first time: `TroopResourceCostDataTests` requires every
`resource_id` in the shipped file to name a configured resource. The tooltip is a lazy
`BasicTooltipViewModel`, so nothing renders until hover and no refresh hook is needed. Predicate and
rows live in the pure `SpecialResourceTroopBadge`. The 11 icons are 1024 px sources drawn at 22 px
here (33 px on the map bar); if they alias, widen the widget first and bake a small variant second.

### Current Values (read from `special_resources_config.xml`, 2026-09-11)

Every resource has cap 10000 and starting amount 0. Daily income is per owned town before the career
gain passive; the battle payout is the base times the enemy-to-player size ratio clamped to 0.5 to 2.

| Resource | Town/day | Battle base | Raid | Siege | Prisoner | Tournament | Hideout |
|---|---:|---:|---:|---:|---:|---:|---:|
| War Spoils | 0.2 | 14 | 12 | 20 | 2 | 3 | 8 |
| Gems | 1.0 | 5 | 3 | 10 | 0 | 8 | 4 |
| Castar | 0.6 | 8 | 4 | 18 | 1 | 6 | 5 |
| Marks | 0.4 | 12 | 0 | 12 | 1 | 10 | 6 |
| Elven Wine | 0.8 | 6 | 0 | 10 | 0 | 8 | 4 |
| Lake Fish | 0.7 | 7 | 5 | 10 | 1 | 8 | 5 |
| War Drums | 0.3 | 14 | 14 | 16 | 2 | 4 | 8 |
| Tribal Relics | 0.4 | 10 | 10 | 12 | 1 | 6 | 8 |
| Dunlending Ale | 0.3 | 10 | 12 | 10 | 1 | 5 | 10 |
| Plunder | 0.3 | 10 | 16 | 14 | 3 | 4 | 10 |
| War Banners | 0.5 | 12 | 6 | 16 | 1 | 6 | 5 |

`troop_resource_costs.xml` holds 85 rows: 43 carry a `daily_upkeep` (the 8 Mordor uruk elites at 0.05
to 0.3, the 13 Black Numenoreans at 0.03 to 0.15 since the second halving of 2026-09-15 (#600; the
2026-09-11 rescale to 0.05 to 0.3 shipped in v2.0.29), the 16 Gondor elites at level 41 and above at
0.2 to 0.4 (#600, the Mordor band: L41 upgrade 4 / upkeep 0.2, L46 5 / 0.3, L51 6 / 0.4), the 3
Ironpass rams at 0.1 to 0.25, and the 3 creatures: spider 1, elephant 10, Mumakil 500) and 42 are
merchant-only Elite Emissary offers across Erebor and the Iron Hills, Dol Guldur, Isengard, Gundabad,
Mirkwood, Rivendell, Rohan and Rhun (Gondor's eight offers carry upkeep too since #600). Only the 43
count as upkeep troops for desertion. `TroopResourceCostDataTests` pins two shapes of this file: every
Gondor troop at level 41 or above carries both an `upgrade_cost` and a `daily_upkeep`, and no tree
troop's `daily_upkeep` exceeds 0.4 (the creatures are exempt), so the 1.0 to 3.0 class cannot return
unnoticed. The Black Numenorean values are exact two-decimal numbers because `FormatAmount` renders
`0.##`: 0.025 would display as 0.03 while charging 0.025.

**`upgrade_cost` does not cover a notable pick.** `TaomVolunteerModel.GetBasicVolunteer` seeds an
EMPTY volunteer slot straight from the recruitment pools, and vanilla's `MaxVolunteerTier` cap in
`RecruitmentCampaignBehavior` gates only the growth of an occupied slot, never the seed, so a pooled
elite arrives at any level and never passes through Patch26. Two of the Gondor sixteen are pooled
(the Ithilien Ranger at 10 percent in Minas Tirith and both Osgiliaths, the Fountain Guard in
`clan_empire_west_1`'s pool and the vassal reward) and shipped for a day with no `recruit_cost`, so
they were free to recruit and only ever cost upkeep. Both now carry a `recruit_cost` equal to their
upgrade cost (6 and 5): a notable pick is a door into the rung, not the emissary's on-demand price,
the Ranger has no incoming upgrade edge so this is the only price it ever pays, and the same attribute
is charged per unit on prisoner recruits (ungated, #563), where an emissary-band price would drain the
balance to zero in one screen and start desertion the next day; and
`EveryVolunteerPooledUpkeepTroop_CarriesRecruitCost` requires it of every pooled upkeep troop
(hand-written pools through `VolunteerRecruitmentService.AllPooledTroopIds` plus every
`recruitment_pools/*.json`). RCA: `docs/reviews/rca-gondor-castar-costs-2026-09-15.md`. The Mumakil's 500 a day is
authored creature pricing (one unit eats about eighteen maximum battle payouts a day); it is a balance
question, not a defect, and the daily toast now makes it visible.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SpecialResources/ISpecialResourceService.cs` | Service interface + TroopUpkeepInfo + TroopDesertionEntry |
| `Main/Features/SpecialResources/SpecialResourceService.cs` | Core logic (resolve, earn, spend, desertion, session) |
| `Main/Features/SpecialResources/ISpecialResourceStorageService.cs` | Storage interface |
| `Main/Features/SpecialResources/SpecialResourceStorageService.cs` | Composite-key dict persistence |
| `Main/Features/SpecialResources/ISpecialResourceConfigProvider.cs` | Config interface (GetByKingdomId, GetByCultureId, GetById, GetTroopCost) |
| `Main/Features/SpecialResources/SpecialResourceConfigProvider.cs` | XML loader with multi-key indexing |
| `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` | CampaignBehavior (8 events, desertion, notifications) |
| `Main/Features/SpecialResources/SpecialResourceEarnPolicy.cs` | The two pure earn gates: participation victory + dedicated-server suppression |
| `Main/Features/SpecialResources/SpecialResourceMessages.cs` | Pure builders for the four outflow messages (daily, overdraft, upgrade spend, recruit charge) |
| `Main/Features/SpecialResources/PartyUpkeepReader.cs` | Reads the party roster and town count off engine objects for the tick, the tooltip and the console dump |
| `Main/Features/SpecialResources/Domain/DailyResourceBreakdown.cs` | Earning, upkeep, net, per-troop upkeep lines, `DaysUntilDepleted` |
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
| `Main/Features/SpecialResources/Hooks/RecruitmentVM_RecruitGate_Patch.cs` | Patch51: block Done button when recruit cost unaffordable; `EvaluateCart` shared with the prefix |
| `Main/Features/SpecialResources/Hooks/RecruitmentVM_ExecuteDone_Patch.cs` | Patch51: prefix on `ExecuteDone`, the commit boundary the Confirm hotkey reaches without the button (#600) |
| `Main/Features/SpecialResources/Hooks/RecruitCartGrouping.cs` | Pure: cart of units to one `RecruitCartEntry` per troop id |
| `Main/Features/SpecialResources/Cheats/SpecialResourceCheats.cs` | `taom.add_special_resources` console command |
| `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` | Map bar UIExtenderEx mixin |
| `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs` | Dynamic icon sprite (extends IconBrushWidget) |
| `Main/Features/SpecialResources/UI/SpecialResourcePrefab.cs` | PrefabExtension: swap widget in BottomInfoBar |
| `Main/Features/SpecialResources/SpecialResourceTroopBadge.cs` | Pure predicate and tooltip rows for the encyclopedia badge (#590) |
| `Main/Features/SpecialResources/UI/EncyclopediaUnitBadgeMixin.cs` | UIExtenderEx mixin on `EncyclopediaUnitVM`: badge visibility, icon sprite, lazy tooltip (#590) |
| `Main/_Module/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaUnitTreeNodeItem.xml` | TAOM clone of the vanilla troop-tree node; carries the badge widget beside the tier and type icons |
| `Main/_Module/ModuleData/special_resources/special_resources_config.xml` | 11 resource definitions |
| `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | 85 cost rows: 43 with daily upkeep, 42 merchant-only emissary offers |

## Dependencies

- `IPathService` (Core) — module data path resolution
- `IModLogger` (Core) — logging (`[SpecRes]` prefix)
- `IDedicatedServerProvider` (CoopInterop) — suppresses every earn path on a headless dedicated server
- UIExtenderEx: map bar mixin + prefab extension, encyclopedia badge mixin (#590)
- Harmony 2.x — Patch26_SpecialResources (3 patches)

## Tests

- `SpecialResourceServiceTests.cs`: 81 tests (resolve, earn, spend, validate, daily tick, projected-net deficit warning, pending transaction, desertion, career passives, session reset, edge cases)
- `SpecialResourcesBehaviorSessionResetTests.cs`: 5 tests (plan 001) against real storage. A new campaign in the same process starts with empty storage and still resets the service's session state without reading any hero; a load whose behavior record lacks the balances key yields empty storage; a save loaded over another campaign's balances restores exactly the saved keys and values; the save direction writes the live dictionary and leaves the storage alone
- `SpecialResourceBreakdownTests.cs`: 22 tests. The breakdown excludes zero-upkeep troops from its lines, applies the career modifiers per line so the lines sum to the total (including a relief past minus 100 percent, which clamps to zero), matches `GetProjectedDailyNet` against an independent expected value; `DaysUntilDepleted`, including a net below the balance's float resolution, which yields no countdown; a merchant-only troop and a troop without a cost row do not desert at zero balance while a mixed party deserts only its upkeep troops; `CommitSession` and `ChargeRecruitCost` return the amount that actually left the wallet, measured through real storage, including a recruit charge that lands on a smaller balance
- `SpecialResourceMessagesTests.cs`: 5 tests. Each outflow message keeps every number in a slot (no digit baked into the default text) and binds the expected attributes; `FormatAmount`
- `SpecialResourceDumpFormatTests.cs`: 8 tests for the `taom.print_special_resources` report, including the breakdown lines
- `SpecialResourceStorageServiceTests.cs`: 19 tests (get/set/add, clamp, multi-hero, multi-resource, restore-null, Contains, and the non-finite cases: `Set` refuses NaN and infinity, `Add` with a NaN delta leaves the balance alone, `RestoreData` repairs a poisoned entry to zero)
- `SpecialResourceServiceGrantTests.cs` — 9 tests for `GrantAmount` (cap clamp, floor at 0, already-at-cap, unresolved kingdom/culture, NaN/Infinity rejection, grant during an open party-screen session) against a real storage instance
- `SpecialResourceEarnPolicyTests.cs` — 8 tests: the AI-led-army regression, player-led still earns, losing side, unresolved battle, player on no side, neither side resolved, and both `MayCreditMainHero` cases
- `SpecialResourceCheatsFormatTests.cs` — 6 tests for the console echo, including a legacy balance above a lowered cap
- `SpecialResourceTierServiceTests.cs` (14), `SpecialResourceConfigProviderTierTests.cs` (6, plus the 2 `GetById` tests for a known and an unknown or null id) and `ResourceTierTests.cs` (3): tier resolution by threshold, `<Tiers>` parsing and sort order, the domain record
- `SpecialResourceTroopBadgeTests.cs`: 14 tests (#590). The predicate for each cost field alone, the merchant-only row shape and a null row; the rows for a captain, an elephant rider and a ram in their fixed order with zero fields omitted; the paid-in note only when the player's resource differs; no digit baked into any label
- `RecruitGatePatchTests.cs`: 4 tests (#600). `RecruitCartGrouping` counts duplicates, skips nulls, keeps first-seen order; the `ExecuteDone` prefix carries Patch51's category and targets `ExecuteDone`
- `TroopResourceCostDataTests.cs`: 5 tests reading the shipped XML: every `resource_id` names a configured resource, every badged row reaches a `SpecialResources\` icon sprite, every Gondor troop at level 41 or above carries an `upgrade_cost` and a `daily_upkeep`, no tree troop's `daily_upkeep` exceeds the 0.4 band ceiling, and every volunteer-pooled upkeep troop carries a `recruit_cost` (#600)
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
| `taom.print_special_resources` | Read-only: name, id, balance, cap, tier, pending spend, then the daily breakdown (income with town count, upkeep with one line per troop type, net). Paste this when a player reports a vanished balance |

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
4. Add a 33x33 PNG icon to `Main/_Module/GUI/SpriteParts/ui_taom/SpecialResources/` and reference it as `icon_sprite="SpecialResources\<file name without extension>"`; then re-run the sprite generator and verify the bake, per [gui-sprite-system.md](gui-sprite-system.md) (a loose PNG alone renders blank)
5. No C# changes needed — fully data-driven

## How to Tune Earning Rates

Edit attributes on the `<Resource>` element. Each resource has its own rates; the table under Configuration is the shipped set. The provider is `Reuse.Singleton`, so a change needs a full game restart, not a new campaign.

## Earning Rules

*Who* qualifies to earn is two decisions, both in `SpecialResourceEarnPolicy`. They are pure statics
so they can be tested without a running campaign (`MapEvent` is sealed and unconstructible in a unit
test) — the behavior keeps the plumbing, the policy keeps the verdict, the same split as
`PatchShieldPolicy` / `CoopSessionPolicy`.

**Participation, not command.** A battle pays out when `MapEvent.PlayerSide == MapEvent.WinningSide`.
The gate this replaced asked whether the player *is* the winning side's `LeaderParty.LeaderHero`,
which conflated fighting with commanding — and **in ordinary single-player, joining any AI lord's army
makes you stop being the leader party's hero, so every victory you fought inside that army paid
zero.** Players who campaign as a vassal will notice earnings they never used to get.

`BattleSideEnum.None` on **either** side fails the gate. An unresolved battle state is the routine
reading on a co-op client — the server is authoritative and never re-broadcasts `BattleState` — and
treating it as a win would pay out for defeats.

**A dedicated server credits nobody.** `Hero.MainHero` exists on a headless server, but it is the idle
world-gen hero the campaign was created around, not anybody's character. `SpecialResourcesBehavior.CanEarn()`
therefore suppresses all five earn paths — `OnMapEventEnded`, `OnRaidCompleted`, `OnPrisonerTaken`,
`OnHideoutCompleted`, `OnTournamentFinished` — when `IDedicatedServerProvider.IsDedicatedServer` is
true, logging one `[SpecRes]` line and staying quiet after. Without it the server banks income nobody
can spend while the remote players who fought the battles earn nothing.

That gate keys off the **process**, not the co-op role: `DedicatedServerProvider` reads whether this
assembly loaded from `Win64_Shipping_Server`. A client-hosted session's host also reports `IsServer`
but is a real player at a real keyboard and must keep earning — which is why the provider exists
instead of reusing `ICoopSessionProvider`. It fails to "not a server", because these gates only ever
suppress.

**Which hero gets the starting seed after a multiplayer join.** `ISpecialResourceService.InitializeHero`
has a caller beyond `OnCharacterCreationIsOver` / `OnGameLoaded`: when a co-op join replaces the
character-creation hero with a host-authored one,
[player-possession.md](player-possession.md) re-invokes the seed against the hero the player actually
ends up controlling, resolving it from the character-creation culture and the live kingdom.

## Desertion Mechanics

- Triggers daily when resource balance is 0 and party has upkeep-costing troops
- An upkeep-costing troop is one whose cost row has `daily_upkeep > 0`. The rule is applied in `CalculateDesertion` and in `GetDailyBreakdown`, never on the presence of a row: 42 of the 85 rows in `troop_resource_costs.xml` are merchant-only Elite Emissary offers, and until 2026-09-11 a party holding one of those at zero balance lost 10% of them a day to "your Gems are depleted" (#558)
- 10% of each troop type deserts per day (minimum 1 per type)
- Center-screen notification: "X elite troops deserted — your [Resource] are depleted!"
- Uses vanilla `TroopRoster.AddToCounts(character, -count)` for roster removal

## Performance

- IoC.Resolve cached in MapBarMixin constructor (not per-refresh)
- Config provider lazy-loaded with dictionary indexes
- No LINQ in hot paths — direct enumeration loops
- SpriteWidget caches resolved sprite (loads once, not per-frame)
- String formatting only when amount changes (cached `_lastAmount`); `HasWarning` is written only when it flips (`_lastWarning`)
- The map-bar refresh (about 5 Hz, 10 Hz in fast-forward) walks the roster once and builds the breakdown to drive `HasWarning`; the cost class equals vanilla's own per-refresh `CalculateClanGoldChange` on the same tick, and a roster-version cache was declined in review 95 (a field plus a stale-icon edge for a few hundred bytes per refresh). Revisit if a profile shows it
- Comprehensive logging uses `LogDebug` for high-frequency paths, `LogInfo` for events

## Changelog

- 2026-09-24 (plan 001): a second campaign started in the same game process no longer inherits the first one's balances. `OnNewGameCreated` wipes the singleton storage, and the SyncData load reads into a null local so a behavior record without the balances key loads empty. A save older than the feature (no record at all) still inherits a previous campaign's balances until the load-path follow-up lands. Five tests in `SpecialResourcesBehaviorSessionResetTests`. In-game smoke owed: finish or quit a campaign, start another without restarting, check the map bar shows only the new culture's starting amount.
- 2026-09-15 (#600): Gondor's 16 elites at level 41 and above cost Castar at the Mordor band (L41 upgrade 4 / upkeep 0.2 a day, L46 5 / 0.3, L51 6 / 0.4); until then Gondor's only rows were the eight merchant-only emissary prices, so a Gondor player promoted into Fountain Guards for gold and XP alone. The Black Numenorean line's upkeep halved again to 0.03 / 0.05 / 0.08 / 0.1 / 0.15 on player feedback that the line is hard to keep (the 2026-09-11 rescale had shipped in v2.0.29 the day before, so the report likely predates it). Three shipped-data tests pin the Gondor coverage, a 0.4 ceiling on tree-troop upkeep, and a `recruit_cost` on every volunteer-pooled upkeep troop: the deep review found the Ithilien Ranger and the Fountain Guard reachable from a notable for gold alone (the volunteer model seeds empty slots from the pools at any level), so both now carry one equal to their upgrade cost (6 / 5). Codex review 112 then found the recruit gate bypassable by the Confirm hotkey (`ExecuteDone` never reads `IsDoneEnabled`); `RecruitmentVM_ExecuteDone_Patch` closes it at the commit boundary. In-game smoke owed.
- 2026-09-13 (#590): the encyclopedia troop tree badges every troop that costs a resource to upgrade, recruit or keep, with the troop's own resource icon and a hover tooltip of the costs; `ISpecialResourceConfigProvider.GetById`; the row's `resource_id` is now gated by `TroopResourceCostDataTests`; six `taom_res_badge_*` keys seeded as English in all 12 languages and translated the same day in the backlog run (#579). Verified in game the same day.
- 2026-09-11 (#558): every outflow is visible. The tooltip renders the real daily breakdown (it had computed upkeep from an empty list since the first commit), with per-troop upkeep rows, days until depleted and a warning flag one day ahead of desertion; a daily income/upkeep line, an overdraft line, an upgrade-spend line and a recruit-charge line join the earning toast; troops whose row carries no `daily_upkeep` no longer desert; `taom.print_special_resources` prints the breakdown; 20 new `taom_res_*` keys, seeded as English in all 12 languages (translator run owed). Data: the 13 Black Numenorean rows rescaled to the Mordor line's upkeep ratio and merchant band. Codex (GPT-6-Astra at ultra, review 95) added three MEDIUM fixes the same day: measured debits behind the spend and charge lines, a range-checked countdown cast, and the zero-balance notice gated on the net.
- 2026-08-03 — Earning is keyed on participation (`MapEvent.PlayerSide == WinningSide`) instead of commanding the winning side, which also fixes single-player: fighting inside an AI lord's army used to pay nothing. Added `SpecialResourceEarnPolicy` (pure, 8 tests) and a dedicated-server gate that suppresses all five earn paths.
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

- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/elite-emissary.md](./elite-emissary.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)

<!-- backlinks-end -->
