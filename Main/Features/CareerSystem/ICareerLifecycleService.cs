using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// The campaign-lifecycle decisions for a hero's career: which career a save with none should be
/// given, which stored choices no longer belong to the hero's career, the paid retrain (#769 E7) and
/// the one-time refund when a career's tree is redesigned (#769 E12). The first two were inline in
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
    /// #1046: a hero whose stored career id the registry no longer defines gets the career, its choices,
    /// tiers and flags cleared (no refund: points derive from level), so the fallback can reassign.
    /// Returns true when something was cleared. Acts only while the registry holds at least one career:
    /// a partly loaded registry resolves nothing and must not reset everyone. A career whose XML element
    /// failed to parse is still declared, so it is kept too. Loaded-save path only.
    /// </summary>
    bool ClearUnknownCareer(string heroStringId);

    /// <summary>
    /// Drop stored choices that positively belong to a DIFFERENT career, returning how many went.
    ///
    /// Deleting requires proof of foreignness, never absence of proof of belonging. A choice whose
    /// owner cannot be resolved is kept: a partially-loaded registry resolves nothing, and the
    /// inverse polarity would delete every choice the player has ever taken.
    /// </summary>
    int RepairForeignChoices(string heroStringId);

    /// <summary>
    /// #769 E7: the paid retrain. Charges the registry's retrain cost, refunds every choice but the
    /// root and refreshes the passive cache. A short purse changes no state.
    /// </summary>
    CareerRetrainResult TryRetrain(string heroStringId, int heroLevel);

    /// <summary>
    /// #769 E12: for every hero holding career data, when the career's tree was redesigned (its
    /// TreeVersion is above the one the hero is stamped with), refund every choice once and stamp
    /// the hero. Every hero, not only the player: a co-op hero or one the player takes over later is
    /// never the main hero at session launch. Returns how many choices the PLAYER lost: 0 when the
    /// player's career did not migrate, and also 0 when it migrated a player who held only the root
    /// (the stamp is still set), so the caller announces a refund only for a count above zero.
    ///
    /// Each migration needs proof the new tree actually loaded: the career lists at least one group
    /// and every listed group resolves with at least one choice. Without it nothing changes and no
    /// flag is set, because a malformed choices file looks exactly like an emptied tree.
    /// </summary>
    int MigrateAllTreeVersions(string playerHeroStringId);
}
