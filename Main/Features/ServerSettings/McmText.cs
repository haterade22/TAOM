using System.Text.RegularExpressions;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// MCM texts without their localization keys (#1385 Phase 4). Each part of a settings group path carries its own
/// <c>{=key}</c>, because MCM splits the path on '/' before it translates each part. The co-op fingerprint and the
/// server defaults file name groups in English, so they read the path through <see cref="Plain"/>.
/// </summary>
internal static class McmText
{
    private static readonly Regex Key = new Regex(@"(^|/)\{=[A-Za-z0-9_!]+\}", RegexOptions.Compiled);

    /// <summary>"{=a}Battle Tactics/{=b}Mixed Formations" becomes "Battle Tactics/Mixed Formations".</summary>
    public static string Plain(string? groupPath) =>
        groupPath == null ? "" : Key.Replace(groupPath, "$1");
}
