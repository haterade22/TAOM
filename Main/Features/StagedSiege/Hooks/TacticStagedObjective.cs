using System;
using TAOM.Features.StagedSiege.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>
/// The team tactic of a staged siege (docs/features/staged-siege.md): while a stage has a job for the team, every AI-controlled
/// formation with a <see cref="BehaviorDefend"/> is sent to the stage's point (attackers take it, defenders hold it), and in
/// stage B the weight is zero so vanilla's own siege tactics run the gatehouse fight. It outweighs vanilla's siege tactics
/// (about 10) with <see cref="StagePlan.ActiveWeight"/>.
///
/// Both overrides run on the team-AI tick, the async AI thread in normal play and the main thread when the service resets the
/// tactic. The only state is what the engine hands over plus the immutable snapshot the service publishes: no container lookup,
/// no log, no switch read. Every override body is wrapped; a throw sets the failed latch, after which the weight is zero and the
/// tick does nothing, and the base tick still runs because it clears the team AI's first-tactic flag.
/// </summary>
public sealed class TacticStagedObjective : TacticComponent, IStagedTacticProbe
{
    private volatile bool _failed;
    private volatile bool _isCurrent;
    private volatile string _status = "idle";
    private StageId? _appliedStage;
    private int _appliedMask;

    public TacticStagedObjective(Team team)
        : base(team)
    {
    }

    public StagedSide Side => Team.Side == BattleSideEnum.Attacker ? StagedSide.Attacker : StagedSide.Defender;

    public bool IsCurrent => _isCurrent;

    public string Status => _status;

    protected override float GetTacticWeight()
    {
        if (_failed)
            return 0f;
        try
        {
            var snapshot = StagedSiegeSlot.SnapshotFor(Team.Mission);
            return snapshot == null || snapshot.Finished ? 0f : StagePlan.TacticWeight(snapshot.Stage, Side);
        }
        catch (Exception ex)
        {
            Fail(ex);
            return 0f;
        }
    }

    protected override void OnApply()
    {
        _isCurrent = true;
        _appliedStage = null;
        base.OnApply();
    }

    protected override void OnCancel()
    {
        _isCurrent = false;
        _appliedStage = null;
        base.OnCancel();
    }

    public override void TickOccasionally()
    {
        if (!_failed)
        {
            try
            {
                Steer();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        // Mandatory: clears the team AI's first-tactic flag and stops machines at mission end.
        base.TickOccasionally();
    }

    private void Steer()
    {
        var snapshot = StagedSiegeSlot.SnapshotFor(Team.Mission);
        if (snapshot == null || snapshot.Finished)
            return;
        var stage = snapshot.Stage;
        var mode = StagePlan.ModeFor(stage, Side);
        if (mode == TacticMode.Vanilla)
        {
            _status = "vanilla in stage " + stage;
            return;
        }

        // A player general keeps his own engine orders; only an AI-led team waits for the point.
        if (StagePlan.StopsMachines(stage, Side) && !(Team.IsPlayerTeam && Team.IsPlayerGeneral))
            StopUsingAllMachines();

        // A mask of which formations are eligible, not a count: one emptying while another fills must still reapply.
        var eligible = 0;
        var mask = 0;
        var formations = FormationsIncludingEmpty;
        for (var i = 0; i < formations.Count; i++)
        {
            if (!IsEligible(formations[i]))
                continue;
            eligible++;
            mask |= StagePlan.FormationBit((int)formations[i].FormationIndex);
        }

        if (_appliedStage == stage && _appliedMask == mask && !IsTacticReapplyNeeded)
            return;

        for (var i = 0; i < formations.Count; i++)
        {
            var formation = formations[i];
            if (!IsEligible(formation))
                continue;
            formation.AI.ResetBehaviorWeights();
            formation.AI.SetBehaviorWeight<BehaviorDefend>(1f).DefensePosition = Position(TargetFor(formation, mode, snapshot));
        }

        _appliedStage = stage;
        _appliedMask = mask;
        IsTacticReapplyNeeded = false;
        _status = mode + " " + stage + " on " + eligible + " formation(s)";
    }

    private static bool IsEligible(Formation formation) =>
        formation.CountOfUnits > 0 && formation.IsAIControlled && formation.AI.GetBehavior<BehaviorDefend>() != null;

    private static StagePoint TargetFor(Formation formation, TacticMode mode, StageSnapshot snapshot)
    {
        if (!formation.QuerySystem.IsRangedFormation)
            return snapshot.Zone.Center;
        if (mode == TacticMode.TakePoint)
            return snapshot.AttackerSupport ?? snapshot.Zone.Center;
        return snapshot.DefenderHold?.Position ?? snapshot.Zone.Center;
    }

    private WorldPosition Position(StagePoint p) =>
        new WorldPosition(Team.Mission.Scene, UIntPtr.Zero, new Vec3(p.X, p.Y, p.Z), false);

    private void Fail(Exception ex)
    {
        _failed = true;
        _status = "failed: " + ex.GetType().Name + ": " + ex.Message;
    }
}
