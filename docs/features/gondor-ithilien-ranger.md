# Gondor Ithilien Ranger (T9 Troop + Faramir Equipment)

## Overview

A standalone T10 Gondor recruit, `gondor_ithilien_ranger` (level 51 on disk since the tier-10 ladder; the original design was T9 / level 41, and this doc's tree below still shows that. `is_basic_troop="true"`, `default_group="Ranged"`), recruitable directly from notables in Minas Tirith, Osgiliath and Cair Andros. Equipped exclusively from the LOTRLOME_Armory Ithilien wardrobe (4 jerkin variants, two rosters each, × matching hood/cloak/boots/bracers + Ithilien bows + Noldor Elven Arrows). Also re-equips Faramir as Captain of the Ithilien Rangers using his dedicated character-specific armor.

## Why This Exists

The previous Gondor troop tree had no troop named "Ithilien Ranger" despite the LOTRLOME_Armory shipping a full Ithilien wardrobe (jerkins, hoods, cloaks, boots, bracers, bows, shields) and the CareerSystem having a "Ranger of Ithilien" career. The Blackroot Vale line (T7 `gondor_brv_ranger` → T9 `gondor_brv_shadowbow`) used Ithilien bows but wore generic Anórien plate (`sk_gd_ano_*`), so none of the actual Ithilien armor variants were equipped by any Gondor troop. Faramir, the lore-canonical Captain of the Ithilien Rangers, was rendering in light leather (`ithilien_jerkin_long`, 30 body armor) that looked like a peasant tunic next to Boromir's heavy Osgiliath plate.

- **Vanilla behavior:** N/A — Gondor is a TAOM-custom culture; no vanilla equivalent.
- **TAOM requirement:** A regional-specialty elite ranger recruitable from Ithilien-area notables, with the full Ithilien wardrobe used. Faramir must visually read as the Ithilien Ranger Captain.
- **Without this feature:** Ithilien wardrobe items go unused; ranger career has no troop counterpart; Faramir looks indistinguishable from a common peasant in encyclopedia portraits.

## Architecture

### Design Challenge

Three coupled constraints:

1. **Standalone, not BRV-line.** Per user direction the new troop is NOT integrated into the Blackroot Vale upgrade chain — no existing troop upgrades into it. Players reach it exclusively via notable recruitment in specific settlements.
2. **Tier filter behavior.** TAOM's `TaomVolunteerModel.MaxVolunteerTier => 6` initially looked like it would block a T9 troop from notable pools. Decompilation of vanilla `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement` (lines 235-249) showed `MaxVolunteerTier` only gates upgrade progression on slots with non-empty `UpgradeTargets`. Initial slot assignment via `GetBasicVolunteer` is unchecked, and the new troop's `<upgrade_targets />` is empty — so the gate never fires.
3. **Wardrobe-only constraint.** Per user direction, the Ithilien Ranger should only wear items literally branded "Ithilien" in their display name. Excludes Ithil Guard plate (`sk_gd_ith_*`) and Minas Ithil shields (`wm_gondor_shield_*_minas_ithil`) despite the prefix similarity.

### Solution Approach

Pure data-layer feature — no new C# code, no new GameModel override, no Harmony patch. Adds:

- One new `<NPCCharacter>` block in `troops/troops_gondor.xml`
- Ranger entries in the Gondor recruitment pools JSON (`recruitment_pools/gondor.json`), which `VolunteerRecruitmentService` loads over its hand-written Gondor pools
- Per-slot equipment swap on the existing `faramir_bat_equipment` and `faramir_civ_equipment` rosters

The new troop has 8 `<EquipmentRoster>` blocks, each pairing one of four Ithilien jerkin variants (two rosters per jerkin) with a hood, boots, cloak, bracers, the same bow, and a Noldor Elven Arrow variant. The engine picks one roster per spawn, giving visual diversity across squads.

### Component Diagram

```
troops/troops_gondor.xml
  └── gondor_ithilien_ranger (T10, level 51 on disk; authored as T9 / level 41, is_basic_troop)
        ├── 8 EquipmentRoster blocks (2 per ithilien_jerkin_* body variant, 4 variants)
        └── <upgrade_targets /> (empty — standalone)

recruitment_pools/gondor.json (loaded by GondorRecruitmentJsonLoader into VolunteerRecruitmentService)
  ├── town_EW1 (Minas Tirith)          → ... + gondor_ithilien_ranger (10 of 100)
  ├── town_EW2, town_EW3 (Osgiliath)   → ... + gondor_ithilien_ranger (10 of 100)
  └── castle_EW4 (Cair Andros)         → gondor_ca_noble (45) + gondor_ca_veteran (45) + gondor_ithilien_ranger (10)
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

The `gondor_ithilien_ranger` `<NPCCharacter>` block sits after `gondor_brv_ranger` (~line 5316). Stats:

| Skill | Value |
|---|---|
| Athletics | 185 |
| Riding | 45 |
| OneHanded | 270 |
| TwoHanded | 175 |
| Polearm | 175 |
| Bow | **380** |
| Crossbow | 65 |
| Throwing | 70 |

Eight `<EquipmentRoster>` blocks, each with the same fixed items (`ithilien_bracers`, `wm_gondor_sword_a03` in Item3, `ladder_ithilien_bow_t10` in Item0) and varied items; the arrows fill Item1 and Item2:

| # | Body | Head | Leg | Cape | Arrows ×2 |
|---|---|---|---|---|---|
| 1 | `ithilien_jerkin_long` | `ithilien_hood` | `ithilien_boots` | `ithilien_cloak` | `wm_elven_arrow_v1_a` |
| 2 | `ithilien_jerkin_long` | `ithilien_hood_var` | `ithilien_boots_heavy` | `ithilien_cloak_var` | `wm_elven_arrow_v2_a` |
| 3 | `ithilien_jerkin_long_var` | `ithilien_hood_masked` | `ithilien_boots` | `ithilien_cloak` | `wm_elven_arrow_v3_a` |
| 4 | `ithilien_jerkin_long_var` | `ithilien_hood_masked_var` | `ithilien_boots_heavy` | `ithilien_cloak_var` | `wm_elven_arrow_v4_a` |
| 5 | `ithilien_jerkin_short` | `ithilien_hood` | `ithilien_boots` | `ithilien_cloak_var` | `wm_elven_arrow_v1_b` |
| 6 | `ithilien_jerkin_short` | `ithilien_hood_var` | `ithilien_boots_heavy` | `ithilien_cloak` | `wm_elven_arrow_v1_c` |
| 7 | `ithilien_jerkin_short_var` | `ithilien_hood_masked` | `ithilien_boots` | `ithilien_cloak_var` | `wm_elven_arrow_v1_d` |
| 8 | `ithilien_jerkin_short_var` | `ithilien_hood_masked_var` | `ithilien_boots_heavy` | `ithilien_cloak` | `wm_elven_arrow_v2_b` |

### Recruitment pools: `Main/_Module/ModuleData/recruitment_pools/gondor.json`

The ranger sits in three pool groups at weight 10 of a 100 total: Minas Tirith (`town_EW1`), Osgiliath (`town_EW2`, `town_EW3`) and Cair Andros (`castle_EW4`). The loader (`GondorRecruitmentJsonLoader`, called from the `VolunteerRecruitmentService` constructor) overwrites matching settlement keys of the hand-written fallback pools, so a notable rolls about 10% Ithilien Ranger in those settlements. `castle_EW15` and `castle_EW16` no longer carry the ranger.

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
| Horse | none | none |

Civilian roster correctly tagged with `equipmentType="Civilian"` per the standalone EquipmentRosters schema.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/troops/troops_gondor.xml` (~line 5316) | `gondor_ithilien_ranger` `<NPCCharacter>` definition with 8 rosters |
| `Main/_Module/ModuleData/recruitment_pools/gondor.json` | Pool groups for `town_EW1`, `town_EW2`, `town_EW3`, `castle_EW4` |
| `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml` (lines 396 to 417) | `faramir_bat_equipment` + `faramir_civ_equipment` rosters |
| `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` | `GondorJsonLoader_ProductionJson_PlacesIthilienRangerAtTenPercent`: drives the real `gondor.json` and checks the ranger at 10% in `town_EW1`, `town_EW2`, `town_EW3`, `castle_EW4` |

## Dependencies

- **LOTRLOME_Armory items** (all verified present at author time):
  - Body: `faramir_armor`, `ithilien_jerkin_{long,long_var,short,short_var}`
  - Head: `ithilien_hood{,_var,_masked,_masked_var}`
  - Leg: `ithilien_boots{,_heavy}`
  - Cape: `ithilien_cloak{,_var}`
  - Gloves: `faramir_bracers`, `ithilien_bracers`
  - Weapons: `wm_gondor_faramir_sword`, `wm_gondor_sword_a03`, `ladder_ithilien_bow_t10`, `wm_ithilien_bow_c` (Faramir), `wm_elven_arrow_v*_*`
- **Vanilla items:** none (all gear is LOTRLOME-provided)

## Tests

- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`: `GondorJsonLoader_ProductionJson_PlacesIthilienRangerAtTenPercent` (4 data rows: `town_EW1`, `town_EW2`, `town_EW3`, `castle_EW4`) reads the production `gondor.json`, so the ranger cannot drop to 0% while the fallback-only tests stay green.
- Full suite at the time of feature ship: 89 in `VolunteerRecruitmentServiceTests` (was 86 before).

## How to Add a Similar Region-Specialty Troop

1. **Define the troop** in the matching `troops/troops_{culture}.xml` with `is_basic_troop="true"`, `<upgrade_targets />`, and one or more `<EquipmentRoster>` blocks.
2. **Verify item IDs** — grep `LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/*.xml` for every `Item.id` referenced. Missing items cause the underwear bug.
3. **Add settlement entries** in `VolunteerRecruitmentService.cs` `Initialize{Culture}Settlements()` with `AddSettlement(string settlementId, params (string troopId, int weight)[] entries)`. For Gondor, also add the weights to `recruitment_pools/gondor.json`, which overwrites matching settlement keys.
4. **Write TDD tests** in `VolunteerRecruitmentServiceTests.cs` covering high-roll, low-roll, and region-specificity (settlement OUTSIDE the targeted area should NOT return the new troop).
5. **Write the commit body** with the feature, item IDs used, settlement allocation, and any save-compat notes: it is the changelog entry.

No code changes outside the service + tests + XML. `TaomVolunteerModel.MaxVolunteerTier = 6` does NOT block T9 troops from notable pools (only gates upgrade progression on slots with non-empty `UpgradeTargets`).

## Performance

None: pure data lookup. `VolunteerRecruitmentService.GetVolunteerTroopId` is called per notable per day at most, runs O(N) over a pool of 3 to 8 entries, allocates zero. Static `Dictionary<string, List<VolunteerChance>>` maps populated once at class load.

## Changelog

- 2026-09-12: The ranger now also reaches the field through Faramir's own party template, `kingdom_hero_party_gondor_faramir_template` (a 0 to 15 stack beside the Ithil Guard ranged line), via the per-hero override in [lord-party-templates.md](lord-party-templates.md) (#580). Recruitment pools were the only route before.
- 2026-06-22 — Restored the Ithilien Ranger to 10% in the live Gondor pools by adding `gondor_ithilien_ranger: 10` to the `gondor.json` Minas Tirith / Osgiliath / Cair Andros groups (the JSON loader overwrites the fallback pool, so its live spawn rate was 0%); added a production-JSON regression test.
- 2026-05-23 — Re-equipped Faramir as Ithilien Ranger Captain (battle + civilian rosters), replacing the peasant-looking leather kit with his dedicated character-specific gear.
- 2026-05-23 — Authored this feature doc and codified the `equipmentType="Civilian"` EquipmentRosters schema rule.
- 2026-05-19 — Upgraded all 8 ranger quivers to Noldar Elven Arrows (`wm_elven_arrow_v*_*`), the highest-damage/largest-quiver arrows in the Armory.
- 2026-05-19 — Dropped the 4 Ithil Guard plate rosters (12 → 8) so rangers wear only items literally branded "Ithilien."
- 2026-05-19 — Added the standalone T9 `gondor_ithilien_ranger` troop with Ithilien-themed equipment rosters, recruitable from Minas Tirith and the two Ithilien-area castles via `VolunteerRecruitmentService`.

## GitHub Issue

- **Issue:** #201 — feat(troops): T9 Ithilien Ranger recruitable from Ithilien-area notables
- **Status:** Closed (shipped in commits `d086f79`, `ae44e2a`, `e41b13f`, `8f6b62a`, `35a05d6`, `7807b28`, `5ae0a2a`)

## Related

- **Memory:** `feedback_equipmenttype_civilian_required` — why Faramir's civilian roster needs `equipmentType="Civilian"`.
- **Feature doc:** [troop-progression.md](troop-progression.md) — the broader recruitment service architecture this feature plugs into.
- **Feature doc:** [gondor-armor-revamp.md](gondor-armor-revamp.md) — sibling KEYforce gear authoring workflow.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/troops.md](../modding/troops.md)

<!-- backlinks-end -->
