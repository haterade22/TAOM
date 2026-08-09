namespace TAOM.Features.Enlistment;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the gated call sites can be reasoned about (and the fail
/// direction unit-tested) without MCM loaded. <c>TaomSettings.Instance</c> is null whenever MCM is
/// absent or not yet initialized, so the setting falls back to its compiled default — off since
/// 2026-08-09. A player without MCM therefore gets the quiet log, which is the right posture for
/// the fallback: a missing settings host should not opt someone into a ten-times-louder trace.
///
/// The static is read on EVERY call rather than cached at construction, which is what makes the MCM
/// property honestly <c>RequireRestart = false</c>: flipping the toggle mid-session takes effect on
/// the next log site, with no restart and no re-resolution of the singleton provider.
/// </summary>
public sealed class EnlistmentDiagnosticsSettingsProvider : IEnlistmentDiagnosticsSettingsProvider
{
    /// <summary>
    /// Pure seam so the fail direction is testable without touching the MCM static. The <c>?? false</c>
    /// must always match the compiled default of <c>TaomSettings.EnableEnlistmentDiagnostics</c> —
    /// see the interface doc. Both are pinned; flip them together or not at all.
    /// </summary>
    internal static bool ResolveEnabled(bool? raw) => raw ?? false;

    public bool IsEnabled => ResolveEnabled(TaomSettings.Instance?.EnableEnlistmentDiagnostics);
}
