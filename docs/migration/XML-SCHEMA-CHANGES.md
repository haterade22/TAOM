# Bannerlord XML Schema Changes: 1.2.12 → 1.3.13

This document tracks XML schema changes between Bannerlord versions to ensure TAOM mod compatibility.

## Analysis Date
2026-01-24

## Source Files Analyzed

### Bannerlord 1.3.13 (Current Target)
| File | Location | Elements |
|------|----------|----------|
| heroes.xml | SandBox/ModuleData/ | 415 Hero elements |
| lords.xml | SandBox/ModuleData/ | 399 NPCCharacter elements |
| spclans.xml | SandBox/ModuleData/ | 97 Faction elements |
| spkingdoms.xml | SandBox/ModuleData/ | 8 Kingdom elements |
| spcultures.xml | SandBoxCore/ModuleData/ | 6 main cultures |

---

## Heroes.xml Schema

### Hero Element Attributes (1.3.13)

| Attribute | Required | Type | Example | Notes |
|-----------|----------|------|---------|-------|
| `id` | Yes | string | `lord_1_1`, `main_hero` | Unique identifier |
| `faction` | Yes | reference | `Faction.clan_empire_north_1` | Links to spclans.xml |
| `spouse` | No | reference | `Hero.lord_1_2` | Bidirectional relationship |
| `father` | No | reference | `Hero.dead_lord_2_1` | Parent relationship |
| `mother` | No | reference | `Hero.lord_1_2` | Parent relationship |
| `text` | No | localized | `{=bsOLRZyS}Description...` | Character backstory |
| `alive` | No | boolean | `false` | Default: true if omitted |
| `preferred_upgrade_formation` | No | string | `HorseArcher` | Military preference |

### Hero ID Naming Conventions

| Pattern | Meaning | Example |
|---------|---------|---------|
| `main_hero` | Player character | `main_hero` |
| `lord_[K]_[N]` | Kingdom K, Lord N | `lord_1_1` (Empire North leader) |
| `lord_[K]_[N]_[R]` | Relative of lord | `lord_1_1_1` (child of lord_1_1) |
| `dead_lord_[K]_[N]` | Historical/deceased | `dead_lord_2_1` |
| `lord_[CODE]_[X]` | Regional code format | `lord_NE8_l`, `lord_A9_s` |

### Kingdom Code Mapping

| Code | Kingdom | TAOM Mapping |
|------|---------|--------------|
| 1 | Empire (all 3) | Dunland/Gondor/Mordor |
| 2 | Sturgia | Dale |
| 3 | Aserai | Harad |
| 4 | Vlandia | Rohan |
| 5 | Battania | Khand |
| 6 | Khuzait | Rhun |

---

## Lords.xml Schema (NPCCharacter)

### NPCCharacter Attributes (1.3.13)

| Attribute | Required | Type | Example | Notes |
|-----------|----------|------|---------|-------|
| `id` | Yes | string | `lord_1_1` | Must match heroes.xml |
| `name` | Yes | localized | `{=ekcBrNN1}Eren` | Display name |
| `age` | Yes | integer | `22`, `52` | Character age |
| `voice` | Yes | string | `earnest`, `curt`, `ironic`, `softspoken` | Voice pack |
| `is_hero` | Yes | boolean | `true` | Always true for lords |
| `is_female` | No | boolean | `true` | Default: false |
| `culture` | Yes | reference | `Culture.empire` | Links to spcultures.xml |
| `default_group` | Yes | string | `Cavalry`, `Infantry` | Battle grouping |
| `occupation` | Yes | string | `Lord` | Always "Lord" |
| `skill_template` | No | reference | `SkillSet.spc_politician_skills_ruler` | Skill preset |
| `face_mesh_cache` | No | boolean | `true` | Performance optimization |

### NPCCharacter Child Elements

```xml
<NPCCharacter id="..." ...>
    <face>
        <BodyProperties version="4" age="X" weight="Y" build="Z" key="[HASH]" />
        <hair_tags><hair_tag name="[CULTURE]" /></hair_tags>
        <beard_tags><beard_tag name="[CULTURE]" /></beard_tags>
        <tattoo_tags><tattoo_tag name="Cleanface" /></tattoo_tags>
    </face>
    <skills>
        <skill id="OneHanded|TwoHanded|Polearm|Bow|..." value="0-300" />
    </skills>
    <Traits>
        <Trait id="Commander|Politician|Valor|Honor|..." value="-3 to 10" />
    </Traits>
    <Equipments>
        <EquipmentRoster civilian="true">
            <equipment slot="Body|Head|..." id="Item.[ITEM_ID]" />
        </EquipmentRoster>
        <EquipmentSet id="[CULTURE]_[CLASS]_template" civilian="true" />
    </Equipments>
</NPCCharacter>
```

### Skills List (18 total)

**Combat:** OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing
**Movement:** Riding, Athletics
**Crafting:** Crafting
**Command:** Tactics, Scouting, Leadership
**Social:** Roguery, Charm, Trade
**Administrative:** Steward, Medicine, Engineering

### Trait IDs

| Trait | Range | Description |
|-------|-------|-------------|
| Commander | 0-10 | Military leadership |
| Politician | 0-10 | Political skill |
| Manager | 0-10 | Economic management |
| Valor | -2 to 2 | Bravery in battle |
| Honor | -2 to 2 | Keeping word |
| Generosity | -2 to 2 | Sharing wealth |
| Mercy | -2 to 2 | Treatment of prisoners |
| Calculating | -2 to 2 | Strategic thinking |
| Egalitarian | -2 to 2 | Social views |
| Oligarchic | -2 to 2 | Power views |
| Authoritarian | -2 to 2 | Governance style |

---

## SPClans.xml Schema

### Faction Element Attributes (1.3.13)

| Attribute | Noble Clan | Minor Faction | Bandit | Notes |
|-----------|------------|---------------|--------|-------|
| `id` | Yes | Yes | Yes | Unique identifier |
| `name` | Yes | Yes | Yes | Localized display name |
| `tier` | Yes | Yes | Yes | Power level (0-6) |
| `owner` | Yes | Yes | No | Hero reference |
| `culture` | Yes | Yes | Yes | Culture reference |
| `super_faction` | Yes | No | No | Parent kingdom |
| `is_noble` | Yes | No | No | `true` |
| `is_minor_faction` | No | Yes | No | `true` |
| `is_bandit` | No | No | Yes | `true` |
| `is_outlaw` | No | Some | Yes | `true` |
| `is_clan_type_mercenary` | No | Mercs | No | `true` |
| `is_mafia` | No | Mafia | No | `true` |
| `banner_key` | Yes | Yes | Yes | Banner visual encoding |
| `color` | Some | Yes | Yes | Primary ARGB color |
| `color2` | Some | Yes | Yes | Secondary ARGB color |
| `initial_home_settlement` | Some | Yes | Yes | Starting location |
| `default_party_template` | No | Yes | Yes | Troop composition |
| `settlement_banner_mesh` | Some | Yes | Yes | Banner mesh reference |

### Clan ID Patterns

| Pattern | Example | Kingdom |
|---------|---------|---------|
| `clan_empire_north_[1-9]` | `clan_empire_north_1` | Empire (Dunland) |
| `clan_empire_west_[1-9]` | `clan_empire_west_5` | Empire West (Gondor) |
| `clan_empire_south_[1-9]` | `clan_empire_south_3` | Empire South (Mordor) |
| `clan_sturgia_[1-9]` | `clan_sturgia_1` | Sturgia (Dale) |
| `clan_aserai_[1-9]` | `clan_aserai_4` | Aserai (Harad) |
| `clan_vlandia_[1-11]` | `clan_vlandia_7` | Vlandia (Rohan) |
| `clan_battania_[1-8]` | `clan_battania_2` | Battania (Khand) |
| `clan_khuzait_[1-9]` | `clan_khuzait_6` | Khuzait (Rhun) |

### Tier Values

| Tier | Meaning | Typical Use |
|------|---------|-------------|
| 0 | Minimal | Player faction start |
| 1 | Very Low | Bandits |
| 2 | Low | Minor clans |
| 3 | Low-Mid | Some mercenaries |
| 4 | Mid | Major clans |
| 5 | High | Prominent clans |
| 6 | Highest | Kingdom rulers |

---

## Key Changes: 1.2.x → 1.3.x

### Kingdoms (spkingdoms.xml)

| Change | 1.2.x | 1.3.x | Impact |
|--------|-------|-------|--------|
| `initial_home_settlement` | Optional | **REQUIRED** | Must specify for all kingdoms |
| `label_color` | Used | Deprecated | Still works but not required |
| `alternative_color` | Used | Deprecated | Still works but not required |
| `alternative_color2` | Used | Deprecated | Still works but not required |

### Cultures (spcultures.xml)

| Change | 1.2.x | 1.3.x | Impact |
|--------|-------|-------|--------|
| `default_stealth_equipment_roster` | Not present | **REQUIRED** | Must specify for StealthEquipments |
| `marriage_bride_equipment_roster` | Not present | New | Optional |
| `settlement_patrol_template_level_1/2/3` | Not present | New | Optional patrol templates |
| `start_point_position_x/y` | Not present | New | Character creation position |
| `<caravan_party_templates>` | Simple | Enhanced structure | New element format |
| `<elite_caravan_party_templates>` | Not present | New | Elite caravan support |
| `<available_ship_hulls>` | Not present | New | Naval support |

### Lords/Heroes

| Change | 1.2.x | 1.3.x | Impact |
|--------|-------|-------|--------|
| `BodyProperties version` | 3 | 4 | New face system version |
| `preferred_upgrade_formation` | Not present | New | Optional upgrade preference |

### Clans (spclans.xml)

| Change | 1.2.x | 1.3.x | Impact |
|--------|-------|-------|--------|
| No significant schema changes detected | - | - | Backward compatible |

---

## XSLT Migration Strategy

### For Lords/Heroes
1. Use XSLT to modify `name` attribute with LOTR names
2. Optionally add `text` attribute with LOTR backstory
3. Keep all other attributes unchanged (IDs, factions, relationships)

### For Clans
1. Use XSLT to modify `name` attribute with LOTR clan names
2. Keep `id`, `owner`, `super_faction` unchanged
3. No structural changes needed

### For Kingdoms
Already implemented in `spkingdoms.xslt`:
- Names, titles, ruler titles, descriptions updated
- Structure preserved from vanilla

### For Cultures
Already implemented in `spcultures.xslt`:
- Names and descriptions updated
- Structure preserved from vanilla

---

## Vanilla Lord Count by Kingdom

| Kingdom ID | TAOM Name | Lord Count | Clan Count |
|------------|-----------|------------|------------|
| empire (north) | Dunland | ~45 | 9 |
| empire_w (west) | Gondor | ~45 | 9 |
| empire_s (south) | Mordor | ~45 | 9 |
| sturgia | Dale | ~45 | 9 |
| aserai | Harad | ~45 | 9 |
| vlandia | Rohan | ~55 | 11 |
| battania | Khand | ~40 | 8 |
| khuzait | Rhun | ~45 | 9 |
| **Total** | | **~365** | **73** |

---

## Files to Create for TAOM

| File | Purpose |
|------|---------|
| `splords.xslt` | Transform vanilla lord names → LOTR names |
| `spheroes.xslt` | Transform hero faction assignments, add backstories |
| `spclans.xslt` | Transform vanilla clan names → LOTR clan names |

---

## Verification Checklist

After creating XSLT transformations:

- [ ] Build succeeds
- [ ] Game launches without XML errors
- [ ] All 8 kingdoms load correctly
- [ ] Lord names display as LOTR names
- [ ] Clan names display as LOTR names
- [ ] Family relationships preserved
- [ ] No missing heroes/lords in encyclopedia
- [ ] Campaign can start without crashes
