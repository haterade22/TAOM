using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Stamps every future save's metadata with the exact TAOM build that wrote it. The shipped
// module version label can span many commits (v2.0.9 covered 34), which made field triage
// of user saves nearly impossible. MetaData serializes its whole dict to JSON and the
// engine only reads known keys, so an extra key is inert; the indexer setter is an upsert
// (never throws on a duplicate). Read back by SandBoxSaveHelper_TryLoadSave_Patch and
// tools/inspect_sav.py.
[HarmonyPatch(typeof(MBSaveLoad), "GetSaveMetaData")]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class MBSaveLoad_GetSaveMetaData_Patch
{
    internal const string BuildKey = "TAOM_Build";

    private static readonly string BuildStamp = ComputeBuildStamp();

    [HarmonyPostfix]
    public static void Postfix(MetaData? __result)
    {
        try
        {
            if (__result != null)
                __result[BuildKey] = BuildStamp;
        }
        catch { /* diagnostic only */ }
    }

    private static string ComputeBuildStamp()
    {
        try
        {
            var assembly = typeof(MBSaveLoad_GetSaveMetaData_Patch).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var version = assembly.GetName().Version?.ToString() ?? "?";
            return string.IsNullOrEmpty(informational) || informational == version
                ? version
                : $"{version} ({informational})";
        }
        catch
        {
            return "<unavailable>";
        }
    }
}
