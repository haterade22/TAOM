# RCA — QuickActions (Codex review #36, 2026-05-06)

## Top-line

Codex adversarial review of QuickActions found **2 HIGH + 1 MEDIUM + 1 INFO** finding that the prior `/deep-review` (5 parallel agents, all PASS on standards/compat/efficiency/completeness/data-flow) missed. All 4 verified via vanilla v1.3.15 decompilation, fixed in same session, regression tests added. Build clean (53/53 QuickActions tests pass).

## Findings + Root Cause Table

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| F1 (HIGH) | "Sell All (Vanilla)" menu option ran a hand-rolled `ProcessSellItem` loop instead of re-entering vanilla `ExecuteSellAllItems`. Dropped capacity-budget, settlement-gold (`TransferAllForSettlement`), full-stack amounts, sort by `RosterElementComparer`, and `ExecuteRemoveZeroCounts` cleanup. | **Engine-bypass anti-pattern** (convention inconsistency) | Decompiled only the filter triplet of `TransferAll`. Assumed the filter was the whole point of `TransferAll` and that calling `ProcessSellItem` per item was equivalent. Did not decompile the post-filter logic. | **New rule (feedback memory):** when a Prefix returns `false` to skip vanilla, AND the menu offers a "use vanilla" option, that option MUST re-enter the vanilla method via a thread-static bypass flag, never hand-roll equivalent logic. Codified in `feedback_vanilla_reentry_via_bypass_flag.md`. |
| F2 (HIGH) | `TrySellItem` sold only 1 unit per stack. Stacks of 50 damaged arrows reported "1 sold" but transferred 1. | **API misread / logic error** (didn't trace into engine method) | Treated `SPItemVM.ProcessSellItem(item, cameFromTradeData=true)` as "sell this whole item." Did not decompile its body, which reads `item.TransactionCount` (default 1) for the sell amount when `cameFromTradeData` is true. Tests used `IInventoryItemAdapter` mocks that bypassed `SPItemVM.TransactionCount` semantics — the abstraction hid the bug. | **New rule (feedback memory):** when invoking a vanilla static delegate from an adapter, decompile the receiver method to identify what state it reads off the parameter object — a "sell this item" delegate may read a transaction-count field. Tests must mirror the real receiver's parameter-state expectations. Codified in `feedback_static_delegate_reads_param_state.md`. **Regression tests added:** `SellAllDamaged_StackOf50_ReportsAllUnitsAsAffected_NotJustOneRow`, `SellAllLowValue_StackOf30_ReportsAllUnitsAsAffected`, `SellAllDamaged_ZeroStack_SkipsItem`. |
| F3 (MEDIUM) | `UnequipAll` direct-mutated `Hero.BattleEquipment` + `ItemRoster.AddToCounts`, then called `SPInventoryVM.RefreshValues()`. RefreshValues only refreshes labels/slots — does not rebuild `RightItemListVM`. UI stayed stale until inventory close + reopen. | **Engine-bypass anti-pattern** (same root cause as F1) | Pattern was valid in isolation (modifier-preserving overload, EquipmentElement.Invalid sentinel), but ignored that the active VM has its own row-rebuild contract via `InventoryLogic.AfterTransfer`. `RefreshValues` was assumed to be "redraw everything" without decompiling its body. | Same rule as F1 codified in `feedback_vanilla_reentry_via_bypass_flag.md`: when a UI is bound to an underlying logic via an event, mutations to the logic must go through the channels the event listens on. Fix routes unequip through `InventoryLogic.TransferCommand` per slot, which fires `AfterTransfer` → rebuilds rows + equipment slots. |
| Obs (INFO) | Code comment says `CampaignEvents.TickEvent` fires "hourly" — actually fires every `Campaign.Tick(float dt)` (every campaign frame). Behavior unaffected (idempotent), but comment is misleading. | **Documentation rot** | Wrote comment from incorrect intuition. Did not decompile `CampaignEvents.cs` to verify cadence. | Documentation correction; no rule needed (single-instance error). Comment now reads "every campaign frame; reconciliation is a single bool compare so per-frame cost is negligible." |

## Root Cause Pattern: "Engine-Bypass Anti-Pattern"

F1 and F3 are the same shape: TAOM code mutates engine state in a way that bypasses the vanilla event/refresh contract the UI is listening on. The deep-review's data-flow agent traced data declarations to consumers but did not check whether the consumers were listening on the channel the producer was using.

The unifying rule:

> **When an inventory/UI screen is open, ALL state mutations to the underlying logic (rosters, equipment, transfers) must route through the engine's command pattern (`InventoryLogic.TransferCommand` here) so the screen's `AfterTransfer`-equivalent listener fires and the UI rebuilds. Direct mutation of the underlying state — even with the modifier-preserving overload — leaves the UI stale.**

This is broader than the modifier-preserving-overload rule (`feedback_adapter_modifier_preserving_overload.md`): that rule covers WHAT object you pass; this rule covers WHICH method you call.

## Feedback Memories Codified

- `feedback_vanilla_reentry_via_bypass_flag.md` — when a Prefix returns false AND the feature offers a "use vanilla" option, re-enter via thread-static bypass; do not hand-roll. Covers F1.
- `feedback_static_delegate_reads_param_state.md` — when invoking a vanilla static delegate, decompile its body to identify required parameter-state fields the caller must set first. Covers F2.
- `feedback_route_via_engine_command_when_ui_active.md` — when a screen is bound to underlying logic, mutations must go through the engine command pattern; direct mutation leaves UI stale. Covers F3.

## Why Deep-Review Missed These

- **Standards (Agent 1):** Doesn't trace engine semantics. Found ADR-007 violations but not engine-bypass.
- **Compatibility (Agent 2):** Verified all method signatures exist. Did not flag that the lambda's behavior differs from `TransferAll`'s post-filter logic — only checked that the methods we called exist.
- **Efficiency (Agent 3):** Per-fire overhead OK (user-driven). Couldn't surface a logic bug.
- **Completeness (Agent 4):** Found IoC/SubModule wiring issues but doesn't trace engine-state mutation.
- **Data Flow (Agent 5):** Closest hit — flagged the missing `IsFiltered` check (correct) but compared the lambda only to `TransferAll`'s filter. Did not decompile the rest of `TransferAll` and so missed capacity/settlement/full-stack divergence. Also didn't notice that `RefreshValues` doesn't rebuild rows.

The lesson: **deep-review's data-flow agent needs explicit instructions to trace not just data declarations but engine event-channel listeners.** Add to its prompt: "When a TaleWorlds VM is bound to underlying logic via events (`AfterTransfer`, `TotalAmountChange`, etc.), check whether mutations bypass that channel."

## Patch History

| Pre-Codex | Post-Codex |
|-----------|-----------|
| `_active.RefreshValues()` after direct mutation | `InventoryLogic.AddTransferCommands(perSlotCommands)` via reflection on private `_inventoryLogic`/`_currentCharacter` |
| Hand-rolled `vanillaSellAll` lambda iterating items | Thread-static `_bypassQuickActions` flag, "Sell All (Vanilla)" calls `__instance.ExecuteSellAllItems()` |
| `del.Invoke(spItem, true)` — sells 1 unit per stack | `spItem.TransactionCount = item.StackAmount; del.Invoke(spItem, true)` — sells full stack |
| Service tracks `sold++; gold += item.ItemValue` per row | Service tracks `unitsSold += stack; gold += item.ItemValue * stack` |

## Tests Added (regression coverage)

- `SellAllDamaged_StackOf50_ReportsAllUnitsAsAffected_NotJustOneRow` — assert `ItemsAffected == 50`, not 1
- `SellAllLowValue_StackOf30_ReportsAllUnitsAsAffected` — same shape for low-value path
- `SellAllDamaged_ZeroStack_SkipsItem` — defensive against zero-stack mock VMs

53/53 QuickActions tests pass after fixes.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
