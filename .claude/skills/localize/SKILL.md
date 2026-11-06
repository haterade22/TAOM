---
name: localize
description: Use after adding player-facing text in C#, data XML or XSLT. Wraps with {=KEY}, registers, seeds the 12 languages, validates. The paid translator runs only on the maintainer's word.
argument-hint: [c#|xml-file|xslt]
---

# Localization Propagation

Get new player-facing text into all 12 supported languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR). Full reference: [docs/localization/TRANSLATOR_GUIDE.md](../../../docs/localization/TRANSLATOR_GUIDE.md) + [tools/README.md](../../../tools/README.md) (localization section). Three cases:

## Case A — new C# text shown to the player
1. Wrap the string: `new TextObject("{=taom_my_feature_label}My Feature")` (always `{=KEY}default` form).
2. Register it: `python tools/harvest_literal_loc_keys.py --apply` lifts the default out of the literal into `taom_module_strings.xml` (or the feature's own file). Idempotent, so a hand-tuned row survives.
3. Seed: `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --seed-only --apply` (one language per run: repeat for each of BR CNs CNt DE FR IT JP KO PL RU SP TR; overrides in `tools/translation_overrides/<lang>.json` always win). **`--sync-ids` is not optional for a new key**: the translator substitutes by id, so without a seeded row the translation is paid for and discarded. Record the owed translation (source issue, keys, module) in the commit body and the issue that tracks the work. When translation is deferred, `--sync-ids --seed-only --apply` seeds the rows without the paid translate phase; the paid run is the same command without `--seed-only`, and it needs the maintainer's word.

## Case B — new SOURCE XML file containing in-game text
1. Register the file in `SubModule.xml` as `<XmlNode><XmlName id="GameText" path="..."/>`.
2. Add a `<LanguageFile>` reference in **all 12** `language_data.xml` files.
3. Create empty per-language stubs (`Languages/<LANG>/std_taom_*.xml`).
4. **Bump** the `LanguageDataXmlTests.HaveExactlyXLanguageFiles` count in [TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs](../../../TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs).
5. Seed per language, as in Case A step 3.

## Case C — XSLT injects new `{=KEY}default` text
1. Harvest the new keys into `Main/_Module/ModuleData/taom_xslt_strings.xml` (precedent: commit `20713a1`).
2. Seed per language, as in Case A step 3.

After translating, run `python tools/loc_length_report.py --since <base>`: it lists translations much longer than their English. For each flagged key shown in a fixed-size Gauntlet widget, check that screen in game in that language, or record it under `Not-tested:` in the commit body. A key counted as "untranslated or identical to English" is checked after its translation lands.

## Case E: changing English that is already translated
Stage the edit, then run `python tools/check_stale_loc_rows.py --staged` (report only). It lists each language row left holding the old translation while the cache entry is unchanged too. Redo those rows and the cache entries in the same commit ([TRANSLATOR_GUIDE.md](../../../docs/localization/TRANSLATOR_GUIDE.md), "Changing English Text That Is Already Translated"); leave a row that is still right, such as an accent-only fix.

## Tools
- `tools/translate_with_claude.py` — 4-tier fallback (override → cache → LLM → English); cache in `tools/translation_cache/<lang>.json` is git-tracked, so re-runs are free. `--provider anthropic` (default, `claude-opus-5`) | `deepseek` | `openrouter`; the last two need no SDK and read `DEEPSEEK_API_KEY` / `OPENROUTER_API_KEY`. `--module TAOM` works with no game installed; `--module all` reads TAOM_Map and Armory from the install, so it needs `$BANNERLORD_GAME_DIR` and exits 2 without it.
- `tools/rebuild_translation_files.py`: rebuild per-language files from cache; never name `--module TAOM` (a row with no cache entry falls back to English, and it overwrites hand edits), and it skips English-only overrides of vanilla keys. It is a dry run until `--apply`, and a run with no `--module` exits 2.
- `tools/translation_status.sh` — coverage report.

## Validate
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "Infrastructure.Localization"`:
- `LanguageDataXmlTests` — enforces 11 LanguageFile refs/language (`AllLanguageDirs_HaveExactlyElevenLanguageFiles`), well-formed XML, no missing files. For Case B the count bump is mandatory or this goes red.
- `LanguageFileCoverageTests`: every key the English source declares has a row in all 12 language files, except an English-only override of a vanilla key. Red here means you registered but never propagated: run the translator with `--sync-ids`.
- `UnregisteredLocalizationKeyBaselineTests` — no `{=taom_*}` literal in C# lacks a ModuleData row. Red means you skipped step 2; `python tools/harvest_literal_loc_keys.py --apply` does it.

## Gotchas

- Search GitHub issue bodies, not only titles, for owed translator runs: #446 and four checkbox items were invisible to a title search.
- Morphologically-rich languages (RU/JP/KO/TR/CN) hit gender-agreement rejections that fall back to English — flag for human polish (no auto-fix).
- Two external modules also have loc (`TAOM_Map`, `LOTRLOME_Armory`) deployed straight to the game install, not in the repo.
