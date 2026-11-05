namespace TAOM.Features.Enlistment;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the gated call sites can be reasoned about (and the fail
/// direction unit-tested) without MCM loaded. <c>TaomSettings.Instance</c> is null whenever MCM is
/// absent or not yet initialized, so the setting falls back to its compiled default: off.
///
/// The trace ships off, and a player without MCM gets it off too: the fallback must encode the SAME
/// posture as the compiled default, or MCM-absent and MCM-present-at-default behaviour diverge in a
/// way no test host can see. The setting was renamed (<c>EnableEnlistmentTrace</c>) when it went back off
/// (#840), because a saved value of the old id would have kept it on.
///
/// The static is read on EVERY call rather than cached at construction, which is what makes the MCM
/// property honestly <c>RequireRestart = false</c>: flipping the toggle mid-session takes effect on
/// the next log site, with no restart and no re-resolution of the singleton provider.
/// </summary>
public sealed class EnlistmentDiagnosticsSettingsProvider : IEnlistmentDiagnosticsSettingsProvider
{
    /// <summary>
    /// Pure seam so the fail direction is testable without touching the MCM static. The <c>?? false</c>
    /// must always match the compiled default of <c>TaomSettings.EnableEnlistmentTrace</c> —
    /// see the interface doc. Both are pinned; flip them together or not at all.
    /// </summary>
    internal static bool ResolveEnabled(bool? raw) => raw ?? false;

    public bool IsEnabled => ResolveEnabled(TaomSettings.Instance?.EnableEnlistmentTrace);
}
