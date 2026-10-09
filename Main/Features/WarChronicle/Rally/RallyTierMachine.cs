using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle.Rally;

/// <summary>
/// The rally's hysteresis, pure. Tier 0 rises to 1 at the tier 1 enter loss and to 2 at the tier 2 enter
/// loss (a jump from 0 is allowed); tier 2 falls to 1 below the tier 2 exit loss; any tier falls to 0
/// below the tier 1 exit loss. Between an exit and its enter the tier holds, so a loss hovering around a
/// threshold does not flap. Every comparison is written as a positive requirement, and a non-finite
/// loss fails closed to tier 0 (no help is granted from a number nobody can trust).
/// </summary>
public static class RallyTierMachine
{
    /// <summary>The tier after a day at <paramref name="loss"/> (a fraction of the baseline lost), from <paramref name="current"/>.</summary>
    public static int Next(int current, float loss, RallyConfig config)
    {
        if (!FiniteFloatValidator.IsFinite(loss))
            return 0;

        if (loss >= config.Tier2.EnterLoss)
            return 2;

        // An unknown stored tier is read as none, so a corrupt row can neither hold nor skip a tier.
        var tier = current == 1 || current == 2 ? current : 0;
        if (tier == 0)
            return loss >= config.Tier1.EnterLoss ? 1 : 0;

        if (!(loss >= config.Tier1.ExitLoss))
            return 0;

        if (tier == 2 && !(loss >= config.Tier2.ExitLoss))
            return 1;

        return tier;
    }
}
