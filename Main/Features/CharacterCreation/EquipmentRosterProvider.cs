using TaleWorlds.Core;

namespace TAOM.Features.CharacterCreation;

public sealed class EquipmentRosterProvider : IEquipmentRosterProvider
{
    public MBEquipmentRoster GetRoster(string rosterId)
    {
        return Game.Current?.ObjectManager?.GetObject<MBEquipmentRoster>(rosterId);
    }

    public bool? HasHorse(string rosterId)
    {
        var roster = GetRoster(rosterId);
        if (roster == null)
            return null;
        return roster.DefaultEquipment[EquipmentIndex.Horse].Item != null;
    }
}
