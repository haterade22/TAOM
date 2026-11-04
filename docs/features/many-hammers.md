# Many Hammers

## Overview

In the smithy, holding Shift while you press Smelt or Refine does five units at once, and holding Ctrl does the whole stack (Ctrl wins when both are held). Smelting stops when your stamina or the charcoal runs out, and refining stops when your stamina or the input materials run out. Weapons you locked in the inventory no longer appear in the smelting list, so they cannot be smelted by accident; one you lock inside the smithy leaves the list at your next smelt or tab switch. Two MCM switches, both on by default, turn the pieces off.

## Why This Exists

- **Vanilla behavior:** one click on Smelt or Refine does one unit. A stack of thirty axes is thirty clicks, each followed by a full list rebuild. A weapon locked in the inventory still sits in the smelting list; vanilla only asks "Are you sure you want to smelt this weapon? It is locked in the inventory."
- **TAOM requirement:** smithing is a routine of repeated clicks for every player who crafts, and TAOM's Erebor and Isengard feats change the stamina cost of a unit, so a bulk action has to ask the smithing model for each unit's cost instead of assuming one.
- **Without this feature:** a stack of loot is a long click chain, and a locked heirloom (the LOTRLOME Armory ships several) is one stray click from the furnace.

## Architecture

### Design Challenge

- **Vanilla does not check cost.** `CraftingCampaignBehavior.DoSmelting` and `DoRefinement` (`CraftingCampaignBehavior.cs:815-862`) subtract materials and stamina and floor stamina at 0 without asking whether the hero can afford it. The only gates are in the screen: `CraftingVM.HaveEnergy` (stamina above 10, `CraftingVM.cs:1038`) and `HaveMaterialsNeeded` (no material would go negative, `:1043-1046`, costs from `:845-849`). A loop that called `DoSmelting` until the screen said no would have to rebuild that screen state itself.
- **The model, not a constant.** The energy of one smelt is `SmithingModel.GetEnergyCostForSmelting(item, hero)`, of one refine `GetEnergyCostForRefining(ref formula, hero)`, and the charcoal a smelt needs is the negative slot of `GetSmeltingOutputForItem(item)`. Reading them for each unit means `TaomSmithingModel`'s cultural feats reach bulk, and so does any cap on the smelt output (#910), because both live behind the model slot. This feature never touches `TaomSmithingModel`.
- **Prices are read again for every unit.** In v1.5.6 a unit's price depends on a perk the player picks (Practical Smelter, Practical Refiner), TAOM's culture feats and the career passive, none of which a single smelt changes on its own; the engine grants a unit's XP before it prices it. The plan is still redone from live numbers before every extra unit, which is cheap and keeps the loop right if a later model prices by skill.
- **Private view-model members.** `SmeltingVM` keeps its behavior, its lock list and its callbacks private. Nothing here reflects into them: the behavior is fetched the way the view model fetches it (`Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>()`, the same instance), and the rows are read through public members.

### Solution Approach

**Bulk.** Two void prefixes (`Patch121`, in TAOM.Client because `SmeltingVM` and `RefinementVM` are in the client-only `ViewModelCollection` assembly) run the extra units and let the original method run. Vanilla then does the last unit, rebuilds the list, re-sorts, reselects and refreshes the screen (`SmeltSelectedItems`, `SmeltingVM.cs:265-280`; `CraftingVM.UpdateAll`), so no refresh code of TAOM's can drift from the engine's. `ManyHammersService.RunExtras` plans from a `UnitSnapshot` (stamina, the unit's cost, what one unit does to each of the nine materials, the party's stock, the stack size) and runs `ICraftBenchUnit.DoOne` for every unit but the last:

1. `BulkCraftRules.PlanUnits` simulates unit by unit. A unit runs only while stamina is above 10 and at least the model's cost, and no material goes below zero. The stack and a fixed cap of 999 bound the count (a cost-free feat or an unbounded formula would otherwise never stop). A negative cost counts as zero.
2. The opening plan fixes how many units fit; one is reserved for vanilla. Before each extra unit the snapshot is read again and the next two units are planned; the loop continues only while both fit (this one and vanilla's).
3. The opening plan also bounds the loop, so a snapshot that never moves (the co-op guest shape) cannot run it away.
4. A throw is logged once and ends the loop; vanilla still does its unit. Because two units must fit before an extra runs, a half-finished `DoSmelting` still leaves a weapon for vanilla's call.

Shift and Ctrl are read with `Input.IsKeyDown`, left and right keys together; TAOM has no input seam for them. Ctrl on a smelt means the selected stack; on a refine, which has no stack, it means as many runs as the stamina and the materials allow.

**Locked items.** A postfix on `SmeltingVM.RefreshList` (the constructor, every smelt, each switch to the Smelting tab, and BannerlordCoop's refresh on a client) drops the rows whose `IsLocked` is set. It asks `BulkCraftRules.SelectionAfterHidingLocked` what to do with the selection: when no row is left it clears any selection, because vanilla's reselect after a smelt would otherwise hand a null to its selection handler (`SmeltingVM.cs:273-277`); a locked selection moves to the first row left of the same weapon, else the first row left. A row locked inside the smithy is not removed by the click: its lock icon lights, the row stays, and the next smelt or switch of tabs rebuilds the list without it. `ManyHammersService.DesiredCount` answers one for a locked selection, so on a locked selection Shift and Ctrl do nothing more: vanilla asks its confirmation question ("Are you sure you want to smelt this weapon? It is locked in the inventory."), Yes smelts exactly one, and No smelts nothing.

**Co-op.** A guest (`ICoopSessionProvider.ShouldDeferToHost`) gets a count of one: its roster and stamina belong to the host. The locked-row filter stays on for everyone, because it only hides a row in the local list.

### Component Diagram

```
Smelt / Refine button (CraftingVM.ExecuteMainAction)
        |
  Patch121 prefix (void, try/catch)  --  Input.IsKeyDown (Shift, Ctrl)
        |
  ManyHammersService  --  IManyHammersSettingsProvider (MCM), ICoopSessionProvider
        |   RunExtras: plan, re-read, DoOne, repeat
  BulkCraftRules (pure)         ICraftBenchUnit
                                 |-- SmeltBenchUnit  --> ICraftingCampaignBehavior.DoSmelting
                                 '-- RefineBenchUnit --> ICraftingCampaignBehavior.DoRefinement
                                      reads: SmithingModel costs, party roster, hero stamina
        |
  vanilla then does the last unit and every refresh

SmeltingVM.RefreshList
        |
  Patch121 postfix  -->  SmeltingListFilter  -->  BulkCraftRules.SelectionAfterHidingLocked
```

## Configuration

### MCM group "Smithing" (GroupOrder 74, shared with the smelt-output cap)

| Setting | Type | Default | Notes |
|---|---|---|---|
| Bulk Smelting and Refining | bool | on | Shift does five, Ctrl the stack. Read at each click, no restart. A co-op guest ignores it |
| Hide Locked Items from Smelting | bool | on | Read at each list rebuild, no restart. A list already on screen changes at its next rebuild |

Only a group's first property carries `GroupOrder` (MCM ignores it on any later one); the two settings take `Order` 10 and 11 so they sit after the group's other features. Both are new settings, so the default reaches every install. The defaults live in `ManyHammersSettingsProvider`, which also serves them before MCM is up; both are plain booleans, so there is nothing to clamp. Both are excluded from the co-op fingerprint in `CoopSettingsRelevance`: the bulk switch is player-local (it gates the local player's own click) and the hide-locked switch is presentation (it filters one client's list).

### Player text

No new in-game text. Four MCM strings (two labels and two hints) carry `{=KEY}` ids: `taom_mh_mcm_bulk`, `taom_mh_mcm_bulk_hint`, `taom_mh_mcm_hide_locked`, `taom_mh_mcm_hide_locked_hint`. The keys are registered in `taom_module_strings.xml` and seeded with the English default in all 12 language files; only the translation is owed (#763). `ManyHammersWordingTests` holds each language row to the current English.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ManyHammers/BulkCraftRules.cs` | Pure rules: count from the keys, the unit plan, the selection after locked rows go |
| `Main/Features/ManyHammers/ManyHammersService.cs` | Co-op, switch and locked-selection gates (`DesiredCount`), `RunExtras` (plan, re-read, run, stop) |
| `Main/Features/ManyHammers/UnitSnapshot.cs`, `LockFilterResult.cs` | Plain-number inputs and results of the rules |
| `Main/Features/ManyHammers/ManyHammersSettingsProvider.cs`, `IManyHammersSettingsProvider.cs` | The two MCM switches |
| `Main/Features/ManyHammers/ManyHammersModule.cs` | Registrations and the `Patch121_ManyHammers` category (one line in `FeatureModules.cs`; `IoC.cs` and `SubModule.cs` untouched) |
| `Main/Adapters/ICraftBenchUnit.cs`, `SmeltBenchUnit.cs`, `RefineBenchUnit.cs`, `CraftBenchStock.cs` | One unit of work over the engine's crafting behavior, the model and the roster |
| `Client/Features/ManyHammers/Hooks/Patch121_*.cs` | The three patches, each a few lines |
| `Client/Features/ManyHammers/Hooks/ManyHammersCalls.cs`, `SmeltingListFilter.cs` | Shared service, key reads, log-once; the in-place list filter |
| `Main/Features/TaomSettings.cs` | `ManyHammersBulkEnabled`, `ManyHammersHideLockedEnabled` |

## Dependencies

- `ICoopSessionProvider` (CoopInterop): `ShouldDeferToHost` for the bulk count.
- `IModLogger` (kernel): the loop's and the patches' fault log.
- `SmithingModel` (the installed model, `TaomSmithingModel` in a TAOM game): read per unit through `Campaign.Current.Models`.

## Tests

- `TAOM.Tests/Features/ManyHammers/BulkCraftRulesTests.cs`: the count per modifier, both held, disabled, an empty or huge stack; the plan stops on stamina (at the reserve, below the cost), on charcoal, on the stack, on the ask; refining stops at the scarcer of two inputs; an output that refills an input counts; zero cost and zero inputs hit the cap; a negative cost reads as zero; nothing asked for, no snapshot, overflow-sized numbers; the snapshot is copied and normalised; `SelectionAfterHidingLocked` leaves an unlocked selection, moves or clears a locked one, clears when no row is left, prefers a row of the same item, and tolerates a null list and a short same-item list.
- `ManyHammersServiceTests.cs`: the switch and the co-op guest gate, a locked selection and a switched-off bulk both count one, `RunExtras` leaves one unit for vanilla, stops with stamina, charcoal and the stack, makes no call for a plan of one, stops early on a live snapshot that drains faster than the model said, is bounded when the snapshot never moves, stops at the cap for an unbounded ask, logs and does not rethrow on a throw in `Read` or `DoOne`, survives a throwing logger.
- `ManyHammersSettingsProviderTests.cs`: the compiled defaults, the MCM defaults matching them, pass-through; the two attributes carry their `{=taom_mh_*}` keys and `RequireRestart` false; the bulk hint states `BulkCraftRules.ShiftCount` in words and both hints state the default; the labels are Title Case.
- `ManyHammersWordingTests.cs`: the English rows equal the attribute literals, and each of the 12 language files holds one row of each `taom_mh_*` key equal to the current English.
- `SmeltingListFilterTests.cs` (`RequiresGame`, real view models built bare): every row locked with an unlocked selection that is not in the list leaves an empty list and no selection (the review's HIGH case); a locked selection moves to the unlocked row of the same item; an unlocked selection stays.
- `BenchUnitAdapterTests.cs`: the refining net effect (inputs take, outputs give, a zero or negative count touches nothing, a same-slot output nets against its input; the engine formula type makes these `RequiresGame`), null refusal, and source pins that the numbers come from the model, the roster and the behavior, never from `TaomSmithingModel`.
- `ManyHammersWiringTests.cs`: the module is listed once and declares the category at GameInit; the services resolve; the three classes carry their target and category; the index holds them from TAOM.Client and none without a client; the targets resolve with the parameter names Harmony injects by (`BindingVerification`); every patch body has a try/catch and stays under 150 lines; both prefixes return void; no reflection into the view models.
- Also gated elsewhere: `HarmonyPatchBindingTests` (every target resolves), `PatchCategoryNumberTests` (registry heading), `CoopVetoClassificationTests` (scans bool prefixes only; these are void), `SettingsFingerprintTests` and `ServerSettingsSchemaTests` (the two new settings).

## How to change the cap or the Shift count

1. Shift's count is `BulkCraftRules.ShiftCount` and the per-click ceiling is `BulkCraftRules.MaxUnits`.
2. The stamina floor (10) is `BulkCraftRules.ReservedStamina`; it mirrors the smithy button and should move only if the engine's `HaveEnergy` does.

## How to verify in game

Not yet confirmed in the running game:

1. Mod Options shows "Smithing" with both switches on.
2. Smelt a stack of five or more weapons: Shift smelts five; Ctrl smelts the whole stack. The list and the material totals refresh once, after the last unit. With only a few charcoal, it stops when the charcoal is gone, and the last unit is vanilla's.
3. Run stamina low (a hero with Erebor or Isengard feats, and one without): a bulk stops with stamina above 10 left or exactly when the next unit would not be paid, never below zero.
4. Refine with Shift and with Ctrl: Ctrl runs until the scarcer input or the stamina is out.
5. Hold both keys: Ctrl's count wins.
6. Lock a weapon in the inventory, open the smithy: it is not in the list. Click a row's lock icon while the smithy is open: the icon lights and the row stays; smelt or switch tabs and it leaves. Lock the selected row: it stays selected until the next rebuild, then moves.
7. With a locked weapon in the party, select the last unlocked row and smelt all of it: the list is empty, nothing is selected, no error (the review's HIGH case).
8. Turn each switch off in Mod Options and reopen the smithy: Shift and Ctrl do one unit; locked weapons appear again.
9. Hold Ctrl on a locked selection: vanilla's question comes first, Yes smelts exactly one, and No smelts nothing. Check it with the hide switch on (lock the selected row in the smithy) and off (the locked weapon is in the list).
10. In a co-op session as a guest: Shift does one unit (UNVERIFIED with two peers). Locked weapons are hidden, and smelting the last unlocked weapon leaves an empty list with no error after the host's refresh.

## Known limits

- A row locked inside the smithy stays visible until the next smelt or tab switch; only the `RefreshList` postfix hides rows.
- A refining formula that names one material as both input and output would be planned on the net amount, more generous than the smithy's own per-slot check. No formula in the game or TAOM does this.
- Each extra unit grants its crafting XP and raises the engine's own events (`OnEquipmentSmeltedByHero`, `OnItemsRefined`) once per unit, as repeated clicks would.
- A switch turned off while the smithy is open takes effect at the next click (bulk) or the next list rebuild (locked rows).

## Changelog

- 2026-10-26: added (#911).

## GitHub Issue

- **Issue:** #911: Bulk smelting and refining, and locked items hidden from smelting
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
