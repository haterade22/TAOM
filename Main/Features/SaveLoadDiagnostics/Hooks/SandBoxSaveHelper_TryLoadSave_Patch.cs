using System;
using System.Text;
using HarmonyLib;
using SandBox;
using TaleWorlds.SaveSystem;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Load lifecycle origin — the Load Game screen click. Resets the [SaveLoad] lifecycle and
// dumps the save's identity from its metadata: ApplicationVersion, creation time, character,
// and the full module list + versions. The module dump answers "which TAOM build wrote this
// save" (the shipped 'v2.0.9' label spans 34 commits; the TAOM_Build stamp from
// MBSaveLoad_GetSaveMetaData_Patch appears here for saves written by an instrumented build).
[HarmonyPatch(typeof(SandBoxSaveHelper), nameof(SandBoxSaveHelper.TryLoadSave))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class SandBoxSaveHelper_TryLoadSave_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix(SaveGameFileInfo saveInfo)
    {
        var svc = _service;
        if (svc == null) return;
        try
        {
            svc.BeginLoadAttempt(saveInfo?.Name ?? "<null>");

            var meta = saveInfo?.MetaData;
            if (meta == null)
            {
                svc.LogPhase(SaveLoadPhase.ModuleCheck, "metadata=<null> (unreadable header)");
                return;
            }

            svc.LogPhase(SaveLoadPhase.ModuleCheck,
                $"appVersion='{meta["ApplicationVersion"]}' created='{meta["CreationTime"]}' " +
                $"character='{meta["CharacterName"]}' taomBuild='{meta["TAOM_Build"] ?? "<pre-diagnostics>"}'");
            svc.LogPhase(SaveLoadPhase.ModuleCheck, $"modules=[{FormatModules(meta)}]");
        }
        catch (Exception ex)
        {
            svc.LogFault(SaveLoadPhase.ModuleCheck, "metadata dump failed", ex);
        }
    }

    private static string FormatModules(MetaData meta)
    {
        var moduleList = meta["Modules"];
        if (string.IsNullOrEmpty(moduleList)) return "<none recorded>";

        var sb = new StringBuilder();
        foreach (var id in moduleList.Split(';'))
        {
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(id).Append(':').Append(meta["Module_" + id] ?? "?");
        }
        return sb.ToString();
    }
}
