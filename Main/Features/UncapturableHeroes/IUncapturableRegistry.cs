namespace TAOM.Features.UncapturableHeroes;

/// <summary>
/// Answers one question: may this hero ever be taken prisoner?
///
/// Does NOT fold the master toggle. Identity is not a decision the toggle should change, and
/// keeping them separate is what lets the service be tested for "toggle off means the registry is
/// never asked" (the same split <see cref="DreadAura.IDreadRegistry"/> documents).
/// </summary>
public interface IUncapturableRegistry
{
    /// <summary>
    /// True if this hero can never be taken prisoner.
    ///
    /// Evaluation order is fixed and the first match wins:
    /// <list type="number">
    /// <item>the exclude list, which returns false and beats everything below it,</item>
    /// <item>the explicit hero id include list,</item>
    /// <item>named hero sets (<c>nazgul_nine</c>), the axis that finds the Nine,</item>
    /// <item>the race rule, which on shipped data finds only Sauron.</item>
    /// </list>
    ///
    /// <paramref name="raceId"/> is nullable because a caller that cannot resolve a race must be
    /// able to say so rather than pass 0, which is a real race (human).
    /// </summary>
    bool IsUncapturable(string heroStringId, int? raceId);
}
