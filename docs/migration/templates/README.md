# v1.4.5 XML Templates — "What right looks like"

> Reference for the canonical 1.4.5 shape of every TAOM-authored XML type, with side-by-side comparison against current TAOM state and a per-file migration recipe.

These docs were extracted from real vanilla XMLs at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\{SandBox,SandBoxCore,StoryMode}\ModuleData\` on 2026-05-22.

## Why these docs exist

Bannerlord v1.4.3 overhauled the equipment template system. Without a reference for the new shape, S5a (mass XML migration) and S5b (missing equipment roster authoring) would be guesswork. These docs are the source of truth — read them before authoring any XML during the migration.

## Documents

| Doc | Covers | Lines |
|---|---|---|
| [characters.md](characters.md) | Lord, hero, wanderer, NPC characters (`<NPCCharacter>` + `<Hero>` elements) | 577 |
| [equipment-rosters.md](equipment-rosters.md) | Equipment rosters + sets (the 1.4.3 critical migration surface) | 701 |
| [troops-and-parties.md](troops-and-parties.md) | Troop characters + party templates + character templates | 706 |

## Cross-cutting findings (all docs agree)

These were surfaced by all three agents and are critical for migration correctness:

### `<EquipmentSet>` vs `<EquipmentRoster>` deprecation scope

- **`<EquipmentSet civilian="true">`** — DEPRECATED in 1.4.3. Use `equipmentType="Civilian"` instead. Zero occurrences in vanilla 1.4.5 (TaleWorlds fully migrated).
- **`<EquipmentRoster civilian="true">`** — STILL VALID. Vanilla 1.4.5 uses this 1,097× in `spnpccharacters.xml` alone. **Do not change inline rosters inside `<NPCCharacter>/<Equipments>` blocks.**
- The migration tool `tools/migrate_equipment_type_1_4_3.py` correctly filters by `<EquipmentSet>` element only.

### `<Flags>` syntax

Single child element with per-flag attributes, NOT multiple `<Flag>` children:

```xml
<Flags IsLordTemplate="true" IsFemaleTemplate="true" />
```

Not:

```xml
<Flag name="IsLordTemplate" />  <!-- WRONG -->
<Flag name="IsFemaleTemplate" /> <!-- WRONG -->
```

### `equipmentType="Battle"` is implicit

Vanilla 1.4.5 battle rosters do NOT add `equipmentType="Battle"`. Only `Civilian` and `Stealth` are explicit. The migration tool should NOT add explicit `Battle` to bare sets — leave them bare.

⚠️ **TAOM tool action needed:** `tools/migrate_equipment_type_1_4_3.py` currently adds explicit `Battle` to bare sets (4,140 changes vs 3,372 raw `civilian="true"` count). This is wrong — should leave bare sets alone. Tool needs revision before `--apply`.

### Deprecated → new flag mapping

| Deprecated flag | 1.4.5 replacement | Notes |
|---|---|---|
| `IsNobleTemplate="true"` | `IsLordTemplate="true"` | 1:1 RENAME — not removed |
| `IsNobleTemplate="false"` | omit or `IsLordTemplate="false"` | Vanilla uses explicit `false` in some places |
| `IsCivilianTemplate="true"` | drop flag; set `equipmentType="Civilian"` on EquipmentSet | |
| `IsCombatantTemplate="true"` | drop flag; omit `equipmentType` (Battle is implicit) | |
| `IsNoncombatantTemplate="true"` | drop flag; set `equipmentType="Civilian"` | Concept = "wears civvies", same as IsCivilianTemplate |
| `IsWandererEquipment="true"` | drop entirely | Wanderer rosters now selected by ID lookup, not flag |
| `IsGentryEquipment="true"` | drop entirely | Concept collapsed |
| `IsRebelHeroEquipment="true"` | drop entirely | Concept collapsed |
| `IsMediumTemplate="true"` | drop entirely | Tier intent now lives in roster ID only |
| `IsHeavyTemplate="true"` | drop entirely | Same |
| `IsFlamboyantTemplate="true"` | drop entirely | |
| `IsStoicTemplate="true"` | drop entirely | |
| `IsNomadTemplate="true"` | drop entirely | |
| `IsWoodlandTemplate="true"` | drop entirely | |

### TAOM file priority (most critical migration targets)

In order of impact:
1. **`taom_child_equipment_templates.xml`** — 160 deprecated flag hits (IsNoncombatantTemplate 60, IsNobleTemplate 60, IsCivilianTemplate 40). Single file, manual review.
2. **13 troop XML files** (`troops_*.xml`) — ~2,017 `<EquipmentSet civilian="true">` occurrences. Mechanical migration.
3. **18 character XML files** (`npcs_*.xml` per-culture + lords.xml + heroes.xml + wanderers + abanissa + dale + khand) — same migration.
4. **15 culture equipment-set files** (`taom_equipment_sets_*.xml`) — additionally need new `<Flags>` elements with proper combinations (IsLordTemplate, IsKingdomRulerTemplate, IsFemaleTemplate, IsChildEquipmentTemplate, IsTeenagerEquipmentTemplate) to be discoverable by the 1.4.3 selection model.
5. **`lords.xslt`** — line 389 has `civilian="true"` passthrough. Update XSLT to emit `equipmentType="Civilian"`.
6. **`taom_wanderer_equipment.xml`** — may have `IsWandererEquipment` flag references. Remove entirely.

### TAOM file `taom_career_starting_equipment.xml` ✅

Already 1.4.5-compatible. No deprecated patterns, no flags needed (selected by ID lookup).

### TAOM file `taom_education_equipment_templates.xml` ✅

Already 1.4.5-compatible. Education templates have no `<Flags>` and no `equipmentType` in vanilla; selected by ID in code.

## TAOM cultures (12, not 10)

Resolved from `Main/_Module/ModuleData/taom_spcultures.xml`:
- erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar, dolguldur, gondor, mordor, shaghana, abanissa

(The earlier "10" figure from `CLAUDE.md` referenced a different counting. The XML is the source of truth.)

## Cross-references

- [v1.4.x-overview.md](../v1.4.x-overview.md) — migration overview
- [v1.4.x-changes.md](../v1.4.x-changes.md) — full TaleWorlds changelog
- [v1.4.x-equipment-overhaul.md](../v1.4.x-equipment-overhaul.md) — equipment system deep dive
- [v1.4.x-taom-impact.md](../v1.4.x-taom-impact.md) — per-surface impact matrix
- [dual-dll-setup.md](../dual-dll-setup.md) — Steam update + DLL backup procedure
- [api-diff-1.3.15-to-1.4.5.md](../api-diff-1.3.15-to-1.4.5.md) — high-risk GameModel signature diff
- [equipment-roster-coverage.csv](../equipment-roster-coverage.csv) — per-culture mandatory roster audit
- [TRACKING.md](../TRACKING.md) — per-session migration status

## Tools

| Tool | Purpose |
|---|---|
| `tools/migrate_equipment_type_1_4_3.py` | Mechanical XML migration (civilian="true" → equipmentType="Civilian"). NEEDS REVISION before --apply: should NOT add explicit Battle to bare sets. |
| `tools/audit_equipment_roster_coverage.py` | Per-culture mandatory roster matrix audit. Output: equipment-roster-coverage.csv. |
| `tools/validate_equipment_flags_1_4_3.py` | Deprecated flag scanner. Exit 1 if any hits remain. |
| `tools/decompile_to_folder.ps1` | ilspycmd wrapper for bulk decompile. |
| `tools/taom-src.ps1` | On-demand type decompiler + cache. ⚠️ Needs version auto-detect fix. |
