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

            // Site 1: MBSubModuleBase ctors (catches base() chain + field-init exceptions)
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

            // Site 2: Module.AddSubModule (catches derived ctor body exceptions wrapped in TargetInvocationException)
            try
            {
                DiagLog.Log(Tag, "Install: finding TaleWorlds.MountAndBlade.Module via reflection");
                var moduleType = ReflectionUtils.FindTypeAcrossLoadedAssemblies(
                    "TaleWorlds.MountAndBlade.Module");
                DiagLog.Log(Tag, $"Install: moduleType={moduleType?.FullName ?? "(null)"}");
                if (moduleType != null)
                {
                    var addSubModule = moduleType.GetMethod("AddSubModule",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    DiagLog.Log(Tag, $"Install: addSubModule={addSubModule?.Name ?? "(null)"}");
                    if (addSubModule != null)
                    {
                        try
                        {
                            DiagLog.Log(Tag, "Install: patching Module.AddSubModule");
                            harmony.Patch(addSubModule, finalizer: new HarmonyMethod(sharedFinalizer));
                            installed++;
                            DiagLog.Log(Tag, "Install: Module.AddSubModule patched");
                        }
                        catch (Exception ex)
                        {
                            DiagLog.LogCaught(Tag, "Patch Module.AddSubModule", ex);
                        }
                    }
                    else
                    {
                        DiagLog.Log(Tag, "Install: Module.AddSubModule not found via reflection (signature may have changed)");
                    }
                }
                else
                {
                    DiagLog.Log(Tag, "Install: TaleWorlds.MountAndBlade.Module not loaded yet; skipping AddSubModule patch");
                }
            }
            catch (Exception ex)
            {
                DiagLog.LogCaught(Tag, "Install/Module.AddSubModule", ex);
            }

            DiagLog.Log(Tag, $"Install: COMPLETE — Finalizer on {installed} construction site(s)");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Install", ex);
        }
    }

    /// <summary>
    /// Shared Finalizer for both patch sites. Unwraps TargetInvocationException (used
    /// by Module.AddSubModule's `constructor.Invoke` call), attributes the failure to
    /// the offending submodule's assembly, and swallows non-TAOM exceptions.
    ///
    /// Codex S5 HIGH fix 2026-05-27: AddSubModule attribution now uses the patch
    /// site's own arguments (subModuleInfo + subModuleAssembly) instead of
    /// ex.TargetSite. Vanilla AddSubModule does
    ///   constructor.Invoke(new object[0])
    /// where constructor comes from subModuleAssembly.GetType(subModuleInfo.
    /// SubModuleClassTypeName). When a third-party ctor body throws via a TaleWorlds
    /// API (e.g. MBObjectManager.GetObject<T>("missing")), ex.TargetSite is the
    /// throwing TaleWorlds method, NOT the third-party ctor. The TargetSite-based
    /// attribution would mis-blame TaleWorlds and swallow exceptions that should be
    /// surfaced. The (subModuleInfo, subModuleAssembly) args are the authoritative
    /// source of "whose ctor failed".
    /// </summary>
    private static Exception? SwallowFinalizer(object __instance, object[] __args, Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            // Unwrap TargetInvocationException to get the real inner ctor exception.
            var ex = __exception;
            while (ex is TargetInvocationException && ex.InnerException != null)
                ex = ex.InnerException;

            string asmName;
            string declTypeName;

            if (__instance is MBSubModuleBase subMod)
            {
                // Site 1 — MBSubModuleBase ctor finalizer. __instance is the derived
                // SubModule itself; direct type read is authoritative.
                var t = subMod.GetType();
                declTypeName = t.FullName ?? "(?)";
                asmName = t.Assembly.GetName().Name ?? "(unknown)";
            }
            else if (TryResolveAddSubModuleTarget(__args, out asmName, out declTypeName))
            {
                // Site 2 — Module.AddSubModule finalizer. Resolved via vanilla args.
            }
            else
            {
                // Both attribution strategies failed — last resort.
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

    /// <summary>
    /// Reads Module.AddSubModule's arguments to find the SubModule class type that
    /// failed to construct. Vanilla signature:
    ///   AddSubModule(SubModuleInfo subModuleInfo, Assembly subModuleAssembly)
    /// args[0] = SubModuleInfo (.SubModuleClassTypeName -> string)
    /// args[1] = Assembly (.GetType(className) -> Type)
    /// SubModuleInfo's type is in TaleWorlds.ModuleManager; we don't reference it
    /// directly to avoid binding to a specific version — reflective read via
    /// .GetType().GetProperty("SubModuleClassTypeName").
    /// </summary>
    private static bool TryResolveAddSubModuleTarget(object[]? args, out string asmName, out string declTypeName)
    {
        asmName = "(unknown)";
        declTypeName = "(unknown)";
        if (args == null || args.Length < 2) return false;

        try
        {
            if (!(args[1] is Assembly subModuleAssembly)) return false;
            var subModuleInfo = args[0];
            if (subModuleInfo == null) return false;

            var classNameProp = subModuleInfo.GetType().GetProperty("SubModuleClassTypeName");
            var className = classNameProp?.GetValue(subModuleInfo) as string;
            if (string.IsNullOrEmpty(className))
            {
                // Couldn't extract class name — fall back to assembly attribution only.
                asmName = subModuleAssembly.GetName().Name ?? "(unknown)";
                declTypeName = "(unknown ctor in " + asmName + ")";
                return true;
            }

            var type = subModuleAssembly.GetType(className, throwOnError: false);
            if (type == null)
            {
                asmName = subModuleAssembly.GetName().Name ?? "(unknown)";
                declTypeName = className;
                return true;
            }

            asmName = type.Assembly.GetName().Name ?? "(unknown)";
            declTypeName = type.FullName ?? className;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
