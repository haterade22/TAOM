# Character Creation Body Properties

## Overview

When the player picks a culture during Character Creation, the player-character preview adopts a TAOM-defined `BodyProperties` key string for that culture instead of the vanilla random-within-min/max default. The body re-applies on every culture change. Cultures not configured fall back to vanilla behavior.

## Why This Exists

- **Vanilla behavior:** When the player selects a culture in CC, vanilla generates a random body using `FaceGen.GetRandomBodyProperties(...)` against the culture's `default_character_creation_body_property_<culture>` from `sandbox_bodyproperties.xml`. The result is non-deterministic — every new game produces a different silhouette for the same culture.
- **TAOM requirement:** Lock in a specific cultural silhouette per culture so the starting body matches lore expectations (e.g., Rohirrim taller/leaner, Dunlendings stockier, dwarves shorter). The modder needs to retune frequently without rebuilding C#.
- **Without this feature:** Body silhouettes are random; cultural body archetypes can't be controlled centrally; tuning means editing vanilla XML or shipping XSLT overrides for both `BodyPropertiesMin` and `BodyPropertiesMax`.

## Architecture

### Design Challenge

This feature sits at a particularly hostile spot in the CC pipeline. Three independent vanilla code paths can clobber the player's body, and each requires a different intercept strategy:

1. **`CharacterCreationContent.SetSelectedCulture`** — public method that sets the `SelectedCulture` property; called by TAOM's `FactionMap.CultureSettingService` reflectively when the player confirms a culture on the map. Postfix here applies our body to `Hero.MainHero` and `CharacterObject.PlayerCharacter`.

2. **`CharacterCreationCultureStageVM.OnCultureSelection`** — VM method whose first statement is `InitializePlayersFaceKeyAccordingToCultureSelection(selectedCulture)`, which writes `selectedCulture.Culture.DefaultCharacterCreationBodyProperty.BodyPropertyMax` (the vanilla culture default from `sandbox_bodyproperties.xml`) directly onto `CharacterObject.PlayerCharacter` via `UpdatePlayerCharacterBodyProperties`. This runs *after* our SetSelectedCulture postfix in the FactionMap path because `CultureSettingService` calls `cultureVM.ExecuteSelectCulture()` *after* `SetSelectedCulture` — `ExecuteSelectCulture` routes through `_onSelection(this)` → `OnCultureSelection`. Without a postfix here, vanilla's culture-default write clobbers ours moments later. **A postfix on `OnCultureSelection` is the canonical hook** — same hook LOTRAOM 1.2.12 used via `SandboxCharacterCreationContent.OnCultureSelected` override (refactored out in v1.3 since `CharacterCreationContent` is now sealed).

3. **`CharacterCreationNarrativeStageView.RefreshAgentVisuals`** — per-frame visual refresh in the narrative stages. The career menu's player `NarrativeMenuCharacter` (id `"player_career_character"`) is constructed at CC initialization with a captured-at-construction body, before any culture is selected. Patch20's existing prefix syncs `Race` onto these characters, but not `BodyProperties`. A sibling Patch29 prefix syncs the player career character's body from `Hero.MainHero.BodyProperties` when it differs.

`BodyProperties` is also a sealed `TaleWorlds.Core` struct. Per ADR-007 it cannot cross service boundaries; only `IPlayerBodyPropertiesAdapter` parses it (`BodyProperties.FromString`) and applies it to `Hero.MainHero` + `CharacterObject.PlayerCharacter`.

There is also a non-obvious engine-side guard: `CharacterObject.UpdatePlayerCharacterBodyProperties` in v1.3.15 wraps its entire body in `if (IsPlayerCharacter && IsHero)` and does not call base. When that guard fails (early CC lifecycle), the override no-ops silently — `BodyPropertyRange.Init` from `BasicCharacterObject` doesn't run either. The adapter therefore writes `Hero.MainHero.{StaticBodyProperties, Weight, Build}` directly *first*, then calls `UpdatePlayerCharacterBodyProperties` to fire `OnPlayerBodyPropertiesChanged` for downstream observers when the guard does pass. The direct writes are the actual mechanism; the override call is for event-firing.

### Solution Approach

Standard TAOM 4-layer stack: Harmony patches → service → adapter → engine. Three Harmony patches (all in the `Patch29_CCBodyProperties` category) cover the three intercept points; all delegate to the same `ICCBodyPropertiesService.ApplyForCulture(stringId)` so the application logic is single-sourced.

### Component Diagram

```
charactercreation/cc_body_properties.xml
        │
  CCBodyPropertiesProvider  (loads + validates entries, lowercase culture-id keyed,
                              Reuse.Singleton — restart-only reload scope)
        │
  CCBodyPropertiesService   (orchestrates lookup, structured logging,
                              exception swallowing)
        │
  IPlayerBodyPropertiesAdapter  (BodyProperties.FromString → direct Hero scalar
                                  writes → UpdatePlayerCharacterBodyProperties)
        ▲
        │ called from three Harmony patches in the Patch29_CCBodyProperties category:
        │
  ┌─────┴─────────────────────────────────────────────────────────────────┐
  │ CharacterCreationContent_SetSelectedCulture_Patch                     │
  │   Postfix on TaleWorlds.CampaignSystem.CharacterCreationContent       │
  │   .CharacterCreationContent.SetSelectedCulture(CultureObject,         │
  │   CharacterCreationManager).                                          │
  │   Fires when culture is committed via FactionMap                      │
  │   (or any other code path that hits SetSelectedCulture).              │
  ├───────────────────────────────────────────────────────────────────────┤
  │ CharacterCreationCultureStageVM_OnCultureSelection_Patch              │
  │   Postfix on TaleWorlds.CampaignSystem.ViewModelCollection            │
  │   .CharacterCreation.CharacterCreationCultureStageVM                  │
  │   .OnCultureSelection(CharacterCreationCultureVM).                    │
  │   Re-applies body AFTER vanilla                                       │
  │   InitializePlayersFaceKeyAccordingToCultureSelection has just        │
  │   overwritten it with the culture's BodyPropertyMax XML default.      │
  │   This is the canonical hook (LOTRAOM 1.2.12 equivalent).             │
  ├───────────────────────────────────────────────────────────────────────┤
  │ CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch│
  │   Prefix on SandBox.GauntletUI.CharacterCreation                      │
  │   .CharacterCreationNarrativeStageView.RefreshAgentVisuals.           │
  │   Per-frame sync from Hero.MainHero.BodyProperties to the career      │
  │   menu's NarrativeMenuCharacter (StringId == "player_career_character"│
  │   ) when it differs.                                                  │
  └───────────────────────────────────────────────────────────────────────┘
```

## Configuration

### Config File: `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`

Per-culture body-properties strings. Cultures listed here override the vanilla random body during CC preview.

```xml
<CCBodyProperties>
  <Culture id="vlandia">
    <BodyProperties version="4"
                    age="30.25"
                    weight="0.5301"
                    build="0.5185"
                    key="0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002" />
  </Culture>
</CCBodyProperties>
```

| Field | Type | Description |
|-------|------|-------------|
| `Culture/@id` | string | Culture string id (case-insensitive). Vanilla: `vlandia`, `empire`, `aserai`, `battania`, `sturgia`, `khuzait`. TAOM custom: `mordor`, `gondor`, `erebor`, `mirkwood`, `lothlorien`, `rivendell`, `dolguldur`, `gundabad`, `isengard`, `umbar`, `dale` |
| `BodyProperties/@version` | int | `4` for the v1.3.15 body-key encoding |
| `BodyProperties/@key` | hex string | Exactly 128 hex characters. Shorter or empty keys are skipped with a warning |
| `BodyProperties/@weight` | float (optional) | Defaults to `0` if absent |
| `BodyProperties/@build` | float (optional) | Defaults to `0` if absent |
| `BodyProperties/@age` | float (optional) | Parsed by vanilla but **NOT applied** by this feature — `Hero.Age` is computed from `Hero.BirthDay`, which the adapter does not touch. Including `age=` has no visible effect |

### Validation

The provider rejects (and warns on) entries with:
- Missing `Culture/@id`
- Missing `<BodyProperties>` child element
- Missing or empty `key` attribute
- `key` length not equal to 128

Duplicate culture ids cause a warning and last-wins. Malformed XML logs an error and the entire file is skipped.

### TAOM Culture-ID Reference

In TAOM, several vanilla culture ids are XSLT-rebound to LOTR factions:

| Culture id | LOTR faction |
|------------|--------------|
| `vlandia` | Rohan |
| `empire` | Dunland |
| `battania` | Khand |
| `aserai` | Harad |
| `sturgia` | Barding |
| `khuzait` | Rhun |

TAOM custom cultures (`mordor`, `gondor`, `erebor`, `mirkwood`, `lothlorien`, `rivendell`, `dolguldur`, `gundabad`, `isengard`, `umbar`, `dale`) keep their natural ids.

### Reload Scope

The provider is `Reuse.Singleton` (DryIoc) — cached for the entire Bannerlord process lifetime. **Edits to `cc_body_properties.xml` require a full Bannerlord restart**, not a save-load or a "new campaign" click.

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/CharacterCreation/ICCBodyPropertiesProvider.cs](../../Main/Features/CharacterCreation/ICCBodyPropertiesProvider.cs) | Provider interface |
| [Main/Features/CharacterCreation/CCBodyPropertiesProvider.cs](../../Main/Features/CharacterCreation/CCBodyPropertiesProvider.cs) | XML loader + validation |
| [Main/Features/CharacterCreation/ICCBodyPropertiesService.cs](../../Main/Features/CharacterCreation/ICCBodyPropertiesService.cs) | Service interface |
| [Main/Features/CharacterCreation/CCBodyPropertiesService.cs](../../Main/Features/CharacterCreation/CCBodyPropertiesService.cs) | Orchestration + structured logging |
| [Main/Adapters/IPlayerBodyPropertiesAdapter.cs](../../Main/Adapters/IPlayerBodyPropertiesAdapter.cs) | Adapter interface |
| [Main/Adapters/PlayerBodyPropertiesAdapter.cs](../../Main/Adapters/PlayerBodyPropertiesAdapter.cs) | `BodyProperties.FromString` parsing + direct Hero scalar writes + `UpdatePlayerCharacterBodyProperties` |
| [Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs](../../Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs) | Postfix on `SetSelectedCulture`. Catches the FactionMap reflective-invoke path |
| [Main/Features/CharacterCreation/Hooks/CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs](../../Main/Features/CharacterCreation/Hooks/CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs) | Postfix on `OnCultureSelection`. Re-applies body after vanilla `InitializePlayersFaceKeyAccordingToCultureSelection` overwrite |
| [Main/Features/CharacterCreation/Hooks/CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs](../../Main/Features/CharacterCreation/Hooks/CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs) | Prefix on `RefreshAgentVisuals`. Syncs body to career-menu `NarrativeMenuCharacter` (StringId `"player_career_character"`) per-frame |
| [Main/_Module/ModuleData/charactercreation/cc_body_properties.xml](../../Main/_Module/ModuleData/charactercreation/cc_body_properties.xml) | Config |

## Dependencies

- `ICCBodyPropertiesProvider` — loads + caches per-culture body strings
- `IPlayerBodyPropertiesAdapter` — wraps `BodyProperties.FromString`, direct Hero scalar writes, and `BasicCharacterObject.UpdatePlayerCharacterBodyProperties`
- `IPathService` (Core) — resolves `ModuleDataPath`
- `IModLogger` (Core) — structured warning/error/info logging

## Tests

- [TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesProviderTests.cs](../../TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesProviderTests.cs) — 14 tests covering: file missing, malformed XML, configured culture, not-configured culture, null/empty cultureId, case-insensitive culture lookup, missing-id skip, missing-BodyProperties skip, missing-key skip, empty-key skip, wrong-hex-length skip, duplicate-id last-wins, caching, age/weight/build attribute preservation
- [TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesServiceTests.cs](../../TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesServiceTests.cs) — 7 tests covering: configured-culture happy path, not-configured no-op, adapter parse-failure warning, null cultureId guard, empty cultureId guard, adapter exception swallowed + logged, success info logging

The adapter (`PlayerBodyPropertiesAdapter`) and the three Harmony patches are intentionally not unit-tested — they are thin wrappers / boundary classes over sealed TaleWorlds engine APIs (`BodyProperties.FromString`, `UpdatePlayerCharacterBodyProperties`, Hero property setters, `NarrativeMenuCharacter.UpdateBodyProperties`). Coverage is via in-game verification.

## How to Add a New Culture Body

1. Open Bannerlord, generate or capture the desired body in any face-customizer (CC, the in-game "edit your face" debug menu, or a save export).
2. Copy the `<BodyProperties version="4" key="..."/>` element exactly. (Optional `weight` and `build` attributes are honoured if present; `age` has no effect — see Configuration table.)
3. Open `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`.
4. Add a new `<Culture id="<your_culture_id>">` block, paste the BodyProperties element inside.
5. Save and **restart Bannerlord** (the provider is process-cached).

No code changes required. Validation warnings will appear in `taom_debug_*.log` (under `<game install>/bin/Win64_Shipping_Client/Logs/`) if the entry is malformed.

## How to Remove a Culture Body Override

Delete the `<Culture id="...">` block from `cc_body_properties.xml` and restart. The culture falls back to vanilla random-body generation with no errors.

## Lessons Learned (during initial implementation)

This feature went through three iterations before working in-game. Each iteration corrected a separate engine-boundary assumption. Captured here so future modders touching CC body state don't repeat them.

| Iteration | What was wrong | Why it was missed | Fix |
|-----------|---------------|-------------------|-----|
| Initial | Adapter relied solely on `playerChar.UpdatePlayerCharacterBodyProperties(...)` to write Hero scalars. Direct writes were initially included, then removed as "redundant" during deep-review | `/deep-review` Agent 2 quoted only the BODY of `CharacterObject.UpdatePlayerCharacterBodyProperties`, missing that the entire body is wrapped in `if (IsPlayerCharacter && IsHero)` (and the override does not call `base`). When the guard fails, the call no-ops silently — including `BodyPropertyRange.Init` from `BasicCharacterObject`. The "redundant" direct writes were the actual mechanism | Restored direct `Hero.MainHero.{StaticBodyProperties, Weight, Build}` writes BEFORE the override call. Two-step pattern: direct writes always succeed; override fires `OnPlayerBodyPropertiesChanged` when guard does pass |
| Second | Service applied vlandia body successfully (logged), but visible body still vanilla | Vanilla `CharacterCreationCultureStageVM.OnCultureSelection` calls `InitializePlayersFaceKeyAccordingToCultureSelection` as its first statement, which writes `culture.DefaultCharacterCreationBodyProperty.BodyPropertyMax` over the player. TAOM's `FactionMap.CultureSettingService` invokes `SetSelectedCulture` (our patch fires) *then* `cultureVM.ExecuteSelectCulture()` → `_onSelection` → `OnCultureSelection` → vanilla overwrite. Our write was clobbered moments later. The reflective-invoke chain hid this from the original signature-only verification | Added sibling postfix on `CharacterCreationCultureStageVM.OnCultureSelection`. Same approach LOTRAOM 1.2.12 used via `SandboxCharacterCreationContent.OnCultureSelected` override; the virtual hook was refactored out in v1.3 (since `CharacterCreationContent` is now sealed), and the body-overwrite logic moved to the stage VM where we can postfix it |

The systemic lesson: **for any state-mutation hook in the CC pipeline, decompile the entire call chain that touches the same state, not just the entry-point method.** Vanilla often has a *parallel* writer that fires from a different code path moments after yours. Captured in [feedback_taleworlds_vm_setter_decompile.md](../../C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_taleworlds_vm_setter_decompile.md).

## GitHub Issue

- **Issue:** #108 — Per-culture default BodyProperties on Character Creation screen
- **Status:** Closed — verified working in-game 2026-05-06
