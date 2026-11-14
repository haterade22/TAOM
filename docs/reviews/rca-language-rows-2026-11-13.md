# RCA: language manifests, seeded rows and the realms keys (#1245)

Findings from the tier 2 review of the trunk fix for #1245, with why each was missed and what now prevents it. Wave 1 ran the data flow and XML lenses at `high`. Codex did not run: the maintainer chose no Codex for this branch on 2026-11-13. No reviewer reported a CRITICAL or HIGH, so no second wave ran. Each row was read again against the files before it was fixed.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | LOW | Data flow 8: `docs/modding/file-catalogue.md` and `docs/reference/localization-map.md` still said 23 language files per language after the change made 25 | A dated count left behind by a change that moves it | The change added files and did not grep the docs for the old count | Both lines now say 25, with the date and the command that measures it. Each line already names its command, so the next change can measure it again |
| 2 | MED | XML 1, Data flow 6: the seed copies three unread keys (`taom_char_next_perk_tier`, `taom_char_next_perk_tier_one`, `taom_char_all_perk_tiers`) into the 12 language files, so a paid pass would translate 36 dead rows | Dead English rows, from trunk (eafb9af54) | `EveryLanguage_DeclaresARowForEveryEnglishKey` wants a row for every registered key, read or not; `check_orphan_loc_rows.py` runs in `data_gates.py`, not in the test suites | FOLLOW-UP #1244: wire the reader or delete the rows with their 36 copies |
| 3 | LOW | Data flow 2: `EnglishPlaceholders()` keeps the first row of a repeated id in file order, and the translator keeps the first source in `TAOM_SOURCES` order and the last row inside a file | Two tie rules for one id | Latent: no id repeats across the 25 sources today | FOLLOW-UP no issue: no current case, and a divergence would flag a seeded row (a false positive), never pass a bad translation |

## Not applied

- Data flow 3: point `EveryCSharpDefault_OfARegisteredTaomKey_MatchesTheRegisteredEnglish` at `EnglishPlaceholders()` to check 13 more C# sites. That gate is code this change did not touch. FOLLOW-UP no issue: the 13 sites match their English today.

## Why each agent missed these

- Row 1 needed a search of the docs for a count, which neither the builder nor a test does. The data flow lens found it by reading the docs that name the language folder.
- Row 2 is not a defect of this change; both lenses report it because the seed copies it.

## Feedback memories to codify

None. Row 1 is the rule that a dated count names its command, which both lines already follow.
