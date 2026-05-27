using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Harmony Finalizer around <see cref="MBSubModuleBase"/>'s constructor. When a
/// third-party mod's SubModule ctor throws (e.g., because it references a removed
/// vanilla API), Bannerlord's launcher would normally show the "couldn't construct
/// X.SubModule" popup and fail to start. This shield swallows the exception, logs
/// it via DiagLog with the offending module's declaring type, and lets the launcher
/// continue past the failed SubModule.
///
/// Refuses to shield TAOM-owned SubModules (let our own errors propagate normally
/// during development).
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports
/// BetaDeps.Foundation.SubModuleConstructionGuard.
/// </summary>
public static class SubModuleConstructionGuard
{
    private const string Tag = "SubModuleConstructionGuard";
    private const string HarmonyId = "TAOM.Dependencies.Foundation.SubModuleConstructionGuard";

    private static int _installed;

    public static void Install()
    {
        if (Interlocked.CompareExchange(ref _installed, 1, 0) != 0) return;

        try
        {
            var harmony = new Harmony(HarmonyId);
            var ctors = typeof(MBSubModuleBase).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ctors.Length == 0)
            {
                DiagLog.Log(Tag, "no MBSubModuleBase ctors found via reflection; aborting install");
                return;
            }

            var finalizer = typeof(SubModuleConstructionGuard).GetMethod(
                nameof(CtorFinalizer),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (finalizer == null)
            {
                DiagLog.Log(Tag, "could not resolve CtorFinalizer; aborting install");
                return;
            }

            int installed = 0;
            foreach (var ctor in ctors)
            {
                try
                {
                    harmony.Patch(ctor, prefix: null, postfix: null, transpiler: null,
                        finalizer: new HarmonyMethod(finalizer));
                    installed++;
                }
                catch (Exception ex)
                {
                    DiagLog.LogCaught(Tag, "Patch ctor", ex);
                }
            }
            DiagLog.Log(Tag, $"installed Finalizer on {installed} MBSubModuleBase ctor(s)");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Install", ex);
        }
    }

    private static Exception? CtorFinalizer(object __instance, Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            var declType = __instance?.GetType();
            var asmName = declType?.Assembly.GetName().Name ?? "(unknown)";

            // Don't shield TAOM-owned SubModule failures — we want to see those.
            if (asmName.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
                return __exception;

            DiagLog.Log(Tag,
                $"swallowed {__exception.GetType().Name} during {declType?.FullName ?? "(?)"} ctor " +
                $"(from {asmName}): {__exception.Message}");
            return null;  // swallow
        }
        catch
        {
            return __exception;  // re-throw on internal failure
        }
    }
}
