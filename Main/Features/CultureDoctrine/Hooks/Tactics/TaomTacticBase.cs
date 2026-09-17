using System;
using System.Linq;
using TAOM.Features.CultureDoctrine.Doctrines;
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
/// 210 lines against ADR-002's 150: this is the one place the vanilla tactic lifecycle exists in
/// TAOM (vanilla's are 180 to 370 lines each) and the four tactics on top are 15 to 40 lines.
/// The formation-slot code writes <c>TacticComponent</c>'s protected fields and cannot leave the
/// class; the decisions it makes are in <see cref="TacticPhaseMachine"/>, <see cref="DoctrineWeights"/>
/// and <see cref="BehaviorWeightApplier"/>.
/// </para>
/// </summary>
public abstract class TaomTacticBase : TacticComponent
{
    private readonly DoctrinePlan _plan;
    private readonly float _multiplier;
    private readonly TacticPhaseMachine _machine = new TacticPhaseMachine();
    private Formation? _cavalry;
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

    internal Formation? MainInfantry => _mainInfantry;
    internal Formation? Archers => _archers;

    /// <summary>Set in <see cref="BeforeApply"/> by a tactic that uses <c>BehaviorDefend</c>.</summary>
    internal WorldPosition DefensePosition { get; set; } = WorldPosition.Invalid;

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
            if (!current.HasValue || !OnPhaseTick(current.Value))
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
        _status = phase.Value + StatusSuffix;
    }

    protected override void ManageFormationCounts()
    {
        if (_plan.Split == FormationSplit.OneOneTwoOne)
        {
            AssignTacticFormations1121();
            _cavalry = null;
            return;
        }
        // TacticFrontalCavalryCharge.ManageFormationCounts, verbatim: one block per class, the
        // main infantry flagged, no Side written (the 1/1/2/1 split is the one that sets sides).
        ManageFormationCounts(1, 1, 1, 1);
        _mainInfantry = Pick(f => f.QuerySystem.IsInfantryFormation);
        if (_mainInfantry != null)
            _mainInfantry.AI.IsMainFormation = true;
        _archers = Pick(f => f.QuerySystem.IsRangedFormation);
        _cavalry = Pick(f => f.QuerySystem.IsCavalryFormation);
        _rangedCavalry = Pick(f => f.QuerySystem.IsRangedCavalryFormation);
        _leftCavalry = null;
        _rightCavalry = null;
    }

    private Formation? Pick(Func<Formation, bool> isClass) =>
        ChooseAndSortByPriority(FormationsIncludingEmpty, f => f.CountOfUnits > 0 && isClass(f), f => f.IsAIControlled, f => f.QuerySystem.FormationPower).FirstOrDefault();

    // Vanilla's test, extended with the single cavalry slot.
    protected override bool CheckAndSetAvailableFormationsChanged()
    {
        var count = Team.GetAIControlledFormationCount();
        if (count != _AIControlledFormationCount)
        {
            _AIControlledFormationCount = count;
            IsTacticReapplyNeeded = true;
            return true;
        }
        return !Holds(_mainInfantry, q => q.IsInfantryFormation) || !Holds(_archers, q => q.IsRangedFormation)
            || !Holds(_leftCavalry, q => q.IsCavalryFormation) || !Holds(_rightCavalry, q => q.IsCavalryFormation)
            || !Holds(_cavalry, q => q.IsCavalryFormation) || !Holds(_rangedCavalry, q => q.IsRangedCavalryFormation);
    }

    private static bool Holds(Formation? formation, Func<FormationQuerySystem, bool> isClass) =>
        formation == null || (formation.CountOfUnits != 0 && isClass(formation.QuerySystem));

    // Vanilla's test on the leading formation, with the plan's threshold and its doubling once joined.
    private bool HasBattleBeenJoined()
    {
        var lead = _plan.Split == FormationSplit.OneOneOneOne ? _cavalry : _mainInfantry;
        var enemy = lead?.CachedClosestEnemyFormation;
        if (enemy == null || lead!.AI.ActiveBehavior is BehaviorCharge || lead.AI.ActiveBehavior is BehaviorTacticalCharge)
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
    }

    private Formation? FormationFor(FormationRole role)
    {
        switch (role)
        {
            case FormationRole.MainInfantry: return _mainInfantry;
            case FormationRole.Archers: return _archers;
            case FormationRole.LeftCavalry: return _leftCavalry;
            case FormationRole.RightCavalry: return _rightCavalry;
            case FormationRole.Cavalry: return _cavalry;
            case FormationRole.RangedCavalry: return _rangedCavalry;
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
