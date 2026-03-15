using TaleWorlds.Core;

namespace TAOM.Features.CharacterCreation;

public interface IEquipmentRosterProvider
{
    MBEquipmentRoster GetRoster(string rosterId);
}
