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
        // Only register the container the engine does NOT already provide. The element-type
        // containers used by HoNFormationPreset's fields — Dictionary<string,int>,
        // Dictionary<int,int>, List<string> — are ALL registered by the engine's own
        // SaveableBasicTypeDefiner.DefineContainerDefinitions() (verified via ilspycmd on the
        // installed TaleWorlds.SaveSystem.dll, 2026-06-21). Re-registering them here hits the
        // duplicate branch in SaveableTypeDefiner.ConstructContainerDefinition →
        // Debug.FailedAssert("duplicate definition for ...") at save-system init (assert noise in
        // editor/debug; no-op in shipping). List<HoNFormationPreset> is mod-specific (the SyncData
        // payload type) and must be registered here.
        ConstructContainerDefinition(typeof(List<HoNFormationPreset>));
    }
}
