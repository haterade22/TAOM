namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// The daily wage as the WALLET should show it (field report 2, second half: "it doesn't show up as
/// a positive in the expected change part of your wallet").
///
/// TAOM pays the enlistment wage itself, through <c>ServiceRewardService.PayDailyWage</c> and the
/// commander's purse — it is deliberately not part of vanilla's clan finance arithmetic. The
/// consequence is that the clan gold-change tooltip, which is assembled entirely from that
/// arithmetic, has no idea the player has an income at all. Gold appeared once a day from nowhere
/// and the projection said it never would.
///
/// So this is a DISPLAY line and nothing else. The one caller that actually moves money passes
/// <c>applyWithdrawals: true</c> (<c>ClanVariablesCampaignBehavior</c> rounds the result and applies
/// it), and the model excludes the line on exactly that call — otherwise the player would be paid
/// twice for the same day's service, once by us and once by the finance model.
///
/// ServeAsSoldier gets this coupling for free by making the finance model its payment path; we keep
/// the two separate because our wage is commander-funded with arrears, which vanilla's model has no
/// concept of.
/// </summary>
public interface IEnlistmentWagePreview
{
    /// <summary>
    /// Nominal daily wage for the player's current rank, or 0 when not enlisted.
    ///
    /// Nominal — what the contract owes — rather than what the commander can actually afford today.
    /// A projection that swung with a lord's purse would be unreadable, and the shortfall already
    /// has its own message on the day it happens.
    /// </summary>
    int GetDailyWage();
}
