---
paths:
  - "Main/**/*Provider*.cs"
  - "Main/**/*Config*.cs"
  - "Main/**/*Settings*.cs"
  - "Main/**/*Loader*.cs"
  - "Main/**/*Tuning*.cs"
  - "Client/**/*Settings*.cs"
  - "Main/SceneScripts/**"
  - "TAOM.Tests/**/*Provider*Tests.cs"
  - "TAOM.Tests/**/*Config*Tests.cs"
  - "TAOM.Tests/**/*Settings*Tests.cs"
---

## Config Providers MUST Validate (MANDATORY for user-editable JSON/XML, MCM settings, AND editor-visible fields on engine-discovered classes)

Any provider or boundary class that exposes user-editable values must validate semantic constraints after deserialization or before consumption, not just syntax. Parse success is NOT validation success. This rule covers **three** source categories, all functionally identical (user-editable, untrusted, flow into comparisons + native engine calls):

1. **JSON/XML config files** under `Main/_Module/ModuleData/` (the original case: RevoltTuning, EditorCacheRebuild config).
2. **MCM settings** exposed via the in-game settings UI (e.g., `TaomSettings.RebuildDistanceCacheAction` lambda parameters).
3. **Editor-visible fields** on engine-discovered classes: public instance fields with `[EditableScriptComponentVariable]` on `ScriptComponentBehavior` subclasses, public fields on `GameModel` subclasses that the editor surfaces, equivalent attributes on `CampaignBehaviorBase` that the engine reads. **The map author / player edits these directly in the scene/campaign editor: they're config in every functional sense even though there's no JSON file.**

**Rule:** Before any comparison, range check, or pass-through to a native engine call, the consumer (loader for category 1, boundary class for categories 2 and 3) must:
1. Range-check every numeric field against its engine-valid bounds
2. Enforce ordering invariants between related fields (e.g., warning-threshold ≥ trigger-threshold)
3. Reject sign flips on fields whose meaning is directional (penalties must be ≤ 0; bonuses must be ≥ 0)
4. **For every `float`/`double` field: reject `NaN` and `±Infinity` BEFORE the range check.** IEEE-754 NaN comparisons always return `false`, so `value < min || value > max` evaluates `false` for NaN and the bad value sneaks through. Use `TAOM.Core.Validation.FiniteFloatValidator.IsFinite[InRange|AtMost|AtLeast]`: never write bare `< min || > max` checks on floats. **This bug has shipped three times** (Career cooldown review #31: NaN cooldown made ability "always ready"; EditorCacheRebuild review #38: NaN smoke-test tolerance silently disabled the parallel-safety gate; scene-scripts CS_Road 2026-05-13: NaN `Width` / `ElevationOffset` / `RepeatU`/`RepeatV` flowed through to native `Mesh.AddTriangle` because the rule was only documented for category 1 and the script-author didn't classify editor fields as config).
5. Log a warning and fall back to the compiled default for any field that fails: never silently apply a bad value
6. Emit a summary warning when any reversion occurred so the user knows to look at prior warnings
7. **When a value is settable from BOTH JSON and MCM, enforce the same range/ordering invariants at BOTH surfaces (or centralize the clamp).** Two entry points validated by different authors drift: CombatMechanics (2026-07-02): the JSON provider enforced `autoKnockdownWeightRatio ≥ neutralWeightRatio` (Branch A below neutral would auto-floor ordinary horse charges), but the MCM slider clamped to a bare `[2,30]`, so slider values 2 to 5 recreated exactly the state the JSON invariant existed to prevent. Fix pattern: the settings provider derives its clamp floor from the validated config (`Math.Max(sliderMin, ceil(neutral))`), and the MCM hint documents the floor.
8. **A string handed to a native call is bounded at load** to the engine's buffer and the character set its own data uses, reverting with a warning (Race Ability glow, 2026-10-04: a particle-effect name went into a 64-byte `strcpy_s`; `docs/reviews/rca-race-ability-glow-2026-10-04.md` G1).
9. **A provider read on a hot path (per hit, per frame, per agent update, per campaign tick) takes the MCM object once** in a private lazy `Settings` accessor, reads through it, and joins `HotPathSettingsProvidersTests` (mission) or `CampaignHotPathSettingsProvidersTests` (campaign) in the same commit, because nothing else points a new provider at those gates (`lessons/misc.md`).

**Why:** Review #25 (RevoltTuning) found a HIGH bug where the provider logged "Loaded" success for any parseable file. A plausible user edit like a sign-flipped penalty `1.0` (should be `-1.0`) would silently flip the feature from "soften revolts" to "accelerate revolts" with no warning. Syntax-error tests (missing file, malformed JSON) did not cover this class of failure.

**Especially validate any string field the CONSUMER branches on.** If a template/service does `switch (value)` / `if (value == "X")` with an `else`/default fallback, an unrecognized value (a typo) silently takes the default and changes mechanics: the field is "parsed-but-unresolvable" (the M1 trap). Validate the field against the known set at load and SKIP+warn on an unknown non-empty value; allow empty only if empty is the explicit, intended default. Example (Codex review #61): `LotrIssueConfigProvider` passed Combat `variant` through unvalidated, and `CombatLotrIssue` routed any unknown value to its DefeatRaids `else` branch, so `variant="CaptureLord"` (typo) silently became a different quest. Fixed with a `ValidCombatVariants` gate + per-value tests.

**Test requirement:** Tests must cover semantically-invalid-but-parseable values for every validated field: not just missing-file and malformed-JSON cases. One test per validation rule.

**Doc requirement:** When documenting "edit this file to retune," state the reload scope explicitly. `Reuse.Singleton` providers (the TAOM default) cache for the entire Bannerlord process: changes require a full application restart, not a new campaign or save-load. Never claim "next game load" without cross-checking the DryIoc lifetime. Never flip a compiled MCM default under an unchanged name: MCM persists per property and loads the saved value over the default, so the new value would reach fresh installs only. Rename the setting (comment why) and the new default reaches every player; `python tools/check_mcm_default_flips.py` checks it (#1017). The renamed setting's `HintText` tells the player their saved value was reset to the new default and to set their own again (RCA `docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md`).
