# Culture-Aware Town Marketplace

## Overview

Each day, every town in TAOM is seeded with a small rotation of items pulled from a culture-specific pool keyed to the town's current owner. Walk into Minas Tirith → see Gondor swan-cavalry helms and Ithilien gear; capture it as Mordor → the next day the rotating stock starts showing morgul and morannon items. The same mechanic applies to all 16+ LOTRLOME cultures without per-item authoring.

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
- **Daily injection with cap**: For each town on `DailyTickSettlementEvent`, read the owner culture, draw K=6 weighted-random items from the pool, and add via `Settlement.ItemRoster.AddToCounts`. A per-town distinct-item cap (60) prevents unbounded growth — vanilla's price-driven trade flow handles depletion organically.
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
CultureMarketplaceBehavior (DailyTickSettlementEvent)
        │
        ▼
  ITownRosterAdapter → Settlement.ItemRoster.AddToCounts
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

### Tunables (in code: `MarketplaceTuning.Default`)

| Field | Default | Description |
|-------|--------:|-------------|
| `ItemsPerTownPerDay` | 6 | Number of weighted-random items injected per town on each `DailyTickSettlementEvent`. |
| `PerTownTotalRosterCap` | 200 | Maximum distinct items in the town's full roster at which injection stops for the day. Vanilla `DistributeInitialItemsToTowns` runs 25 village-production passes per town, so towns commonly start at 30-80 distinct items — the cap is set high enough to leave 120+ headroom for our K=6 daily draws. Headroom (cap − current roster count) is the effective draw limit; the field bounds unbounded growth from edge cases. |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs` | Thin `CampaignBehaviorBase` — wires `DailyTickSettlementEvent` / `OnGameLoaded` / `OnNewGameCreated`, delegates to services. |
| `Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs` | Loads optional XML overrides, validates weights via `FiniteFloatValidator`. |
| `Main/Features/CultureMarketplace/CultureItemPoolService.cs` | Builds the culture → `CultureItemPool` dict from `IItemPoolAdapter`, applies blacklist + weight boosts, falls through to ID prefix when attribute is missing. |
| `Main/Features/CultureMarketplace/CultureMarketplaceInjectionService.cs` | Weighted-random draw with per-town headroom enforcement. |
| `Main/Features/CultureMarketplace/CultureMarketplaceIoC.cs` | DryIoc registrations (all `Reuse.Singleton`). |
| `Main/Features/CultureMarketplace/Domain/*.cs` | `MarketplaceTuning`, `CultureItemPool`, `ItemPoolEntry`, `ItemPoolItem`, `MarketplaceConfigOverride`. |
| `Main/Adapters/IItemPoolAdapter.cs` + `ItemPoolAdapter.cs` | Wraps `MBObjectManager.GetObjectTypeList<ItemObject>()`; ID-prefix table for culture fallback. |
| `Main/Adapters/ITownRosterAdapter.cs` + `TownRosterAdapter.cs` | Wraps `Settlement.OwnerClan.Culture` and `Settlement.ItemRoster.AddToCounts` per ADR-007. |
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

Total: 52 tests, all green (`dotnet test --filter "FullyQualifiedName~CultureMarketplace|FullyQualifiedName~ItemPoolAdapter"`).

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

## GitHub Issue

- **Issue:** [#207 — feat(marketplace): culture-aware item injection for town markets](https://github.com/haterade22/TAOM/issues/207)
- **Status:** Open (closes with completion-workflow Phase 4)
