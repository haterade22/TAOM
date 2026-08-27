using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <inheritdoc cref="IHeroSwitchService"/>
/// <remarks>
/// Runs from PlayerSwitchContentHandler at character-creation handler priority 1100, which is
/// after vanilla's 800 and after TAOM's own 1050. Everything destructive in
/// CharacterCreationManager.ApplyFinalEffects (Renown = 0, ApplyCulture, the culture teleport)
/// and every TAOM grant (race, career, gold, equipment) has already landed on the throwaway hero
/// and the throwaway clan by then, both of which this service deletes moments later. That is why
/// the lord being taken over needs no repair of any kind.
/// </remarks>
public class HeroSwitchService : IHeroSwitchService
{
    private readonly IPlayerIdentityAdapter _identity;
    private readonly ICareerCreationHandler _career;
    private readonly IPlayerSwitchPolicyProvider _policy;
    private readonly IModLogger _logger;

    public HeroSwitchService(
        IPlayerIdentityAdapter identity,
        ICareerCreationHandler career,
        IPlayerSwitchPolicyProvider policy,
        IModLogger logger)
    {
        _identity = identity;
        _career = career;
        _policy = policy;
        _logger = logger;
    }

    public SwitchOutcome Execute(SwitchPlan plan)
    {
        if (!plan.IsValid || !_policy.Current.Enabled)
            return SwitchOutcome.NotAttempted;

        // A failed probe means our reading of the engine is wrong. Refuse the whole handover
        // rather than run the half of it that happens not to need reflection.
        if (!_identity.CanReassignPlayerClan)
        {
            _policy.DisableForSession(
                "Campaign.PlayerDefaultFaction could not be reassigned; the player clan pointer would be left on the abandoned clan");
            return SwitchOutcome.Blocked;
        }

        if (!_identity.IsSwitchable(plan.HeroId))
        {
            _logger.LogWarning($"Player Switcher: '{plan.HeroId}' is no longer switchable, keeping the created character");
            return SwitchOutcome.Blocked;
        }

        try
        {
            return Run(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Player Switcher: handover failed, continuing as the created character: {ex}");
            return SwitchOutcome.Failed;
        }
    }

    private SwitchOutcome Run(SwitchPlan plan)
    {
        // Snapshot first. After ApplyPlayerCharacter, Hero.MainHero and Clan.PlayerClan describe
        // the lord, so nothing about the created character can be read back.
        var ticket = _identity.Capture(plan.HeroId, plan.CareerId);
        if (!ticket.IsValid)
        {
            _logger.LogWarning("Player Switcher: could not capture the pre-swap state, keeping the created character");
            return SwitchOutcome.Blocked;
        }

        var takeover = plan.Path == SwitchPath.AssumeIdentity;

        if (takeover && string.IsNullOrEmpty(ticket.TargetClanId))
        {
            _logger.LogWarning($"Player Switcher: '{plan.HeroId}' reported no clan to take over, keeping the created character");
            return SwitchOutcome.Blocked;
        }

        if (!takeover)
            _identity.AdoptIntoPlayerClan(plan.HeroId);

        _identity.ApplyPlayerCharacter(plan.HeroId);

        if (takeover)
        {
            // Must precede RemoveOriginalHero. KillCharacterAction only reaches
            // DestroyClanAction when victim.Clan != Clan.PlayerClan, so moving the pointer here
            // is what lets the abandoned clan (and the character-creation party still registered
            // to it) be swept automatically instead of lingering in the campaign.
            _identity.ReassignPlayerClan(ticket.TargetClanId);
        }

        if (plan.TransferGold)
            _identity.TransferGold(ticket.OriginalHeroId, plan.HeroId);

        if (!string.IsNullOrEmpty(ticket.CareerId))
            _career.OnCareerSelected(plan.HeroId, ticket.CareerId);

        _identity.MarkClanAndKingdomKnown(plan.HeroId);

        _identity.RemoveOriginalHero(ticket.OriginalHeroId);

        if (!takeover)
        {
            // The adoption path keeps the player's own clan, so nothing destroys the
            // character-creation party for us and it would linger as a second player-owned
            // party. Absorbed after the removal, so the created character is already out of its
            // roster, and always by the single captured id.
            _identity.AbsorbOriginalParty(ticket.OriginalPartyId);
        }

        _identity.ClearPendingNotifications();

        _logger.LogInfo($"Player Switcher: player is now '{plan.HeroId}' via {plan.Path}");
        return SwitchOutcome.Switched;
    }
}
