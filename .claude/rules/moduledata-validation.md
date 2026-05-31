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
---

# Validate ModuleData cross-references before committing

When you add, edit, or restructure **troops, characters, lords, cultures, equipment rosters, party templates, or the validator schemas**, run the schema-driven validator before committing:

```bash
python tools/validate_moduledata.py          # full report (errors + warnings)
python tools/validate_moduledata.py --json report.json --warnings-as-errors
```

For *targeted* checks mid-task (rather than a full run), the `taom-moduledata` MCP server exposes the same engine as tools — `mcp__taom-moduledata__item_exists`, `troop_exists`, `culture_exists`, `find_references`, `validate_moduledata`, `list_cultures` (it must be loaded; restart Claude after enabling it — see the feature doc).

It is one read-only pass that consolidates the old per-task validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, the `equipmentType` PowerShell snippet, the duplicate-id-across-Armory-folder checks) and catches the recurring data-integrity bug classes:

| Code (severity) | Bug class it catches |
|---|---|
| `BROKEN_ITEM_REF` (error) | a `Item.X` ref to no defined item — the "underwear bug" (troop spawns naked) |
| `BROKEN_TROOP_REF` (error) | an `NPCCharacter.X` ref (upgrade_target, party-stack `troop=`, culture `basic_troop=`) to a deleted troop |
| `UNKNOWN_CULTURE` (error) | a `culture="Culture.X"` that is not a real StringId (e.g. `rohan` instead of `vlandia`, `dale` instead of `sturgia`) |
| `DUPLICATE_{NPC,CULTURE,ROSTER}_ID` (error) | the same id defined twice |
| `DUPLICATE_ITEM_DEF` (warn) | an Armory item id defined in >1 `LOTRLOME_items` folder (engine silently shadows one) |
| `MISSING_CIVILIAN_TYPE` (warn) | a civilian roster whose `<EquipmentSet>` lacks `equipmentType="Civilian"` (Faramir/Boromir wrong-outfit) |
| `INVALID_ENUM` (warn) | `default_group` not Infantry/Ranged/Cavalry/HorseArcher |
| `BROKEN_PARTY_TEMPLATE_REF` (warn) | a `PartyTemplate.X` ref to an undefined template |

## Discipline

- **Schemas are the source of truth.** Field types, enums, cross-ref targets, and the civilian rule live in `tools/schemas/*.json` — add new fields/enums there, never hardcode them in Python.
- **When you add a NEW file that defines `<NPCCharacter>`** (a new wanderer/companion/template file), add its path to `taom_npccharacter.json` `applies_to`, or its duplicate-id + enum checks silently won't run (Codex review 2026-05-30 found 3 such files uncovered).
- A `PreToolUse` hook (`check-moduledata-validation.sh`) auto-runs the **error**-severity checks on every Claude-driven commit that stages ModuleData XML and blocks on failure — but it does NOT surface warnings, so run the tool yourself to see `MISSING_CIVILIAN_TYPE` / `INVALID_ENUM` / `DUPLICATE_ITEM_DEF` / `BROKEN_PARTY_TEMPLATE_REF`.

## Why this rule exists

These bug classes recur (the underwear bug, dup ids across Armory folders, dead troop refs after deletions, `rohan`-vs-`vlandia` typos) and each was previously caught — if at all — by a separate hand-run script. The validator (adopted from `TheOldRealms/TOR_Tools`' schema/validation architecture, MIT) makes them catchable in one pass. Full design: [docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md). Sibling data rules: [troops.md](troops.md), [xml-data.md](xml-data.md), [vanilla-data-comparison.md](vanilla-data-comparison.md). Matcher-authoring lesson: `feedback_prefix_ref_matchers_are_attribute_agnostic` (memory).
