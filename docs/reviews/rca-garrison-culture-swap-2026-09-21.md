# RCA: garrison + militia culture swap (issue #632), 2026-09-21

**Feature:** conversion re-mans a converted fief's garrison and militia in the new culture.
**Review:** `/deep-review`, 4 lenses (Standards, Engine compatibility, Data flow, XML), run on Opus
because the Fable usage limit was exhausted mid-review (user authorised the substitution).
**Outcome:** 2 HIGH, 3 MEDIUM, 9 LOW. All fixed in-session. Full suite green at 9,965 passing.

## Top line

The feature worked on every culture anyone would think to test, and was catastrophically wrong on
the four nobody would. The bug and the gate written to catch the bug shared one assumption, so the
gate could not fail. That is the lesson worth keeping; the rest is detail.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | Garrison candidates were grouped by `CharacterObject.Culture`. Four conversion targets field another culture's line, so their own tag carries almost nothing: Lothlorien's sole non-hero `Soldier` is `gear_practice_dummy_lothlorien`, and a conversion re-manned an entire captured city with Practice Dummies. Khand's six were all guards and arena dummies. Shaghana and Abanissa indexed zero. | Data flow | Two different notions of "this culture's troops" were used in one feature and never compared: the admission gate asked `HasCulturePool` (recruitment pools), the index asked `troop.Culture` (XML tag). Both are called "the culture's troops" in English. | Index derives from the same authority the gate uses. New test `NoConversionTargetIndexesAPracticeDummyOrAGuard`. Lesson below. |
| 2 | HIGH | `GarrisonCultureCoverageTests` could not fail for the cultures that broke. It seeded candidates from the cultures present in `troops_*.xml` (18) and then filtered by `HasCulturePool`, so the 4 targets that author no troops of their own never entered the loop at all. | Testing | The gate was built from the same wrong premise as the code: "a culture's troops live in `troops_<culture>.xml`". A gate that shares the code's assumption tests the assumption against itself. | Seeded from `GetPooledCultureIds()` with a `>= 20` floor so it cannot pass vacuously. |
| 3 | MED | `CharacterObject.All` also swept in 73 arena dummies, settlement guards, caravan guards and the spider creature, plus vanilla Calradian troops for the six ids TAOM retags. | Data flow | `Occupation == Soldier` reads like "is a line troop" and is not: TAOM authors dummies and guards as Soldiers. | The pool closure excludes all of them structurally, because none is recruitable. |
| 4 | MED | Singleton adapter held a strong reference to `Campaign`, rooting the finished campaign's whole object graph after `Campaign.OnDestroy` nulls `Campaign.Current`. | Lifecycle | The cache held only strings, so the "stale engine objects" half of the singleton rule was correctly reasoned about and the "retained graph" half was not. `MapReachAdapter` already carries a comment about exactly this. | `WeakReference`, with the reason in the comment. |
| 5 | MED | An exception inside `EnsureCaches` still stamped the campaign, latching a half-built index for the rest of the session behind a single warning. | Lifecycle | The catch was written for correctness ("no swap" is fail-safe) without asking how long the degraded state lasts. | Stamp only on success, so the next conversion retries. |
| 6 | MED | Nothing pinned the shipped `culture_conversion_config.json` against the compiled defaults, and two things looked like they did. `lint_docs.py`'s config-drift check reads only fenced ```json blocks; this doc uses a table. The provider tests write temp fixtures and never open the shipped file. | Gate gap | A clean `config_drift: 0` was read as coverage. | `CultureConversionShippedConfigTests`, values and key names both. |
| 7 | MED | All 14 tests in `GarrisonCultureSwapServiceTests` broke the mandatory `MethodName_State_Expected` convention, in a folder whose sibling file holds it rigorously. | Standards | Convention held in the file being extended, dropped in the file being created. | Renamed; nothing systemic. |
| 8 | LOW | Doc comment claimed a mutating `foreach` over `GetTroopRoster()` throws. On v1.5.3 it does not: no mutation path clears the cached list, so it silently enumerates pre-mutation copies. | Engine claim | Plausible-sounding engine behaviour asserted in a comment without reading the invalidation path. | Corrected, and re-justified on vanilla's own `RemoveMilitiasFromParty` idiom. |
| 9 | LOW | The `catch` claimed "head count is preserved per swap, so the roster is never left short" while the code removed before adding. | Engine claim | The comment described the intent; the ordering did not implement it. | Add-before-remove, so the claim is now true. Also `RemoveZeroCounts` moved to `finally`. |
| 10 | LOW | Comment said "all 16 TAOM cultures author the full militia set". 24 cultures exist; 16 author all four and 8 author none, and the 8 resolve non-null with empty slots, so the service's `toMilitia == null` guard could not fire as designed. | Data | Counted the cultures that had the data instead of the cultures that exist. | Corrected; added `CultureMilitiaTroops.IsEmpty` and the service now tests it. |
| 11 | LOW | The blank-`fromCultureId` branch, the one blank input that proceeds rather than returning early, had no test. | Testing | The other nine guards were covered, which made the gap invisible by contrast. | Test added. |
| 12 | LOW | `CultureTroopIndex.RolesAt` allocated an empty array per miss while its two siblings returned static empties. | Efficiency | Third method added after the pattern was established. | Static `NoRoles`. |
| 13 | LOW | Portuguese `substituidos` missing its accent. | Localization | Hand-written translation, no accent checker. | Fixed. The file already carried `substituível` accented, which is the evidence it was a typo. |
| 14 | LOW | The new XML comment was inserted between the `<!-- SpecialResources -->` header and the row it labels. | Data | Anchored the insert on the string rather than on its section header. | Moved above the header. |

## Root cause pattern: the gate inherited the code's premise

Findings 1 and 2 are one failure seen twice. The feature needed "the troops culture X fields". Two
expressions of that existed in the codebase and were treated as interchangeable:

- `VolunteerRecruitmentService.CultureMap[X]`: what X **recruits**. Already the conversion gate.
- `troop.Culture == X`: what is **tagged** X in troop XML.

They agree for 18 of 22 cultures. They disagree exactly where TAOM deliberately has one culture
field another's line, which is a design feature of this mod and is documented in the recruitment
pools. The code picked the wrong one, and then the coverage gate, written in the same sitting by the
same author, picked the same wrong one for its candidate list. So the gate ran 22 cultures' worth of
assertions, passed, and proved nothing about the four that were broken.

**The generalisable rule: a data gate must derive its candidate set from a different authority than
the code it guards, or it is a tautology.** Specifically, when a feature is gated on predicate P
("may this happen at all"), the test that proves the feature works must enumerate from P too, not
from whatever collection happens to be convenient. Here the gate enumerated troop files while the
feature was gated on recruitment pools, and the gap between those two sets was precisely the bug.

This is a sibling of the already-recorded "a gate that quietly checks nothing reads exactly like a
clean run" (finding 6 is a second instance of that in this same changeset: `config_drift: 0` was
vacuous). Both are cases where a green result carried no information, and in both the green result
was read as evidence.

## Why each lens missed what it missed

- **Standards** correctly scoped itself to ADR compliance and conventions, and found the real
  convention break (7). The index's data source is not a standards question. It did independently
  flag the cache-latching (5) as an observation.
- **Engine compatibility** verified 44 members with zero incompatibilities and caught the two false
  engine claims in comments (8, 9) plus the retained-`Campaign` leak (4). It explicitly routed the
  "TAOM authors 285 Soldier troops onto vanilla culture ids" observation onward as a data question
  rather than swallowing it, which is the correct handoff and is how finding 3 got a second look.
- **Data flow** found 1, 2 and 3. This is the fourth consecutive feature where the data-flow lens
  found the only HIGHs, which keeps matching the skill's own claim that it is the highest-value lens.
  It found them by tracing the *two ends* of a chain and comparing them, rather than reading either
  end on its own.
- **XML** found 6, 13 and 14, and correctly declined to "fix" the Slavic plural agreement, matching
  the neighbouring shipped row rather than inventing a house style mid-review.
- **Efficiency, Completeness and Design lenses did not run.** Wave 1 exhausted the review budget for
  this session after the Fable limit forced a restart on Opus. Their absence is recorded rather than
  papered over: the applied fixes have not had a design pass.

## Pre-existing, not fixed here

`CultureConversionService.RunDailyChecks` gates the loyalty floor as
`if (!loyalty.HasValue || loyalty.Value < MinLoyaltyToConvert) continue;`. `NaN < 50f` is `false`, so
a NaN loyalty **passes** the gate. This is the inverted-early-exit shape
`.claude/rules/csharp-architecture.md` bans, and it is not in this changeset's diff. It is recorded
here because this change is the provenance-change case the rule names from the other side: passing
that gate now tears down and rebuilds two rosters rather than only flipping a culture. Reachability
is UNVERIFIED (`RequireStableLoyalty` defaults false, and the only TAOM loyalty override feeds a
provider-validated `ExplainedNumber`). Filed against issue #632 rather than fixed, per the
changed-code-only boundary.

## Lessons to codify

One, appended to `docs/reviews/lessons/testing.md`: a data gate must enumerate from the same
authority the feature is gated on, and from a different one than the code under test derives its own
working set. See the root-cause section above.
