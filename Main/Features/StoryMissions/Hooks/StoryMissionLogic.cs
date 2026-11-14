using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.StoryMissions.Hooks;

/// <summary>
/// The end of a story mission (#1105): polls the Long Defeat run's result, shows an end cue, and ends the mission about five seconds later
/// with <c>Mission.EndMission</c>, so the engine pops back to the campaign map. MUST be <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md).
/// It spawns and throws nothing in <c>AfterStart</c> (it does not override it). Both exits tell the watch the mission is gone. The decisions
/// live in <see cref="StoryEndWatch"/>; this class only reads the session and the engine's clock.
/// </summary>
public sealed class StoryMissionLogic : MissionLogic
{
    private readonly LongDefeatSession _session;
    private readonly StoryEndWatch _watch;
    private readonly IModLogger _logger;
    private readonly Action<StoryOutcome>? _onVerdict;
    private bool _cueShown;
    private bool _faultLogged;
    private bool _verdictReported;

    /// <param name="onVerdict">Called once, on the main thread, with the mission's verdict: when the run has one, or at an exit before it.</param>
    internal StoryMissionLogic(LongDefeatSession session, StorySide side, IModLogger logger, IOutcomeLog outcomes, string missionKey, string? music,
        Action<StoryOutcome>? onVerdict = null)
    {
        _session = session;
        _logger = logger;
        _watch = new StoryEndWatch(side, logger, outcomes, missionKey);
        MissionKey = missionKey;
        Music = music;
        _onVerdict = onVerdict;
    }

    /// <summary>The launcher's serial for this mission, the key of its check lines.</summary>
    internal string MissionKey { get; }

    /// <summary>The mission's own track under ModuleSounds, or null. The client view list reads it: behaviors join the mission before views.</summary>
    internal string? Music { get; }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        try
        {
            if (_watch.Step(dt, _session.Result, _session.Fault != null))
                Mission.EndMission();
            if (!_cueShown && _watch.Verdict is { } verdict)
            {
                _cueShown = true;
                InformationManager.DisplayMessage(new InformationMessage(new TextObject(CueText(verdict)).ToString()));
                ReportVerdict();
            }
        }
        catch (Exception ex)
        {
            if (!_faultLogged)
                _logger.LogError($"[StoryMissions] the end watch failed ({ex.GetType().Name}: {ex.Message}) {ex.StackTrace}");
            _faultLogged = true;
        }
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _watch.Left(_session.Result, _session.Fault != null);
        ReportVerdict();
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        _watch.Left(_session.Result, _session.Fault != null);
        ReportVerdict();
    }

    private void ReportVerdict()
    {
        if (_verdictReported || _onVerdict == null || !(_watch.Verdict is { } verdict))
            return;

        _verdictReported = true;
        try
        {
            _onVerdict(verdict);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[StoryMissions] the verdict callback failed ({ex.GetType().Name}: {ex.Message}) {ex.StackTrace}");
        }
    }

    private static string CueText(StoryOutcome verdict) => verdict switch
    {
        StoryOutcome.Won => "{=!}Story mission won. Returning to the map.",
        StoryOutcome.Lost => "{=!}Story mission lost. Returning to the map.",
        _ => "{=!}Story mission ended with no verdict. Returning to the map.",
    };
}
