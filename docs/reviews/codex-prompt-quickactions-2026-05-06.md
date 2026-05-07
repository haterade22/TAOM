# Codex Adversarial Review Prompt -- QuickActions (TAOM)

You are reviewing a Bannerlord 1.3.15 mod feature ported into TAOM. This feature replaces the inventory screen's "Sell All" button with a multi-action menu (Sell Damaged / Sell Low Value / Unequip All / vanilla) and adds a per-save persistent toggle for the inventory search box. Owns Harmony patch slot Patch34_QuickActions. Three Harmony patches (Prefix on ExecuteSellAllItems, Postfix on SPInventoryVM ctor, Postfix on RefreshCallbacks) plus a Postfix on OnFinalize, one CampaignBehavior, one service, one settings provider, one audio player, two adapters (IInventoryVMAdapter, IInventoryItemAdapter) plus extension to IPlayerEquipmentAdapter.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

NOTE: This feature does NOT use kingdom/culture IDs at all. The cheatsheet is included for completeness only. Skip ID cross-reference.

## READ FIRST

- `docs/features/quick-actions.md` -- feature doc with full architecture + dependency graph
- `c:/Users/mikew/.claude/plans/feature-port-session-shiny-popcorn.md` -- planning context
- `Main/Features/QuickActions/` -- all source
- `Main/Adapters/{IInventoryVMAdapter,InventoryVMAdapter,IInventoryItemAdapter,InventoryItemAdapter,IPlayerEquipmentAdapter,PlayerEquipmentAdapter}.cs`
- `TAOM.Tests/Features/QuickActions/` -- 50 tests, all passing

## Known Suspects -- CONFIRM or DISPUTE

Claude's deep-review (5 parallel agents) already caught and fixed these. Your job is to verify the FIXES are correct AND find anything the deep-review missed.

1. **MultiSelectionInquiryData callback signatures** -- the constructor signature has both `affirmativeAction: Action<List<InquiryElement>>` and `negativeAction: Action<List<InquiryElement>>`. The TAOM service passes a callback for both. Verify callback shapes against `TaleWorlds.Core.MultiSelectionInquiryData` v1.3.15 ctor.

2. **Stale-VM lifecycle**: `Patch34_SPInventoryVMCapture` sets `_active` on construction; `Patch34_SPInventoryVMFinalize` clears it via `ClearActiveIfMatches(__instance)`. CONFIRM the OnFinalize Postfix actually fires for SPInventoryVM (decompile to verify the method exists and is called by the inventory screen close). Check whether construct-of-VM-2-before-finalize-of-VM-1 sequence is possible (e.g., re-opening inventory while another inventory is closing) and whether the `ReferenceEquals` guard handles it correctly.

3. **vanillaSellAll lambda parity with vanilla TransferAll**: The Prefix in `Patch34_SellAllItemsMenu.cs` constructs a `vanillaSellAll` lambda that mirrors `SPInventoryVM.TransferAll(isBuy:false)`'s filter (`!IsFiltered && !IsLocked && IsTransferable`). It does NOT mirror the capacity-budget logic, settlement-mode logic (`TransferAllForSettlement`), or warehouse-mode logic vanilla applies after the filter. Decompile `SPInventoryVM.TransferAll` v1.3.15 in full and report whether the missing post-filter logic is a user-visible parity bug for "Sell All (Vanilla)".

4. **Modifier preservation in unequip**: `PlayerEquipmentAdapter.StripEquipment` calls `roster.AddToCounts(element, 1)` where `element` is `EquipmentElement` (not `ItemObject`). Per `feedback_adapter_modifier_preserving_overload.md`, this is the modifier-preserving overload. Verify by decompiling `ItemRoster.AddToCounts(EquipmentElement, int)` and confirming the `ItemModifier` is carried through. Cross-reference: `ItemRoster.AddToCounts(ItemObject, int)` would discard modifier; verify our call resolves to the correct overload.

5. **InventorySearchCampaignBehavior cross-campaign Singleton safety**: The behavior is registered `Reuse.Singleton` in `QuickActionsIoC.cs`. It subscribes to `OnNewGameCreatedEvent`, `OnGameLoadedEvent`, `TickEvent`. It does NOT subscribe to `OnSessionLaunchedEvent` (unlike `MessengerCampaignBehavior` which uses that hook for cross-campaign-in-same-process resets). The only mutable state is `_isSearchAvailable` (bool, default true). Determine whether the missing `OnSessionLaunchedEvent` subscription creates a bug: if the player exits to main menu (without process restart) and starts a new campaign, does `OnNewGameCreatedEvent` fire FIRST, BEFORE the singleton has a chance to retain stale state? If yes, no bug. If `OnNewGameCreatedEvent` could fire later (or not at all in some edge), there's a latent stale-state risk.

6. **`SPInventoryVM.RefreshCallbacks` Postfix attach point for IsSearchAvailable**: `Patch34_SPInventoryVMSearchApply` Postfixes `RefreshCallbacks` and writes `__instance.IsSearchAvailable = behavior.IsSearchAvailable`. Verify (a) `RefreshCallbacks` is the correct lifecycle point — it should fire ONCE per inventory-open AFTER the VM is bound to the UI; (b) writing `IsSearchAvailable = false` actually hides the search box (vs only blanking the search text); (c) no other vanilla code overrides `IsSearchAvailable` between our Postfix and the user's first visible frame.

## File list

### Main/Features/QuickActions/

- `IQuickActionsService.cs` -- service interface (4 methods)
- `QuickActionsService.cs` -- main filter + dispatch + audio + refresh logic
- `IQuickActionsSettingsProvider.cs` / `QuickActionsSettingsProvider.cs` -- 15 settings wrapped from TaomSettings
- `Models/QuickActionType.cs` -- 4-value enum (SellDamaged, SellLowValue, UnequipAll, OriginalSellAll)
- `Models/QuickActionResult.cs` -- struct: status, items, gold
- `Models/DamagedQualityPreset.cs` -- 4-value enum + ToThreshold extension + dropdown index mapping
- `Audio/IQuickActionsAudioPlayer.cs` / `Audio/QuickActionsAudioPlayer.cs` -- wraps SoundEvent.PlaySound2D("event:/ui/transfer")
- `QuickActionsIoC.cs` -- DryIoc registration
- `Hooks/InventorySearchCampaignBehavior.cs` -- SyncData("TAOM_IsInventorySearchAvailable") + RegisterEvents
- `Hooks/Patch34_SellAllItemsMenu.cs` -- Prefix returning false on SPInventoryVM.ExecuteSellAllItems
- `Hooks/Patch34_SPInventoryVMCapture.cs` -- Postfix on ctor, captures active VM
- `Hooks/Patch34_SPInventoryVMSearchApply.cs` -- Postfix on RefreshCallbacks, applies IsSearchAvailable
- `Hooks/Patch34_SPInventoryVMFinalize.cs` -- Postfix on OnFinalize, clears active VM

### Main/Adapters/

- `IInventoryVMAdapter.cs` / `InventoryVMAdapter.cs` -- wraps SPInventoryVM (right-pane list, ProcessSellItem delegate, IsSearchAvailable, RefreshDisplay, TryUnequipAllPlayerSlots)
- `IInventoryItemAdapter.cs` / `InventoryItemAdapter.cs` -- wraps SPItemVM (item value, equip state, EquipmentElement passthrough)
- `IPlayerEquipmentAdapter.cs` / `PlayerEquipmentAdapter.cs` -- existing adapter EXTENDED with TryUnequipAllPlayerSlots + StripEquipment helper

### Test files

- `TAOM.Tests/Features/QuickActions/QuickActionsServiceTests.cs` -- 34 tests (skip-guard exhaustion, threshold matrix, modifier preservation, audio, refresh, no-inventory degrade)
- `TAOM.Tests/Features/QuickActions/InventorySearchCampaignBehaviorTests.cs` -- 7 tests (default state, OnTick reconcile both directions, OnNewGameCreated seed both values, OnGameLoaded reconcile)
- `TAOM.Tests/Features/QuickActions/DamagedQualityPresetTests.cs` -- 9 tests (preset → threshold, dropdown index → preset, out-of-range fallback)

### Modified

- `Main/Features/TaomSettings.cs` -- 15 new MCM settings (GroupOrder 30/31/32) under "Inventory/Quick Actions"
- `Main/IoC.cs` -- QuickActionsIoC.RegisterQuickActionsFeature(container) call
- `Main/SubModule.cs` -- _harmony.PatchCategory("Patch34_QuickActions") + behavior registration
- `CLAUDE.md` -- Patch34 row + feature path entry
- `CHANGELOG.md` -- top entry

## REQUIRED SECTIONS

### VANILLA CODE

Decompile and paste as code blocks:

1. `SPInventoryVM.ExecuteSellAllItems` v1.3.15 -- target of our Prefix. We need its full body to verify no other side effects we're missing.

2. `SPInventoryVM.TransferAll(bool isBuy)` v1.3.15 -- the underlying logic ExecuteSellAllItems calls. Our `vanillaSellAll` lambda mirrors its filter triplet. Compare line-by-line: filter conditions, capacity-budget logic, settlement-mode logic, warehouse-mode logic.

3. `SPInventoryVM.RefreshCallbacks()` v1.3.15 -- target of our IsSearchAvailable-apply Postfix. Verify it is called exactly once per inventory open and after VM binding.

4. `SPInventoryVM.OnFinalize()` v1.3.15 -- target of our cleanup Postfix. Verify it runs on inventory close.

5. `SPInventoryVM` constructor (3-arg) v1.3.15 -- target of our active-VM-capture Postfix. Verify the parameter types match our `[HarmonyPatch]` declaration: `(InventoryLogic, bool, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>)`.

6. `SPItemVM.ProcessSellItem` v1.3.15 -- the static `Action<SPItemVM, bool>` field we invoke. Verify our null-guard is the right pattern (it's null until RefreshCallbacks fires).

7. `ItemRoster.AddToCounts(EquipmentElement, int)` v1.3.15 -- modifier-preserving overload. Confirm `ItemModifier` is carried through.

8. `Equipment[EquipmentIndex] = EquipmentElement.Invalid` semantics -- does this fully clear the slot, or just zero the count?

### SCENARIO ANALYSIS

For each scenario, walk through and report findings.

**Scenario A: Player has filtered inventory to "Weapons only", clicks Sell All, picks "Sell All (Vanilla)" from our menu.**
What happens? Vanilla would only sell weapons. Does our `vanillaSellAll` lambda match that behavior? (Should — we added the IsFiltered check.)

**Scenario B: Player has 200 items (well above weight capacity) and clicks Sell All → Sell All (Vanilla) at a settlement market with low gold.**
Vanilla `TransferAll` has settlement-mode logic that handles gold-affordability. Our lambda doesn't. Is this a user-visible bug? Decompile `TransferAllForSettlement` to check.

**Scenario C: Player opens inventory, closes it, reopens it. Each open spawns a new SPInventoryVM. Each ctor fires our SetActive Postfix; each OnFinalize fires our ClearActiveIfMatches.**
Trace: Is there a timing window where the OLD VM's OnFinalize runs AFTER the NEW VM's ctor? If yes, our `ClearActiveIfMatches(oldVm)` would correctly leave the new active VM in place. If the order is reversed (new ctor before old finalize), our `_active` would be set to the new VM, then the old finalize would attempt to clear it but `ReferenceEquals(_active, oldVm)` would be false, so no-op. Verify both orderings are safe.

**Scenario D: User loads a campaign saved BEFORE QuickActions was installed. SyncData has no "TAOM_IsInventorySearchAvailable" key. The bool field defaults to `true`. MCM EnableInventorySearch is `false`.**
Will the user see the search box on first inventory open? Trace OnGameLoadedEvent's reconcile-vs-MCM logic.

**Scenario E: Player exits to main menu (no process restart), starts a new campaign. The Singleton InventorySearchCampaignBehavior is the same instance. `_isSearchAvailable` is whatever it was at end-of-prior campaign. OnNewGameCreatedEvent fires.**
Does our handler reseed before any inventory is opened? What happens if (somehow) the player opens inventory between session-launch and OnNewGameCreated?

**Scenario F: User has UseCustomThreshold=false, DamagedPreset=Pristine. Clicks Sell Damaged.**
Pristine returns 0f from ToThreshold. Service has `if (threshold >= 0f) return false;` sentinel guard. Result: nothing sells. Correct? Or is Pristine meant to mean "sell nothing" implicitly via this sentinel, in which case the behavior is right but the dropdown UX is confusing (user expects "Pristine" to mean "items at pristine quality" not "disabled")?

**Scenario G: An item has `IsTransferable = false` AND a damaged modifier. Should our SellDamaged sell it?**
Verify the filter: IsTransferable is checked BEFORE the modifier check in IsDamagedSellTarget. Confirm consistent with vanilla — quest items (`IsQuestItem`) are non-transferable, so this should be safe.

### CONFIG CROSS-REFERENCE

Skip — feature has no XML/JSON config.

### FINDINGS OR OBSERVATIONS

For each issue found, output:

```
SEVERITY: HIGH/MEDIUM/LOW/P1/P2
LOCATION: file:line
CLAIM: what the bug is
EVIDENCE: why you believe it
FIX: minimum change
```

If no issues, write "NO ADDITIONAL FINDINGS" and explain what you verified.

## QUALITY GATES

You MUST decompile every vanilla method/property listed in VANILLA CODE. Pasting "could not access decompiled folder" is unacceptable -- the path is `E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem.ViewModelCollection/TaleWorlds.CampaignSystem.ViewModelCollection.Inventory/SPInventoryVM.cs` (note: that folder is v1.4 — for v1.3.15 verification use ilspycmd against `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.ViewModelCollection.dll`).

You MUST run through every scenario A-G. If a scenario is N/A, explain why.

You MUST address every Known Suspect with CONFIRMED or DISPUTED, not "needs more research."

## Prior review lessons

SUCCESSES (from prior reviews):
- Config ID cross-ref caught rohan/dol_guldur mismatches
- Vanilla decompilation caught missing gates
- Lifecycle tracing caught stale caches
- Codex caught HIGH bugs Claude missed when both reviewed (DeadCivilianEquipment fallback in SiegeDismount, CharCreation race-allow-list bypass)

FAILURES (Codex has been wrong about):
- Codex assumed empire=Rohan (it is Dunland) — N/A here, no kingdom logic
- Codex flagged vanilla-matching code as bugs — verify your claims with decompiled vanilla before flagging
- Codex skipped hard sections — every section is required

## Output

Write your report to: `docs/reviews/codex-adversarial-quickactions-2026-05-06.md`

End with a Known Suspects table:

| # | Suspect | Verdict (CONFIRMED/DISPUTED) | Evidence |
|---|---------|------------------------------|----------|

Then a top-line summary: total findings by severity, total verified-clean areas.
