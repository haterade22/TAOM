# TAOM Tools

Data generation, rebalancing, and content pipeline scripts.

All Python scripts support `--dry-run` (preview) and `--apply` (write). Run from the repo root.

## XML I/O convention (MANDATORY for scripts that edit ModuleData XML)

Bannerlord ModuleData XML is UTF-8 (some files with a BOM, e.g. `TAOM_Map/settlements.xml`; most repo files without) and CRLF. To edit byte-faithfully:

```python
had_bom = path.read_bytes().startswith(b"\xef\xbb\xbf")   # detect
text = path.read_text(encoding="utf-8-sig")               # decode (strips BOM from the string)
# ... regex edits ...
path.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))   # write, re-prepend BOM as bytes
```

- **Never** write the BOM as a string literal `"﻿"` (fragile if the `.py` is re-encoded).
- **Never** read with plain `utf-8` (leaves the BOM as a stray U+FEFF in the decoded string).
- Back up before destructive writes: `path.with_suffix(path.suffix + ".bak").write_bytes(path.read_bytes())`.
- Scene/asset/id comparisons must be **case-insensitive** (Windows lookup is) — lowercase both sides.

Reference: `docs/reviews/rca-scene-tooling-2026-05-28.md` (why this convention exists) + `.claude/rules/vanilla-data-comparison.md`.

---

## Content Generation

| Script | Purpose | Output |
|--------|---------|--------|
| `generate_gondor_troops.py` | Generate Gondor troop tree XML | `Main/_Module/ModuleData/troops/troops_gondor.xml` |
| `generate_rhun_troops.py` | Generate Rhûn troop tree XML | `Main/_Module/ModuleData/troops/troops_rhun.xml` |
| `generate_gondor_armor.py` | Generate Gondor armor item definitions and append to LOTRLOME_Armory | LOTRLOME_Armory armor XMLs |
| `generate_char_creation_equipment.py` | Generate character creation equipment rosters for 10 custom cultures | `Main/_Module/ModuleData/taom_char_creation_equipment.xml` |
| `generate_xslt.py` | Generate `spcultures.xslt` from LOTRAOM reference data | `Main/_Module/ModuleData/spcultures.xslt` |
| `generate_batch2_wanderers.py` | Generate wanderers for 8 kingdoms lacking LOTRAOM data | taom_wanderers*.xml files |
| `extract_wanderers.py` | Convert LOTRAOM wanderer data into TAOM format | 4 taom_wanderer_*.xml files |

## Rebalancing

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `rebalance_troops.py` | Uniform baseline + cultural modifier for all troop skills | `--dry-run`, `--apply` |
| `rebalance_armor.py` | Baseline + cultural modifier formula for all armor items | `--dry-run`, `--apply`, `--export-csv` |
| `rebalance_weapons.py` | Points-based weapon damage with per-culture multipliers | `--dry-run`, `--apply`, `--export-csv` |
| `rebalance_lords.py` | Baseline + cultural modifier + age scaling for all lords | `--dry-run`, `--apply`, `--export-csv`, `--skills-only` |

## Lords & Equipment Assignment

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `assign_lord_equipment.py` | Replace vanilla equipment template refs with TAOM culture-specific templates in `lords.xml` | `--dry-run`, `--apply` |
| `assign_xslt_lord_equipment.py` | Assign LOTR equipment to XSLT-transformed lords in `lords.xslt` | `--dry-run`, `--apply` |
| `complete_lords_xslt.py` | Make all vanilla lord attributes explicit in `lords.xslt` (no passthrough) | `--dry-run`, `--apply`, `--export-csv` |
| `fix_lord_cultures_and_mounts.py` | Fix lord cultures + add mounts to battle equipment templates | `--dry-run`, `--apply` |

## Cleanup (One-Shot)

| Script | Purpose |
|--------|---------|
| `cleanup_deleted_gondor_armor.py` | Remove orphaned Gondor armor entries whose FBX sources were deleted | `--dry-run`, `--apply` |
| `cleanup_deleted_gondor_items.py` | Remove deleted Gondor item definitions from LOTRLOME_Armory (no args) |

## Faction Map

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `assemble_faction_map.py` | Assemble FactionMap from cropped region PNGs via template matching; outputs regions.json, factions.json, polygon_widgets.xml | None (hardcoded paths) |
| `border_match.py` | Position regions by matching alpha outlines to map border lines (dependency of assemble_faction_map) | None |
| `process_faction_map.py` | Process full-canvas region PNGs into deploy-ready FactionMap assets | `--input`, `--output`, `--checklist`, `--dry-run` |

## Settlements

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `merge_settlements.py` | Merge settlement names/owners from repo into map file, preserving positional data | `--dry-run`, `--apply` |

---

## PowerShell Scripts

| Script | Purpose | Key Parameters |
|--------|---------|----------------|
| `Generate-Settlements.ps1` | Generate `settlements.xml` from `scene.xscene` entity data | `-SceneFile`, `-ExistingSettlements`, `-OutputFile` |
| `Apply-SettlementNames.ps1` | Apply LOTR names to `settlements.xml` from a name mapping file | `-SettlementsFile`, `-DryRun` |
| `Settlement-Breakdown.ps1` | Report settlement counts by region and type | None |
| `Generate-ActionSets.ps1` | Generate Bannerlord 1.3-compatible `action_sets.xml` merging Native + custom dwarf animations | `-NativePath`, `-OldModPath`, `-OutputPath` |
| `Generate-SceneEntitiesDoc.ps1` | Extract settlement entities from `scene.xscene` into markdown doc | `-SceneFile`, `-OutputFile` |

---

## Localization Pipeline

Four scripts that together produce, translate, validate, and inject loc XMLs across all 12 supported languages × 3 modules (TAOM + TAOM_Map + LOTRLOME_Armory). Per-language full coverage is **~10,019 strings** across 27 files (7 TAOM + 1 Map + 19 Armory).

| Script | Purpose | Output |
|--------|---------|--------|
| `generate_translation_template.py` | Generate English templates for a target language across the 7 TAOM source XMLs | `Main/_Module/ModuleData/Languages/<LANG>/std_taom_*.xml` |
| `translate_with_claude.py` | AI first-draft translation via Claude API (Sonnet 4.5). 4-tier fallback: override → cache → LLM → English. Translates TAOM + TAOM_Map + LOTRLOME_Armory. | All 27 language XMLs for `<LANG>` |
| `rebuild_translation_files.py` | Inject cached translations into XML files from scratch (rebuilds the language file structure using English source + overrides + cache). Use after API runs to apply translations cleanly. | All 27 language XMLs for `<LANG>` (or `--all` for every language) |
| `translation_status.sh` | One-shot status dashboard: per-language cache size + last batch line + running process count. | (stdout) |

**Source XMLs** (the engine's English fallback + translator's discoverable list):
- `Main/_Module/ModuleData/taom_module_strings.xml` (~653 — faction names, UI labels)
- `Main/_Module/ModuleData/taom_wanderer_strings.xml` (~1,177 — wanderer backstories)
- `Main/_Module/ModuleData/named_companions/named_companion_strings.xml` (~126 — Aragorn etc.)
- `Main/_Module/ModuleData/taom_cc_strings.xml` (~772 — CC narratives)
- `Main/_Module/ModuleData/taom_career_strings.xml` (~2,050 — career names + tooltips)
- `Main/_Module/ModuleData/taom_messenger_strings.xml` (~29 — Messenger UI)
- `Main/_Module/ModuleData/taom_xslt_strings.xml` (~1,431 — kingdom/culture/clan/lord/hero descriptions extracted from XSLT)

**External-module source XMLs** (not in repo, in game install — already include English text with inline `{=KEY}`):
- `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` (~1,102 settlement names)
- `<game>/Modules/LOTRLOME_Armory/ModuleData/Languages/loc_*.xml` (~2,782 equipment names across 19 files)

**Configuration:**
- Overrides (hand-curated canonical translations, e.g. Tolkien proper nouns): `tools/translation_overrides/<lang>.json` — git-tracked, edit freely
- Cache (machine-written API results): `tools/translation_cache/<lang>.json` — git-tracked, ~700KB-1.3MB per language

**Setup:** Set `ANTHROPIC_API_KEY` env var. Estimated cost ~$3-10 per language for a full first pass (~10,000 strings). Cache makes re-runs effectively free.

**Usage:**
```bash
# Preview what would be translated and rough cost (no API calls):
python tools/translate_with_claude.py --lang RU --dry-run

# Pilot a small batch first to validate prompt quality:
python tools/translate_with_claude.py --lang RU --module TAOM --max-entries 50 --apply

# Full run for a language (all 27 files across 3 modules):
python tools/translate_with_claude.py --lang RU --apply

# Translate just one module:
python tools/translate_with_claude.py --lang RU --module Armory --apply

# After API runs: rebuild XML files from cache + overrides
python tools/rebuild_translation_files.py --lang RU
python tools/rebuild_translation_files.py --all  # all 12 langs

# Status check on a running parallel batch:
bash tools/translation_status.sh
```

**Parallel runs:** `translate_with_claude.py` can be safely run for multiple languages in parallel (each language uses its own cache file — no contention). Recommended for full-suite refresh.

**Quality enforcement:** the script validates that every `{VARIABLE}` and `{?GENDER}{?}{\?}` placeholder/conditional present in the English source is preserved verbatim in the translation. Translations that fail validation keep the English text — never get poisoned with broken markup.

**Idempotent / resumable:** cache persists every batch, so an interrupted run resumes from where it stopped on the next invocation. Re-running a fully-translated language is a no-op.

See `docs/localization/TRANSLATOR_GUIDE.md` for the translator-facing workflow + canonical Tolkien name conventions per language.

---

## Common Dependencies

**Python:** `xml.etree.ElementTree`, `argparse`, `re`, `csv`
**Image processing (faction map only):** `Pillow`, `numpy`
**AI translation (translate_with_claude.py only):** `anthropic` SDK

**Hardcoded paths** (update if your environment differs):
- TAOM repo: `c:\Users\mikew\source\repos\TAOM\`
- LOTRAOM assets: `E:\LOTRAOMAssets\`
- Bannerlord: `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\`

---

## Data Files

| File | Purpose |
|------|---------|
| `armor_rebalance.csv` | Exported armor tier classifications from `rebalance_armor.py --export-csv` |
| `weapon_rebalance.csv` | Exported weapon rebalance data from `rebalance_weapons.py --export-csv` |
| `lords_inventory.csv` | Exported lord attribute inventory from `complete_lords_xslt.py --export-csv` |
