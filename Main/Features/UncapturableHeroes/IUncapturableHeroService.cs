namespace TAOM.Features.UncapturableHeroes;

/// <summary>
/// All policy for "this hero can never be taken prisoner". Primitives only, so the two Harmony
/// hooks stay thin boundary conversions (ADR-002 / ADR-007).
///
/// Two entry points because the two seams need different things. The battle seam only needs to be
/// told the verdict, because vanilla itself performs the escape (<c>MapEvent</c> falls through to
/// <c>MakeHeroFugitiveAction</c> when capture is skipped). The direct-capture seam has no such
/// fall-through, so it needs the escape performed for it and must know whether that succeeded.
/// </summary>
public interface IUncapturableHeroService
{
    /// <summary>
    /// The verdict alone: is this hero protected, and is the feature on? Performs no mutation and
    /// shows nothing. Used by the battle seam, which only has to deny the capture.
    /// </summary>
    bool ShouldDenyCapture(string heroStringId, int? raceId);

    /// <summary>
    /// Announces a battle escape that <see cref="ShouldDenyCapture"/> already caused. Separate from
    /// the verdict so the verdict stays a pure function: the battle hook denies first, then reports.
    /// Silent unless <paramref name="playerRelevant"/> and the config's announce flag are both set.
    /// </summary>
    void OnBattleCaptureDenied(string heroDisplayName, bool playerRelevant);

    /// <summary>
    /// The direct-capture path: decide, perform the escape, then announce.
    ///
    /// Returns <c>true</c> only when the hero was actually turned into a fugitive. A <c>false</c>
    /// return means the caller must let vanilla capture proceed, which is what keeps the veto
    /// fail-open: if the escape could not be performed, skipping vanilla would leave the hero
    /// neither captured nor escaped.
    /// </summary>
    bool TryPreventCapture(string heroStringId, int? raceId, string heroDisplayName, bool playerRelevant);
}
