# RCA — CastleRecruitment missing-template guard deep-review findings (2026-07-07)

**Scope:** deep review (5 agents: standards, api-compat, efficiency, completeness, data-flow) of the #332 fix — `CastleNotableMaintainer` template pre-check + per-castle fail-safe. Standards/efficiency/completeness passed clean; api-compat and data-flow returned 3 findings (2 MEDIUM, 1 LOW), independently cross-confirmed on the central one. All verified against the installed v1.4.6 DLLs before fixing (`HeroCreator.CreateNotable`/`CreateHero`, `DefaultHeroCreationModel.GetRandomTemplateByOccupation`, `CultureObject.Deserialize`, `MBObjectManager.ReadObjectReferenceFromXml:1497-1515`).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | `if (HeroCreator.CreateNotable(...) == null)` is dead code — CreateNotable either returns a Hero or throws (`CreateHero` derefs the template before any return path); the comment implied it was a real safety net | api-verification | The null-check was copied from the `CultureConversionAdapter.ReplaceNotable` precedent (#325) without re-verifying the API's actual failure mode — the precedent carries the same dead branch | Kept as an explicit **forward guard** with an honest comment ("unreachable on v1.4.6 — CreateNotable throws rather than returning null") in BOTH call sites; the comment now names the real guards (pre-checks + catch) |
| 2 | MED | The pre-check's `Any(t => t != null && t.Occupation == occupation)` tolerates null entries that the ENGINE's own filter (`Where(x => x.Occupation == occupation)`, no null check) NREs on — a culture with a null `NotableTemplates` entry (malformed `<notable_templates>` ref; `ReadObjectReferenceFromXml` returns null on a missing `name` attribute) passes the pre-check and still throws, bypassing the warn-once dedup → daily `LogError` spam on ticking castles | data-flow / engine-mirror mismatch | The pre-check mirrored the engine's *occupation* filter but not its *null-handling* — the `t != null` term made OUR lambda safe while silently diverging from the engine lambda it was predicting. Same root as #1: the #325 precedent's shape was trusted as "the known-good guard" | Null-entry gate added BEFORE the occupation loop in `CastleNotableMaintainer` (skip culture entirely + warn once per culture) and propagated to `CultureConversionAdapter.ReplaceNotable`. Lesson generalized in LESSONS-LEARNED (see below) |
| 3 | LOW | No unit test pins the guard behavior (pre-check skip, warn-once dedup, no-escape-from-`EnsureAllCastles`) | test-coverage | Not missed — deliberate: `CastleNotableMaintainer` is engine-glue (statics: `Settlement.All`, `HeroCreator`) exempt per ADR-008; documented as "game-tested by convention" in the feature doc since 2026-06-12 | Declined (simplicity criterion: an abstraction layer added only to make a boundary testable is complexity without behavior gain). Recorded here so the decision is written down |

## Root-cause pattern

**A guard copied from a precedent inherits the precedent's unverified assumptions.** The #325 pre-check was treated as the proven recipe and transplanted verbatim — including its dead `== null` branch (wrong claim about the engine's failure mode) and its null-*tolerant* match that diverges from the engine's null-*intolerant* filter. A pre-check that predicts an engine decision must replicate the engine expression's semantics exactly, null-handling included; every term you add for your own safety (`t != null`) is a term the engine does not evaluate and therefore a case where your prediction and its behavior part ways.

Same shape as the C++-port rule (`feedback_native_port_hot_path_audit.md`: "upstream worked ≠ port is fit") and the sibling-hook rule (`harness-facts.md`: mirror the sibling's FULL convention set) — precedent-copying substitutes for verification.

## Why each agent missed/caught these

- **standards / efficiency / completeness (haiku):** correctly out of scope — findings 1-2 are engine-semantics questions their rule sets don't ask.
- **api-compat (sonnet, taleworlds-researcher):** caught both — by decompiling the installed `HeroCreator` instead of trusting the diff's comments. This is the agent whose job is exactly "verify the API's failure mode"; it did.
- **data-flow (sonnet):** caught #2 independently (rated MED vs api-compat's LOW — the MED rating stands because of the un-deduped daily error spam) and cleared the two claims that mattered most: the weighted-random draw cannot return null on a non-empty filtered list (so the pre-check fully covers the #332 path), and the warn-once HashSet is per-campaign (behavior re-constructed per `OnGameStart`), so no cross-campaign suppression.
- **The original fix author (this session):** wrote the guard from the #325 precedent + the crash stack without re-decompiling `CreateNotable`'s return path — the precise gap `evidence-over-claims` §A warns about when the "reviewer" being trusted is your own prior code.

## Outcome

- Null-entry gate + honest forward-guard comments in `CastleNotableMaintainer.EnsureCastleNotables` and `CultureConversionAdapter.ReplaceNotable`.
- Suite green after fixes: 4,153 passed / 0 failed.
- LESSONS-LEARNED "Campaign Mechanics" entry corrected: the canonical `CreateNotable` pre-check recipe now includes the null-entry gate (the entry as first written taught the gapped recipe).
