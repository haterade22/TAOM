# Codex Adversarial Review - CultureMarketplace - 2026-05-20

Scope: independent review of the freshly-built `CultureMarketplace` feature. I read the prepared prompt, the feature doc, the prior RCA for already-fixed dead-code findings, the config stub, and the `CLAUDE.md` CultureMarketplace rows. I did not re-flag `CountPerInjection`, `EnumerateTowns`, or `TownInjectionContext`.

Review basis: TAOM source in this workspace, installed Bannerlord v1.3.15 DLLs, and installed `LOTRLOME_Armory` XML under `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData`.

## 1. VANILLA CODE

### VillageGoodProductionCampaignBehavior

Source: installed v1.3.15 `TaleWorlds.CampaignSystem.dll`, decompiled through `tools/taom-src.ps1`.

Vanilla registers a daily settlement tick and a new-game follow-up:

```csharp
public override void RegisterEvents()
{
    CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DailyTickSettlement);
    CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUp);
}

private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int i)
{
    if (i == 1)
    {
        DistributeInitialItemsToTowns();
        CalculateInitialAccumulatedTaxes();
        foreach (Village item in Village.All)
        {
            float num = MBRandom.RandomFloat * 5f;
            for (int j = 0; (float)j < num; j++)
            {
                TickProductions(item.Settlement);
            }
        }
    }
}
```

Initial town stocking is not a zero-roster path. Vanilla runs 25 initial production passes and writes into town rosters:

```csharp
private void DistributeInitialItemsToTowns()
{
    int num = 25;
    foreach (Town allTown in Campaign.Current.AllTowns)
    {
        float num2 = 0f;
        Settlement settlement = allTown.Settlement;
        foreach (Village allVillage in Campaign.Current.AllVillages)
        {
            if (allVillage.Bound == settlement)
            {
                num2 += 1f;
                TickGoodProduction(allVillage, initialProductionForTowns: true);
                TickFoodProduction(allVillage, initialProductionForTowns: true);
            }
        }
    }
}

private void TickGoodProduction(Village village, bool initialProductionForTowns)
{
    foreach (var production in village.VillageType.Productions)
    {
        ItemObject item = production.Item1;
        int num = MBRandom.RoundRandomized(
            Campaign.Current.Models.VillageProductionCalculatorModel
                .CalculateDailyProductionAmount(village, production.Item1).ResultNumber);
        if (num > 0)
        {
            if (!initialProductionForTowns)
            {
                village.Owner.ItemRoster.AddToCounts(item, num);
                CampaignEventDispatcher.Instance.OnItemProduced(item, village.Owner.Settlement, num);
            }
            else if (village.TradeBound != null)
            {
                village.TradeBound.ItemRoster.AddToCounts(item, num);
            }
        }
    }
}
```

Conclusion for S1: static decompilation cannot produce one universal fresh Gondor town `ItemRoster.Count`; it depends on bound villages and production categories. It does prove the count is not expected to be zero after vanilla initialization.

### TownMerchantsCampaignBehavior

Source: installed `Modules\SandBox\bin\Win64_Shipping_Client\SandBox.dll`, decompiled with `ilspycmd`.

The prompt asked for `OnSessionLaunched` if it exists. It does not exist on `TownMerchantsCampaignBehavior` in this installed v1.3.15 build. The behavior only registers location-character spawning:

```csharp
public override void RegisterEvents()
{
    CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(
        (object)this,
        (Action<Dictionary<string, int>>)LocationCharactersAreReadyToSpawn);
}

public override void SyncData(IDataStore dataStore)
{
}
```

Conclusion: the initial roster stocking relevant to the cap audit comes from `VillageGoodProductionCampaignBehavior`, not `TownMerchantsCampaignBehavior`.

### DailyTickSettlement dispatch path

Source: installed v1.3.15 `TaleWorlds.CampaignSystem.dll`, decompiled with `ilspycmd`.

`CampaignEvents` invokes the event synchronously:

```csharp
public static IMbEvent<Settlement> DailyTickSettlementEvent => Instance._dailyTickSettlementEvent;

public override void DailyTickSettlement(Settlement settlement)
{
    Instance._dailyTickSettlementEvent.Invoke(settlement);
}
```

`CampaignEventDispatcher` forwards to receivers in a plain loop:

```csharp
public override void DailyTickSettlement(Settlement settlement)
{
    CampaignEventReceiver[] eventReceivers = _eventReceivers;
    for (int i = 0; i < eventReceivers.Length; i++)
    {
        eventReceivers[i].DailyTickSettlement(settlement);
    }
}
```

`CampaignPeriodicEventManager` has a generic ticker that can run in parallel when `_doParallel` is true, but the daily settlement ticker is initialized with `doParallel: false`:

```csharp
internal void Initialize(MBReadOnlyList<T> list, Action<T> action, bool doParallel)
{
    _list = list;
    _action = action;
    _doParallel = doParallel;
}

// parallel branch exists only when _doParallel is true
TWParallel.For(0, _currentFrameToTickListFlattened.Count, delegate(int startInclusive, int endExclusive)
{
    for (int i = startInclusive; i < endExclusive; i++)
    {
        _action(_currentFrameToTickListFlattened[i]);
    }
}, 1);

_dailyTickSettlementTicker.Initialize(list, delegate(Settlement x)
{
    CampaignEventDispatcher.Instance.DailyTickSettlement(x);
}, doParallel: false);
```

Conclusion for S7: `DailyTickSettlement` listeners are sequential in this installed v1.3.15 path. The shared `Random` in `CultureMarketplaceBehavior` is not a confirmed threading bug.

### ItemRoster and Town market data

Source: installed v1.3.15 `TaleWorlds.CampaignSystem.dll`, decompiled with `ilspycmd`.

`ItemRoster.Count` is the distinct roster-element count:

```csharp
public int Count => _count;
```

`AddToCounts(ItemObject, int)` preserves vanilla behavior by routing through `EquipmentElement`; TAOM uses the same lower-level shape in `TownRosterAdapter.cs:48`.

```csharp
public int AddToCounts(ItemObject item, int number)
{
    if (number == 0)
    {
        return -1;
    }
    return AddToCounts(new EquipmentElement(item), number);
}

public int AddToCounts(EquipmentElement rosterElement, int number)
{
    if (number == 0)
    {
        return -1;
    }
    int num = FindIndexOfElement(rosterElement);
    if (num < 0)
    {
        ...
    }
}
```

Save-load invalid-item cleanup exists:

```csharp
public static void CalculateCachedStatsOnLoad()
{
    foreach (ItemRoster item in InstanceListForLoadGame)
    {
        ReplaceInvalidItemsWithTrash(item);
        RemoveZeroCountsFromRoster(item);
        item.CalculateCachedStats();
    }
    InstanceListForLoadGame.Clear();
}

private static void ReplaceInvalidItemsWithTrash(ItemRoster itemRoster)
{
    if (itemRoster._data == null)
    {
        return;
    }
    for (int num = itemRoster._data.Length - 1; num >= 0; num--)
    {
        ItemObject item = itemRoster._data[num].EquipmentElement.Item;
        if (item != null && !item.IsReady)
        {
            ItemObject item2 = itemRoster._data[num].EquipmentElement.Item;
            itemRoster.AddToCounts(DefaultItems.Trash, itemRoster._data[num].Amount);
            ...
        }
    }
}
```

Town price state reacts to inventory changes:

```csharp
public TownMarketData MarketData => _marketData;

public override int GetItemPrice(ItemObject item, MobileParty tradingParty = null, bool isSelling = false)
{
    return MarketData.GetPrice(item, tradingParty, isSelling);
}

protected override void OnInventoryUpdated(ItemRosterElement item, int count)
{
    MarketData.OnTownInventoryUpdated(item, count);
}
```

Conclusion: adding to `Settlement.ItemRoster` is the correct vanilla surface for market inventory and price-state notification. Missing item IDs on save-load are self-healed by vanilla into trash rather than left as raw broken references.

### Culture reference loading

Source: installed v1.3.15 `TaleWorlds.Core.dll`, `TaleWorlds.CampaignSystem.dll`, and `TaleWorlds.ObjectSystem.dll`, decompiled with `ilspycmd`.

`ItemObject.Culture` is a `BasicCultureObject`:

```csharp
public BasicCultureObject Culture { get; private set; }

Culture = (BasicCultureObject)objectManager.ReadObjectReferenceFromXml(
    "culture",
    typeof(BasicCultureObject),
    node);
```

`CultureObject` is a sealed subclass:

```csharp
public sealed class CultureObject : BasicCultureObject
{
    ...
}
```

`MBObjectBase.StringId` is the stored ID, and XML `id` values are assigned directly:

```csharp
public string StringId { get; set; }

public virtual void Deserialize(MBObjectManager objectManager, XmlNode node)
{
    Initialize();
    StringId = node.Attributes["id"].Value;
}
```

`ReadObjectReferenceFromXml` strips the `Culture.` prefix and looks up the object by the second segment:

```csharp
public MBObjectBase ReadObjectReferenceFromXml(string attributeName, Type objectType, XmlNode node)
{
    if (node.Attributes[attributeName] == null)
    {
        return null;
    }
    string value = node.Attributes[attributeName].Value;
    string text = value.Split(".".ToCharArray())[0];
    if (text == value)
    {
        throw new MBInvalidReferenceException(value);
    }
    string text2 = value.Split(".".ToCharArray())[1];
    if (text == string.Empty || text2 == string.Empty)
    {
        throw new MBInvalidReferenceException(value);
    }
    return GetPresumedObject(text, text2);
}
```

Conclusion for S4: comparing `StringId` strings is the right cross-type comparison. A loaded `culture="Culture.gondor"` reference compares as `gondor`, not `Culture.gondor`.

## 2. DEEP ANALYSIS

### 2a. Cap-vs-Headroom Semantics (S1)

TAOM path:

```csharp
// CultureMarketplaceBehavior.cs:78-79
var rosterCount = _townAdapter.GetRosterDistinctItemCount(settlement);
var picks = _injection.SelectItems(cultureId, rosterCount, _rng);

// TownRosterAdapter.cs:28-31
public int GetRosterDistinctItemCount(Settlement settlement)
{
    var roster = settlement?.ItemRoster;
    return roster?.Count ?? 0;
}

// CultureMarketplaceInjectionService.cs:32-46
if (currentRosterCount >= _tuning.PerTownInjectedCap)
{
    return Array.Empty<string>();
}
var headroom = _tuning.PerTownInjectedCap - currentRosterCount;
var drawCount = Math.Min(_tuning.ItemsPerTownPerDay, headroom);
```

The cap is the whole `Settlement.ItemRoster.Count`, not a count of CultureMarketplace-injected items.

Concrete required walk-through with a Gondor town starting at 50 vanilla distinct items:

| Day | Start distinct roster count | Headroom to cap 60 | Draw count | End count if draws are distinct new IDs |
| --- | ---: | ---: | ---: | ---: |
| 1 | 50 | 10 | 6 | 56 |
| 2 | 56 | 4 | 4 | 60 |
| 3 | 60 | 0 | 0 | 60 |

So the player sees at most 10 CultureMarketplace distinct items in that town, then no further new Gondor items are injected until the total roster count drops below 60. If vanilla initialization or trade already puts the town at 60+ distinct entries, CultureMarketplace injects zero items from day one.

I could not derive a single static "typical Gondor count" from decompilation alone, because vanilla count depends on bound villages, production categories, randomized production rounding, and consumption entries. The vanilla code above proves the fresh count is not structurally 0. Given 25 initial production passes, a high double-digit distinct count is plausible in a large town.

Verdict: S1 CONFIRMED as an implementation semantic. Whether it is an intended total-roster bloat cap or an injected-item cap is ambiguous, but the field name `PerTownInjectedCap` and feature goal point toward injected-item semantics.

### 2b. WeightedDraw Distribution (S5)

TAOM path:

```csharp
// CultureMarketplaceInjectionService.cs:61-72
private static string WeightedDraw(CultureItemPool pool, Random rng)
{
    var roll = (float)(rng.NextDouble() * pool.TotalWeight);
    var cumulative = 0f;
    for (var i = 0; i < pool.Items.Count; i++)
    {
        cumulative += pool.Items[i].Weight;
        if (roll <= cumulative)
            return pool.Items[i].ItemId;
    }
    return pool.Items.Count > 0 ? pool.Items[pool.Items.Count - 1].ItemId : null;
}
```

For a 1211-entry Rhun pool with every weight equal to `1.0f`:

- `1211f` is exactly representable as IEEE-754 single precision. All integer sums up to 16,777,216 are exact.
- `Random.NextDouble()` returns values in `[0.0, 1.0)`, so `roll` is strictly less than `1211f` except for possible cast rounding up to an exact boundary.
- The final `cumulative` is exactly `1211f`.
- The fall-through fallback is not reachable for equal unit weights.
- Expected selection probability for each item is `1 / 1211 = 0.082576%`.

There is a minuscule boundary skew from using `<=` rather than `<`: a roll that lands exactly on an integer boundary is assigned to the earlier bucket. With `Random.NextDouble()`'s discrete output, this is negligible and does not create a last-item bias. For non-integer configured weights, `CultureItemPool.TotalWeight` and `WeightedDraw` use the same float sum order, so the final cumulative should match `TotalWeight` for an unmutated pool.

Verdict: S5 DISPUTED. The last-item fallback is safe for the 1211x unit-weight case. If desired, the smallest polish is to compute `roll` and `cumulative` as `double` and compare with `<`, but this is not a correctness finding.

### 2c. Lifecycle Holes (S2, S6)

New game start:

- `CultureMarketplaceBehavior.RegisterEvents` wires `OnNewGameCreatedEvent`, `OnGameLoadedEvent`, and `DailyTickSettlementEvent` at `Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs:35-39`.
- `OnNewGameCreated` immediately calls `EnsurePoolBuilt` at `CultureMarketplaceBehavior.cs:47`.
- `EnsurePoolBuilt` calls `_poolService.BuildPools()` and sets `_poolBuilt = true` at `CultureMarketplaceBehavior.cs:55-56`.
- If `BuildPools` throws, the catch logs but leaves `_poolBuilt` false at `CultureMarketplaceBehavior.cs:59-62`.
- Daily tick then retries because `OnDailyTickSettlement` calls `EnsurePoolBuilt` whenever `_poolBuilt` is false at `CultureMarketplaceBehavior.cs:68`.

Failure latch verdict: S2 CONFIRMED. A permanent failure repeats on every town daily tick forever. The adapter cache makes some retries cheap after the first scan, but the log spam and repeated failed build path remain.

Save-load:

- `SyncData` is intentionally empty at `CultureMarketplaceBehavior.cs:42-45`.
- Injected items live in vanilla `Settlement.ItemRoster`, and vanilla `ItemRoster.CalculateCachedStatsOnLoad` replaces non-ready item references with `DefaultItems.Trash`.
- `OnGameLoaded` rebuilds the in-memory pool at `CultureMarketplaceBehavior.cs:48`.

Save-load verdict: S6 DISPUTED as a corruption issue. Vanilla has an invalid-item cleanup path. The cap issue can still be affected after load because trash or replacement entries count toward the total roster cap, but that is the same whole-roster cap semantic from S1.

Settlement ownership change:

- `TownRosterAdapter.GetCurrentCultureId` reads `settlement?.OwnerClan?.Culture?.StringId` every daily tick at `Main/Adapters/TownRosterAdapter.cs:18-20`.
- No settlement-to-culture cache is used.

If Minas Tirith is captured by Mordor, the next daily tick reads the current owner clan culture and asks for the Mordor pool. This part is clean.

### 2d. Prefix Map Audit (S3)

The prompt text said there was no `rohan_` row, but the reviewed code does have one at `Main/Adapters/ItemPoolAdapter.cs:27`.

Prefix rows:

```csharp
// Main/Adapters/ItemPoolAdapter.cs:12-42
private static readonly (string Prefix, string CultureId)[] PrefixMap =
{
    ("sk_gd_",        "gondor"),
    ("sm_mordor_",    "mordor"),
    ("wm_isengard_",  "isengard"),
    ("sk_uruk_hai_",  "isengard"),
    ("sm_uruk_",      "isengard"),
    ("sk_dwarf_erebor_", "erebor"),
    ("mkwd_",         "mirkwood"),
    ("rivendell_",    "rivendell"),
    ("morannon_",     "mordor"),
    ("morgul_",       "mordor"),
    ("haradrim",      "aserai"),
    ("easterling",    "khuzait"),
    ("dunland_",      "empire"),
    ("rohan_",        "vlandia"),
    ("whiterun_",     "vlandia"),
    ("ithilien_",     "gondor"),
    ("gondor_",       "gondor"),
    ("faramir_",      "gondor"),
    ("imrahil_",      "gondor"),
    ("legolas_",      "mirkwood"),
    ("thranduil_",    "mirkwood"),
    ("theoden_",      "vlandia"),
    ("witchking_",    "mordor"),
    ("nazgul_",       "mordor"),
    ("sauron_",       "mordor"),
    ("anduril",       "gondor"),
    ("strider_",      "gondor"),
    ("cts_rohan_",    "vlandia"),
};
```

The service then uses attribute culture first and prefix fallback only for missing attributes:

```csharp
// Main/Features/CultureMarketplace/CultureItemPoolService.cs:48-55
var cultureId = !string.IsNullOrEmpty(item.CultureId)
    ? item.CultureId
    : item.PrefixCultureId;

if (string.IsNullOrEmpty(cultureId))
{
    unresolved++;
    continue;
}
```

Sample of real installed LOTRLOME IDs audited:

| Item ID | XML culture signal | Prefix result | Final service culture | Result |
| --- | --- | --- | --- | --- |
| `sk_gd_ano_gloves_a` | `gondor` | `gondor` | `gondor` | OK |
| `sk_gd_ano_bracer_inf_med_a` | `gondor` | `gondor` | `gondor` | OK |
| `sm_mordor_shield_mid_a` | `mordor` | `mordor` | `mordor` | OK |
| `wm_isengard_shield_a01` | `isengard` | `isengard` | `isengard` | OK |
| `sk_uruk_hai_bracer_elite_a1` | `isengard` | `isengard` | `isengard` | OK |
| `sm_uruk_sword_a` | `mordor` | `isengard` | `mordor` | OK today; prefix would be wrong if attribute missing |
| `sk_dwarf_erebor_arrow_a` | `erebor` | `erebor` | `erebor` | OK |
| `mkwd_inf3_vambraces` | `mirkwood` | `mirkwood` | `mirkwood` | OK |
| `mirkwood_sword_a01` | none | none | unresolved | Issue |
| `mirkwood_spear_a01` | none | none | unresolved | Issue |
| `mirkwood_spear_a02` | none | none | unresolved | Issue |
| `mirkwood_glaive_a01` | none | none | unresolved | Issue |
| `rivendell_gloves_gold` | `rivendell` | `rivendell` | `rivendell` | OK |
| `haradrim_gloves` | `aserai` | `aserai` | `aserai` | OK |
| `wm_harad_glaive_a01` | none | none | unresolved | Issue |
| `easterling_shield` | `aserai` | `khuzait` | `aserai` | OK today; prefix would be wrong if attribute missing |
| `dunland_caerdh_shield_elite_a` | `empire` | `empire` | `empire` | OK |
| `dunland_caerdh_short_spear_c` | none | `empire` | `empire` | OK |
| `rohan_horse_armor_scalemail` | `rohan` | `vlandia` | `rohan` if attribute resolves | Issue |
| `whiterun_bracers` | `vlandia` | `vlandia` | `vlandia` | OK |
| `ithilien_bracers` | `gondor` | `gondor` | `gondor` | OK |
| `gondor_swan_horse_armor_1` | `gondor` | `gondor` | `gondor` | OK |
| `faramir_bracers` | `gondor` | `gondor` | `gondor` | OK |
| `imrahil_gloves` | `gondor` | `gondor` | `gondor` | OK |
| `legolas_gloves` | `mirkwood` | `mirkwood` | `mirkwood` | OK |
| `thranduil_gloves` | `mirkwood` | `mirkwood` | `mirkwood` | OK |
| `theoden_sword` | `vlandia` | `vlandia` | `vlandia` | OK |
| `witchking_sword` | `mordor` | `mordor` | `mordor` | OK |
| `nazgul_sword` | `mordor` | `mordor` | `mordor` | OK |
| `sauron_vambraces` | `mordor` | `mordor` | `mordor` | OK |
| `anduril` | `gondor` | `gondor` | `gondor` | OK |
| `strider_sword` | `gondor` | `gondor` | `gondor` | OK |
| `cts_rohan_shield` | `vlandia` | `vlandia` | `vlandia` | OK |

Concrete installed XML evidence for unresolved no-culture items:

```xml
<!-- LOTRAOM_weapons.xml:3998-4002 -->
<CraftedItem
    id="mirkwood_sword_a01"
    name="{=aom_mirkwood_sword_a01_name}[Mirkwood] Sword I"
    crafting_template="OneHandedSword"
    is_merchandise="true">

<!-- LOTRAOM_weapons.xml:8956-8960 -->
<CraftedItem
    id="wm_harad_glaive_a01"
    name="{=aom_wm_harad_glaive_a01_name}[Harad] Glaive I"
    crafting_template="TwoHandedPolearm"
    is_merchandise="true">
```

Concrete installed XML evidence for invalid Rohan culture IDs:

```xml
<!-- LOTRAOM_horses.xml:231-237 -->
<Item
    id="rohan_horse_armor_scalemail"
    name="{=aom_rohan_horse_armor_scalemail_name}[Rohan] Horse Armour - Scalemail"
    mesh="lrd_horse_armour_10"
    culture="Culture.rohan"
    weight="25"
    is_merchandise="true"
```

Verdict: S3 CONFIRMED with a different shape than the prompt's premise. I did not find a current wrong route caused purely by fixed-order prefix overlap for attributed items, because attribute culture wins. I did find real fallback coverage gaps and a real invalid culture-alias hazard.

### 2e. Race / Threading Audit (S7)

The vanilla dispatch code above shows:

- `CampaignPeriodicEventManager` can run some tickers in parallel.
- `_dailyTickSettlementTicker` is explicitly initialized with `doParallel: false`.
- `CampaignEventDispatcher.DailyTickSettlement` loops receivers sequentially.
- `CampaignEvents.DailyTickSettlement` invokes the event synchronously.

Verdict: S7 DISPUTED. The shared `_rng` at `Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs:19` is not a confirmed race in v1.3.15 daily settlement dispatch. No fix is required for threading. If TaleWorlds changes the ticker to parallel in a future version, the smallest fix would be a lock around `_rng` or a thread-local `Random`.

## 3. CONFIG CROSS-REFERENCE

### PrefixMap output IDs

All 10 unique output culture IDs in `Main/Adapters/ItemPoolAdapter.cs:14-41` are valid under the prompt's TAOM ID cheatsheet:

| PrefixMap culture ID | Cheatsheet meaning | Verdict |
| --- | --- | --- |
| `gondor` | custom culture | OK |
| `mordor` | custom culture | OK |
| `isengard` | custom culture | OK |
| `erebor` | custom culture | OK |
| `mirkwood` | custom culture | OK |
| `rivendell` | custom culture | OK |
| `aserai` | vanilla/XSLT culture for Harad | OK |
| `khuzait` | vanilla/XSLT culture for Easterlings/Rhun | OK |
| `empire` | vanilla/XSLT culture for Dunland | OK |
| `vlandia` | vanilla/XSLT culture for Rohan | OK |

No PrefixMap output uses invalid `rohan` or invalid `dol_guldur`.

### LOTRLOME folder/culture expectations

Installed `LOTRLOME_items` parse summary, using `Item` and `CraftedItem` nodes:

| LOTRLOME area | Expected TAOM culture ID | Installed signal observed | Verdict |
| --- | --- | --- | --- |
| Gondor | `gondor` | `Culture.gondor`, plus `sk_gd_`, `gondor_`, `faramir_`, `imrahil_`, `anduril`, `strider_` | OK |
| Mordor | `mordor` | `Culture.mordor`, plus `sm_mordor_`, `morannon_`, `morgul_`, `witchking_`, `nazgul_`, `sauron_` | OK |
| Rohan | `vlandia` | Rohan folder uses `Culture.vlandia`; root horse harnesses use invalid `Culture.rohan` | Mismatch |
| Erebor / Iron Hills | `erebor` | `Culture.erebor`, plus `sk_dwarf_erebor_` | OK |
| Isengard | `isengard` | `Culture.isengard`, plus Isengard/Uruk prefixes | OK |
| Rivendell | `rivendell` | `Culture.rivendell`, plus `rivendell_` | OK |
| Mirkwood | `mirkwood` | Mostly `Culture.mirkwood`, but root crafted weapons `mirkwood_*` lack culture and miss PrefixMap | Gap |
| Rhun / Easterlings | `khuzait` | Rhun folder uses `Culture.khuzait`; root `easterling_*` includes some `Culture.aserai` shield entries | Mixed, attribute wins |
| Harad | `aserai` | Harad folder uses `Culture.aserai`; root `wm_harad_glaive_a01` lacks culture and misses PrefixMap | Gap |
| Dunland / Thenn | `empire` | `Culture.empire`, plus `dunland_` fallback | OK |
| Gundabad | `gundabad` | `Culture.gundabad` | OK by attribute; no PrefixMap fallback |
| Dol Guldur | `dolguldur` | `Culture.dolguldur` | OK by attribute; no invalid `dol_guldur` output |
| Arnor | `gondor` | `Culture.gondor` | OK |
| Mercenary | `gondor` in installed item data | `Culture.gondor` | OK |
| Troll | `mordor` | `Culture.mordor` | OK |
| Lothlorien root items | `lothlorien` | `Culture.lothlorien` | OK by attribute; no PrefixMap fallback |
| Umbar | `umbar` | no installed `LOTRLOME_items` folder found in this snapshot | Not applicable |

The most important mismatch is `Culture.rohan`: the cheatsheet explicitly says Rohan must use `vlandia`, but installed root horse harnesses use `Culture.rohan`.

## 4. FINDINGS OR OBSERVATIONS

### Findings

[MEDIUM] Main/Features/CultureMarketplace/CultureMarketplaceInjectionService.cs:32 — Cap Semantics — `PerTownInjectedCap` is enforced against the whole vanilla `Settlement.ItemRoster.Count`, because `CultureMarketplaceBehavior.cs:78` passes `TownRosterAdapter.GetRosterDistinctItemCount()` and `TownRosterAdapter.cs:28-31` returns `settlement.ItemRoster.Count`. Vanilla initial production writes non-TAOM items into town rosters before the feature's daily injections, so a town starting at 50 distinct vanilla entries gets at most 10 CultureMarketplace distinct entries before the cap blocks all future injections. If the town starts at 60+, the feature injects nothing. Smallest fix: either rename/document the setting as a total-roster cap and raise the default, or count only existing items from the current culture pool and cap that injected/pool subset.

[MEDIUM] Main/Features/CultureMarketplace/CultureItemPoolService.cs:48 — Culture ID Normalization — Attribute culture wins over prefix fallback, but installed LOTRLOME root horse harnesses use invalid `culture="Culture.rohan"` (`LOTRAOM_horses.xml:235`, also lines 254, 273, 292, 311, 330). The PrefixMap row at `ItemPoolAdapter.cs:27` would correctly map `rohan_` to `vlandia`, but it is ignored whenever `item.Culture?.StringId` is non-empty. Because `ReadObjectReferenceFromXml` strips `Culture.` and looks up `rohan`, a presumed or externally supplied `rohan` culture object sends these items to a `rohan` pool that Rohan towns (`vlandia`) never request. Smallest fix: normalize known aliases (`rohan` -> `vlandia`, and any future invalid aliases) before grouping, or only trust attribute culture if it is in an allow-list of valid TAOM culture IDs.

[LOW] Main/Adapters/ItemPoolAdapter.cs:20 — Prefix Fallback Coverage — Real installed no-culture Mirkwood and Harad crafted weapons are unresolved because the PrefixMap has `mkwd_` and `haradrim`, but not `mirkwood_` or `wm_harad_`. `CultureItemPoolService.cs:52-55` drops unresolved items, so `mirkwood_sword_a01`, `mirkwood_spear_a01`, `mirkwood_spear_a02`, `mirkwood_glaive_a01`, and `wm_harad_glaive_a01` never enter any marketplace pool despite being `is_merchandise="true"` in `LOTRAOM_weapons.xml:3998-4002`, `4023-4027`, `4041-4045`, `4057-4061`, and `8956-8960`. Smallest fix: add explicit `mirkwood_ -> mirkwood` and `wm_harad_ -> aserai` fallback rows and add tests using these real IDs.

[LOW] Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs:50 — Failure Latch — `EnsurePoolBuilt` catches every `BuildPools` exception, logs, and leaves `_poolBuilt` false at `CultureMarketplaceBehavior.cs:59-62`; `OnDailyTickSettlement` retries on every town tick while false at `CultureMarketplaceBehavior.cs:68-69`. A persistent config or adapter failure will spam logs forever across every town daily tick. Smallest fix: add a build-failed latch, attempt counter, or log-once/backoff path after permanent failures. Keep a manual reset only if live retry is genuinely needed.

### Known Suspect Verdicts

S1 CONFIRMED - The cap uses whole-roster distinct count, not CultureMarketplace-injected distinct count. Vanilla initial production can consume most or all headroom before CultureMarketplace runs.

S2 CONFIRMED - Build failure has no permanent failure latch and retries forever on daily town ticks.

S3 CONFIRMED - I did not find a current fixed-order prefix over-match causing attributed items to route wrong, but the prefix audit found real fallback coverage gaps and a real invalid `Culture.rohan` alias hazard. The prompt's claim that no `rohan_` row exists is disputed; the row exists at `ItemPoolAdapter.cs:27`.

S4 DISPUTED - The string equality path is correct for valid cultures. `ItemObject.Culture` loads a `BasicCultureObject`, `Clan.Culture` is a `CultureObject : BasicCultureObject`, and both compare by `MBObjectBase.StringId`. `Culture.gondor` is looked up as `gondor`, not as `Culture.gondor`.

S5 DISPUTED - With 1211 entries all weighted `1.0f`, float accumulation is exact and the last-item fallback is not a distribution-bias bug. Each item has probability `1 / 1211 = 0.082576%`, ignoring negligible exact-boundary behavior from `<=`.

S6 DISPUTED - No SyncData is acceptable for the injected roster entries because they live in vanilla `Settlement.ItemRoster`. On load, vanilla replaces non-ready item references with trash and recalculates cached stats. The remaining risk is the S1 total-roster cap counting replacement/trash entries.

S7 DISPUTED - Daily settlement tick dispatch is sequential in installed v1.3.15 (`doParallel: false`), so the shared `Random` is not a confirmed race.

S8 DISPUTED - I found no active TAOM quest XML or quest code reference that reserves `anduril`, `witchking_sword`, `theoden_sword`, `faramir_armor`, `nazgul_sword`, `sauron_*`, or `strider_sword`. Hits are equipment sets, NPC/lord loadouts, crafting templates, item definitions, and the commented example in `culture_marketplace_config.xml:13` (`anduril` as a sample blacklist). No quest-locking bug is confirmed.

### Observations

- `ItemPoolAdapter.ItemExists` is still present in `IItemPoolAdapter.cs:9` and `ItemPoolAdapter.cs:89-100`, but I did not classify it as a finding because the prior RCA scope was specifically the already-fixed dead scaffolding and this method may be intended for future config validation.
- `sm_uruk_` currently maps to `isengard`, while real installed `sm_uruk_sword_a` is a Mordor item by attribute (`LOTRAOM_weapons.xml:159-163`). This is safe today because attribute wins, but it is a good regression-test candidate for any future item that loses its culture attribute.
- `easterling` maps to `khuzait`, while installed `easterling_shield` is tagged `Culture.aserai` (`LOTRAOM_shields.xml:560-566`). Attribute wins today. If a future no-attribute root `easterling_*` item is really Harad-tagged, this row will route it to Rhun.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 2 | LOW: 2
VERDICT: ISSUES FOUND
