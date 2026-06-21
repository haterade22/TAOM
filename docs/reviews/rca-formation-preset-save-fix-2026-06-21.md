# RCA — Formation Preset save-corruption fix review (2026-06-21)

**Scope.** Phase 3e root-cause analysis for the `/deep-review` (5 agents) + `/review-codex` pass on the Formation
Preset save-corruption fix (CompanionTactics). The fix itself — removing the unserializable
`[SaveableField(3)] DateTime _createdAt` from `HoNFormationPreset` and gating the WIP feature off by default — was
validated as **correct, necessary, and sufficient** for the reported CTD by both reviews (Codex Q1–Q7 all clean;
all 5 deep-review agents PASS on the core change; the field-id gap at 3 is save-load-safe, verified against the
installed `FieldLoadData.FillObject` / `TypeDefinition.GetFieldDefinitionWithId`).

This RCA covers the **two LOW findings on the fix's supporting code** (the new test + the pre-existing definer), both
fixed in-session. No HIGH/MED findings. No current-crash defects — both are future-risk / editor-noise.

## Findings

| # | Sev | Bug | Category | Found by | Why missed | Preventive action |
|---|-----|-----|----------|----------|------------|-------------------|
| 1 | LOW | `HoNFormationPresetSerializationTests` accepted any `List<basic>` / `Dictionary<basic,basic>` by element type without checking the EXACT closed container is registered (would false-pass a future `Dictionary<float,int>`), AND false-rejected registered enums (engine has an `EnumDefinition` branch; `SaveableCoreTypeDefiner` registers enums). | Test precision | deep-review Agent 5 (container half) + Codex (container + enum) | The test (authored this session) used a **structural approximation** of the engine's serialization rule instead of the exact rule (exact closed-type registration; enum registration). Agents 1–4 don't model the engine's container/enum registration semantics; Agent 5 caught the container false-positive but not the enum false-negative. | Test rewritten to an **exact-closed-type allowlist** (basic + registered class + registered container + registered enum), each entry cited to its definer, **failing closed** on unknown types, + a source-parse consistency check tying the TAOM-side entries to the definer. Recurrence of `feedback_mirror_table_drifts_from_production` (a test that approximates production semantics can both false-pass and false-fail). |
| 2 | LOW | `FormationPresetSaveableTypeDefiner.DefineContainerDefinitions()` re-registered `Dictionary<string,int>`, `Dictionary<int,int>`, `List<string>` — all already registered by the engine's `SaveableBasicTypeDefiner` — hitting the `else` branch of `SaveableTypeDefiner.ConstructContainerDefinition` → `Debug.FailedAssert("duplicate definition")` at save-system init (assert-noise in editor/debug; no-op in shipping). Pre-existing (not introduced by this fix). | Engine-API misuse | Codex only | deep-review Agent 2 (API compat) READ the right method but **drew the wrong conclusion** — it called the duplicate registration "a harmless no-op via the `HasDefinition` guard." It read the `if (!HasDefinition) register` happy path and stopped, missing that the `else` branch is a `FailedAssert`, not a silent skip. | Removed the three redundant registrations (kept only the mod-specific `List<HoNFormationPreset>`); verified safe — the engine provides all three (ilspycmd on installed `SaveableBasicTypeDefiner`). Recurrence of `feedback_codex_caught_api_misread.md`: resolved by re-decompiling when Agent 2 and Codex disagreed, per the rule. |

## Root-cause pattern

Both findings are about the **supporting scaffolding mirroring engine behavior**, not the user-facing fix:

- **Don't approximate an engine rule you can state exactly.** A regression guard that asserts "the engine can
  serialize this" must encode the engine's *actual* acceptance rule (exact closed-type registration; enum
  registration), not a structural look-alike. An approximation fails in both directions — it passes types the
  engine rejects (the original crash class) and rejects types the engine accepts.
- **Read the else branch before concluding "harmless."** An agent that reads only a method's happy path
  (`if (!HasDefinition) register`) and reports "no-op on duplicate" has not read the method. The else branch (a
  `FailedAssert`) inverted the conclusion. The fix is the existing rule (re-decompile on reviewer disagreement),
  which worked — Codex's contradiction of Agent 2 triggered the re-decompile that settled it.

## Why each deep-review agent missed finding #2

- **Agent 1 (Standards):** out of scope — no ADR/registration-gap rule covers "engine already provides this container."
- **Agent 2 (API compat):** in scope, READ `ConstructContainerDefinition`, but misclassified the duplicate path as a
  no-op (read the `if`, not the `else`). This is the finding's why-missed.
- **Agent 3 (Efficiency):** out of scope — init-time, not a hot path.
- **Agent 4 (Completeness):** out of scope — tests/docs/issue presence, not engine-call correctness.
- **Agent 5 (Data Flow):** traced field types → definer registrations and confirmed coverage, but its question was
  "is each field type covered?" (yes) not "does the definer register something the engine already owns?" (the
  inverse). It correctly noted the containers are engine-provided but did not flag the duplicate-assert consequence.

## Feedback memories

No new rules — both findings are recurrences of existing, correctly-documented rules that fired as designed:

- `feedback_codex_caught_api_misread.md` — Agent 2 vs Codex disagreement on a TaleWorlds API was resolved by
  re-running ilspycmd (not by siding with the more confident agent). Working as intended.
- `feedback_mirror_table_drifts_from_production.md` — a test mirroring production semantics needs a consistency
  check; the rewrite adds the source-parse tie-in.

Manufacturing a new rule for a LOW, self-caught, already-covered pattern would be rule-bloat. The existing rules held.

## Outcome

Both findings fixed in-session. Build clean (0/0); 84/84 CompanionTactics tests pass (the precision rewrite added 2
test methods). Pre-existing, unrelated `GetVolunteerTroopId_DolGuldur*` failures (9) in the working tree are
data-drift from in-flight troop WIP — out of scope for this fix (documented in the session summary).
