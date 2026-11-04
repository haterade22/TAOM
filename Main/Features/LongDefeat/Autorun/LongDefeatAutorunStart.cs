using System;
using TAOM.Core.Logging;

namespace TAOM.Features.LongDefeat.Autorun;

/// <summary>
/// How the client turns a plan into a driver. The driver takes the config catalog, so it is built only for an armed plan: an unarmed
/// player session resolves the plan and nothing else. A set variable that cannot arm the run is logged here, once, because no driver
/// exists to announce it.
/// </summary>
internal static class LongDefeatAutorunStart
{
    /// <summary>The driver for this process, or null when <c>TAOM_LONGDEFEAT_AUTORUN</c> is unset or unusable.</summary>
    internal static LongDefeatAutorunDriver? Driver(LongDefeatAutorunPlan plan, Func<LongDefeatAutorunDriver> build, IModLogger logger)
    {
        if (plan.Armed)
            return build();

        if (plan.Problem != null)
            logger.LogWarning("[LongDefeat][Autorun] not armed: " + plan.Problem);

        return null;
    }
}
