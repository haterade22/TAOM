using System;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.LostFooting.Hooks;

/// <summary>
/// Entry point of the Lost Footing census (#894, docs/features/lost-footing.md): ticks the per-mission service from
/// <c>OnMissionTick</c>, the one place every read runs (main thread, never overlapping the agent tick). No event
/// subscription, no Harmony patch, and nothing it does changes the battle. A fresh instance, and so fresh state, is
/// built for every mission. The service owns the clock, the gate and the log lines; this class only ticks, catches
/// and asks for the end-of-battle summary.
///
/// Lifecycle (v1.5.5): <c>Mission.EndMissionInternal</c> runs <c>OnEndMissionInternal</c> over every behavior (which
/// calls <see cref="OnEndMission"/>) before the agents are removed, and <c>RemoveMissionBehavior</c> runs
/// <c>OnRemoveBehavior</c>, so a teardown that skips the first still ends here. The summary is written once.
/// </summary>
public sealed class LostFootingMissionLogic : MissionLogic
{
    private readonly LostFootingService _service;
    private readonly IModLogger _logger;

    private bool _disabled;

    public LostFootingMissionLogic(LostFootingService service, IModLogger logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_disabled) return;

        try
        {
            _service.Tick(dt);
        }
        catch (Exception ex)
        {
            // Never take a battle down over a measurement: this mission runs unobserved from here.
            _disabled = true;
            _logger.LogError($"[LostFooting] disabled for this mission after {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnEndMission() => Finish();

    public override void OnRemoveBehavior()
    {
        Finish();
        base.OnRemoveBehavior();
    }

    private void Finish()
    {
        try
        {
            _service.Finish();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[LostFooting] summary at mission end failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
