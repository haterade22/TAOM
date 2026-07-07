using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// The "every behavior's SyncData was read without throwing" milestone — after this stamp,
// a load failure is past the deserialization pipeline this feature instruments.
[HarmonyPatch(typeof(CampaignBehaviorManager), nameof(CampaignBehaviorManager.LoadBehaviorData))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class CampaignBehaviorManager_LoadBehaviorData_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix()
    {
        try { _service?.LogPhase(SaveLoadPhase.AllBehaviorDataLoaded, string.Empty); }
        catch { /* diagnostic only */ }
    }
}
