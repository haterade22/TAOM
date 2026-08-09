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
3. Propagate: `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply` (machine-translates to the 11 AI languages; PL is hand-translated, overrides in `tools/translation_overrides/<lang>.json` always win). **`--sync-ids` is not optional for a new key** — the translator substitutes by id, so without a seeded row the translation is paid for and discarded.

## Case B — new SOURCE XML file containing in-game text
1. Register the file in `SubModule.xml` as `<XmlNode><XmlName id="GameText" path="..."/>`.
2. Add a `<LanguageFile>` reference in **all 12** `language_data.xml` files.
3. Create empty per-language stubs (`Languages/<LANG>/std_taom_*.xml`).
4. **Bump** the `LanguageDataXmlTests.HaveExactlyXLanguageFiles` count in [TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs](../../../TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs).
5. Run `tools/translate_with_claude.py`.

## Case C — XSLT injects new `{=KEY}default` text
1. Harvest the new keys into `Main/_Module/ModuleData/taom_xslt_strings.xml` (precedent: commit `20713a1`).
2. Run `tools/translate_with_claude.py`.

## Tools
- `tools/translate_with_claude.py` — 4-tier fallback (override → cache → Claude LLM → English); cache in `tools/translation_cache/<lang>.json` is git-tracked, so re-runs are free.
- `tools/rebuild_translation_files.py` — rebuild per-language files from cache.
- `tools/translation_status.sh` — coverage report.

## Validate
`dotnet test TAOM.Tests --filter "Infrastructure.Localization"`:
- `LanguageDataXmlTests` — enforces 11 LanguageFile refs/language (`AllLanguageDirs_HaveExactlyElevenLanguageFiles`), well-formed XML, no missing files. For Case B the count bump is mandatory or this goes red.
- `LanguageFileCoverageTests` — every key the English source declares has a row in all 12 language files. Red here means you registered but never propagated: run the translator with `--sync-ids`.
- `UnregisteredLocalizationKeyBaselineTests` — no `{=taom_*}` literal in C# lacks a ModuleData row. Red means you skipped step 2; `python tools/harvest_literal_loc_keys.py --apply` does it.

## Gotchas
- Morphologically-rich languages (RU/JP/KO/TR/CN) hit gender-agreement rejections that fall back to English — flag for human polish (no auto-fix).
- Three external modules also have loc (`TAOM_Map`, `LOTRLOME_Armory`) deployed straight to the game install — not in the repo.
