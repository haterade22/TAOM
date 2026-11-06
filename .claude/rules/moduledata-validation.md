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

It is one read-only pass that consolidates the old per-task validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, the `equipmentType` PowerShell snippet, the duplicate-id-across-Armory-folder checks) and catches the recurring data-integrity bug classes:

A script that writes XML, or that reads what a troop carries, follows [data-scripts.md](data-scripts.md): the XML
I/O convention (idioms A and B), backups, dry run, parse before write. Older tool comments cite this file for it.

## Three modules, not one

Two of the three data modules, `TAOM_Map` and `LOTRLOME_Armory`, live in the game install and are not in git. The
commit hook fires only on staged repo files, so run `python tools/validate_moduledata.py` by hand after any edit
there. Any XSLT: `python tools/check_external_xslt.py`. Coverage: [validator codes](../../docs/reference/moduledata-validator-codes.md), "What actually gets scanned".

## Gate per file kind

Run `python tools/data_gates.py` before committing. It runs the gates for the files git shows as changed,
plus any live Armory or `TAOM_Map` file you name on the command line (git cannot see those), and prints
PASS, FAIL or NOT RUN for each with the doc to read first. `--all` runs every gate; `--list` prints the
kind table; `--with-tests` adds the dotnet rows. A gate that cannot run (no install, no lxml, SKIPPED, a
dotnet row without `--with-tests`) has not passed. The table is `GATES` in
`tools/data_gates.py`: register a new gate there once.

## GUI prefab gates (`Main/_Module/GUI`)

These tests gate TAOM's Gauntlet prefabs and in-code PrefabExtensions. The ones marked (install) read the
installed game through `BANNERLORD_GAME_DIR` and go Inconclusive without it.

| Test | Fails on |
|---|---|
| `PrefabElementTypeBindingTests` (install) | an element that names neither a widget class nor a prefab |
| `PrefabExtensionBindingTests` (install) | a `[PrefabExtension]` movie that is missing or differs in case, or an XPath that matches nothing in the winning prefab |
| `PrefabInsertPositionTests` | a Child insert with no Index or not on `/Children`; an Append or Prepend on `/Children` |
| `PrefabHintParentGateTests` | an in-code `HintWidget` whose parent sets `DoNotAcceptEvents="true"` |
| `PrefabCloneWidgetReferenceTests` (install) | a TAOM copy of a vanilla prefab that drops a Widget-typed reference or a `DataSource` the vanilla file declares (Id-paired elements); a TAOM prefab identical to the vanilla file it shadows |
| `PartyTupleVanillaParityTests` (install) | a TAOM copy of a party tuple prefab that differs from vanilla beyond the two 1.5.x edits |
| `PrefabExtensionBrushTests`, `PrefabBrushPropertyTests` | a brush no module in TAOM's set declares; a `Brush.<Property>` that is not a `Brush` property |
| `PrefabAttributeNameBindingTests` (install) | an attribute the engine cannot set, outside a vanilla-parity allowlist; a `VisualDefinition` or `VisualState` attribute the engine never reads |
| `GauntletStretchInCoverRatchetTests` | a new `StretchToParent` child of a `CoverChildren` parent with no max size (grid parents exempt); three older files are counted and may only fall |

## Discipline

- **Schemas are the source of truth.** Fields, enums and cross-ref targets live in `tools/schemas/*.json`; never
  hardcode them in Python.
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
