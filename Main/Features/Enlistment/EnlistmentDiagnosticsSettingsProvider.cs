namespace TAOM.Features.Enlistment;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the gated call sites can be reasoned about (and the fail
/// direction unit-tested) without MCM loaded. <c>TaomSettings.Instance</c> is null whenever MCM is
/// absent or not yet initialized, so the setting falls back to its compiled default — off from
/// 2026-08-09, and back on TEMPORARILY from 2026-08-28 for the #520 in-game smoke.
///
/// While the trace is on by default, a player without MCM gets it too, and that is the correct
/// pairing rather than an oversight: the fallback must encode the SAME posture as the compiled
/// default, or MCM-absent and MCM-present-at-default behaviour diverge in a way no test host can
/// see. When #520's smoke is done, both flip back together.
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
    internal static bool ResolveEnabled(bool? raw) => raw ?? true;

    public bool IsEnabled => ResolveEnabled(TaomSettings.Instance?.EnableEnlistmentDiagnostics);
}
