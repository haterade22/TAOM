namespace TAOM.Adapters;

/// <summary>
/// One captured lord of a boosted kingdom as the escape pass sees it, read once per day by
/// <see cref="IPrisonerEscapeAdapter"/>. Pure data: no engine type crosses into the services. The decision
/// fields mirror vanilla's <c>PrisonerReleaseCampaignBehavior.DailyHeroTick</c> (v1.5.4 :201-250;
/// <see cref="IsAlive"/> is its raise-site filter, since the daily hero ticker runs over the living heroes).
/// <see cref="CaptorInMapEventOrSiege"/> and <see cref="IsPlayerClan"/> are TAOM's own exclusions, stricter
/// than vanilla, whose battle-or-siege gate is only in <c>HourlyPartyTick</c> (:255) for prisoners over
/// the party limit. <see cref="HeroId"/> and <see cref="KingdomId"/> are keys: the kingdom selects the
/// boost multiplier and is not a vanilla input.
/// </summary>
public sealed class PrisonerEscapeSnapshot
{
    public string HeroId { get; set; } = string.Empty;

    /// <summary>The kingdom of the lord's clan (the captive's side, not the captor's).</summary>
    public string KingdomId { get; set; } = string.Empty;

    public bool IsAlive { get; set; }

    public bool IsPrisoner { get; set; }

    /// <summary>The captor is a mobile party (a lord's party), not a settlement garrison.</summary>
    public bool CaptorIsMobile { get; set; }

    /// <summary>The mobile captor currently sits in a settlement.</summary>
    public bool CaptorInSettlement { get; set; }

    /// <summary>The captor's members that are not wounded; read for mobile captors only.</summary>
    public int CaptorHealthyMembers { get; set; }

    /// <summary>
    /// Vanilla's three half-chance cases: the player's main party holds the lord, the captor is a settlement
    /// of the player's clan, or the captor is a mobile party sitting in a settlement of the player's clan.
    /// </summary>
    public bool PlayerHeld { get; set; }

    /// <summary>
    /// Vanilla's relative escape terms as one factor, <c>1 + the sum of the AddFactor slots</c> (v1.5.4
    /// :223-245): the governor's perks of a town or castle captor, the captor party's perks, the captive's
    /// Fleet Footed and the captor leader's Valor. Defaults to 0, so an unset value fails closed.
    /// </summary>
    public float EscapeFactor { get; set; }

    /// <summary>TAOM's own exclusion, not vanilla's: the captor is in a battle or a siege.</summary>
    public bool CaptorInMapEventOrSiege { get; set; }

    /// <summary>The result of the <c>CanHeroBeReleased</c> campaign event; false when it could not be asked.</summary>
    public bool CanBeReleased { get; set; }

    public bool IsMainHero { get; set; }

    /// <summary>TAOM's own exclusion, not vanilla's: the lord belongs to the player's clan (decision D4).</summary>
    public bool IsPlayerClan { get; set; }
}
