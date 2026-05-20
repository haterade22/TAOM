# Codex Adversarial Review - CultureMarketplace Fixes - 2026-05-20

Scope: second-pass review of the CultureMarketplace fix code only, focused on C1-C6 fix regressions, C5 routing edge cases, and C6 diagnostic logging.

Review basis: TAOM source in this workspace, the prior CultureMarketplace review, the fix prompt, installed `Alliance.Wargs` item XML, and installed Bannerlord v1.3.15 source material generated through `tools/taom-src.ps1`. I did not use `E:\Decompiled_Bannerlord`.

Tests: attempted `dotnet test TAOM.Tests --filter "FullyQualifiedName~CultureMarketplace"`. The run was blocked by sandbox/SDK filesystem access while MSBuild tried to read `C:\Users\mikew\AppData\Local\Microsoft SDKs`. No test results are claimed.

## 1. SUSPECT VERDICTS

### S1 - Routing + Alias Interaction: DISPUTED

The routing branch applies `ApplyCultureAlias` to every routed target before grouping. In the concrete `cultures="rohan,mordor"` scenario, `rohan` becomes `vlandia` and `mordor` stays `mordor`; the item lands in both pools. The `continue` at line 72 also correctly prevents later attribute/prefix processing.

`Main/Features/CultureMarketplace/CultureItemPoolService.cs:63`

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

### S2 - Routing Iteration Order vs Blacklist: CONFIRMED

`AddToGroup` uses the post-alias target culture for blacklist lookup, so a routed item blacklisted for `mordor` is skipped only for `mordor` and can still be added to other routed cultures. That part is correct and intentionally covered by `BuildPools_RoutedItem_HonorsBlacklist`.

The edge-case bug is duplicate route targets. The provider stores route targets as a plain `List<string>`, and the pool service appends once per entry after alias normalization. Therefore both `cultures="mordor,mordor"` and `cultures="rohan,vlandia"` add the same item twice to a single canonical pool, doubling that item's draw weight without a warning.

`Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs:155`

```csharp
var cultureIds = new List<string>();
foreach (var raw in culturesRaw.Split(','))
{
    var trimmed = raw.Trim();
    if (!string.IsNullOrEmpty(trimmed))
        cultureIds.Add(trimmed);
}
```

`Main/Features/CultureMarketplace/CultureItemPoolService.cs:133`

```csharp
if (!grouped.TryGetValue(cultureId, out var list))
{
    list = new List<ItemPoolEntry>();
    grouped[cultureId] = list;
}
list.Add(new ItemPoolEntry(itemId, weight));
```

[LOW] Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs:155 - Config validation - Routed cultures are not de-duplicated after alias normalization, so duplicate targets or alias collisions like `rohan,vlandia` add duplicate `ItemPoolEntry` rows and silently boost the item in one culture pool - Fix: canonicalize route targets, de-duplicate per item with an ordinal-ignore-case set after aliasing, and log ignored duplicates.

### S3 - C4 Failure Latch Counter Increment: DISPUTED

If `BuildPools` fails on attempt 1 and succeeds on attempt 2, `_failedAttempts` remains `1`, but `_poolBuilt` becomes `true` at `CultureMarketplaceBehavior.cs:62`. Every later `EnsurePoolBuilt` call returns at line 58. The stale counter is not observable unless the code grows a reset/rebuild path later.

### S4 - Diagnostic Logging Cost: DISPUTED

The diagnostic loop is at the end of `BuildPools`, and `BuildPools` returns immediately once `_pools != null`. The per-culture `OrderByDescending`, `Take(4)`, `Select`, and `string.Join` allocations are startup/build-time only, not per tick. Retry-after-failure can call `BuildPools` again only while `_pools` is still null; failures before successful pool construction do not emit the per-culture diagnostics.

### S5 - Routing-via-Config-Only Invariant: DISPUTED

Routing is checked before attribute/prefix grouping, and the routing branch always `continue`s. A `warg_brown` item tagged `Culture.isengard` and routed to `isengard,mordor,gundabad,dolguldur` lands once in each listed culture, including Isengard once. It does not fall through and get added a second time via its attribute. The only duplicate route is the S2 config-edge case.

### S6 - C1 Cap Rename Leftover References: DISPUTED

Production source has no stale `PerTownInjectedCap` references. The remaining hits are historical review/prompt/RCA documents. The cap guard is `currentRosterCount >= _tuning.PerTownTotalRosterCap` at `CultureMarketplaceInjectionService.cs:32`; the only other production use is the headroom calculation at line 45.

### S7 - Case-Sensitivity Asymmetry: DISPUTED

`WARG_brown` in config does not match runtime item id `warg_brown`, because routing uses `StringComparer.Ordinal` at `CultureMarketplaceConfigProvider.cs:47`. This is consistent with item IDs being exact/case-sensitive in Bannerlord object lookup and with existing blacklist/boost dictionaries also using ordinal item-id keys.

Installed v1.3.15 source, generated through `pwsh tools/taom-src.ps1 path TaleWorlds.ObjectSystem.MBObjectManager`:

`C:\Users\mikew\.taom-src\v1.3.15\TaleWorlds.ObjectSystem.MBObjectManager.cs:75`

```csharp
private readonly Dictionary<string, T> _registeredObjects;

_registeredObjects = new Dictionary<string, T>();

internal T GetObject(string objId)
{
    _registeredObjects.TryGetValue(objId, out var value);
```

## 2. NEW FINDINGS

Additional findings beyond S1-S7: none.

Confirmed known-suspect finding affecting the verdict:

[LOW] Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs:155 - Config validation - Routed cultures are not de-duplicated after alias normalization, so duplicate targets or alias collisions like `rohan,vlandia` add duplicate `ItemPoolEntry` rows and silently boost the item in one culture pool - Fix: canonicalize route targets, de-duplicate per item with an ordinal-ignore-case set after aliasing, and log ignored duplicates.

## 3. REGRESSION CHECK

C1: Clean. `PerTownTotalRosterCap` exists in `MarketplaceTuning.cs`, defaults to 200, and production use is limited to the cap guard and headroom calculation in `CultureMarketplaceInjectionService.cs`. This preserves the intended total-roster-cap semantic and removes the misleading injected-cap name from source.

C2: Clean except for the S2 duplicate-route edge. Non-routed items choose attribute or prefix, then apply `ApplyCultureAlias` once at `CultureItemPoolService.cs:85`. Routed items apply alias once per route target at line 67 and then `continue`. There is no attribute + routing double-add path.

C3: Clean. `mirkwood_` was inserted after `mkwd_`, and those prefixes do not overlap. `wm_harad_` was inserted after `haradrim`, and those prefixes do not overlap either. `ResolveByPrefix` still uses first-match wins at `ItemPoolAdapter.cs:108-111`, but the new rows do not shadow existing rows.

C4: Clean. Attempt 3 still logs the specific failure at `CultureMarketplaceBehavior.cs:68` before `_gaveUp` is set and the inert-session log is emitted at line 72. `OnDailyTickSettlement` short-circuits on `_gaveUp` at line 80. A successful attempt after an earlier failure does not reset `_failedAttempts`, but `_poolBuilt=true` makes that counter inert for the rest of the behavior lifetime.

## 4. CULTURE ID CROSS-REFERENCE

Seeded C5 routing entries in `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml:30-33` use:

| Culture ID | Validity |
| --- | --- |
| `isengard` | Valid TAOM custom culture; appears in TAOM XML, e.g. `cc_body_properties.xml:126` and Isengard clan rows. |
| `mordor` | Valid TAOM custom culture; appears in TAOM XML, e.g. `taom_careers.xml:83`. |
| `gundabad` | Valid TAOM custom culture; appears in TAOM XML, e.g. `cc_body_properties.xml:131`. |
| `dolguldur` | Valid TAOM custom culture; appears in TAOM XML, e.g. `cc_body_properties.xml:136`. |

No seeded route uses invalid `dol_guldur` or `dolguldor`.

The four seeded warg IDs also exist in installed `Alliance.Wargs/ModuleData/Items/LOTR/lotr_warg.xml`: `warg_brown`, `warg_dark`, `warg_albino`, and `warg_saddle`; all are tagged upstream with `culture="Culture.isengard"`, matching the reason for cross-culture routing.

## QUALITY GATES

1. S1-S7 each have a CONFIRMED/DISPUTED verdict.
2. Code-block citations are included for routing, route parsing/grouping, and installed v1.3.15 `MBObjectManager`.
3. C1-C4 regression checks are explicitly covered.
4. Final verdict line is present below.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 1
VERDICT: ISSUES FOUND
