using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;

namespace TAOM.Dependencies;

/// <summary>
/// Pre-Native module that provides Harmony (forked) and UIExtenderEx patches.
/// Must load before Native so patches are in place before any prefabs/brushes are loaded.
/// </summary>
public class SubModule : MBSubModuleBase
{
    private const string GuardHarmonyId = "TAOM.HarmonyGuard";

    static SubModule()
    {
        InstallAssemblyResolveHandler();

        EarlyLog.Info("[TAOM.Dependencies] Static init: loading TaleWorlds.Engine.GauntletUI");
        Assembly.Load("TaleWorlds.Engine.GauntletUI");

        UIConfig.DoNotUseGeneratedPrefabs = true;
        EarlyLog.Info("[TAOM.Dependencies] Static init: DoNotUseGeneratedPrefabs = true");
    }

    /// <summary>
    /// BetaDeps parity (DR3 Phase 4 — 2026-05-25): list expanded from 4 to 22 simple
    /// names so that any third-party mod's bundled Microsoft.Extensions.* / Serilog /
    /// MonoMod.* / System.* copy gets redirected to OUR loaded version. Without this,
    /// a consumer mod that ships its own Newtonsoft.Json v12 could shadow ours at JIT
    /// time and break serialisation. List mirrors BetaDeps.Foundation.AssemblyVersionShim.
    /// </summary>
    private static readonly string[] RedirectedSimpleNames =
    {
        // BUTR stack (original 4)
        "0Harmony",
        "MCMv5",
        "Bannerlord.UIExtenderEx",
        "Bannerlord.ButterLib",
        "Bannerlord.Harmony",
        // Microsoft.Extensions family (ButterLib's DI substrate)
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Primitives",
        // Logging
        "Serilog",
        "Serilog.Extensions.Logging",
        // Harmony's transitive deps (if they ship as separate DLLs alongside Lib.Harmony 2.4.2)
        "Mono.Cecil",
        "MonoMod.Core",
        "MonoMod.Utils",
        // System.* polyfills (NET 4.7.2 sometimes resolves these differently across mods)
        "System.Buffers",
        "System.Memory",
        "System.Numerics.Vectors",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Threading.Tasks.Extensions",
        "System.ValueTuple",
        // JSON
        "Newtonsoft.Json",
    };

    private static int _assemblyResolveInstalled;

    /// <summary>
    /// Installs the AssemblyResolve handler exactly once across the AppDomain. Safe
    /// to call from anywhere; subsequent calls are no-ops. Used by both the
    /// SubModule static cctor (when TAOM.Dependencies loads in normal order) AND by
    /// the four AliasStubSubModule ctors (which fire earlier when the stub modules
    /// are constructed before our main SubModule). BetaDeps parity (DR3 Phase 4).
    /// </summary>
    public static void InstallAssemblyResolveHandler()
    {
        DiagLog.Log("Dependencies", "InstallAssemblyResolveHandler: entered");
        if (Interlocked.CompareExchange(ref _assemblyResolveInstalled, 1, 0) != 0)
        {
            DiagLog.Log("Dependencies", "InstallAssemblyResolveHandler: already installed, returning");
            return;
        }
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += RedirectBundledDependencies;
            DiagLog.Log("Dependencies", $"InstallAssemblyResolveHandler: hook registered for {RedirectedSimpleNames.Length} simple names");
            EarlyLog.Info($"[TAOM.Dependencies] AssemblyResolve installed for {RedirectedSimpleNames.Length} simple names");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught("Dependencies", "InstallAssemblyResolveHandler", ex);
            EarlyLog.Error($"[TAOM.Dependencies] InstallAssemblyResolveHandler failed: {ex.Message}");
        }
    }

    private static Assembly? RedirectBundledDependencies(object sender, ResolveEventArgs args)
    {
        var requested = new AssemblyName(args.Name);
        foreach (var simpleName in RedirectedSimpleNames)
        {
            if (!string.Equals(requested.Name, simpleName, StringComparison.Ordinal))
                continue;

            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.Ordinal))
                {
                    EarlyLog.Info($"[TAOM.Dependencies] AssemblyResolve: redirecting '{args.Name}' to loaded '{loaded.FullName}'");
                    return loaded;
                }
            }
        }
        return null;
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        DiagLog.Log("Dependencies", "OnSubModuleLoad: entered");
        EarlyLog.Info($"[TAOM.Dependencies] Harmony forked v{typeof(Harmony).Assembly.GetName().Version} loaded from {typeof(Harmony).Assembly.GetName().Name}");
        DiagLog.Log("Dependencies", $"OnSubModuleLoad: Harmony assembly = {typeof(Harmony).Assembly.GetName().Name} v{typeof(Harmony).Assembly.GetName().Version}");

        try
        {
            DiagLog.Log("Dependencies", "OnSubModuleLoad: applying Harmony guards");
            ApplyHarmonyGuards();
            DiagLog.Log("Dependencies", "OnSubModuleLoad: Harmony guards applied OK");
            EarlyLog.Info("[TAOM.Dependencies] UnpatchAll guard applied");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught("Dependencies", "OnSubModuleLoad/ApplyHarmonyGuards", ex);
            EarlyLog.Error($"[TAOM.Dependencies] Failed to apply Harmony guards: {ex.Message}");
        }

        DiagLog.Log("Dependencies", "OnSubModuleLoad: checking for duplicate Harmony");
        CheckForDuplicateHarmony();

        try
        {
            // Codex review 2026-05-22 (P1): `_ = typeof(UIExtender)` only fetches the Type
            // object — it does NOT execute the class's static constructor where
            // UIConfigPatch.Patch / ViewModelPatch.Patch / etc. are applied.
            // RunClassConstructor forces the static cctor to run (idempotent — JIT
            // marks the type as initialized after the first call).
            DiagLog.Log("Dependencies", "OnSubModuleLoad: forcing UIExtenderEx static cctor");
            RuntimeHelpers.RunClassConstructor(typeof(Bannerlord.UIExtenderEx.UIExtender).TypeHandle);
            DiagLog.Log("Dependencies", "OnSubModuleLoad: UIExtenderEx static cctor done");
            EarlyLog.Info("[TAOM.Dependencies] UIExtenderEx static cctor executed (system patches applied)");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught("Dependencies", "OnSubModuleLoad/UIExtenderEx", ex);
            EarlyLog.Error($"[TAOM.Dependencies] UIExtenderEx initialization failed: {ex.Message}");
        }

        // DR3 Phase 4 C-series: install ALL the defensive shields here. Originally
        // CollectAssemblyTypesShim + SubModuleConstructionGuard were called from
        // AliasStubSubModule.ctor, but observed 2026-05-27 — stub ctors are not
        // reliably reached (BLSE / launcher behavior unknown). OnSubModuleLoad fires
        // for TAOM.Dependencies's main SubModule deterministically, so all shields
        // install from one known-working hook. PatchShield + SaveShield were already
        // here; CollectAssemblyTypesShim + SubModuleConstructionGuard moved here.
        DiagLog.Log("Dependencies", "OnSubModuleLoad: installing defensive shields");

        try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → CollectAssemblyTypesShim.Install"); CollectAssemblyTypesShim.Install(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "CollectAssemblyTypesShim.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] CollectAssemblyTypesShim.Install failed: {ex.Message}"); }

        try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SubModuleConstructionGuard.Install"); SubModuleConstructionGuard.Install(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "SubModuleConstructionGuard.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] SubModuleConstructionGuard.Install failed: {ex.Message}"); }

        try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }

        try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SaveShield.Install"); SaveShield.Install(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "SaveShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] SaveShield.Install failed: {ex.Message}"); }

        // Trigger VersionProbe explicitly so the version is logged. Without this,
        // VersionProbe's lazy-detect via Major/Minor getters never fires (no consumer
        // touches it today). Observed 2026-05-27 — version probe silent in diag.log.
        try
        {
            DiagLog.Log("Dependencies", "OnSubModuleLoad: → VersionProbe (triggering detection)");
            var detected = VersionProbe.IsDetected;
            DiagLog.Log("Dependencies", $"OnSubModuleLoad: VersionProbe.IsDetected={detected} (Major={VersionProbe.Major}, Minor={VersionProbe.Minor}, Revision={VersionProbe.Revision})");
        }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "VersionProbe trigger", ex); }

        // Write a session summary to diag.log on process exit so users can see the
        // shield's swallow-counts even if no crash dump is produced.
        try
        {
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            {
                try { PatchShield.WriteSessionSummary(); } catch { }
            };
            DiagLog.Log("Dependencies", "OnSubModuleLoad: ProcessExit hook for session summary registered");
        }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "ProcessExit hook", ex); EarlyLog.Error($"[TAOM.Dependencies] ProcessExit hook failed: {ex.Message}"); }

        DiagLog.Log("Dependencies", "OnSubModuleLoad: complete");
        EarlyLog.Info("[TAOM.Dependencies] OnSubModuleLoad complete");
    }

    /// <summary>
    /// Called when the main menu has rendered — signals the crash-loop detector that
    /// this session reached menu (deletes the launch marker, snapshots modlist as
    /// last-good). Override of MBSubModuleBase.OnGameInitializationFinished, the
    /// closest TaleWorlds lifecycle hook to "we made it past load."
    /// </summary>
    public override void OnGameInitializationFinished(Game game)
    {
        base.OnGameInitializationFinished(game);
        DiagLog.Log("Dependencies", "OnGameInitializationFinished: entered (main menu reached)");

        try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → MarkSessionLaunchSuccessful"); IncompatibleModDetector.MarkSessionLaunchSuccessful(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "MarkSessionLaunchSuccessful", ex); EarlyLog.Error($"[TAOM.Dependencies] MarkSessionLaunchSuccessful failed: {ex.Message}"); }

        // Second PatchShield pass — captures patches registered by mods that hook this
        // lifecycle event (after our OnSubModuleLoad).
        try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }

        DiagLog.Log("Dependencies", "OnGameInitializationFinished: complete");
    }

    private static void ApplyHarmonyGuards()
    {
        var harmony = new Harmony(GuardHarmonyId);
        var unpatchAll = AccessTools.Method(typeof(Harmony), nameof(Harmony.UnpatchAll));
        harmony.Patch(unpatchAll, prefix: new HarmonyMethod(typeof(SubModule), nameof(UnpatchAllGuard)));
    }

    private static bool UnpatchAllGuard(string harmonyID)
    {
        if (harmonyID is null)
        {
            EarlyLog.Error("[TAOM.Dependencies] Blocked UnpatchAll(null) -- would wipe all Harmony patches globally");
            return false;
        }
        return true;
    }

    private static void CheckForDuplicateHarmony()
    {
        var harmonyAssembly = typeof(Harmony).Assembly;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "0Harmony" && asm != harmonyAssembly)
            {
                EarlyLog.Error($"[TAOM.Dependencies] Another 0Harmony.dll detected: {asm.FullName} at {asm.Location}. May cause patching conflicts.");
                return;
            }
        }
        EarlyLog.Info("[TAOM.Dependencies] No duplicate Harmony assemblies detected");
    }
}
