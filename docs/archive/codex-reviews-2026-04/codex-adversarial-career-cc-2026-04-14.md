# Codex Adversarial Review: Career Selection in Character Creation

## Vanilla Code

Note: direct `ilspycmd` decompilation of the installed DLL was blocked by this session's command policy, so the vanilla excerpts below come from `E:\Decompiled_Bannerlord\...`.

### CharacterCreationManager.TrySwitchToNextMenu

```csharp
public bool TrySwitchToNextMenu()
{
    string stringId = CurrentMenu.StringId;
    SelectedOptions[CurrentMenu].OnConsequence(this);
    foreach (NarrativeMenu narrativeMenu in NarrativeMenus)
    {
        if (narrativeMenu.InputMenuId.Equals(stringId))
        {
            CurrentMenu = narrativeMenu;
            ModifyMenuCharacters();
            return true;
        }
    }
    return false;
}
```

Source: `E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterCreationContent/CharacterCreationManager.cs:229`

### CharacterCreationManager.ApplyFinalEffects

```csharp
public void ApplyFinalEffects()
{
    Clan.PlayerClan.Renown = 0f;
    CharacterCreationContent.ApplyCulture(this);
    foreach (KeyValuePair<NarrativeMenu, NarrativeMenuOption> selectedOption in SelectedOptions)
    {
        selectedOption.Value.ApplyFinalEffects(CharacterCreationContent);
    }
    ...
    foreach (KeyValuePair<int, ICharacterCreationContentHandler> handler in _handlers)
    {
        handler.Value.OnCharacterCreationFinalize(this);
    }
}
```

Source: `E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterCreationContent/CharacterCreationManager.cs:309`

### NarrativeMenuOption.ApplyFinalEffects

```csharp
public void ApplyFinalEffects(CharacterCreationContent characterCreationContent)
{
    characterCreationContent.ApplySkillAndAttributeEffects(
        Args.AffectedSkills.ToList(),
        Args.FocusToAdd,
        Args.SkillLevelToAdd,
        Args.EffectedAttribute,
        Args.AttributeLevelToAdd,
        Args.AffectedTraits.ToList(),
        Args.TraitLevelToAdd,
        Args.RenownToAdd,
        Args.GoldToAdd,
        Args.UnspentFocusToAdd,
        Args.UnspentAttributeToAdd);
}
```

Source: `E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterCreationContent/NarrativeMenuOption.cs:84`

### Empty-menu advancement behavior

```csharp
public override void OnNextStage()
{
    if (CharacterCreationManager.TrySwitchToNextMenu())
    {
        RefreshMenu();
    }
    else
    {
        _affirmativeAction();
    }
}

public override bool CanAdvanceToNextStage()
{
    if (SelectionList.Count != 0)
    {
        return SelectionList.Any((CharacterCreationOptionVM s) => s.IsSelected);
    }
    return true;
}
```

Source: `E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem.ViewModelCollection/TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation/CharacterCreationNarrativeStageVM.cs:168`

## Career Menu Chain Analysis

TAOM inserts the new menu with `InputMenuId = "narrative_adulthood_menu"`:

```csharp
var careerMenu = new NarrativeMenu(
    CareerMenuId,
    AdulthoodMenuId,
    "",
    ...);
manager.AddNewMenu(careerMenu);
```

Source: `Main/Features/CharacterCreation/CareerMenuService.cs:83`

That matches vanilla traversal: `TrySwitchToNextMenu()` advances by finding the first menu whose `InputMenuId` equals the current menu's `StringId`. So adulthood correctly flows into `narrative_career_menu`.

There is no TAOM menu whose `InputMenuId` is `narrative_career_menu`, so a successful advance from the career menu returns `false` and the narrative stage finalizes. Vanilla `ApplyFinalEffects()` then applies all selected narrative bonuses before calling `OnCharacterCreationFinalize`, so the career menu's skill/attribute bonuses do get applied if a career option was actually selected.

Finalization order is otherwise safe. Vanilla calls `CharacterCreationContent.ApplyCulture(this)` before handler finalization, and TAOM also force-sets `Hero.MainHero.Culture` before `AssignCareer()`:

```csharp
if (Hero.MainHero != null && Hero.MainHero.Culture?.StringId != selectedCulture.StringId)
{
    Hero.MainHero.Culture = selectedCulture;
}
AssignCareer(selectedCulture.StringId, Hero.MainHero?.StringId);
```

Source: `Main/Features/CharacterCreation/CharacterCreationContentService.cs:162`

`CareerCreationHandler.OnCareerSelected()` does not read `Hero.Culture`; it only resolves the career, stores it, adds the root choice, and refreshes cache. So the culture dependency chain is safe.

## Config Cross-Reference

- `career_menu.json` contains 50 `career_string_id` entries.
- `taom_careers.xml` contains 50 `<Career id=...>` entries.
- I did not find a `career_string_id`/`Career id` count mismatch.
- I did not find invalid `rohan` / `dol_guldur` style IDs in `EligibleCultures`; the XML uses valid IDs such as `vlandia`, `empire`, `empire_w`, `empire_s`, and `dolguldur`.
- I did not find an invalid `"guard"` roster fallback for the supported TAOM custom cultures or vanilla XSLT cultures:
  - Custom guard rosters exist in `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml`.
  - Vanilla guard rosters exist in `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/ModuleData/sandbox_equipment_sets.xml`.

## Findings

### HIGH

[HIGH] Main/_Module/ModuleData/charactercreation/cultures.json:143 — Lifecycle completeness — `shaghana` and `abanissa` are still selectable character-creation cultures (`cultures.json:143`, `:153`), but `taom_careers.xml` defines no careers eligible for either culture. `CareerMenuService` filters options strictly by `SelectedCulture.StringId` (`Main/Features/CharacterCreation/CareerMenuService.cs:170`), vanilla treats an empty narrative menu as advanceable (`CharacterCreationNarrativeStageVM.cs:192-198`), and TAOM's `TrySwitchToNextMenu` patch returns `false` when no option is selected and there is no next menu (`Main/Features/FactionMap/Hooks/TrySwitchToNextMenu_Patch.cs:18-35`). That path finalizes character creation with no `SelectedCareerStringId`, and `AssignCareer()` then also fails its registry fallback because no career matches either culture (`Main/Features/CharacterCreation/CharacterCreationContentService.cs:188-210`). Result: those cultures silently bypass the new feature and exit CC with no career assigned. Fix: either remove `shaghana`/`abanissa` from CC until they have careers, or add eligible careers and a test that guarantees every selectable CC culture has at least one career option.

### MEDIUM

[MEDIUM] TAOM.Tests/Features/CharacterCreation/CareerMenuServiceTests.cs:141 — ADR-008 / test correctness — `RegisterCareerMenu_ClearsStaleSelection` does not call `RegisterCareerMenu()` at all. It sets stale state, then asserts that a brand-new service instance starts null (`:149-153`). That does not verify the singleton leak fix at `CareerMenuService.RegisterCareerMenu()` line 68, so the regression described in the prompt would still ship if that reset were removed or bypassed. Fix: exercise `RegisterCareerMenu()` itself with a real/fake `CharacterCreationManager`, or cover the full CC lifecycle with a test that proves stale selection is cleared on re-entry.

## Observations

### INFO

- Menu insertion after adulthood is correct. `CareerMenuService` uses `InputMenuId = "narrative_adulthood_menu"` and vanilla traverses by matching `InputMenuId` to the current menu `StringId`.
- Career bonuses are applied correctly when a career is selected. `NarrativeMenuOption.OnSelect()` populates `Args`, and vanilla `ApplyFinalEffects()` applies every selected option's `Args` before TAOM finalization.
- Finalization order is safe for culture-dependent assignment. Vanilla applies culture before handler finalization, and TAOM also force-sets `Hero.MainHero.Culture` before `AssignCareer()`.
- The `"guard"` equipment fallback exists for TAOM custom cultures in `taom_char_creation_equipment.xml` and for vanilla cultures in SandBox's `sandbox_equipment_sets.xml`.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 0
VERDICT: ISSUES FOUND
