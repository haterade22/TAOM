# RCA: zero-cost troop upgrades (#537), and what the review caught in the fix

**Date:** 2026-09-03
**Trigger:** player crash bundle `a7dc3a2091eab4b1e2275cdfcf1a0a994caa0fe2`, TAOM v2.0.23.0 on Bannerlord v1.4.8.119303
**Scope:** the CTD itself, plus six findings the deep review raised against the fix before it shipped

## Top line

Vanilla `DefaultPartyTroopUpgradeModel.GetXpCostForUpgrade` returns 0 for any upgrade edge whose
target does not reach a higher tier, and `CampaignUIHelper.GetTroopXPTooltip` evaluates
`troop.Xp % cost` with no guard. Eleven TAOM upgrade edges had that shape. Ten were deliberate
same-level branches; one was an authoring slip. A player hovering a stack of `dg_uruk_foul` on the
party screen crashed to desktop.

The original defect is the smaller half of this document. Six findings were raised against the
**fix** during review, two of them defects I introduced, and those are where the durable lessons
are.

## The original defect

`CharacterObject.Tier` is `clamp(ceil((level - 5) / 5), 0, MaxCharacterTier)`, a pure function of
the `level=` attribute in the troop XML. When `target.Tier <= source.Tier` the cost loop
`for (i = source.Tier + 1; i <= target.Tier; i++)` never executes and the method returns 0. Three
engine consumers read that zero and all three are wrong at it:

| Consumer | Behaviour at cost 0 |
|---|---|
| `CampaignUIHelper.GetTroopXPTooltip` | `troop.Xp % 0` throws `DivideByZeroException` |
| `PartyUpgraderCampaignBehavior` | gates on `cost > 0`, so AI parties promote the whole stack for gold alone |
| `PartyBase.OnXpChanged` | clamps roster XP to `Number * maxCost`, wiping banked XP every tick |

`PatchShield` swallows only `MissingMethod` / `MissingField` / `TypeLoad`, so this propagated as a
hard CTD rather than a logged blip.

**Why it was never caught:** nothing in the repo related `level=` to a divisor. The existing
`UPGRADE_SKILL_REGRESSION` gate walks the same upgrade edges and even parses `level=`, but only to
put a number in a failure message. The tier ladder was treated as presentation, not as arithmetic
the engine performs.

## Findings table

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Data fix `dg_uruk_foul` 11 -> 6 crossed vanilla's `Tier < 2` prisoner-recruitment gate, making every captured Uruk Foul in existing saves permanently unrecruitable | data / engine-threshold | The level was chosen to satisfy the tier formula and to sit on the canonical ladder. Neither criterion has anything to do with what CONSUMES the resulting tier | Re-laddered the whole uruk line so the entry troop stays tier 2. Lesson: "A data fix that moves a tier is a gameplay change" |
| 2 | MEDIUM | `float span = level + 4` computes in int, so a pathological level wraps negative and `(int)` of the resulting float is `int.MinValue`. A guard whose purpose is "never return a non-positive cost" could return the most negative int there is | float->int cast | The named category in `csharp-architecture.md` was read as being about NaN specifically; this input is an int, so the rule did not feel like it applied | Clamp the level itself before the arithmetic (`MaxLateralUpgradeLevel`), plus tests at `int.MaxValue`. Sixth instance of a category the rule already names |
| 3 | MEDIUM | Wrote a UTF-8 BOM into five files that had none in `HEAD`, by using `encoding='utf-8-sig'` on both read and write | tooling / byte fidelity | `utf-8-sig` is the correct READ encoding here, so reaching for it felt like following the convention. The change is invisible in a diff viewer and the files still parse, build and validate | Restored byte fidelity from `git show HEAD:`. Lesson: "Write generated files with the encoding the file already has" |
| 4 | MEDIUM | `test_max_character_tier_mirrors_the_game_model` asserted `10 == Validator._MAX_CHARACTER_TIER`, both sides python, so it could not fail when the C# override changed | test tautology | The test named the right constant and carried a comment saying the two must move together, which made it read as a pin | Rewritten to regex `MaxCharacterTier => (\d+)` out of `TaomCharacterStatsModel.cs` and fail loudly if the file or pattern is missing |
| 5 | MEDIUM | The new gate returns `[]` identically for "clean", "troops directory renamed" and "no troop carries a level", because both upgrade checks read two literal non-recursive globs | silent gate | The globs were inherited unchanged from `_upgrade_skill_regressions`, so they looked like established convention rather than a blind spot | New `UPGRADE_INDEX_EMPTY` error emitted from the shared index, covering both gates, with five tests |
| 6 | LOW | No test pinned the memoisation refactor, so nothing would have failed if `SKILL_TEMPLATE_SHADOWS_SKILLS` began emitting twice | test coverage | The refactor was verified by reading and by a one-off manual run, neither of which survives into the suite | `SharedIndexTests`: emission counted across both checks, and across two calls on one instance |
| 7 | LOW | An em dash in a new test comment, and `PartyBase.FixExpAndUpgrade` named throughout when the 1.4.8 method is `OnXpChanged` | prose / naming | The dash scan was keyword-filtered and could not see the line. The method name came from reading the clamp body without capturing its signature | Both corrected in code, CHANGELOG and issue. Scan for both dash characters across the whole diff, not a filtered subset |

## Root-cause pattern: the fix was reasoned about only against the formula it had to satisfy

Findings 1, 2 and 4 are the same mistake wearing three hats. Each time, a value was chosen or a
test written so that it satisfied `ceil((level - 5) / 5)`, and the question "what else reads this"
was never asked:

- Finding 1 picked a level that satisfied the tier formula and never asked what reads the tier.
- Finding 2 wrote arithmetic that satisfied the formula and never asked what the cast produces for
  inputs the formula was not thinking about.
- Finding 4 pinned the formula's constant against itself and never asked which file an editor would
  actually touch.

The bug being fixed was a tier-ladder bug, so the tier ladder became the whole frame. The
generalisable move is to treat any derived value as a fan-out and enumerate its consumers before
changing an input to it. That is exactly the discipline `.claude/rules/gamemodels.md` rule 9 already
demands for GameModel overrides, under "Cross-entity propagation"; it applies to data inputs too,
and nothing said so.

## Repeat offenders

**Finding 2 is the sixth instance of the float->int cast category.** `.claude/rules/csharp-architecture.md`
already names it, lists five prior instances, and instructs: *"gate the value itself, not arithmetic
derived from it"*, and *"If a 6th instance appears in a category this section doesn't name, widen the
scope again rather than patching the instance."* This one IS in a named category, so the scope does
not need widening. What failed is narrower and worth recording: every prior instance involved a
non-finite float arriving from config or the engine, so the rule reads as being about NaN. Here the
input is an `int` from XML and the overflow happens in the int addition BEFORE the float ever
exists. Same category, different entry point.

**Suggested rule amendment, not yet made:** the cast section should say that the hazard begins at
the first arithmetic on the input, not at the cast, and give the integer-overflow-feeding-a-float
shape as an example alongside the NaN ones.

## Why each review agent missed what it missed

| Agent | What it caught | What it missed, and why |
|---|---|---|
| Standards | Both em dashes; confirmed the service seam against ADR-002/007 | Nothing in its brief; the BOM is not a standards-rule concern and it never read bytes |
| API compatibility | Finding 1, the prisoner-recruitment gate, by enumerating every tier-keyed consumer | Nothing material. It also mislabelled three call sites as absent, having grepped the single-file shipping build rather than the categories tree |
| Efficiency | Finding 2, by actually working the arithmetic for `int.MaxValue` rather than declaring it unreachable | Rated it LOW on likelihood, which undersells that it negates the function's own invariant |
| Completeness | Findings 6 and the two doc rows | Did not question whether the tests it counted were vacuous, which is how finding 4 survived it |
| Data flow | Finding 4, plus the undocumented economic ripple | Did not reach finding 1: it traced `dg_uruk_foul` through TAOM's own consumers and TAOM has no tier gate, so the trace ended inside the repo |
| Tooling correctness | Findings 3 and 5, both by executing degraded scenarios rather than reading | Scoped to the XML file it was given, so it found one BOM of the five |

The structural point: findings 1 and 3 were each found by exactly one agent, and neither was the
agent a reader would predict. Finding 1 needed someone willing to decompile an unrelated engine
model; finding 3 needed someone willing to look at bytes. Both came from briefs that asked for
evidence rather than opinion.

## Preventive actions taken

1. `UPGRADE_TIER_COLLAPSE` validator check with a `_LATERAL_BY_DESIGN` allowlist, wired into the
   commit hook and documented in `.claude/rules/moduledata-validation.md`.
2. `UPGRADE_INDEX_EMPTY` so neither upgrade gate can silently check nothing.
3. `TaomPartyTroopUpgradeModel.GetXpCostForUpgrade` floors a zero cost, so a future data collapse is
   an oddity rather than a crash.
4. Level clamp at the cast site in `TroopCostService`, with degenerate-input tests.
5. A real cross-language pin on `MaxCharacterTier`.
6. Three entries appended to `docs/reviews/lessons/gamemodels-services.md`.

## Still owed

The in-game smoke. Hover a `dg_uruk_foul` stack and a `lindon_knight_golden_flower` stack on the
party screen: no CTD, a non-zero upgrade XP cost, and the lateral upgrades no longer available at
zero XP. Then confirm a Dol Guldur campaign still recruits Uruk Fouls from prisoners.
