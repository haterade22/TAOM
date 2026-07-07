using HarmonyLib;
using TaleWorlds.SaveSystem.Load;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Container sibling of ObjectHeaderLoadData_CreateObject_Patch: LoadContext.Load only
// calls CreateObject when GetObjectTypeDefinition() returns true, so a container whose
// SaveId has no ContainerDefinition in this build (e.g. a List<T> registered by a definer
// the running build lacks) is skipped with NO engine log. Dedup + cap in the service.
[HarmonyPatch(typeof(ContainerHeaderLoadData), nameof(ContainerHeaderLoadData.GetObjectTypeDefinition))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class ContainerHeaderLoadData_GetObjectTypeDefinition_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix(bool __result, ContainerHeaderLoadData __instance)
    {
        var svc = _service;
        if (svc == null || __result) return;
        try { svc.MarkUnknownSaveId("container", __instance.SaveId?.GetStringId() ?? "<null>"); }
        catch { /* diagnostic only */ }
    }
}
