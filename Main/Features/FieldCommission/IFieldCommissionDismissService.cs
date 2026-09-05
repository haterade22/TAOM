using System.Collections.Generic;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// Dismissing a promoted companion back to the ranks (#540): the hero is removed from the game and
/// one soldier of the troop type they were promoted from rejoins the main party. Merit is never
/// touched. Both entry points (the companion's own dialogue and the settlement-menu picker) end
/// in <see cref="DismissAndReport"/>, so the verdict, the ordering and the feedback live in one
/// place. Gear the companion carried is lost, as it is when vanilla fires a wanderer.
/// </summary>
public interface IFieldCommissionDismissService
{
    /// <summary>The verdict for one hero id plus the names the prompts render. Pure read.</summary>
    DismissCandidate Evaluate(string heroId);

    /// <summary>Every promoted companion whose verdict is <see cref="DismissOutcome.Ok"/>, in
    /// promotion order. Pure read.</summary>
    IReadOnlyList<DismissCandidate> GetDismissableCompanions();

    /// <summary>
    /// Re-evaluates, then removes the hero, refunds one origin soldier (wounded if the companion
    /// was) and forgets the promoted id, in that order. A verdict other than Ok is returned
    /// untouched with nothing changed; <see cref="DismissOutcome.RemovalFailed"/> means the engine
    /// declined the removal and nothing else was applied.
    /// </summary>
    DismissOutcome Dismiss(string heroId);

    /// <summary><see cref="Dismiss"/> plus the player-facing result message.</summary>
    DismissOutcome DismissAndReport(string heroId);

    /// <summary>The settlement-menu path: a picker over <see cref="GetDismissableCompanions"/>,
    /// a confirm inquiry that names the lost gear, then <see cref="DismissAndReport"/>. Shows
    /// nothing when nobody qualifies.</summary>
    void OpenDismissPicker();
}
