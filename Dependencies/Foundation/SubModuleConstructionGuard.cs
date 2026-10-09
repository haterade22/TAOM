using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Harmony Finalizer on the <see cref="MBSubModuleBase"/> constructors. It catches exceptions
/// thrown during the implicit base() chain or in field initialisers of a third-party SubModule
/// that run before the explicit ctor body; the swallowed constructor then returns normally.
///
/// Bannerlord v1.5.5 (changeset 124170) catches derived-constructor exceptions inside
/// <c>Module.AddSubModule</c> and ends the game with a message box naming the module, so TAOM
/// no longer intercepts that path. The former second site, a finalizer on AddSubModule, was
/// removed because it can no longer see an exception there.
///
/// Refuses to shield TAOM-owned SubModules (let our own errors propagate during dev).
///
/// BetaDeps parity (DR3 Phase 4, 2026-05-27).
/// </summary>
public static class SubModuleConstructionGuard
{
    private const string Tag = "SubModuleConstructionGuard";
    private const string HarmonyId = "TAOM.Dependencies.Foundation.SubModuleConstructionGuard";

    private static int _installed;

    public static void Install()
    {
        DiagLog.Log(Tag, "Install: entered");
        if (Interlocked.CompareExchange(ref _installed, 1, 0) != 0)
        {
            DiagLog.Log(Tag, "Install: already installed, returning");
            return;
        }

        try
        {
            DiagLog.Log(Tag, "Install: constructing Harmony instance");
            var harmony = new Harmony(HarmonyId);
            DiagLog.Log(Tag, "Install: Harmony constructed");

            var sharedFinalizer = typeof(SubModuleConstructionGuard).GetMethod(
                nameof(SwallowFinalizer),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (sharedFinalizer == null)
            {
                DiagLog.Log(Tag, "Install: could not resolve SwallowFinalizer; aborting install");
                return;
            }
            DiagLog.Log(Tag, "Install: SwallowFinalizer resolved");

            int installed = 0;

            // MBSubModuleBase ctors (catches base() chain + field-init exceptions)
            try
            {
                DiagLog.Log(Tag, "Install: enumerating MBSubModuleBase ctors via reflection");
                var baseCtors = typeof(MBSubModuleBase).GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                DiagLog.Log(Tag, $"Install: MBSubModuleBase has {baseCtors.Length} reflectable ctor(s)");
                foreach (var ctor in baseCtors)
                {
                    try
                    {
                        DiagLog.Log(Tag, $"Install: patching MBSubModuleBase ctor ({ctor.GetParameters().Length} args)");
                        harmony.Patch(ctor, finalizer: new HarmonyMethod(sharedFinalizer));
                        installed++;
                        DiagLog.Log(Tag, $"Install: MBSubModuleBase ctor patched");
                    }
                    catch (Exception ex)
                    {
                        DiagLog.LogCaught(Tag, $"Patch MBSubModuleBase ctor ({ctor.GetParameters().Length} args)", ex);
                    }
                }
                if (baseCtors.Length == 0)
                {
                    DiagLog.Log(Tag, "Install: MBSubModuleBase has no reflectable ctors (compiler-generated default skipped)");
                }
            }
            catch (Exception ex)
            {
                DiagLog.LogCaught(Tag, "Install/MBSubModuleBase ctors", ex);
            }

            DiagLog.Log(Tag, $"Install: COMPLETE — Finalizer on {installed} construction site(s)");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Install", ex);
        }
    }

    /// <summary>
    /// Finalizer for the MBSubModuleBase constructors. Attributes the failure to the
    /// offending submodule's assembly (read from the instance being constructed) and
    /// swallows non-TAOM exceptions.
    /// </summary>
    private static Exception? SwallowFinalizer(object __instance, Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            // Unwrap any TargetInvocationException to get the real inner ctor exception.
            var ex = __exception;
            while (ex is TargetInvocationException && ex.InnerException != null)
                ex = ex.InnerException;

            string asmName;
            string declTypeName;

            if (__instance is MBSubModuleBase subMod)
            {
                // __instance is the derived SubModule itself; direct type read is authoritative.
                var t = subMod.GetType();
                declTypeName = t.FullName ?? "(?)";
                asmName = t.Assembly.GetName().Name ?? "(unknown)";
            }
            else
            {
                // __instance is not a SubModule: last resort.
                asmName = "(unknown)";
                declTypeName = ex.TargetSite?.DeclaringType?.FullName ?? "(?)";
            }

            // Don't shield TAOM-owned ctor failures — we want to see those.
            if (asmName.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
                return __exception;

            DiagLog.Log(Tag,
                $"swallowed {ex.GetType().Name} during {declTypeName} ctor " +
                $"(from {asmName}): {ex.Message}");
            return null;  // swallow
        }
        catch
        {
            return __exception;  // re-throw on internal failure
        }
    }
}
