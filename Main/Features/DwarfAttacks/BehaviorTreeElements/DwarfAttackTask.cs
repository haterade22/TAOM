using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.DwarfAttacks.Hooks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DwarfAttacks.BehaviorTreeElements;

/// <summary>
/// One attack, the one <see cref="Chosen"/> names (BruteForceTask's pattern). First tick: start the action on channel
/// 0, stamp that attack's cooldown (an interrupted attack still spends it), and keep the dwarf's facing. Then keep
/// running while the clip plays; when its progress reaches the attack's impact fraction, deliver the blow once. If the
/// action is replaced first (a hit reaction or a fall outranks it), finish with no blow. The action may show a frame
/// late, so it gets a short grace before "not ours" counts as lost. Main thread: the trees tick from
/// DwarfAttacksMissionBehavior.OnMissionTick (#592). Must not throw (a throw stops the tree for the battle).
/// </summary>
public class DwarfAttackTask : BTTask, IBTBannerlordBase, IBTDwarfAttacksBlackboard
{
    private const float StartGraceSeconds = 0.5f;

    private readonly DwarfAttackService _service;
    private readonly DwarfAttacksSettingsProvider _settings;
    private readonly DwarfAttackChecks _checks;
    private bool _running;
    private bool _seenOurs;
    private float _startedAt;
    private DwarfAttack _attack;
    private ActionIndexCache _action = ActionIndexCache.act_none;
    private Vec2 _startPosition;
    private Vec2 _forward;

    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<float?> _headButtLastFired;
    private BTBlackboardValue<float?> _dropKickLastFired;
    private BTBlackboardValue<DwarfAttack?> _chosen;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }
    public BTBlackboardValue<float?> HeadButtLastFired { get => _headButtLastFired; set => _headButtLastFired = value; }
    public BTBlackboardValue<float?> DropKickLastFired { get => _dropKickLastFired; set => _dropKickLastFired = value; }
    public BTBlackboardValue<DwarfAttack?> Chosen { get => _chosen; set => _chosen = value; }

    public DwarfAttackTask(DwarfAttackService service, DwarfAttacksSettingsProvider settings, DwarfAttackChecks checks)
    {
        _service = service;
        _settings = settings;
        _checks = checks;
    }

    public override BTTaskStatus Execute()
    {
        Agent dwarf = Agent.GetValue();
        Mission mission = Mission.Current;
        // The tree holds this handle across frames; a recycled slot answers for its new tenant (#592).
        if (dwarf == null || mission == null || !AgentSlotIdentity.IsCurrentOccupant(dwarf) || !dwarf.IsActive())
            return Finish(BTTaskStatus.FinishedWithFalse);

        if (!_running) return Start(dwarf, mission);

        bool ours = dwarf.GetCurrentAction(0) == _action;
        if (ours) _seenOurs = true;
        float progress = ours ? dwarf.GetCurrentActionProgress(0) : -1f;

        if (!ours && (_seenOurs || mission.CurrentTime - _startedAt > StartGraceSeconds))
        {
            // "Never played" is a data fault only when this dwarf's action set has no clip for the action. A hit
            // reaction can replace the attack before a tick sees it, which is an ordinary interruption.
            if (_seenOurs || MBActionSet.CheckActionAnimationClipExists(dwarf.ActionSet, _action))
                _checks.RecordCutShort(_attack);
            else
                _checks.RecordNeverPlayed(_attack, dwarf.Name);
            return Finish(BTTaskStatus.FinishedWithFalse);
        }
        if (!ours || !_service.HasReachedImpact(_attack, progress)) return BTTaskStatus.Running;

        DwarfAttackHitResult hit = DwarfAttackHit.Deliver(mission, dwarf, _attack, _forward, _service, _settings.DamagePercent);
        if (hit.Victim == null)
            _checks.RecordNoVictim(_attack);
        else if (hit.Damage > 0)    // at 0 % damage no blow lands, so there is no hit to prove
            _checks.RecordHit(_attack, dwarf.Name, hit.Victim, hit.Distance, progress,
                (dwarf.Position.AsVec2 - _startPosition).Length, hit.Damage, hit.KnockedDown);
        return Finish(BTTaskStatus.FinishedWithTrue);
    }

    private BTTaskStatus Start(Agent dwarf, Mission mission)
    {
        if (Chosen.GetValue() is not DwarfAttack attack) return BTTaskStatus.FinishedWithFalse;
        Chosen.SetValue(null);

        Vec2 look = dwarf.LookDirection.AsVec2;
        if (!(look.Length > 1e-4f)) return BTTaskStatus.FinishedWithFalse;

        ActionIndexCache action = DwarfAttacksCombat.For(attack);
        if (!dwarf.SetActionChannel(0, action, ignorePriority: true, additionalFlags: DwarfAttacksCombat.StartFlags))
            return BTTaskStatus.FinishedWithFalse;

        _running = true;
        _seenOurs = false;
        _attack = attack;
        _action = action;
        _startedAt = mission.CurrentTime;
        _startPosition = dwarf.Position.AsVec2;
        // The victim is judged against the facing the attack started with: the clip may turn the body (the drop kick is side-on).
        _forward = look.Normalized();
        (attack == DwarfAttack.HeadButt ? HeadButtLastFired : DropKickLastFired).SetValue(_startedAt);
        dwarf.MakeVoice(SkinVoiceManager.VoiceType.Yell, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
        return BTTaskStatus.Running;
    }

    private BTTaskStatus Finish(BTTaskStatus status)
    {
        _running = false;
        _seenOurs = false;
        return status;
    }
}
