using TaleWorlds.Core;

namespace TAOM.Features.CharacterCreation;

public interface IEquipmentRosterProvider
{
    MBEquipmentRoster GetRoster(string rosterId);

    /// <summary>
    /// Checks whether the roster with the given ID contains a horse in the default equipment.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the roster exists and has a horse; <c>false</c> if the roster exists
    /// but has no horse; <c>null</c> if the roster was not found.
    /// </returns>
    bool? HasHorse(string rosterId);
}
