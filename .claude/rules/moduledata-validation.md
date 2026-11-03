---
paths:
  - "**/troops/troops_*.xml"
  - "**/characters/*.xml"
  - "**/equipmentsets/*.xml"
  - "**/taom_spcultures.xml"
  - "**/taom_partyTemplates.xml"
  - "**/named_companions/*.xml"
  - "**/taom_wanderers.xml"
  - "**/taom_education_character_templates.xml"
  - "tools/schemas/*.json"
  - "tools/validate_moduledata.py"
  - "tools/taom_schema.py"
---

# Validate ModuleData cross-references before committing

When you add, edit, or restructure **troops, characters, lords, cultures, equipment rosters, party templates, or the validator schemas**, run the schema-driven validator before committing:

```bash
python tools/validate_moduledata.py          # full report (errors + warnings)
python tools/validate_moduledata.py --json report.json --warnings-as-errors
```

For *targeted* checks mid-task (rather than a full run), the `taom-moduledata` MCP server exposes the same engine as tools — `mcp__taom-moduledata__item_exists`, `troop_exists`, `culture_exists`, `find_references`, `validate_moduledata`, `list_cultures` (it must be loaded; restart Claude after enabling it — see the feature doc).

It is one read-only pass that consolidates the old per-task validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, the `equipmentType` PowerShell snippet, the duplicate-id-across-Armory-folder checks) and catches the recurring data-integrity bug classes. Every code, what it catches and how to repair it: [validator codes](../../docs/reference/moduledata-validator-codes.md).

A script that writes XML, or that reads what a troop carries, follows [data-scripts.md](data-scripts.md): the XML
I/O convention (idioms A and B), backups, dry run, parse before write. Older tool comments cite this file for it.

## Three modules, not one

Two of the three data modules, `TAOM_Map` and `LOTRLOME_Armory`, live in the game install and are not in git. The
commit hook fires only on staged repo files, so run `python tools/validate_moduledata.py` by hand after any edit
there. Any XSLT: `python tools/check_external_xslt.py`. Coverage: [validator codes](../../docs/reference/moduledata-validator-codes.md), "What actually gets scanned".

## Gate per file kind

Run the gates for every kind of file you touched, as an author before committing and as the
`/deep-review` XML lens (`.claude/skills/deep-review/lenses/7-xml.md`) after. One list, so a new
gate is registered here once. A gate that cannot run (no install, no lxml) has not passed.

| Kind | Read first | Run |
|---|---|---|
| Any engine-registered XML, repo or live | `tools/validate_xml_schemas.py` docstring | `python tools/validate_xml_schemas.py <files>` (finds each file's module; add SubModule.xml when it changed; `--live` for whole live modules) |
| troops, characters, equipment rosters, cultures, clans, party templates, lords | this rule, `troops.md`, `xml-data.md` | `python tools/validate_moduledata.py`, reading the warnings as well as the errors; `python -m pytest tools/tests/test_generator_regions_in_sync.py` (a hand edit inside a generator's region fails it: change the generator's table, then `--apply`); for wanderers and `taom_wanderer_strings.xml`, `dotnet test ... --filter FullyQualifiedName~WandererBackstoryCoverageTests` |
| XSLT anywhere | `xslt.md` | `python tools/check_external_xslt.py`; transform and diff against the vanilla file (`xslt.md` file map); `CulturePartyTemplateTests` for `spcultures.xslt` |
| Armory items, crafting pieces, weapon descriptions | `docs/reference/armory-guide.md` | `python tools/audit_armory_refs.py`; `python tools/audit_polearm_shield_parity.py` for weapons and pieces; `python tools/generate_armour_classes.py --check` for armour (re-run with `--apply` when stale); `python tools/register_armory_names.py --check` for names with no English row |
| action sets, monsters, monster usage | `armory-guide.md` "action_sets structure" | `python tools/audit_action_set_parity.py`; `python tools/audit_mount_parity.py` for creature mounts (report-only: it always exits 0, so read the output); `python tools/wire_hill_troll_race.py --check` and the `TrollBruteForceWiringTests` / `TrollHitPointsLiveDataTests` filter for the troll sets and Monsters; `python tools/bind_orc_goblin_locomotion.py --check` for the orc and goblin sets and the orc gait variants (a reinstall drops their rows and parity still passes) |
| the hill troll and dwarf rig FBX in the live Armory `AssetSources` (the gate's `SPEC`: head, eye, mouth, hands or arms); a new race rig is unchecked until its meshes are added to `SPEC` | the tool's docstring | `python tools/check_race_morph_channels.py` (exact counts: head, eye and mouth 101, hands or arms 26; SKIPPED has not passed) |
| `TAOM_Map` settlements | `docs/reference/taom-map-settlement-naming.md` | `python tools/validate_moduledata.py` (`LANDLESS_CULTURE`, `FORTIFICATION_WITHOUT_VILLAGE`, `SETTLEMENT_ASSIGNMENT_DRIFT`; pin a deliberate owner or culture change in `tools/settlement_assignments.json`); `python tools/add_map_villages.py --check`; `python tools/add_map_fortifications.py --check` (a town or castle rename goes into its `FORTS` row too); `python tools/oneoff/write_village_descriptions.py --check` (#1042 descriptions and orphan rows, not the names; exit 1 after a reinstall of an older package) |
| banner icons, banner keys, banner sheets and sprites | `docs/reference/banner-icon-generation.md` | `python tools/pack_banner_sheets.py --audit` (cells, sprites, Kit packages and their source hash, unused or mergeable sheets); `python tools/check_rdc_entries.py --module "<install>/Modules/TAOM" --under BannerIcons --types tex` after a Kit import |
| GUI prefabs, PrefabExtension XML | `gui-ui.md` | sprite ids against `TAOMSpriteData.xml` (manual); every `Command.*` handler resolves to a TAOM method with the right parameter count (manual); `PrefabExtensionBrushTests`; `EveryPrefabAttribute_NamesASettableWidgetProperty` (binding gate); `PrefabElementTypeBindingTests`; `GauntletStretchInCoverRatchetTests`; for a TAOM copy of a vanilla prefab, `PrefabCloneWidgetReferenceTests` |
| language files, `{=KEY}` text | `docs/reference/localization-map.md` | `python tools/check_external_loc_coverage.py`; `dotnet test ... --filter FullyQualifiedName~Infrastructure.Localization` (`LanguageDataXmlTests`, `LocalizationKeyConsistencyTests`: one text per id and per shared key, inline defaults equal the registration; `LanguageTextIntegrityTests`: no wrong-script word in the rows or the translation cache); `python tools/register_armory_names.py --check` for Armory item and piece names |
| `project.mbproj` | the tool's docstring | `python tools/audit_mbproj_registration.py` |
| voice definitions, module sounds (`voice_definitions.xml`, `module_sounds.xml`) | `tools/audit_voice_clip_lengths.py` docstring | `python tools/audit_voice_clip_lengths.py` (only the module sounds a voice binds: clip paths, voice types, sound categories and clip lengths per slot; for the rest of `module_sounds.xml`, only duplicate names) |
| scenes, prefabs | `docs/modding/module-map.md` (prefab entity cap) | `python tools/check_prefab_budget.py` (counts `TAOM_Map` only; sum all modules) |
| XML a `tools/` generator wrote | `tools/README.md` "XML I/O convention" | the generator's own `--verify` or `--check`; `python tools/check_generator_item_refs.py`; hand-read three generated entries |

## Discipline

- **Schemas are the source of truth.** Fields, enums and cross-ref targets live in `tools/schemas/*.json`; never
  hardcode them in Python. Allowlists with a stated reason per entry stay in `taom_schema.py`.
- **A new file that defines `<NPCCharacter>`:** add its path to `taom_npccharacter.json` `applies_to`, or its
  duplicate-id and enum checks silently do not run.
- **A new error code:** add its `--code` line to `check-moduledata-validation.sh`. `CommitGateCoverageTests` fails
  until you do.
- **Warnings:** the commit hook runs the errors only, so run the tool yourself to see them.
- **PASS is not loaded in game.** New or edited item and equipment XML is not done until a full game restart and an
  in-game visual check: the engine registers those folders at launch.
- **Data applied at runtime** (career rosters, for example) is invisible to the sweep, because no `NPCCharacter`
  names it. Cover it with a shipped-data test.

The reasons and the history: [validator codes](../../docs/reference/moduledata-validator-codes.md), "Discipline".
