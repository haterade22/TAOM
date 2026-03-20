---
paths:
  - "Main/_Module/ModuleData/troops/**"
  - "Main/_Module/ModuleData/taom_partyTemplates.xml"
  - "Main/Features/TroopProgression/**"
---

# Troop Management Rules

## When Adding or Restructuring Troops

Update ALL of the following (checklist):

| Step | File(s) | What to do |
|------|---------|------------|
| 1. Define troops | `Main/_Module/ModuleData/troops/troops_{culture}.xml` | Add NPCCharacter with skills, equipment, upgrade_targets, race, culture |
| 2. Party templates | `Main/_Module/ModuleData/taom_partyTemplates.xml` | Add to ALL relevant templates for the culture (hero, patrol L1/L2/L3, outlaw, rebels, mercenary, vassal_reward) |
| 3. Culture config | `Main/_Module/ModuleData/taom_spcultures.xml` | Update `basic_troop` / `elite_basic_troop` if entry point changed |
| 4. Recruitment code | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | Add/update settlement, clan, and culture fallback pools |
| 5. Recruitment tests | `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` | TDD: write tests FIRST, then implement |
| 6. NPC references | `Main/_Module/ModuleData/characters/npcs_{culture}.xml` | Check villager upgrade_targets, caravan guard references |
| 7. CHANGELOG | `CHANGELOG.md` | Document the changes |

## Troop ID Naming Convention

`{culture_prefix}_{origin}_{role}` — Examples:
- `dg_goblin_slave` — Dol Guldur, goblin race, slave role
- `dg_khamul_shadow_initiate` — Dol Guldur, Khamul's line, shadow initiate
- `gondor_ano_peasant` — Gondor, Anórien origin, peasant role

## Race Attributes by Culture

| Culture | Race Lines | Race Attribute |
|---------|-----------|---------------|
| Dol Guldur | Goblin | `race="goblin"` |
| Dol Guldur | Orc | `race="orc"` |
| Dol Guldur | Uruk | `race="dg_uruk"` |
| Dol Guldur | Khamul (human) | no `race` attribute |
| Gondor | Human | no `race` attribute |
| Gundabad | Goblin/Orc | `race="goblin"` / `race="orc"` |

## Party Template Types

Each culture typically has these templates in `taom_partyTemplates.xml`:

| Template | Purpose | Typical Composition |
|----------|---------|-------------------|
| `kingdom_hero_party_{culture}_template` | Lord armies | Full range T1-T9 |
| `kingdom_hero_party_mercenary_{culture}_template` | Mercenary bands | Mid-tier professional |
| `kingdom_hero_party_outlaw_{culture}_template` | Outlaw parties | Low-tier rabble |
| `patrol_party_{culture}_template_level_1` | Weak patrols | Low-mid tier |
| `patrol_party_{culture}_template_level_2` | Medium patrols | Mid tier |
| `patrol_party_{culture}_template_level_3` | Elite patrols | High tier |
| `rebels_{culture}_template` | Rebel uprisings | Low tier masses |
| `vassal_reward_troops_{culture}` | Vassal rewards | Elite troops |
| `militia_{culture}_template` | Town garrison | Militia troops |

## Save Compatibility

- **Never change troop IDs** — rename display names only (keep `id` attribute)
- **Never delete troops** — orphan them (remove from upgrade_targets) but keep in file
- **is_basic_troop** — marks a troop as a standalone recruitment entry point
