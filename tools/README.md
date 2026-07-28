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
| `validate_all_troop_refs.py` | Per-task: `sk_*/ar_*/clo_urukscout_*/urukscout_*` refs across all 7 culture troop XMLs vs LOTRLOME_Armory — the "underwear bug" gate (superseded by `validate_moduledata.py`'s `BROKEN_ITEM_REF`; kept for now). | (none) |
| `validate_gondor_refs.py` | Legacy Gondor-only predecessor of the above (`sk_gd_*` in `troops_gondor.xml`). Superseded — prefer `validate_moduledata.py`. | (none) |
| `audit_item_refs.py` | Per-task: every `Item.X` ref vs the multi-module item registry (superseded by `validate_moduledata.py`'s `BROKEN_ITEM_REF`). | `--show-locations`, `--limit` |
| `validate_mesh_refs.py` | **Mesh/collision-body ref validator** (read-only, pure stdlib). Extracts every `mesh`/`body_name`/`shield_body_name`/`holster_mesh`/`holster_mesh_with_weapon`/`flying_mesh` ref from item + crafting-piece XML and checks existence across 3 tiers: A) rgl_log content warnings (authoritative), B) `.tpac` Metamesh TOC (visual meshes), C) `.tpac` PhysicsShape TOC (collision bodies, exact; coarse byte-scan only as a fallback for unparsable packs). **A missing `bo_` body is a CONFIRMED cause of infinite mission-load hangs** (#352) — `PreloadHelper.WaitForMeshesToBeLoaded` spins forever on a name that never resolves. Run it after any weapon/armor/crafting authoring. A clean PASS only means "clean within `--items` scope" — scoping too narrow is what let #352 ship. Owns body validation (don't add body checks to `Audit-MeshRefs.ps1`). See `docs/features/mesh-ref-validation.md`. | `--scan-bodies`, `--rgl-log`, `--no-rgl-log`, `--no-tier-b`, `--items`, `--game`, `--tpac-modules`, `--json`, `--code`, `--warnings-as-errors` |

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
| `generate_gondor_armor.py` | Phase-1 Gondor armor item author (Anorien/MT/Osgiliath/Cair Andros/Ithilien) — writes to `lotraom-assets` | LOTRLOME_Armory armor XMLs (`--dry-run`, `--apply`, `--armory-path`) |
| `generate_gondor_armor_phase2.py` | Phase-2 author for 8 missing Gondor families (Lossarnach/PG/Har/Anf/Sere/Leb/Bel/Lam) — defaults to Steam install (#99) | LOTRLOME_Armory (`--dry-run`, `--apply`, `--armory-path`) |
| `generate_mordor_armor.py` | Mordor armor author (#211): generic orc pool `sk_gn_orc_*` (9 helmet shapes) + `sk_md_orc_*` paint sets + KEYforce Morannon sub-line `sk_md_mor_*` (92 items). All helmets emit `hair_cover_type="all"` + `beard_cover_type="all"`; all bracers explicitly `covers_hands="false"`. | (`--dry-run`, `--apply`, `--armory-path`) |
| `generate_isengard_armor.py` | Isengard `sk_is_orc_*` paint helmets + `clo_urukscout_*` cloth overlays (#211) | (`--dry-run`, `--apply`, `--armory-path`) |
| `generate_dolguldur_armor.py` | Dol Guldur `sk_dg_orc_*` paint helmets (Brt+Vgd excluded per spec; #211) | (`--dry-run`, `--apply`, `--armory-path`) |
| `generate_erebor_armor.py` | Erebor Iron Hills `sk_dwarf_iron_*` author — parses spec at runtime; **defaults to `iron_hills/` folder, NOT `erebor/`** (the canonical-folder rule; #211) | (`--dry-run`, `--apply`, `--armory-path`) |
| `generate_rhun_armor.py` | Rhûn final Loke-Rim elite helmets — closes the 22-item gap (#211) | (`--dry-run`, `--apply`, `--armory-path`) |
| `generate_starter_armor.py` | Author low-stat career-archetype starter armor (chest+legs × 3 archetypes) for the 12 non-Gondor career cultures by cloning each culture's own items (mesh/cover flags borrowed), slot stat re-set to anchors Ranged ~5 / Cavalry ~7 / Infantry ~9; no `value=` → trivial computed resale. Gondor excluded (hand-tuned). See `docs/features/starting-equipment-tuning.md`. | (`--apply`, `--armory-path`) |
| `wire_career_starter_armor.py` | Rewire `taom_career_starting_equipment.xml` so every career roster sets Body+Leg from the matching `starter_*` items and CLEARS Head/Cape/Gloves, keeping weapons + mounts. Idempotent. | (`--apply`) |
| `generate_char_creation_equipment.py` | Generate character creation equipment rosters for 10 custom cultures | `Main/_Module/ModuleData/taom_char_creation_equipment.xml` |
| `generate_xslt.py` | Generate `spcultures.xslt` from LOTRAOM reference data | `Main/_Module/ModuleData/spcultures.xslt` |
| `generate_batch2_wanderers.py` | Generate wanderers for 8 kingdoms lacking LOTRAOM data | taom_wanderers*.xml files |
| `extract_wanderers.py` | Convert LOTRAOM wanderer data into TAOM format | 4 taom_wanderer_*.xml files |
| `add_townsfolk_battle_rosters.py` | Append a plain battle `<EquipmentRoster>` (mirroring each civilian one) to every civilian-only `<NPCCharacter>` so townsfolk/notables aren't naked as arena-stand spectators (arena spawns them with battle equipment; #295). Idempotent (skips NPCs that already have a battle roster), BOM/CRLF-preserving. `--apply` writes (default dry-run); `--glob` overrides the default `npcs_*.xml` scope. | `Main/_Module/ModuleData/characters/npcs_*.xml` |

## Rebalancing

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `rebalance_troops.py` | Uniform per-(level,group) baseline + per-culture `CULTURAL_MODS` curve for all troop skills (16 cultures incl. `goblin`/`mistymountainorcs`/`dale`). **Weapon spec is equipment-driven (#340/#341):** Bow↔Crossbow swap + Polearm↔TwoHanded sanity swap follow the troop's actual weapon classes (via `taom_schema.build_item_class_registry`; the writer hard-fails without the game install). `detect_culture` routes id-based elite sub-lines to their own modifier (`iron_hills_*`→`iron_hills`, `mordor_uruk_*`→`mordor_uruk`, `orthanc_*`→`isengard_orthanc`; `rhun_new`→`rhun`); `SKIP_TROOP_IDS` excludes `cave_troll` + `harad_elephant_rider` + `harad_mumakil_rider`; militia take the L21 baseline BY DESIGN (tough for siege/village defense). `--dry-run` always reports the 5 hand-tuned `gondor_loss_noble*` residuals — don't `--apply` over them (#343). See `docs/features/troop-skill-balance.md`. | `--dry-run`, `--apply`, `--game-modules <path>` |
| `analyze_troop_balance.py` | **Read-only** per-culture troop balance overview (imports `rebalance_troops.py`'s curve verbatim — run BEFORE any rebaseline; equipment-aware like the writer, degrades to name-only detection with a warning when the game install is missing). HTML+md+JSON to `tools/reports/troop-balance/`: heatmap parity matrix, outliers, upgrade/weight cross-refs, level-monotonicity check (no lower level stronger than a higher), militia-excluded. | `--outlier-threshold N`, `--stdout` |
| `rebalance_armor.py` | Baseline + cultural modifier formula for all armor items | `--dry-run`, `--apply`, `--export-csv` |
| `rebalance_weapons.py` | Points-based weapon damage with per-culture multipliers | `--dry-run`, `--apply`, `--export-csv` |
| `rebalance_lords.py` | Baseline + cultural modifier + age scaling for all lords (XSLT + XML). Its `CULTURE_MAP` is the culture-attr→TAOM map other tools import — **battania=khand** (NOT mirkwood; fixed 12b06e47 after Variag lords wore elven mods). | `--dry-run`, `--apply`, `--export-csv`, `--skills-only` |
| `raise_party_template_maxes.py` | Set `max_value="50"` on every stack of the 8 bandit raider/boss templates + all `kingdom_hero_party_*` templates in `taom_partyTemplates.xml` (boss 1/1 hero stacks kept; never lowers; idempotent; #315). | `--dry-run` (default), `--apply` |
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
| `apply_culture_skills_traits.py` | **Lord SkillSet generator** — source of truth for `taom_lord_skill_sets.xml` (**never hand-edit the XML**; 74 archetypes incl. per-culture balance variants, canonical overrides, `archetype_alias` ×6 cultures). Pre-flight: regen on a clean tree must diff empty before any `--apply` (the 1f7a7a9a drift lesson); per-culture `--culture X --apply` re-resolution is UNSAFE on drifted cultures — use `repoint_evil_lord_skillsets.py` instead. See `docs/ai-includes/lord-skills-authoring.md`. | `--skillsets-only`, `--culture <key>`, `--all-cultures`, `--apply` |
| `repoint_evil_lord_skillsets.py` | Balance-pass repoint/parity (#322/#323/#326): culture-scoped skill_template swaps onto variant sets + full inline-`<skills>` sync from the sets XML + `TEMPLATE_ASSIGN` for template-less adults. Post-condition + idempotency checked. Safe alternative to per-culture generator re-resolution on drifted cultures. | `--dry-run` (default), `--apply` |
| `author_elf_lords.py` | One-off lord/clan authoring reference (#324): complete NPCCharacter+Hero+Faction wiring for 10 new elf lords + 2 Lothlórien clans, inline skills from live SkillSets, donor face keys; copy this pattern for future lord expansions. Parties/clan is tier-capped: <3→1, 3-4→2, 5+→3. | `--dry-run` (default), `--apply` |
| `extract_perks.py` | Parse the decompiled `DefaultPerks.cs` into a perk catalog: 374 perks × 18 skills × 12 tiers (levels 25→300), each pair with role + numeric bonus + `{VALUE}`-rendered effect. Output `tools/data/bannerlord_perks.json` (committed) + `tools/reports/lord-balance/perks.html`. **Re-run after an engine bump.** See `docs/features/lord-perk-review.md`. | `--defaultperks <path>`, `--stdout` |
| `analyze_lord_balance.py` | **Read-only** per-culture lord stats + perk review (lord analog of `analyze_troop_balance.py`). Resolves authoritative skills via `skill_template`→`taom_lord_skill_sets.xml` (**the engine ignores inline `<skills>`**); one HTML per culture + `index.html` — 18-skill per-lord table, every unlocked perk (deduped by SkillSet), data-quality (unresolved templates, inline/SkillSet drift). See `docs/features/lord-perk-review.md`. | `--stdout`, `--culture <name>` |

## Cleanup (One-Shot)

| Script | Purpose |
|--------|---------|
| `cleanup_deleted_gondor_armor.py` | Remove orphaned Gondor armor entries whose FBX sources were deleted | `--dry-run`, `--apply` |
| `cleanup_deleted_gondor_items.py` | Remove deleted Gondor item definitions from LOTRLOME_Armory (no args) |
| `rollback_erebor_iron_misfile.py` | One-off: remove mis-filed `sk_dwarf_iron_*` items from `erebor/` (used once during the #211 deep-review RCA) | `--dry-run`, `--apply` |

## Troop revamps (#212 + polish #224 — completed one-offs, kept for the mechanical-swap pattern)

All are EquipmentRoster swappers over the culture troop XMLs; `--dry-run` (default) / `--apply`.

| Script | Purpose |
|--------|---------|
| `apply_gondor_troop_revamp.py` | Mechanical EquipmentRoster swap for 107 Gondor troops + delete orphan blocks (#99) |
| `apply_mordor_troop_revamp.py` | Swap + 21 new orc/Nurn Warg/Black Uruk troops + 14 deletes (#212) |
| `apply_isengard_troop_revamp.py` | Swap + 13 new `isengard_orc_*` troops; `orthanc_*` line preserved (#212) |
| `apply_dolguldur_troop_revamp.py` | Swap (17 refits) + 12 deletes (old Khamûl stubs + berserker line); flexible indent regex (#212) |
| `apply_gundabad_troop_revamp.py` | Swap + 1 new `gundabad_bolgs_ironfang` T8 + 4 deletes (#212) |
| `apply_erebor_troop_revamp.py` | Swap (41 refits) + 13 new `iron_hills_noble_*` troops; 0 deletes (#212) |
| `cleanup_deleted_troops_212.py` | Sweep deleted-troop refs from `taom_partyTemplates.xml`, `troop_weights.xml`, `troop_resource_costs.xml` (#212) |
| `expand_party_templates_212.py` | Insert new troops into `kingdom_hero_party_<culture>_template` blocks via positional splice (#212) |
| `apply_gondor_polish_224.py` | **Delta-style** Gondor polish: per-slot `set`/`clear`/`replace` ops + 2 new PG cavalry NPCs + upgrade-target patch (#224 — distinct from the full-roster swap pattern) |

## One-offs — `tools/oneoff/` — UE→Bannerlord asset pipeline (2026-07-15/16)

The Rivendell (ElvenForestCity UE 5.1 kit) + Tents (Fab) conversion pipeline. Full workflow,
format findings, and gotchas: [`docs/reference/ue-to-bannerlord-asset-pipeline.md`](../docs/reference/ue-to-bannerlord-asset-pipeline.md);
review RCA: `docs/reviews/rca-asset-pipeline-tools-2026-07-16.md`.

| Script | Purpose |
|--------|---------|
| `ue_export_rivendell.py` / `ue_export_rivendell_fixup.py` | UE **editor Python** (headless `UnrealEditor-Cmd -run=pythonscript -EnablePlugins=PythonScriptPlugin`): bulk StaticMesh→FBX (UCX riding along, LOD0 only), Texture2D→TGA (fixup retries failures as PNG/EXR/BMP), + `material_bindings.json` (mesh→material-instance→texture/scalar params, parent-chain walk). Read-only on the source project. |
| `blender_normalize_rivendell.py` | Headless Blender batch (MS-Store app → invoke `blender-launcher.exe`, DETACHES: completion = `_normalize_report/<run>/DONE.txt`). Modes: `mesh` (per-asset: bake, lowercase `sm_<kit>_*`, `bo_` twin + physics material, decimate cap, shardable `--shard i/n`), `building` (join level → one mesh), `citysplit` (grid-cluster city → per-structure chunks + layout JSON), `level` (merged reference). `--kit`/`--physmat`/`--material-map`. |
| `blender_dump_level_placements.py` / `blender_reconstruct_buildings.py` | Match UE level instances back to modular kit meshes (squashed-key matcher, 92% on the house level) → placements JSON / rebuild a building by stamping modular meshes + their `bo_` twins at recorded transforms. Rivendell-hardcoded (`KIT`); assembled-scene direction currently parked. |
| `analyze_rivendell_bindings.py` / `build_rivendell_material_sheet.py` | Bindings JSON → master-material taxonomy (+ per-mesh CSV) / → `material_rename_map.json` + `material_sheet.csv` (final material = texture-set stem incl. `t_` prefix — user decision; same-set instances merged, `_foliage`/`_translucent` family suffixes). |
| `generate_rivendell_materials.py` | Writes `*_mtl.tpac` material files by cloning a hand-made template (tpac v2; three 16-byte texture-GUID slots; **checksum unvalidated by the editor**). `--force` only overwrites names in `_generated_manifest.json` — hand-made materials are code-protected. |
| `convert_rivendell_textures.py` / `convert_tent_textures.py` | Metal-rough → Bannerlord spec-gloss `t_*_{d,n,s}[,h]` (packed ORM / separate Substance maps; `_s` = R:metal G:gloss B:AO, empirical vs shipped kits; constant-high-metal guard on untrusted ChannelMaps). |
| `blender_prep_tripo_prop.py` | Headless Blender prep for Tripo AI props (throne pilot 2026-07-25, renamed+generalized for the Gondor ships 2026-07-28): scale by height or hull length (length axis auto-rotated to +X), `--decimate-tris` for multi-million-tri sources (bake source keeps full res → high-to-low bake, scale-aware cage), **xatlas-style chart re-UV** (region-growing + fragment merge; Smart UV Project fragments dense organic meshes — probe data in the docstring), selected-to-active rebake + fresh AO, `bo_` twin, Cycles preview render. `--probe-angles`/`--probe-spreads` measure UV candidates without baking. |
| `convert_tripo_prop_textures.py` | Single-set d/n/s packer for separate PBR maps (bake staging or Substance export — the Substance round-trip converter; workflow in the docstring). Same `_s` packing as the kit converters. |
| `blender_dump_fbx_inventory.py` | Headless object-level FBX dump → JSON (names, world transforms, bboxes, tri counts, materials, LOD/bo_ structure). Used to reverse-engineer kit-FBX composition (e.g. the Minas Tirith wall template) before assembling new pieces. |
| `blender_assemble_lond_cirion_wall.py` | Assembles Lond Cirion wall section 01 (symmetric L, 6 walls + 5 towers, 45° corner tower) from the Gondor castle-kit pieces into the MT-template kit format (base/.lod3/.lod6/bo_ per section, origin pivot). Layout table + interface math in the docstring — copy the pattern for further sections. |

## One-offs — `tools/oneoff/`

**Convention (2026-07-12): one-off scripts land in `tools/oneoff/`** — finished migration/authoring
scripts kept for reference (rerunning one is the exception, not the design). The top-level `tools/`
dir holds only living, recurring tools. The initial sweep moved 33 scripts referenced by no living
doc (per-culture clan/lord authors, `fix_v1_4_5_item_ids.py`-class migration fixes, kitbash test
builders, dao-rock scene one-offs, `md_to_html.py`) plus the 11 legacy lords-migration scripts at
`tools/oneoff/lords-migration/`. When a new script finishes its one job, `git mv` it here in the
same session; when authoring a script you expect to rerun, keep it in `tools/` and add a README row.

## Review analytics

| Script | Purpose | CLI Flags |
|--------|---------|-----------|
| `analyze_reviews.py` | Parse `docs/reviews/REVIEW-LOG.md` tables → per-prompt-version accuracy + cumulative bug chart at `docs/reviews/progress.png` (karpathy/autoresearch port) | `--summary`, `--no-plot` |
| `spider_render_triage.py` | One-command crash triage: auto-finds latest `taom_debug` + `rgl_log` + crash dump, prints a VERDICT (stdlib, read-only, fail-soft) | (no flags) |

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
