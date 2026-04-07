# Codex Adversarial Review: Career System Implementation vs TOR_Core

**Date:** 2026-04-07
**Target:** `Main/Features/CareerSystem/` (44 files)
**Verdict:** needs-attention

No-ship. The current CareerSystem can persist invalid progression states, likely cannot serialize custom career data safely, and is missing core TOR-equivalent mutation, ability, passive, and UI wiring.

## Findings

### [CRITICAL] Choice selection bypasses tier unlocks and exclusivity invariants

**File:** `CareerScreenVM.cs:110-119`

**What's wrong:** `ExecuteSelectChoice` writes directly through `_dataService.TryAddChoice(...)`, which only enforces max-count and duplicate checks. `CareerRegistry.IsTierAvailable` is level-based only — the `TierUnlocks`/`HasAttribute` path is never consulted. A hero can take Tier 2/3 nodes without unlock requirements and accumulate multiple keystones in the same tier.

**TOR does:** `CareerChoiceObject.IsActiveForHero` enforces attribute requirements, quest completions, and tier-group exclusivity before allowing selection.

**Impact:** Invalid career trees, balance breakage, saved states that later code cannot reason about.

**Fix:** Replace direct `TryAddChoice` calls with a service method that validates tier unlocks and mutual exclusion using choice/group metadata before persisting.

### [CRITICAL] Career save data relies on an unregistered custom type

**File:** `CareerPersistenceBehavior.cs:20-24`

**What's wrong:** `_taom_careerData` stores `Dictionary<string, HeroCareerData>` directly. No `SaveableTypeDefiner`, `ConstructContainerDefinition`, or `[SaveableField]` registration for `HeroCareerData` exists anywhere in TAOM. Bannerlord's save system requires custom classes to be registered explicitly.

**TOR does:** Uses `[SaveableField]` on `HeroExtendedInfo` with proper type definer registration.

**Impact:** Career state may not round-trip reliably across save/load, especially across reloads or schema changes.

**Fix:** Register `HeroCareerData` and `Dictionary<string, HeroCareerData>` with a `SaveableTypeDefiner`. Add integration tests for save/load, pre-feature saves, and config-removal.

### [CRITICAL] Mutation system drops most of TOR's capability surface

**File:** `Mutations/MutationService.cs:24-97`

**What's wrong:** `MutateAbility` only accepts `AbilityTemplateData`. `ApplyMutation` rejects non-float properties. `mutation.Operation` is parsed but never used. No support for `TriggeredEffectTemplate` or `StatusEffectTemplate`. No loader/consumer for `taom_ability_templates.xml` — mutated data is not connected to a live ability pipeline.

**TOR does:** `MutationObject` supports 3 target types (AbilityTemplate, TriggeredEffectTemplate, StatusEffectTemplate), applies root-first then selected choices, uses `Action<AbilityTemplate>` lambdas that can read hero skill levels at runtime.

**Impact:** Keystones/passives depending on template mutation either do nothing or are impossible to express. Core functional gap vs TOR.

**Fix:** Expand mutation targets beyond `AbilityTemplateData`, honor `OperationType`, and connect mutated template output to the actual ability runtime.

### [HIGH] Active career abilities are not wired into a usable battle flow

**File:** `CareerAbilityService.cs:10-29`

**What's wrong:** Service instantiates `CareerAbility` with hardcoded 10-second cooldown and no template load. Mission behavior only ticks state and adds kill-based charges on `OnAgentRemoved`. No targeting mode, slow-time, crosshair, damage/heal charge sources, or activation path. No caller triggers `ActivateAbility` from UI/input.

**TOR does:** `AbilityManagerMissionLogic` handles 6 charge types, slow-time targeting (0.3x), 6 crosshair modes, weapon sheath/restore, and double-use keystones.

**Impact:** Careers can advertise abilities in config/UI but the player has no way to cast them.

**Fix:** Load ability data from `taom_ability_templates.xml`, add activation entry point, implement battle-side targeting/cast execution.

### [HIGH] Career UI is only partially scaffolded

**File:** `GUI/Prefabs/CareerSystem/CareerScreen.xml:51-80`

**What's wrong:** XML instantiates `CareerChoiceGroupWidget` but no prefab/widget definition exists. No `PrefabExtension` injects a career button into Character Developer. `CareerScreenGameState` is declared but unused — screen opens as raw global layer.

**TOR does:** Full `CareerScreen` with 3-tier visual tree, `CareerScreenGameState`, `CharacterDeveloperVMExtension` button injection, per-choice tooltips.

**Impact:** Main progression UI is unreachable or broken at runtime.

**Fix:** Implement concrete widget definitions, add `PrefabExtension` for Character Developer button, drive screen through `GameState`.

### [HIGH] Most passive effects have no model integration

**File:** `PassiveEffectType.cs:3-50`

**What's wrong:** Enum exposes broad TOR-like surface (`Health`, `Damage`, `ArmorPenetration`, `SwingSpeed`, `TroopResistance`, `TroopRegeneration`, `InventoryCapacity`, etc.) and XML uses them, but `CareerPassiveHelper.ApplyFactor/ApplyFlat` only appears in 8 model call sites. Most enum members have no application path.

**TOR does:** 44 PassiveEffectTypes applied across 21 GameModels via `CareerHelper.ApplyBasicCareerPassives()`.

**Impact:** Players spend permanent career points on perks that never affect game calculations.

**Fix:** Audit every `PassiveEffectType` against a concrete application site. Block config from using unsupported types until model coverage exists.

## What TAOM Does Better

1. **XML-driven career definitions** — Adding a new career = XML file, not a new C# class per career (TOR has 22 `*Choices.cs` files + 13 `CareerButton/*.cs` files). Significantly more extensible.

2. **IoC + adapter pattern** — `ICareerDataService`, `ICareerPassiveService`, etc. are testable and mockable. TOR uses `TORCareers.Instance` static singleton throughout.

3. **Test foundation** — 6 test files covering service logic with NSubstitute mocks. TOR has zero career tests.

4. **Clean domain model** — `CareerDefinition`, `CareerChoiceDefinition`, `MutationDefinition` are pure POCOs loaded from XML. TOR mixes domain, registration, and UI concerns in `CareerObject`.

5. **Race-awareness** — TAOM's `CareerDefinition` has `AllowedRaces`, preventing cross-race career assignment. TOR has no race system.

## Architecture Comparison

| Aspect | TOR | TAOM | Verdict |
|--------|-----|------|---------|
| Career definitions | 22 C# static classes | XML config | **TAOM** — extensible |
| Mutation system | Lambda-based, 3 target types, runtime hero reads | Float-only DTO clone, 1 target type, disconnected | **TOR** — core gap |
| Passive application | 44 types across 21 models | Enum defined, 8 model sites | **TOR** — coverage gap |
| Charge system | 6 types + slow-time + crosshairs | Kill-only + hardcoded cooldown | **TOR** — depth gap |
| Save/load | `[SaveableField]` + type definer | `SyncData` without type registration | **TOR** — reliability |
| UI depth | Full screen + GameState + CharDev button | Partial prefab, no widget defs | **TOR** — completeness |
| Test coverage | Zero | 6 test files | **TAOM** — testable |
| Career switching | Dialog-based + restrictions | Service exists, validation incomplete | **TOR** — maturity |
| Event coverage | 11 campaign events | ~5 events | **TOR** — coverage |
| Extensibility | New C# class per career | XML entry | **TAOM** — data-driven |

## Recommended Next Steps

1. **Add choice-selection validator** enforcing tier unlocks + mutual exclusion (CRITICAL)
2. **Register `HeroCareerData` with `SaveableTypeDefiner`** (CRITICAL)
3. **Expand mutation pipeline** to honor OperationType, support all 3 target types, connect to ability runtime (CRITICAL)
4. **Implement ability activation path** with input handling, targeting, and battle HUD (HIGH)
5. **Complete UI pipeline** — widget definitions, PrefabExtension, GameState (HIGH)
6. **Wire remaining PassiveEffectTypes** into corresponding GameModels (HIGH)
