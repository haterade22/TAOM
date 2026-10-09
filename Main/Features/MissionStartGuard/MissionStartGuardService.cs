// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MissionStartGuard.Models;

namespace TAOM.Features.MissionStartGuard;

/// <summary>
/// Decides whether a throw out of a mission start call is survived, and reports it
/// (docs/features/mission-start-guard.md). Pure: every engine or UI call goes through
/// <see cref="IMissionStartGuardAdapter"/>. Everything public is safe to call from inside a catch block: it never
/// throws, and a failing log or message costs only that output.
///
/// <para>State is per mission: the counts reset when a new mission begins (<see cref="BeginMission"/>), not when the
/// same mission's <c>AfterStart</c> runs again, so an escaped throw that makes the engine load the mission every frame
/// cannot refill the budgets each frame. Every caller runs on the main thread (<c>Mission.AfterStart</c> and the start
/// calls inside it), so the state takes no lock.</para>
///
/// <para>The prefix is the canary for a lost guard: it hands the finalizer a per-call flag through Harmony's
/// <c>__state</c>, and <see cref="EndMissionStart"/> (the finalizer, which PatchShield never strips) writes one warning
/// per process when that call's prefix did not run, or when the transpiler's live swap count is not
/// <see cref="ExpectedSites"/>.</para>
/// </summary>
public sealed class MissionStartGuardService : IMissionStartGuardService
{
    /// <summary>The start calls the transpiler wraps inside <c>Mission.AfterStart</c>.</summary>
    internal const int ExpectedSites = 6;

    internal const int MaxNoticesPerMission = 3;
    internal const int MaxFullLogsPerMission = 10;
    internal const int MaxEscapedLogsPerProcess = 3;

    private readonly IMissionStartGuardSettingsProvider _settings;
    private readonly IMissionStartGuardAdapter _adapter;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    private WeakReference<object>? _mission;
    private int _caught;
    private int _notices;
    private bool _summaryWritten;
    private int _escapedLogged;
    private bool _guardWarned;

    public MissionStartGuardService(IMissionStartGuardSettingsProvider settings, IMissionStartGuardAdapter adapter,
        IDedicatedServerProvider server, IModLogger logger)
    {
        _settings = settings;
        _adapter = adapter;
        _server = server;
        _logger = logger;
    }

    public bool ShouldSurvive(Exception exception) =>
        exception != null && !(exception is OutOfMemoryException) && ToggleOn();

    public void BeginMission(object? missionToken)
    {
        if (missionToken != null && _mission != null && _mission.TryGetTarget(out var current)
            && ReferenceEquals(current, missionToken))
            return;

        _mission = missionToken == null ? null : new WeakReference<object>(missionToken);
        _caught = 0;
        _notices = 0;
        _summaryWritten = false;
    }

    public void Report(StartCall call, string ownerType, string ownerAssembly, Exception exception)
    {
        _caught++;
        if (_caught <= MaxFullLogsPerMission)
            Safe(() => _logger.LogError(MissionStartGuardLines.BuildCaughtLine(call, ownerType, ownerAssembly, exception)));
        else if (_caught == MaxFullLogsPerMission + 1)
            Safe(() => _logger.LogWarning(MissionStartGuardLines.BuildSuppressedWarning(MaxFullLogsPerMission)));

        ShowNoticeIfAllowed(call, ownerAssembly, exception);
    }

    public void EndMissionStart(bool prefixRan, int liveSwaps)
    {
        if (!_guardWarned)
        {
            if (!prefixRan)
                WarnGuardLost(MissionStartGuardLines.BuildPrefixLostWarning());
            else if (liveSwaps != ExpectedSites)
                WarnGuardLost(MissionStartGuardLines.BuildSwapsLostWarning(liveSwaps, ExpectedSites));
        }

        if (_caught == 0 || _summaryWritten) return;
        _summaryWritten = true;
        Safe(() => _logger.LogWarning(MissionStartGuardLines.BuildSummaryLine(_caught, _notices)));
    }

    public void ReportEscaped(Exception exception)
    {
        if (_escapedLogged >= MaxEscapedLogsPerProcess) return;
        _escapedLogged++;
        Safe(() => _logger.LogError(MissionStartGuardLines.BuildEscapedLine(exception, _escapedLogged, MaxEscapedLogsPerProcess)));
    }

    public void LogInstall(int wrappedSites)
    {
        if (wrappedSites == ExpectedSites)
        {
            Safe(() => _logger.LogInfo(MissionStartGuardLines.BuildInstallLine(wrappedSites, ToggleOn())));
            return;
        }

        _guardWarned = true;   // the install warning says the guard is off; the canary need not say it again
        Safe(() => _logger.LogWarning(MissionStartGuardLines.BuildSoftFailWarning(wrappedSites, ExpectedSites)));
    }

    private void WarnGuardLost(string line)
    {
        _guardWarned = true;
        Safe(() => _logger.LogWarning(line));
    }

    private void ShowNoticeIfAllowed(StartCall call, string ownerAssembly, Exception exception)
    {
        Safe(() =>
        {
            if (_server.IsDedicatedServer || _notices >= MaxNoticesPerMission) return;
            _notices++;
            _adapter.ShowNotice(ownerAssembly, call.ToString(), exception.GetType().Name);
        });
    }

    private bool ToggleOn()
    {
        try { return _settings.SurviveMissionStartFailures; }
        catch { return true; }
    }

    /// <summary>Runs one output step; a failing log or message costs only itself.</summary>
    private static void Safe(Action step)
    {
        try { step(); }
        catch { /* the report runs inside a catch block of the engine's start call: it must not throw */ }
    }
}
