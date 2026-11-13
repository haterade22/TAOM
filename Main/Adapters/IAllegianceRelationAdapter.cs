using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Adapters;

/// <summary>
/// The lords of a new campaign and their raw relations, for <c>KingdomAllegianceService.FlipOpposedFriendships</c>
/// (docs/features/kingdom-allegiance.md). Call <see cref="ReadLords"/> first; the other two members address lords by their index
/// in that list.
/// </summary>
public interface IAllegianceRelationAdapter
{
    /// <summary>The lords vanilla's new-game seeding rolls relations for, alive or dead, the player excluded, each sided by their own
    /// clan.</summary>
    IReadOnlyList<SideIds> ReadLords();

    /// <summary>The raw personal relation of two lords, no clan-leader mapping.</summary>
    int ReadRelation(int first, int second);

    /// <summary>Writes the raw personal relation of two lords: no event, no notice.</summary>
    void WriteRelation(int first, int second, int value);

    /// <summary>Drops the lords read by <see cref="ReadLords"/>, so a finished run keeps no campaign object alive.</summary>
    void Release();
}
