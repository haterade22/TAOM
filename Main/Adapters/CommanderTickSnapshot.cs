namespace TAOM.Adapters;

/// <summary>
/// The cheap, allocation-free view of the commander that a per-tick pump is allowed to take.
///
/// THE COST RULE THIS TYPE EXISTS TO ENFORCE: a pump may make AT MOST ONE
/// <c>CampaignObjectManager.Find&lt;Hero&gt;</c> per expensive pass, and ZERO
/// <c>Find&lt;MobileParty&gt;</c>. Both are unindexed LINEAR SCANS — <c>Find&lt;Hero&gt;</c> walks the
/// dead-or-disabled hero list and then every living hero; <c>Find&lt;MobileParty&gt;</c> walks every
/// mobile party in the world (lords, villagers, caravans, militia, garrisons, bandits).
///
/// Deliberately does NOT carry name, culture, faction or settlement objects. The full
/// <see cref="CommanderSnapshot"/> renders <c>hero.Name.ToString()</c> (a TextObject render, one
/// allocation) and walks Culture / Clan.MapFaction / CurrentSettlement for fields no pump reads.
/// That type is for the hourly reconciler and the load normalizer, which are correctly slow.
///
/// A <c>readonly struct</c> rather than a class like its siblings, because this is the one type
/// produced on a pump — the deviation is intentional.
/// </summary>
public readonly struct CommanderTickSnapshot
{
    public bool Exists { get; }
    public bool IsAlive { get; }
    public bool IsPrisoner { get; }
    public string PartyId { get; }
    public bool PartyIsActive { get; }
    public bool PartyIsInMapEvent { get; }
    public string PartySettlementId { get; }

    /// <summary>
    /// Identity for the commander's CURRENT map event, or 0 when there is none.
    ///
    /// The engine supplies no identity of its own: <c>MapEvent</c> derives from
    /// <c>MBObjectBase</c> but every instance is built with a bare <c>new MapEvent()</c> and is
    /// never registered with <c>MBObjectManager</c>, so its <c>StringId</c> and <c>Id</c> are
    /// unset. The adapter therefore mints a token from a one-slot reference cache. Compare tokens
    /// to detect "this is a DIFFERENT battle than last tick"; never treat the value as stable
    /// across sessions or persist it.
    /// </summary>
    public int MapEventToken { get; }

    public bool HasParty => !string.IsNullOrEmpty(PartyId);

    public bool IsInSettlement => !string.IsNullOrEmpty(PartySettlementId);

    /// <summary>Alive, not a prisoner, and fielding an active party — i.e. followable this tick.</summary>
    public bool IsFollowable => Exists && IsAlive && !IsPrisoner && HasParty && PartyIsActive;

    public static CommanderTickSnapshot Missing => default;

    public CommanderTickSnapshot(
        bool exists,
        bool isAlive = false,
        bool isPrisoner = false,
        string partyId = null,
        bool partyIsActive = false,
        bool partyIsInMapEvent = false,
        string partySettlementId = null,
        int mapEventToken = 0)
    {
        Exists = exists;
        IsAlive = isAlive;
        IsPrisoner = isPrisoner;
        PartyId = partyId;
        PartyIsActive = partyIsActive;
        PartyIsInMapEvent = partyIsInMapEvent;
        PartySettlementId = partySettlementId;
        MapEventToken = mapEventToken;
    }
}
