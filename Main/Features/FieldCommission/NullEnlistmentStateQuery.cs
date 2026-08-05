using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// Null-object fallback for <see cref="IEnlistmentStateQuery"/>. Registered with
/// <c>ifAlreadyRegistered: IfAlreadyRegistered.Keep</c> in <see cref="FieldCommissionIoC"/> so it
/// only takes effect if the Enlistment feature's own registration hasn't already claimed the
/// interface — see the wiring note in <see cref="FieldCommissionIoC"/> for the required
/// registration ORDER (Enlistment must register first for its real implementation to win).
/// <see cref="IsEnlisted"/> is always false, so FieldCommission behaves exactly as if the player
/// is never enlisted when the Enlistment feature is absent/disabled.
/// </summary>
public class NullEnlistmentStateQuery : IEnlistmentStateQuery
{
    public bool IsEnlisted => false;

    public EnlistmentState State => EnlistmentState.NotEnlisted;

    public string CommanderHeroId => null;

    public string EnlistedHeroId => null;

    public double? ContractEndDay => null;

    public bool IsCommanderParty(string partyId) => false;
}
