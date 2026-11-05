using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The per-soldier state of Marshal's Word: the archers' draw hold, the held line's slot clamp, and the attack flag and firing order that hold the line took.</summary>
internal sealed class MarshalWordComponent : AgentComponent
{
    private static readonly Random Jitter = new();
    private static readonly OutcomeCounter Holds = Outcomes.Counter(MarshalChecks.HoldCounterId);
    private static readonly OutcomeCounter Releases = Outcomes.Counter(MarshalChecks.ReleaseCounterId);
    private static readonly OutcomeCounter Clamps = Outcomes.Counter(MarshalChecks.ClampCounterId);
    private static volatile bool _faulted;

    private readonly double _jitter = Jitter.NextDouble() * MarshalTuning.WordJitter;
    private double _since;
    private bool _standAside;
    private double _nextAsideRead = double.NegativeInfinity;
    private double _releasedAt = double.NegativeInfinity;
    private int _releasedByWord;
    private int _stateCode;
    private bool _attackCleared;
    private bool _firingOwed;
    private double _nextHoldRead = double.NegativeInfinity;
    private double _woundedAt = double.NegativeInfinity;
    private bool _qualifies;
    private string _whyNot = "";
    private readonly SlotTrendCount _trend = new();
    private bool _slotValid;
    private bool _anchored;
    private float _slotX;
    private float _slotY;
    private float _slotDistance = float.NaN;

    public MarshalWordComponent(Agent agent)
        : base(agent)
    {
    }

    /// <summary>True once the callback threw: it writes nothing for the rest of the process, and a held line acts as off, so the mission tick gives back what the line took.</summary>
    internal static bool IsFaulted => _faulted;

    internal DrawState State => (DrawState)Volatile.Read(ref _stateCode);

    /// <summary>The soldier's latest stand-aside answer, read by the driver's census.</summary>
    internal bool StandingAside => _standAside;

    /// <summary>The mission time the current draw state began, read on the main thread on the unproved assumption that native calls the callback serially.</summary>
    internal double StateSince => _since;

    /// <summary>This component cleared the soldier's attack flag and has not yet put it back.</summary>
    internal bool OwesAttackFlag => _attackCleared;

    /// <summary>This component changed the soldier's firing order and has not yet given the company's own back.</summary>
    internal bool OwesFiringOrder => _firingOwed;

    /// <summary>The attack flag read off now, after this component cleared it.</summary>
    internal bool AttackFlagOff => _attackCleared && (Agent.GetAgentFlags() & AgentFlag.CanAttack) == 0;

    /// <summary>The attack flag reads on now, whoever cleared it or put it back: a pointer read, the soldier's own flag word.</summary>
    internal bool AttackFlagOn => (Agent.GetAgentFlags() & AgentFlag.CanAttack) != 0;

    /// <summary>The latest read found the soldier standing in the held line.</summary>
    internal bool HoldQualifies => _qualifies;

    /// <summary>The first clause that kept the soldier out of the held line at the latest read, or an empty string.</summary>
    internal string HoldWhyNot => _whyNot;

    /// <summary>A slot is stored: the order point of the soldier, or the anchor a Stop order took.</summary>
    internal bool SlotStored => _slotValid;

    /// <summary>The slot is an anchor the soldier's own place set under a Stop order.</summary>
    internal bool SlotAnchored => _anchored;

    /// <summary>Metres from the soldier to the stored slot at the latest read, or NaN with none.</summary>
    internal float SlotDistance => _slotDistance;

    /// <summary>The slot trend counts since the last call, which starts them again; read on the main thread, from the mission tick.</summary>
    internal SlotCounts TakeSlotTrend() => _trend.Take();

    /// <summary>The mission time of the latest real wound, or negative infinity.</summary>
    internal double WoundedAt => Volatile.Read(ref _woundedAt);

    /// <summary>Main thread, from the mission tick: reads the hold facts when due, then clears or gives back the attack flag and firing order this component took.</summary>
    internal void RefreshHold(bool holdOn, double now)
    {
        holdOn &= !_faulted;
        ReadHold(holdOn, now);
        var want = MarshalHoldLineRules.Want(holdOn, _qualifies, WoundedAt, now);
        ApplyHold(want == HoldWant.FlagOff);
        ApplyFiringOrder(want != HoldWant.Off);
    }

    /// <summary>Main thread: gives back whatever this component took from the soldier.</summary>
    internal void ReleaseHold()
    {
        ApplyHold(false);
        ApplyFiringOrder(false);
        _qualifies = false;
        ForgetSlot();
        _nextHoldRead = double.NegativeInfinity;
    }

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

    /// <summary>A real wound opens the strike back window. Any thread: one double.</summary>
    public override void OnHit(Agent affectorAgent, int damage, in MissionWeapon affectorWeapon, in Blow b, in AttackCollisionData collisionData)
    {
        if (MarshalHoldLineRules.IsWound(damage, b.IsMissile))
            Volatile.Write(ref _woundedAt, Mission.Current?.CurrentTime ?? double.NegativeInfinity);
    }

    /// <summary>Any thread, native free: expires the next hold read, and the main thread restores on its next pass (build-spec 9.2).</summary>
    public override void OnFormationSet() => Volatile.Write(ref _nextHoldRead, double.NegativeInfinity);

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
            ClampToSlot(MarshalFireTable.HoldOf(company), now, ref inputVector);
        }
        catch (Exception ex)
        {
            _faulted = true;
            Outcomes.Swallowed("MarshalsWord", "MarshalWordComponent.OnAIInputSet", ex);
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

    private void ClampToSlot(bool holdOn, double now, ref Vec2 inputVector)
    {
        ReadHold(holdOn, now);
        if (!holdOn || !_qualifies || !_slotValid || !MarshalHoldLineRules.Beyond(_slotDistance) || !MarshalHoldLineRules.Moves(inputVector.x, inputVector.y))
            return;
        var own = Agent.Position.AsVec2;
        var toSlotWorld = new Vec2(_slotX - own.x, _slotY - own.y);
        var toSlot = Agent.Frame.rotation.TransformToLocal(toSlotWorld);
        var steered = MarshalHoldLineRules.ClampInput(inputVector.x, inputVector.y, toSlot.x, toSlot.y);
        if (steered.X == inputVector.x && steered.Y == inputVector.y)
            return;
        inputVector = new Vec2(steered.X, steered.Y);
        _trend.Steer(toSlotWorld.Length);
        Clamps.Pass();
    }

    private void ReadHold(bool holdOn, double now)
    {
        if (!holdOn)
        {
            _qualifies = false;
            ForgetSlot();
            return;
        }

        if (now < Volatile.Read(ref _nextHoldRead))
            return;
        Volatile.Write(ref _nextHoldRead, now + MarshalTuning.HoldReadSeconds);
        var formation = Agent.Formation;
        if (formation == null)
        {
            _qualifies = false;
            _whyNot = "no-formation";
            ForgetSlot();
            return;
        }

        var order = formation.GetReadonlyMovementOrderReference().OrderEnum;
        var weapon = Agent.WieldedWeapon;
        var arrangement = formation.ArrangementOrder.OrderEnum;
        var facts = new HoldFacts(
            arrangement == ArrangementOrder.ArrangementOrderEnum.ShieldWall || arrangement == ArrangementOrder.ArrangementOrderEnum.Circle
            || arrangement == ArrangementOrder.ArrangementOrderEnum.Square,
            order != MovementOrder.MovementOrderEnum.Charge && order != MovementOrder.MovementOrderEnum.ChargeToTarget && order != MovementOrder.MovementOrderEnum.Retreat,
            !formation.IsAIControlled,
            Agent.HasShieldCached,
            Agent.HasMount,
            !weapon.IsEmpty && weapon.CurrentUsageItem.IsRangedWeapon,
            Agent.IsDetachedFromFormation,
            Agent.IsUsingGameObject,
            Agent == Mission.Current?.MainAgent,
            Agent.IsRunningAway,
            Agent.HumanAIComponent?.IsInterestedInAnyGameObject() ?? false);
        _qualifies = MarshalHoldLineRules.Qualifies(facts);
        _whyNot = MarshalHoldLineRules.WhyNot(facts);
        ReadSlot(formation, order == MovementOrder.MovementOrderEnum.Stop, now);
    }

    // Under Stop the engine answers the soldier's own place (Formation.cs, StandGround), so the first place is kept as an anchor and the member is not asked at all.
    private void ReadSlot(Formation formation, bool standGround, double now)
    {
        var own = Agent.Position.AsVec2;
        var orderValid = false;
        var order = Vec2.Zero;
        if (MarshalHoldLineRules.NeedsEnginePlace(standGround, _qualifies))
        {
            var place = formation.GetOrderPositionOfUnit(Agent);
            orderValid = place.IsValid;
            order = orderValid ? place.AsVec2 : Vec2.Zero;
        }

        var slot = MarshalHoldLineRules.ReadSlot(standGround, _anchored, _slotX, _slotY, orderValid, order.x, order.y, own.x, own.y);
        _slotValid = slot.Valid;
        _anchored = slot.Anchored;
        _slotDistance = float.NaN;
        if (slot.Valid)
        {
            _slotX = slot.X;
            _slotY = slot.Y;
            var dx = own.x - slot.X;
            var dy = own.y - slot.Y;
            _slotDistance = (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
        }

        _trend.Read(_slotDistance, MarshalHoldLineRules.StrikingBack(WoundedAt, now), _qualifies);
    }

    private void ForgetSlot()
    {
        _slotValid = false;
        _anchored = false;
        _slotDistance = float.NaN;
        _trend.Forget();
    }

    /// <summary>Clears the soldier's attack flag or puts it back, keeps the debt until the write was made, and drops it when the engine's climbing clear took the flag too.</summary>
    private void ApplyHold(bool clear)
    {
        var flags = Agent.GetAgentFlags();
        var engineCleared = Agent.IsDetachedFromFormation && (flags & AgentFlag.CanDefend) == 0;
        switch (MarshalHoldRules.FlagAction(clear, (flags & AgentFlag.CanAttack) != 0, _attackCleared, engineCleared))
        {
            case AttackFlagAction.Clear:
                Agent.SetAgentFlags(flags & ~AgentFlag.CanAttack);
                _attackCleared = true;
                break;
            case AttackFlagAction.Restore:
                Agent.SetAgentFlags(flags | AgentFlag.CanAttack);
                _attackCleared = false;
                break;
            case AttackFlagAction.Forget:
                _attackCleared = false;
                break;
        }
    }

    // The company's own order is given back from the formation, because AddUnit, RemoveUnit and a change of the company's order overwrite the soldier's (Formation.cs).
    private void ApplyFiringOrder(bool lineStands)
    {
        if (!lineStands && !_firingOwed)
            return;
        var formation = Agent.Formation;
        var current = Agent.GetFiringOrder();
        var hold = FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire;
        var company = formation == null ? current : (int)formation.FiringOrder.OrderEnum;
        switch (MarshalHoldLineRules.FiringAction(lineStands, current, (int)hold, company, _firingOwed))
        {
            case FiringAct.Hold:
                Agent.SetFiringOrder(hold);
                _firingOwed = true;
                break;
            case FiringAct.Restore:
                Agent.SetFiringOrder(formation!.FiringOrder.OrderEnum);
                _firingOwed = false;
                break;
            case FiringAct.Forget:
                _firingOwed = false;
                break;
        }
    }

    private void Publish(DrawState state) => Volatile.Write(ref _stateCode, (int)state);

    private bool ReadStandsAside(Formation? formation, int company)
    {
        if (MarshalFireTable.ModeOf(company) == FireMode.Free)
            return true;
        var weapon = Agent.WieldedWeapon;
        var target = Agent.GetTargetAgent();
        var facts = new StandAsideFacts(
            formation == null,
            weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon,
            formation != null && formation.FiringOrder.OrderEnum == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire,
            Agent.IsDetachedFromFormation,
            Agent.IsUsingGameObject,
            target == null ? double.PositiveInfinity : Agent.Position.Distance(target.Position));
        return MarshalFireRules.StandsAside(facts);
    }
}
