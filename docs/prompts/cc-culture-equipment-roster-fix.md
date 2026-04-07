# Fix: CC Parent Equipment Rosters for Custom Cultures

## Problem

When a player selects a custom culture (erebor, mordor, gondor, rivendell, etc.) during Character Creation, `Hero.MainHero.Culture.StringId` returns a **vanilla** culture ID (e.g., `"battania"`) instead of the custom one at runtime.

### Root Cause

BL's `CharacterCreationCampaignBehavior.GetParentMenuNarrativeMenuCharacterArgs` (decompiled at `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\CampaignBehaviors\CharacterCreationCampaignBehavior.cs`, lines 290-291) hardcodes equipment roster lookups:

```csharp
"mother_char_creation_none_" + characterCreationManager.CharacterCreationContent.SelectedCulture.StringId
"father_char_creation_none_" + characterCreationManager.CharacterCreationContent.SelectedCulture.StringId
```

When `SelectedCulture.StringId` is `"erebor"`, this looks for `"mother_char_creation_none_erebor"` — an equipment roster that doesn't exist. This causes a crash/fallback during the parent narrative stage, which silently reverts the hero's culture to the `CharacterObject.PlayerCharacter` template's default culture (typically `battania`).

BL's `ApplyCulture()` at `CharacterCreationContent.cs:135` **does** correctly call `Hero.MainHero.Culture = SelectedCulture`, but by the time it runs, the culture has already been corrupted by the parent stage equipment failure.

### Evidence

From TAOM logs:
```
[INFO] CC Finalize: SelectedCulture='erebor', Hero.Culture before='battania'
[INFO] CC Finalize: Force-set Hero.Culture to 'erebor' (was 'battania')
```

The force-set in `CharacterCreationContentService.OnCharacterCreationFinalize` is a workaround, not a fix.

## Fix Required

### Step 1: Create Parent Equipment Rosters for All Custom Cultures

For each custom culture that TAOM registers, create `mother_char_creation_none_{cultureId}` and `father_char_creation_none_{cultureId}` equipment rosters in an XML file.

**Custom cultures needing rosters** (10 total — check `Main/_Module/ModuleData/taom_spcultures.xml` for exact list):
- `gondor`, `mordor`, `erebor`, `rivendell`, `lothlorien`, `mirkwood`, `isengard`, `gundabad`, `dolguldur`, `umbar`

**Vanilla cultures that already have rosters** (skip these):
- `empire`, `vlandia`, `sturgia`, `aserai`, `battania`, `khuzait`

**Location:** Create `Main/_Module/ModuleData/cc_equipment_rosters.xml` (or similar)

**Equipment roster format** (check vanilla examples at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\` — search for `mother_char_creation_none_empire`):

```xml
<MBEquipmentRosters>
  <EquipmentRoster id="mother_char_creation_none_erebor">
    <EquipmentSet civilian="true">
      <!-- Use appropriate cultural civilian items from LOTRLOME_Armory -->
      <Equipment slot="Body" id="Item.{erebor_civilian_body}" />
      <Equipment slot="Leg" id="Item.{erebor_civilian_legs}" />
      <Equipment slot="Cape" id="Item.{erebor_civilian_cape}" />
    </EquipmentSet>
  </EquipmentRoster>
  <EquipmentRoster id="father_char_creation_none_erebor">
    <EquipmentSet civilian="true">
      <Equipment slot="Body" id="Item.{erebor_civilian_body}" />
      <Equipment slot="Leg" id="Item.{erebor_civilian_legs}" />
    </EquipmentSet>
  </EquipmentRoster>
  <!-- Repeat for all 10 custom cultures -->
</MBEquipmentRosters>
```

**Item IDs:** Cross-reference with `LOTRLOME_Armory` module for culture-appropriate civilian items:
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\{culture}\body_armors.xml`
- Use low-tier civilian items (not military armor)
- Characters in CC parent stage appear in underwear if items are missing — validate every ID

### Step 2: Register the Equipment Roster XML

Add the new XML file to `Main/_Module/ModuleData/` and ensure it's loaded by the mod. Check how existing equipment XMLs are registered — likely via `SubModule.xml` or automatic by convention.

### Step 3: Verify with `DefaultCharacterCreationBodyProperty`

Each custom culture in `taom_spcultures.xml` should also define `default_character_creation_body_property`. Without it, `CharacterCreationCultureStageVM.InitializePlayersFaceKeyAccordingToCultureSelection()` skips the face preview update. Check existing vanilla cultures for the format.

### Step 4: Remove the Force-Set Workaround

After rosters are created and verified working:
1. Remove the force-set block in `Main/Features/CharacterCreation/CharacterCreationContentService.cs` (lines ~149-156 that check `Hero.MainHero.Culture?.StringId != selectedCulture.StringId`)
2. The log lines can stay for debugging

### Step 5: Test

1. Start new game → select Erebor culture
2. Parent narrative stage should show correctly dressed parents (not underwear)
3. Check log: `CC Finalize: SelectedCulture='erebor', Hero.Culture before='erebor'` — culture should match BEFORE the force-set
4. In-game: `Hero.MainHero.Culture.StringId` should return `"erebor"`
5. Career system should auto-assign the correct Erebor career (Ironguard/Crossbow Master/Ram Rider)
6. Repeat for each custom culture

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/taom_spcultures.xml` | List of all custom culture IDs |
| `Main/_Module/ModuleData/cc_equipment_rosters.xml` | NEW — parent equipment rosters |
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs:149-156` | Force-set workaround to remove |
| `E:\Decompiled_Bannerlord\Campaign\...\CharacterCreationCampaignBehavior.cs:290-291` | The vanilla code that does the roster lookup |
| `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\` | Vanilla equipment roster examples |
| `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\` | Culture-appropriate items |

## Context

This blocks the career system from working correctly — careers are assigned based on `Hero.Culture.StringId`, and if that returns the wrong culture, the wrong career gets assigned. The career system has a safety net (force-set in CC finalize + legacy fallback in `CareerCampaignBehavior.OnSessionLaunched`), but the proper fix is making the equipment rosters exist so BL's own CC pipeline works correctly.
