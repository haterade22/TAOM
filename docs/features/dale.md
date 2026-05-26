# Dale (Bardings of the Long Lake)

> **Authoring guide**: this feature is the worked example for the end-to-end culture authoring process. See [`docs/ai-includes/new-culture-authoring.md`](../ai-includes/new-culture-authoring.md) for the repeatable phase-by-phase guide (armor manifest → generator → wiring → recruitment → review → iteration loops). Dale shipped in 11 commits; expect a similar shape for the next culture.

## Overview

Dale is the kingdom of the Bardings — the descendants of Bard the Bowman, ruling the rebuilt city at the foot of the Lonely Mountain (Erebor) and the surviving folk of Lake-town (Esgaroth). This feature adds Dale-themed armor (163 items modeled by Solus) and a 35-troop tree (Dale caps at T7 — no T8 elites) spanning **Excellent Archers** (bronze) + **Royal Crossbowmen** (silver), **Great Infantry**, **Cavalry** split into Light (bronze) + Heavy (silver), a royal **Riverman** spear-and-shield line, two Lake-Town infantry lines (**Watch** halberds and **Pikeman** vanilla pikes), plus 4 garrison militia troops.

## Why This Exists

- **Vanilla behavior:** `Culture.sturgia` is reskinned to "Barding" / "Dale" via `spcultures.xslt`, but the culture had no troops, no party templates, no recruitment pools, and no faction-themed armor. NPCs, hero abilities, and lord equipment rosters already existed but with nothing to recruit from a settlement.
- **TAOM requirement:** A complete, lore-grounded Dale playable culture matching the established TAOM faction template (Gondor/Erebor/Rohan-grade depth).
- **Without this feature:** Sturgia kingdom settlements would offer vanilla Sturgian peasants/warriors (visually wrong — fur-clad northmen, not Bardings) and no troop pipeline would exist for the Dale lord clans defined in `npcs_dale.xml`.

## Architecture

### Design Challenge

Dale rides on vanilla `Culture.sturgia` rather than a custom culture id (matches the Rohan/Khand/Rhun/Harad/Dunland pattern, simplifies kingdom wiring, retains save-compat with vanilla). That means:
- All troop XML uses `culture="Culture.sturgia"`
- The XSLT culture-rename block must additionally override 9 military-attribute slots (basic_troop / elite / militia / party-template / equipment-rosters)
- VolunteerRecruitmentService binds `CultureMap["sturgia"]` rather than a custom Dale key

Solus's mesh naming preserves some quirks (`chivlary` typo in 4 slots, `chivalry` spelling only on chests, `infrantry` typo, `lake_town_mariner` for the bracers/skirmisher Esgaroth gear). The engine binds meshes by exact name — we preserve verbatim.

The new armor was authored from `.tpac` files we couldn't open directly. The tool `tools/tpac_skeleton_scan.py --all-types` (originally written for the spider-skeleton work) lists every AssetItem in a `.tpac`. Running it across the 5 Dale tpacs produced 169 mesh IDs which became the source-of-truth manifest.

### Solution Approach

Two author-time Python generators produce all the XML. Both follow the Gondor generator pattern.

```
.tpac binary mesh files          (5 files, ~169 meshes)
        |
tpac_skeleton_scan.py --all-types  (existing tool)
        |
tools/dale_armor_meshes.txt        (manifest)
        |
generate_dale_armor.py             (parses manifest, applies STAT_TIERS + class/tier->material lookup)
        |
LOTRLOME_Armory/.../dale/*.xml     (163 items across 5 slot files)

troop_design (Python data structure, 35 troops with explicit equipment)
        |
generate_dale_troops.py
        |
Main/_Module/ModuleData/troops/troops_dale.xml

spcultures.xslt (Culture[@id='sturgia'] adds 9 military attrs)
taom_partyTemplates.xml (+9 Dale templates)
Main/_Module/SubModule.xml (registers troops_dale)
VolunteerRecruitmentService.cs (+ InitializeDaleCulture)
```

### Troop Tree

**Lake-Town Levy** (T2 → T3 root; smallfolk and townsfolk of Esgaroth):
- T2 `dale_recruit` "Lake-Town Peasant" (basic_troop) → T3 `dale_militia` "Lake-Town Militia"

**Lake-Town Watch line** (T4-T6, off `dale_militia`; 2H halberds/polearms + 1H sword sidearm, no shield, shock infantry):
- T4 `dale_lake_town_skirmisher` "Lake-Town Watchman" → T5 `dale_lake_town_mariner` "Lake-Town Veteran Watchman" → T6 `dale_lake_town_veteran` "Lake-Town Officer of the Watch"
- Weapons: `sturgia_2haxe_1_t4` / `billhook_polearm_t2` (T4); `sturgia_polearm_1_t5` / `sturgia_2haxe_2_t5` (T5-T6). Skill curve: Polearm-primary + mild TwoHanded (for axe-style overhead swings).

**Lake-Town Pikeman line** (T4-T7, off `dale_militia`; vanilla pikes + 1H sword sidearm, no shield, anti-cavalry):
- T4 `dale_footman` "Lake-Town Patrolman" → T5 `dale_spearman` "Lake-Town Pikeman" → T6 `dale_veteran_spearman` "Lake-Town Veteran Pikeman" → T7 `dale_lake_town_hearthguard` "Lake-Town Hearthguard" (T7 terminal)
- Weapons: `fine_pike_t4` / `military_fork_pike_t3` (T4); `vlandia_pike_1_t5` / `thamaskene_pike_t4` (T5+). Skill curve: Polearm-only (no TwoHanded — pikes use Polearm exclusively).

**Dalian Levy** (T3, elite_basic_troop; the royal-line recruit) — branches four ways:

**Excellent Archers** (Bard's heritage; +10-15 Bow over baseline at every tier; **bronze** archer armor, 1 variant per tier):
- T4 `dale_bowman` "Dalian Yeoman" (`a01`) → T5 `dale_longbowman` "Dalian Bowman" (`a02`) → T6 `dale_royal_archer` "Dalian Marksman" (`a03`) → T7 `dale_black_arrow_marksman` "Dalian Barding" (`a04`, T7 terminal)

**Royal Crossbowmen** (parallel ranged branch off Dalian Levy; vanilla crossbows + bolts + 1H sword sidearm; **silver** archer armor, 1 variant per tier):
- T4 `dale_crossbowman` "Dalian Crossbowman" (`b01`) → T5 `dale_veteran_crossbowman` "Dalian Veteran Crossbowman" (`b02`) → T6 `dale_royal_crossbowman` "Dalian Master Crossbowman" (`b03`) → T7 `dale_master_crossbowman` "Dalian Royal Crossbowman" (`b04`, T7 terminal). Note: ID order and display-name order intentionally desynced — "Royal" is reserved as the highest-rank title across the kingdom, so the T7 troop holds it even though its ID was originally authored as `dale_master_*`.
- Weapons: `crossbow_c` (T4) → `crossbow_d` (T5) → `crossbow_e` (T6) → `crossbow_f` (T7) with `bolt_a/b/c/d/e` ammo progression.

**Great Infantry** (Dalian Militia → Dalian Royal Swordsman; "variation per level" — one bronze + one silver infrantry variant per tier: `aNN`+`bNN`):
- T4 `dale_man_at_arms` "Dalian Militia" → T5 `dale_guardsman` "Dalian Guardsman" → T6 `dale_royal_guard` "Dalian Swordsman" → T7 `dale_running_river_warden` "Dalian Royal Swordsman" (T7 terminal)

**Riverman line** (spear + shield + 1H sword, **infrantry** mesh silver `b01-b03`; royal-tier water-folk):
- T4 `dale_riverman` "Dalian Riverman" (`b01`) → T5 `dale_shipman` "Dalian Shipman" (`b02`) → T6 `dale_dalian_mariner` "Dalian Mariner" (`b03`, terminal)

**Decent Cavalry** (T4 root splits into LIGHT + HEAVY; ~30% under Rohan parity per Tolkien's Éothéod-vs-Bardings split):
- T4 `dale_outrider` "Dalian Merchant Guard" — chivlary **`a01`** (bronze, roster A) **+ `b01`** (silver, roster B): mixed split-point armor reflecting the branching choice. Splits to:
  - **LIGHT CAVALRY** (silver chivlary): T5 `dale_knight` "Dalian Northman Scout" (`b02`) → T6 `dale_veteran_northman_scout` "Dalian Veteran Northman Scout" (`b03`+`b04`, T6 terminal).
  - **HEAVY CAVALRY** (bronze chivlary): T5 `dale_royal_cavalier` "Dalian Cavalry" (`a02`) → T6 `dale_kinsman_of_eorl` "Dalian Heavy Cavalry" (`a03`) → T7 `dale_kings_guard` "Dalian King's Guard" (`a04`, T7 terminal).

**Militia** (XSLT bindings for garrison spawns):
- T2 `dale_militia_spearman` → T4 `dale_militia_veteran_spearman`
- T2 `dale_militia_archer` → T4 `dale_militia_veteran_archer`

### Lore Sources

- *The Hobbit* ch. 14 "Fire and Water" — Bard's "Black Arrow" heirloom of Girion, Erebor-forged
- *The Hobbit* ch. 17 — Lake-men at the Battle of Five Armies "armed with long swords and tall spears," Bardings bore "great bows"
- *LOTR* Appendix A III "Durin's Folk" — Erebor-made mail-shirts reach as far as Esgaroth (Dale-Erebor trade)
- *LOTR* Appendix B — Battle of Dale TA 3019; Brand falls before the Gate of Erebor, Dáin defends his body
- *Two Towers* "Riders of Rohan" — Aragorn explicitly names the Bardings as kin to the Rohirrim (Northmen ancestry)
- Éothéod (Rohirrim ancestors) per *Unfinished Tales* "Cirion and Eorl" were the **horse-breeding** branch — Dale was the city-state branch. Cavalry is fanon-extrapolated and intentionally minor.

## Configuration

No JSON or runtime-tunable config. Two author-time data sources:

| File | Purpose |
|------|---------|
| `tools/dale_armor_meshes.txt` | Frozen manifest of Solus's mesh IDs (re-emit by re-running `tpac_skeleton_scan.py --all-types` against the 5 dale_kingdom .tpac files) |
| `tools/generate_dale_troops.py` `build_troops()` | Hardcoded 35-troop manifest with explicit equipment + skill curves |

## Key Files

### Created

| File | Purpose |
|------|---------|
| `tools/generate_dale_armor.py` | Generator: manifest → 5 armor XML files |
| `tools/generate_dale_troops.py` | Generator: hardcoded troop list → troops_dale.xml |
| `tools/dale_armor_meshes.txt` | Mesh ID manifest (one per line, deduped, sorted) |
| `Main/_Module/ModuleData/troops/troops_dale.xml` | 35 NPCCharacter definitions |
| `<armory>/ModuleData/LOTRLOME_items/dale/head_armors.xml` | 32 helmets |
| `<armory>/ModuleData/LOTRLOME_items/dale/body_armors.xml` | 41 chests (incl. 5 with gender-variation slim meshes + 9 cloth overlays) |
| `<armory>/ModuleData/LOTRLOME_items/dale/leg_armors.xml` | 32 boots |
| `<armory>/ModuleData/LOTRLOME_items/dale/arm_armors.xml` | 32 gauntlets/bracers (10 with `covers_hands="false"`) |
| `<armory>/ModuleData/LOTRLOME_items/dale/shoulder_armors.xml` | 26 pauldrons (Solus authored partial variants for archer + mariner) |

### Modified

| File | Change |
|------|--------|
| `Main/_Module/ModuleData/spcultures.xslt` | Dale block (lines 1138-1235): added 9 military attribute overrides on `Culture[@id='sturgia']` |
| `Main/_Module/ModuleData/taom_partyTemplates.xml` | Added 9 Dale party templates (lines 962-1056) |
| `Main/_Module/SubModule.xml` | Registered `troops/troops_dale` XmlNode |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | Added `InitializeDaleCulture()` + call in static ctor |
| `tools/validate_all_troop_refs.py` | Added `"dale"` to the cultures list |
| `<armory>/SubModule.xml` | Registered `LOTRLOME_items/dale` Items XmlNode |

## Dependencies

- `IVolunteerRecruitmentService` (Features/TroopProgression) — Dale recruitment is one entry in its `CultureMap["sturgia"]` pool
- Vanilla Sturgia items (weapons, horses, shields, harnesses) referenced via `Item.sturgia_*`, `Item.northern_spear_*`, `Item.charger`, `Item.chain_horse_harness`, etc. — all in SandBoxCore/Native
- LOTRAOM shared items (`Item.rohan_horse_armor_scalemail`) for Dale's modest cavalry harness (kinship nod to Rohirrim)
- Existing Dale scaffolding: `npcs_dale.xml`, `taom_equipment_sets_dale.xml`, `heroes.xml` Dale entries — none modified this session

## Tests

`TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` adds 4 tests (~+50 lines):

- `GetVolunteerTroopId_DaleCulture_LowRoll_ReturnsRecruit` — roll 0 → dale_recruit
- `GetVolunteerTroopId_DaleCulture_MidRoll_ReturnsMilitia` — roll 5 → dale_militia
- `GetVolunteerTroopId_DaleCulture_HighRoll_ReturnsSquire` — roll 10 (terminal) → dale_squire
- `GetVolunteerTroopId_DaleSettlement_NoSettlementPool_FallsThroughToCulture` — no-settlement-pool path falls through to culture pool, not null

Cross-reference validation:
- `python tools/validate_all_troop_refs.py` — confirms every `sk_dale_*` armor reference in `troops_dale.xml` resolves to a real item in `LOTRLOME_Armory` (the underwear-bug gate per `multi_folder_id_uniqueness` memory)

## How to add a new Dale troop

1. Open `tools/generate_dale_troops.py`
2. Add a new `troops.append(Troop(...))` block to `build_troops()` with explicit ID, tier, default_group, skills, upgrades, rosters
3. If the troop spawns from a recruitment pool: update `InitializeDaleCulture()` in `VolunteerRecruitmentService.cs` and add a test in `VolunteerRecruitmentServiceTests.cs`
4. If the troop appears in lord parties: add a `PartyTemplateStack` to `kingdom_hero_party_dale_template` (and/or patrol/mercenary/etc.) in `taom_partyTemplates.xml`
5. Run `python tools/generate_dale_troops.py --apply` to regenerate `troops_dale.xml`
6. Run `python tools/validate_all_troop_refs.py` to confirm every armor reference still resolves
7. Run `./build.ps1 -RunTests` to verify build + tests pass

## How to add new Dale armor

1. Drop the new `.tpac` files into `LOTRLOME_Armory/Assets/dale_kingdom/`
2. Re-run `pwsh tools/tpac_skeleton_scan.py "<path-to-tpac>" --all-types` to harvest mesh names
3. Append the new mesh IDs to `tools/dale_armor_meshes.txt` (one per line, sorted)
4. Verify the new IDs match the regex in `generate_dale_armor.py:MESH_RE` — if Solus introduced a new class or slot keyword, extend the parser
5. Run `python tools/generate_dale_armor.py --apply`
6. Run `python tools/validate_all_troop_refs.py`

## Known Limitations / Follow-ups

- **Lord equipment rosters still use vanilla Sturgia items** (caught by /deep-review Data Flow agent, MEDIUM finding, intentional deferral). `taom_equipment_sets_dale.xml` has 11 lord rosters (`dale_bat_template_medium_a..e`, `dale_civ_template_default_a..`) that reference vanilla items like `sturgian_helmet_closed`, `sturgia_cavalry_armor`, `chivalric_kite_shield`. Dale lords therefore appear as vanilla Sturgians in battle, while their troops appear correctly as Bardings. Visual mismatch only — no runtime error. Follow-up: replace lord roster items with `sk_dale_*` equivalents once an authoring pass on `taom_equipment_sets_dale.xml` lands.
- **Localization:** Armor items use inline `{=aom_<id>_name}[Dale] Display Name` fallbacks — no separate `loc_dale.xml` in `LOTRLOME_Armory/Languages/`. Same convention as Gondor's armor authoring. Translators can add language overrides later via the standard pipeline.
- **Per-settlement recruitment flavor:** Currently a single culture-level pool. If we want Esgaroth settlements to favor `dale_lake_town_skirmisher` over `dale_recruit`, author a `recruitment_pools/dale.json` (Gondor pattern) or add `AddSettlement()` calls to `InitializeDaleCulture()`.
- **Dale kingdom and clans:** Still inherit the vanilla Sturgia kingdom and clans. Future work could split off a Dale kingdom in `taom_spkingdoms.xml` with Brand-line clan IDs.

## Verification History

- `/verify quick` — build green (one pre-existing TAOM.Dependencies warning)
- `/deep-review dale` — 5 agents PASSED:
  - Standards: PASS
  - API Compatibility (v1.4.5): PASS (all 9 culture attrs + NPCCharacter + MBPartyTemplate schemas verified against installed DLLs)
  - Efficiency: PASS (CultureMap write is unique, static-ctor scope)
  - Completeness: tests, doc, CHANGELOG, issue identified — addressed in this session
  - Data Flow: 7 flows traced, 0 hard gaps, 1 MEDIUM inconsistency (lord rosters — documented as known follow-up)
- `python tools/validate_all_troop_refs.py` — 121 armor refs in troops_dale.xml, all resolve. Combined with manual non-armor (weapons/horses/shields/arrows) cross-check against 25,184 vanilla+armory IDs: all 50 non-armor refs resolve.
