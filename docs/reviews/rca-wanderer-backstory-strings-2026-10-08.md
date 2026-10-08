# RCA: wanderer backstory strings (#757, 2026-10-08)

**Summary.** Talking to any of 53 wanderers of the goblin, mistymountainorcs, bluecraig, lindon and
arthedain cultures showed "ERROR: Text with id backstory_c doesn't exist! Variation:
spc_wanderer_goblin_8" in place of the introduction. Each culture's wanderers were cloned from a donor's
`NPCCharacter` blocks (gundabad, rivendell, gondor) and no rows were added to
`taom_wanderer_strings.xml`. The fix wrote 424 new English rows, seeded them as placeholders into the
12 language files, and added `WandererBackstoryCoverageTests`. A seven-lens `/deep-review` then
confirmed the findings below, all fixed in the same change.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 0 | HIGH (player-facing) | 53 wanderers had no backstory rows, so conversations printed ERROR text | data completeness | `GameTexts.FindText` renders a missing id as text instead of failing load; no test compared the strings file with the wanderer list; #257 fixed the same gap for two cultures by hand (2026-05-31) with no test, and it recurred three times | `WandererBackstoryCoverageTests`; the three culture and kingdom recipes name the strings file |
| 1 | MEDIUM | `mistymountainorcs_8` narrated as female on a male template | content vs data | writers were told to read `is_female`, found none, and still keyed on the surname "Goremaiden"; no engine default was stated in the brief | rows made sex-neutral; brief future writers that a template without `is_female` is male |
| 2 | MEDIUM | `arthedain_6` placed its healer "among the ruins of Annúminas", a living town in TAOM | lore vs map data | the writer brief listed "Annúminas ruins" from book lore without checking `arthedain.md`'s settlement table | rows fixed; briefs cite the feature doc's settlement table, not book lore |
| 3 | MEDIUM | the 53 `generic_backstory` rows retold the first wanderer's personal deeds, but the engine plays that line only from a second wanderer of the same template | engine semantics | the line's condition (`LordConversationsCampaignBehavior.cs:1735-1745`, v1.5.4) was read for "optional", not for "who speaks it" | rows rewritten as template-level lines; step 7 of the wanderer recipe now says who speaks it |
| 4 | MEDIUM | `NoTranslatedString_MixesWritingSystems` flagged the pipeline's own verbatim-English placeholders (950 before, 3,070 after this change), burying real translator damage | gate design | the test predates the seed-then-translate convention and never consulted the registered English | the test skips a row equal to its registered English, as `AccentStrippedTranslationTests` already does |
| 5 | LOW | stories missed their template's top skill (Medicine, Engineering, Tactics); Five Armies dating clashed with the heroes' ages; copy and spelling slips | content polish | writers read skill sets but were not told to surface the top one | fixed in the rows |
| 6 | LOW | test reimplemented a ModuleData locator, used culture-sensitive comparison, and let duplicate or empty rows pass | test quality | copied the sibling `CharacterFaceCoverageTests` shape | uses `CultureDataFixture.ModuleDataPath()`, ordinal comparers, two added assertions |
| 7 | LOW | stale counts in five docs; the Delete recipe would leave an orphan row | doc drift | counts are dated measurements nobody re-runs after a culture lands | re-measured; Delete recipe names the `generic_backstory` row |

## Root-cause pattern

Findings 0 to 3 share one cause: data was cloned or authored against a mental model (a donor culture,
book lore, "optional means unimportant") instead of against what the engine and TAOM's own data say.
The cheapest guard is the one that already exists for other culture tables (lesson "Hold a new
culture's coverage with tests that read the data", 2026-10-07): a test that reads the shipped data.

## Why each agent missed these

The original bug predates any review of this change. Within the review, every confirmed finding was
caught by at least one lens; Standards and Completeness found the process items, XML found 1, 2 and 5,
Engine compatibility and Design found 3, Data flow and Design found 4.

## Feedback memories to codify

None new. The standing rule is already "A safety barrier that rests on shipped data needs a test" in
`docs/reviews/lessons/data-content-cultures.md`; the new entry there records this as its late application.
