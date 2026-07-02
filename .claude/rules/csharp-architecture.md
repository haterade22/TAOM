---
paths:
  - "Main/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM Architecture Quick Reference

Full guide: `docs/ai-includes/architecture.md`

## Layer Stack

```
HarmonyPatch / GameModel / CampaignBehavior   ← THIN (<150 lines, no logic)
                    │ delegates to
              Service (IXxxService)            ← ALL business logic here
                    │ uses
              Adapter (IXxxAdapter)            ← wraps sealed TaleWorlds types
                    │ wraps
         TaleWorlds Engine (Hero, Agent…)      ← sealed, never cross boundary
```

## Non-Negotiable Rules

| Rule | Detail |
|------|--------|
| Entry points <150 lines | ADR-002: delegate immediately to service |
| No sealed types in services | ADR-007: `IHeroAdapter` not `Hero` |
| Constructor injection only | No service locator in services |
| Convert at boundary | Adapt sealed types in the entry point, not deep in services |
| `?.` for computed properties | TaleWorlds getters crash before your null check — see `adapters.md` |

## IoC Lifetimes

| Lifetime | Use For |
|----------|---------|
| `Reuse.Singleton` | Services, engines, caches |
| `Reuse.Transient` | Hooks, stateless helpers |

## Test Coverage Requirements (ADR-008)

| Component | Required | Notes |
|-----------|----------|-------|
| Services | 100% | Must be mockable via constructor injection |
| Engines | 100% | Pure functions — easy to test |
| Hooks | 80%+ | Use `NSubstitute` mocks for adapters |
| Entry Points | Not required | Harmony/GameModel — test via game |

## Entity State Matrix (MANDATORY for OnGameLoaded behaviors)

Any `CampaignBehaviorBase` that **mutates Hero/Settlement/Clan state on load** must enumerate all possible entity states before writing the mutation code. Build a state matrix:

| State | Key Properties | Should mutate? |
|-------|---------------|----------------|
| (each possible state) | (property values) | Yes/No + why |

**Why:** Review #23 found a HIGH bug where `EnsureCompanionsPlaced()` teleported recruited companions out of the player's party on load because the "skip if already placed" check didn't account for traveling-with-party state. The state matrix would have caught this at design time.

**Rule:** If your OnGameLoaded handler calls `ChangeState`, `EnterSettlementAction`, `SetHeroRace`, or any other state-mutating action on a Hero, enumerate:
- Unrecruited / idle in settlement
- Recruited / in player party (traveling on map)
- Recruited / in player party (visiting settlement)
- Dead / disabled
- Prisoner
- Fugitive

Skip any state where mutation would corrupt the entity.

**Idempotent vs destructive:** Before copying a behavior pattern from another feature, ask: "Is this operation idempotent?" Injecting a banner color twice is harmless. Moving a Hero between locations is destructive. Destructive load-path operations need stricter guards than their new-game counterparts.

## Config Providers MUST Validate (MANDATORY for user-editable JSON/XML, MCM settings, AND editor-visible fields on engine-discovered classes)

Any provider or boundary class that exposes user-editable values must validate semantic constraints after deserialization or before consumption, not just syntax. Parse success is NOT validation success. This rule covers **three** source categories, all functionally identical (user-editable, untrusted, flow into comparisons + native engine calls):

1. **JSON/XML config files** under `Main/_Module/ModuleData/` (the original case — RevoltTuning, EditorCacheRebuild config).
2. **MCM settings** exposed via the in-game settings UI (e.g., `TaomSettings.RebuildDistanceCacheAction` lambda parameters).
3. **Editor-visible fields** on engine-discovered classes — public instance fields with `[EditableScriptComponentVariable]` on `ScriptComponentBehavior` subclasses, public fields on `GameModel` subclasses that the editor surfaces, equivalent attributes on `CampaignBehaviorBase` that the engine reads. **The map author / player edits these directly in the scene/campaign editor — they're config in every functional sense even though there's no JSON file.**

**Rule:** Before any comparison, range check, or pass-through to a native engine call, the consumer (loader for category 1, boundary class for categories 2 and 3) must:
1. Range-check every numeric field against its engine-valid bounds
2. Enforce ordering invariants between related fields (e.g., warning-threshold ≥ trigger-threshold)
3. Reject sign flips on fields whose meaning is directional (penalties must be ≤ 0; bonuses must be ≥ 0)
4. **For every `float`/`double` field: reject `NaN` and `±Infinity` BEFORE the range check.** IEEE-754 NaN comparisons always return `false`, so `value < min || value > max` evaluates `false` for NaN and the bad value sneaks through. Use `TAOM.Core.Validation.FiniteFloatValidator.IsFinite[InRange|AtMost|AtLeast]` — never write bare `< min || > max` checks on floats. **This bug has shipped three times** (Career cooldown review #31 — NaN cooldown made ability "always ready"; EditorCacheRebuild review #38 — NaN smoke-test tolerance silently disabled the parallel-safety gate; scene-scripts CS_Road 2026-05-13 — NaN `Width` / `ElevationOffset` / `RepeatU`/`RepeatV` flowed through to native `Mesh.AddTriangle` because the rule was only documented for category 1 and the script-author didn't classify editor fields as config).
5. Log a warning and fall back to the compiled default for any field that fails — never silently apply a bad value
6. Emit a summary warning when any reversion occurred so the user knows to look at prior warnings
7. **When a value is settable from BOTH JSON and MCM, enforce the same range/ordering invariants at BOTH surfaces (or centralize the clamp).** Two entry points validated by different authors drift: CombatMechanics (2026-07-02) — the JSON provider enforced `autoKnockdownWeightRatio ≥ neutralWeightRatio` (Branch A below neutral would auto-floor ordinary horse charges), but the MCM slider clamped to a bare `[2,30]`, so slider values 2–5 recreated exactly the state the JSON invariant existed to prevent. Fix pattern: the settings provider derives its clamp floor from the validated config (`Math.Max(sliderMin, ceil(neutral))`), and the MCM hint documents the floor.

**Why:** Review #25 (RevoltTuning) found a HIGH bug where the provider logged "Loaded" success for any parseable file. A plausible user edit like a sign-flipped penalty `1.0` (should be `-1.0`) would silently flip the feature from "soften revolts" to "accelerate revolts" with no warning. Syntax-error tests (missing file, malformed JSON) did not cover this class of failure.

**Especially validate any string field the CONSUMER branches on.** If a template/service does `switch (value)` / `if (value == "X")` with an `else`/default fallback, an unrecognized value (a typo) silently takes the default and changes mechanics — the field is "parsed-but-unresolvable" (the M1 trap). Validate the field against the known set at load and SKIP+warn on an unknown non-empty value; allow empty only if empty is the explicit, intended default. Example (Codex review #61): `LotrIssueConfigProvider` passed Combat `variant` through unvalidated, and `CombatLotrIssue` routed any unknown value to its DefeatRaids `else` branch, so `variant="CaptureLord"` (typo) silently became a different quest. Fixed with a `ValidCombatVariants` gate + per-value tests.

**Test requirement:** Tests must cover semantically-invalid-but-parseable values for every validated field — not just missing-file and malformed-JSON cases. One test per validation rule.

**Doc requirement:** When documenting "edit this file to retune," state the reload scope explicitly. `Reuse.Singleton` providers (the TAOM default) cache for the entire Bannerlord process — changes require a full application restart, not a new campaign or save-load. Never claim "next game load" without cross-checking the DryIoc lifetime.

## Engine-Float Decision Gates: NaN Must FAIL the Gate (MANDATORY — the runtime sibling of the config rule above)

The rule above protects floats at LOAD time. It does nothing for floats the ENGINE hands in at RUNTIME — momentum, velocity, damage, resistance, health, distance — which arrive per hit/per tick and can be NaN when native state is corrupt. Every NaN comparison returns `false`, so the safety of a gate depends entirely on its polarity:

```csharp
// ❌ WRONG — inverted early-exit: NaN <= 0f is false, so NaN PASSES into the active branch
if (momentumRemaining <= 0f) return false;
/* ... proceed to force SlicedThrough with NaN momentum ... */

// ✅ RIGHT — positive requirement: NaN > 0f is false, so NaN fails the gate
if (!(momentumRemaining > 0f)) return false;

// ✅ RIGHT — owned-verdict services defer to vanilla on garbage, never emit a verdict computed from NaN
if (float.IsNaN(speedFactor) || float.IsNaN(context.VictimKnockDownResistance)) return null;
```

**Rule:** for every decision gate on an engine-sourced float, write the gate as a **positive requirement** to proceed (or add an explicit `float.IsNaN` guard), and for `bool?` fall-through services return `null` on non-finite input so vanilla decides instead of an owned true/false computed from garbage. Add one NaN unit test per gate.

**Why:** 4th instance of the NaN-gate bug class (Career cooldown #31, EditorCacheRebuild #38, CS_Road 2026-05-13 — all CONFIG floats, which produced the loader rule; CombatMechanics 2026-07-02 — ENGINE floats: `momentumRemaining <= 0f` passed NaN and could force cleave chains, a NaN charge velocity became an owned `false` suppressing knockdowns vanilla would grant). Each recurrence happened because the rule's scope was one category narrower than the bug. This section closes the runtime category; if a 5th instance appears in a category this section doesn't name, widen the scope again rather than patching the instance. Enforced at review time by `/deep-review` Agent 5 rule 4b. RCA: `docs/reviews/rca-combat-mechanics-2026-07-02.md`.

## Lookup Functions With Fallbacks: Validate Before Lookup (MANDATORY)

When a lookup function MAY return a "default" or "fallback" value for invalid input (with a warning log, sentinel value, or coerced default), the caller MUST validate the input's validity BEFORE the lookup whenever the result is used as a comparison key in a security/correctness decision. The fallback exists for *logging-and-survival*, NOT for *acceptance*.

**The trap:** the fallback masks invalid input as a "valid-looking" value that happens to match the allow-list, causing silent acceptance of state the caller would have rejected if it had known the input was invalid.

**The rule:** if a lookup function can return a fallback, treat that lookup as "best-effort name resolution for diagnostic output" and add an explicit validity gate before any decision logic depends on the result.

```csharp
// ❌ WRONG — invalid IDs silently coerced to "human" sneak past allow-list when culture allows "human"
var raceName = _raceManager.GetRaceNameFromId(faceGenRaceId);  // returns "human" for unknown IDs
bool allowed = cultureData.Races.Any(r => r == raceName);
if (allowed) {
    PreserveValue(faceGenRaceId);  // ← invalid integer preserved
}

// ✅ RIGHT — validate the input BEFORE the lookup, treat invalid as "not allowed"
bool valid = _raceManager.IsValidRaceId(faceGenRaceId);
var raceName = valid ? _raceManager.GetRaceNameFromId(faceGenRaceId) : null;
bool allowed = valid && cultureData.Races.Any(r => r == raceName);
```

**Why this rule exists:** Codex Review #33 (CharacterCreation race-filter, 2026-05-06). `RaceManager.GetRaceNameFromId` (RaceManager.cs:126-131) returns `"human"` as fallback for unknown IDs. `SetPlayerRace` accepted that fallback name, checked it against the culture's allow-list, and for cultures that allow `human` (Mordor, vanilla cultures, Isengard, Gundabad, Dol Guldur — i.e., most cultures) preserved the original junk integer. `Hero.CharacterObject.Race` accepts arbitrary integers; downstream engine calls would silently receive a corrupt race ID for a Mordor save.

**Applies to:** any lookup function whose XML doc, log line, or implementation says "defaults to X for unknown input" (`GetRaceNameFromId`, `GetCultureData` returning a default culture, `GetItemFromId` returning a default item, `MBObjectManager.GetObject<T>` for missing IDs, etc.). When in doubt, read the function body — if it logs a warning and returns a value, that value is fallback, not validation.

**How to apply:** every `GetXxxFromId` / `LookupXxx` style function should be paired with an `IsValidXxxId` / `ContainsXxx` validator on the same interface. If the validator doesn't exist, the lookup function is effectively unsafe for security decisions and the caller must add validation by some other means (e.g., comparing the returned name against a sentinel default).

**Test requirement:** when fixing a finding of this class, add a regression test where the lookup returns the fallback value and assert the caller rejects the input. Example: `SetPlayerRace_InvalidFaceGenRaceId_DoesNotPreserve_FallsBackToCultureDefault` ([CharacterCreationContentServiceTests.cs](../../TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs)).

**Sibling rule:** see "Config Providers MUST Validate" above for the input-validation rule at the LOADER side; this rule is the input-validation rule at the CONSUMER side. Both are needed because the loader's validation may be downstream of mid-process state mutation (e.g., a save-load that brought in junk race IDs from a prior mod version).

## One Engine Type for Many Config Variants: Audit EVERY Type-Keyed Engine Path (MANDATORY)

When a generic template instantiates **one** engine type for **many** logical config variants (the LotrIssues pattern: 27 Combat configs all become `typeof(CombatLotrIssue)`; 14 Deliver → `typeof(DeliverGoodsLotrIssue)`), the engine's `GetType()`-keyed bookkeeping treats all those variants as a SINGLE object. Before shipping such a feature — and when reviewing one — you MUST enumerate **every** engine code path that branches on `instance.GetType()` for that base type and confirm the collapsed-to-one-type behavior is acceptable for each. Finding ONE such path and stopping is the exact miss this rule prevents.

**How to enumerate:** decompile the engine base type + its manager/behavior and grep for `GetType()`, `.GetType() ==`, `IssueType`, `is <Type>`, and any `Dictionary<Type,...>` keyed on the runtime type. For `IssueBase` the set is (v1.4.6): **(1) spawn over-representation score** + **(2) per-settlement/clan "already has this type" zero-out** + **(3) accept gate** (`CheckPreconditions` → `IssueQuestCanBeDuplicated`, default `false`) + **(4) cooldown** (keyed on `type.Name`) + **(5) despawn**. Each is independent; (1)/(2)/(4) only throttle *spawning* (acceptable, documented as a limitation), but (3) is a HARD accept-block — the player could hold at most ONE active quest per template across all configs until `IssueQuestCanBeDuplicated => true` is overridden.

**Why this rule exists:** Codex review #61 (2026-06-20). The 5-agent deep-review's lifecycle agent read `IssueBase.CheckPreconditions` for the per-type spawn saturation but traced only the over-representation SCORE and stopped — it missed the SEPARATE hard accept-gate **in the same method**. One method, two type-keyed mechanisms; the soft one was found, the hard one shipped. Pinned by `LotrIssueTemplateInvariantsTests` (asserts `IssueQuestCanBeDuplicated == true` for all 3 templates) and the deep-review skill's per-type check. RCA: `docs/reviews/rca-lotr-issues-wave0-2026-06-17.md` (Codex pass section).

**Test requirement:** for each engine type-keyed behavior you deliberately opt into (e.g. `IssueQuestCanBeDuplicated => true`), add a reflection/invariant test pinning the override so a refactor can't silently restore the breaking default.

## File Layout

```
Main/Features/MyFeature/
├── IMyFeatureService.cs
├── MyFeatureService.cs
├── MyFeatureIoC.cs          ← Reuse.Singleton registrations
├── Models/
│   └── TaomMyModel.cs       ← GameModel override (if needed)
└── Hooks/
    └── MyPatch.cs           ← Harmony patch (if needed)
Main/Adapters/
├── IMyTypeAdapter.cs
└── MyTypeAdapter.cs
TAOM.Tests/Features/MyFeature/
└── MyFeatureServiceTests.cs
```

## Stale-file re-read

Long sessions edit many files. Cached `Read` content drifts: a teammate-agent may have re-written the same file, a hook or skill may have run `dotnet format`, the user may have edited via the IDE. Editing against stale content produces opaque "no match" failures that look like permission/conflict bugs.

**Rule:** Before editing any C# file you have not Read in the last ~10 tool calls of the current turn, re-Read it.

- Hard signal to re-Read: another agent ran in this turn; `git status` shows changes you didn't make; the Edit tool returns a "string not found" error.
- Soft signal to re-Read: you're about to make >1 edit to the same file, the file is in a hot area (Main/Adapters, GameModels), or it's been more than ~5 minutes wall-clock since you last looked.

The re-Read costs nothing. The Edit failure plus diagnosis costs minutes.
