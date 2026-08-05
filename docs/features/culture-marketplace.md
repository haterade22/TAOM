# Culture-Aware Town Marketplace

## Overview

Each day, every town in TAOM is seeded with a small rotation of items pulled from a culture-specific pool keyed to the town's current owner. Walk into Minas Tirith → see Gondor swan-cavalry helms and Ithilien gear; capture it as Mordor → the next day the rotating stock starts showing morgul and morannon items. The same mechanic applies to all 16+ LOTRLOME cultures without per-item authoring.

> **Cross-feature:** this feature keys on `OwnerClan.Culture` (current owner, changes the instant a fief is captured), NOT `Settlement.Culture`. [CultureConversion](culture-conversion.md) only flips `Settlement.Culture` after a hold period — so a freshly-captured fief stocks the new owner's goods immediately while its troops/loyalty still reflect the original culture until conversion completes. This goods-vs-troops lag is intended. See culture-conversion.md → "Cross-feature interactions."

## Why This Exists

LOTRLOME_Armory ships ~6,155 culture-tagged items across 17 LOTR factions (Gondor, Mordor, Rohan, Erebor, Isengard, Rivendell, Mirkwood, Rhun, Harad, Dunland, Gundabad, Dol Guldur, Arnor, Iron Hills, Mercenary, Thenn, Troll). Without this feature none of them appear in trade.

- **Vanilla behavior:** `ItemObject.Culture` is silently ignored by Bannerlord's marketplace. Town inventory is restocked daily by `VillageGoodProductionCampaignBehavior.TickGoodProduction`, which only pushes items from `VillageType.Productions` (food, hides, raw materials). Equipment in vanilla markets is incidental — it arrives via caravan transfers and defeated-lord sells.
- **TAOM requirement:** LOTRLOME's lore-correct catalog needs to surface during normal trade, biased toward the owning culture so a Gondor town feels like Gondor and an Easterling town feels like Rhun.
- **Without this feature:** Markets are dominated by vanilla Calradian items regardless of culture. Players can't gear up locally; lore-correct equipment is reachable only by killing lords for their gear.

## Architecture

### Design Challenge

1. Vanilla never reads `item.Culture` at market time, so the fix has to come from outside the existing production pipeline.
2. The item pool is huge (~6,155 items across 9–10 distinct Bannerlord culture IDs). Authoring per-culture allowlists by hand would be untenable.
3. ~50% of LOTRLOME shields are missing the `<Item culture="...">` attribute, so attribute-based grouping alone leaves gaps.
4. Markets need rotation — a one-time seed leaks out as players and NPCs buy, leaving towns bare mid-campaign.
5. Conquest must shift market identity immediately. If Mordor takes Minas Tirith the player should see Mordor goods on the very next daily tick.

### Solution Approach

Pure CampaignBehavior layer — no Harmony patch needed, because `Settlement.ItemRoster` is a public mutable property and `CampaignEvents.DailyTickSettlementEvent` is a public extension point. The feature consists of:

- **Auto-derived item pool**: On first daily tick (or `OnGameLoaded`/`OnNewGameCreated`), scan every `ItemObject` in `MBObjectManager` and group by `Culture.StringId`. Items missing the culture attribute fall through to an ID-prefix table (`sm_mordor_` → mordor, `wm_isengard_` → isengard, etc.) so shields aren't excluded.
- **Optional XML overrides**: `culture_marketplace_config.xml` lets authors blacklist specific item IDs (keep Anduril quest-only) or boost weights (make certain Minas Tirith helms more common in Gondor markets). The provider validates every weight with `FiniteFloatValidator` and reverts NaN / Infinity / negative / >1000 values to 1.0 with a warning, per the project config-validation rule.
- **Daily injection with cap**: For each town on `DailyTickSettlementEvent`, read the owner culture, draw K=6 weighted-random items from the pool, and add via `Settlement.ItemRoster.AddToCounts`. A per-town distinct-item cap (`PerTownTotalRosterCap` = 200 since 2026-05-20; counts distinct roster entries, not total quantity) prevents unbounded growth — vanilla's price-driven trade flow handles depletion organically.
- **Dynamic ownership**: Culture is read fresh from `town.OwnerClan?.Culture?.StringId` on every tick. Conquest immediately shifts the pool used for the next injection.

### Component Diagram

```
culture_marketplace_config.xml (optional)
        │
  CultureMarketplaceConfigProvider (FiniteFloatValidator-guarded)
        │
        ▼
  CultureItemPoolService ◄── IItemPoolAdapter (MBObjectManager scan)
        │  builds culture → CultureItemPool dict once
        ▼
CultureMarketplaceInjectionService (weighted draw + per-town cap)
        │
        ▼
CultureMarketplaceBehavior (DailyTickSettlementEvent + OnNewGameCreatedPartialFollowUp)
        │  per-tick order:
        │   ① EnsureGuaranteedStock     — bypass cap
        │   ② FilterForeignCultureItems — capped removal
        │   ③ weighted-random injection — capped at PerTownTotalRosterCap
        ▼
  ITownRosterAdapter → Settlement.ItemRoster.AddToCounts / GetItemCount / RemoveItem / EnumerateRoster
```

## Configuration

### Config File: `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml`

Optional. If absent or empty, the engine auto-derives all pools from `MBObjectManager`. The file is read once on first lookup; lifetime is `Reuse.Singleton` so edits require a full Bannerlord restart — not a save reload.

| Element | Attribute | Type | Description |
|---------|-----------|------|-------------|
| `<Culture>` | `id` | string | Bannerlord culture StringId (e.g., `gondor`, `mordor`, `aserai`). |
| `<Blacklist><Item>` | `id` | string | Item StringId to exclude from this culture's pool. |
| `<Boost><Item>` | `id` | string | Item StringId whose draw weight should change from the default 1.0. |
| `<Boost><Item>` | `weight` | float | Draw weight in `[0, 1000]`. NaN / Infinity / negative / above max → revert to 1.0 with warning. |
| `<Routing><Item>` | `id` | string | Item StringId to route across multiple culture pools. The item IGNORES its `culture=` attribute and ID-prefix fallback and appears ONLY in the listed cultures' pools. |
| `<Routing><Item>` | `cultures` | string | Comma-separated culture IDs (e.g., `isengard,mordor,gundabad,dolguldur`). Whitespace around commas is trimmed. Aliases are normalized (e.g., `rohan` → `vlandia`). |
| `<Routing><Item>` | `min_stock` | int | Optional. 0–100. Guaranteed-floor stock per listed culture's town markets; daily tick tops up if the count falls below the floor. **Bypasses `PerTownTotalRosterCap`** — lore-essential items always available. Bad values (negative, non-integer, over ceiling) revert to 0 with warning. Default 0 (no floor). |

### Tunables (in code: `MarketplaceTuning.Default`)

| Field | Default | Description |
|-------|--------:|-------------|
| `ItemsPerTownPerDay` | 6 | Number of weighted-random items injected per town on each `DailyTickSettlementEvent`. |
| `PerTownTotalRosterCap` | 200 | Maximum distinct items in the town's full roster at which injection stops for the day. Vanilla `DistributeInitialItemsToTowns` runs 25 village-production passes per town, so towns commonly start at 30-80 distinct items — the cap is set high enough to leave 120+ headroom for our K=6 daily draws. Headroom (cap − current roster count) is the effective draw limit; the field bounds unbounded growth from edge cases. |
| `MaxFilterRemovalsPerTick` | 6 | Maximum foreign-culture items the daily filter pass removes per town per tick. Mirrors `ItemsPerTownPerDay` so injection and filtering cancel in steady state. The new-game initial-seed sweep IGNORES this cap (one-time cleanup of vanilla seeding). |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs` | Thin `CampaignBehaviorBase` — wires `DailyTickSettlementEvent` / `DailyTickEvent` (digest flush) / `OnGameLoaded` / `OnNewGameCreated` / `OnNewGameCreatedPartialFollowUp`. Daily tick runs three passes in order: guaranteed-stock top-up (cap-bypassing) → cross-culture filter (capped) → weighted-random injection. The follow-up event runs an uncapped initial filter sweep to clean vanilla's `DistributeInitialItemsToTowns` seed. |
| `Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs` | Loads optional XML overrides, validates weights via `FiniteFloatValidator`. |
| `Main/Features/CultureMarketplace/CultureItemPoolService.cs` | Builds the culture → `CultureItemPool` dict from `IItemPoolAdapter`, applies blacklist + weight boosts, falls through to ID prefix when attribute is missing. |
| `Main/Features/CultureMarketplace/CultureMarketplaceInjectionService.cs` | Weighted-random draw with per-town headroom enforcement. |
| `Main/Features/CultureMarketplace/CultureMarketplaceIoC.cs` | DryIoc registrations (all `Reuse.Singleton`). |
| `Main/Features/CultureMarketplace/Domain/*.cs` | `MarketplaceTuning` (+ `MaxFilterRemovalsPerTick`), `CultureItemPool`, `ItemPoolEntry`, `ItemPoolItem`, `MarketplaceConfigOverride`, `RoutedItem` (item-id + cultures + min_stock), `MarketplaceDailyDigest` (accumulates the staggered per-settlement passes, renders one line per in-game day). |
| `Main/Adapters/IItemPoolAdapter.cs` + `ItemPoolAdapter.cs` | Wraps `MBObjectManager.GetObjectTypeList<ItemObject>()`; ID-prefix table for culture fallback. |
| `Main/Adapters/ITownRosterAdapter.cs` + `TownRosterAdapter.cs` | Wraps `Settlement.OwnerClan.Culture` + `Settlement.ItemRoster` operations per ADR-007. Exposes `AddItem`, `GetItemCount`, `RemoveItem` (via `AddToCounts(-N)`), `EnumerateRoster` (returning `RosterItemSnapshot` DTOs that keep `ItemObject` out of the service layer). |
| `Main/Adapters/RosterItemSnapshot.cs` | TAOM-owned DTO carrying `ItemId + CultureStringId + Count` for one roster entry. |
| `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml` | Optional XML override (ships empty). |

## Dependencies

- `IPathService` (Core/Infrastructure) — locates `ModuleDataPath` for the XML override.
- `IModLogger` (Core/Logging) — structured logging.
- `FiniteFloatValidator` (Core/Validation) — NaN/Infinity guard for the XML weights.
- `MBObjectManager` (TaleWorlds) — wrapped by `IItemPoolAdapter`; the source of every loaded `ItemObject`.
- `Settlement.ItemRoster` (TaleWorlds) — wrapped by `ITownRosterAdapter`; receives injected items via `AddToCounts(EquipmentElement, int)` (modifier-preserving overload per `.claude/rules/adapters.md`).

## Tests

- `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceConfigProviderTests.cs` — 18 tests: missing file, malformed XML, blacklist/boost happy paths, NaN/Infinity/negative/over-max weight rejection, missing weight attribute, culture-without-id skip, idempotent re-read, **`<Routing>` parsing happy path (4 wargs)**, **`<Routing>` missing id/cultures skip + warn**, **`<Routing>` whitespace-trimming**, **`<Routing>` empty cultures string skip**.
- `TAOM.Tests/Features/CultureMarketplace/CultureItemPoolServiceTests.cs` — 17 tests: attribute grouping, prefix fallback, no-culture-signal exclusion, blacklist application, weight boost, idempotent build, unknown-culture null return, pre-build invocation throws, attribute-vs-prefix precedence, Rohan alias normalization (Codex C2), case-insensitive alias, **routed item appears in all listed cultures**, **routed item does NOT appear in attribute culture if not in routing list**, **routing overrides attribute**, **routing honors blacklist**, **routing culture alias normalized**.
- `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceInjectionServiceTests.cs` — 10 tests: null/unknown culture, at-cap + near-cap clamping, typical draw count, picks belong to pool, weighted-bias holds across 2000 trials (0.70 ≤ ratio ≤ 0.95 for a 10:1:1 split), empty pool, zero-total-weight pool, null RNG throws.
- `TAOM.Tests/Features/CultureMarketplace/ItemPoolAdapterPrefixTests.cs` — 7 tests covering Mirkwood crafted-weapon prefix (Codex C3), Harad crafted-weapon prefix (Codex C3), regression on Gondor/Mordor/Rohan/Dunland prefixes, null/empty/unknown defenses.

- `TAOM.Tests/Features/CultureMarketplace/CultureItemPoolServiceClassifierTests.cs` — 6 tests covering the extracted `ClassifyEffectiveCulture` pure function (attribute wins, prefix fallback, alias normalization, case-insensitivity, null/empty defenses).
- `TAOM.Tests/Features/CultureMarketplace/GetRoutedItemsForCultureTests.cs` — 6 tests covering the routing-by-culture lookup (4 wargs in each of the 4 evil cultures, none in gondor/vlandia, alias normalization on input).
- `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorGuaranteedStockTests.cs` — 8 tests covering the `EnsureGuaranteedStock` pass via reflection: top-up to min_stock, no-op when at/above floor, partial-stock delta, multi-item independence, min_stock=0 skip, no routed items → no-op, AddItem-failure handling.
- `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorFilterTests.cs` — 9 tests covering the `FilterForeignCultureItems` pass via reflection: foreign LOTRLOME item removed, vanilla universal kept, same-culture kept, routed item kept (warg in mordor town), removal cap honored, zero cap = no-op, empty roster = no-op, mixed roster discrimination, RemoveItem-failure handling.

- `TAOM.Tests/Features/CultureMarketplace/MarketplaceDailyDigestTests.cs` — 15 tests covering the daily roll-up: quiet day returns null, an all-no-op day returns null, foreign-removals-only still earns a line, totals sum across towns, active-over-touched town counts, top-3 ordering + id tie-break + cap, top list omitted when no town was active, picks-vs-injections divergence surfaced/suppressed, counters reset on flush and days do not leak, same town recorded twice accumulates, null/empty settlement id still counts toward totals.

Total: 105 tests across 9 classes, all green (`dotnet test --filter "FullyQualifiedName~CultureMarketplace|FullyQualifiedName~ItemPoolAdapter"`).

## Cross-Culture Item Routing

Some items legitimately belong to a faction GROUP rather than a single culture — Warg mounts (`warg_brown`, `warg_dark`, `warg_albino`, `warg_saddle`) are tagged `Culture.isengard` upstream but lore-correctly should appear in every "evil" culture's markets (Isengard, Mordor, Gundabad, Dol Guldur).

The `<Routing>` section of [`culture_marketplace_config.xml`](../../Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml) handles this:

```xml
<Routing>
  <Item id="warg_brown"  cultures="isengard,mordor,gundabad,dolguldur" />
  <Item id="warg_dark"   cultures="isengard,mordor,gundabad,dolguldur" />
  <Item id="warg_albino" cultures="isengard,mordor,gundabad,dolguldur" />
  <Item id="warg_saddle" cultures="isengard,mordor,gundabad,dolguldur" />
</Routing>
```

Listed items IGNORE their `culture=` attribute and ID-prefix fallback. They appear ONLY in the listed cultures' pools. Per-culture blacklists still apply (a routed item can be blacklisted from one of its routed cultures). Culture aliases (e.g., `rohan` → `vlandia`) are normalized so the routing target list can use either form.

Currently routed: the 4 warg items above. To add more cross-culture items, append `<Item>` entries and restart Bannerlord.

## Guaranteed Stock (min_stock)

Weighted-random injection isn't enough for items the player MUST be able to find — with ~200 items in a culture's pool and K=6 daily draws, each specific item has a ~3% per-day chance of appearing in a given town. After in-game testing showed that wargs were missing from Orthanc (Isengard's capital), the `min_stock` attribute was added:

```xml
<Routing>
  <Item id="warg_brown"  cultures="isengard,mordor,gundabad,dolguldur" min_stock="1" />
  <Item id="warg_dark"   cultures="isengard,mordor,gundabad,dolguldur" min_stock="1" />
  <Item id="warg_albino" cultures="isengard,mordor,gundabad,dolguldur" min_stock="1" />
  <Item id="warg_saddle" cultures="isengard,mordor,gundabad,dolguldur" min_stock="1" />
</Routing>
```

Every daily settlement tick, [`CultureMarketplaceBehavior.EnsureGuaranteedStock`](../../Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs) checks the town's current count of each routed item whose `Cultures` list includes the town's owner culture. If the count is below `min_stock`, it tops up the difference via `ITownRosterAdapter.AddItem`. **This bypasses `PerTownTotalRosterCap`** — lore-essential items must always be available, even in towns that hit the cap from other vanilla / TAOM flows.

The player can still buy guaranteed items normally; the next daily tick restocks them. Valid `min_stock` range is 0–100; out-of-range, non-integer, or NaN-equivalent values revert to 0 with a warning per the project's [`csharp-architecture.md` "Config Providers MUST Validate"](../../.claude/rules/csharp-architecture.md) rule.

## Cross-Culture Filter

Vanilla's `VillageGoodProductionCampaignBehavior.DistributeInitialItemsToTowns` seeds each town's `ItemRoster` with ~25 village-production passes at `OnNewGameCreatedPartialFollowUpEvent(i=1)`. Vanilla has no culture awareness, so LOTRLOME-authored items tagged with one culture can leak into towns of unrelated cultures (e.g., `[Gondor] Light Horse Armour — Pinnath Gelin` appearing in Orthanc). Subsequent vanilla flows (caravans, lord sells, workshop output) can keep adding cross-cultural items at a slower rate.

[`CultureMarketplaceBehavior.FilterForeignCultureItems`](../../Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs) snapshots the roster (`ITownRosterAdapter.EnumerateRoster`), computes each item's effective culture via the shared [`ICultureItemPoolService.ClassifyEffectiveCulture`](../../Main/Features/CultureMarketplace/ICultureItemPoolService.cs) (attribute → prefix → alias chain — same logic the pool builder uses), and removes items whose effective culture is non-empty AND ≠ the town owner's culture AND NOT in the routing list for this culture.

Two safeguards:
- **Routed items are preserved.** A warg in a Mordor town has effective culture `isengard` but is in the routing list for `mordor` → kept. Without this protection, the filter would treat the warg as foreign and remove it.
- **Vanilla universals are preserved.** Items with no `Culture` attribute AND no recognized ID-prefix (food, trade goods, base vanilla armour) are left alone. The filter ONLY targets items that have positively been classified into a culture.

Bounded removal:
- Daily ticks cap removal at `MaxFilterRemovalsPerTick` (default 6) to bound the visible per-tick change.
- The `OnNewGameCreatedPartialFollowUp(i=2)` initial sweep ignores the cap to clean the entire vanilla initial-seed pollution in one pass before the player ever sees the markets.

**Known limitation:** the filter classifier uses `item.Culture.StringId` (attribute) but does NOT recompute the ID-prefix fallback from `RosterItemSnapshot.ItemId`. LOTRLOME items lacking a culture attribute (e.g., the no-attribute Mirkwood crafted weapons covered by `ItemPoolAdapter.PrefixMap`) are treated as universals from the filter's perspective and won't be removed even in non-Mirkwood towns. The user-reported bug (`[Gondor]` / `[Rohan]` items in Orthanc) was about attribute-cultured items, which the filter handles correctly.

## Culture ID Normalization

Some LOTRLOME items declare `culture="Culture.<id>"` with IDs that don't match a valid TAOM culture — most notably the Rohan horse harnesses in `LOTRAOM_horses.xml` which use `Culture.rohan` (Rohan towns actually use `vlandia` per the TAOM culture cheatsheet in CLAUDE.md). [`CultureItemPoolService`](../../Main/Features/CultureMarketplace/CultureItemPoolService.cs) has a `CultureAliases` dictionary that normalizes invalid IDs into valid game cultures during pool grouping. Currently aliases:

| Invalid `Culture.X` attribute | Normalized to |
|---|---|
| `rohan` | `vlandia` |

If a future LOTRLOME update ships another invalid alias, add one entry to `CultureAliases`. The map uses `StringComparer.OrdinalIgnoreCase` so case variants (Rohan, ROHAN) all resolve.

## How to add a new culture's items

If LOTRLOME ships items for a new culture in the future, **no code changes are needed** as long as the items have either:

1. an `<Item culture="Culture.<id>">` attribute pointing at a valid Bannerlord culture StringId, OR
2. a recognizable item-ID prefix already in `ItemPoolAdapter.PrefixMap`.

If the new items use a new prefix that doesn't map to an existing culture, add one row to `ItemPoolAdapter.PrefixMap` (`Main/Adapters/ItemPoolAdapter.cs`) and rebuild. The `culture_marketplace_config.xml` is only for blacklists / weight tuning, not for declaring cultures.

## How to keep a specific item out of markets

Add it to the culture's blacklist in `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml`:

```xml
<CultureMarketplaceConfig>
  <Culture id="gondor">
    <Blacklist>
      <Item id="anduril" />
    </Blacklist>
  </Culture>
</CultureMarketplaceConfig>
```

Restart Bannerlord (the config is `Reuse.Singleton` and cached for the process lifetime). The item will never be drawn from the Gondor pool, though existing roster entries from prior ticks remain until traded/sold normally.

## Performance

- `IItemPoolAdapter` caches its scan result on first call. ~6,155 items × one `ItemObject` allocation each → trivial at startup, zero per-tick cost.
- `CultureItemPoolService` builds the per-culture dict once (`if (_pools != null) return;` guard).
- Per-tick work is one dict lookup + K=6 weighted-random draws over a single culture's list (~100–1,200 entries depending on culture). Linear scan; no allocations per tick beyond the result list.
- No `SyncData` — injected items live in vanilla `Settlement.ItemRoster` which the engine already persists. Save-load is unaffected.

## Changelog

- 2026-08-03 — **Rolled the per-town line up into one daily digest** (`MarketplaceDailyDigest`, flushed on `DailyTickEvent`). The 2026-07-27 gate was correct and still not enough: the surviving line ran at a sustained ~30/min with no ceiling — 1,687 lines, 36 % of a 37-minute session log — and the file's job is crash forensics from user machines, where an unbounded steady-state stream buries the evidence. One line per in-game day now carries the active/touched town counts, the injected/guaranteed/foreign totals, and the top three towns by activity. Two things the per-town line could not give you survive the roll-up: the foreign-strip total across *every* town (the old gate excluded `removed` from the per-line decision, so it only showed on lines that survived for another reason), and a picks-vs-injections divergence, which previously meant diffing `picks=` against `+N injected` by eye across the whole session. Accumulation is bounded by "since the last `DailyTickEvent`", so the staggered `DailyTickSettlementEvent` needs no alignment assumption.
- 2026-07-27 — **Per-town daily log re-gated to injection only** (`added > 0 || topUp > 0`). The gate added on 2026-07-04 also accepted `removed > 0`, which is true on ~99% of ticks: foreign-item strip is steady-state housekeeping, not an event — vanilla restocks cross-cultural goods daily and the filter strips them again. That made the gate inert; the line was 95.2% of a 47,365-line session log, and 83.2% of those lines had nothing injected. The foreign count still prints on every surviving line. Measured 45,080 → 7,568 lines/session. Removals per town per day are flat (~3.6, steady across a 4-hour session) and roster counts plateau, so this is equilibrium — the suppressed lines were not masking a runaway. Lesson: `docs/reviews/lessons/build-tooling-workflow.md` "A log-volume gate verified against the log that motivated it is not verified".
- 2026-06-17 — Fixed `TownRosterAdapter.RemoveItem` underflow (6,949 logged errors/session): removal was sized from the first stack but applied to a modifier-less `EquipmentElement`, driving a different stack negative; rewrote to remove per modifier-preserving stack clamped to its amount.
- 2026-05-21 — Extracted `ICultureMarketplaceMaintenanceService` (behavior shrank below ADR-002 limit), added a one-shot `_initialSweepDone` flag for the initial filter sweep, and made `TownRosterAdapter.GetItemCount` sum across modifier-split stacks. Deep-review fix-ups (#207 follow-up).
- 2026-05-21 — Added guaranteed warg stock (`min_stock` on `<Routing><Item>`, cap-bypassing daily top-up) and a cross-culture filter pass that removes foreign-culture LOTRLOME items (capped daily, uncapped one-time new-game sweep); shared `ClassifyEffectiveCulture` classifier (#207 follow-up).
- 2026-05-20 — Deduplicated routed cultures after alias normalization so `mordor,mordor` / `rohan,vlandia` no longer silently double an item's draw weight (#207).
- 2026-05-20 — Added the `<Routing>` mechanism so cross-culture items (the 4 warg items) appear in all listed cultures' pools, plus per-culture pool-size diagnostic logging (#207).
- 2026-05-20 — Post-Codex fixes: renamed cap to `PerTownTotalRosterCap` (60 → 200), added the `rohan` → `vlandia` culture alias, added Mirkwood/Harad prefix-fallback rows, and added a 3-attempt pool-build failure latch (#207).
- 2026-05-20 — Initial feature: `CultureMarketplaceBehavior` injects K=6 weighted-random culture-appropriate items per town per day, auto-deriving pools from `MBObjectManager` with ID-prefix fallback and dynamic owner-culture binding (#207).

## GitHub Issue

- **Issue:** [#207 — feat(marketplace): culture-aware item injection for town markets](https://github.com/haterade22/TAOM/issues/207)
- **Status:** Open (closes with completion-workflow Phase 4)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-conversion.md](./culture-conversion.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
