namespace TAOM.Features.Enlistment;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the gate can be unit-tested without MCM loaded.
/// <c>TaomSettings.Instance</c> is null whenever MCM is absent or not yet initialized, so the
/// setting falls back to its compiled default — on.
///
/// The static is read on EVERY call rather than cached at construction, which is what makes the
/// MCM property honestly <c>RequireRestart = false</c>: flipping the switch mid-session is picked
/// up by the next reconciler tick, with no restart and no re-resolution of this singleton.
/// </summary>
public sealed class EnlistmentFeatureSettingsProvider : IEnlistmentFeatureSettingsProvider
{
    /// <summary>
    /// Pure seam so the fail direction is testable without touching the MCM static. The
    /// <c>?? true</c> must always match the compiled default of
    /// <c>TaomSettings.EnableEnlistment</c> — flip them together or not at all.
    /// </summary>
    internal static bool ResolveEnabled(bool? raw) => raw ?? true;

    public bool IsEnabled => ResolveEnabled(TaomSettings.Instance?.EnableEnlistment);

    /// <summary>Same fail-open reasoning, same must-match-the-compiled-default rule.</summary>
    internal static bool ResolveOfferLeaveOnArrival(bool? raw) => raw ?? true;

    public bool OfferLeaveOnArrival =>
        ResolveOfferLeaveOnArrival(TaomSettings.Instance?.OfferLeaveOnArrival);
}
