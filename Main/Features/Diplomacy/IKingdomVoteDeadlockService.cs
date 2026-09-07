using TAOM.Adapters;

namespace TAOM.Features.Diplomacy;

/// <summary>
/// Decision half of the kingdom-vote deadlock guard. The three Harmony patches that use it take
/// sealed engine view models and cannot run outside a live campaign, so every judgement they make
/// lives here where it is testable.
///
/// Both patch seams route through the SAME <see cref="ShouldSuppressBallot"/> call on purpose: two
/// seams gating one decision with independently written guards is how they end up contradicting each
/// other (lessons/harmony-il.md, "Two seams that gate the same decision must carry the SAME guards").
/// </summary>
public interface IKingdomVoteDeadlockService
{
    /// <summary>
    /// True when this ballot must not be opened in the kingdom decision window.
    ///
    /// <para>Returns false only for a null ballot, where there is nothing to judge. A FAULT while
    /// judging returns true (suppress), because vanilla is not a safe default at this call site: its
    /// multi-clan branch builds the unclosable window, and its single-clan branch calls
    /// <c>GetChosenOutcomeText()</c> on a null <c>_chosenOutcome</c> and throws. A wrongly suppressed
    /// ballot costs the player one reopen of the Kingdom screen; a wrongly opened one costs the
    /// session.</para>
    /// </summary>
    bool ShouldSuppressBallot(IKingdomBallotAdapter ballot);

    /// <summary>
    /// Tells the player a vote was withdrawn, at most once per ballot. Never throws: the callers are
    /// mid-teardown of a window that is otherwise unclosable.
    /// </summary>
    void AnnounceLapsedBallot(IKingdomBallotAdapter ballot);
}
