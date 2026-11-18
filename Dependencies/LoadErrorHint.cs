using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;

namespace TAOM.Dependencies;

/// <summary>
/// Adds <see cref="GameVersionCheck.LoadErrorHint"/> to the engine's own "Error while loading TAOM" box.
///
/// Engine facts (installed v1.5.7, Module.cs:1172-1216): when a submodule DLL fails to load, HandleSubmoduleLoadError
/// builds the box text with the private Module.GetAssemblyLoadResultsMessage(module, ...), which is hardcoded English
/// with no language key. On a critical error the caller adds "Please update or disable this mod..." after it; the
/// non-critical "dependency conflict" box (a failed <c>Assemblies</c> entry) gets the hint too. So a postfix on that method
/// is the one place where TAOM can put the usual cause, the wrong game version, into the box.
///
/// <see cref="GameVersionNotice"/> installs this on every client start, also when the version check says Match or
/// Unknown: a stale TAOM.Dependencies (#371) or a mixed install still reaches the engine box. Only for TAOM and the
/// modules that depend on it. Never throws: a throw in the installer would fail TAOM.Dependencies itself, and a
/// throw in the postfix would replace the engine's box with a crash.
/// </summary>
internal static class LoadErrorHint
{
    private const string Tag = "LoadErrorHint";

    private static HashSet<string> _owners = new(StringComparer.OrdinalIgnoreCase);
    private static string? _hint;
    private static bool _appended;

    internal static void TryInstall(List<ModuleInfo> modules, string? pin, string? taomVersion, int major, int minor, int revision, string harmonyId)
    {
        try
        {
            _hint = GameVersionCheck.LoadErrorHint(pin, taomVersion, major, minor, revision);
            _owners = new HashSet<string>(GameVersionNotice.OwnerModules(modules).Select(m => m.Id), StringComparer.OrdinalIgnoreCase);
            var target = AccessTools.Method(typeof(Module), "GetAssemblyLoadResultsMessage");
            if (target == null || _owners.Count == 0)
            {
                DiagLog.Log(Tag, $"not installed: target={target != null}, modules={_owners.Count}");
                return;
            }

            new Harmony(harmonyId).Patch(target, postfix: new HarmonyMethod(typeof(LoadErrorHint), nameof(AppendHint)));
            DiagLog.Log(Tag, $"installed for {string.Join(",", _owners)}");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "TryInstall", ex);
        }
    }

    /// <summary>
    /// Postfix on Module.GetAssemblyLoadResultsMessage: one blank line, the hint, one line break. Once per process: the
    /// engine joins one message per failed submodule into one box (Module.cs:1144), and TAOM.Client fails with TAOM.dll.
    /// </summary>
    private static void AppendHint(ModuleInfo module, ref string __result)
    {
        try
        {
            if (_hint == null || module == null || !_owners.Contains(module.Id)) return;
            if (_appended) return;
            _appended = true;
            __result = __result + Environment.NewLine + _hint + Environment.NewLine;
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "AppendHint", ex);
        }
    }
}
