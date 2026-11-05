using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.LocalizationOverride.Hooks;

/// <summary>
/// Patches MBTextManager.GetLocalizedText to allow English string overrides.
///
/// Vanilla GetLocalizedText short-circuits for English: it returns the inline text
/// from the {=ID}text value and never checks LocalizedTextManager. This means
/// module_strings.xml entries that reuse vanilla {=ID} tokens are silently ignored.
///
/// This prefix intercepts the call, extracts the {=ID}, and returns our override
/// text if one is registered, making the overrides in taom_module_strings.xml
/// actually take effect. It does that only while the text language is English (#706):
/// for any other language vanilla reads that language's own row, so the prefix falls
/// through and the player's translation shows.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch25_LocalizationOverride")]
public static class MBTextManager_GetLocalizedText_Patch
{
    // Keyed by a (string, start, length) slice so the per-call probe reads the id in place instead of
    // allocating it: this prefix runs on every localized text resolve. Ordinal, like the string key it
    // replaces. Written only at module load (SubModule) and in tests.
    private static readonly Dictionary<IdSlice, string> _overrides = new();

    // Decides English or not, and writes the once-per-language-change log line. Replaced only by tests.
    private static OverrideLanguageGate _languageGate = NewGameGate();

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

        // Only English skips vanilla's dictionary; every other language has a row of its own to show.
        if (!_languageGate.AllowsOverrides())
            return true;

        if (_overrides.TryGetValue(new IdSlice(text, 2, idLength), out string overrideText))
        {
            __result = overrideText;
            return false;
        }

        return true;
    }

    public static void RegisterOverride(string id, string text)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));
        _overrides[new IdSlice(id, 0, id.Length)] = text;
    }

    public static void ClearOverrides()
    {
        _overrides.Clear();
    }

    // This feature is static-only and has no IoC registration, so the gate is built here. The logger is
    // resolved inside the lambda, on the rare path that writes a line, never per lookup.
    private static OverrideLanguageGate NewGameGate()
        => new(new TextLocalizerAdapter(), static () => IoC.Resolve<IModLogger>());

    // Test seam: installs a gate over a fake language; null puts back the one the game uses.
    internal static void UseLanguageGate(OverrideLanguageGate? gate)
    {
        _languageGate = gate ?? NewGameGate();
    }
}
