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
| `LANDLESS_CULTURE` (error) | a culture on a `Lord`-occupation `NPCCharacter`, `<Faction>` or `<Kingdom>` that owns **no settlement** — vanilla `SpawnLordParty` ends with an unguarded `Settlement.All.First(x => x.Culture == hero.Culture)`, so its lords CTD the daily clan tick (#374). Known-landless cultures live in `_LANDLESS_BY_DESIGN` with a stated reason |
| `MOUNTED_DWARF` (error) | a `race="dwarf"` `NPCCharacter` tagged `default_group="Cavalry"`/`"HorseArcher"`, or able to reach a `slot="Horse"` item (own inline roster, or a standalone roster it names). The dwarf skeleton's rider bone is misaligned, so a mounted dwarf spawns inside the horse mesh — the same invariant `Patch46_TournamentDwarfDismount` enforces at runtime. **Both halves are needed:** `CharacterObject.GetFormationClass()` ignores `default_group` when `IsHero` and reads `BattleEquipment` instead, so an enum-only check passes a lord who still spawns mounted |
| `DUPLICATE_{NPC,CULTURE,ROSTER}_ID` (error) | the same id defined twice |
| `DUPLICATE_ITEM_DEF` (warn) | an Armory item id defined in >1 `LOTRLOME_items` folder (engine silently shadows one) |
| `MISSING_CIVILIAN_TYPE` (warn) | a civilian roster whose `<EquipmentSet>` lacks `equipmentType="Civilian"` (Faramir/Boromir wrong-outfit) |
| `INVALID_ENUM` (warn) | `default_group` not Infantry/Ranged/Cavalry/HorseArcher |
| `BROKEN_PARTY_TEMPLATE_REF` (warn) | a `PartyTemplate.X` ref to an undefined template |

## Discipline

- **Schemas are the source of truth.** Field types, enums, cross-ref targets, and the civilian rule live in `tools/schemas/*.json` — add new fields/enums there, never hardcode them in Python.
- **When you add a NEW file that defines `<NPCCharacter>`** (a new wanderer/companion/template file), add its path to `taom_npccharacter.json` `applies_to`, or its duplicate-id + enum checks silently won't run (Codex review 2026-05-30 found 3 such files uncovered).
- A `PreToolUse` hook (`check-moduledata-validation.sh`) auto-runs the **error**-severity checks on every Claude-driven commit that stages ModuleData XML and blocks on failure — but it does NOT surface warnings, so run the tool yourself to see `MISSING_CIVILIAN_TYPE` / `INVALID_ENUM` / `DUPLICATE_ITEM_DEF` / `BROKEN_PARTY_TEMPLATE_REF`.
- **The validator does NOT enforce engine XSD required-attributes**, and `characters/clans.xml` has **no schema at all** (`tools/schemas/` covers only equipmentsets / npccharacter / spcultures). A `<Faction>` (clan) missing a `Factions.xsd`-required attribute such as `initial_home_settlement` passes both this tool and the commit hook, surfacing only when the engine/editor loads the file. When editing `clans.xml`, verify in the Bannerlord editor (or an engine load) too — a clean `validate_moduledata.py` is necessary but not sufficient for clan data. (RCA: `clan_umbar_3` shipped without its home settlement, fixed 2026-06-22. See [docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md) "Coverage boundary".) **That hole is now half of a known CTD, so treat it as load-bearing rather than cosmetic:** a faction with a null `InitialHomeSettlement` is exactly the first of the two faults behind #374, and the second — a lord whose culture owns no settlement — is now gated by `LANDLESS_CULTURE` above. Either alone is harmless; together they throw `InvalidOperationException` out of `Campaign.Tick`. Patch65 repairs the faction at runtime, but a clan shipped without `initial_home_settlement` is still a data defect this tool cannot see. (See [docs/features/lord-spawn-guard.md](../../docs/features/lord-spawn-guard.md).) The same hole is wider for **armory `action_sets.xml` structure**, which neither the validator nor the hook covers at all: the hook fires only on `Main/_Module/ModuleData/*.xml` (`check-moduledata-validation.sh:65`), the live file lives in the game install, and the only copy in this repo — `docs/reference/lotrlome-armory-snapshot/action_sets.xml` — is re-snapshotted with no structural check. Gate it with `python tools/audit_action_set_parity.py` (defaults to the live install; pass `--live <path>` to audit the tracked snapshot), which exits non-zero on any root-level `<action>` — the game client loads such a file silently while the dedicated-server engine throws `KeyNotFoundException` at `/action_sets/action` and dies on boot, so a clean single-player session proves nothing. (2026-08-03: 168 orphaned elements from twelve self-closing tavern sets. See [docs/reference/armory-guide.md](../../docs/reference/armory-guide.md) "action_sets structure".)
- **PASS ≠ in-game loaded (the new-file / restart blind spot).** The validator parses the XML files off disk in the Python process — it proves refs *resolve on disk now*, NOT that the running/last-launched engine loaded them. Bannerlord registers each `<XmlName id="Items" path="LOTRLOME_items/<culture>">` **directory** at process launch and globs it (`DirectoryInfo.GetFiles("*.xml")`) at campaign start, with no hot-reload (decompile-verified: `Module.cs:246→1032`; `Campaign.cs:1471 LoadXML("Items")` → `MBObjectManager.cs:894/900/901/903`). So a **NEW** item/equipment XML file added after launch is null in-engine — the character spawns **naked** (the "underwear bug") — even though this validator, the build, and unit tests all pass (none start a campaign). **Any change that adds or edits item/equipment XML is not "done" until a full game RESTART + an in-game visual check** (new campaign, spawn/select the affected character, confirm clothed). This covers the `generate_*_armor.py` family, `/new-culture`, `/author-armor`, and any file dropped into a folder-registered `LOTRLOME_items/<culture>/` dir. Corollary: keep backups on a non-`.xml` extension (`.bak-*`) — the glob is `*.xml`, so a `*.xml` backup left in a registered dir gets globbed and injects a duplicate item id. (RCA: the 12 non-Gondor `starter_armors.xml` shipped naked-until-restart 2026-06-30. See [docs/features/starting-equipment-tuning.md](../../docs/features/starting-equipment-tuning.md) + docs/reviews/LESSONS-LEARNED.md "A NEW item XML file only loads at process launch".)

## Why this rule exists

These bug classes recur (the underwear bug, dup ids across Armory folders, dead troop refs after deletions, `rohan`-vs-`vlandia` typos) and each was previously caught — if at all — by a separate hand-run script. The validator makes them catchable in one pass. Full design: [docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md). Sibling data rules: [troops.md](troops.md), [xml-data.md](xml-data.md), [vanilla-data-comparison.md](vanilla-data-comparison.md). Matcher-authoring lesson: `feedback_prefix_ref_matchers_are_attribute_agnostic` (memory).
