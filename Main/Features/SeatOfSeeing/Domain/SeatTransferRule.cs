namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What the transfer reads of the player's order controller: the target company, and the selection with its soldier counts.</summary>
internal readonly record struct SeatTransferFacts(bool TargetExists, bool TargetSelected, int SelectedCompanies, int SelectedUnits, int TargetUnits);

/// <summary>Why a Ctrl and number transfer is refused; <c>None</c> means it may be given.</summary>
internal enum SeatTransferRefusal
{
    None,
    NoTarget,
    NoSource,
    NoUnits,
}

/// <summary>A transfer's verdict: the refusal, or the number of soldiers to move (every soldier of the source companies).</summary>
internal readonly record struct SeatTransferPlan(SeatTransferRefusal Refusal, int Number);

/// <summary>
/// Ctrl and a number key moves the selected companies' soldiers into that company (owner decision, design section 3). The order is the one the
/// troop transfer popup gives, <c>SetOrderWithFormationAndNumber(Transfer, target, n)</c> (<c>MissionOrderTroopControllerVM.ExecuteConfirmTransfer</c>,
/// v1.5.7 :492-498), which splits the number across the selected companies by their size (<c>OrderController.cs:915-975</c>) and divides by their
/// total, so a total of zero must never reach it. A target company with no soldiers is a valid target: the popup lists all eight companies and
/// bars only those that are selected (<c>MissionOrderVM.OnTransferStarted</c> :1036-1040).
/// </summary>
internal static class SeatTransferRule
{
    /// <summary>The number keys 1 to 8 name the player's eight companies, index 0 to 7 (key 0, index 100, is "all").</summary>
    internal const int Companies = 8;

    /// <summary>
    /// The patch takes the key press instead of vanilla's when the command mouse is live, Left Control is down (the key vanilla's own toggle
    /// reads, <c>MissionOrderTroopControllerVM.cs:617</c>) and the index names a company. Every other press runs vanilla.
    /// </summary>
    internal static bool Intercepts(bool commandLive, bool leftControlDown, int formationIndex) =>
        commandLive && leftControlDown && formationIndex >= 0 && formationIndex < Companies;

    /// <summary>The target leaves the sources if it was selected; nothing left to send, or no soldiers in what is left, is a refusal.</summary>
    internal static SeatTransferPlan Plan(SeatTransferFacts facts)
    {
        if (!facts.TargetExists)
            return new SeatTransferPlan(SeatTransferRefusal.NoTarget, 0);

        var companies = facts.SelectedCompanies - (facts.TargetSelected ? 1 : 0);
        if (companies <= 0)
            return new SeatTransferPlan(SeatTransferRefusal.NoSource, 0);

        var units = facts.SelectedUnits - (facts.TargetSelected ? facts.TargetUnits : 0);
        return units > 0 ? new SeatTransferPlan(SeatTransferRefusal.None, units) : new SeatTransferPlan(SeatTransferRefusal.NoUnits, 0);
    }

    /// <summary>The word of an outcome line for a refusal.</summary>
    internal static string Reason(SeatTransferRefusal refusal) => refusal switch
    {
        SeatTransferRefusal.NoTarget => "no-target",
        SeatTransferRefusal.NoSource => "no-source",
        SeatTransferRefusal.NoUnits => "no-units",
        _ => "none",
    };
}
