TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (game v1.5.7). Review the UNCOMMITTED change in this working tree (branch `chore/loc-seed-english-rows`, HEAD `git rev-parse HEAD`): show it with `git diff HEAD --stat` and `git diff HEAD`. This is a read-only review: do not edit, stage, build or commit anything.

## The change

The maintainer asked for English seed rows. 100 `{=KEY}` ids that the English sources declare had no row in any of the 12 language folders under `Main/_Module/ModuleData/Languages/<LANG>/` (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR), so two tests failed on trunk (`LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` and `LongDefeatHudStringsTests.EveryKey_IsSeededInAllTwelveLanguageFiles`) and the translator had no row to write into. The change is the output of the project's own command, run once per language with no translation call:

`python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --seed-only --apply`

It added 98 rows to each `std_taom_module_strings_<locale>.xml` and 2 rows to each `std_taom_wotr_strings_<locale>.xml`: 24 files, 1,200 added lines, 0 removed. Each row holds the English text. No translation, no cache file and no override file was changed.

## What to look for

1. **Only additions, only the intended files.** Confirm 0 removed or changed lines, no file outside the 24, no line-ending or BOM or encoding change in any of them (compare with `git show HEAD:<path>`), and that each file is still well-formed XML.
2. **Each seeded row is right.** For every seeded id, the row's `text` must equal the English default of that key in its English source (`Main/_Module/ModuleData/taom_module_strings.xml`, `taom_wotr_strings.xml` or wherever the key is declared; the source rows have the form `text="{=KEY}English"`, and a language row holds the text without the `{=KEY}` prefix). Check XML escaping of apostrophes, ampersands, angle brackets, quotes and non-ASCII letters (for example Anórien), and that variables such as `{TARGET}` are kept exactly.
3. **Placement.** Are the rows inserted where the tool's other rows of that file sit (inside the `<strings>` element, the same indentation), with no duplicate id in any file, and no id seeded into the wrong file of the pair?
4. **Completeness.** After the change, does any English key still lack a row in any language (read the test `TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs` for the exact rule and its exceptions), and did the seed add a row for a key that must NOT have one (an English-only override of a vanilla key: see `VanillaKeyOverrideTests`)?
5. **The next translation pass.** Read `tools/translate_with_claude.py` for how a later paid run finds rows to translate. Will it pick up these seeded rows (rows whose text equals the English), or does a seeded row look "already done" and stay English for ever? Read `tools/check_stale_loc_rows.py` and the translator guide `docs/localization/TRANSLATOR_GUIDE.md` for the documented flow, and say whether this change follows it.
6. **In-game effect.** For a non-Latin language (JP, KO, CNs, CNt, RU), does an English row behave differently from a missing row (a missing row falls back to the English default in the `{=KEY}default` literal)? Does `LanguageTextIntegrityTests` or any gate treat an English row in a CJK file as a fault?

## Output

Findings most severe first, each with a severity (P1 breaks the game or a language; P2 a wrong or missing row a player will see, or a row the translator will never translate; P3 minor), the file and line, and the evidence you read. Say UNVERIFIED for any claim you did not read the source for. End with what you checked and found correct.
