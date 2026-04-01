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
3. If neither set of frames exists, sets `__result` to `settlement.GatePosition` and returns `false` to skip the original entirely.
4. Any exception within the prefix is caught and logged; the original method is allowed to run (`return true`) to avoid cascading failures.

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
  |     `-- No:  __result = settlement.GatePosition  --> return false (skip original)
  |
  `-- Exception? --> log + return true
```

## Configuration
None. The fallback logic is fully self-contained in the patch.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs` | Harmony Prefix on `BesiegerCamp.GetSiegeCampPartyPosition`; implements the null-frame guard and fallback chain |

## Dependencies
- `TaleWorlds.CampaignSystem.Siege.BesiegerCamp` — target type (sealed)
- `TaleWorlds.Library.Debug.Print` — used for in-game diagnostic messages (red/yellow channel `17592186044416`)
- `HarmonyLib` — `[HarmonyPatch]`, `[HarmonyPrefix]`

## Tests
No unit tests exist for the Siege feature in `TAOM.Tests/Features/`. The patch delegates no logic to a service — the entire guard is implemented inline in the Prefix. Testing would require constructing a `BesiegerCamp` instance with controlled frame arrays, which is not feasible without the game runtime.

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

## GitHub Issue
- **Issue:** Unknown (introduced in commit `d3cb87c` — "fix: add patch to guard against IndexOutOfRangeException in siege camp positioning")
- **Status:** Unknown
