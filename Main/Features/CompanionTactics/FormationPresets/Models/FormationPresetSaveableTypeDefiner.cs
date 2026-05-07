using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

/// <summary>
/// Registers <see cref="HoNFormationPreset"/> with the SaveSystem.
///
/// BaseId 726900601 matches the original developer's mod, so existing CompanionTactics
/// saves import without manual migration. Class id 101 — matches source.
///
/// First TAOM use of <see cref="SaveableTypeDefiner"/>; CareerSystem deliberately avoided
/// the pattern via primitive SyncData. <c>FormationPresetCampaignBehavior</c> wraps the
/// SyncData call in try/catch in case BaseId collides with another mod.
/// </summary>
public class FormationPresetSaveableTypeDefiner : SaveableTypeDefiner
{
    public FormationPresetSaveableTypeDefiner() : base(726900601) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(HoNFormationPreset), 101);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<HoNFormationPreset>));
        ConstructContainerDefinition(typeof(Dictionary<string, int>));
        ConstructContainerDefinition(typeof(Dictionary<int, int>));
        ConstructContainerDefinition(typeof(List<string>));
    }
}
