# Codex Self-Review — CultureMarketplace fixes (2026-05-20)

You already reviewed CultureMarketplace once today (your previous report at `docs/reviews/codex-adversarial-culturemarketplace-2026-05-20.md` — 0 CRITICAL, 0 HIGH, 2 MEDIUM, 2 LOW). All 4 findings (C1–C4) were fixed in-session. After your review, in-game testing surfaced two more findings (C5 + C6) — both have been addressed.

This pass reviews **only the fixes**, not the rest of the feature. Hunt for bugs in OUR FIX code: regressions, edge cases the fix missed, new bugs introduced. Pretend the original feature was correct and only the fix diffs are suspicious.

## What was fixed (six changes to review)

| # | Sev (prior) | What was changed | Fix file:line |
|---|------|------------------|---|
| C1 | MED | Renamed `PerTownInjectedCap` → `PerTownTotalRosterCap`. Raised default 60 → 200. | [`Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs`](../../Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs) |
| C2 | MED | Added `CultureAliases` dict mapping `rohan` → `vlandia` (case-insensitive). Normalization applied between attribute-lookup and grouping in `BuildPools`. | [`Main/Features/CultureMarketplace/CultureItemPoolService.cs`](../../Main/Features/CultureMarketplace/CultureItemPoolService.cs) — class field + `ApplyCultureAlias` helper |
| C3 | LOW | Added 2 rows to PrefixMap: `("mirkwood_", "mirkwood")` and `("wm_harad_", "aserai")`. | [`Main/Adapters/ItemPoolAdapter.cs`](../../Main/Adapters/ItemPoolAdapter.cs) |
| C4 | LOW | Added 3-attempt failure latch in `EnsurePoolBuilt`. After 3 failed `BuildPools` attempts, `_gaveUp=true` and the feature is inert for the session. Tick handler short-circuits on `_gaveUp`. | [`Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs`](../../Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs) |
| C5 | NEW (user finding) | New `<Routing>` XML section + `GetItemRouting()` provider method + `BuildPools` routing branch. Items in routing IGNORE attribute/prefix and appear ONLY in listed cultures' pools. 4 warg items seeded. | [`Main/Features/CultureMarketplace/ICultureMarketplaceConfigProvider.cs`](../../Main/Features/CultureMarketplace/ICultureMarketplaceConfigProvider.cs), [`CultureMarketplaceConfigProvider.cs`](../../Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs), [`CultureItemPoolService.cs`](../../Main/Features/CultureMarketplace/CultureItemPoolService.cs), [`culture_marketplace_config.xml`](../../Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml) |
| C6 | NEW (user finding) | Added per-culture diagnostic logging at boot. One info-level line per culture with item count and first 4 sample IDs. | [`Main/Features/CultureMarketplace/CultureItemPoolService.cs`](../../Main/Features/CultureMarketplace/CultureItemPoolService.cs) — end of `BuildPools` |

## TAOM ID CHEATSHEET (unchanged from prior review)
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (vanilla): vlandia=Rohan, empire=Dunland, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid culture ID (Rohan uses vlandia) — but C2's alias map fixes that for items tagged Culture.rohan.

## Known Suspects in the FIXES — CONFIRM or DISPUTE

### Suspect 1: routing + alias interaction (C5 × C2)

`BuildPools` checks routing first, then falls through to attribute → prefix-fallback → alias normalization. But the routing branch also calls `ApplyCultureAlias(cId)` on each routed culture ID. Concrete scenario: a hypothetical entry `<Item id="x" cultures="rohan,mordor" />` — does `x` end up in `vlandia` AND `mordor` pools, or just `mordor`? Read the code:

```csharp
if (routing.TryGetValue(item.ItemId, out var routedCultures))
{
    foreach (var cId in routedCultures)
    {
        var routedTarget = ApplyCultureAlias(cId);
        if (!AddToGroup(grouped, overrides, routedTarget, item.ItemId))
            continue;
    }
    routedItems++;
    continue;
}
```

VERIFY: does the alias normalization handle the routing case correctly? CONFIRMED or DISPUTED.

### Suspect 2: routing iteration order vs blacklist (C5 × pre-existing override)

`AddToGroup` checks the blacklist using the TARGET culture id (after alias). A routed item that fails the blacklist in one culture is silently skipped (no warning, no log). Is that desirable, or should we log? Also: if the routing list contains the SAME culture twice (`<Item cultures="mordor,mordor"/>`), does the item get added twice to the mordor pool? VERIFY.

### Suspect 3: C4 failure latch counter increment

```csharp
catch (Exception ex)
{
    _failedAttempts++;
    _logger.LogError($"... attempt {_failedAttempts}/{MaxPoolBuildAttempts}: ...");
    if (_failedAttempts >= MaxPoolBuildAttempts)
    {
        _gaveUp = true;
        ...
    }
}
```

Concrete: if `BuildPools` succeeds on attempt 2, does the counter (now at 1) ever get reset? Trace what happens if BuildPools throws on attempt 1, succeeds on attempt 2. Is the counter cleanup correct? Or is it OK because once `_poolBuilt=true` we never enter EnsurePoolBuilt again?

### Suspect 4: diagnostic logging cost (C6)

The new per-culture log loop calls `string.Join(", ", kvp.Value.Items.Take(4).Select(e => e.ItemId))` once per culture. For a typical run with ~14 cultures this is ~14 allocations, all at startup, never per tick. But verify: is this in a code path that could fire more than once? E.g., does EnsurePoolBuilt's retry-after-failure path call BuildPools again, which would log the per-culture diagnostic again?

### Suspect 5: routing-via-config-only invariant (C5)

Items listed in `<Routing>` are routed regardless of their `culture=` attribute. But the iteration order in `BuildPools` is: check routing first, otherwise attribute, otherwise prefix. What if a routed item has a `culture=` attribute that matches one of its routed cultures? E.g., `warg_brown` is tagged `Culture.isengard` AND routed to `["isengard","mordor","gundabad","dolguldur"]`. Does it land in isengard pool ONCE (via routing) or TWICE (once via routing, once via attribute)? Trace the code.

### Suspect 6: C1 cap rename leftover references

The rename `PerTownInjectedCap` → `PerTownTotalRosterCap` touched the field declaration, the constructor parameter, and uses in `CultureMarketplaceInjectionService`. Grep for any stale `PerTownInjectedCap` reference anywhere in the codebase that wasn't updated.

### Suspect 7: case-sensitivity asymmetry (C2 × routing)

`CultureAliases` uses `StringComparer.OrdinalIgnoreCase`. The routing dict uses `StringComparer.Ordinal`. The pools dict uses `StringComparer.OrdinalIgnoreCase`. Is this consistent? Walk through a scenario where the user writes `<Item id="WARG_brown" cultures="isengard" />` — does the routing lookup `routing.TryGetValue("warg_brown", ...)` return that entry? (Probably not — ordinal comparison.) Is that the intended behavior?

## Files to review

NEW / MODIFIED in this fix round (focus here):
- Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs
- Main/Features/CultureMarketplace/CultureItemPoolService.cs
- Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs
- Main/Features/CultureMarketplace/ICultureMarketplaceConfigProvider.cs
- Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs
- Main/Features/CultureMarketplace/CultureMarketplaceInjectionService.cs
- Main/Adapters/ItemPoolAdapter.cs
- Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml
- TAOM.Tests/Features/CultureMarketplace/CultureItemPoolServiceTests.cs
- TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceConfigProviderTests.cs
- TAOM.Tests/Features/CultureMarketplace/ItemPoolAdapterPrefixTests.cs

UNCHANGED but useful context:
- docs/features/culture-marketplace.md
- docs/reviews/codex-adversarial-culturemarketplace-2026-05-20.md (prior review)
- docs/reviews/rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md (RCA with Phase 2 + 2.5 addenda)

## REQUIRED SECTIONS

### 1. SUSPECT VERDICTS
CONFIRMED / DISPUTED for each of S1–S7 with reasoning. Cite file:line.

### 2. NEW FINDINGS
Any bugs in the fix code that the 7 Known Suspects don't cover. CRITICAL / HIGH / MEDIUM / LOW.

### 3. REGRESSION CHECK
Reread the prior C1–C4 fixes and confirm they still correctly fix the original bugs WITHOUT introducing new behavior the fix wasn't supposed to introduce. In particular:
- C1: confirm `currentRosterCount >= _tuning.PerTownTotalRosterCap` is the only place the cap is checked
- C2: confirm `CultureAliases` is applied EXACTLY once per item path (not twice through prefix + routing)
- C3: confirm new prefix rows don't shadow existing rows (order matters — first match wins)
- C4: confirm the failure latch doesn't suppress a legitimate failure log on attempt 3 of 3

### 4. CULTURE ID CROSS-REFERENCE
For C5's seeded warg entries: confirm `isengard,mordor,gundabad,dolguldur` are all valid TAOM culture IDs (per cheatsheet). No invalid `dol_guldur` underscore variant or `dolguldor` typo.

## QUALITY GATES (must satisfy all 4)
1. CONFIRMED/DISPUTED verdict on every numbered suspect S1–S7.
2. At least 2 concrete code-block citations with file:line.
3. Regression check section explicitly addresses C1–C4 carryover.
4. Final verdict line: CLEAN / ISSUES FOUND.

## Output to
`docs/reviews/codex-adversarial-culturemarketplace-fixes-2026-05-20.md`
