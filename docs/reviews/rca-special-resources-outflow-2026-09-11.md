# RCA: Special Resources outflow visibility (#558), deep review of 2026-09-11

**Scope.** The `/deep-review` pass (five agents: standards, engine compatibility, efficiency,
completeness, cross-system data flow) over the #558 changeset: `GetDailyBreakdown` as the single daily
calculation, the tooltip rewrite, the four outflow messages, the zero-upkeep desertion fix, the
console dump, and the Black Numenorean cost rescale.

**Top line.** Standards and compatibility passed clean (20 of 20 engine members verified against the
installed v1.4.8 DLLs). Six findings survived verification: two code defects (both fixed, both
pre-existing lines that the change had left in place), one efficiency note declined with reasoning,
one documentation count, one test gap, and one process failure of the author's own. No HIGH.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | The one-day-ahead deficit warning fired for any positive balance with a negative net, upkeep troops or not, while the map-bar flag and desertion both require upkeep troops. Reachable only through a negative career gain, but the three gates disagreed (`SpecialResourcesBehavior.OnDailyTickHero`). | Gate consistency | The branch predates the change. The rewrite swapped its arithmetic onto `breakdown.Net` and kept its condition; the three gates were written from three specs, never from each other (lesson "Two gates on one condition must be written from each other", `lessons/gamemodels-services.md`). | Fixed: the branch now requires `hasUpkeepTroops`, the same term the mixin and the desertion branch use. Not unit-testable (behavior); the shared term is the guard. |
| 2 | MED | `SpecialResourceStorageService.Set` floored with `Math.Max(0f, amount)`, and `Math.Max(0f, NaN)` is NaN, so a non-finite write was stored, survived every later `Add` and the save round trip (`RestoreData` validated nothing; `ClampAll` is no longer called on load), and the map bar's `(int)amount` would render it as int.MinValue. | NaN gate, seventh sighting: a persistence floor is a decision gate too | Pre-existing. The author's own prompt to the data-flow agent asserted "SyncData clamps" from a stale reading of the `SyncData` comment; the agent tested the premise instead of accepting it. | Fixed: `Set` refuses a non-finite value (the previous balance stands) and `RestoreData` repairs a poisoned entry to 0 without dropping the key. Four storage tests. |
| 3 | MED, declined | `OnRefreshCore` computes the breakdown on every map-bar refresh (5 to 10 Hz per the agent's citation of `MapInfoVM`), allocating two small lists. | Efficiency | Known at design time. | Declined under `.claude/rules/simplicity-criterion.md`: the agent itself rated the cost class equal to vanilla's per-refresh `CalculateClanGoldChange`, and a roster-version cache adds a field plus a stale-icon edge (a town or passive change with no balance change). Recorded here and in the commit `Rejected:` trailer; revisit if profiling shows it. |
| 4 | LOW | The feature doc listed `SpecialResourceServiceTests` at 60 tests; the file holds 81. | Documentation | A stale count carried over from the existing bullet. | Fixed after counting the attributes. |
| 5 | LOW | No test exercised an upkeep modifier below minus 100 percent, where the per-line clamp must hold. | Test coverage | One modifier value exercised. | Added `GetDailyBreakdown_UpkeepModifierBelowMinusOneHundredPercent_ClampsLinesAndTotalToZero`; it passed before any change, so it pins rather than fixes. |
| 6 | Process | The issue body, the memory file and the plan stated that prisoner recruitment never reaches `OnUnitRecruitedEvent`. The installed decompile shows `RecruitPrisonersCampaignBehavior.OnMainPartyPrisonerRecruited` dispatching `OnUnitRecruited(troop, 1)` per unit, and the mercenary path dispatching it too. | Evidence | An Explore agent's "not found" over the dump was relayed unverified, against `.claude/rules/evidence-over-claims.md` A.4. The data-flow agent read the behavior from the installed DLL and cited the line. | Correction posted on #558; memory corrected; no code change (the recruit charge was already reaching prisoners). |

## Root-cause pattern

Findings 1 and 2 share the shape of the lesson appended this session ("A cost row is not an upkeep
row"): a line that was correct for the inputs it was written against, left untouched while the inputs
around it changed, and therefore absent from every diff anyone reviewed. Finding 1 surfaced only when
the three thresholds were laid side by side in one prompt; finding 2 only when the write path was asked
what it does with garbage rather than what it does with a number.

Finding 6 is the inverse: a claim of absence produced by a search, accepted because it agreed with the
author's reading of a code comment. An agent that cannot find something has proved nothing about the
installed engine, and the cheap way to know was the one used on the second pass: open the behavior
in the installed decompile.

## Why the author missed these and the agents did not

- **Data flow (agent 5)** was told to compare the three gates and to state what each formula returns
  for NaN. Both defects fell out of the comparison. The author had reviewed each branch where it lived.
- **Data flow (agent 5)** treated the prompt's "SyncData clamps" as a claim and read the code.
- **Completeness (agent 4)** counted the test attributes instead of reading the number in the doc.
- **Efficiency (agent 3)** cited the refresh cadence before rating; that is what kept the finding at
  MEDIUM and made the decline defensible.

## Feedback memories to codify

None new. The lesson appended to `docs/reviews/lessons/gamemodels-services.md` this session covers the
shape of findings 1 and 2; `evidence-over-claims.md` A.4 already covers finding 6, and the
special-resources feature memory now carries the corrected fact.

## Codex pass (GPT-6-Astra at ultra, review 95)

Dispatched after the five agents and the fix-loop re-check, on the committed 9e78afd2 / e80949b7, the
first TAOM review on that model. Verdict ISSUES FOUND: 0 CRITICAL, 0 HIGH, 3 MEDIUM, 0 LOW, and no
false positive. Each was verified against the source before it was implemented; F2's arithmetic was
reproduced independently in float32 before the fix.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| F1 | MED | `ChargeRecruitCost` returned the nominal cost and `CommitSession` the pending amount, after a storage write that floors at zero. The volunteer screen is gated (Patch51); the party screen's prisoner recruit is not, so a spider recruited at a balance of 2 would have toasted a debit of 5. The same interleaving reaches the upgrade toast when a prisoner recruit inside the same screen lowers the balance before Done commits the queue. | Return value as receipt | The value was correct as a request and never followed into the store that clamps it. The two return-value tests used a substitute storage whose `Get` returns 0, so they proved a requested `Add`, never a debit. | Fixed: both paths measure before and after and return the difference; both tests moved to real storage and two new ones pin the floored cases. Follow-up #563 for the ungated prisoner path. |
| F2 | MED | `DaysUntilDepleted` cast `Math.Ceiling(balance / -Net)` to int unchecked. A net below the balance's float resolution (a Dale player with one town, +0.7, against 0.2 + 0.2 + 0.3 of upkeep nets minus 5.96e-8) gives 1.68e10 days, and the cast is int.MinValue: "Depleted in -2147483648 days" for a balance that never moves. Codex compiled the domain type and ran the shipped data through the CLR. | Float-to-int cast, finite overflow | The cast rule in `csharp-architecture.md` names NaN and infinity; every input here was finite. The test covered two ordinary countdowns. | Fixed: no countdown when `balance + Net` does not lower the stored float, the division in double, a range check before the cast; regression test with the reproduced fixture. Rule widened to finite overflow. |
| F3 | MED | The zero-balance notice ("troops are deserting") showed whenever the balance was zero and upkeep troops were present, but the tick applies the net before testing the balance, so with income covering upkeep nothing deserts. | Gate written from a different predicate than the event | The notice was written from the balance alone; the flag next to it already carried the net. Third gate on the same event, and the one the fix-loop re-check did not lay beside the others. | Fixed: the notice requires `Net <= 0` (the flag's own term at zero) and states the rule ("desert each day while you have no X") instead of a loss in progress. |

Observations Codex recorded that are not findings: the extended tooltip rows are reachable (it traced
the renderer that honours `OnlyShowWhenExtended`); `TroopRoster.AddToCounts` handles a wounded majority
safely; the storage repair has no enumeration hazard; all 77 cost rows resolve at runtime (the spider
lives in `characters/spider_creature.xml`); the declined refresh cache is defensible; string variables
are not universally verbatim in Bannerlord's substitution engine, so "cannot be double-processed" is
not a general API guarantee, only true for the numeric values and rendered names used here; the
Mumakil's 500 a day is authored creature pricing and a balance question, not a defect of these commits.
Its test-sensitivity table classed 21 of the new tests as unable to compile against the pre-change API,
7 as behavioural regressions, 1 as passing either way, and flagged `GetDailyBreakdown_Net_MatchesGetProjectedDailyNet`
as comparing a helper with its own wrapper; it now asserts the independent 0.9.

Root cause shared by F1 and F3: a value or a message written from the point where it is decided, not
from the point where its truth is settled (the store's floor, the tick's order). F2 is the float-cast
class again, one category wider than the rule described it.
