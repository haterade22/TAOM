using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

public interface IChargeKnockdownService
{
    // Weight-driven two-branch decision (spec mechanic 5). true/false = owned verdict for a horse
    // charge; null = not a horse charge / disabled / shrug-off — fall through to base.
    bool? DecideChargeKnockdown(in ChargeKnockdownContext context);
}
