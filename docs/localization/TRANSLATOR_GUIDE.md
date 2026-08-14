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

### Line endings and encoding (read this before scripting against these files)

Hand translators can ignore this section; anyone writing a script that edits the per-language files
cannot. Every file under `Main/_Module/ModuleData/Languages/<LANG>/` is **UTF-8 with no BOM**: all
144 of them, measured 2026-08-14.

**The line terminator is not uniform, so detect it instead of assuming it.** Every `std_taom_*` type
except `std_taom_enlistment_strings_*.xml` ends its lines with a **doubled CR, `\r\r\n`**. That file
and `language_data.xml` use a plain `\r\n`. Measured on `std_taom_module_strings_deu-DE.xml`: 2,451
line terminators, every one of them `\r\r\n`. The TR, FR, PL and RU copies of that file report the
same count.

The doubled CR is produced, not inherited, so it comes back after every rebuild.
`build_translation_xml` in `tools/rebuild_translation_files.py` joins its lines on `\r\n` (`:107`),
and `rebuild_language` writes the result with `Path.write_text(content, encoding="utf-8")` (`:159`).
Default text mode expands each `\n` to `\r\n` on Windows, so `\r\n` goes in and `\r\r\n` comes out.
The doubled types are exactly the ten entries in that script's `taom_sources` list (`:132-143`); the
enlistment file is plain because it is not in that list, so the rebuild never touches it.

Two consequences:

- **A regex whose terminator is `\r?\n` matches nothing on a doubled file.** On that DE file
  `/>\r?\n` finds 0 matches, while the literal `/>\r\r\n` finds 2,442. The `\r?` consumes the first
  CR and then `\n` is asked to match the second CR, which fails, and the empty-`\r?` backtrack fails
  the same way. Capture the terminator and re-emit it verbatim rather than hard-coding either form,
  or the same script will corrupt the two files in each language directory that use the other one.
- **A plain text-mode read silently doubles the file.** Python's universal-newline translation turns
  each `\r\r\n` into `\n\n`, so a text-read plus text-write round trip inserts a blank line between
  all 2,451 lines and rewrites every line of the file. That is exactly the whole-file-rewrite defect
  the XML I/O convention in [tools/README.md](../../tools/README.md) exists to prevent. Read bytes,
  decode, edit, encode, write bytes.

A per-row substitution that leaves the surrounding bytes untouched is always safer here than a
normalise-then-rewrite pass.

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

> **Trap: the cache does not notice that the English changed.** Tier 2 matches on `string_id` alone
> (`elif e.string_id in cache`); it never compares the English source it was translated from. Editing
> the text of a key that has already been translated therefore does **not** invalidate its cached
> translation. The rebuild step below resolves every key from that same cache, so it writes the old
> wording back into all 12 language files and silently undoes your edit. Whenever you change existing
> English source text (not just add a new key), update or delete those keys in
> `tools/translation_cache/<lang>.json` in the same change.
> Found 2026-08-06 (#388), where 165 career health strings changed "+75" to "+9"; worked example:
> `tools/retune_career_health.py`. New keys are unaffected: an absent key always reaches the API.
> The cache is only the second of two gates, and a stale row usually never even reaches it. See
> "Changing English Text That Is Already Translated" below.

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

## Changing English Text That Is Already Translated

Adding a new key works. **Changing the English of a key that already has translations does not.** No
path through the pipeline re-translates such a row, so all 12 languages keep showing wording that
matched the old English, indefinitely. Nothing reports it: neither `translate_with_claude.py` nor
`rebuild_translation_files.py` ever compares a target row against the English it was translated
from, so a run that leaves every language stale prints the same `Untranslated entries discovered: 0`
as a run with nothing to do.

### Why: two gates, and neither one looks at the source text

| Gate | Where | What it matches on |
|------|-------|--------------------|
| **Discovery** | `_diff_files`, `tools/translate_with_claude.py:296-306` | Returns an entry only when `cur_text == eng_text`, meaning the target row still literally holds the English string. (An id the target file does not declare at all also qualifies, because `cur_text` defaults to the English: `tgt_map.get(sid, eng_text)` at `:303`.) A row that already holds a translation is never returned, so it never reaches the overrides, the cache, or the API. `_diff_against_settlement_source` (`:309-326`) filters the TAOM_Map settlement keys the same way. |
| **Cache** | `main()`, `tools/translate_with_claude.py:883` | `elif e.string_id in cache` matches on `string_id` alone. So on the occasions a row does reach discovery (a file just reset by `generate_translation_template.py --apply`, or an entry whose translation failed validation and fell back to English), the cache answers with the translation of the OLD English. |

**`--sync-ids` does not close this.** `sync_missing_ids` (`:711-754`) computes
`missing = [sid for sid in src_map if sid not in tgt_map]`, so it appends only the keys a
per-language file lacks entirely. A key that is present but stale is not missing.

**`rebuild_translation_files.py` does not close it either.** Its `resolve` (`:124-129`) does visit
every key in the English source, but it resolves override, then cache, then English fallback, and
that cache is the same `string_id`-keyed dictionary. It writes the stale translation back, and
because it rebuilds the whole file it will also overwrite a row you corrected by hand.

The one thing that outranks the cache is `tools/translation_overrides/<lang>.json`, checked first in
both tools. A key parked there is pinned no matter what the cache holds.

### Two zero-cost repairs, both used on 2026-08-14

**1. Only a number changed, so substitute per row.** When a cultural-feat description moved from
"increased by 10%." to "increased by 20%.", every language's sentence around the numeral was still
correct. A targeted per-row substitution is exact, costs nothing, and keeps each language's own
phrasing, which a fresh LLM pass would rewrite.

The numeral is not written the same way everywhere, so the substitution has to survive all of these.
Real values from `taom_feat_mor_ps_desc` after the 2026-08-14 edit:

| Form | Languages | Example |
|------|-----------|---------|
| `20%`, sign straight after the digits | BR, CNs, IT, JP, KO, PL, RU, SP | `Предел размера отряда увеличен на 20%.` |
| Percent sign FIRST | TR | `Müfreze büyüklüğü sınırı %20 arttı.` |
| Space before the percent sign | DE, FR | `Limite de taille du groupe augmentée de 20 %.` |
| Space before the NUMBER | CNt | `部隊規模上限增加 20%。` |

The form also varies row to row inside one language, not just language to language: German writes
`20 %` on `taom_feat_mor_ps_desc` and `40%` on `taom_feat_bcg_ps_desc`. Anchor on the digits, keep
whatever surrounds them, and never assume one pattern covers a whole language file.

**2. A new key whose English is verbatim identical to an existing key, so copy the rows.**
`taom_feat_bcg_ps_desc` is "Party size limit increased by 40%.", character for character the same
English as `taom_feat_gob_ps_desc`. Copying that key's 12 existing translations is free and better
than paying for a pass that would produce twelve slightly different renderings of one sentence.

### The checklist when you edit an existing English string

1. Change the English in the source XML under `Main/_Module/ModuleData/`.
2. Apply the same change to all 12 per-language rows: by substitution if it is mechanical, by hand
   or a translator run if the meaning actually moved.
3. Update or delete those keys in `tools/translation_cache/<lang>.json`, so no later run can serve
   the old wording back.
4. Respect the file format while you do it: no BOM, and whichever line terminator that particular
   file already uses. See "Line endings and encoding" under **File Format** above.

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
- **OWED (2026-08-14): `taom_feat_bcg_ps` ("Blue Craig Swarm") ships as English in all 12 languages.** The Blue Craig party-size feat was added with no API key available, so the name was seeded as English and never translated. Its description was not affected: `taom_feat_bcg_ps_desc` is verbatim-identical English to `taom_feat_gob_ps_desc`, so the goblin translation was copied into all 12. The name is deliberately ABSENT from `tools/translation_cache/*.json`, because a cache hit is consulted before the LLM tier and seeding English there would block the translation permanently. To close this, run `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply` for the 11 AI languages (PL is hand-translated). Nothing fails while this is open: `LanguageFileCoverageTests` is a presence check and the row exists holding English, so the suite stays green with the string permanently untranslated.

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
