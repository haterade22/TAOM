using System;
using System.Threading;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The archers' draw hold, reached through <c>Agent.OnAIInputSet</c> for an AI-controlled soldier whose callback flag the driver turned on.</summary>
internal sealed class MarshalFireComponent : AgentComponent
{
    private static readonly Random Jitter = new();
    private static readonly OutcomeCounter Holds = Outcomes.Counter(MarshalChecks.HoldCounterId);
    private static readonly OutcomeCounter Releases = Outcomes.Counter(MarshalChecks.ReleaseCounterId);
    private static volatile bool _faulted;

    private readonly double _jitter = Jitter.NextDouble() * MarshalTuning.WordJitter;
    private double _since;
    private bool _standAside;
    private double _nextAsideRead = double.NegativeInfinity;
    private double _releasedAt = double.NegativeInfinity;
    private int _releasedByWord;
    private int _stateCode;

    public MarshalFireComponent(Agent agent)
        : base(agent)
    {
    }

    /// <summary>True once the callback threw: it writes nothing for the rest of the process, and the AI's own flags stand.</summary>
    internal static bool IsFaulted => _faulted;

    internal DrawState State => (DrawState)Volatile.Read(ref _stateCode);

    /// <summary>The soldier's latest stand-aside answer, read by the driver's census.</summary>
    internal bool StandingAside => _standAside;

    /// <summary>Main thread only: the soldier is back in the idle state, because native stops calling a component whose flag is off.</summary>
    internal void Reset()
    {
        Publish(DrawState.Idle);
        _since = 0.0;
        _nextAsideRead = double.NegativeInfinity;
    }

    /// <summary>The counter a shot at this time belongs to (build-spec 7.3). Any thread: two reads.</summary>
    internal ShotKind Classify(double now)
    {
        var state = State;
        if (state == DrawState.Holding)
            return ShotKind.WhileHolding;
        if (state != DrawState.Idle || now - Volatile.Read(ref _releasedAt) > MarshalTuning.RecentReleaseSeconds)
            return ShotKind.Free;
        return Volatile.Read(ref _releasedByWord) != 0 ? ShotKind.AfterWord : ShotKind.AfterOther;
    }

    public override void OnAIInputSet(ref Agent.EventControlFlag eventFlag, ref Agent.MovementControlFlag movementFlag, ref Vec2 inputVector)
    {
        if (_faulted)
            return;
        try
        {
            var now = Mission.Current.CurrentTime;
            var formation = Agent.Formation;
            var company = formation?.Index ?? -1;
            if (now >= _nextAsideRead)
            {
                _standAside = ReadStandsAside(formation, company);
                _nextAsideRead = now + MarshalTuning.StandAsideReadSeconds;
            }

            var attacking = (movementFlag & Agent.MovementControlFlag.AttackMask) != 0;
            var cancelling = (movementFlag & Agent.MovementControlFlag.DefendMask) != 0;
            var wordDue = MarshalFireRules.WordDue(MarshalFireTable.WordTimeOf(company), _since, _jitter, now);
            var step = MarshalFireRules.Step(State, _since, new DrawInput(attacking, cancelling, _standAside, wordDue, now));
            _since = step.Since;
            Publish(step.State);
            Apply(step.Action, now, wordDue, ref movementFlag);
        }
        catch (Exception ex)
        {
            _faulted = true;
            Outcomes.Swallowed("MarshalsWord", "MarshalFireComponent.OnAIInputSet", ex);
        }
    }

    private void Apply(DrawAction action, double now, bool wordDue, ref Agent.MovementControlFlag movementFlag)
    {
        if (action == DrawAction.Hold)
        {
            movementFlag &= ~(Agent.MovementControlFlag.AttackMask | Agent.MovementControlFlag.DefendMask);
            movementFlag |= Agent.MovementControlFlag.AttackDown;
            Holds.Pass();
        }
        else if (action == DrawAction.Release)
        {
            movementFlag &= ~Agent.MovementControlFlag.AttackMask;
            Volatile.Write(ref _releasedByWord, wordDue ? 1 : 0);
            Volatile.Write(ref _releasedAt, now);
            Releases.Pass();
        }
    }

    private void Publish(DrawState state) => Volatile.Write(ref _stateCode, (int)state);

    // Native reads, so the answer is kept for StandAsideReadSeconds.
    private bool ReadStandsAside(Formation? formation, int company)
    {
        var weapon = Agent.WieldedWeapon;
        var target = Agent.GetTargetAgent();
        var facts = new StandAsideFacts(
            MarshalFireTable.ModeOf(company) == FireMode.Free,
            formation == null,
            weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon,
            formation != null && formation.FiringOrder.OrderEnum == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire,
            Agent.IsDetachedFromFormation,
            Agent.IsUsingGameObject,
            target == null ? double.PositiveInfinity : Agent.Position.Distance(target.Position));
        return MarshalFireRules.StandsAside(facts);
    }
}
