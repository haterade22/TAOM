# Troop Weight System

> **⚠️ MECHANISM CHANGED 2026-07-11 (count → limit).** The "elite tax" is now applied by **deflating the
> party-size LIMIT** (`TaomPartySizeModel` → `ITroopWeightService.ApplyPartySizeWeightPenalty`), NOT by
> weighting the member COUNT. Every troop count now reads **raw** everywhere (map/party-screen/battle agree);
> the displayed limit shrinks with heavy troops. The count-getter patches, weighted-display hooks, the
> `WeightedCountCache`, and the `[CountFlicker]` diagnostic were removed. **The Overview / Architecture /
> Phantom-Wounded / count-cache sections below describe the pre-rework design and are retained for history —
> the authoritative current behavior is the "Count-Display Investigation + Planned Rework → IMPLEMENTED
> 2026-07-11" section.**

## Overview

Elite and supernatural units consume more party capacity than standard troops. A cave troll takes 4 party slots, Rivendell elves take 2 each, and legendary commanders take 3. This prevents players from fielding armies composed entirely of elite units, encouraging balanced army compositions that fit Middle-earth lore.

## Why This Exists

- **Vanilla behavior:** All troops count as 1 party member regardless of power level. A party of 100 cave trolls uses the same capacity as 100 peasant militia.
- **TAOM requirement:** LOTR factions have wildly different power tiers. Elven warriors are individually far more powerful than orc grunts. Without constraints, players (and AI) would always recruit the highest-tier units, making army composition meaningless.
- **Without this feature:** Players can field 100+ Rivendell blademasters or cave trolls, trivializing combat and breaking the intended faction asymmetry where evil factions rely on numbers and good factions on quality.

## Architecture

### Design Challenge

Bannerlord's party size system is deeply integrated — `PartyBase.NumberOfAllMembers` and related properties are called hundreds of times per campaign tick for movement, wages, party limit warnings, and AI decisions. The solution must:
1. Be performant (called very frequently)
2. Never decrease the member count (would break game systems expecting raw count)
3. Update UI consistently (recruitment screen, party management screen)
4. Be toggleable (MCM setting for players who don't want this restriction)

### Solution Approach

Four Harmony postfix/prefix patches intercept the property getters that return party member counts. When the weighted count exceeds the raw count, the patch increases `__result`. This approach modifies the *perceived* party size without changing actual troop storage, so all vanilla systems (recruitment, AI, save/load) work unchanged.

Patches target `PartyBase`-level getters only (not `TroopRoster` getters) to avoid firing on every roster in the game (prisoner, garrison, temp rosters). Two additional UI patches ensure the recruitment screen and party management screen display the correct weighted counts.

### Component Diagram

```
troop_weights.xml
        |
  TroopWeightXmlLoader (IPathService for path resolution)
        |
  TroopWeightService (caches weights by StringId)
       / \
      /   \
PartyBase    UI Hooks
Hooks (2)    (2 - Recruitment + Party VM)
      \   /
       \ /
  Harmony Patches (Patch17_TroopWeight)
  [TaomSettings.EnableTroopWeight guard]
```

## Phantom-Wounded Display Fix (2026-06-07)

### The bug

A brand-new campaign showed the player party as **"62 troops / 16 wounded"** with no battle fought. The wounds were **phantom**. The party genuinely had **46 soldiers, 0 wounded**, that *weighed* 62 toward the 23 cap because some were weight-≥2 troops.

Vanilla derives the displayed wounded count by subtracting two sibling getters:

```
wounded = PartyBase.NumberOfAllMembers - PartyBase.NumberOfHealthyMembers
```

This feature weights `NumberOfAllMembers` (→ 62) but deliberately leaves `NumberOfHealthyMembers` **unweighted** (→ 46), because that getter feeds gameplay, not just display. So the weight surplus (62 − 46 = 16) rendered as phantom wounds. A weight-2 troop adds 2 to `NumberOfAllMembers` but 1 to `NumberOfHealthyMembers`; the gap is the phantom count.

### Why the getter is NOT weighted (the fix is display-only)

Weighting `NumberOfHealthyMembers` globally would be the tidy fix but is **gameplay-dangerous**. Decompile-verified consumers that would break: `PartyGroupTroopSupplier` (battle troop supply), `MapEventParty._healthyManCountAtStart` + `DisorganizedStateCampaignBehavior` (casualty tracking), `DefaultTroopSacrificeModel` (sacrifice limit — would let you sacrifice more men than you have), `DefaultInventoryCapacityModel`, `DefaultPartyDesertionModel`, battle strength/winner determination. So the fix touches **display only**.

### The four display surfaces fixed

All four compute `NumberOfAllMembers − NumberOfHealthyMembers`. Each gets a display-only Postfix in `Patch17_TroopWeight` that rewrites the shown numbers with a weighted (healthy, wounded) split via `ITroopWeightService.GetWeightedHealthAndWounded`, so **battle-ready + wounded equals the weighted member total** the panel header already shows (e.g. "Battle Ready 62 / Wounded 0", matching "62/23").

| Surface | Vanilla method | What the Postfix rewrites |
|---------|----------------|---------------------------|
| Main party HUD health tooltip | `CampaignUIHelper.GetMainPartyHealthTooltip` | "Battle Ready Troops" + "Wounded Troops" values; strips the spurious healing-rate block when weighted wounded == 0 |
| Any-party health tooltip | `CampaignUIHelper.GetPartyHealthTooltip(PartyBase)` | Same |
| Encounter "X vs Y" menu item | `GameMenuPartyItemVM.RefreshCounts` | `PartySize` / `PartyWoundedSize` / `PartySizeLbl` |
| Party map nameplate text | `Helpers.PartyBaseHelper.GetPartySizeText(PartyBase)` | Rebuilds the `str_party_health` TextObject with weighted `HEALTHY_NUM` / `WOUNDED_NUM` |

All four run for **every** party (the `NumberOfAllMembers` weighting is not main-party-only), so enemy/ally party tooltips and nameplates with heavy troops are corrected too. All four gate on `TaomSettings.EnableTroopWeight` — toggling the feature off reverts every surface to vanilla.

### Known property: separate-ceiling rounding

`GetWeightedHealthAndWounded` ceilings weighted healthy and weighted wounded **independently** (matching the existing `PartyVMPopulatePartyListLabelHook`). For **integer** weights — all TAOM ships — `healthy + wounded` exactly equals the weighted member total. With *fractional* weights and mixed wound states, the two ceilings can sum to 1 above `Ceiling(total)`, making the tooltip read 1 higher than the panel header. Cosmetic-only; documented rather than "fixed" because changing it would make the tooltip disagree with the party-list label.

### Performance

`GetWeightedHealthAndWounded` walks the roster (allocation-free — no intermediate collection) and caches the result per party in a `ConditionalWeakTable<PartyBase, box>` keyed by `MemberRoster.VersionNo`. The weak table is reference-keyed (no `GetHashCode` collisions) and auto-evicts on party GC (no unbounded growth — unlike the `Dictionary<int,...>` caches in the count hooks). `VersionNo` is decompile-verified to bump on wound/heal (`TroopRoster.AddToCountsAtIndex` → `UpdateVersion()` when `woundedCountChange != 0`), so the cached wounded count is never stale after a battle.

RCA: [`docs/reviews/rca-troopweight-phantom-wounded-2026-06-07.md`](../reviews/rca-troopweight-phantom-wounded-2026-06-07.md).

## Shed-on-Upgrade — AI lords respect troop weight (2026-06-16)

### The gap

A user reported AI lords (e.g. a Lothlórien army of 317 elves) fielding far more elite troops than
the weight budget should allow. Investigation (decompile-verified) found:

- **AI recruitment already respects weight.** Vanilla `RecruitmentCampaignBehavior` reads the patched
  `PartyBase.NumberOfAllMembers` everywhere it decides "am I full / recruit more," so AI parties stop
  *recruiting* at their weighted cap — exactly like the player.
- **AI auto-upgrades did NOT.** `PartyUpgraderCampaignBehavior.UpgradeReadyTroops(PartyBase)` auto-
  upgrades an AI party's ready troops with no party-size check (it skips only `MainParty`). In vanilla
  this is harmless because an upgrade preserves headcount, and headcount == weight when every troop is
  weight 1. TAOM breaks that invariant: a party fills its cap with weight-1 recruits, then auto-upgrades
  them into weight-2/3 elites and balloons to 2–3× its intended weighted budget. Nothing trimmed it.
- **The player party is immune** — it never auto-upgrades (you upgrade manually and see the weighted
  count via the display patches).

### The fix

A postfix on `UpgradeReadyTroops` (`Patch17_TroopWeight`) runs once per party *after* all its upgrades
(so the roster mutation happens outside vanilla's loop). The hook re-guards `MainParty`/`!IsActive`
(vanilla's own guard), skips leaderless parties (see below), early-outs when
`CalculateWeightedMemberCount(party) <= PartySizeLimit`, then reads the roster into engine-free
`WeightedTroopEntry` rows and calls the pure planner:

`ITroopWeightService.PlanShed(entries, limit)` sheds the lowest-value bodies first (ascending Tier,
then Weight; never heroes/leader) until the weighted total is back within the limit — "fewer, better
troops." Removals apply via `MemberRoster.AddToCounts(character, -count)`. Side benefit: over-cap
*legacy* parties trim on their next upgrade tick too. Gated on `EnableTroopWeight`; event-gated
`[TroopWeight][diag]` logging fires only when a shed happens (strip after in-game sign-off).

### Scope: leader-run parties only (2026-08-07)

Vanilla drives `UpgradeReadyTroops` from `DailyTickPartyEvent`, and `CampaignPeriodicEventManager`
tickers that over **`MobileParty.All`** — so the postfix sees militia, garrison, villager, caravan and
bandit parties, not just lord parties. Only a leader-run party has a limit worth enforcing:
`CalculateMobilePartyMemberSizeLimit` skips the leader/clan/steward branch when `LeaderHero` is null
and returns a flat 20-body base, and `GetPartyMemberSizeLimit` returns 0 for a settlement's own
`PartyBase` (which reaches the hook via `MapEventEnded`). Shedding to those numbers is deletion, not
enforcement, so the hook returns early on `party.LeaderHero == null`.

Shipped 2026-06-16 without that guard, it capped **every settlement's militia at ~20** regardless of
prosperity for three weeks — the overlay kept reporting the model's `MilitiaChange` because the model
never sees the shed. It also undid `Patch39_BanditPartySize` (bandit parties are leaderless, so their
scaled-up spawn rosters were cut back on the next daily tick) and trimmed garrisons to
`CalculateGarrisonPartySizeLimit`, a limit vanilla computes but never enforces as a hard cap — which
is the same reason the garrison tooltip below is deferred as "not party-size-budgeted".

### Auto-resolve is power-driven, NOT count-driven (why weight can't touch it)

Simulated-battle strength is `Σ over each man (Number − Wounded) × per-troop-power`, built by iterating
`MemberRoster` directly (`MapEvent` → `MapEventSide` → `DefaultMilitaryPowerModel.GetPowerOfParty`). It
**never reads** the TAOM-patched `NumberOfAllMembers`/`NumberOfRegularMembers` getters (it uses the
deliberately-unweighted `NumberOfHealthyMembers` for side size). So troop weight is invisible to auto-
resolve, and 317 elves correctly fight as 317 real bodies at elite power. The *only* weight-side lever
for "elven armies should be a bit lower in a fight" is making the army **be** smaller — which shed-on-
upgrade does. (Scaling per-troop power by weight in `TaomMilitaryPowerModel.GetDefaultTroopPower` is the
only alternative and was rejected — it would nerf elite combat strength, contradicting the design.)

### UI count displays (weighted vs raw)

Surfaces that read the raw `MemberRoster.TotalManCount` show the *unweighted* headcount. Newly corrected
to the weighted total:

| Surface | Method | Status |
|---------|--------|--------|
| Clan-screen party list ("X/limit" + "Party Size:" subtitle) | `ClanPartyItemVM.UpdateProperties` | **Fixed** (postfix, via `TroopWeightDisplayHook`) |
| Main-party health tooltip — "Land Troop Capacity" row | `CampaignUIHelper.GetMainPartyHealthTooltip` (raw at `:1083`) | **Fixed** (extended `RewriteHealthTooltip`; the red over-capacity tint still follows vanilla's raw compare — cosmetic) |
| Map party-nameplate hover tooltip ("Troops (N)" + formation breakdown) | data-bound map widget — exact builder not located in the decompile | **Deferred** — needs in-game tracing before a safe patch |
| Town garrison tooltip | `CampaignUIHelper.GetTownGarrisonTooltip` (`:561`) | **Deferred** — weighting a garrison count is debatable (garrisons aren't party-size-budgeted) |
| Map info bar troop count | `MapInfoVM` | **Deferred** — low impact (player's own count) |

Already weight-aware (no work): anything reading `NumberOfAllMembers` directly, and the 6 prior patches
(recruitment screen, party-transfer label, the 3 health-tooltip battle-ready/wounded surfaces, map
nameplate *number* via `PartyBaseHelper.GetPartySizeText`). `ArmyManagementItemVM.Strength` is a power
figure, not a count — out of scope.

## Count-Display Investigation + Planned Rework (2026-07-11)

Player reports of "incorrect troop count" split into **two distinct symptoms**, both traced this session.
The umbrella conclusion: the weighting makes displayed party-size numbers disagree with the real body
count, and that (not any single bug) is what confuses players. Issue investigation lives here so a future
session doesn't re-derive it.

### Symptom A — campaign-map nameplate "200 then 20 then back" (NOT the weighting)
The always-visible number over a party icon reads RAW `NumberOfHealthyMembers` via
`SandBoxUIHelper.GetPartyHealthyCount` (`SandBox.ViewModelCollection.dll`) — a value TroopWeight never
patches (`PartyNameplateVM.RefreshDynamicProperties` → `Count`). So the map flicker is **not** the
weighting. A temporary `[CountFlicker]` diagnostic (`SandBoxUIHelper_GetPartyHealthyCount_Patch`,
`Patch17_TroopWeight` category — logs a classified line on any large-ratio swing) captured **38 events**
in one session and proved the mechanism is the vanilla **army-sum**: `GetPartyHealthyCount` sums an army
*leader's* attached parties, so a leader's nameplate shows the whole-army healthy total (e.g. Dûrzan
`swing=52->890`, where `armyTotalHealthy = 52 solo + 838 attached`) and its solo count otherwise —
swinging as army membership/leadership churns. Overwhelmingly lords (37/38 events), essentially not
bandits (1 tiny event). Every event had `NumberOfAllMembers == fresh` (no cache defect) and **zero**
desertion events fired — desertion (`AlignmentDesertion` + `SpecialResources`) is remove-only + daily,
so it cannot produce a back-and-forth swing.

### Symptom B — "party screen shows 325 / capacity 407, but only 159 fight" (IS the weighting, working as designed)
Map + battle show the RAW body count (159); the party screen (weighted `NumberOfAllMembers`) and the
"Land Troop Capacity" bar (weighted healthy+wounded) show the ~2× WEIGHTED slot cost. This is the
feature doing exactly what it's built to do (heavy troops cost more party-size budget) — but the mismatch
reads as "the game can't count." Whether 325 is correct weighting (elite-heavy party) vs an
over-weight bug is confirmable per-party via the existing `[TroopCountDiag]` dump (per-slot weights,
logged on party-screen open).

### IMPLEMENTED 2026-07-11 — option 2: weighting moved from the count onto the size limit
Every surface now shows the real (raw) count while the "heavy troops cost more" cap is preserved. Because
the party screen and the engine's recruit gate read the *same* `NumberOfAllMembers` getter, you can't make
one raw and the other weighted by patching the getter — so the weighting was **relocated off the count**:

1. **Unpatched** `PartyBase.NumberOfAllMembers` / `NumberOfRegularMembers` (patches + hooks + the
   `WeightedCountCache` deleted) → every count (party screen, nameplate, capacity, tooltips, menus, battle)
   now reads RAW and they all agree.
2. **Removed** the 5 `TroopWeightDisplayHook` surfaces + the `[CountFlicker]` diagnostic (~26 files total).
   The phantom-wounded fix those hooks provided is now moot — nothing is weighted in display, so no surplus
   can render as fake wounds.
3. **`TaomPartySizeModel.GetPartyMemberSizeLimit`** now calls `ITroopWeightService.ApplyPartySizeWeightPenalty`,
   which subtracts the weight surplus (`ceil(weighted) − raw`) from the limit — clamped so it never drops
   below 1 (pure, unit-tested `TroopWeightService.ComputeSizePenalty`). Boundary math:
   `weighted = baseLimit` ⟺ `raw = baseLimit − (weighted − raw)`; enforcement path
   `MobileParty.PartySizeRatio = NumberOfAllMembers / PartySizeLimit` (`MobileParty.cs:1176`) +
   `PartyBase.PartySizeLimit` (`PartyBase.cs:343-351`).
4. **Shed-on-upgrade** adapted to the deflated frame: over-cap when `raw > PartySizeLimit`; recovers the
   pre-deflation base (`deflated + surplus`) so `PlanShed` (unchanged) still trims in the weighted frame.

**Visible consequence (by design):** the party-size **limit** now shrinks as heavy troops are added
(`150 / 240` instead of `150 / 300`) — the honest way to show the weight cost without an invisible recruit
wall. The recruit *cap* is preserved exactly; only the intermediate fill-ratio shifts slightly.

**Blast-radius handling** — unpatching a global getter changed every consumer of the weighted count:
- `SpecialResources` battle-reward scaling (`SpecialResourcesBehavior:253`) read the weighted enemy count;
  **preserved** by switching it to an explicit `CalculateWeightedMemberCount` call (same rewards).
- `SettlementFood`'s garrison food-leak correction (`TownFoodSnapshot`) computed `weighted − raw`; now `= 0`
  because the getter is raw, and vanilla food math is correct at source — **net food unchanged**, the
  correction self-neutralizes (left in place; its rationale is now handled upstream).
- Incidental side-effects the old global weighting had on OTHER engine consumers (party speed, etc.) are
  intentionally gone — the feature now affects only the size cap, its stated purpose.

Symptom A (army-sum nameplate) is separate and left as vanilla behavior — a later decision (show solo count
on nameplates, or calm army churn) if players still complain now that the party-UI counts are honest.

### The elite tax is one of three contributors to the same `ExplainedNumber`

`TaomPartySizeModel.GetPartyMemberSizeLimit`
([`Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs:33-56`](../../Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs))
layers three TAOM modifiers onto the single `ExplainedNumber` vanilla hands back, in this order:

| # | Line | Call | How it lands |
|---|------|------|--------------|
| 1 | `:40` | `ICulturalFeatsService.ApplyPartySizeFeats` (culture party-size feat) | `AddFactor`, a percentage |
| 2 | `:44` | `ICareerPassiveService.ApplyFlat(…, PassiveEffectType.PartySize)` (career passive) | flat `Add`, authored as a body count |
| 3 | `:51` | `ITroopWeightService.ApplyPartySizeWeightPenalty` (this feature) | result-frame subtraction of the weight surplus |

So the elite tax **competes** with a culture bonus on the same number, and the two can cancel. On a
Mordor, Isengard, Dol Guldur or Gundabad party the weight-2.0 uruks, wargs and black guards can make
the surplus subtract more than a small percentage bonus adds, which is why those cultures' party-size
feats now carry a 20% floor, pinned by `ApplyPartySizeFeats_EvilCultures_WithinTwentyToFiftyPercent`.
Rationale and the excluded cultures: [cultural-feats.md](./cultural-feats.md) "Evil-culture party-size
floor". For how this limit relates to the roster a party is handed at spawn (a separate model that the
limit does not cap), see [party-template-sizing.md](../reference/party-template-sizing.md).

Two things about that interaction read backwards if you assume weight tracks alignment. Both counted
against `troop_weights.xml` and the `troops/troops_*.xml` rosters on 2026-08-14:

- **A weighted troop is not an evil-culture marker.** Rivendell has 22 of its 30 troop ids weighted
  and Mirkwood 13 of 19, against Dol Guldur 16 of 50, Isengard 8 of 52, Mordor 6 of 49 and Gundabad 3
  of 30 (Erebor carries 16 of 60). The evil trees are mostly weight-1.0, so the surplus that eats
  their feat comes from a minority of the roster.
- **Three of the seven cultures the floor test pins take no tax at all.** Goblin, Blue Craig and Misty
  Mountain Orcs have zero entries in `troop_weights.xml`, so for them the floor raises a bonus that
  nothing is subtracting from.

Ordering matters only for contributor 3. `ExplainedNumber` sums factors and applies them to
`BaseNumber`, so an `Add` lands in the base frame whenever it runs; the weight penalty is a
result-frame body count, so `SubtractResultFramePenalty` divides `1 + SumOfFactors` back out
(`Main/Features/TroopWeight/TroopWeightService.cs:141-153`) and must run after the feats so it reads
the boosted `ResultNumber` as its base.

Contributor 2 is the easiest of the three to miss. The cultural-feats floor section names only 1 and
3, so a reader arriving from there sees two of the three. The two pages that carry all three are
[party-template-sizing.md](../reference/party-template-sizing.md) and the `TaomPartySizeModel` row in
[gamemodel-registry.md](../reference/gamemodel-registry.md).

**Open question, deliberately unanswered.** The Overview above frames this feature as a composition
incentive. A culture whose roster is heavy end to end has no composition to choose, so for it the tax
degenerates into a flat cap cut: an all-weight-2.0 roster settles at `raw = baseLimit / 2` by the
boundary math in point 3 above. On the counts above that lands on Rivendell and Mirkwood, and neither
culture appears in `ApplyPartySizeFeats`, so nothing offsets it. The Overview also names that exact
case ("100+ Rivendell blademasters") as the thing to prevent, so the flat cut may be the intent rather
than a gap. Nobody has written down which. The 20% floor answers the opposing-bonus question for
Mordor, Isengard, Dol Guldur and Gundabad; it does not answer this one.

## Configuration

### Config File: `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`

Simple XML format with one element per weighted troop. Any troop not listed defaults to weight 1.0.

```xml
<TroopWeights>
    <TroopWeight id="cave_troll" weight="4.0" />
    <TroopWeight id="imladris_blademaster" weight="2.0" />
</TroopWeights>
```

| Attribute | Type | Description |
|-----------|------|-------------|
| `id` | string | NPCCharacter StringId (case-insensitive) |
| `weight` | float | Party capacity multiplier (must be > 0) |

### Current Weight Tiers

> **Note (2026-05-14):** The `cave_troll` TroopWeight is currently commented out in `troop_weights.xml` (WIP — see CHANGELOG "Phase 9c — Disable troll content in-place"). The 4.0 tier remains documented here for reference and will reactivate when the troop is re-enabled.

| Weight | Count | Troop Types |
|--------|-------|-------------|
| 4.0 | 1 | Cave trolls (currently disabled) |
| 3.0 | 7 | Rivendell Gondolin units (5), Mirkwood palace guard (2), Erebor royal elite (2) |
| 2.0 | ~70 | All Imladris/Mirkwood elves, warg riders (all cultures), Black Numenoreans, Khamul's elite, Dol Guldur uruk black guard, Mordor elite captains, Orthanc guard, Erebor oathsworn |
| 1.0 | default | All standard human/orc/goblin infantry, archers, militia, cavalry |

### MCM Setting

`TaomSettings.EnableTroopWeight` (default: `true`) — toggleable at runtime, checked by every patch before executing.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TroopWeight/ITroopWeightService.cs` | Service interface: `GetTroopWeight(string)`, `CalculateWeightedMemberCount(PartyBase)`, `PlanShed(...)`, etc. |
| `Main/Features/TroopWeight/TroopWeightService.cs` | Core implementation with `Dictionary<string, float>` cache (case-insensitive) + the pure `PlanShed` planner |
| `Main/Features/TroopWeight/TroopShedPlanning.cs` | Engine-free `WeightedTroopEntry` / `ShedInstruction` types for the pure shed planner |
| `Main/Features/TroopWeight/Hooks/PartyUpgraderUpgradeReadyTroops*` | Shed-on-upgrade: interface + boundary hook + `Patch17_TroopWeight` postfix on `UpgradeReadyTroops` |
| `Main/Features/TroopWeight/Hooks/ClanPartyItemVM_UpdateProperties_Patch.cs` + `IOnClanPartyItemUpdateProperties.cs` | Clan-screen party-list weighted count (handled in `TroopWeightDisplayHook`) |
| `Main/Features/TroopWeight/ITroopWeightXmlLoader.cs` | Loader interface |
| `Main/Features/TroopWeight/TroopWeightXmlLoader.cs` | XML parser using `IPathService`, graceful degradation on missing file |
| `Main/Features/TroopWeight/TroopWeightIoC.cs` | `RegisterTroopWeightFeature()` + `InitializeHooks()` |
| `Main/Features/TroopWeight/Hooks/IOn*.cs` | 8 hook interfaces (2 PartyBase + 2 party/recruitment UI + 4 phantom-wounded display) |
| `Main/Features/TroopWeight/Hooks/*Hook.cs` | 5 hook implementations (the original 4 + `TroopWeightDisplayHook`, which implements the 4 display interfaces) |
| `Main/Features/TroopWeight/Hooks/*_Patch.cs` | 8 Harmony patches (all `Patch17_TroopWeight`): 2 PartyBase getters + RecruitmentVM + PartyVM + the 4 phantom-wounded display patches |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | Weight definitions (~80 entries) |
| `Main/Features/TaomSettings.cs` | MCM toggle (`EnableTroopWeight`) |

## Dependencies

- `IPathService` (Core/Infrastructure) — Resolves `ModuleDataPath` for XML file location
- `IModLogger` (Core/Logging) — Error/warning logging
- `TaomSettings` (Features) — MCM toggle check in every patch

## Tests

- `TAOM.Tests/Features/TroopWeight/TroopWeightServiceTests.cs` — covers null/empty/known/unknown IDs, caching, case insensitivity, cache clearing, PLUS the `ComputeWeightedHealthyAndWounded` phantom-wounded core: the regression case (weight-2 troops, 0 real wounds → 0 wounded), the `healthy + wounded == weighted total` invariant, real-wounded weighting, empty/null, negative-wounded, wounded>number floor, and fractional-weight ceiling
- `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs` — 10 tests covering valid XML, missing file, lazy loading, duplicates, zero/negative weights, missing attributes, invalid values, case insensitivity, reload
- `TAOM.Tests/Features/TroopWeight/TroopWeightHooksTests.cs` — construction / interface / null-tolerance for all hook implementations including `TroopWeightDisplayHook` (the display hooks touch sealed TaleWorlds types, so behavior is verified in-game; the weighted math is fully unit-tested via the service core)

## How to Add a New Weighted Troop

1. Open `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`
2. Add a `<TroopWeight id="troop_string_id" weight="2.0" />` element
3. The `id` must match the NPCCharacter's `id` attribute in the troop XML files (case-insensitive)
4. No code changes needed — the loader picks up new entries on next game load
5. To force a mid-game reload, the `TroopWeightXmlLoader.ReloadWeights()` method is available but not currently exposed via UI

## How to Add a New Weight Tier

Weight values are continuous floats — any positive value works. Common tiers:
- `1.0` — Standard (default for unlisted troops)
- `2.0` — Elite (occupies 2 party slots)
- `3.0` — Legendary (occupies 3 party slots)
- `4.0` — Monster (occupies 4 party slots)

## Performance

- **Troop weight lookup:** `Dictionary<string, float>` eagerly populated at startup — O(1) per troop, no lazy caching or writes on hot path
- **Party member count cache:** shared `WeightedCountCache<PartyBase>` (a reference-keyed `ConditionalWeakTable<PartyBase, …>`) in `PartyBaseNumberOfAllMembersHook` / `PartyBaseNumberOfRegularMembersHook`, invalidated by `MemberRoster.VersionNo` changes. Keyed by **object identity, never `GetHashCode()`** — the pre-2026-07-11 `Dictionary<int hashcode, …>` let two hashcode-colliding parties that shared a `VersionNo` read each other's weighted count, producing the campaign-map "200 ↔ 20" party-count flicker on raw `NumberOfAllMembers`/`NumberOfRegularMembers` reads (RCA `docs/reviews/rca-troopweight-phantom-wounded-2026-06-07.md` §2). The table GC-evicts a party's entry with the party, so it can't grow unbounded (there was never any 25%-at-2000-entries trim — that was doc drift).
- **PartyBase-only patching:** Patches target `PartyBase.NumberOfAllMembers` / `NumberOfRegularMembers` only, NOT `TroopRoster.TotalManCount` / `TotalHealthyCount`. TroopRoster getters fire for every roster in the game (prisoners, garrisons, temp rosters); patching them caused IndexOutOfRange on partially-initialized rosters during game loading.
- **Thread-safety:** the display cache (`TroopWeightService._healthCache`) and the count caches (`WeightedCountCache`) both use `ConditionalWeakTable`, which is internally synchronized — safe even though the getters can be touched off the main campaign-tick thread (the earlier unlocked `Dictionary` was not).

## Changelog

- 2026-07-11 — **Count → limit rework (raw counts everywhere).** Relocated the "elite tax" from weighting the member count to deflating the party-size limit: `TaomPartySizeModel` now subtracts `ceil(weighted)−raw` from the limit (`ApplyPartySizeWeightPenalty` / pure `ComputeSizePenalty`, clamped ≥1), and the two count-getter patches + 5 weighted-display hooks + `WeightedCountCache` + `[CountFlicker]` diagnostic were deleted (~26 files). Every troop count now reads raw (map/party-screen/battle agree); the displayed limit shrinks with heavy troops; the recruit cap is preserved exactly. Shed-on-upgrade adapted to the deflated frame. Ripples: `SpecialResources` reward scaling preserved via explicit weighted-count call; `SettlementFood` garrison correction self-neutralizes (net food unchanged). New string `{=taom_troop_weight_size}` (needs `/localize`).
- 2026-07-11 — Count-cache collision fix + flicker diagnostic (SUPERSEDED same day by the rework above, which deleted both). Had replaced the `GetHashCode()`-keyed `Dictionary` in the count-getter hooks with a reference-keyed `WeightedCountCache` (closing the cross-party contamination flagged in the 2026-06-07 RCA §2) and added the `[CountFlicker]` diagnostic that PROVED the campaign-map "200↔20" flicker is the vanilla army-sum (raw `NumberOfHealthyMembers`), NOT the weighting.
- 2026-06-16 — Shed-on-upgrade: `Patch17_TroopWeight` postfix on `UpgradeReadyTroops` makes AI lords respect the weight budget by trimming the cheapest bodies via the pure `ITroopWeightService.PlanShed` planner; also fixed unweighted UI counts (clan-screen party list + main-party "Land Troop Capacity" row).
- 2026-06-07 — Phantom-wounded display fix across four UI surfaces (main/any-party health tooltips, encounter menu item, party nameplate text), rewriting battle-ready/wounded from a weighted split so the surplus no longer shows as fake wounds.
- 2026-05-14 — Phase 9c: disabled the `cave_troll` weight-4.0 entry in-place (WIP troll content). Phase 9b: added `TroopWeightHooksTests.cs` (10 tests) for the four `IOn*` hook implementations.
- 2026-03-26 — Initial feature: data-driven `troop_weights.xml` (~80 troops), `Patch17_TroopWeight` PartyBase getter postfixes + 2 UI patches, `EnableTroopWeight` MCM toggle; PartyBase-only patching after TroopRoster-level patches caused IndexOutOfRange freezes.

## GitHub Issues

- **Feature:** #41 — [feat: Troop Weight System — Elite unit party capacity](https://github.com/haterade22/TAOM/issues/41) — Closed
- **Bug fix:** #45 — [fix: TroopWeight crashes and freezes from TroopRoster-level patches](https://github.com/haterade22/TAOM/issues/45) — Closed
- **Feature:** #282 — [feat: AI lords respect troop weight on auto-upgrade (shed-on-upgrade) + fix unweighted UI counts](https://github.com/haterade22/TAOM/issues/282) — Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/auto-resolve-diagnostics.md](./auto-resolve-diagnostics.md)
- [docs/features/troop-skill-balance.md](./troop-skill-balance.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)

<!-- backlinks-end -->
