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
    private bool _cueShown;
    private bool _faultLogged;

    internal StoryMissionLogic(LongDefeatSession session, StorySide side, IModLogger logger, IOutcomeLog outcomes, string missionKey)
    {
        _session = session;
        _logger = logger;
        _watch = new StoryEndWatch(side, logger, outcomes, missionKey);
    }

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
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        _watch.Left(_session.Result, _session.Fault != null);
    }

    private static string CueText(StoryOutcome verdict) => verdict switch
    {
        StoryOutcome.Won => "{=!}Story mission won. Returning to the map.",
        StoryOutcome.Lost => "{=!}Story mission lost. Returning to the map.",
        _ => "{=!}Story mission ended with no verdict. Returning to the map.",
    };
}
