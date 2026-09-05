# RCA — deep-review of the `/improve` audit-fix branches (2026-06-13)

Deep-review (5 dimensions: Standards, API-compat, Efficiency, Completeness, Data-flow) of the four
implementation branches produced by the first `/improve` audit, each adversarially verified.

## Scope reviewed

| Branch | Plan | Change |
|--------|------|--------|
| `impl-001` | Cross-campaign singleton resets | Career + SpecialResources clear singleton dicts on `OnNewGameCreated`; SyncData load-branch uses a null local so an absent save-key yields a fresh empty dict |
| `impl-002` | NaN/Infinity config guards | `FiniteFloatValidator.IsFinite` gate in `TroopWeightXmlLoader` + `MutationParams.GetFloat` |
| `impl-003` | Hot-path perf | SpatialGrid 2s→100ms (#219); `TaomSettings` cached across 8 Patch17 sites + BattleBalance provider + Warg BT decorator |
| `impl-005` | Security/tooling | Scrub vendored BUTR credential + vet-grep; pin MCP servers; `process_faction_map.py` argv (no `python -c` path interpolation) |

Combined merge (`verify-improve`) built clean (0 errors) and the full suite was GREEN
(3160 passed / 1 pre-existing FiefManagement failure / 2 skipped; +18 new tests).

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| 1 | LOW | `impl-002` −Infinity test asserted only `LogWarning(Contains("bad_troop"))`. Both the new finite-guard message and the pre-existing `<=0` (positive) guard message embed the troop id, and `-Infinity <= 0` is *true*, so the assertion could not prove the *new finite* guard was the path that rejected −Infinity (it could have been the legacy positive guard). NaN/Infinity tests do implicitly pin the finite guard (`NaN<=0`/`Infinity<=0` are both `false`, so only the finite guard rejects them). Correctness was never in doubt — `Count==0` proves rejection. | tests / precision | The test asserted on a substring (`bad_troop`) shared by *two* guard messages, so it could not discriminate *which* guard fired for the one input both guards reject. | **Fixed** in `impl-002`: tightened the −Infinity assertion to also `Contains("finite")` (the discriminating word in the new message). General rule below. |

Two further candidate findings (both on `impl-001`'s `SpecialResourcesBehaviorTests`) were raised and **refuted** in verification:
- The `try/catch(NullReferenceException)` around `OnNewGameCreated(null)` — refuted: the catch is narrowed (not bare); the storage-clear runs *before* the `Hero.MainHero` read, so the assertion holds; and if the engine ever returned null instead of throwing, the production `if (hero == null) return;` guard makes the test still pass. The proposed "extract a method" fix is a `simplicity-criterion` reject (abstraction for an already-correct, already-isolated test).
- Reliance on NSubstitute's default `ref`-arg behavior — refuted: it faithfully mirrors the production load path (absent key → ref untouched → `RestoreData(null)` → fresh empty dict), which is the exact contract under test.

## Root-cause pattern: assert the *discriminating* signal, not a shared one

When a test must prove **which of two guards/branches handled an input**, asserting on a value common to both branches (here, the troop id present in *both* warning messages) cannot discriminate — especially for an input that *both* branches would reject (`-Infinity` fails both `IsFinite` and `<=0`). The test passes whether the new code exists or not, so it doesn't actually protect the new behavior.

**Rule (generalizable):** a regression test for a newly-added guard that sits in front of, or beside, an existing guard must assert on the **new guard's discriminating output** (its distinct message text, distinct return, distinct side effect) — not on a signal the pre-existing guard also produces. Pick a test input the new guard *uniquely* handles where possible (NaN/+Infinity here); where the input is handled by both (−Infinity), pin the path explicitly via the discriminating message.

This is a sibling of `tests.md` "Skip-Guard Exhaustion" (write a test per guard) — extended with "and make each test actually attributable to *its* guard."

## Why each deep-review dimension's result was what it was

- **Standards / API-compat / Efficiency / Data-flow:** clean across all four branches. Data-flow specifically confirmed (a) the `impl-001` reset genuinely fixes the cross-campaign leak (`RestoreData(null)` → `?? new Dictionary` → fresh empty dict) and runs before the `Hero.MainHero` read so it cannot wipe a freshly-CC'd career; (b) `impl-002` guards every cited config float entry point; (c) `impl-003` caches the settings ref without a staleness regression and the SpatialGrid cadence change has no consumer that depended on 2s staleness.
- **Completeness** is the only dimension that fired — and only at LOW, on test-assertion precision, not on missing coverage. The fix-quality bar ("does each test pin its own guard?") is finer than "does a test exist?"; the new rule above closes that gap.

## Note on RCA severity scope

Per `.claude/rules/harness-facts.md` + `feedback_root_cause_mandatory.md`, Phase-3e RCA is mandatory for **every** confirmed finding regardless of severity — hence this file exists for a single LOW. The systemic lesson (assert the discriminating signal) is worth more than the one-line fix.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
