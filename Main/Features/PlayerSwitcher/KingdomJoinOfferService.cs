using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <inheritdoc cref="IKingdomJoinOfferService"/>
/// <remarks>
/// Gated hard on an actual adoption. The predecessor mod raised this prompt without checking
/// whether a handover had happened at all, so an ordinary character creation could be asked
/// whether it wanted to join a kingdom; its own feature doc filed that as a quirk. Three
/// conditions have to hold, and each excludes a real case:
///
/// the handover must have succeeded (otherwise the player is their own character and nothing has
/// changed); it must have been the ADOPTION path (taking over a lord already puts you in their
/// kingdom, so the question is meaningless); and the clan must actually be kingdomless with a
/// kingdom of its culture available to join.
/// </remarks>
public class KingdomJoinOfferService : IKingdomJoinOfferService
{
    private readonly IPlayerSwitchSession _session;
    private readonly IKingdomJoinAdapter _kingdoms;
    private readonly IInquiryAdapter _inquiry;
    private readonly IPlayerSwitchPolicyProvider _policy;
    private readonly IModLogger _logger;

    public KingdomJoinOfferService(
        IPlayerSwitchSession session,
        IKingdomJoinAdapter kingdoms,
        IInquiryAdapter inquiry,
        IPlayerSwitchPolicyProvider policy,
        IModLogger logger)
    {
        _session = session;
        _kingdoms = kingdoms;
        _inquiry = inquiry;
        _policy = policy;
        _logger = logger;
    }

    public void OfferIfEarned()
    {
        if (!_policy.Current.Enabled)
            return;

        // A partially completed handover still made the player that hero, so the offer is
        // still the right question to ask.
        if (_session.LastOutcome != SwitchOutcome.Switched &&
            _session.LastOutcome != SwitchOutcome.SwitchedWithErrors)
            return;

        // A taken-over lord already belongs to whatever kingdom their clan belongs to.
        if (_session.LastPath != SwitchPath.AdoptIntoPlayerClan)
            return;

        var kingdomId = _kingdoms.FindJoinableKingdomForPlayerCulture();
        if (string.IsNullOrEmpty(kingdomId))
            return;

        var kingdomName = _kingdoms.GetKingdomName(kingdomId);

        _inquiry.ShowTwoOptionInquiry(
            "taom_ps_join_title", "Swear to a king?",
            "taom_ps_join_body", "Your clan stands alone. {KINGDOM} would take your oath and your banner. Join them now?",
            "taom_ps_join_yes", "Swear the oath",
            "taom_ps_join_no", "Stay independent",
            onOptionA: () => _kingdoms.JoinPlayerClanToKingdom(kingdomId),
            onOptionB: () => { },
            bodyVariableName: "KINGDOM", bodyVariableValue: kingdomName);

        _logger.LogInfo($"Player Switcher: offered the player clan a place in '{kingdomId}'");
    }
}
