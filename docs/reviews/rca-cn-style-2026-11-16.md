# RCA: Chinese AI drafts follow the Chinese team's text (#1421)

Findings from the review of #1421: `tools/build_translation_reference.py` writes an English and team text pair
for every row of ours that carries the Chinese team's text, and `translate_with_claude.py` gives each CNs and CNt
batch the team's terms and style examples from it (`tools/_team_reference.py`). Tools, tests and docs only, no C#
or XML, so it ran as tier 2: the tooling lens at `high`. Codex did not run: it has had no usage since 2026-11-15,
and the maintainer said to skip it. The lens rated one finding HIGH by its rule for a writer that can lose data;
git restores the file, and the fix is small and tested, so no second wave ran. Each finding was read again against
the files, and rows 2 and 3 against the shipped `cns.json`, before it was fixed.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | Tooling F1: a drop with no rows for the language (a wrong zip, a drop with no CNt folder) wrote `"pairs": []` over the tracked reference and exited 0; the translator then sent CN requests with no guide and printed nothing | Writer with no empty-input guard | The builder tested a good drop and a dry run, not a wrong drop | A language with no team rows and no runtime files is a problem, and a language with no pairs writes nothing; exit 2. `test_main_DropWithoutRowsForTheLanguage_WritesNothingAndExits2`. The count line names the modules read |
| 2 | MED | Tooling F2: term matching read the words inside placeholders, so `{?RULER.GENDER}` matched the screen label `GENDER => 性别` (130 TAOM rows had such a match) | Matcher reads markup as text | The tests used plain sentences only | `words()` drops every `{...}` expression first; `test_terms_WordInsideAPlaceholder_IsNotATerm` |
| 3 | MED | Tooling F3: 51 lowercase single words were terms, and the team's text for many is a context label (`horse => 骑兵队`, a cavalry unit; `victory => 战斗胜利`); `lord` matched 16 of 49 sample batches | Term rule from a guess, not the data | The builder looked at one sample batch, whose single words were names | A single-word term starts with a capital letter, is not all caps and has at least four letters; `test_terms_LowercaseSingleWord_IsNeverATerm`, `test_terms_AllCapsSingleWord_IsNeverATerm` |
| 4 | LOW | Tooling F4: the most-used-text test passed on sort order alone (洛汗 sorts first), and the builder's same rule had no test | Test that cannot fail | The fixture's majority text was also the first in sort order | The majority text now sorts last; `test_reference_pairs_TwoTeamTexts_KeepTheOneUsedMost` |
| 5 | LOW | Tooling F5: no test proved that `main` passes the reference to `call_model`, the default path | Plumbing with no test | Only the batched path got a test | `test_main_PassesTheLanguagesReferenceToEveryBatch` |
| 6 | LOW | Tooling F6: a CNs or CNt run with no reference file printed nothing | Silent default | The empty reference was designed for the other ten languages | The run prints "Team reference: none" for a team language (`TEAM_LANGUAGES`) |

## Not applied

- Tooling F7: `estimate_cost` does not count the guide (about 2,100 characters per 40-entry sentence batch). The run prints the real cost from the API's usage, and the error in the estimate is small, so a change fails the simplicity criterion.

## Why each agent missed these

- Rows 1 and 5: the builder tested the paths it ran by hand, not a wrong input or a dropped argument.
- Rows 2 and 3: the term rule was judged on one batch of names and one batch of MCM hints. The lens measured it over the whole reference and 49 batches.

## Feedback memories to codify

None. The lesson goes to `docs/reviews/lessons/localization-ui.md`.
