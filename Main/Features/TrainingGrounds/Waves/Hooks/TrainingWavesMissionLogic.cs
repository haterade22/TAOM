using System;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.TrainingGrounds.Waves.Hooks;

/// <summary>
/// The mission side of the wave fight (docs/features/training-waves.md), joined to vanilla's "ArenaPracticeFight" mission, the town's
/// Training Grounds. Thin (ADR-002): it finds vanilla's practice controller, hands the yard to a <see cref="WaveFightDirector"/>, calls it
/// once each tick behind a guard and settles the run when the mission ends. Without that controller (every other mission) it does
/// nothing. It is a <see cref="MissionLogic"/>, never a bare <see cref="MissionBehavior"/> (the null in the engine's logic list), it
/// spawns nothing in <c>AfterStart</c>, and it reads no engine callback that can run off the main thread: counts are polled in the tick.
/// </summary>
internal sealed class TrainingWavesMissionLogic : MissionLogic
{
    private readonly WaveBetService _bets;
    private readonly ITrainingWavesConfigProvider _config;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly ScreenFaultLog _faults;
    private WaveFightDirector? _director;

    public TrainingWavesMissionLogic(WaveBetService bets, ITrainingWavesConfigProvider config, IOutcomeLog outcomes, IModLogger logger)
    {
        _bets = bets;
        _config = config;
        _outcomes = outcomes;
        _logger = logger;
        _faults = new ScreenFaultLog(logger, "TrainingWaves");
    }

    public override void AfterStart()
    {
        try
        {
            var controller = Mission.GetMissionBehavior<ArenaPracticeFightMissionController>();
            if (controller == null)
                return;

            var arena = new ArenaWaveArena(Mission, controller, _logger);
            _director = new WaveFightDirector(_bets, arena, _config, _outcomes, _logger, n => MBRandom.RandomInt(n));
            _director.Attach();
        }
        catch (Exception ex)
        {
            _faults.LogOnce("after start", ex);
            _director = null;
        }
    }

    public override void OnMissionTick(float dt)
    {
        var director = _director;
        if (director == null)
            return;

        try
        {
            director.Tick(Mission.CurrentTime);
        }
        catch (Exception ex)
        {
            _faults.LogOnce("mission tick", ex);
            _faults.Run("settling a fault", () => director.Fault());
        }
    }

    /// <summary>During a fight the player is asked first, as vanilla asks in its own practice fight; leaving then forfeits the stake.</summary>
    public override InquiryData? OnEndMissionRequest(out bool canPlayerLeave)
    {
        canPlayerLeave = true;
        if (_director?.FightInProgress != true)
            return null;

        return new InquiryData(
            new TextObject("{=zv49qE35}Practice Fight").ToString(),
            GameTexts.FindText("str_give_up_fight").ToString(),
            true,
            true,
            GameTexts.FindText("str_ok").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            Mission.OnEndMissionResult,
            null);
    }

    protected override void OnEndMission()
    {
        _faults.Run("mission end", () => _director?.MissionEnding());
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        _faults.Run("removal", () => _director?.Detach());
        _director = null;
    }
}
