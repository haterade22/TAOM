# Codex Pre-Review Findings: career-enum-prefab-cleanup (2026-07-06)

> Findings-only record; the full session transcript was not retained.
> Prompt: `codex-prereview-career-enum-prefab-cleanup-2026-07-06.prompt.md`. Tokens used: ~138k.
> Note: `dotnet test` was blocked in the Codex sandbox (dotnet first-time-setup
> UnauthorizedAccessException); the suite was run green by the orchestrator instead.

## Findings

P3 | `TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs:102` | Rename completeness
not grep-clean: old vocabulary survived in test METHOD NAMES — `HorseChargeDamage` (102/115),
`HorseHealth` (260/270), `ShruggedOff` (454/461), and `CustomResource*` in
`TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs:807/832/872/886`.
Production/XML references clean; `isShruggedOff` in `TaomCombatMechanicsModel.cs:60/62` correctly
preserved (engine parameter name). **FIXED same session** (substring sweep of both test files).

P3 | `TAOM.Tests/Features/CareerSystem/CareerConfigProviderTests.cs:480` | Retired enum name
`WindsOfMagic` survived as the negative-parse test exemplar (lines 480/488). Not shipped
XML/runtime. **FIXED same session** (exemplar → synthetic `NoSuchEffectType`).

## Suspect audit

- A (rename completeness): holds for production + XML; test-name survivors as above (fixed).
- B (deleted members): holds for production + XML; test exemplar as above (fixed).
- C (ordinal safety): CLEAN — no int casts, no value persistence, no Enum.GetValues order use,
  no enum-indexed arrays; saves persist career/choice/tier/flag strings, never this enum.
- D (prefab closure): CLEAN — only CareerFooterSlide/CareerHeaderSlide/CareerNodePanel defined and
  referenced; widths 520 + 1400 = 1920.
- E (LINQ equivalence): CLEAN — `_choiceChangedAction` still fires only after Select/DeSelect.
- F (warning/parse parity): CLEAN — both gates use case-insensitive `Enum.TryParse(..., true, ...)`.
- G (consumers gate): CLEAN — all shipped `type=` values present in `PassiveEffectConsumers`;
  `CareerChoicesIntegrationTests` reads the real `Main/_Module/ModuleData` XML.

P1: 0 | P2: 0 | P3: 2 (both fixed same session)
VERDICT: cleanup-only issues; no functional findings.
