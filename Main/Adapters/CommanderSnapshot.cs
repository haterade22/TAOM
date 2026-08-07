namespace TAOM.Adapters;

/// <summary>
/// One-read snapshot of an enlistment commander's world state. Produced by
/// <see cref="ICommanderLordAdapter"/> so reconciler/attachment services stay pure over
/// plain data (no sealed types, one engine round-trip per evaluation).
/// </summary>
public sealed class CommanderSnapshot
{
    public bool Exists { get; }
    public bool IsAlive { get; }
    public bool IsPrisoner { get; }
    public string PartyId { get; }
    public bool PartyIsActive { get; }
    public bool PartyIsInMapEvent { get; }
    public bool PartyIsInSettlement { get; }
    public string SettlementId { get; }

    /// <summary>Which vanilla menu a discharge into this settlement should open: town / castle / village.</summary>
    public string SettlementMenuId { get; }
    public string CultureId { get; }
    public string FactionId { get; }
    public string Name { get; }

    public bool HasParty => !string.IsNullOrEmpty(PartyId);

    public CommanderSnapshot(
        bool exists,
        bool isAlive = false,
        bool isPrisoner = false,
        string partyId = null,
        bool partyIsActive = false,
        bool partyIsInMapEvent = false,
        bool partyIsInSettlement = false,
        string settlementId = null,
        string settlementMenuId = null,
        string cultureId = null,
        string factionId = null,
        string name = null)
    {
        Exists = exists;
        IsAlive = isAlive;
        IsPrisoner = isPrisoner;
        PartyId = partyId;
        PartyIsActive = partyIsActive;
        PartyIsInMapEvent = partyIsInMapEvent;
        PartyIsInSettlement = partyIsInSettlement;
        SettlementId = settlementId;
        SettlementMenuId = settlementMenuId;
        CultureId = cultureId;
        FactionId = factionId;
        Name = name;
    }

    public static CommanderSnapshot Missing { get; } = new CommanderSnapshot(exists: false);
}
