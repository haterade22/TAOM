# RCA: orcs and goblins overbreed (#628, 2026-09-19)

**Top line.** Mike reported orc and goblin lords having too many children. Nothing had changed recently:
the fertility values dated from 2026-03-25. The bug was structural. `TaomPregnancyModel.ComputeBaseChance`
multiplied the race modifier (2.0 to 3.0 for orc-kin) after vanilla's clan population brake, so the brake
lost to the bonus, and the start-of-campaign child generator's `excluded_cultures` list had never been
extended to the three orc cultures added after it was written (`goblin`, `mistymountainorcs`,
`bluecraig`). Seven causes were fixed together (see the CHANGELOG entry). A six-lens `/deep-review` ran in
two waves (Standards, Engine compatibility, Data flow, Efficiency; then Completeness, Design) as
`deep-reviewer`. No CRITICAL, no HIGH. Every confirmed finding was LOW: stale or overstated docs, a
miscounted CHANGELOG, test robustness, and one dead tracking pointer. All were fixed in the same session
except the ADR-007 extraction, which stays FOLLOW-UP.

## The bug itself

| # | Cause | Why it shipped | Prevention now in place |
|---|---|---|---|
| A | The race bonus multiplied after the clan brake, so an orc clan at 1.5x its cap bred at vanilla's unbraked rate and clans sat at the `2*cap` ceiling | The model was written as "vanilla formula, then times the race modifier", which reads as harmless. Nobody asked what a multiplier does to a brake that works by scaling toward zero: at x2 the brake's first half is cancelled | A bonus above 1.0 applies only when an NPC clan is at or under its cap; tests pin the edge (`..._NpcClanAtCap_...`, `..._NpcClanOneLordPastCap_...`) and the property (`..._BonusRaceNeverBeatsHumanRate`). Lesson: campaign-mechanics |
| B | `excluded_cultures` listed four orc cultures; three added later were missing, so every goblin, Misty Mountain and Blue Craig clan started topped up with children | A hand list with no gate against the definitive set. Repeat of the pool-composition lesson in `lessons/data-content-cultures.md` (an exclusion list checked against its own output, not the definitive list) | `ShippedFertilityConfigTests` derives the orc cultures from `cultures.json` and fails on any not excluded, and fails on an excluded id that names no culture. Lesson extended: data-content-cultures |
| C | Humans fertile 18 to 195, orc and berserker to 50 | The window decline is spread over the whole window, so a long window is also a slow decline; the 195 was set alongside the 200-year lifespan (2026-03-26) without that effect in view | Ceilings pinned by `ShippedFertilityConfigTests` (no race above 1.5x, orc-kin to 45, humans to 60) |

## Review findings

| # | Lens | Sev | Finding | Category | Why missed | Fix and prevention |
|---|---|---|---|---|---|---|
| 1 | Standards | LOW | `TaomPregnancyModel.cs` and the test header said the ADR-007 extraction "is tracked separately as #131"; #131 closed 2026-05-14 deferring it, and no open issue tracks it | Dead tracking pointer | The comment was true when written; closing an issue does not revisit the comments that cite it | Both comments now say #131 closed without the extraction and nothing tracks it. FOLLOW-UP: a successor issue, recommended to Mike (not filed here: `/issue` is user-invoked) |
| 2 | Standards, Efficiency, Design | LOW | The new test carried its own repo-root walker and a third regex parser of `cultures.json`, which silently drops an entry whose keys are reordered | Duplicated helper, reuse-before-write | Copied from `CultureRaceConsistencyTests`, whose "no JSON library" justification is stale | Test now uses `RepoPaths.RepoPath` and the production `CultureCreationDataProvider.LoadCultures()`. The same swap in `CultureRaceConsistencyTests` is FOLLOW-UP (pre-existing code) |
| 3 | Standards, Engine, Completeness | LOW | CHANGELOG said "Seven causes" over five bullets and "All seven failed before the fix" over eleven tests | Unverified count in an artifact | The entry was written from the RED run's failure count, not by recounting the list it summarised | Rewritten: seven numbered causes; "7 of the first 11 failed ... 4 boundary pins"; the two review-added tests named as post-fix |
| 4 | Standards, Completeness | LOW | "under its cap" in the comment, doc and CHANGELOG where the gate is at-or-under | Prose looser than the code | The at-cap boundary test existed; the prose was not checked against it | "at or under" everywhere; the gate itself now reads `aliveLords <= clanCap` (Design proposal A) |
| 5 | Data flow | LOW | Docs promised "a new orc kingdom fails the test until it is excluded"; that holds only if the culture is in `cultures.json` | Overstated guarantee | The derivation's input was not questioned against the runtime key (the clan's `Culture.StringId`) | Docs state the condition. A derivation from `lords.xml` was considered and rejected as more parsing than the risk warrants today (all 22 cultures agree, per the Data flow trace) |
| 6 | Data flow, Completeness | LOW | `initial-child-generation.md` said edits take effect "on the next new game"; `race-age-system.md` said nothing of reload | Reload scope (csharp-architecture "Doc requirement") | Pre-existing sentence; the #628 paragraph was appended under it without re-reading it | Both docs state the full-restart requirement (`Reuse.Singleton`) |
| 7 | Data flow | LOW | Nothing asserted that each `excluded_cultures` id resolves; a typo would exclude nothing, silently | Dead config key (xml-data "ship a test asserting every KEY resolves") | The new test checked one direction only (orc cultures within the list) | `ShippedInitialChildGeneration_EveryExcludedCulture_IsARealCulture` |
| 8 | Engine | LOW | `race-age-system.md` cited a dwarf window of 30-120 (`fertilityEnd: 120`) and a human 18-45 window in the formula section | Doc drift | The values table was refreshed; the prose below it was not re-read | Corrected to 18-220 and 18-60 |
| 9 | Engine | LOW | The doc said `comesOfAge` is the "minimum age to be considered an adult"; the engine gate is `AgeModel.HeroComesOfAge` (18), which `TaomAgeModel` does not override | Overstated field semantics | Pre-existing row, in the section the change touched | Row states what the field controls |
| 10 | Completeness | LOW | Overview said "Men live 60-85 years" against a 200-year `maxAge` | Doc drift | Same as 8 | Corrected |

Design proposals applied (behaviour-preserving, pinned by existing and new tests): A, the cap condition
stated directly as `aliveLords <= clanCap` instead of through the derived float; B, the production loader in
the shipped-config test. Efficiency found nothing in production code (one per-day call per eligible hero).
Engine compatibility verified 22 API usages against the installed 1.5.3 DLLs, none incompatible.

## Convergence pass

One `deep-reviewer` checked the applied improvements for standards and parity (the first attempt hit the
session rate limit and returned nothing; the retry completed). No CRITICAL, HIGH or MEDIUM. It confirmed
both improvements preserve behaviour (`aliveLords <= clanCap` equals `populationFactor >= 1f` on every
reachable input: cap is 4 to 28 and the quotient is exactly 1.0 at `alive == cap`; the provider-based
parser yields the same seven orc cultures, and the shipped `cultures.json` has no empty `races` and no
duplicate id). Its LOW findings, all fixed:

| # | Finding | Fix |
|---|---|---|
| 11 | Two more "under its population cap" phrasings (the `race-age-system.md` changelog line, `configs-balance.md`), and `configs-balance.md` credited the brake rule to `ShippedFertilityConfigTests` | "at or under"; the brake is credited to `TaomPregnancyModelTests`, the 1.5 ceiling to `ShippedFertilityConfigTests` |
| 12 | An em dash left on a `race-age-system.md` line this change edited | Colon |
| 13 | The `comesOfAge` row left out its second consumer: `TaomAgeModel.GetAgeLimitForLocation` raises the minimum age of location-spawned NPCs of the race | Row names both consumers; a value above 18 changes both |
| 14 | The "is a real culture" test used the orc-derivation dictionary, which skips cultures with no races, so such a culture would read as unknown (latent: none ships) | Existence set now built from every culture id |

It also reported that the #628 work had already landed: Mike's joint commit `83bdad85` (2026-09-19 10:46)
holds the code, tests, data, docs and CHANGELOG entry, pushed. This RCA, the two lesson entries and the
convergence fixes above follow in the next joint commit. The ADR-007 extraction stays FOLLOW-UP (item 1).

## Root-cause pattern

Items B, 1, 5 and 7 share one shape: **a list or pointer maintained by hand, checked against nothing.**
The exclusion list, the `#131` pointer, and the test's own derivation each were correct when written and
went wrong when the world around them changed (new cultures, a closed issue, a culture absent from one
file). The fix in every case is a check in the direction the drift happens: derive the set from its
source, assert every key resolves, and name the condition under which the derivation is blind.

Items 3, 4, 8, 9 and 10 are the artifact-accuracy class: prose written from memory of the change instead
of re-read against the code and data, which `evidence-over-claims.md` section C already forbids. The
reviewers caught all five; no new rule is needed.

## Why each lens missed nothing HIGH, and what each caught

- **Standards** caught 1, 2, 3, 4. It could not see 5 or 7: they need the data traced to runtime.
- **Engine compatibility** caught 3, 8, 9 and verified every engine claim in the docs, including that a
  negative chance never conceives and that `Clan.AliveLords` counts newborns.
- **Data flow** caught 5, 6, 7 and traced seven flows with no gap: the fix reaches every path a child is
  created or a chance is computed on.
- **Efficiency** found no production issue; its test-file items overlapped 2.
- **Completeness** caught 10 and independently confirmed 3's 7/4 split.
- **Design** proposed A and B and examined the step at the cap (1.3x to about 0.9x in one lord) as
  inherent to the two approved rules.

## Feedback memories to codify

None new. The durable lessons go to `lessons/campaign-mechanics.md` (bonus after brake) and extend the
existing exclusion-list lesson in `lessons/data-content-cultures.md`.
