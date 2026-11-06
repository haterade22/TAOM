# Tavern Specialities

## Overview

The tavern maid offers default dishes, a mug of ale with a bowl of stew, in a town whose culture vanilla has no dishes
for. Vanilla's seven cultures keep their own dishes.

## Why This Exists

- **Vanilla behavior:** `TavernEmployeesCampaignBehavior.conversation_tavernmaid_offers_food_on_condition` (private,
  v1.4.8, `SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior.cs:729-820`) sets the `DRINK` and `FOOD` text
  variables inside seven culture branches, `vlandia`, `empire`, `khuzait`, `aserai`, `sturgia`, `battania` and `nord`,
  and nothing for any other culture. It returns true whatever the culture, once the speaker is a tavern maid and no
  drink has been ordered in this settlement today.
- **TAOM requirement:** the maid names a dish and a drink in every town.
- **Without this feature:** in a TAOM-culture town the variables keep their last value, so the maid offers the
  previous town's dishes, or a blank where none was ever set (#937).

## Architecture

### Design Challenge

A postfix would have to decide whether vanilla set the variables, which means keeping a copy of its seven culture ids
and a binding test to keep the copy honest. A pre-set avoids the question: each of vanilla's seven branches sets both
variables, so vanilla overwrites the pre-set for exactly its own ids, and for every other culture the pre-set stands.
Nothing else reads the two globals. No culture list is needed here, and a culture vanilla adds later is handled by
vanilla itself.

### Solution Approach

A void Harmony prefix, Patch111, category `Patch111_TavernSpecialities`, applied at GameInit. It sets `DRINK` to
`{=taom_tavern_drink_default}a mug of ale` and `FOOD` to `{=taom_tavern_food_default}a bowl of stew` before vanilla
runs, and never skips the original. The prefix runs at every conversation start that reaches the line, which is cheap:
two text variables. The two keys are registered in the module strings and not yet translated. An unset variable renders
blank and the line still shows, so a missed pre-set degrades to the old behavior.

### Component Diagram

```
conversation_tavernmaid_offers_food_on_condition
   prefix: DRINK = a mug of ale, FOOD = a bowl of stew
   vanilla: seven culture branches overwrite both for their own ids
```

## Configuration

None.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TavernSpecialities/Hooks/TavernEmployeesCampaignBehavior_OffersFood_TavernSpecialities_Patch.cs` | The prefix |
| `Main/Features/TavernSpecialities/TavernSpecialitiesModule.cs` | Patch category declaration |

## Dependencies

None.

## Tests

- `TAOM.Tests/Features/TavernSpecialities/TavernSpecialitiesBindingTests.cs`: `BindingVerification` for the target resolving and the category applying and JIT-compiling with a prefix attached and no postfix; the patch class being a prefix; the category literal
- `TAOM.Tests/Features/TavernSpecialities/TavernSpecialitiesWiringTests.cs`: module listing, category id and phase

## How to give a culture its own dishes later

Vanilla's branches stay the source for its seven cultures. For a TAOM culture, add a branch to the prefix keyed on
`MobileParty.MainParty.CurrentSettlement.Culture.StringId` that sets the two variables for that id, with new `taom_*`
keys, registered and translated.

## In-game check

Talk to the tavern maid in a Gondor town: she offers a mug of ale with a bowl of stew. In a Rohan town she keeps ale
and a Sunor sausage.

## Changelog

- 2026-10-19: added (#937).
- 2026-11-05: ported to the 1.4.x line (plan 133); the module runs through `Main/Composition` (plan 105).

## GitHub Issue

- **Issue:** #937 Tavern maids offer stale or empty dishes in TAOM-culture towns
- **Status:** ported to 1.4.x; in-game check owed
