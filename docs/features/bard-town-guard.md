# Bard Town Guard

## Overview

The tavern musician's conversation no longer throws in a town whose culture owns no other town. One transpiler swaps
the engine's random town pick for a TAOM pick that runs vanilla's pick first and only steps in when it found nothing.
The musician's lyrics are vanilla's, untouched.

## Why This Exists

- **Vanilla behavior:** `TavernEmployeesCampaignBehavior.conversation_talk_bard_on_condition` (private, v1.4.8,
  `TavernEmployeesCampaignBehavior.cs:641-665`; the pick is at `:650` and `.Name` is read at `:651`) picks a town with
  `Settlement.All.GetRandomElementWithPredicate(x => x.IsTown && x.Culture == Settlement.CurrentSettlement.Culture && x != Settlement.CurrentSettlement)`
  and reads `.Name` of the result for the `RANDOM_TOWN` text variable. `GetRandomElementWithPredicate` returns `default`
  when nothing matches, so `.Name` throws a `NullReferenceException` that no conversation code catches.
- **When it runs:** the condition of the `talk_bard` start line (`:463`); only a musician reaches the pick.
- **TAOM requirement:** talking to the musician in any tavern works.
- **Without this feature:** in a town whose culture owns no other town the conversation condition throws. Seven towns
  are in that state on this laptop's installed TAOM_Map (2026-09-07): `town_DG1`, `town_GT1`, `town_isengard`, `town_L1`,
  `town_LN1`, `town_R1` and `town_GBC1` (cultures dolguldur, goblin, isengard, lothlorien, lindon, rivendell,
  bluecraig). The count can grow, because `CultureConversionAdapter` changes a settlement's culture at runtime (#824).

## Architecture

### Design Challenge

The throw sits in the middle of a method that also builds the lyrics list and picks a random lyric. A prefix that
skips the original would have to copy that text, and TAOM adds no player text here.

### Solution Approach

A call-site swap: the transpiler rewrites exactly one
`TaleWorlds.Core.Extensions.GetRandomElementWithPredicate<Settlement>(MBReadOnlyList<Settlement>, Func<Settlement,bool>)`
call in place to `BardTownPickHook.PickTown`, which has the same signature, so the stack and the labels are unchanged.
`PickTown` is three expressions: vanilla's pick; if null, the same engine pick over every town except the current
settlement; if still null, the current settlement, so `.Name` never reads null. A normal town behaves exactly as
vanilla, because the first pick is vanilla's own. If the match count is not exactly one, the transpiler returns the
original instructions and logs one warning, never a half-patched method. The module hands the transpiler its logger
through `InitializeStatics`.

Patch110, category `Patch110_BardTownGuard`, applied at GameInit. No model, setting, save data or new text.

### Component Diagram

```
conversation_talk_bard_on_condition
   |  (transpiled call site)
BardTownPickHook.PickTown --> engine pick, then engine pick over other towns, then the current settlement
```

## Configuration

None.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BardTownGuard/Hooks/BardTownPickHook.cs` | The replacement pick |
| `Main/Features/BardTownGuard/Hooks/BardTownTranspiler.cs` | The one-call swap, fail-safe |
| `Main/Features/BardTownGuard/Hooks/TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch.cs` | Patch class and target |
| `Main/Features/BardTownGuard/BardTownGuardModule.cs` | Patch category declaration and logger handoff |

## Dependencies

- `IModLogger` (Core): the single warning when the call site is not found

## Tests

`TAOM.Tests/Features/BardTownGuard/BardTownGuardBindingTests.cs`:

- `TheTarget_ExistsAndHasExactlyOnePickCall_InTheInstalledEngine` (`BindingVerification`)
- `TheTranspiler_SwapsThatOneCallForTheHook_InTheInstalledEngine` (`BindingVerification`): one hook call, no engine call, same instruction count, and the pick's branch labels move to the hook call
- `PatchClass_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts` (`BindingVerification`): the real patch class is applied and the JIT compiles the replacement
- `PatchClass_IsInTheLiteralCategory` (`BindingVerification`)
- `PickTown_RunsTheEnginePickTwice_ThenNamesTheCurrentSettlement`: the hook's IL
- a zero-match and a two-match stream are returned unchanged with one warning

`TAOM.Tests/Features/BardTownGuard/BardTownGuardWiringTests.cs`: module listing, category id and phase, no behavior or model.

## Performance

The hook runs only where vanilla's pick ran; the second pick runs only when the first returned null.

## Known gaps

The tavern maid's local speciality line sets `DRINK` and `FOOD` only for vanilla culture ids
(the `DRINK` and `FOOD` block starts at `TavernEmployeesCampaignBehavior.cs:774`). Follow-up.

## In-game check

- In any of the seven towns (`town_DG1`, `town_GT1`, `town_isengard`, `town_L1`, `town_LN1`, `town_R1`, `town_GBC1`),
  talking to the musician never crashes, and about one lyric in four names another town.
- In a Gondor town (Gondor still has 11 towns on that map), a lyric that names a town names a Gondor town, which proves the engine-first
  branch.
- The game log shows no `[BardTownGuard]` warning at game start.

## Changelog

- 2026-10-19: added (#824).
- 2026-11-04: ported to the 1.4.x line from 1.5.x eed06b9c7 (#824).

## GitHub Issue

- **Issue:** #824 Tavern musician conversation may crash in towns with no same-culture town
- **Status:** Open
