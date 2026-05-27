using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Harmony Finalizers around TWO sites where third-party SubModule construction can
/// fail in Bannerlord v1.4.5:
///
///   1. <see cref="MBSubModuleBase"/> implicit base constructor — catches exceptions
///      thrown DURING the implicit base() chain or in field initialisers of the
///      derived class that run before the explicit ctor body.
///   2. <c>TaleWorlds.MountAndBlade.Module.AddSubModule(SubModuleInfo, Assembly)</c> —
///      a private method which calls <c>constructor.Invoke(new object[0])</c> on
///      the derived SubModule's ctor (verified via ilspycmd on v1.4.5). This is
///      where exceptions in the derived ctor BODY surface (wrapped in
///      <see cref="TargetInvocationException"/>). The MBSubModuleBase-ctor finalizer
///      alone doesn't catch these because base() has already returned successfully
///      by the time the derived body throws.
///
/// Without site #2, derived ctor body exceptions propagate up and crash the launcher
/// at module construction — exactly what we're trying to prevent.
///
/// Refuses to shield TAOM-owned SubModules (let our own errors propagate during dev).
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-27, v1.4.5 verification follow-up).
/// BetaDeps's original implementation only patched MBSubModuleBase ctors; the
/// AddSubModule patch is a v1.4.5-correctness addition.
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
            var sharedFinalizer = typeof(SubModuleConstructionGuard).GetMethod(
                nameof(SwallowFinalizer),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (sharedFinalizer == null)
            {
                DiagLog.Log(Tag, "could not resolve SwallowFinalizer; aborting install");
                return;
            }

            int installed = 0;

            // Site 1: MBSubModuleBase ctors (catches base() chain + field-init exceptions)
            try
            {
                var baseCtors = typeof(MBSubModuleBase).GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var ctor in baseCtors)
                {
                    try
                    {
                        harmony.Patch(ctor, finalizer: new HarmonyMethod(sharedFinalizer));
                        installed++;
                    }
                    catch (Exception ex)
                    {
                        DiagLog.LogCaught(Tag, $"Patch MBSubModuleBase ctor ({ctor.GetParameters().Length} args)", ex);
                    }
                }
                if (baseCtors.Length == 0)
                {
                    DiagLog.Log(Tag, "MBSubModuleBase has no reflectable ctors (compiler-generated default skipped)");
                }
            }
            catch (Exception ex)
            {
                DiagLog.LogCaught(Tag, "Install/MBSubModuleBase ctors", ex);
            }

            // Site 2: Module.AddSubModule (catches derived ctor body exceptions wrapped in TargetInvocationException)
            try
            {
                var moduleType = ReflectionUtils.FindTypeAcrossLoadedAssemblies(
                    "TaleWorlds.MountAndBlade.Module");
                if (moduleType != null)
                {
                    var addSubModule = moduleType.GetMethod("AddSubModule",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (addSubModule != null)
                    {
                        try
                        {
                            harmony.Patch(addSubModule, finalizer: new HarmonyMethod(sharedFinalizer));
                            installed++;
                        }
                        catch (Exception ex)
                        {
                            DiagLog.LogCaught(Tag, "Patch Module.AddSubModule", ex);
                        }
                    }
                    else
                    {
                        DiagLog.Log(Tag, "Module.AddSubModule not found via reflection (signature may have changed)");
                    }
                }
                else
                {
                    DiagLog.Log(Tag, "TaleWorlds.MountAndBlade.Module not loaded yet; skipping AddSubModule patch");
                }
            }
            catch (Exception ex)
            {
                DiagLog.LogCaught(Tag, "Install/Module.AddSubModule", ex);
            }

            DiagLog.Log(Tag, $"installed Finalizer on {installed} construction site(s)");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Install", ex);
        }
    }

    /// <summary>
    /// Shared Finalizer for both patch sites. Unwraps TargetInvocationException (used
    /// by Module.AddSubModule's `constructor.Invoke` call), logs culprit attribution,
    /// and swallows non-TAOM exceptions.
    /// </summary>
    private static Exception? SwallowFinalizer(object __instance, Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            // Unwrap TargetInvocationException to get the real inner ctor exception.
            var ex = __exception;
            while (ex is TargetInvocationException && ex.InnerException != null)
                ex = ex.InnerException;

            // Attribute to the source assembly. For MBSubModuleBase ctor finalizer,
            // __instance is the derived SubModule itself. For Module.AddSubModule
            // finalizer, __instance is the Module — and the inner exception's stack
            // walks back through the offending ctor.
            string asmName;
            string declTypeName;
            if (__instance is MBSubModuleBase subMod)
            {
                var t = subMod.GetType();
                declTypeName = t.FullName ?? "(?)";
                asmName = t.Assembly.GetName().Name ?? "(unknown)";
            }
            else
            {
                // Module.AddSubModule path — walk the exception stack for the first
                // non-engine frame.
                declTypeName = ex.TargetSite?.DeclaringType?.FullName ?? "(?)";
                asmName = ex.TargetSite?.DeclaringType?.Assembly.GetName().Name ?? "(unknown)";
            }

            // Don't shield TAOM-owned ctor failures.
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
