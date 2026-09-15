namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// Who leads the player's clan, read once so the leadership rule can be decided without the
/// engine. Every field is an id or a flag; the adapter fills it, the service judges it.
/// </summary>
public readonly struct PlayerClanLeadership
{
    public PlayerClanLeadership(
        string heroId,
        string heroClanId,
        string playerClanId,
        string playerClanName,
        string leaderId,
        bool isHeroInPlay)
    {
        HeroId = heroId;
        HeroClanId = heroClanId;
        PlayerClanId = playerClanId;
        PlayerClanName = playerClanName;
        LeaderId = leaderId;
        IsHeroInPlay = isHeroInPlay;
    }

    /// <summary>Hero.MainHero.</summary>
    public string HeroId { get; }

    /// <summary>The clan Hero.MainHero actually belongs to (Hero.Clan). Empty when clanless.</summary>
    public string HeroClanId { get; }

    /// <summary>
    /// Clan.PlayerClan, which is Campaign.PlayerDefaultFaction and NOT necessarily the same clan as
    /// <see cref="HeroClanId"/>: a takeover whose ReassignPlayerClan failed leaves them apart.
    /// </summary>
    public string PlayerClanId { get; }

    /// <summary>Display name of Clan.PlayerClan, for the one message the repair shows.</summary>
    public string PlayerClanName { get; }

    /// <summary>Clan.PlayerClan.Leader, or empty when the clan has no leader at all.</summary>
    public string LeaderId { get; }

    /// <summary>
    /// Alive, spawned and not disabled. A prisoner or fugitive is still in play; vanilla heir
    /// selection promotes a captive heir too. Named for what it means rather than <c>IsAlive</c>,
    /// because <c>Hero.IsAlive</c> is only <c>!IsDead</c> and would let a disabled hero through.
    /// </summary>
    public bool IsHeroInPlay { get; }

    public bool IsValid => !string.IsNullOrEmpty(HeroId) && !string.IsNullOrEmpty(PlayerClanId);

    public static PlayerClanLeadership None =>
        new PlayerClanLeadership(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false);
}
