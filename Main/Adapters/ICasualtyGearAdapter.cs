using System.Collections.Generic;
using TAOM.Features.BattleGearLoot.Domain;

namespace TAOM.Adapters;

public interface ICasualtyGearAdapter
{
    IReadOnlyList<GearPiece> Pieces { get; }

    bool VanillaGivesNothing { get; }

    void Grant(IReadOnlyList<int> slots);
}
