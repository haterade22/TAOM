namespace TAOM.Adapters;

/// <summary>
/// The part of a side's ready list one <c>TroopSupplierProbabilityModel</c> call just appended for one party: the
/// engine's <c>(FlattenedTroopRosterElement, MapEventParty, float)</c> tuples between the list's length before the base
/// call and its length after. The siege troop picker removes the entries of troops the player left out, so the engine
/// never allocates them: no agent spawns for them and they never reach battle bookkeeping.
/// </summary>
public interface IReadyListWindow
{
    /// <summary>The party's id, <c>PartyBase.Id</c>: the key a plan's keep counts are filed under.</summary>
    string PartyId { get; }

    /// <summary>The party's battle side as the engine's <c>BattleSideEnum</c> int: 0 defender, 1 attacker. Anything else is no side.</summary>
    int Side { get; }

    /// <summary>How many appended entries remain in the window. Falls by one per <see cref="RemoveAt"/>.</summary>
    int Count { get; }

    /// <summary>How many entries the side's whole ready list holds now, the other parties' entries included.</summary>
    int ListCount { get; }

    /// <summary>The character id of an entry, or null when the entry names no troop.</summary>
    string? CharacterIdAt(int index);

    /// <summary>Whether the entry is the player's own character, who is never left out.</summary>
    bool IsPlayerCharacterAt(int index);

    /// <summary>Removes an entry from the engine's list. Indices of the entries before it do not move.</summary>
    void RemoveAt(int index);
}
