using System.Collections.Generic;

namespace TAOM.Features.CustomBattles.Config;

/// <summary>
/// DTO for <c>custom_battle/custom_battle_commanders.json</c>. Maps a faction key (culture
/// StringId) to an ordered list of commander lord StringIds. Deserialized by Newtonsoft; the
/// validated, sanitized form is exposed via <see cref="ICustomBattleCommandersProvider"/>.
/// </summary>
public class CustomBattleCommandersConfig
{
    /// <summary>
    /// Faction (culture StringId) -> ordered lord StringIds. Order is the display order.
    /// Nullable (no initializer) so an absent "factions" key deserializes to null and the provider
    /// can distinguish "no factions map" from an explicit empty map.
    /// </summary>
    public Dictionary<string, List<string>> Factions { get; set; }
}
