namespace TAOM.Features.CareerSystem;

/// <summary>
/// The campaign-lifecycle decisions for a hero's career: which career a save with none should be
/// given, and which stored choices no longer belong to the hero's career. Both were inline in
/// <c>CareerCampaignBehavior</c>; they are business logic, so ADR-002 puts them here and leaves the
/// behavior a thin delegate.
/// </summary>
public interface ICareerLifecycleService
{
    /// <summary>
    /// Assign the first career eligible for <paramref name="cultureId"/> when the hero has none.
    /// Returns true when a career was assigned.
    ///
    /// ONLY legitimate on the loaded-save path. On a new campaign the hero has no career yet
    /// simply because character creation has not run, and granting one there is the bug this
    /// service was extracted during: see <c>docs/features/career-system.md</c>.
    /// </summary>
    bool AssignFallbackCareerIfMissing(string heroStringId, string cultureId);

    /// <summary>
    /// Drop stored choices that positively belong to a DIFFERENT career, returning how many went.
    ///
    /// Deleting requires proof of foreignness, never absence of proof of belonging. A choice whose
    /// owner cannot be resolved is kept: a partially-loaded registry resolves nothing, and the
    /// inverse polarity would delete every choice the player has ever taken.
    /// </summary>
    int RepairForeignChoices(string heroStringId);
}
