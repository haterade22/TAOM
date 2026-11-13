# RCA: seeded language rows and the TAOM_Map coverage baseline (deep-review 2026-11-13)

Two tests were red on the trunk after #1169 and #1164 (221 keys with no row in the 12 language files), and the data gate `loc-coverage` failed. On the project lead's word this change seeds the rows in English, tags the 20 new clan names in Brazilian Portuguese and Italian, and raises six numbers of `tools/external-loc-coverage-baseline.json`. `/deep-review` ran lens 7 (XML) and lens 5 (Data flow) at `high` effort, as a workflow with one skeptic per finding of MEDIUM or above. Nothing was MEDIUM or above, so no skeptic ran. Codex did not run (out of usage).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The ten Khand clan names are English phrases ("Dust Wolves"), and `generate_name_tags.py` gives them the proper-noun tag through its "name kept as written" branch (`{.NS}` in BR, `{.MN}` in IT). `docs/features/name-grammar-tags.md` says a row still in English is not tagged. | Tool rule and its doc disagree for a seeded row | The seed puts English text into a row, which the tag rules were not written for: they see a body equal to its English and take it for a name that is kept as written. | Left as it is: the tags are derived again when the names are translated (`generate_name_tags.py --check` fails until then), and an untagged name was also wrong in both languages. FOLLOW-UP #1129: the translation run of these keys. |
| 2 | LOW | Once the tests are green, no gate reports that 221 rows are still English, and the comment on the backlog issue #1129 described the keys as not seeded. | Backlog record behind the change | The record was written by another session before this change. | A new comment on #1129 after the merge: seeded on 2026-11-13, tags applied, the paid run owed; `translate_with_claude.py --lang <L> --module TAOM --dry-run` lists the keys under "needs LLM". |
| 3 | NIT | The draft commit body for the baseline named the wrong regions (Dunland and Lorien) and a write order that the file times do not prove. The villages are Dol Guldur and Mirkwood (`Culture.dolguldur`, `Culture.mirkwood` in the live `settlements.xml`). | Explanation repeated from a scout, not from the proof | The orchestrator copied the scout's account into the draft. The two lenses then disagreed with it and with each other, and the orchestrator read the live file. | The commit body states the proof (every counted row has a cached translation equal to its English, no key lacks a row) and names the villages from the file. Covered by the lesson "A sentence about what the engine never does names the search behind it". |
| 4 | NIT | The draft commit body for the seed said "no change in game". The 20 tagged clan names change which article and preposition vanilla sentences pick in BR and IT. | Effect of a generated tag not traced to the player | The orchestrator reasoned from the seeded text alone. | The commit body states the change. One-off. |

## Root-cause pattern

Findings 3 and 4 are both a summary written before its evidence was read, the pattern of `evidence-over-claims.md` C.1. Both were in drafts and were corrected before the commit.

## Why each agent missed these

- Lens 5 found findings 1, 2 and 4, and the right cultures for finding 3. Lens 7 found the wrong write order of finding 3 and took the regions from the body of `65914879c`, which names them wrongly too.
- The four scouts that planned the change did not trace the tag to a rendered sentence; the question was not in their briefs.
- Lenses 1, 2, 3, 4 and 6 and the Tooling lens did not run: the change holds no C# and no script.

## Feedback memories to codify

None.
