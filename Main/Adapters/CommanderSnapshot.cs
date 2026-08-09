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

    /// <summary>Display name of the settlement the commander is in. Status board only — never a lookup key.</summary>
    public string SettlementName { get; }

    /// <summary>The commander's party is besieging something — distinct from being in a battle.</summary>
    public bool PartyIsBesieging { get; }

    /// <summary>
    /// StringId of the leader party of the army the commander belongs to, or null when he has
    /// none. Identity, not a flag: joining requires knowing WHICH army, and leaving requires
    /// knowing whether the one we are in is still his.
    /// </summary>
    public string ArmyLeaderPartyId { get; }

    /// <summary>Which vanilla menu a discharge into this settlement should open: town / castle / village.</summary>
    public string SettlementMenuId { get; }
    /// <summary>
    /// Who holds the commander prisoner, and where. Both null unless <see cref="IsPrisoner"/>.
    ///
    /// Separate from the four settlement fields above, which all resolve through
    /// <c>PartyBelongedTo</c> — null for a prisoner, because a captured lord has no party. These
    /// come from <c>PartyBelongedToAsPrisoner</c> instead. Display only, never lookup keys.
    ///
    /// They exist so the player can be TOLD where their commander went. Captivity is the one
    /// commander-loss case that is locatable at all: a lord whose party was merely destroyed has
    /// no position until the engine respawns him.
    /// </summary>
    public string CaptorName { get; }

    /// <inheritdoc cref="CaptorName"/>
    public string CaptivitySettlementName { get; }

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
        string settlementName = null,
        bool partyIsBesieging = false,
        string armyLeaderPartyId = null,
        string settlementMenuId = null,
        string captorName = null,
        string captivitySettlementName = null,
        string cultureId = null,
        string factionId = null,
        string name = null)
    {
        Exists = exists;
        IsAlive = isAlive;
        IsPrisoner = isPrisoner;
        CaptorName = captorName;
        CaptivitySettlementName = captivitySettlementName;
        PartyId = partyId;
        PartyIsActive = partyIsActive;
        PartyIsInMapEvent = partyIsInMapEvent;
        PartyIsInSettlement = partyIsInSettlement;
        SettlementId = settlementId;
        SettlementName = settlementName;
        PartyIsBesieging = partyIsBesieging;
        ArmyLeaderPartyId = armyLeaderPartyId;
        SettlementMenuId = settlementMenuId;
        CultureId = cultureId;
        FactionId = factionId;
        Name = name;
    }

    public static CommanderSnapshot Missing { get; } = new CommanderSnapshot(exists: false);
}
