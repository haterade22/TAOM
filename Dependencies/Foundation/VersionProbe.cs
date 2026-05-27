using System;
using System.Reflection;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Detects the installed Bannerlord version via reflection on
/// <c>TaleWorlds.Library.ApplicationVersion.FromParametersFile()</c>, which reads
/// <c>parameters/Version.xml</c> in the game install. Used by DiagLog for incident
/// reports and (future) by IncompatibleModDetector for version-conditional rules.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25, target list corrected 2026-05-27).
/// The original BetaDeps probe referenced
/// <c>TaleWorlds.ModuleManager.ApplicationVersionHelper.GameVersion()</c> — a type
/// that does not exist in v1.4.5. Reverified all reflection targets via ilspycmd
/// against the installed v1.4.5 DLLs:
///   - `TaleWorlds.Library.ApplicationVersion` exists with `Major`/`Minor`/`Revision` properties
///   - `ApplicationVersion.FromParametersFile()` exists (static, optional path arg)
///   - `Module.CurrentModule` exists but has NO `Version` property (was BetaDeps invention)
///
/// Reflective rather than direct-typed so this code survives future API drift without
/// recompiling. All paths exception-safe via TrySwallow.
/// </summary>
public static class VersionProbe
{
    private const string Tag = "VersionProbe";

    private static bool _detected;
    private static int _major;
    private static int _minor;
    private static int _revision;

    public static int Major { get { EnsureDetected(); return _major; } }
    public static int Minor { get { EnsureDetected(); return _minor; } }
    public static int Revision { get { EnsureDetected(); return _revision; } }

    /// <summary>
    /// Returns true if version detection succeeded (Major+Minor were extracted from
    /// the engine reflection target). False on any failure.
    /// </summary>
    public static bool IsDetected { get { EnsureDetected(); return _major > 0; } }

    private static void EnsureDetected()
    {
        if (_detected) return;
        DiagLog.Log(Tag, "EnsureDetected: entered (first call)");
        _detected = true;
        try
        {
            DetectViaApplicationVersion();
            if (_major > 0)
            {
                DiagLog.Log(Tag, $"EnsureDetected: COMPLETE — detected Bannerlord v{_major}.{_minor}.{_revision} via ApplicationVersion.FromParametersFile");
                return;
            }
            DiagLog.Log(Tag, "EnsureDetected: DetectViaApplicationVersion returned but _major == 0");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "EnsureDetected/ApplicationVersion", ex);
        }
        DiagLog.Log(Tag, "EnsureDetected: could not detect Bannerlord version (engine reflection paths exhausted)");
    }

    private static void DetectViaApplicationVersion()
    {
        DiagLog.Log(Tag, "DetectViaApplicationVersion: entered");
        var avType = ReflectionUtils.FindTypeAcrossLoadedAssemblies(
            "TaleWorlds.Library.ApplicationVersion");
        if (avType == null)
        {
            DiagLog.Log(Tag, "DetectViaApplicationVersion: TaleWorlds.Library.ApplicationVersion not loaded; skipping");
            return;
        }
        DiagLog.Log(Tag, $"DetectViaApplicationVersion: avType.FullName={avType.FullName}");

        // FromParametersFile is a static method taking an optional string arg.
        var fromFile = avType.GetMethod("FromParametersFile",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);
        if (fromFile == null)
        {
            DiagLog.Log(Tag, "DetectViaApplicationVersion: FromParametersFile(string) not found, trying parameterless");
            fromFile = avType.GetMethod("FromParametersFile",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
        }
        if (fromFile == null)
        {
            DiagLog.Log(Tag, "DetectViaApplicationVersion: ApplicationVersion.FromParametersFile not found at all");
            return;
        }
        DiagLog.Log(Tag, $"DetectViaApplicationVersion: found FromParametersFile with {fromFile.GetParameters().Length} param(s)");

        object? versionObj;
        try
        {
            DiagLog.Log(Tag, "DetectViaApplicationVersion: invoking FromParametersFile");
            versionObj = fromFile.GetParameters().Length == 1
                ? fromFile.Invoke(null, new object?[] { null })
                : fromFile.Invoke(null, null);
            DiagLog.Log(Tag, $"DetectViaApplicationVersion: FromParametersFile returned {(versionObj == null ? "null" : versionObj.GetType().Name)}");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "ApplicationVersion.FromParametersFile.Invoke", ex);
            return;
        }
        if (versionObj == null)
        {
            DiagLog.Log(Tag, "DetectViaApplicationVersion: FromParametersFile returned null");
            return;
        }

        if (TryExtractIntProperty(versionObj, "Major", out var major)) _major = major;
        if (TryExtractIntProperty(versionObj, "Minor", out var minor)) _minor = minor;
        if (TryExtractIntProperty(versionObj, "Revision", out var revision)) _revision = revision;
        DiagLog.Log(Tag, $"DetectViaApplicationVersion: extracted Major={_major}, Minor={_minor}, Revision={_revision}");
    }

    private static bool TryExtractIntProperty(object instance, string propertyName, out int value)
    {
        value = 0;
        try
        {
            var prop = instance.GetType().GetProperty(propertyName);
            if (prop == null) return false;
            var raw = prop.GetValue(instance);
            if (raw == null) return false;
            value = Convert.ToInt32(raw);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
