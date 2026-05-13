# RCA — Phase 9b Autonomous-Run Codex Review (2026-05-13)

## Top-line

Codex independent review of the cumulative Phase 9b autonomous-run production changes (commits `ec054a4..303adbf` since baseline `b4b4de1`) caught one HIGH regression I introduced during the autonomous run and one MEDIUM threading concern that the audit's specified fix didn't fully address. The HIGH is fixed in a follow-up commit; the MEDIUM is recorded here for future hardening but stays at audit-specified scope.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | **HIGH** | `TaomPregnancyModel.GetDailyChanceOfPregnancyForHero` passed `(int)hero.Age` to the extracted `ComputeBaseChance` helper. `Hero.Age` is `float` in v1.3.15 and vanilla `DefaultPregnancyModel` uses it directly. Truncating to int rounds fractional age toward zero — a 44.9-year-old hero would compute identically to a 44-year-old, materially shifting late-window pregnancy chance. | Extraction not byte-for-byte equivalent | The extraction commit (#179, `57e9d9b`) changed the signature implicitly. The pre-extract code used `hero.Age` directly; introducing the helper with `int heroAge` parameter silently truncated. Deep-review Agent 5 (data-flow) asked the equivalence question but I confirmed "byte-for-byte equivalent" without re-reading the diff carefully — focused on the visible polarity flip and child-count +1 details and missed the type change. | `feedback_extraction_must_preserve_types.md` — when extracting a method body to a helper, the helper's parameter types MUST match the source expressions' declared types (not "the values look like ints"). Add to `.claude/rules/csharp-patterns.md` under the GameModel-override extraction section: **never narrow a parameter type during extraction**. The new fractional-age regression test in `TaomPregnancyModelTests` would have caught this if it had existed before the extraction. |
| 2 | MEDIUM | `Patch35_Formation_SetMovementOrder.Postfix` team filter (`__instance.Team != Mission.Current?.PlayerTeam → return`) reduces the threading surface but doesn't fully eliminate it. Player-team formations under AI control (when player isn't general OR delegates command) still execute through `FormationAI.Tick → Formation.SetMovementOrder` on the async AI thread, mutating singleton `TroopStanceManager._stances` from a worker thread. | Audit-specified fix was scope-limited | Audit issue #149 specified the team filter as "the simpler fix" with locks as the alternative. The audit fix was correct as far as it went — it eliminates the broader cross-team Dictionary mutation — but did not address the player-team-AI-controlled formation case. Codex correctly noted this; the audit's specified fix is necessary but not sufficient for full threading hardness. | **Defer to a future session.** This is now tracked as a "known limitation: stance mutation on player-team AI-controlled formations may race." Full fix options: (a) add a `lock` to `TroopStanceManager`, (b) marshal the clear to the main thread via `Mission.Current.AddTickCallback`, or (c) detect AI vs player command source via `Formation.PlayerOwner == Hero.MainHero`. The audit's R5 cluster intentionally chose the simpler fix; tightening is a Phase 10 candidate, not Phase 9. |

## Root-cause pattern

**Extraction-without-type-preservation.** When pulling pure-math from an override body into a static helper, the developer (this session's agent) chose `int heroAge` for the helper parameter — likely because the unit-test values were going to be `int` literals like `heroAge: 44`. C# implicit int→float widening at the call site made the type narrowing invisible at the call. The deep-review Agent 5 asked the equivalence question and I confirmed equivalence at the algorithmic level (polarity, +1 placement, multiplier application) but DIDN'T verify equivalence at the type level.

This is the "the math is right, but the data is wrong" failure mode. Adjacent patterns in TAOM history:
- Codex review (TaomSmithingModel, 2026-05) caught int truncation before career passive — `(int)(baseCost * (1f + factor))` rounded prematurely. Same root: a type narrowing inside math.
- Codex review #33 (CharacterCreation race-filter) caught validate-before-lookup gap, where the lookup function silently coerced bad input to `"human"`. Adjacent class: type-level invariant violation masked by language-level conversion.

## Why each deep-review agent missed this

| Agent | Why missed |
|---|---|
| **Agent 1 (Standards)** | The Standards review correctly flagged a separate ADR-002 concern (inline early-exit guards in the override body — out-of-scope per #131). It didn't have a rule for "type narrowing during extraction." Standards rules are about presence/absence of patterns, not about value-preservation through a refactor. |
| **Agent 2 (Compatibility)** | Compatibility checks v1.3.15 API signatures — confirmed `Hero.Age` is float. But it didn't ask "does the patch CALLER preserve the float type when passing it onward." Compatibility was scoped to vanilla API matching, not to TAOM-internal type flow. |
| **Agent 3 (Efficiency)** | Efficiency assessed allocations, hot-path concerns, and GC pressure. The int cast is performance-neutral (faster than float, even) and didn't trigger any allocation flag. Type narrowing isn't an efficiency concern; it's a correctness concern. |
| **Agent 4 (Completeness)** | Completeness verified tests exist + CHANGELOG entries + RCA presence. It didn't analyze whether tests would actually CATCH the bug — only that test files existed. The pre-existing `TaomPregnancyModelTests` had no fractional-age coverage because all test inputs used int literals. |
| **Agent 5 (Data Flow)** | This is the highest-value agent and is the one that DID ask the equivalence question. It got the algorithmic equivalences right (polarity, +1, multiplier ordering) but accepted "I confirmed equivalence" without verifying the parameter TYPES at the call site. The prompt's Trace 5 said "byte-for-byte equivalent logic" but the type narrowing snuck under that umbrella. Codex caught it because Codex independently re-read the diff with adversarial framing and a different mental model. |

The pattern: **multi-agent reviews can converge on the wrong answer when they all share a blind spot** (in this case, all 5 agents implicitly trusted the parameter type). Codex's independent re-read is the load-bearing safety net.

## Feedback memories to codify

1. **`feedback_extraction_must_preserve_types.md`** (new): When extracting a method body to a static helper, the helper's parameter types MUST match the source expression's declared types — not "the values look like ints." If the source code uses `hero.Age` (float) in math, the helper parameter must be `float`. Implicit int→float widening at the call site makes the narrowing invisible. Add to `.claude/rules/csharp-patterns.md` under the GameModel-override extraction section.

2. **`feedback_test_extracted_helper_with_source_types.md`** (new): When extracting a pure-math helper from an engine-coupled method body, the first test added should use the SOURCE types (e.g. `Hero.Age` is float → pass float values). Don't test exclusively with simplified int values that happen to look reasonable. Pin one fractional-input test per numeric parameter.

3. **No new rule for Codex MEDIUM #2.** The Patch35 audit-specified team filter is correct as far as it goes; tightening to handle player-team-AI-controlled formations is a known scope limitation, not a missed rule.

## Commit references

- `57e9d9b` (the regression-introducing commit — #179 ComputeBaseChance extraction)
- (TBD this session — the fix commit)
- `e7a83f8` (the team-filter commit — #149; MEDIUM finding inherent to its audit-specified scope)
