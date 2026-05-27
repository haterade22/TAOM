using System;
using System.Reflection;

namespace TAOM.Dependencies.Foundation;

public enum GameBranch
{
    Unknown,
    Public,
    Beta,
}

/// <summary>
/// Detects the installed Bannerlord version + branch (public vs beta) via reflection.
/// Used by IncompatibleModDetector to gate beta-vs-public-specific auto-disable rules
/// and by DiagLog for incident reports.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports
/// BetaDeps.Foundation.VersionProbe. Reflection strategy:
///   1. First try TaleWorlds.ModuleManager.ApplicationVersionHelper.GameVersion()
///   2. Fall back to TaleWorlds.MountAndBlade.Module.CurrentModule.Version
/// Reflective because both have changed signatures across Bannerlord versions and we
/// want to survive future API drift without recompiling.
///
/// Branch classification: Bannerlord 1.4+ is beta; 1.0-1.3 is public; anything else is unknown.
/// TaleWorlds may shift this scheme in future releases — re-classify in
/// <see cref="ClassifyBranch"/> if so.
/// </summary>
public static class VersionProbe
{
    private const string Tag = "VersionProbe";

    private static GameBranch? _cachedBranch;
    private static int _cachedMajor;
    private static int _cachedMinor;

    public static GameBranch Branch
    {
        get
        {
            if (!_cachedBranch.HasValue) _cachedBranch = Detect();
            return _cachedBranch.Value;
        }
    }

    public static int Major { get { _ = Branch; return _cachedMajor; } }
    public static int Minor { get { _ = Branch; return _cachedMinor; } }
    public static bool IsBeta => Branch == GameBranch.Beta;
    public static bool IsPublic => Branch == GameBranch.Public;

    private static GameBranch Detect()
    {
        // Strategy 1: ApplicationVersionHelper.GameVersion()
        try
        {
            var helperType = ReflectionUtils.FindTypeAcrossLoadedAssemblies(
                "TaleWorlds.ModuleManager.ApplicationVersionHelper");
            if (helperType != null)
            {
                var versionObj = ReflectionUtils.TryInvokeStatic(helperType, "GameVersion");
                if (versionObj != null && TryExtractMajorMinor(versionObj, out var major, out var minor))
                {
                    _cachedMajor = major;
                    _cachedMinor = minor;
                    DiagLog.Log(Tag, $"detected via ApplicationVersionHelper: v{major}.{minor}");
                    return ClassifyBranch(major, minor);
                }
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Detect/ApplicationVersionHelper", ex);
        }

        // Strategy 2: Module.CurrentModule.Version
        try
        {
            var moduleType = ReflectionUtils.FindTypeAcrossLoadedAssemblies(
                "TaleWorlds.MountAndBlade.Module");
            if (moduleType != null)
            {
                var currentModuleProp = moduleType.GetProperty("CurrentModule", BindingFlags.Static | BindingFlags.Public);
                var currentModule = currentModuleProp?.GetValue(null);
                if (currentModule != null)
                {
                    var versionMember = currentModule.GetType().GetProperty("Version")
                                        ?? currentModule.GetType().GetProperty("ModuleVersion");
                    var versionObj = versionMember?.GetValue(currentModule);
                    if (versionObj != null && TryExtractMajorMinor(versionObj, out var major, out var minor))
                    {
                        _cachedMajor = major;
                        _cachedMinor = minor;
                        DiagLog.Log(Tag, $"detected via Module.CurrentModule.Version: v{major}.{minor}");
                        return ClassifyBranch(major, minor);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Detect/Module.CurrentModule", ex);
        }

        DiagLog.Log(Tag, "could not detect Bannerlord version; branch=Unknown");
        return GameBranch.Unknown;
    }

    private static bool TryExtractMajorMinor(object versionObj, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        try
        {
            var t = versionObj.GetType();
            var majorMember = (MemberInfo?)t.GetProperty("Major") ?? t.GetField("Major");
            var minorMember = (MemberInfo?)t.GetProperty("Minor") ?? t.GetField("Minor");
            if (majorMember is null || minorMember is null) return false;

            var majorVal = majorMember is PropertyInfo mp ? mp.GetValue(versionObj)
                         : majorMember is FieldInfo mf ? mf.GetValue(versionObj) : null;
            var minorVal = minorMember is PropertyInfo np ? np.GetValue(versionObj)
                         : minorMember is FieldInfo nf ? nf.GetValue(versionObj) : null;
            if (majorVal == null || minorVal == null) return false;

            major = Convert.ToInt32(majorVal);
            minor = Convert.ToInt32(minorVal);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static GameBranch ClassifyBranch(int major, int minor)
    {
        if (major < 1) return GameBranch.Unknown;
        // Bannerlord 1.4+ is currently the beta branch (1.4.0 - 1.4.5 as of 2026-05-25).
        // 1.0-1.3.x is the public branch. Anything >= 2 is future / unknown.
        if (major == 1 && minor >= 4) return GameBranch.Beta;
        if (major == 1) return GameBranch.Public;
        return GameBranch.Beta;
    }
}
