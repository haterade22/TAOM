using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Adapters;

/// <summary>
/// A window over the engine's side-wide ready list (<c>List&lt;(FlattenedTroopRosterElement, MapEventParty, float)&gt;</c>):
/// the entries one party's <c>TroopSupplierProbabilityModel</c> call appended, from the list's length before the base
/// call to its length now. Indices are relative to the window, so a removal can never reach another party's entries.
/// Constructed inside the service's try (the model hands over a factory), so a fault here means no filtering.
/// </summary>
public sealed class ReadyListWindow : IReadyListWindow
{
    private readonly MapEventParty _battleParty;
    private readonly List<(FlattenedTroopRosterElement, MapEventParty, float)> _readyList;
    private readonly int _from;

    public ReadyListWindow(MapEventParty battleParty, List<(FlattenedTroopRosterElement, MapEventParty, float)> readyList, int from)
    {
        _battleParty = battleParty ?? throw new ArgumentNullException(nameof(battleParty));
        _readyList = readyList ?? throw new ArgumentNullException(nameof(readyList));
        if (from < 0 || from > readyList.Count)
            throw new ArgumentOutOfRangeException(nameof(from), from, "the window starts outside the ready list");
        _from = from;
    }

    public string PartyId => _battleParty.Party.Id;

    public int Side => (int)_battleParty.Party.Side;

    public int Count => _readyList.Count - _from;

    public int ListCount => _readyList.Count;

    public string? CharacterIdAt(int index) => _readyList[_from + InWindow(index)].Item1.Troop?.StringId;

    public bool IsPlayerCharacterAt(int index) => _readyList[_from + InWindow(index)].Item1.Troop?.IsPlayerCharacter == true;

    public void RemoveAt(int index) => _readyList.RemoveAt(_from + InWindow(index));

    private int InWindow(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "outside this party's part of the ready list");
        return index;
    }
}
