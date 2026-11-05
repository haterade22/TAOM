using TAOM.Core.Logging;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// Writes the Patch98 window lines for <c>MissionTickProfilerBehavior</c>, keeping it thin (ADR-002). Every
/// line is INFO (FileLogger flushes it synchronously), at most one window per 5 s. The window's top lists are
/// empty when its mission does not attribute (<see cref="MissionTickProfiler.TakeExtrasWindow"/>), so they read
/// <c>none</c> with no second check here.
/// </summary>
public static class ProbeWindowWriter
{
    /// <summary>Right after <c>[TickProfile]</c>: <c>[SpawnProfile]</c> when the window had spawns, then
    /// <c>[ScriptProfile]</c>, then <c>[AnimLoad]</c> while the clip-loading sampler is on.</summary>
    public static void WriteExtras(IModLogger logger, double tSeconds, ExtrasWindow w)
    {
        if (w.Spawns > 0)
            logger.LogInfo(HitchProbeLines.BuildSpawnProfile(tSeconds, w));
        logger.LogInfo(HitchProbeLines.BuildScriptProfile(tSeconds, w));
        if (w.AnimSampling)
            logger.LogInfo(HitchProbeLines.BuildAnimLoad(tSeconds, w));
    }
}
