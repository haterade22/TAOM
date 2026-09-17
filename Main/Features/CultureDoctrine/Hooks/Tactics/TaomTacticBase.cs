using System;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Behaviors;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// The vanilla tactic lifecycle (<c>TacticDefensiveEngagement</c>, `TacticDefensiveEngagement.cs`)
/// written once, driven by a <see cref="DoctrinePlan"/>: formation split, battle-joined test,
/// formation-set change test, the pure <see cref="TacticPhaseMachine"/>, then the phase table
/// through <see cref="BehaviorWeightApplier"/>. A subclass supplies the plan, a pure weight
/// function over a <see cref="TeamQuerySnapshot"/>, and any per-apply positions.
///
/// <para>
/// Every member runs on the team-AI tick (`Team.Tick` -> `TeamAI.Tick`), where vanilla's
/// tactics run: the async AI thread in normal play, and the main thread during deployment
/// (`DeploymentMissionController.SetupAIOfEnemyTeam` calls `ResetTactic` and `Team.Tick(0)`)
/// and in fast-forward. Never concurrently with `OnMissionTick`, which `Mission.OnPreTick`
/// serialises through `WaitTickCompletion`. The rules that follow: the only state is what the
/// engine hands over plus immutable values from the constructor; no IoC, no settings, no logger;
/// every override body is wrapped, and a throw sets <see cref="Failed"/>, after which the weight
/// is 0 and the tick does nothing; formations keep the last-applied weights until the next
/// `MakeDecision` (at most 5 s), which then switches unconditionally because
/// `ResetTacticalPositions` returns false here, as in the six position-free vanilla tactics
/// (DefensiveLine, DefensiveRing and HoldChokePoint return true and re-check the 1.5x on the way
/// out). The status line reads <see cref="Status"/>.
/// </para>
///
/// <para>
/// Over ADR-002's 150 lines: this is the one place the vanilla tactic lifecycle exists in TAOM
/// (vanilla's are 180 to 370 lines each) and the eleven tactics on top are 15 to 45 lines. The
/// split code writes <c>TacticComponent</c>'s protected fields and cannot leave the class; the
/// decisions are in <see cref="TacticPhaseMachine"/>, <see cref="DoctrineWeights"/>,
/// <see cref="FormationSlots"/>, <see cref="VolleyControl"/> and <see cref="BehaviorWeightApplier"/>.
/// </para>
/// </summary>
public abstract class TaomTacticBase : TacticComponent
{
    private readonly DoctrinePlan _plan;
    private readonly float _multiplier;
    private readonly TacticPhaseMachine _machine = new TacticPhaseMachine();
    private readonly VolleyControl _volley = new VolleyControl();
    private Formation? _cavalry;
    private Formation? _secondInfantry;
    private Formation? _leftWing;
    private Formation? _rightWing;
    private Formation? _vanguard;
    private bool _joined;
    private volatile bool _failed;
    private volatile string _status = "idle";

    protected TaomTacticBase(Team team, DoctrinePlan plan, float multiplier)
        : base(team)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _multiplier = multiplier;
    }

    public DoctrinePlan Plan => _plan;
    public bool Failed => _failed;
    public string Status => _status;

    /// <summary>The catalog's engagement distances, written once by the installer on the main
    /// thread in <c>EarlyStart</c> before the team ticks, then read on the team-AI tick and
    /// handed to every TAOM behaviour this tactic applies.</summary>
    public EngagementTunables Engagement { get; internal set; } = EngagementTunables.Default;

    internal Formation? MainInfantry => _mainInfantry;
    internal Formation? SecondInfantry => _secondInfantry;
    internal Formation? Archers => _archers;
    internal Formation? Vanguard => _vanguard;

    /// <summary>Set in <see cref="BeforeApply"/> by a tactic whose infantry holds a position.</summary>
    internal WorldPosition DefensePosition { get; set; } = WorldPosition.Invalid;

    /// <summary>Set in <see cref="BeforeApply"/> by a tactic with a second infantry line.</summary>
    internal WorldPosition SecondLinePosition { get; set; } = WorldPosition.Invalid;

    /// <summary>Set in <see cref="BeforeApply"/> by a tactic that uses <c>BehaviorDefensiveRing</c>.</summary>
    internal TacticalPosition? RingPosition { get; set; }

    protected abstract float Weigh(in TeamQuerySnapshot snapshot);

    /// <summary>Compute positions the phase table needs; called before every apply.</summary>
    protected virtual void BeforeApply(TacticPhase phase)
    {
    }

    /// <summary>Once a second while no phase change is due: return true to re-apply the current
    /// phase now (a position-holding tactic uses it when its high-ground race is lost).</summary>
    protected virtual bool OnPhaseTick(TacticPhase phase) => false;

    /// <summary>Appended to the status line after the phase name.</summary>
    protected virtual string StatusSuffix => "";

    protected override float GetTacticWeight()
    {
        if (_failed)
            return 0f;
        try
        {
            return Weigh(TakeSnapshot()) * _multiplier;
        }
        catch (Exception ex)
        {
            Fail(ex);
            return 0f;
        }
    }

    public override void TickOccasionally()
    {
        if (!_failed)
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }
        // Mandatory: clears the team AI's first-tactic flag and stops machines at mission end.
        base.TickOccasionally();
    }

    // Another tactic takes the team: the archers get their firing order back.
    protected override void OnCancel()
    {
        try
        {
            _volley.Release();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        base.OnCancel();
    }

    private void Tick()
    {
        if (!AreFormationsCreated)
            return;
        var joined = _plan.AlwaysEngaged || HasBattleBeenJoined();
        var changed = CheckAndSetAvailableFormationsChanged();
        var phase = _machine.Step(changed, joined, IsTacticReapplyNeeded, _plan.AlwaysEngaged, out var recount);
        if (!phase.HasValue)
        {
            var current = _machine.Current;
            if (!current.HasValue)
                return;
            if (_plan.HasVolleyControl && _volley.Tick(_archers, _plan.Volley))
                _status = StatusLine(current.Value);
            if (!OnPhaseTick(current.Value))
                return;
            phase = current;
        }
        else
        {
            _joined = joined;
            if (recount)
                ManageFormationCounts();
        }
        BeforeApply(phase.Value);
        Apply(phase.Value == TacticPhase.Engage ? _plan.Engage : _plan.Defend, phase.Value);
        IsTacticReapplyNeeded = false;
        _status = StatusLine(phase.Value);
    }

    private string StatusLine(TacticPhase phase) =>
        phase + StatusSuffix + (_plan.HasVolleyControl ? ":" + _volley.Status : "");

    protected override void ManageFormationCounts()
    {
        var formations = FormationsIncludingEmpty;
        Formation? vanguard = null;
        switch (_plan.Split)
        {
            case FormationSplit.OneOneOneOne:
                // TacticFrontalCavalryCharge.ManageFormationCounts: one block per class.
                ManageFormationCounts(1, 1, 1, 1);
                break;
            case FormationSplit.TwoOneTwoOne:
                ManageFormationCounts(2, 1, 2, 1);
                break;
            case FormationSplit.ThreeOneTwoOne:
                ManageFormationCounts(3, 1, 2, 1);
                break;
            case FormationSplit.OneOneTwoOneVanguard:
                // The HeavyCavalry formation is left out of the cavalry consolidation so the
                // routed troops (FormationRouting) stay a block of their own. An EMPTY one is
                // not: the engine's split takes any empty formation as a transfer target
                // before it asks the predicate (`TacticComponent.cs:229-259`), and a slot that
                // holds no vanguard is just the vanilla 1/1/2/1 with the weight already 0.
                vanguard = FormationSlots.VanguardOf(formations);
                if (vanguard == null)
                {
                    ManageFormationCounts(1, 1, 2, 1);
                    break;
                }
                SplitFormationClassIntoGivenNumber(f => f.QuerySystem.IsInfantryFormation, 1);
                SplitFormationClassIntoGivenNumber(f => f.QuerySystem.IsRangedFormation, 1);
                SplitFormationClassIntoGivenNumber(f => f.QuerySystem.IsCavalryFormation && f != vanguard, 2);
                SplitFormationClassIntoGivenNumber(f => f.QuerySystem.IsRangedCavalryFormation, 1);
                vanguard = FormationSlots.VanguardOf(formations);
                break;
            default:
                ManageFormationCounts(1, 1, 2, 1);
                break;
        }
        var slots = FormationSlots.Assign(formations, InfantrySlots(_plan.Split), _plan.Split == FormationSplit.OneOneOneOne, vanguard);
        _mainInfantry = slots.MainInfantry;
        _secondInfantry = slots.SecondInfantry;
        _leftWing = slots.LeftWing;
        _rightWing = slots.RightWing;
        _archers = slots.Archers;
        _leftCavalry = slots.LeftCavalry;
        _rightCavalry = slots.RightCavalry;
        _cavalry = slots.Cavalry;
        _rangedCavalry = slots.RangedCavalry;
        _vanguard = slots.Vanguard;
    }

    private static int InfantrySlots(FormationSplit split) =>
        split == FormationSplit.TwoOneTwoOne ? 2 : split == FormationSplit.ThreeOneTwoOne ? 3 : 1;

    // Vanilla's test, extended with every TAOM slot.
    protected override bool CheckAndSetAvailableFormationsChanged()
    {
        var count = Team.GetAIControlledFormationCount();
        if (count != _AIControlledFormationCount)
        {
            _AIControlledFormationCount = count;
            IsTacticReapplyNeeded = true;
            return true;
        }
        return !FormationSlots.Holds(_mainInfantry, q => q.IsInfantryFormation) || !FormationSlots.Holds(_secondInfantry, q => q.IsInfantryFormation)
            || !FormationSlots.Holds(_leftWing, q => q.IsInfantryFormation) || !FormationSlots.Holds(_rightWing, q => q.IsInfantryFormation)
            || !FormationSlots.Holds(_archers, q => q.IsRangedFormation)
            || !FormationSlots.Holds(_leftCavalry, q => q.IsCavalryFormation) || !FormationSlots.Holds(_rightCavalry, q => q.IsCavalryFormation)
            || !FormationSlots.Holds(_cavalry, q => q.IsCavalryFormation) || !FormationSlots.Holds(_rangedCavalry, q => q.IsRangedCavalryFormation)
            || !FormationSlots.Holds(_vanguard, q => true);
    }

    // Vanilla's test on the leading formation, with the plan's threshold and its doubling once joined.
    private bool HasBattleBeenJoined()
    {
        var lead = _plan.Split == FormationSplit.OneOneOneOne ? _cavalry : _mainInfantry;
        var enemy = lead?.CachedClosestEnemyFormation;
        if (enemy == null || lead!.AI.ActiveBehavior is BehaviorCharge || lead.AI.ActiveBehavior is BehaviorTacticalCharge || lead.AI.ActiveBehavior is BehaviorFootCharge)
            return true;
        var seconds = lead.CachedMedianPosition.AsVec2.Distance(enemy.Formation.CachedMedianPosition.AsVec2) / enemy.MovementSpeedMaximum;
        return seconds <= _plan.BattleJoinedSeconds + (_joined ? _plan.BattleJoinedSeconds : 0f);
    }

    private void Apply(PhasePlan plan, TacticPhase phase)
    {
        if (Team.IsPlayerTeam && !Team.IsPlayerGeneral && Team.IsPlayerSergeant)
            SoundTacticalHorn(phase == TacticPhase.Engage ? AttackHornSoundIndex : MoveHornSoundIndex);
        var formations = plan.Formations;
        for (var i = 0; i < formations.Count; i++)
        {
            var formation = FormationFor(formations[i].Role);
            if (formation != null)
                BehaviorWeightApplier.Apply(formation, formations[i], this);
        }
        if (_plan.HasVolleyControl)
            _volley.Tick(_archers, _plan.Volley);
    }

    private Formation? FormationFor(FormationRole role)
    {
        switch (role)
        {
            case FormationRole.MainInfantry: return _mainInfantry;
            case FormationRole.SecondInfantry: return _secondInfantry;
            case FormationRole.LeftWing: return _leftWing;
            case FormationRole.RightWing: return _rightWing;
            case FormationRole.Archers: return _archers;
            case FormationRole.LeftCavalry: return _leftCavalry;
            case FormationRole.RightCavalry: return _rightCavalry;
            case FormationRole.Cavalry: return _cavalry;
            case FormationRole.RangedCavalry: return _rangedCavalry;
            case FormationRole.Vanguard: return _vanguard;
            default: return null;
        }
    }

    private TeamQuerySnapshot TakeSnapshot() =>
        TeamQuerySnapshotFactory.Take(Team, FormationsIncludingEmpty, CalculateNotEngagingTacticalAdvantage(Team.QuerySystem));

    private void Fail(Exception ex)
    {
        _failed = true;
        _status = "failed: " + ex.GetType().Name + ": " + ex.Message;
    }
}
