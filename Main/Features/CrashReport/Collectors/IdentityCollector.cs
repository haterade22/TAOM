using System;
using System.Diagnostics;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class IdentityCollector
{
    public IdentitySnapshot Collect(string originatingPatchTarget)
    {
        string blVersion = SafeRead(() => ModuleHelper.GetModuleInfo("Native")?.Version.ToString()) ?? "(unknown)";
        string blExeVersion = SafeRead(GetBannerlordExeFileVersion) ?? "(unknown)";
        string taomVersion = SafeRead(() => ModuleHelper.GetModuleInfo("TAOM")?.Version.ToString()) ?? "(unknown)";
        string taomDllSha1 = SafeRead(() => DllHasher.Sha1OfFile(typeof(IdentityCollector).Assembly.Location)) ?? "(unknown)";
        string language = SafeRead(() => BannerlordConfig.Language) ?? "(unknown)";

        return new IdentitySnapshot(
            BannerlordVersion: blVersion,
            BannerlordExeFileVersion: blExeVersion,
            TaomVersion: taomVersion,
            TaomDllSha1: taomDllSha1,
            OriginatingPatchTarget: originatingPatchTarget ?? "(unknown)",
            LanguageCode: language);
    }

    private static string? GetBannerlordExeFileVersion()
    {
        try
        {
            var mod = Process.GetCurrentProcess().MainModule;
            var path = mod?.FileName;
            if (string.IsNullOrEmpty(path)) return null;
            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch { return null; }
    }

    private static T? SafeRead<T>(Func<T?> f) where T : class
    {
        try { return f(); } catch { return null; }
    }
}
