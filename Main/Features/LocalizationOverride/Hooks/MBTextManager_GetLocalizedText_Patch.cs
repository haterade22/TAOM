using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Localization;

namespace TAOM.Features.LocalizationOverride.Hooks;

/// <summary>
/// Patches MBTextManager.GetLocalizedText to allow English string overrides.
///
/// Vanilla GetLocalizedText short-circuits for English: it returns the inline text
/// from the {=ID}text value and never checks LocalizedTextManager. This means
/// module_strings.xml entries that reuse vanilla {=ID} tokens are silently ignored.
///
/// This prefix intercepts the call, extracts the {=ID}, and returns our override
/// text if one is registered — making the ~120 "The" fixes in taom_module_strings.xml
/// actually take effect.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch25_LocalizationOverride")]
public static class MBTextManager_GetLocalizedText_Patch
{
    private static readonly Dictionary<string, string> _overrides = new();

    static MethodBase TargetMethod()
        => AccessTools.Method(typeof(MBTextManager), "GetLocalizedText");

    [HarmonyPrefix]
    public static bool Prefix(string text, ref string __result)
    {
        if (text == null || text.Length <= 2 || text[0] != '{' || text[1] != '=')
            return true;

        int end = text.IndexOf('}', 2);
        if (end < 0)
            return true;

        int idLength = end - 2;
        if (idLength == 1 && (text[2] == '!' || text[2] == '*'))
            return true;

        string id = text.Substring(2, idLength);

        if (_overrides.TryGetValue(id, out string overrideText))
        {
            __result = overrideText;
            return false;
        }

        return true;
    }

    public static void RegisterOverride(string id, string text)
    {
        _overrides[id] = text;
    }

    public static void ClearOverrides()
    {
        _overrides.Clear();
    }
}
