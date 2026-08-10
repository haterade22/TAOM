# TAOM Translation Guide

This guide is for anyone translating TAOM (Tales From the Age of Men) into another language. It covers the files to edit, the format rules, how to use the AI-assisted translation pipeline, and how to test in-game.

## Overview

TAOM ships with translatable strings across **three modules** and **eight file types**. Total: ~12,000 strings per language.

### TAOM module (8 files, ~8,157 strings)

Located at `Main/_Module/ModuleData/Languages/<LANG>/`:

| File | What it contains | Entries |
|------|------------------|---------|
| `std_taom_module_strings_{locale}.xml` | Faction names, titles, culture terms, UI labels (career screen, main menu, etc.) | ~2,104 |
| `std_taom_wanderer_strings_{locale}.xml` | Wanderer backstories for all cultures | ~1,337 |
| `std_taom_named_companion_strings_{locale}.xml` | Named companion dialog (Aragorn, Legolas, Gimli, etc.) | ~126 |
| `std_taom_cc_strings_{locale}.xml` | Character creation narratives (parents, childhood, youth, education, adulthood) | ~772 |
| `std_taom_career_strings_{locale}.xml` | Career system names, descriptions, ability tooltips, choices | ~2,050 |
| `std_taom_messenger_strings_{locale}.xml` | Messenger feature UI | ~29 |
| `std_taom_lotr_issue_strings_{locale}.xml` | LOTR custom-issue text — issue/quest titles, descriptions, giver dialog, objectives | ~308 |
| `std_taom_xslt_strings_{locale}.xml` | Kingdom/culture/clan/lord/hero descriptions injected via XSLT (Encyclopedia content) | ~1,431 |

### TAOM_Map module (1 file, ~1,102 strings)

Located at `TAOM_Map/ModuleData/Languages/<LANG>/`:

| File | What it contains | Entries |
|------|------------------|---------|
| `loc_settlements.xml` | Settlement names and descriptions (towns, castles, villages) | ~1,102 |

### LOTRLOME_Armory module (19 files, ~2,782 strings)

Located at `LOTRLOME_Armory/ModuleData/Languages/<LANG>/`:

Equipment names organized by culture: `loc_gondor.xml`, `loc_mordor.xml`, `loc_rohan.xml`, etc., plus shared `loc_LOTRAOM_weapons.xml`, `loc_LOTRAOM_shields.xml`, `loc_LOTRAOM_horses.xml`, `loc_LOTRLOME_crafting_pieces.xml`.

---

## File Format

Each translation file is XML with this structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xmlns:xsd="http://www.w3.org/2001/XMLSchema"
      type="string">
  <tags>
    <tag language="Polski" />
  </tags>
  <strings>
    <string id="taom_str_faction_ruler.empire" text="Wodz" />
    <!--          ^^ DO NOT CHANGE               ^^ TRANSLATE THIS -->
  </strings>
</base>
```

**Rules:**
- The `id` attribute is the localization key. **Never change it.**
- The `text` attribute is what you translate.
- Keep the file encoding as UTF-8.
- Use `&amp;` for `&`, `&quot;` for `"`, `&lt;` for `<`, `&gt;` for `>` inside attribute values.

---

## What to Preserve (Do Not Translate)

### Variable Placeholders

Variables are wrapped in `{CURLY_BRACES}`. Copy them exactly as-is:

```xml
<!-- English -->
<string id="..." text="the King, {RULER.NAME}" />

<!-- Polish -->
<string id="..." text="Krol, {RULER.NAME}" />
```

Common variables: `{RULER.NAME}`, `{FACTION_NAME}`, `{TOWN_NAME}`, `{HERO.NAME}`, `{COUNT}`, `{CLAN_NAME}`.

### Gender Conditionals

The `{?CONDITION}...{?}...{\?}` syntax handles gender. Translate the words inside, but preserve the syntax exactly:

```xml
<!-- English -->
<string id="..." text="{?RULER.GENDER}Lady{?}Lord{\?} {RULER.NAME}" />

<!-- Polish -->
<string id="..." text="{?RULER.GENDER}Pani{?}Pan{\?} {RULER.NAME}" />
```

**Important — gender agreement in morphologically rich languages:** If your target language needs more gender variation than English (e.g., Russian needs both pronoun + verb to agree), you may need to add additional `{?VAR}` blocks to make the translation grammatical. The validator in the AI pipeline (see below) rejects translations that change the conditional structure, so manual translations are usually the only way to handle these edge cases cleanly.

### Bracket Prefixes

Equipment names in Armory files often start with culture tags like `[Gondor]`, `[Mordor]`. Keep the brackets; translate or transliterate the inner word per your language convention.

### Proper Nouns (Tolkien Names)

Most Tolkien names should stay in their original form unless there is an established translation in your language's published Tolkien works:

| Keep as-is (default) | Examples |
|----------------------|----------|
| Place names | Gondor, Mordor, Minas Tirith, Rivendell, Osgiliath |
| Character names | Aragorn, Legolas, Gimli, Faramir, Imrahil |
| Faction names | Rohirrim, Dunlendings, Variags |

**Per-language conventions** (refer to your language's published Tolkien editions):
- **Polish** — Skibniewska or Łoziński translations (e.g., "Mordor" stays "Mordor", "Dwarf" → "Krasnolud")
- **Russian** — Kistyakovsky/Muravyov is most widely known (e.g., "Strider" → "Бродяжник", "Rivendell" → "Раздол")
- **Japanese/Korean/Chinese** — established katakana/hanja/汉字 renderings from licensed editions
- **Latin-script languages (SP/DE/FR/IT/BR/TR)** — usually retain English spelling but may transliterate where local convention exists

---

## Translation Workflow

### Option A — AI first-draft pipeline (Recommended)

The repo includes a Claude-powered translation pipeline that produces high-quality first-draft translations across all 27 files per language. You then review and refine.

**Prereqs:**
- `pip install anthropic`
- Set `ANTHROPIC_API_KEY` env var ([get one here](https://console.anthropic.com/settings/keys))

**Cost:** ~$3-10 per language for a full first pass; cache makes re-runs free.

**Commands:**

```bash
# Preview cost and counts (no API calls):
python tools/translate_with_claude.py --lang PL --dry-run

# Run full translation across all 8 TAOM files + TAOM_Map + Armory:
python tools/translate_with_claude.py --lang PL --apply

# Translate just one module:
python tools/translate_with_claude.py --lang PL --module TAOM --apply
python tools/translate_with_claude.py --lang PL --module TAOM_Map --apply
python tools/translate_with_claude.py --lang PL --module Armory --apply

# Pilot small batch first to validate quality:
python tools/translate_with_claude.py --lang PL --module TAOM --max-entries 50 --apply
```

**Which API it calls.** `--provider anthropic` is the default and unchanged. `deepseek` and
`openrouter` serve the same `/chat/completions` shape and need **no SDK installed** — the tool
speaks HTTP through the standard library for those two, so contributing takes a key and nothing
else. Each provider reads its own variable (`ANTHROPIC_API_KEY`, `DEEPSEEK_API_KEY`,
`OPENROUTER_API_KEY`) and the run exits 2 naming the one that is missing.

```bash
# The same work through a different API:
python tools/translate_with_claude.py --lang PL --module TAOM --provider deepseek --apply

# Override the provider's model, or the prices the estimate is printed from:
python tools/translate_with_claude.py --lang PL --provider openrouter \
    --model deepseek/deepseek-v4-pro --price-in 0.435 --price-out 0.87 --apply
```

`--batch` is the Anthropic Batches API (50% price); asking for it with another provider is refused
up front rather than failing mid-run. Batch size is per provider — 40 for Anthropic, 20 for the
others, because a 40-entry Polish batch spends `deepseek-v4-flash`'s whole 8,192-token output
budget and the JSON arrives truncated.

> **The strings leave the project.** `deepseek` and `openrouter` send TAOM's English source text to
> a third party. So does the Anthropic default, but it is worth saying once: the strings are the
> mod's own content, and picking a provider is picking who sees it.

**Pointing it at your install.** `--module TAOM` reads its sources from the repo and needs no game.
`--module TAOM_Map`, `--module Armory` and `--module all` read the installed modules, so set
`$BANNERLORD_GAME_DIR` to your Bannerlord root. A root that is not there now exits 2 naming the
folder; it used to report `0 untranslated entries, ~$0.00` and exit 0, which reads as "nothing to
do" for a module it never looked at.

**Translation chain (4-tier fallback per entry):**

1. **Hand-curated override** — `tools/translation_overrides/<lang>.json` (canonical Tolkien names that should ALWAYS use a specific translation)
2. **Cache** — `tools/translation_cache/<lang>.json` (previously translated; free on re-run)
3. **Claude API** — `claude-opus-5` (`MODEL` in `translate_with_claude.py`) with strict prompt about preserving placeholders
4. **English fallback** — if all else fails or translation breaks placeholder structure, keep English so the game text stays valid

> **Trap — the cache does not notice that the English changed.** Tier 2 matches on `string_id` alone
> (`elif e.string_id in cache`); it never compares the English source it was translated from. Editing
> the text of a key that has already been translated therefore does **not** invalidate its cached
> translation — the next run serves the old wording back into all 12 language files and silently
> undoes your edit. Whenever you change existing English source text (not just add a new key), update
> or delete those keys in `tools/translation_cache/<lang>.json` in the same change.
> Found 2026-08-06 (#388), where 165 career health strings changed "+75" to "+9"; worked example:
> `tools/retune_career_health.py`. New keys are unaffected — an absent key always reaches the API.

After running the API translator, run the rebuild step to inject the cached translations into the actual XML files:

```bash
python tools/rebuild_translation_files.py --lang PL
# or for all 12 languages:
python tools/rebuild_translation_files.py --all
```

### Option B — Manual translation from scratch

Generate English-text templates that you translate by hand:

```bash
python tools/generate_translation_template.py --apply PL
```

This populates the TAOM-module files with English placeholders. For TAOM_Map and LOTRLOME_Armory, English templates are already populated in their per-language directories.

### Option C — Hybrid (AI first, human refinement)

Run the AI pipeline (Option A), then open each file and refine the AI translations. The validator preserves placeholders but quality varies — narrative text (wanderers, hero bios, kingdom descriptions) often benefits from human polish for tone and nuance.

---

## Adding Canonical Translations to Overrides

If you want certain key terms translated consistently (e.g., always render "Strider" as "Бродяжник" in Russian), add them to `tools/translation_overrides/<lang>.json`:

```json
{
  "_comment": "Canonical translations - override AI suggestions",
  "TAOM_eleftheroi_name": "Беорнинги",
  "taom_main_menu_new_game": "Войти в Эпоху Людей",
  "TAOM_gondor": "Гондор"
}
```

The translator script applies overrides BEFORE consulting the cache or API, so they always win. Edit the JSON, re-run `python tools/rebuild_translation_files.py --lang RU`, and your overrides propagate everywhere the key is referenced.

---

## Testing In-Game

1. Build the mod: `./build.ps1`
2. Launch Bannerlord with TAOM, TAOM_Map, and LOTRLOME_Armory all enabled in the launcher's load order
3. Go to **Options → Game → Language** and select your language
4. **Restart Bannerlord** (the engine reloads the language dictionary on startup, not live)
5. Spot-check:
   - **Main menu** — "Enter The Age Of Men" should be translated
   - **New campaign → CC** — parent/childhood/youth prompts in your language
   - **Encyclopedia → Reinos/Kingdoms** — Gondor/Mordor/etc. descriptions translated
   - **Encyclopedia → Personagens/Heroes** — Húrioneth and other hero bios translated
   - **World map** — settlement names (Minas Tirith, Hornburg, etc.) translated
   - **Recruit a wanderer** — dialog translated
   - **Inventory** — armor/weapon names translated
   - **Career screen (V key)** — career names + descriptions translated

---

## Submitting Your Translation

**Option A: GitHub Pull Request (preferred)**
- Fork the repository
- Edit only your translation files in `Languages/{LANG}/`
- Open a PR targeting the active branch (currently `bannerlord-1.4.5`)

**Option B: Send Files Directly**
- Zip your translated XML files (preserve the `<MODULE>/Languages/<LANG>/` folder structure so they drop into the right place)
- Share via Discord or the mod page

---

## How the Engine Picks Up Translations

You don't need to register anything — Bannerlord auto-discovers translation files at startup. The convention:

1. Engine scans every active module's `ModuleData/Languages/` directory recursively for `language_data.xml`
2. Each `language_data.xml` declares the language id and lists `<LanguageFile xml_path="..."/>` entries
3. The engine loads each referenced XML into the global string dictionary
4. When the game references `{=KEY}default text`, it looks up KEY in the dictionary; if present, uses the translation, otherwise keeps the inline default

TAOM mod code doesn't touch this — it's pure engine convention.

---

## String Categories Reference

### Module Strings (taom_module_strings, ~653)

| Pattern | Example | Controls |
|---------|---------|----------|
| `str_faction_ruler.*` | "King", "Chieftain" | Ruler title |
| `str_faction_official.*` | "a noble of Rohan" | NPC reference text |
| `str_adjective_for_faction.*` | "Rohirric" | Adjective form |
| `str_neutral_term_for_culture.*` | "Rohirrim" | Plural demonym |
| `TAOM_*_name`, `TAOM_*_text` | Kingdom encyclopedia entries | Lore descriptions |
| `taom_career_*`, `taom_main_menu_*` | UI labels | Buttons/menus from C# code |

### Wanderer Strings (~1,177) and Named Companion Strings (~126)

Each character has 7 entries: `prebackstory`, `backstory_a/b/c/d`, `response_1`, `response_2`. Narrative game flavor — translate for tone.

### Character Creation Strings (~772)

Each CC narrative entry has `_text` (short prompt) + `_desc` (long flavor) pair, keyed by `taom_cc_<menu>_<culture>_<n>_text/desc`.

### Career Strings (~2,050)

Career names, descriptions, ability tooltips, choice descriptions. Inline `{=KEY}default` in the career XML files; this loc XML provides the centralized translation surface.

### XSLT Strings (~1,431)

Kingdom/culture/clan/lord/hero descriptions injected via XSLT transformation. Heaviest narrative content — most of the Encyclopedia text.

### Messenger Strings (~29)

Small UI surface for the Messenger feature.

### Settlement Names (TAOM_Map, ~1,102)

Town, castle, and village names + descriptions. Keys follow `Settlements.Settlement.name.<id>` and `Settlements.Settlement.text.<id>`.

### Armory Equipment (~2,782 across 19 files)

Armor, weapon, shield, and crafting piece names organized by owning culture.

---

## Current Coverage (as of 2026-05-24)

The AI pipeline has produced first-draft translations across all 11 supported languages (PL was hand-translated by a community member and is preserved):

| Lang | TAOM | TAOM_Map | Armory | XSLT (Encyclopedia) |
|------|------|----------|--------|---------------------|
| RU   | 99%  | 99%      | 92%    | 99.9%               |
| SP   | 97%  | 65%      | 87%    | 97%                 |
| DE   | 97%  | 65%      | 82%    | 97%                 |
| FR   | 97%  | 53%      | 92%    | 100%                |
| IT   | 97%  | 65%      | 88%    | 94%                 |
| BR   | 96%  | 65%      | 86%    | 94%                 |
| JP   | 99%  | 97%      | 92%    | 100%                |
| KO   | 99%  | 95%      | 91%    | 100%                |
| TR   | 94%  | 64%      | 84%    | 97%                 |
| CNs  | 99%  | 98%      | 89%    | 100%                |
| CNt  | 99%  | 100%     | 91%    | 99.9%               |
| PL   | hand-translated by community | partial | partial | 0% (pending) |

Untranslated entries fall back to English text — the game stays valid, just shows English where translation wasn't available.

---

## Known Limitations

- **Career choice/group display names** (e.g. specific tier choice names in the career screen) currently fall back to internal IDs. Adding display names requires schema additions.
- **`CareerButtonPrefab` "Career" label** — embedded directly in a prefab XML and not currently routed through the localization system.
- **Gender-agreement rejections** — morphologically rich languages (RU, JP, KO, TR, CN) often need more gender conditionals than English. The AI validator preserves English in those cases. Manual translation can fix these — they're available in the XML files just as the English fallback.

---

## Questions?

Open an issue on GitHub or reach out on the mod's Discord channel.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/localization-map.md](../reference/localization-map.md)

<!-- backlinks-end -->
## Adding a new strings file to the pipeline (order matters)

Registering a key in a source XML is only half the job — a key that is not wired into the
pipeline ships as its English fallback in all 12 languages, silently, with no error anywhere.

1. **Register every key in the source XML**, including any generated at build time. Keys built at
   RUNTIME from data ids (`"taom_enlist_duty_" + id + "_title"`) are invisible to a literal
   `{=key}` grep, so they need a generator — see `tools/generate_enlistment_duty_strings.py`.
2. Add the file to `SOURCES` in `tools/generate_translation_template.py`.
3. Add it to the TAOM tuple list in `tools/translate_with_claude.py`.
4. Run `generate_translation_template.py --all --apply`.
5. Add a `<LanguageFile>` entry to all 12 `Languages/*/language_data.xml`.
6. Bump the count in `LanguageDataXmlTests.AllLanguageDirs_HaveExactly…LanguageFiles`.
7. Run `translate_with_claude.py --lang <L> --module TAOM --apply` per language.

**Step 1 must complete before step 4.** `generate_translation_template.py --apply` overwrites each
per-language file with a fresh English template, discarding whatever translation is in it. The
cache makes recovery free (re-run the translator; everything returns from cache), but doing it in
the wrong order costs a full re-run — as it did on 2026-08-08, when 84 late-registered duty keys
blanked all 12 freshly-translated enlistment files.

**A non-zero exit is not necessarily failure.** The translator exits 1 if ANY entry failed
validation. Four pre-existing vanilla-derived strings with nested gender conditionals
(`{?TARGET_HERO.GENDER}lady{?}lord{\?}`) fail on every language that has not cached them:
`uc4M4bhG`, `qa4FlTWS`, `uiY3ds0Z`, `V097rA1v`. Check the failed-ID list before treating exit 1 as
a problem with your own strings.
