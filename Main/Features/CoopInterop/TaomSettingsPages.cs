namespace TAOM.Features.CoopInterop;

/// <summary>
/// Every settings page TAOM ships, in one list. The fingerprint log and TAOM.CoopCompat's join check
/// both hash this list, so a page added here is covered in both places.
/// </summary>
/// <remarks>
/// An entry is null while MCM has not built that page yet; <see cref="SettingsFingerprint.ComputeAcross"/>
/// counts it as unavailable. On a dedicated server each page reads taom-server-settings.json.
/// </remarks>
public static class TaomSettingsPages
{
    public static object?[] All() => new object?[]
    {
        TaomSettings.Instance,
        BattleLoadDiagnostics.BattleLoadDiagnosticsSettings.Instance,
        BlowDiagnostics.BlowDiagnosticsSettings.Instance,
        CrashReport.CrashReportSettings.Instance,
    };
}
