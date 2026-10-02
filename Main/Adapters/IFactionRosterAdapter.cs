using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// The engine's characters, heroes, cultures and kingdoms as the faction screen (#704) reads them.
/// Every lookup returns plain <see cref="RosterEntry"/> values; the choices made on them (the elite
/// troop, the living lords, the Leader tab) live in the engine-free <c>FactionRoster</c>.
/// </summary>
public interface IFactionRosterAdapter
{
    /// <summary>A character by id, or null.</summary>
    RosterEntry? Character(string id);

    /// <summary>The culture's elite basic troop, else its basic troop, or null.</summary>
    RosterEntry? CultureTroop(string cultureId);

    /// <summary>Every troop reachable from the culture's basic and elite basic troops through their
    /// upgrades, each once.</summary>
    IReadOnlyList<RosterEntry> CultureTroopTree(string cultureId);

    /// <summary>The leader of the kingdom's ruling clan, alive or not, or null.</summary>
    RosterEntry? Ruler(string kingdomId);

    /// <summary>The leaders of the kingdom's clans other than the ruling clan.</summary>
    IReadOnlyList<RosterEntry> OtherClanLeaders(string kingdomId);

    /// <summary>The culture's wanderer templates, named for display.</summary>
    IReadOnlyList<RosterEntry> Wanderers(string cultureId);

    /// <summary>How many races the engine knows.</summary>
    int RaceCount { get; }
}
