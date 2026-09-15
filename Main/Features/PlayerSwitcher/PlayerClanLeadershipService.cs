using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AiPartySize;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.PlayerSwitcher;

/// <inheritdoc cref="IPlayerClanLeadershipService"/>
/// <remarks>
/// The state table, one row per guard, each pinned by a test:
///
/// <list type="bullet">
/// <item>leader is the player: nothing, the common case</item>
/// <item>Clan.PlayerClan is vanilla's <c>player_faction</c>: nothing, that is never a takeover</item>
/// <item>the player's own clan is not Clan.PlayerClan: nothing, warn. That is a takeover whose
/// clan-pointer write failed; promoting would call SetLeader on the WRONG clan, which assigns
/// hero.Clan and drags the lord out of his house</item>
/// <item>the clan has no leader: nothing, warn. Vanilla succession reads the outgoing leader's
/// gold unguarded</item>
/// <item>the player is dead, disabled or not spawned: nothing</item>
/// <item>this peer is not the campaign authority (co-op client): nothing</item>
/// <item>otherwise: promote, log, tell the player once</item>
/// </list>
///
/// A prisoner IS repaired: vanilla heir selection promotes a captive heir too, and the vote
/// deadlock does not care where the player is standing.
/// </remarks>
public sealed class PlayerClanLeadershipService : IPlayerClanLeadershipService
{
    /// <summary>Localization key for the one-time notice. Registered in taom_player_switcher_strings.xml.</summary>
    public const string RepairedKey = "taom_ps_clan_leader_repaired";

    /// <summary>English fallback, used when a language file has no entry for <see cref="RepairedKey"/>.</summary>
    public const string RepairedFallback =
        "You now lead {CLAN}. Kingdom votes and clan decisions recognise you again.";

    private readonly IPlayerIdentityAdapter _identity;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IInquiryAdapter _inquiry;
    private readonly IModLogger _logger;

    public PlayerClanLeadershipService(
        IPlayerIdentityAdapter identity,
        ICoopSessionProvider coopSession,
        IInquiryAdapter inquiry,
        IModLogger logger)
    {
        _identity = identity;
        _coopSession = coopSession;
        _inquiry = inquiry;
        _logger = logger;
    }

    public bool RepairIfNeeded()
    {
        try
        {
            if (!_coopSession.IsAuthority)
                return false;

            var state = _identity.GetPlayerClanLeadership();
            if (!state.IsValid || !state.IsHeroInPlay)
                return false;

            if (string.Equals(state.LeaderId, state.HeroId, StringComparison.Ordinal))
                return false;

            if (!AiPartySizeService.IsTakenOverPlayerClan(state.PlayerClanId))
                return false;

            if (!string.Equals(state.HeroClanId, state.PlayerClanId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    $"Player Switcher: the player '{state.HeroId}' belongs to clan '{state.HeroClanId}' but " +
                    $"Clan.PlayerClan is '{state.PlayerClanId}'; leaving leadership alone (see #550)");
                return false;
            }

            if (string.IsNullOrEmpty(state.LeaderId))
            {
                _logger.LogWarning(
                    $"Player Switcher: the player's clan '{state.PlayerClanId}' has no leader; leaving leadership alone");
                return false;
            }

            if (!_identity.PromoteToClanLeader(state.HeroId))
                return false;

            _logger.LogInfo(
                $"Player Switcher: promoted '{state.HeroId}' to leader of '{state.PlayerClanId}' in place of " +
                $"'{state.LeaderId}' so kingdom votes recognise the player again (#550)");
            _inquiry.ShowMessage(RepairedKey, RepairedFallback, "CLAN", state.PlayerClanName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Player Switcher: clan leadership repair failed: {ex}");
            return false;
        }
    }
}
