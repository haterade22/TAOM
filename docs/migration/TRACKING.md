# Migration Tracking

Status tracker for Bannerlord 1.2.12 → 1.3.12 migration.

**Last Updated:** 2026-01-24

---

## Overall Progress

| Category | Total | Complete | Remaining |
|----------|-------|----------|-----------|
| Module XML | 1 | 1 | 0 |
| XSLT Transformations | 5 | 5 | 0 |
| Culture XML | 1 | 1 | 0 |
| Kingdom XML | 1 | 1 | 0 |
| Clan XML | 1 | 1 | 0 |
| Lords XML | 1 | 1 | 0 |
| Heroes XML | 1 | 1 | 0 |
| Settlement XML | 1 | 0 | 1 |
| Troop XML | 2 | 0 | 2 |
| LOTRAOM Troop Files | 13 | 13 | 0 |
| Item XML | 1 | 0 | 1 |
| Equipment XML | 1 | 0 | 1 |
| Code Changes | TBD | 0 | TBD |

---

## Module XML

| File | Status | Notes |
|------|--------|-------|
| `SubModule.xml` | COMPLETE | Updated with XSLT entries for kingdoms, cultures, clans, lords |

---

## XSLT Transformations

TAOM uses XSLT transformations to modify vanilla XML at load time, renaming entities with LOTR-themed names while preserving vanilla structure.

| File | Status | Transforms | Notes |
|------|--------|------------|-------|
| `spkingdoms.xslt` | COMPLETE | 8 kingdoms | Dunland, Gondor, Mordor, Dale, Harad, Rohan, Khand, Rhûn |
| `spcultures.xslt` | COMPLETE | 6 cultures | Dunlending, Barding, Haradrim, Rohirrim, Variag, Easterling |
| `spclans.xslt` | COMPLETE | 73 clans | All noble clans across 8 kingdoms |
| `splords.xslt` | COMPLETE | ~350 lords | All lords across 6 base kingdoms |
| `spheroes.xslt` | COMPLETE | 415 heroes | Biographical text for all heroes |

### XSLT Mapping Reference

| Vanilla Kingdom | TAOM Name | Vanilla Culture | TAOM Culture |
|-----------------|-----------|-----------------|--------------|
| empire | Dunland | empire | Dunlending |
| empire_w | Gondor | - | - |
| empire_s | Mordor | - | - |
| sturgia | Dale | sturgia | Barding |
| aserai | Harad | aserai | Haradrim |
| vlandia | Rohan | vlandia | Rohirrim |
| battania | Khand | battania | Variag |
| khuzait | Rhûn | khuzait | Easterling |

---

## Culture XML

| File | Status | Notes |
|------|--------|-------|
| `spcultures.xslt` | COMPLETE | Renames 6 main cultures via XSLT transformation |

### Culture Schema Changes (1.2 → 1.3)
- [x] Verify `default_face_key` format - unchanged
- [x] Check new required attributes - `default_stealth_equipment_roster` now required
- [x] Update culture bonuses format - unchanged
- [x] Verify troop tree references - unchanged

---

## Settlement XML

| File | Status | Notes |
|------|--------|-------|
| `settlements.xml` | NOT STARTED | Settlements and castles |

### Settlement Schema Changes (1.2 → 1.3)
- [ ] Check component structure changes
- [ ] Verify bound village references
- [ ] Update prosperity/loyalty defaults
- [ ] Check workshop/production changes

---

## Kingdom XML

| File | Status | Notes |
|------|--------|-------|
| `spkingdoms.xslt` | COMPLETE | Renames 8 kingdoms via XSLT transformation |

### Kingdom Schema Changes (1.2 → 1.3)
- [x] `initial_home_settlement` now REQUIRED
- [x] `label_color` deprecated but still works
- [x] `alternative_color` deprecated but still works
- [x] `alternative_color2` deprecated but still works

---

## Clan XML

| File | Status | Notes |
|------|--------|-------|
| `spclans.xslt` | COMPLETE | Renames 73 noble clans via XSLT transformation |

### Clan Schema Changes (1.2 → 1.3)
- [x] No significant schema changes detected
- [x] Backward compatible with 1.2 format

---

## Heroes XML

| File | Status | Notes |
|------|--------|-------|
| `spheroes.xslt` | COMPLETE | Adds LOTR-themed biographical text for all 415 heroes |

### Heroes Schema Notes
- [x] Heroes.xml contains family relationships (spouse, father, mother)
- [x] Heroes.xml contains faction references
- [x] `text` attribute holds biographical descriptions
- [x] Hero IDs match lord IDs (lord_X_Y pattern)

---

## Troop XML

| File | Status | Notes |
|------|--------|-------|
| `spnpccharacters.xml` | NOT STARTED | Non-lord characters |
| `splords.xslt` | COMPLETE | Renames ~350 lords via XSLT transformation |

### Troop Schema Changes (1.2 → 1.3)
- [x] `BodyProperties version` changed from 3 to 4
- [x] `preferred_upgrade_formation` attribute added (optional)
- [ ] Verify skill format changes - pending for spnpccharacters
- [ ] Check equipment set references - pending for spnpccharacters

---

## LOTRAOM Troop Files

Copied from `E:/LOTRAOMAssets/LOTRAOM_Jan_1_Patreon/Modules/LOTRAOM/ModuleData/` to `Main/_Module/ModuleData/troops/`.

**Status:** STAGED (copied but NOT registered in SubModule.xml)

| File | Status | Lines | Culture | Notes |
|------|--------|-------|---------|-------|
| `troops_rohan.xml` | STAGED | 5,803 | vlandia | Horse archers, Riders of Rohan |
| `troops_gondor.xml` | STAGED | 6,955 | gondor* | Tower Guard, Knights, Rangers |
| `troops_harad.xml` | STAGED | 1,911 | aserai | Haradrim warriors |
| `troops_mordor.xml` | STAGED | 4,300 | custom* | Orcs, Trolls, Black Númenóreans |
| `troops_isengard.xml` | STAGED | 5,045 | custom* | Uruk-hai |
| `troops_rhun.xml` | STAGED | 2,041 | khuzait | Easterling warriors |
| `troops_dunland.xml` | STAGED | 3,137 | empire | Dunlending hillmen |
| `troops_erebor.xml` | STAGED | 6,249 | custom* | Dwarves of Erebor |
| `troops_rivendell.xml` | STAGED | 7,169 | custom* | Elves of Rivendell |
| `troops_mirkwood.xml` | STAGED | 951 | custom* | Wood Elves, Spiders |
| `troops_gundabad.xml` | STAGED | 1,943 | custom* | Goblins, Orcs |
| `troops_umbar.xml` | STAGED | 2,139 | custom* | Corsairs of Umbar |
| `troops_dolguldur.xml` | STAGED | 7,622 | custom* | Necromancer forces |

*\*Custom cultures require new culture definitions before activation*

### Schema Verification
- [x] No `BodyProperties version` attributes (uses `face_key_template` references)
- [x] XML well-formed
- [ ] Item references need verification (custom LOTRAOM items)
- [ ] Culture references need custom culture definitions

### To Activate These Troops
1. Create custom culture XMLs for non-vanilla cultures (gondor, mordor, erebor, etc.)
2. Add item XMLs for referenced custom equipment
3. Register troop files in `SubModule.xml` with `<XmlNode>` entries
4. Update culture `basic_troop`, `elite_basic_troop`, etc. references

---

## Item XML

| File | Status | Notes |
|------|--------|-------|
| `spitems.xml` | NOT STARTED | Weapons, armor, items |

### Item Schema Changes (1.2 → 1.3)
- [ ] Check weapon stats format
- [ ] Verify armor tier system
- [ ] Update crafting data format
- [ ] Check item culture assignments

---

## Equipment XML

| File | Status | Notes |
|------|--------|-------|
| `equipment_sets.xml` | NOT STARTED | Equipment loadouts |

### Equipment Schema Changes (1.2 → 1.3)
- [ ] Verify equipment slot naming
- [ ] Check civilian/battle equipment
- [ ] Update equipment pool references

---

## Code Changes

| Component | Status | Notes |
|-----------|--------|-------|
| Harmony Patches | NOT STARTED | Verify method signatures |
| GameModels | NOT STARTED | Check overridden methods |
| CampaignBehaviors | NOT STARTED | Event signature changes |
| MissionLogics | NOT STARTED | Mission API changes |

### Known API Changes
- [ ] `Mission` constructor parameters
- [ ] Campaign event delegates
- [ ] Party management methods
- [ ] Settlement query methods

---

## Testing Checklist

### Basic Loading
- [ ] Mod loads without errors
- [ ] No missing XML element warnings
- [ ] No null reference on startup

### Campaign
- [ ] New campaign starts successfully
- [ ] Cultures display correctly
- [ ] Settlements appear on map
- [ ] Lords spawn with correct equipment

### Gameplay
- [ ] Combat functions normally
- [ ] Troop upgrades work
- [ ] Economy systems function
- [ ] AI behaves correctly

---

## Notes

### Migration Session Log

**2026-01-24 (Session 4)**: Migrated LOTRAOM troop files:
- Copied 13 troops_*.xml files (~55,265 lines total) to `Main/_Module/ModuleData/troops/`
- Files staged but NOT registered in SubModule.xml (per request)
- No BodyProperties version updates needed (files use face_key_template references)
- Build verified successful

**2026-01-24 (Session 3)**: Completed Heroes biographical XSLT transformation:
- Created `spheroes.xslt` with biographical text for all 415 heroes
- LOTR-themed descriptions for clan leaders, spouses, heirs, and dead lords
- Updated `SubModule.xml` with Heroes XmlNode entry

**2026-01-24 (Session 2)**: Completed XSLT transformations for all entity types:
- Created `spclans.xslt` with 73 clan name transformations
- Created `splords.xslt` with ~350 lord name transformations across all 6 kingdoms
- Updated `SubModule.xml` with XmlNode entries for clans and lords
- Created `XML-SCHEMA-CHANGES.md` documenting 1.2→1.3 schema differences
- Build verified successful

**2026-01-24 (Session 1)**: Initial tracking setup. Created XSLT transformations for kingdoms and cultures:
- Created `spkingdoms.xslt` with 8 kingdom transformations
- Created `spcultures.xslt` with 6 culture transformations
- Updated `SubModule.xml` with XSLT entries

---

## How to Update This File

1. Change status from `NOT STARTED` → `IN PROGRESS` → `COMPLETE`
2. Add notes about issues encountered
3. Check off subtask checkboxes as complete
4. Update "Last Updated" date at top
5. Add session notes in the log section
