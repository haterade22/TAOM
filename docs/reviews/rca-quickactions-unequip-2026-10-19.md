# RCA: Quick Actions Unequip All left the open screen stale (#917, 2026-10-19)

> Recorded on the 1.5.x line; ported to the 1.4.x line by plan 133. Commit hashes in this file are 1.5.x commits; the 1.4.x ports are plan 133's Part A commits. The cited engine lines are unchanged on v1.4.8.

## Top-line

Unequip All wrote the main hero's equipment slots directly, so the open inventory screen kept showing
the old gear until it was reopened. The May 2026 review (`rca-quickactions-2026-05-06.md` F3) had
already found this and recorded "route through `InventoryLogic.TransferCommand`" as the fix. The fix
went into `InventoryVMAdapter.TryUnequipAllPlayerSlots`, but `QuickActionsService.UnequipAll` kept
calling the direct-write adapter, and a test pinned that wrong route. #917 wires the transfer route.

The deep review of that fix (7 lenses) then found the transfer route is not the whole refresh either:
`AfterTransfer` updates the item rows and the shown set's slots, but not the paper doll, armour,
weight, capacity, the no-saddle warning or the Done button. Vanilla's single-transfer handlers refresh
those with the private `RefreshInformationValues`, which a batch defeats (see row 1). `RefreshDisplay`
now runs `UpdateRightCharacter` then `RefreshInformationValues`, the pair vanilla runs when the shown
outfit or character changes.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | After the transfers, the paper doll, armour, weight, capacity, no-saddle warning and Done button stay stale. `SPInventoryVM.ProcessUnequipItem` (v1.4.8 :3488-3497) follows its transfer with `RefreshInformationValues`; `AfterTransfer` clears the equipment dirty flag on every command (:3840), so even that call can skip the doll when the last command moved nothing (a harness the horse's own transfer already moved). | Engine contract read from the event, not the handler | Both the May fix and the first #917 fix read `AfterTransfer` as "the screen's refresh". Nobody opened the vanilla handler that issues a transfer to see what it does after `AddTransferCommands`. | `RefreshDisplay` calls the private `UpdateRightCharacter` + `RefreshInformationValues` through cached open delegates, with `ReflectionSiteBindingTests` rows and a bind test. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 2 | LOW | The May fix was recorded as done but never reached the caller: the service called `IPlayerEquipmentAdapter`, and `UnequipAll_DelegatesToEquipmentAdapter_ReturnsCount` asserted that route. | Fix landed in the callee only | The regression test for the fix was written against the adapter that got the new code, not against the caller that chooses the adapter. | The service tests assert `IInventoryVMAdapter.TryUnequipAllPlayerSlots` is called. Lesson in `lessons/testing-qa.md`. |
| 3 | LOW | The direct-write fallback in `InventoryVMAdapter` ran when no screen or no `InventoryLogic` was active. The only caller is the menu on an open screen, so it was unreachable, except the null-logic branch, which would have brought the stale screen back. | Dead defensive path | Kept "as belt-and-suspenders" in May without a caller that needed it. | Fallback and `IPlayerEquipmentAdapter.TryUnequipAllPlayerSlots` deleted; the method returns 0 and changes nothing. |
| 4 | LOW | Comments and doc said the adapter "queues" transfers (they apply at once), that `EquipmentIndex.None` on the destination drops into the roster (the destination index is ignored for the player inventory), that `RefreshDisplay` rebuilds the item list (it refreshed labels only), and the doc's test counts and "confirmation flow" coverage were wrong. | Doc drift | Text written from intent, not from the decompile. | Corrected in the same commit. |

## Why each lens saw what it saw

- **Data flow (5) and engine compatibility (2)** found finding 1 independently by reading
  `ProcessUnequipItem` and the dirty flag. This is the class the May RCA asked lens 5 to trace
  ("check whether mutations bypass the event channel"); this time the channel was used but was not
  sufficient, which is the next step of the same rule.
- **Standards (1)** found the doc drift and the pre-existing ADR-007 inquiry calls (follow-up).
- **Design (6)** chose where the refresh lives (`RefreshDisplay`, which the service already calls and
  three tests already pin) and proved the fallback unreachable.

## Follow-ups (pre-existing code, not in this change)

- EquipPresets has the same stale panel: `InventoryScreenAdapter` calls `AddTransferCommands`, then
  `RefreshValues` (labels only). It can reuse the same bound pair.
- `SellDamagedEquipped` and `SellLowValueEquipped` can never act: `GetRightPaneItems` returns only
  player-inventory rows, so `IsEquipped` is never true there. Rated HIGH under the dead-setting rubric.
- Batch sells pass `cameFromTradeData: true`, which skips vanilla's `ExecuteRemoveZeroCounts`, so
  sold-out rows can linger with a count of 0.
- `QuickActionsService` builds TaleWorlds inquiry types and calls `InformationManager` directly
  (ADR-007 static-call form), and its menu text is not localized.
- `FinalizeSell`'s `isUnequip` parameter is never read.

Each needs its own issue on Mike's word.
