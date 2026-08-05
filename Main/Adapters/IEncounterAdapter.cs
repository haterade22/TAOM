namespace TAOM.Adapters;

/// <summary>Which side of a map event a party fights on. Adapter-level mirror of BattleSideEnum's two real sides.</summary>
public enum PartyBattleSide
{
    Defender = 0,
    Attacker = 1,
}

/// <summary>
/// Wraps the static PlayerEncounter surface for enlisted battle interception: joining the
/// commander's map event, creating an encounter against it, and cleaning up stale ones.
/// </summary>
public interface IEncounterAdapter
{
    bool HasCurrent { get; }

    bool IsInsideSettlement { get; }

    /// <summary>StringId of the encountered mobile party, or null.</summary>
    string EncounteredPartyId { get; }

    /// <summary>Side of the given party in its current map event, or null when not in one.</summary>
    PartyBattleSide? GetPartyBattleSide(string partyId);

    bool IsPartyInMapEvent(string partyId);

    /// <summary>True when the main party may join the given party's map event on the given side.</summary>
    bool CanMainPartyJoinBattleOf(string partyId, PartyBattleSide side);

    bool JoinBattle(PartyBattleSide side);

    /// <summary>Restart the player encounter against the given battle parties. Never forces the player out of a settlement.</summary>
    bool RestartBattle(string defenderPartyId, string attackerPartyId);

    bool Finish(bool forcePlayerOutFromSettlement = false);
}
