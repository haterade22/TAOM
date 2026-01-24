# XML Migration Strategy

Step-by-step process for migrating XML files from Bannerlord 1.2 to 1.3.

---

## General Approach

1. **Backup** original files before any changes
2. **Reference** vanilla 1.3 XML for correct schema
3. **Migrate** one file at a time
4. **Test** each file before moving to next
5. **Document** changes in TRACKING.md

---

## Reference Files Location

Vanilla 1.3 XML files are located at:
```
{BANNERLORD_GAME_DIR}/Modules/SandBox/ModuleData/
{BANNERLORD_GAME_DIR}/Modules/Native/ModuleData/
{BANNERLORD_GAME_DIR}/Modules/SandBoxCore/ModuleData/
```

Use these as reference for correct 1.3 schema.

---

## Migration Order

### Phase 1: Core Module

1. **SubModule.xml**
   - Update `module_version` if format changed
   - Verify `DependedModules` references
   - Check `SubModuleClassType` paths

### Phase 2: Cultures

2. **spcultures.xml**
   - Compare with vanilla 1.3 culture structure
   - Update any renamed attributes
   - Verify troop tree references
   - Check cultural bonuses format

### Phase 3: Map Data

3. **settlements.xml**
   - Compare component structure with 1.3
   - Update village binding format if changed
   - Verify prosperity/loyalty defaults
   - Check production/workshop data

### Phase 4: Characters

4. **spnpccharacters.xml**
   - Verify skill format
   - Update equipment references
   - Check face_key format
   - Verify upgrade paths

5. **lords.xml**
   - Same checks as spnpccharacters
   - Verify clan/kingdom assignments

### Phase 5: Items & Equipment

6. **spitems.xml**
   - Check weapon stats format
   - Verify armor tier system
   - Update material/crafting data

7. **equipment_sets.xml**
   - Verify slot naming
   - Check pool references

---

## File-by-File Instructions

### SubModule.xml

```xml
<!-- Check these elements -->
<Module>
  <Name value="TAOM"/>
  <Id value="TAOM"/>
  <Version value="e1.3.0"/>  <!-- Update version scheme -->

  <DependedModules>
    <!-- Verify module IDs match 1.3 -->
    <DependedModule Id="Native" DependentVersion="e1.3.0"/>
    <DependedModule Id="SandBoxCore" DependentVersion="e1.3.0"/>
    <DependedModule Id="SandBox" DependentVersion="e1.3.0"/>
  </DependedModules>
</Module>
```

### spcultures.xml

```xml
<!-- Reference vanilla 1.3 structure -->
<Culture
  id="your_culture"
  name="{=...}Culture Name"
  ...>

  <!-- Verify these elements exist and have correct format -->
  <default_face_key>...</default_face_key>

  <!-- Check basic_troop, elite_basic_troop references -->
  <basic_troop id="your_basic_troop"/>
  <elite_basic_troop id="your_elite_troop"/>

  <!-- Verify bonus format -->
  <cultural_bonuses>
    <!-- Schema may have changed -->
  </cultural_bonuses>
</Culture>
```

### settlements.xml

```xml
<!-- Reference vanilla 1.3 structure -->
<Settlement
  id="your_settlement"
  name="{=...}Settlement Name"
  ...>

  <Components>
    <!-- Verify Town/Castle/Village component format -->
    <Town
      id="..."
      ...>
      <!-- Check inner elements -->
    </Town>
  </Components>

  <!-- Verify location data format -->
  <Locations>
    <!-- Schema may have changed -->
  </Locations>
</Settlement>
```

### spnpccharacters.xml

```xml
<!-- Reference vanilla 1.3 structure -->
<NPCCharacter
  id="your_troop"
  name="{=...}Troop Name"
  ...>

  <!-- Verify skill format -->
  <skills>
    <skill id="OneHanded" value="50"/>
  </skills>

  <!-- Verify equipment reference format -->
  <equipmentSet id="your_equipment_set"/>

  <!-- Verify upgrade format -->
  <upgrade_targets>
    <upgrade id="upgraded_troop"/>
  </upgrade_targets>
</NPCCharacter>
```

---

## Validation Process

### Step 1: Schema Validation
1. Open vanilla 1.3 file of same type
2. Compare root element and attributes
3. Compare child element structure
4. Note any differences

### Step 2: Update File
1. Make minimal changes to match 1.3 schema
2. Keep content (IDs, names, values) unchanged initially
3. Fix only structural/schema issues first

### Step 3: Test Loading
1. Start game with only this file changed
2. Check for XML parsing errors in log
3. Verify data loads correctly in-game

### Step 4: Document
1. Update TRACKING.md with status
2. Note any issues encountered
3. Record schema changes discovered

---

## Common Issues

### Missing Required Attributes
```xml
<!-- Error: Missing required attribute -->
<Element/>

<!-- Fix: Add required attribute -->
<Element required_attr="value"/>
```

### Changed Element Names
```xml
<!-- 1.2 format -->
<old_element_name>...</old_element_name>

<!-- 1.3 format -->
<new_element_name>...</new_element_name>
```

### Changed Nesting Structure
```xml
<!-- 1.2 flat structure -->
<Parent>
  <ChildA/>
  <ChildB/>
</Parent>

<!-- 1.3 nested structure -->
<Parent>
  <Container>
    <ChildA/>
    <ChildB/>
  </Container>
</Parent>
```

---

## Troubleshooting

### Game Won't Load
1. Check game log for XML errors
2. Revert to backup and migrate one element at a time
3. Compare with vanilla 1.3 file

### Missing Content In-Game
1. Verify IDs match between files
2. Check reference IDs (equipment, cultures, etc.)
3. Verify file is listed in SubModule.xml

### Crashes on Campaign Start
1. Check for circular references
2. Verify all required relationships exist
3. Check for null/missing required elements

---

## Checklist Template

Use this for each file:

```
File: [filename.xml]
Date: [date]

Pre-Migration:
- [ ] Backup created
- [ ] Vanilla 1.3 reference obtained
- [ ] Schema differences documented

Migration:
- [ ] Root element updated
- [ ] Attributes updated
- [ ] Child elements restructured
- [ ] References verified

Validation:
- [ ] No XML parse errors
- [ ] Content loads in-game
- [ ] Functionality verified

Post-Migration:
- [ ] TRACKING.md updated
- [ ] Changes documented
```
