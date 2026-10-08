# RCA: vanilla Calradian wanderers in TAOM campaigns (#758, 2026-10-08)

**Summary.** 11 of the 50 wanderers a 2026-10-07 session created were Calradian templates from SandBox
`spspecialcharacters.xml` (four logged sessions: 49 of 191). `CompanionsCampaignBehavior.InitializeCompanionTemplateList`
(v1.5.4 `:344-353`) pools every `CharacterObject` with `IsTemplate` and `Occupation.Wanderer` from every
loaded module; nothing in TAOM removed SandBox's 67. The fix adds one rule to `lords.xslt` that retags
every wanderer template merged before TAOM's `lords` entry to `occupation="NotAssigned"`. A seven-lens
`/deep-review` then found the consequences below, all fixed or decided in the same change.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 0 | HIGH (player-facing) | Calradian wanderers spawned in Middle-earth | data reachability | `FactionRosterAdapter` documented culture `NotableTemplates` as the spawn pool, so the vanilla templates looked unreachable | `lords.xslt` retag; `VanillaWandererTemplateTransformTests`; lesson in `data-content-cultures.md` |
| 1 | HIGH | Retag left Dale (`sturgia`) and Khand (`battania`) with no wanderers, and Refuge warden promotion for their soldiers drew a random-race template | hidden dependency | vanilla templates silently filled the gap; no test counted wanderers per culture | 10 Dale and 10 Khand wanderers authored (Mike); stories, rosters, names, loc placeholders |
| 2 | MEDIUM | Existing saves keep already-spawned vanilla wanderers, never culled or counted | save lifecycle | the first fix note claimed the retag "reaches no hero already spawned" | documented as new-campaign-only (Mike); doc bullet, test docstring, #758 |
| 3 | MEDIUM | `tools/complete_lords_xslt.py --apply` would regenerate `lords.xslt` without the retag | generated file | the rule was hand-written into a generated file | generator emits it; `tools/tests/test_complete_lords_xslt.py` |
| 4 | MEDIUM | Dale and Khand wanderers missing from their culture's `notable_templates`, so the Faction screen tab stayed empty | wiring | the new-culture recipes never named that list | entries added; `RenamedCultureWandererListTests`; recipe line; #762 for 21 older gaps |
| 5 | MEDIUM | All 10 Khand wanderers would share one face (zero-width preset) | data | copied the face Khand notables use without checking its range | `fighter_rhun` |
| 6 | MEDIUM | Two Khand stories put the Siege of Minas Tirith in the past, though the war starts during play | content vs campaign timeline | writer brief named the Pelennor as Khand lore | stories re-anchored |
| 7 | LOW | Load order the fix depends on was untested; coverage test modelled merging by file name and cited the wrong engine method; Dale/Khand block sat in another tool's marker region; archers had no bows; first-person and surname slips | test and data hygiene | reviews of the first fix | load-order test; coverage test merges by XmlName id; block moved; bow sets; text fixes |

Removing vanilla content also removed every female wanderer (TAOM authored none): #761.

## Root-cause pattern

Findings 0, 1 and the female-wanderer gap share one cause: vanilla data was load-bearing in ways
nothing recorded. The pool was wider than the documented one, and the vanilla part of it filled gaps
TAOM had never filled. Before removing vanilla content from a pool, measure what each culture keeps.

## Why each agent missed these

The original bug predates any review. In this review every confirmed finding was caught by at least one
lens: Engine compatibility, Data flow and XML found 1 and 2; Design found 3 and the female gap;
Completeness and Design found 4; the second XML pass found 5 and 6.

## Feedback memories to codify

None; the lesson entry in `docs/reviews/lessons/data-content-cultures.md` carries the rule.
