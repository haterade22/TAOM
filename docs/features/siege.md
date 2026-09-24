# Siege

## Overview
The Siege feature guards against a crash in `BesiegerCamp.GetSiegeCampPartyPosition` that occurs when a TAOM settlement has no `siege_camp_1` scene entities configured in the map. The Harmony Prefix intercepts the call before the engine throws, logs a diagnostic message identifying the problematic settlement, falls back to `siege_camp_2` frames if available, and as a last resort uses the settlement gate position.

## Why This Exists
- **Vanilla behavior:** `BesiegerCamp.GetSiegeCampPartyPosition` assumes `siegeCamp1GlobalFrames` is non-null and non-empty. If the array is empty (because the settlement's map scene has no `siege_camp_1` entities), the method throws an `IndexOutOfRangeException` or produces incorrect behaviour.
- **TAOM requirement:** The TAOM world map contains settlements whose scenes were authored without `siege_camp_1` placement. These must not crash the game during a siege.
- **Without this feature:** Starting a siege against an affected settlement crashes the session with an `IndexOutOfRangeException` in the vanilla `BesiegerCamp` code.

## Architecture

### Design Challenge
`BesiegerCamp` is a sealed TaleWorlds type. The `GetSiegeCampPartyPosition` method cannot be overridden. The only safe intercept point that allows both short-circuit (return false to skip the original) and result injection (`ref __result`) is a Harmony Prefix.

### Solution Approach
A Harmony Prefix on `BesiegerCamp.GetSiegeCampPartyPosition` runs before the original method. It checks whether `siegeCamp1GlobalFrames` is null or empty. If the frames exist, it returns `true` immediately to let the original run unchanged. If they are missing, it:
1. Logs a red warning to `TaleWorlds.Library.Debug` identifying the settlement by name and ID, and the count of camp-2 frames available.
2. If `siegeCamp2GlobalFrames` is non-empty, copies those frames into `siegeCamp1GlobalFrames`, clears camp-2, and returns `true` so the original method can proceed normally with the substituted frames.
3. If neither set of frames exists and there is no besieged settlement to ring (the camp's `SiegeEvent` or its settlement is null), it logs that and returns `true`. Vanilla then throws on the empty camp-1 array, exactly as it did after the catch-all that handled this case before. The path is defensive only: in v1.5.3 both engine callers (`MobileParty.OnPartyJoinedSiegeInternal`, `BesiegerCamp.SetPositionAfterMapChange`) dereference `SiegeEvent.BesiegedSettlement` before calling, so vanilla cannot reach it.
4. Otherwise, with neither set of frames, it places the party on a ring around `settlement.GatePosition` (eight slots per ring, radius 0.5 plus 0.3 per further ring, chosen by the party's index), keeps the gate's `IsOnLand`, and returns `false` to skip the original entirely.
5. Any exception within the prefix is caught and logged, and the original runs (`return true`). The catch can only fire while camp-1 is still null or empty, so vanilla then throws the same `IndexOutOfRangeException` (or an NRE on a null array); see the [patch registry](../reference/harmony-patch-registry.md).

### Component Diagram
```
BesiegerCamp.GetSiegeCampPartyPosition  (Harmony Prefix)
  |
  |-- siegeCamp1GlobalFrames non-empty? --> return true (original runs normally)
  |
  |-- [WARNING] Log: settlement id, name, camp2 frame count
  |
  |-- siegeCamp2GlobalFrames non-empty?
  |     |-- Yes: swap camp2 -> camp1, clear camp2 --> return true
  |     `-- No:  no settlement? --> log + return true (original runs and throws)
  |              else __result = ring slot around GatePosition  --> return false (skip original)
  |
  `-- Exception? --> log + return true (original runs and throws)
```

## Configuration
None. The fallback logic is fully self-contained in the patch.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs` | Harmony Prefix on `BesiegerCamp.GetSiegeCampPartyPosition`; implements the null-frame guard and fallback chain |
| `Main/Features/Siege/.editorconfig` | Sets the seven nullable warnings to error for this folder (nullable ratchet) |
| `TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs` | Unit tests calling the Prefix directly on each of its five paths |

## Dependencies
- `TaleWorlds.CampaignSystem.Siege.BesiegerCamp` — target type (sealed)
- `TaleWorlds.Library.Debug.Print` — used for in-game diagnostic messages (red/yellow channel `17592186044416`)
- `HarmonyLib` — `[HarmonyPatch]`, `[HarmonyPrefix]`

## Tests
`TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs` calls the prefix directly on uninitialized engine objects (the settlement case sets only the members the prefix reads) with a substituted `Debug.DebugManager`. It covers all five paths: camp-1 frames present (vanilla runs, frames untouched), camp-2 handed over as the same array, no settlement (defers without the prefix throwing), a settlement with no frames (ring slot east of the gate for party index 0), and the catch. `SiegeDefenseServiceTests` covers `SiegeDefenseService` and `SiegeEngineAvailabilityServiceTests` covers `SiegeEngineAvailabilityService`.

## How to Fix a Settlement with Missing Siege Camp Entities
The patch logs a message in the format:
```
TAOM: WARNING — Settlement 'Name' (id=id_string) has no siege_camp_1 scene entities (camp2=N frames). Fix in map editor.
```
To properly resolve the warning:
1. Open the settlement's map scene in the Bannerlord scene editor.
2. Add at least one entity named `siege_camp_1` with a valid transform.
3. Rebuild the map and verify siege camp frames are populated.

The patch is a safety net only; the intended fix is to add the scene entities.

## Changelog
- 2026-03-20 — Added the Harmony Prefix on `BesiegerCamp.GetSiegeCampPartyPosition`: guards empty `siegeCamp1GlobalFrames`, swaps camp2 frames into the camp1 slot when camp1 is empty, and falls back to the settlement gate position when both arrays are empty (fixes the `IndexOutOfRangeException` on settlements like "Gwígar" lacking `siege_camp_1` scene entities).
- 2026-09-24: the folder is null-clean and its `.editorconfig` sets the nullable warnings to errors (plan 019, #660); explicit no-settlement branch in the prefix; tests for every path through the prefix. In the same folder, a partial or `null` `KingdomMessages` entry now falls back per field to `SiegeDefenseService`'s defaults ([siege-defense.md](siege-defense.md#configuration)). The no-settlement path stays a logged defer to vanilla: it is unreachable from vanilla in v1.5.3, and the log line is the tripwire if an engine change ever reaches it.

## GitHub Issue
- **Issue:** haterade22/TAOM#660 (plan 019: nullable ratchet, Siege folder graduated). The original guard predates issue tracking (introduced in commit `d3cb87c`: "fix: add patch to guard against IndexOutOfRangeException in siege camp positioning").
- **Status:** #660 tracks branch `improve/019-nullable-ratchet`; the original guard's issue is unknown.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/settlements.md](../modding/settlements.md)

<!-- backlinks-end -->
