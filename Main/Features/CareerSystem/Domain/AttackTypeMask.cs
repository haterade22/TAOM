using System;

namespace TAOM.Features.CareerSystem.Domain;

[Flags]
public enum AttackTypeMask
{
    None = 0,
    Melee = 1,
    Ranged = 2,
    All = Melee | Ranged
}
