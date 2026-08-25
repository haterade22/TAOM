namespace TAOM.Features.Enlistment;

/// <summary>
/// Master on/off for the whole enlistment feature, backed by the MCM checkbox.
///
/// FAIL-OPEN (<c>?? true</c>), matching the compiled default of
/// <c>TaomSettings.EnableEnlistment</c> — MCM is frequently absent, and a feature that silently
/// disables itself whenever the settings mod is missing is worse than one that stays on.
/// The fallback and the compiled default must always agree; both are pinned by tests.
///
/// WHAT "OFF" MEANS, precisely, because "just stop running" is the wrong answer here:
/// an enlisted player is parked HIDDEN and INACTIVE beside their commander, and the code that
/// restores them is the same code the switch turns off. Halting in place would leave them
/// invisible on the map with no menu — a soft-lock created by a settings toggle. So OFF means:
/// no new enlistment can start, and any service in progress ends with ONE honourable discharge
/// through the normal pipeline, which restores presence, closes any encounter and hands the
/// player back somewhere they can act.
/// </summary>
public interface IEnlistmentFeatureSettingsProvider
{
    /// <summary>True when the enlistment feature is active. Default true.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Offer shore leave with a popup when the column enters a settlement. Default true.
    ///
    /// Exists because the option alone is unreachable: a lord's town stop is a few campaign hours
    /// and the service wait menu runs at UnstoppableFastForward, so "Take leave in the settlement"
    /// appears and vanishes inside about two real seconds (#512). Turning this off leaves the menu
    /// option working for a player who would rather not be interrupted.
    /// </summary>
    bool OfferLeaveOnArrival { get; }
}
