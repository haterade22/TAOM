---
name: localize
description: Propagate new player-facing text through TAOM's 12-language localization pipeline — wrap with {=KEY}, register the string, run the Claude translation tool, validate. Use after adding UI text or XSLT strings.
argument-hint: [c#|xml-file|xslt]
---

# Localization Propagation

Get new player-facing text into all 12 supported languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR). Full reference: [docs/localization/TRANSLATOR_GUIDE.md](../../../docs/localization/TRANSLATOR_GUIDE.md) + [tools/README.md](../../../tools/README.md) (localization section). Three cases:

## Case A — new C# text shown to the player
1. Wrap the string: `new TextObject("{=taom_my_feature_label}My Feature")` (always `{=KEY}default` form).
2. Register it: `python tools/harvest_literal_loc_keys.py --apply` lifts the default out of the literal into `taom_module_strings.xml` (or the feature's own file). Idempotent, so a hand-tuned row survives.
3. Propagate: `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply` (machine-translates to all 12 languages, PL included; overrides in `tools/translation_overrides/<lang>.json` always win). **`--sync-ids` is not optional for a new key**: the translator substitutes by id, so without a seeded row the translation is paid for and discarded.

## Case B — new SOURCE XML file containing in-game text
1. Add its row to `tools/_loc_sources.py` (the one ordered table the translator, template and name generators read; `tools/tests/test_loc_sources.py` fails until it matches the files).
2. Add a `<LanguageFile>` reference in **all 12** `language_data.xml` files (`LanguageManifests_EveryEnglishSource_HasExactlyOneLanguageFile` derives the expected list; no count to bump).
3. Create empty per-language stubs (`Languages/<LANG>/std_taom_*.xml`, header copied from a sibling); never `generate_translation_template.py --apply` over existing files, it overwrites them with English.
4. A `SubModule.xml` GameText node only when code looks the ids up through `GameTexts.FindText` (`str_*` or dotted ids). `{=KEY}` text resolves from the language files alone.
5. Run `tools/translate_with_claude.py --sync-ids`.

## Case C — XSLT injects new `{=KEY}default` text
1. A TAOM key: harvest it into `Main/_Module/ModuleData/taom_xslt_strings.xml` (precedent: commit `20713a1`), then translate.
2. **A vanilla key the stylesheet keeps** (it only rewords): register nothing. Vanilla's translations apply; a TAOM row would load after SandBox's and replace a curated translation (`VanillaKeyOverrideTests`). Change of meaning: give it a TAOM key.

## Case D: inline `{=KEY}default` in data XML (troops, characters, heroes, careers, cultures, scenes)
1. `git status` the source files first: the generator reads the working tree, other sessions' edits included.
2. `python tools/generate_name_localization_strings.py --apply` (one pass; `--check` and `tools/tests` fail while a source is ahead of its generated file).
3. A hero, NPC or culture key that two sites share with different English needs its own key first (`EveryNameKey_InTheNameGeneratorsSources_HasOneEnglishDefault`). Heroes' `text=` is saved with the hero, so give **both** sites new keys and retire the old one, or old saves show the other hero's text.
4. `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply`.

## Tools
- `tools/translate_with_claude.py` — 4-tier fallback (override → cache → LLM → English); cache in `tools/translation_cache/<lang>.json` is git-tracked, so re-runs are free. `--provider anthropic` (default, `claude-opus-5`) | `deepseek` | `openrouter`; the last two need no SDK and read `DEEPSEEK_API_KEY` / `OPENROUTER_API_KEY`. `--module TAOM` works with no game installed; `--module all` reads TAOM_Map and Armory from the install, so it needs `$BANNERLORD_GAME_DIR` and exits 2 without it.
- `tools/rebuild_translation_files.py` — rebuild per-language files from cache.
- `tools/translation_status.sh` — coverage report.

## Validate
`dotnet test TAOM.Tests --filter "Infrastructure.Localization"`:
- `LanguageDataXmlTests`: one LanguageFile per English source in every language (`LanguageManifests_EveryEnglishSource_HasExactlyOneLanguageFile`), well-formed XML, no missing files.
- `VanillaKeyOverrideTests` (LiveInstall): no TAOM language row replaces a vanilla key's translation outside its allowlist.
- `LanguageTextIntegrityTests`: decoding damage and wrong-writing-system words (any non-Latin, non-CJK, non-Cyrillic letter, e.g. Devanagari, counts as foreign) in the rows and the cache.
- `LanguageFileCoverageTests` — every key the English source declares has a row in all 12 language files. Red here means you registered but never propagated: run the translator with `--sync-ids`.
- `UnregisteredLocalizationKeyBaselineTests` — no `{=taom_*}` literal in C# lacks a ModuleData row. Red means you skipped step 2; `python tools/harvest_literal_loc_keys.py --apply` does it.

## Gotchas
- Morphologically-rich languages (RU/JP/KO/TR/CN) hit gender-agreement rejections that fall back to English — flag for human polish (no auto-fix).
- Three external modules also have loc (`TAOM_Map`, `LOTRLOME_Armory`) deployed straight to the game install — not in the repo.
