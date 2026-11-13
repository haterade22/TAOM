# Reeve's Ledger

## Overview

The Reeve's Ledger adds six extras to the town management window, the one you open from a town or castle menu and from the F6 fief list. The title shows the fief's name instead of "Manage". A Profit row on the left lists what the fief earns a day, with a tooltip that breaks it down. Towns also get a Patrol row on the right, and so does a castle that has a live patrol party or that the patrol model lets have one (Castle Patrols on, a non-rebel owner, a built Guard House and the owner culture's template). The stash and prison icons sit beside the consumption icon, each with a tooltip listing what they hold. Beside the reserve's plus button, a -10K and a +10K button take gold back out of the reserve or put it in with one click.

## Why This Exists

- **Vanilla behavior:** the window opens with a fixed "Manage" title and four stat rows on each side. The daily gold change of the clan, on the clan screen and in the map bar's gold tooltip, itemizes income per fief, but nothing shows one fief's profit as a single figure. The stash and the prisoners are reachable only from the town menu. The reserve takes gold through a popup with a slider that floors to hundreds, and there is no way to take gold back out.
- **TAOM requirement:** the player who runs several fiefs from F6 should see which fief is open, what it earns, what it holds and how strong its patrol is, and should be able to move reserve gold without the popup.
- **Without this feature:** the player opens the clan screen, the town menu and the popup to learn what the window could show at a glance.

## Architecture

### Design Challenge

- The window is a vanilla view model (`TownManagementVM`) and a vanilla movie (`TownManagement`). The extras must be added without replacing either, so they ride UIExtenderEx: two view model mixins and two prefab insertions.
- `RefreshTownManagementStats` is private and clears both stat lists before it refills them. It runs from the constructor, on a queue change, on a governor change and on a reserve confirm, so the rows are appended after every refill.
- The reserve confirm is the only vanilla path that charges or refunds reserve gold. Driving the popup's slider with a negative amount would be unsafe: the discrete slider widget floors to hundreds and writes the value back, which creates gold.
- The profit figure must match the clan finance screen, which means going through the campaign's model slots (so `TaomClanFinanceModel`'s tariff feats apply) and never through the withdrawing form of the model calls, which takes a fifth of the town's accumulated trade tax on every call.

### Solution Approach

`ReevesLedgerService` is plain numbers in and out and takes no adapter: `ComposeProfit`, `PlanReserveStep`, `SummarizeStash`, `SummarizePrisoners`, `ShowsPatrolRow` and `MayWriteWorld`, which reads the co-op providers live. The one adapter, `IFiefLedgerAdapter`, reads the engine (profit inputs, stash, prisoners, patrol). Nothing writes gold except the reserve view model's own confirm.

- **Title and rows.** `TownManagementLedgerMixin` is `[ViewModelMixin("RefreshTownManagementStats")]`. UIExtenderEx runs its `OnRefresh` at the end of that method, and replays the constructor's refresh once the mixin exists, which is after the constructor's own `RefreshValues` wrote "Manage". The mixin sets `ManageText` to the fief name there, appends a Profit row (`DescriptionType.Gold`) to the left list and, when `ShowsPatrolRow` says so, a Patrol row (`DescriptionType.Security`) to the right list. It removes its own rows before adding them again, so a refresh that the vanilla method did not precede cannot stack a second pair.
- **The settlement.** The view model's constructor reads `Settlement.CurrentSettlement`, and so does the mixin, once, in its own constructor. The remote F6 window swaps that value in before it builds the view model and restores it after `OnFinalize`, so both see the same fief.
- **Profit.** `FiefLedgerAdapter.ReadProfit` makes the same three calls and the same `(int)` truncation as `DefaultClanFinanceModel.AddSettlementIncome` (settlement tax, tariffs, projects, then each bound village), all with `applyWithdrawals: false`, plus the garrison party's `TotalWage` while the garrison is active. The garrison line is the daily wage: the clan's charge tops the garrison purse up to 5,000, which equals the wage once the purse has settled. The total is the signed sum and saturates at the int range, never clamped at zero, so a fief that costs more than it earns shows a negative number. The tooltip ends like the clan finance tooltips: a rundown separator, then the total as the rundown result. It is computed once per refresh, never per frame, and the tooltip formats a snapshot on hover.
- **Patrol.** The row value is the men in `Settlement.PatrolParty`, 0 if none. The tooltip is the engine's own `CampaignUIHelper.GetTownPatrolTooltip`, the call `KingdomSettlementItemVM` makes. That call dereferences the patrol behaviour when a town has no patrol party, so the adapter returns an empty string when both are missing. The row shows for every town and, since #992, for a castle that has a live patrol or that the patrol model allows one (`ReevesLedgerService.ShowsPatrolRow`: `PatrolParty != null`, or `SettlementPatrolModel.CanSettlementHavePatrolParties(settlement, false)`, which Keep Wardens answers yes for a castle with Castle Patrols on, a non-rebel owner clan, a built Guard House and the owner culture's template). A castle that can have neither gets no row, because the engine's only status for it would be "No Guard House". The same tooltip call serves a castle: it reads `settlement.Town` (a castle has one) and `PatrolParty`, and its no-patrol status goes through the patrol model, so it needs no castle branch. No new text: every string is the engine's.
- **Icons.** `ShowLedgerIcons`, `StashTooltip` and `PrisonTooltip` are mixin properties. The stash tooltip lists stacks by count, biggest first, at most 15 lines and then "...and N more". The prison tooltip lists heroes first and then regulars, with the head count in its title. Each has an empty marker. The two icons are inserted as siblings after the vanilla `Consumptions` widget, 95 px to each side, using the game menu's stash and prisoner sprites.
- **Step buttons.** `ReserveStepMixin` is `[ViewModelMixin("ExecuteConfirm")]` on `TownManagementReserveControlVM`. A press plans the step from the view model's own figures, `PlanReserveStep(vm.CurrentReserveAmount, vm.MaxReserveAmount, direction)` (`MaxReserveAmount` is vanilla's smaller of the purse and 10,000; +10K adds the smallest of 10,000, that figure and the room below `int.MaxValue`; -10K takes the smaller of 10,000 and the reserve). It then sets `CurrentGivenAmount = 0`, `CurrentReserveAmount = target` and runs the view model's own `ExecuteConfirm`, in that order. The confirm passes `CurrentReserveAmount + CurrentGivenAmount` to `BuildingHelper.BoostBuildingProcessWithGold`, which charges the purse for a rise and refunds the difference for a fall, so -10K refunds in full and never costs a penalty; it then refreshes the reserve texts and fires the vanilla callback that rebuilds the stat rows. The mixin checks the reserve equals the target afterwards and logs once if not. The +10K button is disabled with no gold and -10K with an empty reserve.
- **Single-player only.** Both buttons write the world, so they show and work only while `MayWriteWorld` holds (`CoopSessionPolicy.IsSinglePlayerAuthority` (no live session, no deferral to a host, no dedicated server, and `IsAuthority`)). The title, rows and icons only read, so they need no gate. See [coop-interop.md](coop-interop.md).
- **Hints.** Each button is built in the shape of vanilla's governor button (SandBox `TownManagement.xml` lines 105-111): a fixed 80 x 44 wrapper `Widget` carrying the id, alignment, offset and visibility, with its own `HintWidget` (the disabled hint, empty while the button is enabled) beside a `ButtonWidget` that stretches to the wrapper and holds its own `HintWidget` for the enabled state. Gauntlet never hovers a disabled widget, so a hint inside the disabled button could never say why it is disabled. The four hint view models and the two icon tooltips are created once and their text or callback is updated in place on each refresh.
- **Guards.** Every member the engine calls (the two constructors, both refreshes, both clicks, all four tooltip functions) runs inside `ScreenFaultLog` (`Main/Core/Logging`, shared with Namegiving), which logs one line per call site and swallows the throw (later calls still run). Every throw is also counted in the `[Session]` summary (#1071). A failed constructor leaves the mixin disabled rather than taking the window down. Each constructor builds the fault log first, so a fault in any later resolve is logged.
- **The switches.** `EnableReevesLedger` (title, rows, icons) and `EnableReserveSteps` (the buttons) sit in the MCM group "Fief Management/Reeve's Ledger", both default on. Each is read once, in the mixin's constructor, so an edit applies the next time the window opens. `ReevesLedgerSettingsProvider` is a concrete class, resolved directly: it has one implementer and no test fakes it.
- **The F6 swap.** The F6 window reads the right fief only because `GauntletFiefManagementScreen` swaps `Settlement.CurrentSettlement` for the screen's whole lifetime; the step buttons read it at click time through the reserve view model's confirm. See [fief-management.md](fief-management.md).

### Component Diagram

```
TaomSettings.EnableReevesLedger / EnableReserveSteps ─ ReevesLedgerSettingsProvider
                                                              │
TownManagementVM ── TownManagementLedgerMixin ──┬── ReevesLedgerService ◄── ICoopSessionProvider, IDedicatedServerProvider
  (RefreshTownManagementStats)                  │         ▲
                                                ├── IFiefLedgerAdapter ── Campaign.Current.Models, Settlement
                                                └── LedgerTooltips, ScreenFaultLog
TownManagementReserveControlVM ── ReserveStepMixin ──┬── ReevesLedgerService
  (ExecuteConfirm)                                   └── the view model's own ExecuteConfirm ── BuildingHelper.BoostBuildingProcessWithGold
TownManagement.xml ◄── ReevesLedgerIconsPrefab (after Consumptions), ReserveStepButtonsPrefab (after ReserveListPanel)
```

## Configuration

There is no JSON file: the step size is a constant in the service and the tooltip cap a constant in `LedgerTooltips`.

| MCM setting | Group | Default | Effect |
|---|---|---|---|
| `EnableReevesLedger` | Fief Management/Reeve's Ledger (GroupOrder 2) | on | the fief name as the title, the Profit and Patrol rows and the icon tooltips |
| `EnableReserveSteps` | Fief Management/Reeve's Ledger | on | the -10K and +10K buttons (single-player only) |

Both are excluded from the co-op settings fingerprint: the first is presentation, the second rests on the `MayWriteWorld` gate (inert under co-op because the buttons are hidden and the click refuses; `CoopSettingsRelevance`). The trade-off: a solo player with a co-op mod TAOM cannot probe gets no buttons, the same as From the Ashes.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ReevesLedger/ReevesLedgerModule.cs` | registers the services; saves nothing |
| `Main/Features/ReevesLedger/ReevesLedgerService.cs` | profit lines, step planning, capped lists, the co-op write gate |
| `Main/Features/ReevesLedger/LedgerTypes.cs` | the plain value types in and out of the service |
| `Main/Features/ReevesLedger/ReevesLedgerSettingsProvider.cs` | the two MCM switches, cached lazily (a concrete class) |
| `Client/Features/ReevesLedger/UI/TownManagementLedgerMixin.cs` | title, rows and icon tooltips |
| `Client/Features/ReevesLedger/UI/ReserveStepMixin.cs` | the two buttons |
| `Main/Features/ReevesLedger/UI/LedgerTooltips.cs` | the three tooltips as tooltip property lists |
| `Main/Core/Logging/ScreenFaultLog.cs` | the guard and the once-per-site log (shared) |
| `Client/Features/ReevesLedger/UI/ReevesLedgerIconsPrefab.cs`, `ReserveStepButtonsPrefab.cs` | the two insertions into the `TownManagement` movie |
| `Main/Adapters/IFiefLedgerAdapter.cs`, `FiefLedgerAdapter.cs` | profit inputs, stash, prisoners, patrol |
| `Main/Features/TaomSettings.cs` | `EnableReevesLedger`, `EnableReserveSteps` |
| `Main/Composition/FeatureModules.cs` | the module line |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | the two exclusions |

## Dependencies

- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the single-player write gate.
- `Campaign.Current.Models.ClanFinanceModel` and `SettlementTaxModel`: the profit numbers, through `TaomClanFinanceModel` where it applies.
- The Fief Management F6 screen ([fief-management.md](fief-management.md)): its `Settlement.CurrentSettlement` swap is what makes the step buttons and the rows read the opened fief from F6.
- UIExtenderEx 2.13.3: `[ViewModelMixin]`, `[PrefabExtension]`, `PrefabExtensionInsertPatch` (`InsertType.Append` places the node after its target, as a sibling).

## Tests

- `TAOM.Tests/Features/ReevesLedger/ReevesLedgerServiceTests.cs` (24): the profit lines, total and saturation, the step caps and edge cases, the stash and prison summaries (zero counts, a non-positive cap), the co-op gate, and the Patrol row rule (a town always, a castle with a patrol or an allowing model, nothing else).
- `TAOM.Tests/Features/ReevesLedger/FiefLedgerAdapterPinTests.cs` (7): source pins (never `applyWithdrawals: true`, the model slots, the patrol null guard, the patrol row reads, the list reads).
- `TAOM.Tests/Features/ReevesLedger/ReevesLedgerWiringTests.cs` (21): the module line, the patrol row gated by the service rule, the container graph, the 150-line cap, the guard and latch on every engine-called member, the fault log built first, one settings read per mixin, the confirm order, the two `MayWriteWorld` reads, no slider write but zero, no `IReserveAdapter`, tooltips and hints created once, the vanilla label keys, the two mixins naming their fault sites apart.
- `TAOM.Tests/Features/ReevesLedger/ReevesLedgerPrefabTests.cs` (7): every binding, command and data source in the inserted XML resolves (read from attribute metadata, constructing nothing); each step button is a wrapper whose hint sits outside the button; both brushes and both sprites exist.
- `TAOM.Tests/Features/ReevesLedger/ReevesLedgerTextsTests.cs` (8, tagged `RequiresGame`: it builds `TextObject`s), `TAOM.Tests/Core/Logging/ScreenFaultLogTests.cs` (9), `ReevesLedgerSettingsProviderTests.cs` (5).
- `TAOM.Tests/Migration/ViewModelMixinBindingTests.cs` (2, one of them a `BindingVerification` gate): every `[ViewModelMixin("X")]` in TAOM names a method that exists on its target view model.
- `CampaignHotPathSettingsProvidersTests`, `SettingsFingerprintTests`: the lazy accessor and the setting counts.

## How to change the step size or the tooltip cap

1. Change `ReevesLedgerService.StepSize` (10,000 today).
2. Edit the four English rows that repeat the figure: `taom_reeve_add`, `taom_reeve_withdraw`, `taom_reeve_add_hint` and `taom_reeve_withdraw_hint` in `taom_module_strings.xml` and the inline defaults in `ReserveStepMixin`. The two switch hints in `TaomSettings` carry no digits on purpose.
3. Reset the same four rows in the 12 language files to the new English text, or the translations keep the old figure (an English edit stales 12 languages).
4. To change the stash and prison cap, change `LedgerTooltips.MaxLines`; the tests read the constant.

## Known Limits

- The gamepad cannot reach the new buttons and icons. The prefab gives them no navigation index; this is accepted.
- A step presses one of two fixed sizes. -10K on a reserve under 10,000 empties it in full.
- The Profit tooltip's Settlement Tax line truncates the model's figure while the window's own row rounds it, so the two can differ by 1.
- Under co-op the buttons are hidden, not greyed. A solo player with a co-op mod TAOM cannot probe gets no buttons either (same as From the Ashes).
- After a language change while the window is open, the button labels keep the old language until the window reopens.
- `RefreshValues` writes "Manage" back to the title if anything calls it after construction; nothing in the vanilla town management view does.

### In-game checklist (owed)

Layout and the hover and click behaviour of the new widgets are unseen in game.

- Five 38 px rows fit the 220 px top half (the left column now has five rows) [Likely].
- The icon offsets (+-95, margin top 100) against a town with many shops and many bound villages.
- A long fief name in the 590 px title tab.
- The rows survive a governor change, a queue change and a reserve confirm, in town and from F6.
- -10K refunds exactly and the reserve never goes negative, with a reserve that is not a multiple of 100.
- The Profit figure matches the clan finance tooltip lines for that fief.
- A hint shows on the disabled +10K (0 gold) and -10K (empty reserve) buttons, and the normal hint shows on an enabled one.
- Both MCM switches off: the next open shows "Manage", no rows or icons; the step switch alone hides only the buttons.
- The F6 step buttons move that fief's reserve and the purse, and each row appears once after a press.
- Tooltip content: more than 15 stacks shows the more line; a captured lord is listed first; the empty markers show.
- Both columns have five rows in a town.
- `ButtonBrush2` extends 22 px each side: check the step buttons do not overlap the plus button.
- A town whose patrol behaviour is missing shows a Patrol row with an empty tooltip and no error.
- A castle with a live patrol, or one the patrol model allows a patrol, shows the Patrol row and its tooltip; any other castle shows no Patrol row (a built Guard House with Castle Patrols off and no patrol is one of those); both have both icons. A town's row is unchanged.
- The button positions beside the plus button.

## Changelog

- 2026-10-19: Reeve's Ledger added (#857).
- 2026-10-24: the Patrol row also shows for a castle with a live patrol or one the patrol model allows a patrol (#992).

## GitHub Issue

- **Issue:** [#857](https://github.com/haterade22/TAOM/issues/857) Town management extras: profit, patrol, reserve buttons, stash and prison tooltips
- **Status:** Shipped on `bannerlord-1.5.x` in `e7cdb2e96` (v2.0.35); its fault log became the shared `ScreenFaultLog` in
  `69b47d00b`. Issue open: the in-game check is owed; its strings are translated in all 12 languages (2026-11-12), tracked in [#931](https://github.com/haterade22/TAOM/issues/931).

---

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/fief-management.md](./fief-management.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
