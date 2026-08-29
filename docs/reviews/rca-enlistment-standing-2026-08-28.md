# RCA: enlisted standing had no reachable source (#520), and the first fix paid quitters

**Date:** 2026-08-28 · **Feature:** Enlistment trust economy · **Issue:** [#520](https://github.com/haterade22/TAOM/issues/520), supersedes the tuning half of #438
**Review:** `/deep-review`, 8 auditors and 3 adversarial skeptics per finding (99 agents). 30 raised, 11 refuted, 19 survived.
**Suite after fixes:** `./build.ps1 -RunTests` → 7732 passed, 0 failed, 2 skipped. `validate_moduledata.py` PASS. `lint_docs.py` 0 long dashes in new prose.

## Top line

The reported bug was that standing never improved and rank stopped at Soldier on day 73 with 2903
service XP. The cause was a closed loop: standing had exactly two sources, duty successes and merit
bands, and both were shut. Eight of thirteen duty rows were mathematically unpassable at the skill a
warrior hero actually has, and the merit band an ordinary battle reaches paid nothing because
cohesion scored a flat zero for an enlisted player.

**The review's most useful finding was not about the bug. It was about the fix.** Paying trust from
the `solid` band, which is what "give the merit path an earner" naturally reduces to, put two shapes
that never fought over the trust line at once. Both were reachable in ordinary play. Neither was
visible in the diff, because the diff changed one integer in a config file and the defect lived in
the arithmetic of a scorer two files away.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| H1 | HIGH | Paying trust from `solid` (minScore 40) rewarded a maximum-kill walkout (ceiling 45) and a soldier who merely stood in his own line (survival 25 + cohesion 15 = 40) | Balance / cross-file arithmetic | The change was one integer in JSON. Nothing in the diff showed what scores are attainable, and the attainability lived in `BattleMeritScorer` plus `MeritConfig`'s comment, neither of which the change touched | `MeritTrustFloorTests` pins the invariant from both sides against the shipped config; `EnlistmentBattlePayoutService` withholds band trust on `LeftTheField` independent of boundaries |
| H2 | HIGH | The new shipped-vs-compiled band parity test compares defaults to defaults, and passes, on all four provider fallback paths (one of which logs nothing) | Test vacuity | The test was written by reading the provider's *interface* (`GetConfig()` returns the config) rather than its *fallback behaviour*. Its own sibling 20 lines above already had the correct shape | Read the FILE for file-vs-default parity, match by key not index, and keep the `_logger.DidNotReceive()` round-trip guard |
| M4 | MED | `bandit_hunt` (50) and `deserter_sweep` (54) landed at exactly `needed == 50`, i.e. 1 roll in 51 | Floor gap | Both new floors passed them. Passable-at-all admits a 2% row; trust-positive-in-expectation is satisfied by any row charging nothing for failure, which is now all thirteen | Fourth assertion `NoFieldDuty_IsPassableOnlyOnANearMaximumRoll` (≤2 rolls of headroom); both rows lowered to 48 and 52 |
| M5 | MED | New prose claimed incidents "stated the stakes" in their popup. They do not: the presenter passes a title, a body and two `Humanize(key)` button labels, and nothing else | False justification written into docs | The sentence was written to justify a scope boundary, from a mental model of the popup rather than from `InteractiveDutyPresenter` | Prose corrected to the real reason (opt-in choice). `press_claim`'s domination recorded and deferred to #521 |
| M6 | MED | No test loaded the shipped `enlistment_duties.json` through the provider's own validation; `LoadDuties` skips a bad row with only a log warning | Coverage gap on the edited artifact | `FieldDutyReachabilityTests` parses the file directly and every other duty test stubs the provider, so "the file is tested" felt true | `ShippedDuties_LoadThroughTheProviderWithoutBeingSkipped` asserts 13/11/3 rows and no warnings |
| M7 | MED | `MeritGeometryScanner` has zero test reach; `nearestAllySq` and `nearestHeroSq` are same-typed locals feeding different gates, and swapping them compiles silently | Untestable seam widened | Accepted seam (the file's own doc comment declares it, ADR-008 exempts entry points) that this change gave a second same-typed local | Named arguments at the `AddSample` call site so a swap is visible in a diff. Full coverage needs `InternalsVisibleTo`, out of scope; recorded, not silently dropped |
| L8 | LOW | `supportSkills` was unbounded; only `[0]` and `[1]` are read, so a third entry is dead data with no feedback to its author | Validation gap | The change added a second entry to 11 rows without asking what the third would do | Provider skips a row with >2 entries and says why; two tests |
| L10 | LOW | The ordering test's worked example inverted its own numbers ("58 above 64") and described a pair-wise inversion the assertion cannot see | Doc-comment drift | Pre-existing, surfaced by reading the file closely for the new floors | Example replaced with one the assertion can actually detect, and the band-ceiling scope stated |
| L9 / L11 / L12 / L13 | LOW | Undocumented consequences: trust is now a one-way ratchet on the field-duty track; the merit score also feeds XP/gold/renown/rep and `distinguished` becomes Infantry-reachable for the first time; duty offer chance moves 0.06 → 0.26 as trust saturates; `HighScrutiny` and all four reputation domains have no reader | Documentation | Each is a second-order effect of a first-order change; none is wrong, all were unwritten | One section each in `enlistment.md` |
| M3 | MED | Sergeant's `minLeadershipSkill: 50` needs 34,575 XP against a 10/day grant: 300-460 days (90-140 even under `GameAccelerationMode.Fast`) versus its own `minDaysServed: 60` | Pre-existing, adjacent | Not reachable from this changeset; found by the ripple auditor computing the engine's own XP curve | Deferred to #521. **Not** folded in |
| L14 | LOW | Five unrelated Rohan spear-id swaps sit uncommitted in the same tree | Another session's work | n/a | Left untouched and unstaged. `/commit-split` before either lands |

## Root-cause pattern: the diff was not where the defect was

H1, M4 and L8 are one shape. Each was a small edit to a **data** file whose consequence lived in
**code** the edit did not touch:

- `"trust": 0` → `1` decided who gets paid, but *who can reach 40* is decided by `BattleMeritScorer`
  and `meritScoring`'s weights.
- `"difficulty": 58` → `50` looked like a tuning step, but *whether 50 is meaningfully passable* is
  decided by `SkillCheckService.RollRange`.
- Adding a second `supportSkills` entry looked additive, but *how many are read* is decided by
  `FieldDutyRuntime`.

In every case the reviewable artifact showed an integer moving and hid the function that gives the
integer meaning. This is the same class as the `charge_type` XML gap and the BannerBearers dead
dictionary key: **a config edit is a call into code, and it must be reviewed as one.**

The second pattern, narrower but sharper: **H1's whole defect is that a guard was emergent rather
than stated.** Nothing paid quitters before, not because anything forbade it, but because the band a
walkout could reach happened to pay zero. The moment one integer moved, the protection vanished with
no test failing, because the protection was never written down. `MeritConfig`'s comment had even
recorded the reasoning ("sized to sink the best possible walkout into the bottom band") and that
sentence was itself wrong, arriving at 45 by omitting kills from a sum the scorer includes. An
invariant that lives only in a comment is not enforced, and a comment nobody re-derives rots into a
false one.

## Why each agent missed these before the review

This section is about the *authoring* pass, not the review agents, since the review caught them.

- **The author (me).** I verified the bug exhaustively and then verified the *mechanism* of the fix
  (does trust now flow?) without asking the adversarial question (who else does it now flow to?).
  The rule I skipped is the one I invoked in the plan: `simplicity-criterion.md` asks what a change
  costs, and I costed it as "one integer, one band". The real cost was a new payout population.
- **TDD gave false comfort.** RED was genuine and observed for every floor I wrote, and both HIGH
  findings sit in space no floor covered. A watched-fail test proves the test works; it says nothing
  about the invariants nobody wrote.
- **The green suite was the H2 defect.** 7723 tests passed with a parity test that could not fail.

## Lessons to codify

Appended to the category files (`docs/reviews/lessons/`), not just recorded here:

1. **testing-qa.md**: a parity test between a shipped data file and its compiled defaults must read
   the FILE; going through the provider tests the fallback against itself.
2. **data-content-cultures.md**: before changing a reward threshold, enumerate the scores attainable
   *without doing the thing the reward is for*, and pin the answer as a test.
3. **campaign-mechanics.md**: an invariant that holds only because two independently-tuned numbers
   happen not to overlap is not enforced; state it in code.

## In-game run, 2026-08-28 21:05 to 21:08: the merit half is proven and H1 reproduced

`taom_debug_2026-08-28_20-58-46.log`, diagnostics on (119 `[EnlistDiag]` lines), deployed
`TAOM.dll` stamped 20:57:39 so the run carries this changeset. One fought-through battle, Infantry,
won:

```
[Enlistment] battle merit 46 -> band 'solid' (kills=0, won=True)
[Enlistment] reward 'merit-solid': xp=12 gold=5 skill=/0 trust=0 renown=0
[Enlistment] reward 'battle-won':  xp=0  gold=0 skill=/0 trust=0 renown=3
```

**The merit path is observed working for the first time.** That line had never appeared in any log
on this machine, which is what made half the trust economy unproven end to end. Renown 3 is the flat
win base of 2 plus the `solid` band's 1, so the band-renown chain resolves too.

**The cohesion fallback is proven live by the score itself.** The player was placed in the Infantry
formation, and infantry role fit requires `CohesionRatio >= 0.5`, so before this change cohesion
contributed 0 AND role fit was structurally unreachable. The ceiling for a zero-kill battle was
therefore survival 25 + commander 10 + engagement 10 = **45**. The observed 46 is one point above a
ceiling the old code could not exceed.

**H1 reproduced on the first live battle.** Decompose 46 with no kills: role fit must be off, because
switching it on requires cohesion and engagement both at 0.5 or better, which is 12.5 points against
the 11 available. So cohesion is saturated near 1.0 while engagement sits near 0.2. That is exactly
the stander shape, in the line all battle, rarely near an enemy, nothing killed, landing at 46 and
inside `solid`. Had the `solid` band shipped paying 1 trust, this battle would have paid standing for
hanging back. The review found it by arithmetic; play reproduced it within three campaign hours.

It also settles the premise the completeness critic flagged as unmeasured: an ally really is inside
25 m on essentially every sample, so cohesion does saturate for anyone standing in a formation.

Standing correctly did not move. The run was clean otherwise: three warnings, all expected, including
the `PlayerEncounter` self-heal firing as designed.

## Still owed: the duty half, which is the larger half

Zero `[Enlistment.Duties]` lines. The oath was sworn at 21:05 and the battle fought at 21:05, about
three campaign hours, against `minDaysBeforeFirstOffer: 3`. No duty was owed, so nothing in this run
touches the eight rows that were impossible or the retune that fixed them. **Nothing here validates
the change that motivated the issue.**

The next run needs 3+ campaign days of service and "Ask your sergeant for work". The line that
settles it is `duty '<id>' completed`, an outcome an untrained hero could effectively not reach
before.

One calibration note from the same run: 46 sat 14 short of `strong`, where standing is actually
earned. Three kills would have made 61, and holding engagement above 0.5 unlocks the 10-point role
fit alone. That is the intended shape, participation pays and presence does not, but it does mean the
duty loop carries most of the standing economy, which is a second reason the duty smoke matters more
than this one did.
