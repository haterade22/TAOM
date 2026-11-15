using System;
using TAOM.Core.Logging;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>
/// How the client turns a plan into a driver: the driver only when the plan is armed. A set variable that cannot arm the run is
/// logged here, once, because no driver exists to announce it.
/// </summary>
internal static class ScenarioAutorunStart
{
    /// <summary>The driver for this process, or null when <c>TAOM_AUTORUN</c> is unset or unusable.</summary>
    internal static ScenarioAutorunDriver? Driver(ScenarioPlan plan, Func<ScenarioAutorunDriver> build, IModLogger logger)
    {
        if (plan.Armed)
            return build();

        if (plan.Problem != null)
            logger.LogWarning("[Autorun][Scenario] not armed: " + plan.Problem);

        return null;
    }
}
