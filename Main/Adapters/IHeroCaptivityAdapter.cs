namespace TAOM.Adapters;

/// <summary>
/// The one captivity mutation TAOM performs: turning a hero who was about to be taken prisoner
/// into a fugitive instead. Keyed by StringId, never <c>Hero</c> (ADR-007).
/// </summary>
public interface IHeroCaptivityAdapter
{
    /// <summary>
    /// Applies <c>MakeHeroFugitiveAction</c> to the named hero.
    ///
    /// Returns <c>true</c> only when the hero resolved and the action was applied. The bool return
    /// is load-bearing rather than cosmetic: the capture-veto prefix decides whether to skip
    /// vanilla based on it, so a lookup miss must be able to say "I did nothing" and let vanilla
    /// capture proceed. A <c>void</c> signature would strand the hero as neither captured nor
    /// escaped.
    /// </summary>
    bool MakeFugitive(string heroStringId);
}
