using System.Collections.Generic;

namespace TAOM.Features.SettlementGuards.Domain;

public sealed class GuardPool
{
    public IReadOnlyList<GuardEntry> Guards { get; }
    public string PrisonGuardTroopId { get; }

    public GuardPool(IReadOnlyList<GuardEntry> guards, string prisonGuardTroopId)
    {
        Guards = guards;
        PrisonGuardTroopId = prisonGuardTroopId;
    }
}
