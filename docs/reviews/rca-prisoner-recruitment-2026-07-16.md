# RCA — PrisonerRecruitment (alignment morale waiver), 2026-07-16

**Feature:** no morale lost recruiting a prisoner of your own faction or alignment side.
**Review:** 5-agent `/deep-review`. Standards PASS, Compatibility PASS, Efficiency 1 finding (REJECTED,
see below), Completeness 1 finding, Data Flow 0 gaps / 2 inconsistencies.
**Verdict:** no live bug found. Two documentation-accuracy findings, both confirmed against the data
and fixed in-session; one efficiency finding rejected with evidence.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | Feature doc presented the three "bandits never waive" barriers as if structural. Barriers 1 and 2 are *facts about the shipped data*, not enforced invariants. Only barrier 2 was pinned by a test. A troop authored with `occupation="Bandit"` + a mainline culture would be recruitable (barrier 3 checks culture) **and** waivable — silently zeroing a −2. | Data invariant | The author (me) verified the barriers *hold today* and stopped there, never asking "what enforces them tomorrow?" I wrote a test for barrier 2 precisely because it was an editable JSON file, and didn't extend the same reasoning to barrier 1, which is editable troop XML. | **Fixed:** added `ShippedTroops_EveryBanditOccupationTroop_CarriesABanditCulture`, deriving the bandit-culture set from `taom_spcultures.xml`. Doc rewritten to state which barriers are data-facts and which test pins each. |
| 2 | LOW | Doc section "Bandits never waive" read as a stronger guarantee than shipped. Bandit-hideout rosters draw *mainline* troops (`dunland_peasant`/`dunland_raider`/`dunland_clan_warrior` → `culture="Culture.empire"`, `occupation="Soldier"`), which an Evil recruiter **does** waive (on the ordinary −1). Correct per design; unstated in the doc. | Doc accuracy | I framed the section around the word "bandit" as players use it ("troops from a hideout") while the code means it two different ways — per-troop `Occupation` for the −2, per-culture `IsBandit` for recruitability. The doc inherited my conflation. | **Fixed:** doc now separates "the −2 is never waived" (the real guarantee) from "what DOES waive: mainline troops in bandit parties" (intended behavior, now explicit). |
| 3 | — | **REJECTED.** Efficiency agent flagged 3 `TaomSettings.Instance` reads per call and proposed caching them in the settings provider's constructor. | — | — | Rejected with evidence: the provider is `Reuse.Singleton` (process lifetime), so constructor-caching would make all three MCM toggles **dead after startup** — the exact HIGH violation `/deep-review` Agent 5 rule 2b exists to catch ("exactly one read site at startup + hint text promising runtime behavior"). Agent 5 independently *verified the live per-call read as required*. The sibling `RecruitmentAlignmentSettingsProvider` reads live for the same reason. The proposed alternative (batch the 3 reads) also loses the short-circuit. No change. |
| 4 | — | **Process (self-reported).** Tests were written before the implementation but never observed RED — both were written before anything ran, so "48 passed" proved nothing about whether the tests *could* fail. | TDD discipline | Batching the write of tests + implementation before the first test run. The TDD mandate's RED step isn't ceremony; it's the only thing that distinguishes a real test from a vacuous one. | **Mitigated in-session** by mutation testing: disabling rule 1 failed exactly 3 tests; removing the Neutral guard failed exactly 2. Both rules are genuinely pinned. See the lesson below. |
| 5 | — | No GitHub issue exists for the feature. | Completeness | TAOM mandates the issue before the closing commit. | **Owed** — deliberately not auto-created; CLAUDE.md gates `/issue` on explicit user intent (public artifact). Not yet a breach: nothing has been committed. |

## Root-cause pattern: "verified true today" ≠ "enforced tomorrow"

Findings 1 and 2 share one root. When I established the bandit safety story, I checked each barrier
against the shipped data, confirmed all three held, and wrote them up as a guarantee. That verification
was correct and is still correct — but a barrier that is true only because nobody has yet authored the
contradicting data is a **coincidence with good hygiene**, not an invariant. The tell was already in my
own reasoning: I pinned barrier 2 with a test *because* it was an editable config file, then failed to
apply that same test to barrier 1, which is equally editable troop XML. The rule was in my head for one
file type and not the other.

This is the same shape as the BannerBearers dead-key CRITICAL two days earlier (2026-07-16): a config
key that resolves to nothing is silent at every layer, so the only defense is a test asserting the
resolution. Here the "key" is an implicit data-authoring rule (never pair `occupation="Bandit"` with a
mainline culture) that no schema, validator, or type expresses.

## Why each agent missed / caught these

| Agent | Outcome |
|---|---|
| 1 Standards | Correctly PASS — this is a data-invariant question, entirely outside its ADR/structure rule set. |
| 2 Compatibility | Correctly PASS. Notably it did the highest-value work of the review: verifying end-to-end that the model actually *resolves* (`SandBoxManager.cs:310` registers the default at `Campaign.cs:1384`; TAOM's `OnGameStart` fires at `:1391`; `GameModels` freezes and backward-scans at `:1392`). That was the one real unknown in the plan and it is now settled at the byte level. |
| 3 Efficiency | Produced the review's only wrong finding, and it was wrong in the dangerous direction — the proposed "fix" would have introduced a HIGH bug (dead MCM toggles). A cheap-model agent optimizing a property read without modeling the settings lifecycle. **The disagreement with Agent 5 was the signal**; per `evidence-over-claims.md`, findings get verified, not implemented on confidence. |
| 4 Completeness | Caught the missing GitHub issue. Also miscounted tests (18 vs the real 32 — it counted `[TestMethod]` declarations, not `[DataRow]` expansions); harmless here, but a reminder that agent-reported counts are claims. |
| 5 Data Flow | **Caught both real findings**, and was the only agent to grep `occupation="Bandit"` across the whole ModuleData tree and read the bandit cultures' troop pools. Consistent with the skill's own claim that Agent 5 is the highest-value agent. Its "these are data facts, not structural guarantees" framing is exactly the distinction I'd missed. |

## Lesson codified

Appended to `docs/reviews/lessons/data-content-cultures.md`: *"A safety barrier that rests on shipped
data needs a test, not a doc paragraph."*

The TDD/mutation point (finding 4) is appended to `docs/reviews/lessons/testing-qa.md`: *"If you didn't
watch the test fail, mutate the code until it does."*
