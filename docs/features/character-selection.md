# Character Selection

> **Used by the Player Switcher (#514).** The transpiler below is what makes a non-human lord's
> character-creation preview render with a race-appropriate action set instead of a bind pose, so
> the picker in [player-switcher.md](player-switcher.md) depends on it staying alive. Do not retire
> it without checking that feature first.


## Overview
Character Selection fixes the character body generator view so that the face-generation preview uses the correct action set for the character's race. Without this patch, all races default to the human facegen animation set, causing visual errors or crashes in the character customisation screen for non-human player races.

## Why This Exists
- **Vanilla behavior:** `BodyGeneratorView.RefreshCharacterEntityAux` constructs an `AgentVisualsData` and sets the action set using the default human action set, without consulting the character's race.
- **TAOM requirement:** TAOM supports multiple playable races (e.g., Elf, Dwarf, Orc) each with distinct skeletons and facegen animations. The body generator must use the race-appropriate `_facegen` action set so the preview renders correctly.
- **Without this feature:** The character customisation screen may crash or display broken facegen animations when the player character is a non-human race.

## Architecture

### Design Challenge
`BodyGeneratorView.RefreshCharacterEntityAux` is a private method with no override point. The action set is set inline immediately after `new AgentVisualsData()` is constructed — there is no virtual method or event to intercept cleanly through a Postfix. The only way to insert the race-aware action set at the right position in the call sequence is to modify the IL directly.

### Solution Approach
A Harmony Transpiler (`RefreshCharacterEntityAuxPatch`) finds the `newobj AgentVisualsData` IL instruction and inserts three additional instructions immediately after it:
1. `Ldarg_0` — push `this` (the `BodyGeneratorView`) onto the stack
2. `Call GetActionSet` — call the static helper which reads `bodyGeneratorView.BodyGen.Race`, looks up the base monster via `FaceGen.GetBaseMonsterFromRace`, and returns the race-appropriate `_facegen` action set via `MBGlobals.GetActionSetWithSuffix`
3. `Callvirt AgentVisualsData.ActionSet` — chain the action-set setter onto the just-constructed `AgentVisualsData`

The patch is registered under the `Late_Transpiler` Harmony category, ensuring it runs after other patches that may alter the same method.

### Component Diagram
```
BodyGeneratorView.RefreshCharacterEntityAux  (Harmony Transpiler)
  IL: newobj AgentVisualsData
  IL: [INSERTED] ldarg_0
  IL: [INSERTED] call RefreshCharacterEntityAuxPatch.GetActionSet(BodyGeneratorView)
  IL: [INSERTED] callvirt AgentVisualsData.ActionSet(MBActionSet)
  ...rest of original IL...

GetActionSet(BodyGeneratorView):
  FaceGen.GetBaseMonsterFromRace(bodyGeneratorView.BodyGen.Race)
  MBGlobals.GetActionSetWithSuffix(monster, isFemale, "_facegen")
  => MBActionSet
```

## Configuration
None.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs` | Harmony Transpiler; inserts race-aware action set into `BodyGeneratorView.RefreshCharacterEntityAux` IL |

## Dependencies
- `TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace` — maps race index to base monster
- `TaleWorlds.MountAndBlade.MBGlobals.GetActionSetWithSuffix` — resolves the `_facegen` action set for a monster/gender combination
- `HarmonyLib` — for `[HarmonyTranspiler]` and IL manipulation

## Tests
No unit tests exist for `CharacterSelection` in `TAOM.Tests/Features/`. The patch modifies generated IL and its correctness depends on the Bannerlord runtime; unit testing a Transpiler requires loading the game assemblies and is not covered.

## How to Update the Action Set Suffix
The suffix `"_facegen"` is hardcoded in the `GetActionSet` static method inside `RefreshCharacterEntityAuxPatch`. To change the action set used for preview, update the string literal in that method. No IL changes are required — only the `GetActionSet` helper logic needs changing.

## Changelog
- 2026-05-14 — Made the `RefreshCharacterEntityAuxPatch` transpiler soft-fail (log + return unmodified IL) instead of throwing `ArgumentException` on a missing ctor / ActionSet setter / IL pattern, so a missed anchor no longer bricks startup (closes #160).

## GitHub Issue
- **Issue:** Unknown (introduced in commit `6a2611e` — "add late patches for character tableau and action set generation, improve race handling")
- **Status:** Unknown

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/player-switcher.md](./player-switcher.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
