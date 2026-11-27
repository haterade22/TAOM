namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// What the transfer reads of the order controller: the target, and the selection with its soldier counts and whether it holds the hero.
/// </summary>
internal readonly record struct SeatTransferFacts(
    bool TargetExists, bool TargetSelected, int SelectedCompanies, int SelectedUnits, int TargetUnits,
    bool Deployment = false, bool SelectedHasHero = false, bool TargetHasHero = false);

/// <summary>Why a Ctrl and number transfer is refused; <c>None</c> means it may be given.</summary>
internal enum SeatTransferRefusal
{
    None,
    NoTarget,
    NoSource,
    NoUnits,
    Deployment,
}

/// <summary>A transfer's verdict: the refusal, or the number of soldiers to move (every soldier of the source companies).</summary>
internal readonly record struct SeatTransferPlan(SeatTransferRefusal Refusal, int Number);

/// <summary>
/// Ctrl and a number key moves the selected companies' soldiers into that company, as the troop transfer popup's confirm does
/// (<c>MissionOrderTroopControllerVM.ExecuteConfirmTransfer</c>, v1.5.7 :492-498). The order divides by the selected companies' total
/// (<c>OrderController.cs:915-975</c>), so a total of zero must never reach it.
/// </summary>
internal static class SeatTransferRule
{
    /// <summary>The number keys 1 to 8 name the player's eight companies, index 0 to 7 (key 0, index 100, is "all").</summary>
    internal const int Companies = 8;

    /// <summary>
    /// Whether the patch takes the key press: the command mouse is live, Left Control is down and the index names a company.
    /// </summary>
    internal static bool Intercepts(bool commandLive, bool leftControlDown, int formationIndex) =>
        commandLive && leftControlDown && formationIndex >= 0 && formationIndex < Companies;

    /// <summary>The target leaves the sources if it was selected; nothing left to send, or no soldiers in what is left, is a refusal.</summary>
    internal static SeatTransferPlan Plan(SeatTransferFacts facts)
    {
        // Vanilla's transfer popup does not open in deployment: MissionOrderVM.OnTransferStarted returns at once when IsDeployment (v1.5.7 :1033-1036).
        if (facts.Deployment)
            return new SeatTransferPlan(SeatTransferRefusal.Deployment, 0);

        if (!facts.TargetExists)
            return new SeatTransferPlan(SeatTransferRefusal.NoTarget, 0);

        var companies = facts.SelectedCompanies - (facts.TargetSelected ? 1 : 0);
        if (companies <= 0)
            return new SeatTransferPlan(SeatTransferRefusal.NoSource, 0);

        // The popup's cap is the sum of OrderTroopItemVM.CurrentMemberCount, which is CountOfUnits less one for the company that holds the player's
        // own agent (OrderTroopItemVM.cs:353, 385; MissionOrderTroopControllerVM.ExecuteConfirmTransfer :492-496), so the hero is never counted.
        var units = facts.SelectedUnits - (facts.SelectedHasHero ? 1 : 0)
            - (facts.TargetSelected ? facts.TargetUnits - (facts.TargetHasHero ? 1 : 0) : 0);
        return units > 0 ? new SeatTransferPlan(SeatTransferRefusal.None, units) : new SeatTransferPlan(SeatTransferRefusal.NoUnits, 0);
    }

    /// <summary>The word of an outcome line for a refusal.</summary>
    internal static string Reason(SeatTransferRefusal refusal) => refusal switch
    {
        SeatTransferRefusal.NoTarget => "no-target",
        SeatTransferRefusal.NoSource => "no-source",
        SeatTransferRefusal.NoUnits => "no-units",
        SeatTransferRefusal.Deployment => "deployment",
        _ => "none",
    };
}
