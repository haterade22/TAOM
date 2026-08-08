using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Validation;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Mission-side merit sampler (the donor's cleanest code, ported): every SampleInterval
/// seconds while the player agent is active, sample cohesion (near formation captain),
/// commander proximity (nearest allied hero) and engagement (nearest enemy); count the
/// player's kills; submit ONE sample at mission end. `: MissionLogic` — NEVER
/// MissionBehavior (BehaviorTreeMissionLogic regression rule). Registered UNCONDITIONALLY
/// from SubModule (the donor's mission.Mode gate at init time never fired — Mode is still
/// StartUp there); all filtering happens inside. This class keeps only lifecycle and sample
/// assembly: <see cref="MeritGeometryScanner"/> owns the engine scan and
/// <see cref="MeritGeometryAccumulator"/> owns the thresholds and ratios.
/// </summary>
public class EnlistmentMeritMissionBehavior : MissionLogic
{
    /// <summary>Mirrors <see cref="MeritScoringConfig.SampleIntervalSeconds"/>'s compiled default.</summary>
    private const float FallbackSampleIntervalSeconds = 2f;

    /// <summary>Going down within this many seconds of the first tick earns the fell-early penalty.</summary>
    private const float FellEarlySeconds = 60f;

    private readonly IEnlistmentStateQuery _query;
    private readonly IBattleMeritAccumulator _accumulator;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly MeritGeometryAccumulator _geometry;
    private readonly float _sampleInterval;

    private bool _active;
    private bool _battleResolved;
    private float _sampleClock;
    private int _kills;
    private float _downSince = -1f;
    private float _missionStart = -1f;
    private bool _fellEarly;

    public EnlistmentMeritMissionBehavior(
        IEnlistmentStateQuery query,
        IBattleMeritAccumulator accumulator,
        IEnlistmentContentStore contentStore,
        MeritScoringConfig scoring)
    {
        _query = query;
        _accumulator = accumulator;
        _contentStore = contentStore;
        _geometry = new MeritGeometryAccumulator(scoring);

        // The provider clamps sampleIntervalSeconds to [0.5,30], but this behavior is handed the
        // config object directly and cannot assume it came from there. A non-finite interval would
        // defeat the cadence gate below, turning a 2-second sampler into a per-frame one.
        var interval = scoring?.SampleIntervalSeconds ?? 0f;
        _sampleInterval = FiniteFloatValidator.IsFiniteInRange(interval, 0.1f, 60f)
            ? interval
            : FallbackSampleIntervalSeconds;
    }

    public override void AfterStart()
    {
        // Self-filter: campaign battles during enlisted service only.
        _active = Campaign.Current != null
            && _query.IsEnlisted
            && _query.State == Domain.EnlistmentState.EnlistedBattle;
    }

    /// <summary>
    /// The battle reached a verdict. Verified against v1.4.7 <c>Mission.cs</c>:
    /// <c>Mission.MissionResult</c> is assigned in exactly one place, <c>CheckMissionEnded</c>,
    /// which calls this on every MissionLogic in the same block. A player-initiated exit instead
    /// reaches <c>RetreatMission()</c>/<c>SurrenderMission()</c> and then <c>EndMission()</c>,
    /// which never produces a result — so this does not fire for a walkout. The argument may
    /// legitimately be null, so latch on the CALL and never on the argument.
    /// </summary>
    public override void OnMissionResultReady(MissionResult missionResult) => _battleResolved = true;

    public override void OnMissionTick(float dt)
    {
        if (!_active)
            return;
        if (_missionStart < 0f)
            _missionStart = Mission.CurrentTime;

        _sampleClock += dt;
        // Positive requirement, not `< interval` — a poisoned clock fails the gate and skips the
        // sample instead of falling straight through into one.
        if (!(_sampleClock >= _sampleInterval))
            return;
        _sampleClock = 0f;

        var main = Mission.MainAgent;
        if (main == null || !main.IsActive())
        {
            if (_downSince < 0f)
            {
                _downSince = Mission.CurrentTime;
                _fellEarly = Mission.CurrentTime - _missionStart < FellEarlySeconds;
            }
            return;
        }

        MeritGeometryScanner.Sample(Mission, main, _geometry);
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (!_active || affectorAgent == null || !affectorAgent.IsMainAgent)
            return;
        if (affectedAgent?.Team != null && affectorAgent.Team != null && affectedAgent.Team.IsEnemyOf(affectorAgent.Team))
            _kills++;
    }

    protected override void OnEndMission()
    {
        if (!_active)
            return;
        _active = false;

        var sample = new MeritSample
        {
            Kills = _kills,
            SurvivalRatio = _downSince < 0f ? 1f : 0f,
            // No verdict means the player ended this mission themselves. Before this flag, walking
            // out at t=5s banked the full survival weight — never went down, so "survived".
            LeftTheField = !_battleResolved,
            CohesionRatio = _geometry.CohesionRatio,
            CommanderProximityRatio = _geometry.CommanderProximityRatio,
            EngagementRatio = _geometry.EngagementRatio,
            FellEarly = _fellEarly,
            AverageEnemyDistance = _geometry.AverageEnemyDistance,
        };
        sample.RoleFit = RoleFitEvaluator.Evaluate(_contentStore.Record.Assignment, sample);
        _accumulator.Submit(sample);
    }
}
