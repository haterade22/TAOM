# Feature Port Session: QuickActions (TransferbuttonMenu)

You are porting feature #5 of 7 from the external-developer drop at `Downloads/Features_fixed/TransferbuttonMenu/` into TAOM's `Main/Features/QuickActions/` (note: feature renamed from "TransferbuttonMenu" to "QuickActions" for clarity per the integration plan). The other 6 features are tracked separately. Don't touch them.

## Prerequisites — read before writing any code

1. **The integration plan**: `C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md` — section "5. TransferbuttonMenu (rename to 'QuickActions' in TAOM — clearer)" has the planned file layout.

2. **This prompt** — end to end.

3. **Pattern templates**:
   - [Main/Features/SiegeDismount/](../../Main/Features/SiegeDismount/) — singleton-service + thin boundary class pattern
   - [Main/Features/MixedFormations/](../../Main/Features/MixedFormations/) — Harmony Prefix-returning-false pattern
   - [docs/features/mixed-formations.md](../features/mixed-formations.md) — feature doc template
   - [docs/reviews/codex-adversarial-siegedismount-2026-05-06.md](../reviews/codex-adversarial-siegedismount-2026-05-06.md) — Codex output format

4. **The decompiled source you're porting**:
   `C:/Users/mikew/Downloads/Features_fixed/_decompiled/TransferbuttonMenu/TransferbuttonMenu.decompiled.cs`

   Read it end-to-end. Critical sections: `InventoryQuickActionsLogic` (the action logic — sell damaged, sell low-value, unequip all), `InventorySearchBehavior` (a CampaignBehavior that bi-directionally syncs `SPInventoryVM.IsSearchAvailable` to a per-save bool), `SellAllPatch` (Prefix on `SPInventoryVM.ExecuteSellAllItems` — returns `false` to skip vanilla, opens MultiSelectionInquiry instead), `TransferbuttonMenuSettings` (14 MCM settings).

## Goal in one sentence

Replace the inventory screen's "Sell All" button with a multi-action menu offering: sell damaged, sell low-value, unequip all, or vanilla sell-all — plus a persistent toggle for the inventory search box.

## Architecture — what to build

### Files to create

```
Main/Features/QuickActions/
├── IQuickActionsService.cs                ← SellAllDamaged(), SellAllLowValue(), UnequipAll(), GetMenuOptions()
├── QuickActionsService.cs                 ← port of InventoryQuickActionsLogic logic; uses adapters for everything
├── IQuickActionsSettingsProvider.cs
├── QuickActionsSettingsProvider.cs        ← wraps TaomSettings.Instance for 14 settings
├── Models/
│   ├── QuickActionType.cs                 ← enum (SellDamaged, SellLowValue, UnequipAll, OriginalSellAll)
│   ├── QuickActionResult.cs               ← struct: items affected count, gold gained, fail reason
│   └── DamagedQualityPreset.cs            ← enum (Pristine=0, Slight=-10%, Moderate=-20%, Heavy=-40%, Custom)
├── QuickActionsIoC.cs
└── Hooks/
    ├── InventorySearchCampaignBehavior.cs ← port InventorySearchBehavior; SyncData("TAOM_IsInventorySearchAvailable") — DIFFERENT key from original to avoid save-compat collision
    └── Patch34_SellAllItemsMenu.cs        ← Prefix on SPInventoryVM.ExecuteSellAllItems; returns false; opens MultiSelectionInquiry with 4 options

Main/Adapters/
├── IInventoryVMAdapter.cs                 ← NEW — load-bearing for feature 6 (EquipPresets); wraps SPInventoryVM reflection chain
├── InventoryVMAdapter.cs                  ← consolidates the 8-name fallback chain for the right-pane item list
├── IInventoryItemAdapter.cs               ← NEW — wraps SPItemVM (item value, equipped state, modifier check)
└── InventoryItemAdapter.cs

TAOM.Tests/Features/QuickActions/
├── QuickActionsServiceTests.cs            ← skip-guard exhaustion (every filter exclusion), threshold matrix, confirmation flow
├── InventorySearchCampaignBehaviorTests.cs
└── DamagedQualityPresetTests.cs           ← preset-to-threshold mapping
```

### Adapter usage — `IInventoryVMAdapter` is the most important deliverable

This adapter is **load-bearing for feature 6 (EquipPresets)**. Both features access `SPInventoryVM` via heavy reflection (the original modules each had separate fallback chains for the same probes). Consolidate ALL the reflection here.

```csharp
public interface IInventoryVMAdapter
{
    /// <summary>The currently active inventory VM, or null if no inventory screen is open.</summary>
    bool IsAvailable { get; }

    /// <summary>Items in the right pane (party inventory side). Returned as adapter wrappers.</summary>
    IReadOnlyList<IInventoryItemAdapter> GetRightPaneItems();

    /// <summary>Try to sell a single item via the SPItemVM's ExecuteSell. Multiple reflection paths
    /// fallback chain — see implementation. Returns true if the sell succeeded.</summary>
    bool TrySellItem(IInventoryItemAdapter item);

    /// <summary>Trigger a refresh of the inventory display (e.g., after batch sell).</summary>
    void RefreshDisplay();

    /// <summary>Read or write the IsSearchAvailable flag on the active VM.</summary>
    bool IsSearchAvailable { get; set; }

    /// <summary>Unequip every slot of the player's main hero. Returns count of slots that had items.</summary>
    int TryUnequipAllPlayerSlots();
}
```

The implementation MUST consolidate the 8 reflection probes the original module used (lines 218–236 of the decompiled). All probes in ONE place. **Per the `feedback_user_facing_promise_must_match_code.md` memory rule: if any probe is stale (renamed in v1.3.15), log a one-time `LogWarning` and gracefully degrade — don't pretend the action succeeded.**

### Harmony patch

Reserve **`Patch34_QuickActions`**. Target: `SPInventoryVM.ExecuteSellAllItems` (Prefix returning false to skip vanilla).

Verify the target exists:
```bash
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM" 2>&1 | grep -A1 "ExecuteSellAllItems"
```

The Prefix opens a `MultiSelectionInquiryData` with 4 actions (sell-all-original, sell-damaged, sell-low-value, unequip-all). Returns `false`. If the inquiry fails to open (e.g., reflection failure), call `vanillaSellAll()` as fallback so the player isn't stranded.

Wire in `Main/SubModule.cs` `OnGameInitializationFinished` (UI patches need View assembly):
```csharp
_harmony.PatchCategory("Patch34_QuickActions");
```

### MCM settings — append to `Main/Features/TaomSettings.cs`

Group: `Inventory/Quick Actions` and `Inventory/Quick Actions/Sell Damaged` and `Inventory/Quick Actions/Sell Low Value` and `Inventory/Quick Actions/Misc`. Total **14 settings** — keep names/defaults verbatim from the original module:

```csharp
// --- Inventory / Quick Actions / General ---

[SettingPropertyGroup("Inventory/Quick Actions", GroupOrder = 30)]
[SettingPropertyBool("Enable Quick Actions", Order = 0,
    HintText = "Master toggle. When off, the inventory 'Sell All' button uses vanilla behavior. When on, it opens a multi-action menu.")]
public bool EnableQuickActions { get; set; } = true;

[SettingPropertyGroup("Inventory/Quick Actions")]
[SettingPropertyBool("Enable Inventory Search", Order = 1,
    HintText = "When on, the inventory screen exposes a search box. Setting persists per save. Default: true.")]
public bool EnableInventorySearch { get; set; } = true;

// --- Sell Damaged ---

[SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged", GroupOrder = 30)]
[SettingPropertyDropdown("Damage Threshold Preset", Order = 0,
    HintText = "Items at or below this damage level are sold. Pristine = unused threshold. Default: Moderate (-20%).")]
public Dropdown<string> DamagedQualityDropdown { get; set; } = new(new[] { "Pristine", "Slight (-10%)", "Moderate (-20%)", "Heavy (-40%)" }, 2);

[SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
[SettingPropertyFloatingInteger("Custom Damage Threshold", -1.0f, 0.0f, "#0.00", Order = 1,
    HintText = "Custom threshold (modifier price multiplier offset). Only used when 'Use Custom Threshold' is on. Default: -0.20.")]
public float DamagedThreshold { get; set; } = -0.2f;

[SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
[SettingPropertyBool("Use Custom Threshold", Order = 2,
    HintText = "Toggle between dropdown preset and custom threshold value above.")]
public bool UseCustomThreshold { get; set; } = false;

[SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
[SettingPropertyBool("Sell Damaged Equipped", Order = 3,
    HintText = "Include items currently equipped on heroes. Off = only sell damaged unequipped items.")]
public bool SellDamagedEquipped { get; set; } = false;

[SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
[SettingPropertyBool("Exclude Damaged Horses", Order = 4,
    HintText = "Skip horses/mounts when selling damaged. Default: true (don't accidentally sell mounts).")]
public bool ExcludeDamagedHorses { get; set; } = true;

// --- Sell Low Value ---

[SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value", GroupOrder = 31)]
[SettingPropertyInteger("Low Value Threshold (denars)", 1, 10000, Order = 0,
    HintText = "Items at or below this denars value are sold. Default: 100.")]
public int LowValueThreshold { get; set; } = 100;

[SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
[SettingPropertyBool("Sell Low Value Equipped", Order = 1,
    HintText = "Include items currently equipped. Default: false.")]
public bool SellLowValueEquipped { get; set; } = false;

[SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
[SettingPropertyBool("Exclude Low Value Food", Order = 2,
    HintText = "Skip food items. Default: true.")]
public bool ExcludeLowValueFood { get; set; } = true;

[SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
[SettingPropertyBool("Exclude Low Value Horses", Order = 3,
    HintText = "Skip horses/mounts. Default: true.")]
public bool ExcludeLowValueHorses { get; set; } = true;

[SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
[SettingPropertyBool("Exclude Low Value Trade Goods", Order = 4,
    HintText = "Skip trade goods (cloth, fur, salt, etc.). Default: false.")]
public bool ExcludeLowValueTradeGoods { get; set; } = false;

// --- Misc ---

[SettingPropertyGroup("Inventory/Quick Actions/Misc", GroupOrder = 32)]
[SettingPropertyBool("Show Confirmation Dialog", Order = 0,
    HintText = "Ask for confirmation before bulk-selling. Default: true.")]
public bool QuickActionsShowConfirmation { get; set; } = true;

[SettingPropertyGroup("Inventory/Quick Actions/Misc")]
[SettingPropertyBool("Play Sounds", Order = 1,
    HintText = "Play audio feedback on action. Default: true.")]
public bool QuickActionsPlaySounds { get; set; } = true;

[SettingPropertyGroup("Inventory/Quick Actions/Misc")]
[SettingPropertyBool("Quick Actions Debug Mode", Order = 2,
    HintText = "Show diagnostic [QuickActions] messages on the in-game HUD. Off = file log only.")]
public bool QuickActionsDebug { get; set; } = false;
```

### IoC + SubModule.cs wiring

`Main/IoC.cs`:
```csharp
using TAOM.Features.QuickActions;
// ...
QuickActionsIoC.RegisterQuickActionsFeature(container);
```

Registers (Reuse.Singleton):
- `IQuickActionsSettingsProvider → QuickActionsSettingsProvider`
- `IInventoryVMAdapter → InventoryVMAdapter`
- `IQuickActionsService → QuickActionsService`

`Main/SubModule.cs`:
- `OnGameInitializationFinished` → `_harmony.PatchCategory("Patch34_QuickActions");`
- `OnGameStart` → `campaignStarter.AddBehavior(new InventorySearchCampaignBehavior(IoC.Resolve<IInventoryVMAdapter>(), IoC.Resolve<IQuickActionsSettingsProvider>(), IoC.Resolve<IModLogger>()));`

### CampaignBehavior tick handling

The original `InventorySearchBehavior` uses a per-frame `OnTick` to bi-directionally sync `SPInventoryVM.IsSearchAvailable`. **DO NOT** copy this into `SubModule.OnApplicationTick` (TAOM's main tick). Instead, register `CampaignEvents.TickEvent` handler on the behavior and run the sync there. Per-frame is overkill for a settings sync; once-per-campaign-tick is enough and avoids burning CPU for an idle inventory screen.

## Cross-session memory rules that apply to THIS feature

| Memory | How it applies here |
|---|---|
| `feedback_substring_keyword_matches_external_data.md` | NOT APPLICABLE — feature uses no scene-name or substring matching against engine state. |
| `feedback_adapter_modifier_preserving_overload.md` | **APPLIES — read carefully.** This feature SELLS items via `SPItemVM.ExecuteSell` reflection. Selling moves an `EquipmentElement` from inventory to "sold" state. Verify the sell path preserves `ItemModifier` (so a "Sharp Charger" sold for X denars retains the modifier in the sale calculation, not falling back to base ItemObject value). The original may have the same bug as SiegeDismount had pre-Codex — using the wrong overload. Trace it and fix. |
| `feedback_user_facing_promise_must_match_code.md` | **APPLIES STRONGLY.** 14 MCM settings — trace EACH ONE to its consumer in the service. Settings the original developer's module does not actually consume must be either implemented or dropped. Likely candidates to scrutinize: `PlaySounds` (does the original actually call SoundManager?), `ShowConfirmation` (verify it gates the inquiry), all the `Exclude*` flags (verify each filter applies). |

## Per-feature gotchas (from the decompiler agent's analysis)

1. **8-fallback reflection chain on right-pane list.** The original tries 8 different field/property names to locate `SPInventoryVM`'s right-pane item list (lines 218–236). Verify CURRENT name in v1.3.15:
   ```bash
   ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM" 2>&1 | grep -E "PartyInventory|PlayerInventory|RightItem|SideItem"
   ```
   Use the v1.3.15 name as the FIRST probe; keep the others as fallbacks for forward compatibility.

2. **5-fallback reflection chain on `ExecuteSell`.** Same pattern for the sell method. Verify in v1.3.15.

3. **`SPInventoryVM.IsSearchAvailable` MAY NOT BE VANILLA.** The original module syncs this property as if it's vanilla. It might actually be added by another mod (UIExtenderEx module). Verify:
   ```bash
   ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM" 2>&1 | grep "IsSearchAvailable"
   ```
   If the property doesn't exist in vanilla v1.3.15, the search-toggle feature is dead code — drop it OR document the external-mod dependency.

4. **`SyncData` key collision.** The original uses `SyncData("HoN_IsSearchAvailable", ref _flag)`. **Rename to `"TAOM_IsInventorySearchAvailable"`** when porting (this is a fresh feature for TAOM users; no save-compat constraint).

5. **`ExecuteSellAllItems` patch order.** TAOM has no other patches on `SPInventoryVM.ExecuteSellAllItems` (verified in CLAUDE.md patch table), so no explicit ordering needed. But TAOM Patch23 patches `SPInventoryVM.UpdateCurrentCharacterIfPossible` — different method, no collision.

6. **Confirmation flow.** When `QuickActionsShowConfirmation = true`, ask via `InformationManager.ShowInquiry` BEFORE selling. When false, sell immediately. Verify each branch is reachable.

## Verification of v1.3.15 API surface

```bash
# Verify SPInventoryVM internals
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM" 2>&1 | head -100

# Verify SPItemVM sell methods
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPItemVM" 2>&1 | grep -E "ExecuteSell|ProcessSell|IsLocked"

# Verify TransferCommand and InventoryLogic
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Inventory.TransferCommand" 2>&1 | grep -A 2 "Transfer"

# MultiSelectionInquiryData
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.MultiSelectionInquiryData" 2>&1 | head -30
```

## Acceptance gates

- Build clean — 0 errors
- Tests: at least 30 tests covering: each MCM setting routes to the correct service path; every filter flag (5 exclude flags + 2 equip flags) has a positive AND negative test; threshold matrix (custom vs preset); confirmation flow; reflection-fallback (mock the adapter to return null and verify graceful degrade); SyncData key persistence
- Full suite stays green
- `docs/features/quick-actions.md` from TEMPLATE
- CHANGELOG.md entry at top
- `/deep-review QuickActions` and `/review-codex QuickActions` — fix every confirmed finding
- New feedback memory if RCA produced one

**Do NOT commit** — leave dirty for in-game test.

## Verification — in-game golden path

1. Start a campaign with ≥1 damaged item AND ≥1 low-value item AND ≥1 equipped hero.
2. MCM → TAOM → "Inventory / Quick Actions" → confirm `Enable Quick Actions = true`.
3. Open a town's market → sell tab → click "Sell All" — verify a multi-action inquiry appears with 4 options, NOT the vanilla bulk-sell.
4. Choose "Sell Damaged" → confirm in popup → expect ≥1 damaged item sold; gold count increases by sum of (item.value * 0.5 * (1 + modifier.priceMultiplier)).
5. Choose "Sell Low Value" with threshold 100 → expect items ≤100 denars sold.
6. Choose "Unequip All" → expect every slot of every party hero stripped to inventory.
7. Disable round-trip: set `Enable Quick Actions = false` → "Sell All" reverts to vanilla bulk-sell.
8. Search round-trip: set `Enable Inventory Search = false` → search box hidden in inventory.

## Final report format

```
QuickActions port complete.
- Files created: [count]
- Files modified: TaomSettings.cs (14 settings), IoC.cs, SubModule.cs (Patch34 + behavior registration)
- IInventoryVMAdapter introduced (load-bearing for feature 6 EquipPresets)
- Reflection probes verified against v1.3.15 SPInventoryVM: [list of confirmed names]
- IsSearchAvailable status: [vanilla / external-mod-dependent / dropped]
- Tests: NN/NN QuickActions tests pass; XXXX/XXXX total
- /deep-review verdict: [PASS / N findings fixed]
- /review-codex verdict: [PASS / N findings fixed]
- Dead settings dropped: [list, if any]
- New feedback memories codified: [list]
- Awaiting in-game verification before commit.
```
