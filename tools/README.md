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
- **Sanctioned alternative** (used by `Assign-SettlementOwners.py` + `rebalance_settlement_prosperity.py`): the full byte round-trip — `raw = open(path,'rb').read()`, `text = raw.decode('utf-8')` (BOM survives as a leading U+FEFF *character*), regex edits, `open(path,'wb').write(text.encode('utf-8'))` (re-emits the BOM bytes). Equally byte-faithful because both read and write are binary-mode (no CRLF translation) and the BOM travels inside the string; the "never plain utf-8" rule above targets the mixed pattern (`utf-8` decode + `write_text`), not this symmetric one. Pick either idiom; don't mix them in one script.
- Back up before destructive writes: `path.with_suffix(path.suffix + ".bak").write_bytes(path.read_bytes())`.
- Scene/asset/id comparisons must be **case-insensitive** (Windows lookup is) — lowercase both sides.

Reference: `docs/reviews/rca-scene-tooling-2026-05-28.md` (why this convention exists) + `.claude/rules/vanilla-data-comparison.md`.

---

## Validation

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `validate_moduledata.py` | **Unified schema-driven cross-reference validator** (read-only). Consolidates the per-task validators below into one engine driven by `tools/schemas/*.json`. Catches broken item/troop/culture/party-template refs, duplicate ids (NPC/culture/roster/Armory-item), missing civilian `equipmentType`, invalid `default_group`. See `docs/features/moduledata-validation.md`. | `--game-modules`, `--moduledata`, `--json`, `--code`, `--warnings-as-errors` |
| `taom_schema.py` | Engine behind the validator (registries + schema model + `Validator` + `build_registries`). Importable; unit-tested (`tools/tests/test_validate_moduledata.py`). | (library) |
| `taom_query.py` | Query API over the engine — `item_exists` / `troop_exists` / `culture_exists` / `find_references` / `validate` / listings. Pure stdlib; backs the MCP server; unit-tested (`tools/tests/test_taom_query.py`). | (library) |
| `taom_mcp_server.py` | **MCP stdio server** exposing the query API as 9 tools so Claude agents query mod-data integrity interactively (registered in `.mcp.json` as `taom-moduledata`; needs the `mcp` SDK; restart Claude to load). See `docs/features/moduledata-validation.md` "MCP server". | `python tools/taom_mcp_server.py` (smoke-test) |
| `validate_all_troop_refs.py` | Per-task: armor refs across troop files vs LOTRLOME_Armory (superseded by `validate_moduledata.py`'s `BROKEN_ITEM_REF`; kept for now). | (none) |
| `audit_item_refs.py` | Per-task: every `Item.X` ref vs the multi-module item registry (superseded by `validate_moduledata.py`'s `BROKEN_ITEM_REF`). | `--show-locations`, `--limit` |
| `validate_mesh_refs.py` | **Mesh/collision-body ref validator** (read-only, pure stdlib). Extracts every `mesh`/`body_name`/`shield_body_name`/`holster_mesh`/`holster_mesh_with_weapon`/`flying_mesh` ref from item XML and checks existence across 3 tiers: A) rgl_log content warnings (authoritative), B) `.tpac` Metamesh TOC (visual meshes), C) raw-byte scan for `bo_` collision bodies (coarse). Built to confirm/eliminate the "missing `bo_` mesh causes battle-load hang" hypothesis. Body-aware + rgl-aware successor to `Audit-MeshRefs.ps1`. See `docs/features/mesh-ref-validation.md`. | `--scan-bodies`, `--rgl-log`, `--no-rgl-log`, `--no-tier-b`, `--items`, `--game`, `--tpac-modules`, `--json`, `--code`, `--warnings-as-errors` |

`validate_moduledata.py` schemas live in `tools/schemas/`. The declarative JSON is the source of truth — add fields/enums/rules there, not in Python.

---

## Save-game diagnostics & recovery

Offline `.sav` triage — stdlib only, no game required. Both understand the v1.4.6 container format (`[int32 metaLen][JSON metadata][raw-deflate GameData]` → Header/ObjectData/ContainerData/Strings archives). See `docs/features/save-load-diagnostics.md`.

| Script | Purpose | CLI |
|--------|---------|-----|
| `inspect_sav.py` | Dump a save's ApplicationVersion / character / module:version table / `TAOM_Build`; `--verify` walks the deflated GameData section framing (OK / TRUNCATED / corrupt with byte offsets). Triage which build wrote a save and whether it's physically intact. | `<path.sav>`, `--verify` |
| `repair_sav_strings.py` | **Recovery for the v2.0.9 momentum >32 KB save-corruption bug** (`ArchiveSerializer` int16-length truncation — see the RCA). Parses the Strings archive, recovers the truncated entry length via the sequential-entry-id anchor, resets the oversized momentum string to empty, re-frames + recompresses to `<name>_fixed.sav`. Diagnose-only by default (non-destructive). Zero campaign-data loss — only the cosmetic war-meter history clears; the fixed save loads on the vanilla engine. **Player-facing Windows how-to: `docs/SAVE-REPAIR-GUIDE.md`** (paste-to-Discord/Nexus ready). | `<path.sav>`, `--repair`, `--force` |
| `repair_sav_strings.ps1` | **PowerShell twin of the above — no Python install** (ships with Windows 10/11). Verified byte-identical (decompressed) output to the `.py` on both failure shapes (day-52 >65 KB desync + day-20 negative-length). Uses .NET `DeflateStream` — the SAME library the engine uses, so the rewritten deflate is guaranteed compatible. Slower (~40 s/large save vs ~1 s) due to per-call overhead, but a one-time repair. This is the **recommended** path for non-technical players (Option A in the guide). | `-Path <sav>`, `-Repair`, `-Force` (run via `powershell -ExecutionPolicy Bypass -File`) |

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
| `add_townsfolk_battle_rosters.py` | Append a plain battle `<EquipmentRoster>` (mirroring each civilian one) to every civilian-only `<NPCCharacter>` so townsfolk/notables aren't naked as arena-stand spectators (arena spawns them with battle equipment; #295). Idempotent (skips NPCs that already have a battle roster), BOM/CRLF-preserving. `--apply` writes (default dry-run); `--glob` overrides the default `npcs_*.xml` scope. | `Main/_Module/ModuleData/characters/npcs_*.xml` |

## Rebalancing

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `rebalance_troops.py` | Uniform baseline + cultural modifier for all troop skills | `--dry-run`, `--apply` |
| `rebalance_armor.py` | Baseline + cultural modifier formula for all armor items | `--dry-run`, `--apply`, `--export-csv` |
| `rebalance_weapons.py` | Points-based weapon damage with per-culture multipliers | `--dry-run`, `--apply`, `--export-csv` |
| `rebalance_lords.py` | Baseline + cultural modifier + age scaling for all lords | `--dry-run`, `--apply`, `--export-csv`, `--skills-only` |
| `audit_cc_bonuses.py` | Audit character-creation skill/attribute/focus bonuses per culture (per-stage uniformity, value-aware worst-case concentration, vanilla-budget comparison, full menu dump). `--apply` zeroes the career-stage payload + culture-base bonus via formatting-preserving line edits (CRLF + inline arrays preserved, writes `.bak`). Reads the 6 `charactercreation/*_menu.json` + `cultures.json` + career eligibility from `career_system/taom_careers.xml`. | `--report` (default), `--out`, `--export-csv`, `--dry-run`, `--apply` |
| `analyze_settlement_prosperity.py` | Read-only starting-prosperity report: LIVE TAOM_Map vs vanilla per class, flat-cluster flags, town gold-equilibrium columns (#317). Reports to `tools/reports/settlement-prosperity/`. | `--stdout`, `--cluster-threshold`, `--game-dir` |
| `rebalance_settlement_prosperity.py` | Lift-only per-class vanilla quantile-map rebaseline of TAOM_Map starting prosperity (LIVE external file, `.bak`, idempotent; seeds NEW campaigns only — #317). | `--dry-run` (default), `--apply`, `--allow-lower`, `--town-uplift`, `--pin-zero-village`, `--preserve`, `--game-dir` |
| `author_settlement_buildings.py` | Source of truth for per-fief starting building levels (lore+role): 221 hand-assigned role tiers + overrides + rationale → pinned expander → per-culture JSONs (`data/settlement_building_levels/`) + audit doc. Asserts full coverage. See `docs/features/settlement-building-levels.md`. | (no flags) |
| `dump_settlement_buildings.py` | Read-only per-fief building-level dump from the LIVE TAOM_Map settlements.xml (the "was" source + verification; writes `reports/settlement-buildings/current_state.json`). | `--culture`, `--all`, `--towns-only`/`--castles-only`, `--json`, `--game-dir` |
| `apply_settlement_buildings.py` | Safe **two-level-regex** applier of the building-level JSONs to the LIVE file (`.bak`, byte-round-trip, exactly-once assertion, range/fort-floor/id-set validation, idempotent). Seeds NEW campaigns only. | `--dry-run` (default), `--apply`, `--culture`, `--game-dir` |

## Lords & Equipment Assignment

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `assign_lord_equipment.py` | Replace vanilla equipment template refs with TAOM culture-specific templates in `lords.xml` | `--dry-run`, `--apply` |
| `assign_xslt_lord_equipment.py` | Assign LOTR equipment to XSLT-transformed lords in `lords.xslt` | `--dry-run`, `--apply` |
| `complete_lords_xslt.py` | Make all vanilla lord attributes explicit in `lords.xslt` (no passthrough) | `--dry-run`, `--apply`, `--export-csv` |
| `fix_lord_cultures_and_mounts.py` | Fix lord cultures + add mounts to battle equipment templates | `--dry-run`, `--apply` |
| `apply_culture_skills_traits.py` | **Lord SkillSet generator** — source of truth for `taom_lord_skill_sets.xml` (74 archetypes incl. per-culture balance variants, canonical overrides, `archetype_alias`). Pre-flight: regen on a clean tree must diff empty before any `--apply`. See `docs/ai-includes/lord-skills-authoring.md`. | `--skillsets-only`, `--culture <key>`, `--all-cultures`, `--apply` |
| `repoint_evil_lord_skillsets.py` | Balance-pass repoint/parity (#322/#323/#326): culture-scoped skill_template swaps onto variant sets + full inline-`<skills>` sync from the sets XML + `TEMPLATE_ASSIGN` for template-less adults. Safe alternative to per-culture generator re-resolution on drifted cultures. | `--dry-run` (default), `--apply` |
| `author_elf_lords.py` | One-off lord/clan authoring reference (#324): complete NPCCharacter+Hero+Faction wiring for 10 new elf lords + 2 Lothlórien clans; copy this pattern for future lord expansions (parties/clan is tier-capped). | `--dry-run` (default), `--apply` |

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

Four scripts that together produce, translate, validate, and inject loc XMLs across all 12 supported languages × 3 modules (TAOM + TAOM_Map + LOTRLOME_Armory). Per-language full coverage is **~12,000 strings** across 28 files (8 TAOM + 1 Map + 19 Armory).

| Script | Purpose | Output |
|--------|---------|--------|
| `generate_translation_template.py` | Generate English templates for a target language across the 8 TAOM source XMLs | `Main/_Module/ModuleData/Languages/<LANG>/std_taom_*.xml` |
| `translate_with_claude.py` | AI first-draft translation via Claude API (Sonnet 4.5). 4-tier fallback: override → cache → LLM → English. Translates TAOM + TAOM_Map + LOTRLOME_Armory. | All 28 language XMLs for `<LANG>` |
| `rebuild_translation_files.py` | Inject cached translations into XML files from scratch (rebuilds the language file structure using English source + overrides + cache). Use after API runs to apply translations cleanly. | All 28 language XMLs for `<LANG>` (or `--all` for every language) |
| `translation_status.sh` | One-shot status dashboard: per-language cache size + last batch line + running process count. | (stdout) |

**Source XMLs** (the engine's English fallback + translator's discoverable list):
- `Main/_Module/ModuleData/taom_module_strings.xml` (~2,104 — faction names, UI labels)
- `Main/_Module/ModuleData/taom_wanderer_strings.xml` (~1,337 — wanderer backstories)
- `Main/_Module/ModuleData/named_companions/named_companion_strings.xml` (~126 — Aragorn etc.)
- `Main/_Module/ModuleData/taom_cc_strings.xml` (~772 — CC narratives)
- `Main/_Module/ModuleData/taom_career_strings.xml` (~2,050 — career names + tooltips)
- `Main/_Module/ModuleData/taom_messenger_strings.xml` (~29 — Messenger UI)
- `Main/_Module/ModuleData/taom_lotr_issue_strings.xml` (~308 — LOTR custom-issue titles, descriptions, giver dialog, objectives)
- `Main/_Module/ModuleData/taom_xslt_strings.xml` (~1,431 — kingdom/culture/clan/lord/hero descriptions extracted from XSLT)

**External-module source XMLs** (not in repo, in game install — already include English text with inline `{=KEY}`):
- `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` (~1,102 settlement names)
- `<game>/Modules/LOTRLOME_Armory/ModuleData/Languages/loc_*.xml` (~2,782 equipment names across 19 files)

**Configuration:**
- Overrides (hand-curated canonical translations, e.g. Tolkien proper nouns): `tools/translation_overrides/<lang>.json` — git-tracked, edit freely
- Cache (machine-written API results): `tools/translation_cache/<lang>.json` — git-tracked, ~700KB-1.3MB per language

**Setup:** Set `ANTHROPIC_API_KEY` env var. Estimated cost ~$3-10 per language for a full first pass (~12,000 strings). Cache makes re-runs effectively free.

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
