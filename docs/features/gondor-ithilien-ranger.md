# Gondor Ithilien Ranger (T9 Troop + Faramir Equipment)

## Overview

A standalone T9 Gondor recruit, `gondor_ithilien_ranger` (level 41, `is_basic_troop="true"`, `default_group="Ranged"`), recruitable directly from notables in Minas Tirith and the two Ithilien-area castles. Equipped exclusively from the LOTRLOME_Armory Ithilien wardrobe (8 jerkin variants × matching hood/cloak/boots/bracers + Ithilien bows + Noldar Elven Arrows). Also re-equips Faramir as Captain of the Ithilien Rangers using his dedicated character-specific armor.

## Why This Exists

The previous Gondor troop tree had no troop named "Ithilien Ranger" despite the LOTRLOME_Armory shipping a full Ithilien wardrobe (jerkins, hoods, cloaks, boots, bracers, bows, shields) and the CareerSystem having a "Ranger of Ithilien" career. The Blackroot Vale line (T7 `gondor_brv_ranger` → T9 `gondor_brv_shadowbow`) used Ithilien bows but wore generic Anórien plate (`sk_gd_ano_*`), so none of the actual Ithilien armor variants were equipped by any Gondor troop. Faramir, the lore-canonical Captain of the Ithilien Rangers, was rendering in light leather (`ithilien_jerkin_long`, 30 body armor) that looked like a peasant tunic next to Boromir's heavy Osgiliath plate.

- **Vanilla behavior:** N/A — Gondor is a TAOM-custom culture; no vanilla equivalent.
- **TAOM requirement:** A regional-specialty elite ranger recruitable from Ithilien-area notables, with the full Ithilien wardrobe used. Faramir must visually read as the Ithilien Ranger Captain.
- **Without this feature:** Ithilien wardrobe items go unused; ranger career has no troop counterpart; Faramir looks indistinguishable from a common peasant in encyclopedia portraits.

## Architecture

### Design Challenge

Three coupled constraints:

1. **Standalone, not BRV-line.** Per user direction the new troop is NOT integrated into the Blackroot Vale upgrade chain — no existing troop upgrades into it. Players reach it exclusively via notable recruitment in specific settlements.
2. **Tier filter behavior.** TAOM's `TaomVolunteerModel.MaxVolunteerTier => 6` initially looked like it would block a T9 troop from notable pools. Decompilation of v1.3.15 `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement` (lines 235-249) showed `MaxVolunteerTier` only gates upgrade progression on slots with non-empty `UpgradeTargets`. Initial slot assignment via `GetBasicVolunteer` is unchecked, and the new troop's `<upgrade_targets />` is empty — so the gate never fires.
3. **Wardrobe-only constraint.** Per user direction, the Ithilien Ranger should only wear items literally branded "Ithilien" in their display name. Excludes Ithil Guard plate (`sk_gd_ith_*`) and Minas Ithil shields (`wm_gondor_shield_*_minas_ithil`) despite the prefix similarity.

### Solution Approach

Pure data-layer feature — no new C# code, no new GameModel override, no Harmony patch. Adds:

- One new `<NPCCharacter>` block in `troops/troops_gondor.xml`
- Three settlement entries in the existing `VolunteerRecruitmentService.cs` static initializer
- Per-slot equipment swap on the existing `faramir_bat_equipment` and `faramir_civ_equipment` rosters

The new troop has 8 `<EquipmentRoster>` blocks, each pairing a unique Ithilien jerkin variant with a matching hood, boots, cloak, bracers, bow, and a unique Noldar Elven Arrow variant. The engine picks one roster per spawn, giving visual diversity across squads.

### Component Diagram

```
troops/troops_gondor.xml
  └── gondor_ithilien_ranger (T9, level 41, is_basic_troop)
        ├── 8 EquipmentRoster blocks (1 per ithilien_jerkin_* body variant)
        └── <upgrade_targets /> (empty — standalone)

VolunteerRecruitmentService.cs InitializeGondorSettlements()
  ├── town_EW1   → gondor_ano_peasant (7) + gondor_ithilien_ranger (3)
  ├── castle_EW15 → gondor_ano_peasant (7) + gondor_ithilien_ranger (3)
  └── castle_EW16 → gondor_ano_peasant (7) + gondor_ithilien_ranger (3)
         │
         └── TaomVolunteerModel.GetBasicVolunteer()
              └── notable.VolunteerTypes[i] = troop  ← no tier filter at this hop

taom_equipment_sets_gondor.xml
  ├── faramir_bat_equipment (battle)
  └── faramir_civ_equipment (civilian, equipmentType="Civilian")
        └── faramir_armor / ithilien_hood / ithilien_cloak_var /
            ithilien_boots_heavy / faramir_bracers / wm_gondor_faramir_sword
```

## Configuration

### Troop XML: `Main/_Module/ModuleData/troops/troops_gondor.xml`

The `gondor_ithilien_ranger` `<NPCCharacter>` block sits after `gondor_brv_ranger` (~line 3420). Stats:

| Skill | Value |
|---|---|
| Athletics | 155 |
| Riding | 40 |
| OneHanded | 230 |
| TwoHanded | 145 |
| Polearm | 145 |
| Bow | **280** |
| Crossbow | 55 |
| Throwing | 70 |

Eight `<EquipmentRoster>` blocks, each with the same fixed items (`ithilien_bracers`, `wm_gondor_sword_a10`) and varied items:

| # | Body | Head | Leg | Cape | Bow | Arrows ×2 |
|---|---|---|---|---|---|---|
| 1 | `ithilien_jerkin_long` | `ithilien_hood` | `ithilien_boots` | `ithilien_cloak` | `wm_ithilien_bow_c` | `wm_elven_arrow_v1_a` |
| 2 | `ithilien_jerkin_long_slim` | `ithilien_hood_var` | `ithilien_boots_heavy` | `ithilien_cloak_var` | `wm_ithilien_bow_b` | `wm_elven_arrow_v2_a` |
| 3 | `ithilien_jerkin_long_var` | `ithilien_hood_masked` | `ithilien_boots` | `ithilien_cloak` | `wm_ithilien_bow_c` | `wm_elven_arrow_v3_a` |
| 4 | `ithilien_jerkin_long_var_slim` | `ithilien_hood_masked_var` | `ithilien_boots_heavy` | `ithilien_cloak_var` | `wm_ithilien_bow_b` | `wm_elven_arrow_v4_a` |
| 5 | `ithilien_jerkin_short` | `ithilien_hood` | `ithilien_boots` | `ithilien_cloak_var` | `wm_ithilien_bow` | `wm_elven_arrow_v1_b` |
| 6 | `ithilien_jerkin_short_slim` | `ithilien_hood_var` | `ithilien_boots_heavy` | `ithilien_cloak` | `wm_ithilien_bow_c` | `wm_elven_arrow_v1_c` |
| 7 | `ithilien_jerkin_short_var` | `ithilien_hood_masked` | `ithilien_boots` | `ithilien_cloak_var` | `wm_ithilien_bow_b` | `wm_elven_arrow_v1_d` |
| 8 | `ithilien_jerkin_short_var_slim` | `ithilien_hood_masked_var` | `ithilien_boots_heavy` | `ithilien_cloak` | `wm_ithilien_bow` | `wm_elven_arrow_v2_b` |

### Recruitment service: `Main/Features/TroopProgression/VolunteerRecruitmentService.cs`

Three settlements updated in `InitializeGondorSettlements()`:

```csharp
AddSettlement("town_EW1",    "gondor_ano_peasant", 7, "gondor_ithilien_ranger", 3); // Minas Tirith
AddSettlement("castle_EW15", "gondor_ano_peasant", 7, "gondor_ithilien_ranger", 3); // Amonost
AddSettlement("castle_EW16", "gondor_ano_peasant", 7, "gondor_ithilien_ranger", 3); // Erethir
```

Weight 7 vs 3 means a notable rolls ~70% basic peasant / ~30% Ithilien Ranger. Old notable troops (`gondor_mt_trainee`, `gondor_ith_watcher`) remain defined and reachable via clan-map fallback.

### Faramir equipment: `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml`

| Slot | Battle (`faramir_bat_equipment`) | Civilian (`faramir_civ_equipment`) |
|---|---|---|
| Item0 (sword) | `wm_gondor_faramir_sword` | — |
| Item2 (bow) | `wm_ithilien_bow_c` | — |
| Item3 (arrows) | `wm_elven_arrow_v2_d` | — |
| Head | `ithilien_hood` | `ithilien_hood` |
| Body | `faramir_armor` | `faramir_armor` |
| Cape | `ithilien_cloak_var` | `ithilien_cloak_var` |
| Gloves | `faramir_bracers` | `faramir_bracers` |
| Leg | `ithilien_boots_heavy` | `ithilien_boots_heavy` |
| Horse | `charger` | — |

Civilian roster correctly tagged with `equipmentType="Civilian"` per the standalone EquipmentRosters schema.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/troops/troops_gondor.xml` (~line 3420) | `gondor_ithilien_ranger` `<NPCCharacter>` definition with 8 rosters |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` (lines 84, 111, 112) | Settlement pool entries for `town_EW1`, `castle_EW15`, `castle_EW16` |
| `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml` (lines 170–193) | `faramir_bat_equipment` + `faramir_civ_equipment` rosters |
| `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` | 6 tests: high-roll returns Ithilien Ranger at each of 3 settlements; low-roll returns basic at the 2 castles; region-specificity check on a non-Ithilien settlement |

## Dependencies

- **LOTRLOME_Armory items** (all verified present at author time):
  - Body: `faramir_armor`, `ithilien_jerkin_{long,long_slim,long_var,long_var_slim,short,short_slim,short_var,short_var_slim}`
  - Head: `ithilien_hood{,_var,_masked,_masked_var}`
  - Leg: `ithilien_boots{,_heavy}`
  - Cape: `ithilien_cloak{,_var}`
  - Gloves: `faramir_bracers`, `ithilien_bracers`
  - Weapons: `wm_gondor_faramir_sword`, `wm_gondor_sword_a10`, `wm_ithilien_bow{,_b,_c}`, `wm_elven_arrow_v*_*`
- **Vanilla items:** none (all gear is LOTRLOME-provided)

## Tests

- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` — 6 new test methods:
  - `_MinasTirith_HighRoll_ReturnsIthilienRanger`
  - `_AmonostCastle_HighRoll_ReturnsIthilienRanger`
  - `_ErethirCastle_HighRoll_ReturnsIthilienRanger`
  - `_AmonostCastle_LowRoll_ReturnsRegularTroop`
  - `_ErethirCastle_LowRoll_ReturnsRegularTroop`
  - `_NonIthilienSettlement_HighRoll_DoesNotReturnIthilienRanger` (region-specificity check on `town_EW2`)
- Full suite at the time of feature ship: 89 in `VolunteerRecruitmentServiceTests` (was 86 before).

## How to Add a Similar Region-Specialty Troop

1. **Define the troop** in the matching `troops/troops_{culture}.xml` with `is_basic_troop="true"`, `<upgrade_targets />`, and one or more `<EquipmentRoster>` blocks.
2. **Verify item IDs** — grep `LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/*.xml` for every `Item.id` referenced. Missing items cause the underwear bug.
3. **Add settlement entries** in `VolunteerRecruitmentService.cs` `Initialize{Culture}Settlements()`. Three-arg overload: `(settlementId, regularTroop, regularWeight, notableTroop, notableWeight)`.
4. **Write TDD tests** in `VolunteerRecruitmentServiceTests.cs` covering high-roll, low-roll, and region-specificity (settlement OUTSIDE the targeted area should NOT return the new troop).
5. **Update CHANGELOG** with the feature, item IDs used, settlement allocation, and any save-compat notes.

No code changes outside the service + tests + XML. `TaomVolunteerModel.MaxVolunteerTier = 6` does NOT block T9 troops from notable pools (only gates upgrade progression on slots with non-empty `UpgradeTargets`).

## Performance

None — pure data lookup. `VolunteerRecruitmentService.GetVolunteerTroopId` is called per notable per day at most, runs O(N) over a 2-entry pool, allocates zero. Static `Dictionary<string, List<VolunteerChance>>` maps populated once at class load.

## GitHub Issue

- **Issue:** #201 — feat(troops): T9 Ithilien Ranger recruitable from Ithilien-area notables
- **Status:** Closed (shipped in commits `d086f79`, `ae44e2a`, `e41b13f`, `8f6b62a`, `35a05d6`, `7807b28`, `5ae0a2a`)

## Related

- **Memory:** [feedback_equipmenttype_civilian_required.md](../../C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_equipmenttype_civilian_required.md) — why Faramir's civilian roster needs `equipmentType="Civilian"`.
- **Feature doc:** [troop-progression.md](troop-progression.md) — the broader recruitment service architecture this feature plugs into.
- **Feature doc:** [gondor-armor-revamp.md](gondor-armor-revamp.md) — sibling KEYforce gear authoring workflow.
