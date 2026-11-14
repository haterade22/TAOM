using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>
/// Decides who leads the player's side when the player starts a Custom Battle as a captain (#1184).
/// The engine reads the general's name for every field battle, so a captain with no general cannot start one.
/// </summary>
internal static class MusterGeneralChoice
{
    /// <summary>
    /// The decision Start acts on. A commander needs no general. A captain gets the lord <see cref="Pick"/> finds.
    /// With no lord left, a field battle is refused; a siege goes on without one, because the engine reads no general
    /// there.
    /// </summary>
    internal static (string? GeneralId, bool Refused) ForStart(bool isCaptain, bool isSiege, IReadOnlyList<string> lordIds,
        string? playerCharacterId, IEnumerable<string> takenIds)
    {
        if (!isCaptain) return (null, false);
        var general = Pick(lordIds, playerCharacterId, takenIds);
        return (general, general == null && !isSiege);
    }

    /// <summary>
    /// Returns the id of the first lord in <paramref name="lordIds"/> (the faction's lords in the screen's order,
    /// famous names first) who is not the player's own character and is not in <paramref name="takenIds"/>: the hero
    /// units of both armies and the enemy's commander. A lord in two places would stand in the battle twice, and
    /// vanilla's own screen also keeps both commanders out of the choice. Returns null when no such lord is left.
    /// </summary>
    internal static string? Pick(IReadOnlyList<string> lordIds, string? playerCharacterId, IEnumerable<string> takenIds)
    {
        var taken = new HashSet<string>(takenIds);
        return lordIds.FirstOrDefault(id => id != playerCharacterId && !taken.Contains(id));
    }
}
