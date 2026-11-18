using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;

namespace TAOM.Dependencies;

/// <summary>
/// Engine boundary for <see cref="GameVersionCheck"/>: what happens when the game is not the Bannerlord
/// version TAOM was built for.
///
/// Engine facts (Module.LoadSubModules, installed v1.5.7): the engine loads every module's DLLs and
/// constructs every MBSubModuleBase in load order, and runs OnSubModuleLoad only after that loop. A
/// TAOM.Client that fails there is a CriticalError: one "Error while loading TAOM" box, then
/// MBDebug.AbortGame and a throw, so the game never reaches the main menu. TAOM.Dependencies loads
/// first, so its SubModule constructor runs before any other TAOM submodule loads and before the engine's
/// box. ModuleHelper is filled before the loop (Module.Initialize calls InitializeModules first).
///
/// On a mismatch this turns TAOM's code off for the session: a prefix on
/// Module.CheckIfSubmoduleCanBeLoadable, which the same loop asks for every submodule before it loads
/// its DLL, answers false for the submodules of TAOM and of every module that depends on TAOM. The game
/// then reaches the main menu and <see cref="MissingTaomNotice"/> shows the TAOM popup there. TAOM's XML
/// is still registered (the loop applies every module's XML list first), so the popup's only button
/// closes the game: a save made without TAOM's code would be damaged. If the prefix can't be installed, a native message box (the call
/// the engine uses for its own box) says the same before the engine's box.
///
/// Not on a dedicated server, and not in the Modding Kit. The engine's own box also checks MBDebug.TestModeEnabled, but
/// /runTest sets that flag only after the submodules load (Module.cs:315), so it is always false here. Runs once per
/// process. Never throws: a throw here would make the engine fail TAOM.Dependencies itself.
/// </summary>
internal static class GameVersionNotice
{
    private const string Tag = "GameVersionNotice";
    private const string HarmonyId = "TAOM.Dependencies.GameVersionGuard";

    // The flags the engine passes for its own "Error while loading" box (Module.LoadSubModules).
    private const uint EngineErrorBoxFlags = 256u;

    private static int _done;
    private static HashSet<SubModuleInfo>? _skipped;

    /// <summary>Set when TAOM's code was turned off; <see cref="MissingTaomNotice"/> shows it at the main menu.</summary>
    internal static string? MenuText { get; private set; }

    /// <summary>From the SubModule constructor. Catches everything, including a failed JIT of the work method.</summary>
    internal static void ShowOnceIfMismatched()
    {
        if (System.Threading.Interlocked.Exchange(ref _done, 1) != 0) return;
        try { Run(); }
        catch (Exception ex) { DiagLog.LogCaught(Tag, "ShowOnceIfMismatched", ex); }
    }

    // NoInlining: every engine reference sits here, so a member missing on another game version fails
    // this method's JIT inside the caller's try instead of failing the constructor.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        if (GameNetwork.IsDedicatedServer)
        {
            DiagLog.Log(Tag, "skipped: dedicated server");
            return;
        }

        // The Modding Kit loads TAOM.Dependencies too, and its engine can lag the client's: turning TAOM's
        // code off there would break the Kit. IncompatibleModDetector.RunEarlyPhase skips it the same way.
        if (IncompatibleModDetector.IsEditorBin(typeof(GameVersionNotice).Assembly.Location))
        {
            DiagLog.Log(Tag, "skipped: Modding Kit");
            return;
        }

        var modules = ModuleHelper.GetModules();
        var taom = modules.FirstOrDefault(m => IsTaom(m.Id));
        if (taom == null)
        {
            DiagLog.Log(Tag, "skipped: TAOM is not in the loaded module list");
            return;
        }

        var subModulePath = Path.Combine(taom.FolderPath, "SubModule.xml");
        var (pin, taomVersion) = GameVersionCheck.ReadSubModule(File.Exists(subModulePath) ? File.ReadAllText(subModulePath) : null);
        int major = VersionProbe.Major, minor = VersionProbe.Minor, revision = VersionProbe.Revision;
        var match = GameVersionCheck.Evaluate(pin, major, minor, revision);
        DiagLog.Log(Tag, $"game v{major}.{minor}.{revision}, TAOM {taomVersion ?? "?"} pins Native {pin ?? "?"}: {match}");
        LoadErrorHint.TryInstall(modules, pin, taomVersion, major, minor, revision, HarmonyId);
        if (match != GameVersionMatch.Mismatch) return;

        EarlyLog.Error($"[TAOM.Dependencies] Wrong Bannerlord version: game v{major}.{minor}.{revision}, TAOM is made for {pin}");
        if (TrySkipTaomCode(modules))
        {
            MenuText = GameVersionCheck.Text(pin!, taomVersion, major, minor, revision, taomSkipped: true);
            return;
        }

        TaleWorlds.Library.Debug.ShowMessageBox(
            GameVersionCheck.Text(pin!, taomVersion, major, minor, revision, taomSkipped: false),
            GameVersionCheck.Caption, EngineErrorBoxFlags);
    }

    private static bool IsTaom(string id) =>
        string.Equals(id, MissingTaomCheck.TaomModuleId, StringComparison.OrdinalIgnoreCase);

    /// <summary>TAOM and every module that depends on it: the guard's skip list and <see cref="LoadErrorHint"/>'s owners.</summary>
    internal static List<ModuleInfo> OwnerModules(List<ModuleInfo> modules) =>
        modules.Where(m => IsTaom(m.Id) || m.DependedModules.Any(d => IsTaom(d.ModuleId))).ToList();

    private static bool TrySkipTaomCode(List<ModuleInfo> modules)
    {
        try
        {
            var owners = OwnerModules(modules);
            _skipped = new HashSet<SubModuleInfo>(owners.SelectMany(m => m.SubModules));
            var target = AccessTools.Method(typeof(Module), nameof(Module.CheckIfSubmoduleCanBeLoadable));
            if (target == null || _skipped.Count == 0)
            {
                DiagLog.Log(Tag, $"TrySkipTaomCode: target={target != null}, submodules={_skipped.Count}; showing the box instead");
                return false;
            }

            new Harmony(HarmonyId).Patch(target, prefix: new HarmonyMethod(typeof(GameVersionNotice), nameof(SkipTaomSubModule)));
            DiagLog.Log(Tag, $"TAOM's code is off for this session: {_skipped.Count} submodule(s) of {string.Join(",", owners.Select(m => m.Id))}");
            return true;
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "TrySkipTaomCode", ex);
            return false;
        }
    }

    /// <summary>Prefix on Module.CheckIfSubmoduleCanBeLoadable: false for TAOM's submodules, so the engine never loads their DLLs.</summary>
    private static bool SkipTaomSubModule(SubModuleInfo subModuleInfo, ref bool __result)
    {
        if (_skipped == null || subModuleInfo == null || !_skipped.Contains(subModuleInfo)) return true;
        __result = false;
        return false;
    }
}
