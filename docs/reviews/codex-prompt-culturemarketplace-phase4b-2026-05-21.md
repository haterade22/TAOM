# Codex Adversarial Review — CultureMarketplace Phase 4b fixes (2026-05-21)

You previously reviewed CultureMarketplace twice (2026-05-20) — original adversarial pass, then self-review of those fixes. Both passes are at:
- `docs/reviews/codex-adversarial-culturemarketplace-2026-05-20.md`
- `docs/reviews/codex-adversarial-culturemarketplace-fixes-2026-05-20.md`

Today (2026-05-21) the user reported additional in-game findings; we added `min_stock` (guaranteed-stock) + `<Routing>` extensions + a cross-culture filter pass. Internal `/deep-review` then found 2 HIGH + 1 MED + 1 LOW in THAT work; all fixed. Now I'm asking you to review the LATEST fix layer.

## What changed in the latest fixes (4 items to audit)

### D1 (HIGH fixed) — One-shot sweep flag in `OnNewGameCreatedPartialFollowUp`

`Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs`:

```csharp
private bool _initialSweepDone;
...
private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int i)
{
    if (_initialSweepDone) return;
    if (i < 2) return;
    EnsurePoolBuilt();
    if (!_poolBuilt) return;

    try
    {
        var totalRemoved = 0;
        foreach (var settlement in Campaign.Current?.Settlements ?? throw ...)
        {
            if (settlement == null || !settlement.IsTown) continue;
            var cultureId = _townAdapter.GetCurrentCultureId(settlement);
            if (string.IsNullOrEmpty(cultureId)) continue;
            totalRemoved += _maintenance.FilterForeignCultureItems(settlement, cultureId, removalCap: int.MaxValue);
        }
        _initialSweepDone = true;
        if (totalRemoved > 0)
            _logger.LogInfo(...);
    }
    catch (Exception ex)
    {
        _logger.LogError(...);
        _initialSweepDone = true;   // don't retry on exception either
    }
}
```

Verify: is the flag correctly placed? Edge cases — what if `EnsurePoolBuilt` returns with `!_poolBuilt` for the first invocation (i=2, 3) and then succeeds at i=5? The current code never sets `_initialSweepDone` in the `!_poolBuilt` branch, so subsequent invocations re-try. Is that the right behavior, or should we set the flag after the first attempt regardless?

### D2 (HIGH fixed) — Maintenance service extraction

New `Main/Features/CultureMarketplace/ICultureMarketplaceMaintenanceService.cs` + `CultureMarketplaceMaintenanceService.cs`. Service takes Settlement as a "transit" parameter (forwards to adapter, never reads off it). Registered in `CultureMarketplaceIoC.cs`; SubModule.cs ctor passes 6 deps now.

Verify:
- Is the ADR-007 spirit honored — the service holds Settlement as a parameter but never accesses Settlement.* properties? Re-read CultureMarketplaceMaintenanceService.cs and confirm.
- Does the behavior delegation pattern match TAOM precedents (SpecialResources, SettlementGuards)? Or is there a closer existing pattern that would have been cleaner (e.g., having the adapter expose a `WithSettlement(...)` opaque token)?
- Are the new tests (`CultureMarketplaceMaintenanceServiceGuaranteedStockTests`, `CultureMarketplaceMaintenanceServiceFilterTests`) covering the same surface as the reflection-private tests they replaced? Same test method names, same edge cases?

### D3 (MED fixed) — `GetItemCount` sums all stacks

`Main/Adapters/TownRosterAdapter.cs`:

```csharp
public int GetItemCount(Settlement settlement, string itemId)
{
    ...
    var roster = settlement.ItemRoster;
    var total = 0;
    for (var i = 0; i < roster.Count; i++)
    {
        if (roster.GetItemAtIndex(i) == itemObject)
            total += roster.GetElementNumber(i);
    }
    return total;
}
```

Verify:
- Is `roster.GetItemAtIndex(i) == itemObject` a safe comparison? `ItemObject` is the canonical singleton from `MBObjectManager`, so reference equality should work. Or should this use `MBObjectBase.Equals` / StringId comparison?
- Performance: O(roster.Count) per call. Called once per routed item per daily tick per town. With ~4 routed items × ~80 roster items × ~200 towns × daily = ~64K iterations per game day. Acceptable?
- Are there other callers of `GetItemCount` outside `EnsureGuaranteedStock`? Trace and confirm no regression.

### D4 (LOW fixed) — Routing dict comparer

`Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs`:

```csharp
_routing = new Dictionary<string, RoutedItem>(StringComparer.OrdinalIgnoreCase);
```

Verify: are there any lookups that DEPEND on the case-sensitive behavior we just removed? Grep for `_routing.TryGetValue(`, `routing.TryGetValue(` in all CultureMarketplace + tests. If any existing test deliberately checks that an uppercase key MISSES the dict, this change would break it.

## Known Suspects

### S1: `_initialSweepDone` set on exception (D1)

Setting the flag in the catch block means a transient exception (e.g., a single settlement's iteration throws) prevents retry on later i values. Is this acceptable, or should we let later i values retry? The reasoning was: one-shot is the contract; the exception is bug-level, not transient. CONFIRM or DISPUTE.

### S2: D2's "transit" Settlement parameter purity (ADR-007)

The new `CultureMarketplaceMaintenanceService` accepts `Settlement` in its public API and forwards it to `ITownRosterAdapter` without reading any property off it. Re-read the service code and confirm there are ZERO `.X` accesses on the Settlement parameter (only `_adapter.Method(settlement, ...)` calls). If any property access leaked in, that's an ADR-007 violation.

### S3: D3 multi-stack semantics for AddItem (consistency)

`GetItemCount` now sums across stacks. But `AddItem(settlement, itemId, count)` creates a `new EquipmentElement(itemObject)` with NO modifier, so it always adds to (or creates) the no-modifier stack. If a town has "Sharp warg_brown ×0" + "no-mod warg_brown ×0", and we top up to 1, we get a single no-mod warg_brown stack. Subsequent GetItemCount returns 1. Consistent. ✓

But what if the town has "Sharp warg_brown ×0" (empty stack — is vanilla supposed to remove empty stacks? confirm by decompile) + "no-mod warg_brown ×3". GetItemCount returns 3 (sums non-empty). 3 ≥ MinStock=1 → no top-up. ✓

Is there ANY scenario where GetItemCount returns N but AddItem(N+1)'s actual visible-to-player count diverges? CONFIRM or DISPUTE.

### S4: D4 case-insensitivity downstream

The routing dict is now OrdinalIgnoreCase keyed. The pool builder uses `routing.TryGetValue(item.ItemId, out var routed)`. If a modder writes `<Item id="WARG_BROWN" ...>` and the runtime ItemObject.StringId is lowercase `warg_brown`, the lookup now succeeds (good). But the routing's RoutedItem.ItemId stores the AUTHOR's case ("WARG_BROWN") — downstream `EnsureGuaranteedStock` calls `_townAdapter.GetItemCount(settlement, entry.ItemId)` with "WARG_BROWN", which then calls `MBObjectManager.Instance?.GetObject<ItemObject>(itemId)` — verify this is case-insensitive in vanilla v1.3.15.

## REQUIRED SECTIONS

1. CONFIRMED / DISPUTED verdict on S1, S2, S3, S4.
2. Any NEW findings not covered by S1-S4.
3. Regression-check on D1-D4 with file:line.
4. Final verdict: CLEAN / ISSUES FOUND.

## QUALITY GATES

1. At least 2 code-block citations.
2. CONFIRMED/DISPUTED for every suspect.
3. Regression-check section.
4. Final verdict line.

## Output

Write to: `docs/reviews/codex-adversarial-culturemarketplace-phase4b-2026-05-21.md`
