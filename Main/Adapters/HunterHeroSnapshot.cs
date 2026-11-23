namespace TAOM.Adapters;

/// <summary>
/// What the Eye of Sauron hunt (#765 M8) needs to know about a candidate hunter, read from the live hero on every use.
/// Plain values only, so the service stays free of engine types (ADR-007).
/// </summary>
public sealed class HunterHeroSnapshot
{
    public static readonly HunterHeroSnapshot Missing = new();

    public bool Exists { get; set; }

    public bool IsAlive { get; set; }

    /// <summary><c>Hero.IsActive</c>: the hero is in play, not disabled or not yet born.</summary>
    public bool IsActive { get; set; }

    public bool IsFugitive { get; set; }

    public bool IsPrisoner { get; set; }

    /// <summary><c>PartyBelongedToAsPrisoner != null</c>.</summary>
    public bool IsHeldByAParty { get; set; }

    /// <summary><c>DeathMark != None</c>.</summary>
    public bool IsMarkedForDeath { get; set; }

    /// <summary>His live <c>MapFaction</c> StringId, or null.</summary>
    public string? FactionId { get; set; }

    /// <summary>The live faction is at war with the player's kingdom (<c>FactionManager.IsAtWarAgainstFaction</c>).</summary>
    public bool AtWarWithPlayerKingdom { get; set; }

    /// <summary>His live faction's safe-passage oath with the player (<c>NotAttackableByPlayerUntilTime</c>) is in the future.</summary>
    public bool FactionHoldsOath { get; set; }

    /// <summary><c>PartyBelongedTo != null</c>, led by him or not.</summary>
    public bool IsInAParty { get; set; }

    /// <summary>The StringId of the party he LEADS, or null when he leads none.</summary>
    public string? LedPartyId { get; set; }

    public bool CanLeadParty { get; set; }

    /// <summary><c>CharacterObject.Occupation == Lord</c>.</summary>
    public bool IsLord { get; set; }

    /// <summary>His settlement holds a tournament or is under siege (vanilla's spawn guard, HeroSpawnCampaignBehavior:163).</summary>
    public bool SettlementBlocksSpawn { get; set; }

    /// <summary>The settlement he governs (<c>GovernorOf.Settlement</c>), or null. A spawn removes him as governor.</summary>
    public string? GovernorOfSettlementId { get; set; }
}
