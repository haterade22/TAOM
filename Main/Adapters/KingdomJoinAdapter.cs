using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc cref="IKingdomJoinAdapter"/>
public class KingdomJoinAdapter : IKingdomJoinAdapter
{
    private readonly IModLogger _logger;

    public KingdomJoinAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string FindJoinableKingdomForPlayerCulture()
    {
        var clan = Clan.PlayerClan;
        if (Campaign.Current == null || clan == null)
            return string.Empty;

        // Already in a kingdom (the takeover path normally lands here), so there is nothing to ask.
        if (clan.Kingdom != null)
            return string.Empty;

        var cultureId = clan.Culture?.StringId;
        if (string.IsNullOrEmpty(cultureId))
            return string.Empty;

        foreach (var kingdom in Campaign.Current.Kingdoms)
        {
            if (kingdom == null || kingdom.IsEliminated)
                continue;
            if (!string.Equals(kingdom.Culture?.StringId, cultureId, StringComparison.Ordinal))
                continue;
            if (kingdom.Leader == null)
                continue;

            // Do not offer a kingdom the player already leads.
            if (kingdom.Leader == Hero.MainHero)
                continue;

            return kingdom.StringId;
        }

        return string.Empty;
    }

    public string GetKingdomName(string kingdomId)
        => FindKingdom(kingdomId)?.Name?.ToString() ?? string.Empty;

    public void JoinPlayerClanToKingdom(string kingdomId)
    {
        var kingdom = FindKingdom(kingdomId);
        var clan = Clan.PlayerClan;
        if (kingdom == null || clan == null || clan.Kingdom != null)
            return;

        // Named arguments deliberately. The third positional slot became a CampaignTime in 1.4.8,
        // so the predecessor mod's ApplyByJoinToKingdom(clan, kingdom, true) no longer compiles and
        // a positional call here would be one refactor away from meaning something else.
        ChangeKingdomAction.ApplyByJoinToKingdom(
            clan: clan,
            newKingdom: kingdom,
            showNotification: true);

        _logger.LogInfo($"Player Switcher: player clan joined kingdom '{kingdomId}'");
    }

    private static Kingdom? FindKingdom(string kingdomId)
        => string.IsNullOrEmpty(kingdomId)
            ? null
            : Campaign.Current?.CampaignObjectManager?.Find<Kingdom>(kingdomId);
}
