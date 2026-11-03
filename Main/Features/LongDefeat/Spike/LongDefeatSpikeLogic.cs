using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Spike;

/// <summary>The spike's mission boundary (#1079): it ticks <see cref="LongDefeatSpikeRunner"/> on the main thread and answers team power for <c>TeamQuerySystem</c>.</summary>
internal sealed class LongDefeatSpikeLogic : MissionLogic, IBattlePowerCalculationLogic
{
    // A constant, not a running total: it keeps RemainingPowerRatio and TotalPowerRatio near 1, so power never orders TacticBreachWalls' one-way retreat
    // (ShouldRetreat) or TacticDefendCastle's sally. The spike therefore cannot show that retreat; Phase 1 decides whether waves may retreat.
    private const float TeamPower = 1000000f;

    private readonly LongDefeatSpikeOptions _options;
    private readonly LongDefeatSpikeFortress _fortress;
    private readonly IModLogger _logger;
    private LongDefeatSpikeRunner? _runner;

    public LongDefeatSpikeLogic(LongDefeatSpikeOptions options, LongDefeatSpikeFortress fortress, IModLogger logger)
    {
        _options = options;
        _fortress = fortress;
        _logger = logger;
    }

    public float GetTotalTeamPower(Team team) => TeamPower;

    public override void OnMissionTick(float dt)
    {
        _runner ??= new LongDefeatSpikeRunner(Mission, _options, _fortress, _logger);
        _runner.Tick(dt);
    }
}
