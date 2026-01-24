# Migration Tracking

Status tracker for Bannerlord 1.2.12 → 1.3.12 migration.

**Last Updated:** 2026-01-24

---

## Overall Progress

| Category | Total | Complete | Remaining |
|----------|-------|----------|-----------|
| Module XML | 1 | 0 | 1 |
| Culture XML | 1 | 0 | 1 |
| Settlement XML | 1 | 0 | 1 |
| Troop XML | 2 | 0 | 2 |
| Item XML | 1 | 0 | 1 |
| Equipment XML | 1 | 0 | 1 |
| Code Changes | TBD | 0 | TBD |

---

## Module XML

| File | Status | Notes |
|------|--------|-------|
| `SubModule.xml` | NOT STARTED | Update module_version, dependencies |

---

## Culture XML

| File | Status | Notes |
|------|--------|-------|
| `spcultures.xml` | NOT STARTED | Primary culture definitions |

### Culture Schema Changes (1.2 → 1.3)
- [ ] Verify `default_face_key` format
- [ ] Check new required attributes
- [ ] Update culture bonuses format
- [ ] Verify troop tree references

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

## Troop XML

| File | Status | Notes |
|------|--------|-------|
| `spnpccharacters.xml` | NOT STARTED | Non-lord characters |
| `lords.xml` | NOT STARTED | Lord characters |

### Troop Schema Changes (1.2 → 1.3)
- [ ] Verify skill format changes
- [ ] Check equipment set references
- [ ] Update face_key format if needed
- [ ] Verify upgrade path syntax

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

**2026-01-24**: Initial tracking setup. Migration not yet started.

---

## How to Update This File

1. Change status from `NOT STARTED` → `IN PROGRESS` → `COMPLETE`
2. Add notes about issues encountered
3. Check off subtask checkboxes as complete
4. Update "Last Updated" date at top
5. Add session notes in the log section
