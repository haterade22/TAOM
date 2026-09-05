# RCA — War of the Ring phase-day ordering, deep-review (2026-07-30)

**Top line:** A user request to move the Rohan/Isengard war to Day 30 and the full War of the Ring to
Day 44 was a four-constant retune. The `/deep-review` on that retune found the constants correct but
surfaced a pre-existing defect underneath them: `CheckPhaseTransition`'s two guards are sequential
`if`s, so any equal or inverted `(phase1Day, phase2Day)` pair runs BOTH transitions inside one call
and the Isengard war is never observable. Three of the four value sources could produce such a pair;
the MCM sliders — the path that actually governs in-game — were validated by nothing at all. Fixed
in-session with a centralized clamp plus tightened load-time validation; 24 new tests, suite green
(4529).

## What the retune itself got right, and what it got wrong

The four constants (shipped JSON, MCM defaults, `TaomSettingsProvider` fallbacks,
`WarOfTheRingConfig` compiled defaults) were set correctly and verified. Engine timing was checked
against the installed v1.4.7 DLLs and cleared: `Campaign.CreateCampaignEvents` re-aligns the daily
periodic event to an exact tick multiple of `CampaignStartTime` on both new-game and load, and
`CampaignTime.Days(1f)` is bit-exact at 864,000,000 ticks, so Day 30/44 carries no fractional-drift
risk that Day 2/14 did not.

The mistake was in **how** the retune avoided a collision it had already noticed. `PhaseConfig`'s
own default is `TriggerDay = 30`, so `Phase1` and `Phase2` both defaulted to 30 — equal days. The
retune fixed that by giving `Phase2` a distinct compiled default of 44 and wrote a comment
explaining why. That comment is evidence the equal-days hazard was **understood at the time** and
then closed for exactly one of four sources — the narrowest one (JSON missing or unparsable). The
generalisation from "these two defaults collide" to "any source can produce a colliding pair, and
the validator's strictly-`<` check permits it" was never made.

## Findings

| # | Sev | Bug | Category | Why it was missed | Preventive action |
|---|-----|-----|----------|-------------------|-------------------|
| 1 | HIGH | MCM `Phase1TriggerDay`/`Phase2TriggerDay` had no cross-field ordering validation. `WarOfTheRingConfigProvider.ValidateConfig` guards only the JSON object; `GetEffectivePhaseDays` prefers the MCM pair whenever `IsAvailable` (i.e. whenever MCM is installed — the common case) and returned it verbatim. Both sliders are individually legal in `[1,365]`, so Phase 1 = 100 / Phase 2 = 44 is reachable with no error, no warning, and no correction. MCM 5.12.1's `[SettingPropertyInteger]` min/max are UI-only metadata — `BaseSettingsJsonConverter.ReadJson` assigns the deserialized value with no range check — so a stale or hand-edited settings file bypasses even the slider bounds. | Dual-surface validation (2nd instance) | The retune treated the MCM values as "just constants I'm changing" rather than as a **user-editable config surface** subject to the same validation rule as the JSON. `csharp-architecture.md` §"Config Providers MUST Validate" point 7 covers exactly this and was not consulted, because the change did not *look* like config-provider work — it looked like editing four numbers. | `GetEffectivePhaseDays` now clamps `phase1 = max(1, phase1); phase2 = max(phase1+1, phase2)` after source selection. Clamping at the single point of use rather than per-provider covers all four sources at once, including a substituted `ITaomSettingsProvider` in tests. |
| 2 | HIGH | Equal days pass validation and collapse the escalation. `ValidateConfig` used strictly `<` (`Phase2 < Phase1`), so `30 == 30` sailed through. `TestMode.Phase1Day`/`Phase2Day` were never inspected by `ValidateConfig` at all. With an equal pair, `CheckPhaseTransition` enters `IsengardWar` and — because the second guard is a separate `if`, not `else if`, and `TransitionToPhase` mutates `CurrentPhase` synchronously — immediately overwrites it with `FullWar` in the same call. `DeclareConfiguredWars` does run, so Rohan is attacked, but no external reader (map meter, momentum service, save) ever observes `IsengardWar`. Both transitions log normally, so nothing looks wrong. | Ordering invariant / off-by-one operator | The `<` operator reads as correct at a glance — it *is* the right check for "Phase 2 must not precede Phase 1." The actual requirement is strictly-after, and the difference only matters because of the sequential-`if` structure in a *different file*. Reviewing the validator in isolation cannot reveal this; it needs the consumer's control flow in view at the same time. | `ValidateConfig` now uses `<=` and applies the same pair of checks to `TestMode`. Kept alongside the service clamp deliberately: the clamp guarantees the invariant, the validator **warns the author** that their edited JSON is wrong, which a silent clamp cannot do. |
| 3 | LOW | The Test Mode MCM tooltip promised "(2/5 days)" while the shipped JSON has been 1/3 since 2026-05-22, and JSON always wins for any key it contains. `TestModeConfig`'s compiled defaults were also still 2/5. | Doc/code drift | The 2026-05-22 retune tightened `testMode` to 1/3 in the JSON and updated neither the tooltip nor the compiled fallback. Two months of reviews passed over it because no test and no linter compares a `HintText` string against the value it describes. | HintText now names the source file and says 1/3; `TestModeConfig` defaults moved to 1/3 so the missing-JSON fallback matches shipped intent. |
| 4 | LOW | `docs/features/diplomacy.md`'s body was updated to 30/44 but its Changelog section still ended at "2026-05-22 — retuned to Day 2 / Day 14", so the file contradicted itself. | Doc completeness | Introduced by the retune. Two feature docs describe this system; the changelog of the sibling doc was updated and this one was not, because the body edits were driven by grepping for stale *numbers* and the changelog contained no stale number — it was correct-as-history but incomplete. | Mirrored entry added. When a change updates one doc's changelog, grep for every other doc describing the same system. |

## Root-cause pattern: the rule fires on the *surface*, not on the *diff size*

Findings 1 and 2 share one cause. A four-constant edit does not present as config-provider work, so
the config-validation rule never came to mind — even though every one of those four constants **is**
a user-editable config value, and one of them is a live in-game slider. The rule's trigger is the
nature of the value, not the size or shape of the change that touches it.

This is the **second instance** of the dual-surface class. The first was CombatMechanics
(2026-07-02): the JSON provider enforced `autoKnockdownWeightRatio >= neutralWeightRatio` while the
MCM slider clamped to a bare `[2,30]`, so slider values 2–5 recreated exactly the state the JSON
invariant existed to prevent. Same shape here: JSON validated, MCM not, and the MCM path is the one
players actually use. A repeat offender needs a mechanical check, not a stronger reminder.

The sharpest evidence that reminders are insufficient:
[`MomentumSettingsProvider`](../../Main/Features/WarOfTheRingMomentum/MomentumSettingsProvider.cs) —
a sibling feature in the same folder — already implements this pattern correctly, and its own doc
comment cites *"TaomSettingsProvider precedent"* for it. `TaomSettingsProvider` does not implement
what that comment claims. A doc comment asserting a precedent is not evidence the precedent exists;
it was written by an author who assumed rather than opened the file.

## Why each deep-review agent missed it before the fix

These findings came from *this* review; the point below is why they survived the 2026-05-13 and
2026-05-22 passes over the same code.

- **Standards / architecture** — checks ADR conformance per file. `TaomSettingsProvider` is four
  clean pass-through properties and `ValidateConfig` is a well-formed validator; neither is a
  standards violation in isolation. The defect only exists in the *relationship* between them.
- **API compatibility** — scoped to engine signatures. Every TaleWorlds call here is correct; this
  pass has no reason to open an MCM settings class.
- **Efficiency** — `CheckPhaseTransition` is genuinely clean (no allocation, no LINQ, one-time
  transitions). Correct verdict, wrong question for this bug.
- **Completeness** — counted 25 existing tests and read them as good coverage. It did not ask
  *which branches* they cover. Every test pins `IsAvailable = false` in `Setup()`, so the entire
  MCM branch of `GetEffectivePhaseDays` and `GetEffectiveEnabled` had **zero** coverage — the single
  fact that best predicted this bug, invisible to a test *count*.
- **Data flow** — this is the agent that found it, once pointed at the four-source fan-in. Earlier
  passes traced the JSON→service chain (which is validated and correct) and stopped there, never
  enumerating the MCM chain as a separate flow into the same consumer.

## Lessons to codify

Appended to `docs/reviews/lessons/gamemodels-services.md`:

1. **A cross-field invariant belongs at the point of use, not replicated per surface.** Per-source
   validation scales linearly with sources and drifts; N sources means N chances to forget the
   N+1th. Clamp once where the values converge, and keep per-source validation only for its distinct
   job — telling the author their input was wrong.
2. **"Config" is defined by the value, not by the diff.** Any user-editable value — JSON, MCM
   slider, editor-visible field — carries the validation rule with it, including when the change
   that touches it is a one-line constant edit.
3. **A guard whose correctness depends on control flow in another file must be reviewed with that
   file open.** `<` vs `<=` here is only wrong because `CheckPhaseTransition` uses sequential `if`s.
4. **A doc comment citing a precedent is a claim, not evidence.** Open the cited file before relying
   on it. `MomentumSettingsProvider` cited a `TaomSettingsProvider` pattern that did not exist.
5. **Test counts do not measure branch coverage.** When a service selects among N sources, assert
   that a test exercises each; a uniform `Setup()` that pins one branch hides the rest behind a
   healthy-looking total.

## Verification

RED confirmed before any production edit — 5 service tests failed with `Expected:<IsengardWar>.
Actual:<FullWar>` across the MCM, JSON, and TestMode sources, and 4 provider tests failed on the
newly-validated rules. After the fix: 4529 passed, 0 failed, 2 skipped (pre-existing warg-attack
skips). 24 tests added — 6 covering the previously-uncovered MCM branch, 12 in the new
`WarOfTheRingConfigProviderTests` (none existed; gap flagged in `plans/_audit/2026-06-12-harvest.md:244`),
6 pinning the shipped JSON so doc/code drift is caught by the suite rather than by review.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
