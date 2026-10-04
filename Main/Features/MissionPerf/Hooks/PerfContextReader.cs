using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using TAOM.Adapters;
using TAOM.Features.BattleLoadDiagnostics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// Gathers the values of one <c>[PerfContext]</c> line at a mission's first tick: the build flavour (read at
/// run time from TAOM's own <see cref="DebuggableAttribute"/>, ADR-005), the CLR and GC posture, the mission's
/// scene and agent count, the player's quality options, memory headroom, and which diagnostics are on. Each
/// engine read has its own try/catch, falling back to -1 (written <c>na</c>, or -1 for agents) or
/// <c>unknown</c>, so one bad read costs one field, never the line; the first failure (a memory read that
/// returns false included) comes back as one status line for the caller to log.
/// </summary>
internal static class PerfContextReader
{
    internal static PerfContext Read(Mission mission, IGraphicsOptionsAdapter options,
        IBattleLoadDiagnosticsSettingsProvider settings, int missionInProcess, bool measuring, out string? faultLine)
    {
        string? fault = null;
        T Safe<T>(string field, Func<T> read, T fallback)
        {
            try { return read(); }
            catch (Exception ex)
            {
                fault ??= TickProfileLines.BuildContextReadFault(field, ex);
                return fallback;
            }
        }

        var jitDisabled = Safe("build", () =>
            typeof(MissionTickProfilerBehavior).Assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false, false);
        var memLoad = -1;
        var availPhysMb = -1L;
        if (MemorySampleReader.TryRead(out var sample))
        {
            memLoad = sample.MemLoadPercent;
            availPhysMb = sample.AvailPhysMb;
        }
        else
        {
            fault ??= TickProfileLines.MemoryReadFailedLine;
        }

        var context = new PerfContext(
            jitDisabled ? "Debug" : "Release",
            !jitDisabled,
            Environment.Version.ToString(),
            GCSettings.IsServerGC,
            GCSettings.LatencyMode.ToString(),
            missionInProcess,
            Safe("scene", () => mission.SceneName, "unknown"),
            Safe("agents", () => mission.AllAgents.Count, -1),
            Safe("textureQuality", () => options.TextureQualityOption, -1),
            Safe("shadowQuality", () => options.ShadowmapResolutionOption, -1),
            Safe("particleDetail", () => options.ParticleDetailOption, -1),
            Safe("ragdolls", () => options.RagdollOption, -1),
            memLoad,
            availPhysMb,
            measuring,
            Safe<System.Collections.Generic.IReadOnlyList<string>>("diag", () => TickProfileLines.DiagTokens(
                settings.IsEnabled, settings.StallWatchdogEnabled, settings.StallWatchdogBundleEnabled,
                settings.ExitStallSamplerEnabled, settings.MissionTickStallSamplerEnabled, settings.MemorySamplerEnabled,
                BattleLoadDiagnosticsSettings.Instance?.EnableMissionPerfHeartbeat ?? true),
                Array.Empty<string>()));
        faultLine = fault;
        return context;
    }
}
