using TaleWorlds.Core;

namespace TAOM.Features.CharacterCreation;

public sealed class EquipmentRosterProvider : IEquipmentRosterProvider
{
    public MBEquipmentRoster GetRoster(string rosterId)
    {
        return Game.Current?.ObjectManager?.GetObject<MBEquipmentRoster>(rosterId);
    }
}
