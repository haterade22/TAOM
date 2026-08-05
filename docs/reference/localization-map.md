# Localization — file & path map

> Full localization component map. Extracted from CLAUDE.md 2026-07-18. Workflow: `/localize` + `docs/localization/TRANSLATOR_GUIDE.md`.


12 supported languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR) × 3 modules (TAOM, TAOM_Map, LOTRLOME_Armory) = ~10K strings per language. PL is community-hand-translated; the other 11 have AI first-draft translations (Claude Sonnet 4.5 via `tools/translate_with_claude.py`).

| Component | Location | Notes |
|-----------|----------|-------|
| **Translator-facing guide** | [docs/localization/TRANSLATOR_GUIDE.md](../../docs/localization/TRANSLATOR_GUIDE.md) | Full workflow, AI pipeline, manual fallback, Tolkien naming conventions |
| **Source loc XMLs** (English defaults + translator's discoverable key list) | `Main/_Module/ModuleData/taom_*_strings.xml` (×10) + `named_companions/named_companion_strings.xml` | 11 source files. Each entry uses `text="{=KEY}default"` format. **`taom_enlistment_strings.xml` (36 keys, #375/#376) is source-only so far** — it is registered as a GameText node in `Main/_Module/SubModule.xml`, so English renders, but no language has `std_taom_enlistment_strings_*.xml` yet. |
| **Per-language translation files** | `Main/_Module/ModuleData/Languages/<LANG>/std_taom_*.xml` | 10 files per language today. Engine auto-discovers via `language_data.xml`. **When the enlistment translations land, this becomes 11 and `LanguageDataXmlTests.AllLanguageDirs_HaveExactlyTenLanguageFiles` must move 10 → 11** (name + message included) — the test pins the count deliberately, so a half-finished translation pass fails loudly instead of shipping a language with a missing file. |
| **External module translations** | `<game>/Modules/TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml`, `<game>/Modules/LOTRLOME_Armory/ModuleData/Languages/<LANG>/loc_*.xml` | Not in repo (deployed straight to game install). |
| **Translation tools** | [tools/translate_with_claude.py](../../tools/translate_with_claude.py), [tools/rebuild_translation_files.py](../../tools/rebuild_translation_files.py), [tools/generate_translation_template.py](../../tools/generate_translation_template.py), [tools/translation_status.sh](../../tools/translation_status.sh) | See [tools/README.md](../../tools/README.md#localization-pipeline). |
| **Overrides** (hand-curated canonical translations) | `tools/translation_overrides/<lang>.json` | E.g., Russian Tolkien names: Бродяжник, Мордор. Always wins over LLM. |
| **Cache** (machine-translated, resumable) | `tools/translation_cache/<lang>.json` | Git-tracked. Re-runs free. ~700KB-1.3MB per lang. |
| **Validation tests** | [TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs](../../TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs) | Enforces 8 LanguageFile refs per language, well-formed XML, no missing files. |

- **New C# player-facing text:** wrap `{=KEY}default`, add to `taom_module_strings.xml`, re-run the translation tool. **New source XML text files:** SubModule GameText node + `<LanguageFile>` x12 + stubs + bump the `LanguageDataXmlTests` count. **XSLT-injected `{=KEY}` text:** harvest into `taom_xslt_strings.xml` (precedent `20713a1`), then translate. Full workflow: `/localize` + [TRANSLATOR_GUIDE.md](../../docs/localization/TRANSLATOR_GUIDE.md).

