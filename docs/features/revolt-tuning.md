# Revolt Tuning

## Overview

Softens vanilla Bannerlord's revolt mechanic so LOTR's frequent settlement flips don't spawn rebel kingdoms every few weeks. Raises the loyalty thresholds required for revolt and dampens the different-culture ownership penalty. Values are JSON-configurable without recompilation.

## Why This Exists

- **Vanilla behavior:** `DefaultSettlementLoyaltyModel` in v1.3.15 applies a brutal -3.0/day loyalty penalty when the owner's culture differs from the settlement's culture (and -1.0/day for different-culture governors). Settlements enter a "rebellious" visual state at loyalty ≤ 25 and actually revolt at loyalty ≤ 15, spawning an independent rebel clan via `RebellionsCampaignBehavior.StartRebellionEvent`.
- **TAOM requirement:** LOTR settings involve constant territory exchange (Gondor↔Mordor, Rohan↔Isengard, Dale↔Easterlings). Under vanilla rules a newly-conquered town hits the revolt threshold in roughly 28 days, which floods the map with rebel kingdoms and destabilizes the War of the Ring narrative.
- **Without this feature:** Settlements captured during a war revolt before the conqueror can stabilize them, spawning new factions that dilute the intended 18-kingdom LOTR political landscape.

## Architecture

### Design Challenge

Two TaleWorlds systems collide:
1. `DefaultSettlementLoyaltyModel` exposes the thresholds and penalty magnitudes as virtual properties — overridable via GameModel.
2. `RebellionsCampaignBehavior.CheckRebellionEvent` reads those thresholds indirectly through `Campaign.Current.Models.SettlementLoyaltyModel`, so raising thresholds in our model automatically gates rebellion triggers without any Harmony patch on the behavior.

The feature therefore needs only a GameModel override, not a behavior hook. The constraint is that there is only one active GameModel per base class — `TaomSettlementLoyaltyModel` already exists for cultural feat loyalty bonuses, so we extend it rather than create a second model.

### Solution Approach

1. `RevoltTuning` feature owns the JSON config and provider (isolated from `CulturalFeats`).
2. Existing `TaomSettlementLoyaltyModel` gains constructor injection of `IRevoltTuningConfigProvider` and four new property overrides that read values from the cached config.
3. Cultural feat logic in `CalculateLoyaltyChange` stays untouched.

### Component Diagram

```
revolt_tuning_config.json
          |
RevoltTuningConfigProvider (loads + caches)
          |
TaomSettlementLoyaltyModel (GameModel — CulturalFeats)
          |
Campaign.Current.Models.SettlementLoyaltyModel
          |
RebellionsCampaignBehavior (reads thresholds)
```

## Configuration

### Config File: `Main/_Module/ModuleData/configs/revolt_tuning_config.json`

Newtonsoft JSON. Missing file or parse failure falls back to compiled defaults and logs a warning/error. Result is cached for the process lifetime.

| Field | Type | Vanilla | TAOM Default | Description |
|-------|------|---------|--------------|-------------|
| `rebellionStartLoyaltyThreshold` | int | 15 | 5 | Actual rebellion fires at loyalty ≤ this value |
| `rebelliousStateStartLoyaltyThreshold` | int | 25 | 10 | Rebellious warning state triggers at loyalty ≤ this value |
| `settlementOwnerDifferentCultureLoyaltyEffect` | float | -3.0 | -1.0 | Daily loyalty change when owner's culture differs from settlement's |
| `governorDifferentCultureLoyaltyEffect` | float | -1.0 | -0.5 | Daily loyalty change when governor's culture differs from settlement's |

### Current Values

The shipping values reflect the "soft tune" design: revolts stay possible but require sustained neglect plus a 1.4× militia-to-garrison ratio and the 25% daily roll (both hardcoded in `RebellionsCampaignBehavior` and out of scope for this feature). Different-culture ownership is still a penalty — just not an instant loyalty collapse.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/RevoltTuning/RevoltTuningConfig.cs` | POCO with defaults |
| `Main/Features/RevoltTuning/IRevoltTuningConfigProvider.cs` | Provider interface |
| `Main/Features/RevoltTuning/RevoltTuningConfigProvider.cs` | JSON loader with cache + fallback |
| `Main/Features/RevoltTuning/RevoltTuningIoC.cs` | DryIoc singleton registration |
| `Main/Features/CulturalFeats/Models/TaomSettlementLoyaltyModel.cs` | GameModel — consumes the config for four property overrides |
| `Main/_Module/ModuleData/configs/revolt_tuning_config.json` | Tunable values |

## Dependencies

- `IRevoltTuningConfigProvider` — exposes `GetConfig()` returning a cached `RevoltTuningConfig`
- `IPathService` (Core) — resolves `ModuleDataPath` to locate the JSON file
- `IModLogger` (Core) — warn-on-missing, error-on-parse-failure

## Tests

- `TAOM.Tests/Features/RevoltTuning/RevoltTuningConfigProviderTests.cs` — 6 tests covering:
  - Valid JSON parse (all four fields)
  - Missing file → defaults + warning log
  - Malformed JSON → defaults + error log
  - Partial JSON → merges with defaults
  - Repeat calls return cached instance (reference equality)
  - Default-value spec (guard against accidental drift from the "soft tune" decision)

`TaomSettlementLoyaltyModel` itself is a thin GameModel entry point — per [gamemodels.md](../../.claude/rules/gamemodels.md) rule 8, it's verified live rather than unit-tested.

## How to Retune Revolt Frequency

1. Open `Main/_Module/ModuleData/configs/revolt_tuning_config.json`
2. Adjust any of the four fields (no recompile needed). **The provider caches on first load and holds for the Bannerlord process lifetime — you must fully quit and relaunch Bannerlord for JSON edits to take effect.** Switching campaigns or loading a save in the same session will continue to use the originally loaded values.
3. To make revolts **rarer**: raise `rebellionStartLoyaltyThreshold` and `rebelliousStateStartLoyaltyThreshold` closer to vanilla (15/25), lower the penalty magnitudes closer to 0
4. To make revolts **more common**: lower the thresholds (e.g., 2/5) and raise penalty magnitudes (e.g., -2.0/-1.0)
5. To restore vanilla exactly: `{15, 25, -3.0, -1.0}`

If you need to disable rebellions entirely, set `rebellionStartLoyaltyThreshold` to `0` (loyalty ≤ 0 never fires in practice).

## Validation Guardrails

`RevoltTuningConfigProvider.Validate` sanity-checks values after deserialization and logs warnings + falls back to defaults when it detects:

- Either threshold outside `[0, 100]` (loyalty range)
- `rebelliousStateStartLoyaltyThreshold` lower than `rebellionStartLoyaltyThreshold` (ordering inversion — the "warning" state must gate before the actual trigger)
- Positive value for either culture-penalty (these are daily penalties; a positive value would be a bonus and fight the feature's purpose)

Invalid individual fields are reverted to their compiled default; other fields in the file continue to be applied. A summary warning is emitted when any reversion occurs.

## Out of Scope

- `MilitiaGarrisonRatio` (hardcoded 1.4× in `RebellionsCampaignBehavior` — would require a Harmony patch)
- Post-rebellion clan strength, starting troops, or renown
- Context-aware scoping (grace period after conquest, watched-faction filtering)
- MCM integration

## GitHub Issue

- **Issue:** _pending — create via `gh issue create` with the completion workflow_
- **Status:** Implementation complete; docs updated

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
