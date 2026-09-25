using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.TrollBruteForce.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce.BehaviorTreeElements;

/// <summary>
/// The smash itself. First tick: start the action on channel 0 and stamp the cooldown (an interrupted smash still
/// spends it). Then keep running while the clip plays; when its progress reaches the impact fraction, deliver the
/// ring once. If the action is replaced first (a hit reaction or a fall outranks it), finish with no ring. The
/// action may show a frame late, so it gets a short grace before "not ours" counts as lost. Main thread: the trees
/// tick from BehaviorTreeMissionLogic.OnMissionTick (#592). Must not throw (a throw stops the tree for the battle).
/// </summary>
public class BruteForceTask : BTTask, IBTBannerlordBase, IBTTrollBruteForceBlackboard
{
    private const float StartGraceSeconds = 0.5f;

    private ITrollBruteForceService? _service;
    private IModLogger? _logger;
    private bool _running;
    private bool _seenOurs;
    private float _startedAt;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<float?> _lastFired;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<float?> LastFired { get => _lastFired; set => _lastFired = value; }

    public override BTTaskStatus Execute()
    {
        Agent troll = Agent.GetValue();
        Mission mission = Mission.Current;
        // The tree holds this handle across frames; a recycled slot answers for its new tenant (#592).
        if (troll == null || mission == null || !troll.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(troll))
            return Finish(BTTaskStatus.FinishedWithFalse);

        _service ??= IoC.Resolve<ITrollBruteForceService>();
        _logger ??= IoC.Resolve<IModLogger>();

        if (!_running) return Start(troll, mission);

        bool ours = troll.GetCurrentAction(0) == TrollBruteForceCombat.Action;
        if (ours) _seenOurs = true;
        float progress = ours ? troll.GetCurrentActionProgress(0) : -1f;

        if (!ours && (_seenOurs || mission.CurrentTime - _startedAt > StartGraceSeconds))
        {
            _logger.LogInfo($"[TrollBruteForce] {troll.Name}'s smash was cut short before impact " +
                $"({(_seenOurs ? "action replaced" : "action never showed")}).");
            return Finish(BTTaskStatus.FinishedWithFalse);
        }
        if (!ours || !_service.HasReachedImpact(progress)) return BTTaskStatus.Running;

        BruteForceRingResult ring = BruteForceRing.Deliver(mission, troll, _service);
        _logger.LogInfo($"[TrollBruteForce] Brute Force by {troll.Name} at progress {progress:0.00}: " +
            $"ring={ring.Hit} hit, {ring.KnockedDown} knocked down, {ring.Skipped} skipped.");
        return Finish(BTTaskStatus.FinishedWithTrue);
    }

    private BTTaskStatus Start(Agent troll, Mission mission)
    {
        if (!troll.SetActionChannel(0, TrollBruteForceCombat.Action, ignorePriority: true,
                additionalFlags: TrollBruteForceCombat.StartFlags))
            return BTTaskStatus.FinishedWithFalse;

        _running = true;
        _seenOurs = false;
        _startedAt = mission.CurrentTime;
        LastFired.SetValue(_startedAt);
        troll.MakeVoice(SkinVoiceManager.VoiceType.Yell, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
        return BTTaskStatus.Running;
    }

    private BTTaskStatus Finish(BTTaskStatus status)
    {
        _running = false;
        _seenOurs = false;
        return status;
    }
}
