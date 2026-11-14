---
name: localize
description: "Use for player-facing text in C#, XML or XSLT. Wraps with {=KEY}, registers English, validates. Translation XMLs (Languages/<LANG>/) are the project lead's: never seed, translate or edit them."
argument-hint: [c#|xml-file|xslt]
---

# Localization Propagation

Get new player-facing text into all 12 supported languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR). Full reference: [docs/localization/TRANSLATOR_GUIDE.md](../../../docs/localization/TRANSLATOR_GUIDE.md) + [tools/README.md](../../../tools/README.md) (localization section). Three cases:

## Rule: the translation XMLs are the project lead's

**Do not touch the translation XMLs**: the per-language files under `Main/_Module/ModuleData/Languages/<LANG>/` (rows, stubs, `language_data.xml`) and the translation cache and overrides in `tools/`. That means no `--seed-only` or `--sync-ids` runs, no translator runs, no `rebuild_translation_files.py`, no `name_tags.py --apply`, and no hand-written seed rows. The project lead runs the language pass once in a while and picks up every registered English key then. Your part is the `{=KEY}default` literal and its English row (the English source files such as `taom_module_strings.xml`). Until the lead's pass, `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` is red for the new keys. That is expected; name the keys in your report and leave the language files alone. The steps below marked **(project lead only)** are kept for the lead's pass.

## Case A — new C# text shown to the player
1. Wrap the string: `new TextObject("{=taom_my_feature_label}My Feature")` (always `{=KEY}default` form).
2. Register it: `python tools/harvest_literal_loc_keys.py --apply` lifts the default out of the literal into `taom_module_strings.xml` (or the feature's own file). Idempotent, so a hand-tuned row survives.
3. **(project lead only)** Seed: `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --seed-only --apply` (one language per run: repeat for each of BR CNs CNt DE FR IT JP KO PL RU SP TR; overrides in `tools/translation_overrides/<lang>.json` always win). **`--sync-ids` is not optional for a new key**: the translator substitutes by id, so without a seeded row the translation is paid for and discarded. Add a row to the open translation backlog issue (#1129 since 2026-11-12; TRANSLATOR_GUIDE.md names the current one) with the source issue, the keys and the module, and close the feature issue once only translation remains; an issue body edit needs no ask. The paid run is the same command without `--seed-only`, and it needs the maintainer's word.

## Case B — new SOURCE XML file containing in-game text
1. Add its row to `tools/_loc_sources.py` (the one ordered table the translator, template and name generators read; `tools/tests/test_loc_sources.py` fails until it matches the files).
2. **(project lead only)** Add a `<LanguageFile>` reference in **all 12** `language_data.xml` files (`LanguageManifests_EveryEnglishSource_HasExactlyOneLanguageFile` derives the expected list; no count to bump).
3. **(project lead only)** Create empty per-language stubs (`Languages/<LANG>/std_taom_*.xml`, header copied from a sibling); never `generate_translation_template.py --apply` over existing files, it overwrites them with English.
4. A `SubModule.xml` GameText node only when code looks the ids up through `GameTexts.FindText` (`str_*` or dotted ids). `{=KEY}` text resolves from the language files alone.
5. **(project lead only)** Run `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --seed-only --apply` per language (`--module` is required; `all` also writes the live Armory, #1056).

## Case C — XSLT injects new `{=KEY}default` text
1. A TAOM key: harvest it into `Main/_Module/ModuleData/taom_xslt_strings.xml` (precedent: commit `20713a1`). Translation is the lead's pass.
2. **A vanilla key the stylesheet keeps** (it only rewords): register nothing. Vanilla's translations apply; a TAOM row would load after SandBox's and replace a curated translation (`VanillaKeyOverrideTests`). Change of meaning: give it a TAOM key.

## Case D: inline `{=KEY}default` in data XML (troops, characters, heroes, careers, cultures, scenes)
1. `git status` the source files first: the generator reads the working tree, other sessions' edits included.
2. `python tools/generate_name_localization_strings.py --apply` (one pass; `--check` and `tools/tests` fail while a source is ahead of its generated file).
3. A hero, NPC or culture key that two sites share with different English needs its own key first (`EveryNameKey_InTheNameGeneratorsSources_HasOneEnglishDefault`). Heroes' `text=` is saved with the hero, so give **both** sites new keys and retire the old one, or old saves show the other hero's text.
4. **(project lead only)** Seed per language, and add the backlog row, as in Case A step 3.
5. **(project lead only)** A new kingdom, clan, culture or town name also needs its BR and IT grammar tag: `python tools/generate_name_tags.py --write`, then `python tools/name_tags.py --lang all --apply` ([name-grammar-tags.md](../../../docs/features/name-grammar-tags.md)).

## Case E: changing English that is already translated
Stage the edit, then run `python tools/check_stale_loc_rows.py --staged` (report only). It lists each language row left holding the old translation while the cache entry is unchanged too. Report those keys; the lead redoes those rows and the cache entries in the same commit ([TRANSLATOR_GUIDE.md](../../../docs/localization/TRANSLATOR_GUIDE.md), "Changing English Text That Is Already Translated"); leave a row that is still right, such as an accent-only fix.

## Tools
- `tools/translate_with_claude.py` — 4-tier fallback (override → cache → LLM → English); cache in `tools/translation_cache/<lang>.json` is git-tracked, so re-runs are free. `--provider anthropic` (default, `claude-opus-5`) | `deepseek` | `openrouter`; the last two need no SDK and read `DEEPSEEK_API_KEY` / `OPENROUTER_API_KEY`. `--module TAOM` works with no game installed; `--module all` reads TAOM_Map and Armory from the install, so it needs `$BANNERLORD_GAME_DIR` and exits 2 without it.
- `tools/rebuild_translation_files.py`: rebuild per-language files from cache; never name `--module TAOM` (a row with no cache entry falls back to English, and it overwrites hand edits), and it skips English-only overrides of vanilla keys. It is a dry run until `--apply`, and a run with no `--module` exits 2.
- `tools/translation_status.sh` — coverage report.

## Validate
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "Infrastructure.Localization"`:
- `LanguageDataXmlTests`: one LanguageFile per English source in every language (`LanguageManifests_EveryEnglishSource_HasExactlyOneLanguageFile`), well-formed XML, no missing files.
- `VanillaKeyOverrideTests` (LiveInstall): no TAOM language row replaces a vanilla key's translation outside its allowlist.
- `LanguageTextIntegrityTests`: decoding damage and wrong-writing-system words (any non-Latin, non-CJK, non-Cyrillic letter, e.g. Devanagari, counts as foreign) in the rows and the cache.
- `LanguageFileCoverageTests`: every key the English source declares has a row in all 12 language files, except an English-only override of a vanilla key. Red for your new keys is expected until the lead's language pass (see the rule above); red for keys you did not add is someone else's to report.
- `UnregisteredLocalizationKeyBaselineTests` — no `{=taom_*}` literal in C# lacks a ModuleData row. Red means you skipped step 2; `python tools/harvest_literal_loc_keys.py --apply` does it.

**(project lead only)** Then, after translating, run `python tools/loc_length_report.py --since <base>`: it lists translations much longer than their English. For each flagged key shown in a fixed-size Gauntlet widget, check that screen in game in that language, or record it under `Not-tested:` in the commit body. A key counted as "untranslated or identical to English" is checked after its translation lands.

## Gotchas

- Search GitHub issue bodies, not only titles, for owed translator runs: #446 and four checkbox items were invisible to a title search.
- Morphologically-rich languages (RU/JP/KO/TR/CN) hit gender-agreement rejections that fall back to English — flag for human polish (no auto-fix).
- Two external modules also have loc (`TAOM_Map`, `LOTRLOME_Armory`) deployed straight to the game install, not in the repo.
