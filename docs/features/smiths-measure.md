# Smith's Measure

## Overview

Smelting a weapon now returns at most one of each iron grade, of iron ore and of hardwood (the cap is an MCM slider, 1 to 10), and no hardwood at all when any metal comes out. The charcoal cost is untouched. The refinement tab of the smithing screen gains a Make Tools card: one iron and one hardwood make one Tools good, for smithy stamina and some Smithing experience, with a hover text that says what is missing and what the recipe costs. Both pieces ship together under #910 and sit in one MCM group, "Smithing".

## Why This Exists

- **Vanilla behavior:** `DefaultSmithingModel.GetSmeltingOutputForItem` (v1.5.5, `:178`) adds up the material of every crafting piece, moves one unit of the highest grade present down one grade (`AddSmeltingReductions`, `:336`) and charges one charcoal. A weapon made of several large pieces therefore returns several of a grade. There is no tools recipe on the refinement tab.
- **TAOM requirement:** the heavy loot of the Armory's weapons makes "pick up, smelt, sell the iron" a money loop, so the yield needs a ceiling. Tools, a trade good the economy already prices, gives surplus iron and wood a small use at the smithy.
- **Without this feature:** a large looted weapon smelts into several of a grade, and the refinement tab has nothing to do with plain iron and wood.

## Architecture

### Design Challenge

- **The cap belongs in the model, not a patch.** Three callers read the smelting output: the smelt itself (`CraftingCampaignBehavior.cs:848`), the smelting list's per-item yield (`SmeltingItemVM.cs:197`) and the selected item's preview (`CraftingVM.cs:845`). `GetSmeltingOutputForItem` is a public override on `DefaultSmithingModel`, and `TaomSmithingModel` already inherits it, so one override changes what the player is shown and what the player receives.
- **The model is built in `SubModule.cs`** with two services. The override needs a third (the settings), and a constructor parameter would need an edit to that single-owner file, so the model resolves its provider once in its constructor, as `TaomPartySizeModel` does for its patrol hook.
- **A refining formula cannot name Tools.** `Crafting.RefiningFormula` takes `CraftingMaterials` values for its output and Tools is not one. The cost call (`GetEnergyCostForRefining`, which the vanilla model answers with 6 and the Practical Refiner perk, `:140`) only needs the formula to exist, so the adapter passes one iron, one hardwood and no output.
- **The stacked reductions can reach zero.** TAOM's smithing feats and career passive reduce the cost through an `ExplainedNumber` that has no lower limit, so the model's answer can be 0 or below. The service floors the cost at 1.
- **The seam on the screen is a private method.** `CraftingVM.RefreshEnableMainAction` (`:963`) runs from `UpdateAll` (`:925`, after every tab switch) and directly from a hero change (`:1059`), a refinement selection (`:1287`) and a smelt selection (`:1293`). A UIExtenderEx mixin hooks it by name; `ViewModelMixinBindingTests` fails if the method is renamed or overloaded.

### Solution Approach

No Harmony patch, no campaign behavior, nothing of TAOM's saved: the roster and the crafting stamina are the engine's own saved state.

1. **The cap.** `TaomSmithingModel.GetSmeltingOutputForItem` calls the base, then `SmeltingYieldCap.Apply` when the "Cap Smelting Yield" setting is on. The rule works on the engine's array (iron ore, six iron grades, hardwood, charcoal): slots 0 to 6 are lowered to the cap; hardwood is dropped when any of them is positive, else lowered to the cap; charcoal and every negative number are left alone. It only lowers a value, so it can never give more than the engine would, and it works on a copy.
2. **Make Tools.** `MakeToolsService` answers a verdict (disabled, needs iron, needs hardwood, too tired, ready), the stamina cost and the experience. The cost is the model's refining cost for the recipe, floored at 1, and the gate is vanilla's own: every smithy action needs stamina above 10 (`CraftingVM.cs:1038`, and a co-op host refuses at 10 or less), so the verdict is too tired when the stamina is 10 or less or under the cost. The experience is 30 percent of the value added (Tools minus the two inputs, 250 less 60 less 25 at the base values, so about 50), never below 0. A press takes the inputs, gives the Tools, spends the stamina and pays the experience.
3. **The card.** `MakeToolsMixin` (a `BaseViewModelMixin<CraftingVM>` hooked on `RefreshEnableMainAction`) converts the current crafting hero to `ICraftingWorkshopAdapter` and shows the card on the refinement tab only. After a press it calls the view model's own public `ExecuteSwitchToRefinement`, which runs `UpdateAll` and so refreshes the material counts, the stamina bars and the card. `MakeToolsPrefab` appends the card as a sibling after the materials list, at the left edge with a margin of 200 so it clears the crafting-hero toggle (x 60 to 190). The card is vanilla's main-action button with its own brushes (`Crafting.MainAction.Button` and `.Text`), so it names no sprite of TAOM's. The hover shows in both states: a hint widget only relays its parent's hover and a disabled widget is skipped by hit-testing, so one tooltip view model feeds two hints, a sibling of the button (explains the greyed state, as vanilla's main action does) and a size-less one inside it (the enabled state, as vanilla's Reset Angle button does). The card root stays hoverable for that reason. The too-tired line is vanilla's own translated `{=PRE5RKpp}`, which the screen already shows for the same rule, so TAOM carries no row for it.
4. **Items come from the model.** The adapter asks `SmithingModel.GetCraftingMaterialItem` for the iron (third grade) and the hardwood, and takes the Tools good from `DefaultItems.Tools`, so no item id is spelled in code.

### Component Diagram

```
TaomSettings (MCM group "Smithing")
        |
SmithsMeasureSettingsProvider (clamps, one warning per bad cap)
        |                          \
TaomSmithingModel                   MakeToolsService  (verdict, cost floor, experience)
  GetSmeltingOutputForItem              |
        |                        ICraftingWorkshopAdapter  ->  CraftingWorkshopAdapter (hero, roster, stamina, model)
SmeltingYieldCap.Apply                  |
                                 MakeToolsMixin + MakeToolsPrefab  (TAOM.Client)
```

## Configuration

MCM group "Smithing" (GroupOrder 74, shared with the bulk smelting setting of #911, whose properties take Order 10 and up). All three are new settings, so their defaults reach every install, and none needs a restart. The text is the `{=KEY}English` form.

| Setting | Type | Default | Effect |
|---------|------|---------|--------|
| Cap Smelting Yield | bool | on | Turns the cap on. Off leaves the engine's smelting output untouched. |
| Smelting Yield Cap | int, 1 to 10 | 1 | The most of one iron grade, of iron ore and of hardwood a smelt returns. A value outside the range (a hand-edited MCM json) reads as 1, with one warning in the log per distinct bad value. |
| Enable Make Tools | bool | on | Shows the Make Tools card. Off hides it; the press refuses as well. |

The two cap settings count as simulation-relevant for the co-op fingerprint (the cap changes what a smelt returns). The Make Tools switch is player-local (`SmithsMeasureMakeTools` in the `PlayerLocal` set of `CoopSettingsRelevance`): the card is not shown in a co-op session and a dedicated server never loads Client, so no peer reads it. Under co-op the host's smelt reads the host's cap settings, so a client whose cap differs sees one yield in the preview and receives another.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SmithsMeasure/SmithsMeasureModule.cs` | Registers the provider and the service; listed once in `Main/Composition/FeatureModules.cs` |
| `Main/Features/SmithsMeasure/SmeltingYieldCap.cs` | The pure cap on the engine's output array |
| `Main/Features/SmithsMeasure/MakeToolsService.cs` | Verdict, cost floor, experience, the press |
| `Main/Features/SmithsMeasure/SmithsMeasureSettingsProvider.cs` | The clamped MCM reads |
| `Main/Features/SmithsMeasure/SmithsMeasureTexts.cs` | The player-facing lines of the card (the too-tired line is vanilla's key) |
| `Main/Features/CulturalFeats/Models/TaomSmithingModel.cs` | The `GetSmeltingOutputForItem` override |
| `Main/Adapters/ICraftingWorkshopAdapter.cs`, `CraftingWorkshopAdapter.cs` | The hero, the party's stores and the stamina as plain values |
| `Client/Features/SmithsMeasure/UI/MakeToolsMixin.cs` | The card's view model |
| `Client/Features/SmithsMeasure/UI/MakeToolsPrefab.cs` | The inserted widget |
| `Main/Features/TaomSettings.cs` | The three properties |

## Dependencies

- `ISmithsMeasureSettingsProvider` (this feature): the three MCM reads.
- `ICraftingWorkshopAdapter` (Adapters): `Hero`, `MobileParty.MainParty.ItemRoster`, `ICraftingCampaignBehavior` and `SmithingModel`.
- `TaomSmithingModel` keeps its existing energy-cost overrides (the five smithing feats: Erebor, Isengard, Mordor, Goblin and Misty Mountain Orcs, and the career passive `SmithingCostReduction`), and Make Tools reads that cost through them, so those reductions apply to the button too. There is no separate stamina setting and no new feat.

## Tests

- `SmeltingYieldCapTests`: each grade, hardwood with and without metal, charcoal and negatives untouched, a short array, a cap of 3, cap off, a cap under 1.
- `MakeToolsServiceTests`: each missing input, too little stamina (10 or less is too tired), the cost floor at 1 for 0 and negative model costs, the experience from the value added (never negative, no overflow), the press and its refusals.
- `SmithsMeasureSettingsProviderTests`: every rejected cap with one warning, the defaults when MCM is down.
- `TaomSmithingModelCapTests`: the model caps through the provider and returns the engine's array when off.
- `MakeToolsMixinTests`, `MakeToolsPrefabTests`: the card follows the tab and the hero, a fault hides it and logs once, every binding resolves, both hints and the hoverable root, the margin, the XPath and insert type are checked against the installed vanilla `Crafting.xml`, the brushes exist, no sprite is named.
- `SmithsMeasureWiringTests`, `SmithsMeasureWordingTests`: the module is listed once, `IoC.cs` and `SubModule.cs` are untouched, no Harmony patch, the English rows equal the literals, every language file holds one current row of each key, and the vanilla too-tired key has no TAOM row.
- `CampaignHotPathSettingsProvidersTests` carries the provider's rows.

## How to Retune

1. The cap: change "Smelting Yield Cap" in MCM; it applies to the next list refresh or smelt.
2. The experience share or the minimum cost: `MakeToolsService` (`XpShareOfValueAdded`, `MinimumStaminaCost`).
3. The recipe: `CraftingWorkshopAdapter` (`IronGrade`, `Hardwood`) and the sentence in `SmithsMeasureTexts`.

## How to verify in game

1. Smelt a large weapon with the cap on, then off: the list yield, the preview and the roster all show the same capped numbers.
2. A weapon whose pieces return iron: no hardwood comes back. A weapon of wood alone returns one hardwood.
3. Open the refinement tab: the Make Tools card sits at the lower left, above the materials strip, clear of the crafting-hero portrait and toggle, the refinement panel and the strip's change texts at 1080p and at one other resolution; it is hidden on the Forging and Smelting tabs.
4. With no iron, then no hardwood, then low stamina, the card is greyed and the hover text names the one missing thing and the recipe. Hover a greyed card (the reason and the recipe) and an enabled one (the recipe and the cost). With stamina at 10 the card is greyed with vanilla's rest line ("You must rest and spend time before you can do this action.").
5. A press adds one Tools, removes one iron and one hardwood, lowers the stamina bar by the cost and raises Crafting experience; the counts and the card refresh at once.
6. Switch the crafting hero to a companion: the card reads that hero's stamina.
7. The Practical Refiner perk and a feat or passive reduce the cost shown in the hover text.
8. Under a co-op module the card is absent.

## Known limits

- **Balance.** At the engine's base values the recipe turns goods worth 85 into a Tools worth 250, so a craft adds about 165 of trade value for a few stamina. Whether that is a sink or a source depends on what the towns pay for Tools and what iron and hardwood cost to buy; it is a balance call for the maintainer, and the experience share and the cost are the knobs.
- **A press keeps the refinement choice.** The refresh goes through `ExecuteSwitchToRefinement`, which re-checks each refinement action's inputs and keeps the selected one (it picks the first enabled action only when none is selected). An action whose inputs the press used up stays selected but greyed.
- **The cost can be driven to 1.** The service floors the model's cost at 1, so a stack of reductions makes the craft nearly free in stamina rather than free.
- **The stacked reductions on the existing energy costs.** `ExplainedNumber` has no lower limit, so the three existing energy-cost overrides can already return 0 or below. That is not changed here.

## Changelog

- 2026-10-26: added (#910).

## GitHub Issue

- **Issue:** [#910 Smith's Measure: capped smelting yield and a Make Tools button](https://github.com/haterade22/TAOM/issues/910)
- **Status:** Committed in 42148af4c (v2.0.35) and on `origin/bannerlord-1.5.x`; in-game checks owed.
