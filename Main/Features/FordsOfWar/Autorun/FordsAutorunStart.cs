using System;
using TAOM.Core.Logging;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>
/// How the client turns a plan into a driver. The driver takes the Fords config catalog, so it is built only for an armed plan: an
/// unarmed player session resolves the plan and nothing else. A set variable that cannot arm the run is logged here, once, because
/// no driver exists to announce it.
/// </summary>
internal static class FordsAutorunStart
{
    /// <summary>The driver for this process, or null when <c>TAOM_FORDS_AUTORUN</c> is unset or unusable.</summary>
    internal static FordsAutorunDriver? Driver(FordsAutorunPlan plan, Func<FordsAutorunDriver> build, IModLogger logger)
    {
        if (plan.Armed)
            return build();

        if (plan.Problem != null)
            logger.LogWarning("[FordsOfWar][Autorun] not armed: " + plan.Problem);

        return null;
    }
}
