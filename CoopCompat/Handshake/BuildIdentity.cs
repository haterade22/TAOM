using System;
using System.Reflection;
using TAOM.CoopCompat.Messages;
using TAOM.Features.CoopInterop;

namespace TAOM.CoopCompat.Handshake;

/// <summary>
/// What a peer says about its own build in a hello: the wire protocol, the TAOM build text and the
/// module version ids (MVID) of TAOM and CoopCompat. A release zip gives every peer the same MVIDs;
/// a dev build with the same version number does not, and Coop's own join check cannot tell.
/// </summary>
public sealed class BuildIdentity
{
    public BuildIdentity(int protocolVersion, string taomVersion, string taomMvid, string coopCompatMvid)
    {
        ProtocolVersion = protocolVersion;
        TaomVersion = taomVersion;
        TaomMvid = taomMvid;
        CoopCompatMvid = coopCompatMvid;
    }

    public int ProtocolVersion { get; }
    public string TaomVersion { get; }
    public string TaomMvid { get; }
    public string CoopCompatMvid { get; }

    /// <summary>True when a peer's build differs from this one in any way that can change the wire or the rules.</summary>
    public bool DiffersFrom(int protocolVersion, string taomVersion, string taomMvid, string coopCompatMvid) =>
        ProtocolVersion != protocolVersion
        || !string.Equals(TaomVersion, taomVersion, StringComparison.Ordinal)
        || !string.Equals(TaomMvid, taomMvid, StringComparison.Ordinal)
        || !string.Equals(CoopCompatMvid, coopCompatMvid, StringComparison.Ordinal);

    /// <summary>The identity of the TAOM and CoopCompat assemblies loaded in this process.</summary>
    public static BuildIdentity FromLoadedAssemblies()
    {
        var taom = typeof(SettingsFingerprint).Assembly;
        var compat = typeof(BuildIdentity).Assembly;
        return new BuildIdentity(CoopCompatProtocol.ProtocolVersion, VersionText(taom), taom.ManifestModule.ModuleVersionId.ToString(),
            compat.ManifestModule.ModuleVersionId.ToString());
    }

    // The assembly version is fixed by design; the informational version carries the per-build stamp.
    private static string VersionText(Assembly assembly)
    {
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return info?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
