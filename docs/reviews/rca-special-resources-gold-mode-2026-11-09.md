# RCA: Special Resources gold mode (#1109), deep review of 2026-11-09

**Scope.** `/deep-review` at risk tier 1 (two GameModels change) over the uncommitted #1109 changeset on
`feat/special-resources-gold-mode-148`: the MCM toggle and rate, the gold surcharges in
`TaomPartyTroopUpgradeModel` and `TaomPartyWageModel`, the stand-down of earning, upkeep, desertion and
the Patch26 and Patch51 gates, the map bar, the encyclopedia badge, the Elite Emissary gold sale, 26
localisation files and the docs. First wave: Agent 5 data flow, Agent 2 engine compatibility, Agent 7
XML. Codex did not run (no paid-dispatch request). The code graph was UNCHECKED: graphify is not
installed on the laptop and its output root is a drive the laptop lacks.

**Top line.** No CRITICAL or HIGH, so no second wave. Every engine member the change binds checked out
against a full decompile of the installed v1.4.8 DLLs (10 verified, 0 incompatible). One MEDIUM gap
mattered to players: gold mode made the eight elites with a `recruit_cost` free to recruit from
prisoners, and the new feature doc stated the false engine fact that hid it. That claim was the same
one corrected after the #558 review on 2026-09-11, so it is a repeat. The second MEDIUM was a label key
no screen can show, added in the main session's own edit after the build. The rest were LOW: an
undocumented Supply Lines interaction, three display or documentation gaps, a saturation undercharge in
the emissary, mixed line endings in 12 language files, a stale MCM hint and a wrong comment. My own
verification run also exposed a regression the v2.4.0 release left on the trunk: the two release notes
were linked from nowhere, so the doc-graph ratchet failed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | In gold mode `SpecialResourcesBehavior.OnUnitRecruited` returns before `ChargeRecruitCost`, and the party screen's prisoner recruit never prices through `GetTroopRecruitmentCost`, so the eight troops with a `recruit_cost` (elephant, Mumakil, spider, three rams, Ithilien Ranger, Fountain Guard) cost nothing from prisoners while resource mode charges them. v1.4.8 raises `OnUnitRecruited` from `RecruitPrisonersCampaignBehavior.cs:130` once per prisoner unit. | Data flow, repeat | The plan treated "the surcharge is in the recruit-price model" as covering every recruit path and never asked which recruit paths skip the model. The engine research listed the raisers of `OnUnitRecruited` from the recruit VM and `RecruitmentCampaignBehavior` and missed the third, inside another behavior's listener. The same omission was recorded as finding 6 of `rca-special-resources-outflow-2026-09-11.md`, and the correction lived in that RCA and a Codex prompt, not where the next search looks. | Fixed on the maintainer's word: `SpecialResourceGoldPrisonerBehavior` listens to `OnMainPartyPrisonerRecruitedEvent` and takes `recruit_cost` x rate per unit in gold, ungated like the resource charge (#563); `GetGoldPrisonerRecruitCharge` sums in double and saturates; four service tests and three source pins. The three raisers are now a gotcha in `docs/reference/engine/campaignevents-and-campaignbehavior.md`, and a lesson in `lessons/campaign-mechanics.md`. |
| 2 | MED | The new feature doc said prisoner recruits raise no `OnUnitRecruited` in v1.4.8 and called the correct code comments stale. | Evidence | The builder copied the research report's claim into the doc without opening the decompile. | Corrected in `special-resources.md`; the engine-doc gotcha above is the durable record. |
| 3 | MED | `taom_res_gold_price` ("Elite troop price"), the label on the upgrade surcharge, could never render: `DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade` builds `new ExplainedNumber(...)` without `includeDescriptions`, so there is no explainer and `Add` drops every description (`ExplainedNumber.cs:158-161`, `:235`), and the only reader takes `RoundedResultNumber` (`CharacterObject.cs:514-517`). Its English row also lacked the `{=key}` prefix, and its English text in the CJK and Russian files failed `NoTranslatedString_MixesWritingSystems`. | Dead data | The main session added the key to replace the builder's upkeep label on the upgrade line, judging the wording and never asking whether that `ExplainedNumber` keeps descriptions at all. The builder's original label was equally invisible there. | Key and its 13 rows deleted; the surcharge is added without a description; the doc records why. Lesson in `lessons/gamemodels-services.md`. |
| 4 | LOW | Supply Lines prices troops through `GetTroopRecruitmentCost(troop, Hero.MainHero)` (`SupplySourceService.cs:379-392`) and multiplies by its own markup, so gold mode adds the surcharge there times up to about 2.6; resource mode never charged `recruit_cost` on that path. | Data flow | The research mapped the engine's callers of the model and not TAOM's own. | Kept on the maintainer's word and documented in the accepted list. When a model method is gated, grep `Main/` for its callers as well as the decompile (lesson 1 covers it). |
| 5 | LOW | The recruit screen's cart preview sums `TroopWage` (`GetCharacterWage`, deliberately free of the surcharge), so it shows less than the next day's wage. | Display | Accepted design (`GetCharacterWage` feeds `Campaign.AverageWage`); the preview was not on the plan's list of readers. | Documented in the accepted list. |
| 6 | LOW | The emissary's quantity picker offered `balance / price` units, while `Purchase` priced `ToGold(merchant_cost x quantity, rate)`, which saturates at `MaxGold`: a buyer with more than 10,000,000 gold could buy past it and pay only 10,000,000. | Saturation without a consumer check | `MaxGold` was added for overflow safety and nothing that consumes the price checks for it. | Fixed: the maximum quantity stops at `MaxGold / price` and a larger purchase is refused before any grant; three tests. |
| 7 | LOW | `taom.print_special_resources` still prints the resource breakdown, which gold mode does not charge. | Display | The cheat was outside the plan's touch-point table. | Documented. |
| 8 | LOW | The 12 new `taom_emissary_gold` rows ended in CRLF while every other line in those files ends in CR CR LF; git stores them byte for byte. | Data hygiene | The rows were seeded by hand instead of through the project's seeder, which writes each file's own ending (TRANSLATOR_GUIDE.md:320-321). | Rewritten to the file's ending; all 33 lines of each file now match. One-off. |
| 9 | LOW | The `HideEmissaryWhenNoResource` hint described only factions without a resource; gold mode also hides the option for a buyer whose resource differs. | Documentation | The faction limit was added after the hint was written. | Hint text extended. |
| 10 | LOW | `PartyUpkeepReader.Enumerate`'s comment called its input "the party screen's roster"; its callers pass the party's `MemberRoster` or the desertion model's `troopsToDesert`. | Documentation | Written from the plan, not from the callers. | Comment corrected. |
| 11 | LOW, process | The trunk at `7383cc5aa` fails `test_doc_graph_ratchet` (orphans 23 against 22, components 28 against 26): `docs/releases/v2.4.0-discord.md` and `v2.4.0-testers.md` were linked from nowhere. | Verification order | The v2.4.0 release verified the tree at `538c7cbe3`, before the release commit wrote the two notes, and nothing after that ran the tool tests. | Both notes now have rows in `docs/INDEX.md` beside the other release notes; the ratchet passes. The `/release` skill's Phase 5 now says to add those rows. |

## Root-cause pattern

Findings 1, 3 and 4 share one shape: a change that moves a charge or a label onto an engine seam
assumed the seam is the whole flow. The recruit-price model is not every recruit, an `ExplainedNumber`
label is not every tooltip, and the engine's callers are not every caller. Each was found by reading
the consumer side (who raises the event, who builds the number, who calls the method), which the plan
did for the three engine price paths and not for the paths around them.

## Why each agent missed these

- **The builder** worked from the plan, and the plan's research carried finding 1's omission; finding 2
  is the builder repeating it in prose.
- **The main session** added finding 3's key in a wording fix, outside the plan and before the review.
- **Agent 5 (data flow)** found 1, 3, 4, 5, 6, 7 and 9; **Agent 2 (engine)** found 1, 4, 5, 6 and 10;
  **Agent 7 (XML)** found 3 and 8. Nothing they reported was missed by all three.
- **Finding 11** was outside the change and outside every lens; the full tool-test run exposed it.

## Feedback memories to codify

None beyond the two lessons: both are subsystem rules, and the engine-doc gotcha carries the repeat.
