using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.CreatureSiegeRole.Domain;

// The role decision, the slots and anchors it sends a creature to, and the reapply rule.
public static partial class CreatureSiegeRules
{
    // --- the role ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// What a creature should be doing. The order of the rules is the order of the decision:
    /// <list type="number">
    /// <item>Released, whatever else holds: not AI-controlled, fleeing, a Retreat order, no side.</item>
    /// <item>A defender holds the gate under AI command and obeys the player's order otherwise (D8).</item>
    /// <item>An attacker the player commands goes to the gate only on a Charge or ChargeToTarget given before both gates are
    /// passable; any other order, or any order after the breach, is obeyed (D7).</item>
    /// <item>An attacker not yet through a shut outer gate stands off while a ram works and strikes the gate otherwise; one
    /// through it, or with the outer gate open, strikes a shut inner gate; with every gate open it holds the courtyard.</item>
    /// </list>
    /// </summary>
    public static RoleDecision DecideRole(in RoleInputs inputs)
    {
        if (!inputs.IsAIControlled) return Release(RoleReason.NotAIControlled);
        if (inputs.IsFleeing) return Release(RoleReason.Fleeing);
        if (inputs.Formation.Order == SiegeFormationOrder.Retreat) return Release(RoleReason.RetreatOrder);

        var playerCommanded = inputs.Formation.IsPlayerCommanded;

        switch (inputs.Side)
        {
            case SiegeSide.Defender:
                return playerCommanded
                    ? Release(RoleReason.PlayerDefender)
                    : new RoleDecision(SiegeRole.HoldGate, RoleReason.DefendingGate);

            case SiegeSide.Attacker:
                break;

            default:
                return Release(RoleReason.NoSide);
        }

        var bothPassable = inputs.OuterPassable && (!inputs.InnerPresent || inputs.InnerPassable);
        if (playerCommanded)
        {
            if (bothPassable) return Release(RoleReason.PlayerOrderAfterBreach);

            var order = inputs.Formation.Order;
            if (order != SiegeFormationOrder.Charge && order != SiegeFormationOrder.ChargeToTarget)
                return Release(RoleReason.PlayerOrder);
        }

        if (!inputs.OuterPassable && !inputs.PastOuterGate)
        {
            return inputs.RamWorking
                ? new RoleDecision(SiegeRole.StandOff, RoleReason.RamWorking)
                : new RoleDecision(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed);
        }

        if (inputs.InnerPresent && !inputs.InnerPassable)
            return new RoleDecision(SiegeRole.StrikeInner, RoleReason.InnerGateClosed);

        return new RoleDecision(SiegeRole.HoldCourtyard, RoleReason.BothGatesOpen);
    }

    private static RoleDecision Release(RoleReason reason) => new(SiegeRole.Release, reason);

    // --- slots ------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Which side of the gate a creature stands on: +1 the attacker side (the gate frame's forward axis points at the
    /// attackers), -1 the inner side. Vanilla's own rule when it picks a gate wait point (<c>MovementOrder</c>). A creature
    /// on the gate's line, a position that is not finite and a gate with no usable axis all read as the attacker side.
    /// </summary>
    public static int SideOfGate(float agentX, float agentY, float gateX, float gateY, float forwardX, float forwardY)
    {
        if (!TryUnit(forwardX, forwardY, out var unitX, out var unitY)) return 1;

        var along = (agentX - gateX) * unitX + (agentY - gateY) * unitY;
        return along < 0f ? -1 : 1;
    }

    /// <summary>
    /// The <paramref name="index"/>th slot for a creature striking a gate, on <paramref name="side"/> of it (see
    /// <see cref="SideOfGate"/>): the first slot is <see cref="StrikeStandOff"/> out, the next two fill the row left and
    /// right <see cref="StrikeLateral"/> apart, and each further row sits <see cref="SlotRowDepth"/> further out. The slot
    /// faces the gate. <see cref="SiegeSlot.None"/> for a negative index or anything not finite.
    /// </summary>
    public static SiegeSlot StrikeSlot(float gateX, float gateY, float forwardX, float forwardY, int side, int index) =>
        GateSlot(gateX, gateY, forwardX, forwardY, side, index, StrikeStandOff, StrikeLateral);

    /// <summary>
    /// The <paramref name="index"/>th slot for a creature waiting off a gate while a ram works: <see cref="StandOffDistance"/>
    /// out, rows <see cref="SlotRowDepth"/> further out. The gate's axis is the ram's lane, so no column sits on it: the row's
    /// columns are <see cref="StandOffLateral"/> to one side, the same to the other, then twice that to the first side.
    /// </summary>
    public static SiegeSlot StandOffSlot(float gateX, float gateY, float forwardX, float forwardY, int side, int index) =>
        GateSlot(gateX, gateY, forwardX, forwardY, side, index, StandOffDistance, StandOffLateral, clearLane: true);

    /// <summary>
    /// The <paramref name="index"/>th slot of a hold around an anchor: the first is the anchor itself, the next two sit
    /// <see cref="HoldLateral"/> to each side across the gate's axis, and each further row sits <see cref="SlotRowDepth"/>
    /// further inside (away from the attackers). <paramref name="facingSign"/> +1 faces the attackers' side, -1 the inside.
    /// </summary>
    public static SiegeSlot HoldSlot(float anchorX, float anchorY, float forwardX, float forwardY, int facingSign, int index)
    {
        if (index < 0 || !TryUnit(forwardX, forwardY, out var unitX, out var unitY)) return SiegeSlot.None;

        var lateral = LateralOf(index % SlotColumns, HoldLateral);
        var inward = (index / SlotColumns) * SlotRowDepth;
        var sign = facingSign < 0 ? -1 : 1;

        // The gate's side axis is (unitY, -unitX); inward is the opposite of the forward axis.
        return Finished(anchorX + unitY * lateral - unitX * inward, anchorY - unitX * lateral - unitY * inward,
            unitX * sign, unitY * sign);
    }

    private static SiegeSlot GateSlot(float gateX, float gateY, float forwardX, float forwardY, int side, int index,
        float firstDistance, float lateralStep, bool clearLane = false)
    {
        if (index < 0 || !TryUnit(forwardX, forwardY, out var unitX, out var unitY)) return SiegeSlot.None;

        var column = index % SlotColumns;
        var lateral = clearLane ? LaneClearLateralOf(column, lateralStep) : LateralOf(column, lateralStep);
        var distance = firstDistance + (index / SlotColumns) * SlotRowDepth;
        var sign = side < 0 ? -1 : 1;

        return Finished(gateX + unitX * sign * distance + unitY * lateral, gateY + unitY * sign * distance - unitX * lateral,
            -unitX * sign, -unitY * sign);
    }

    // Column 0 is the centre, column 1 one step to the gate's side axis, column 2 one step the other way.
    private static float LateralOf(int column, float step) => column == 0 ? 0f : column == 1 ? step : -step;

    // No column on the axis: one step to the gate's side axis, one step the other way, then a second step out on the first side.
    private static float LaneClearLateralOf(int column, float step) => column == 0 ? step : column == 1 ? -step : 2f * step;

    private static SiegeSlot Finished(float x, float y, float faceX, float faceY) =>
        FiniteFloatValidator.IsFinite(x) && FiniteFloatValidator.IsFinite(y)
            ? new SiegeSlot(x, y, faceX, faceY, true)
            : SiegeSlot.None;

    // --- anchors ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the ground at an anchor is within <see cref="AnchorHeightTolerance"/> of its gate's base: it is ground level
    /// and not a wall walk or a gatehouse roof. A NaN or infinite height is not.
    /// </summary>
    public static bool IsAnchorHeightValid(float groundZ, float gateBaseZ) =>
        System.Math.Abs(groundZ - gateBaseZ) <= AnchorHeightTolerance;

    /// <summary>
    /// The computed anchor behind the outer gate: <see cref="InsideAnchorDistance"/> along the inner side of its axis. NaN
    /// when the gate's axis is not usable. The adapter finds the ground height at this point.
    /// </summary>
    public static (float X, float Y) InsideOuterGatePoint(SiegeGateReading outer)
    {
        if (!TryUnit(outer.ForwardX, outer.ForwardY, out var unitX, out var unitY)) return (float.NaN, float.NaN);

        return (outer.OriginX - unitX * InsideAnchorDistance, outer.OriginY - unitY * InsideAnchorDistance);
    }

    /// <summary>
    /// Where a defender may hold, best first: the outer gate's middle position, the inner gate's, then a point inside the
    /// outer gate. A gate without a middle position, or whose middle position lies more than <see cref="MaxMiddleAnchorDistance"/>
    /// from the gate itself (TAOM scenes park it on another part of the map), offers none. Each is marked by whether its ground is at its own gate's
    /// base height; whether a creature can walk there is asked per creature.
    /// </summary>
    public static IReadOnlyList<AnchorCandidate> HoldGateAnchors(SiegeGateReading outer, SiegeGateReading? inner, float insideGroundZ) =>
        Anchors(outer, inner, insideGroundZ, outerFirst: true);

    /// <summary>Where an attacker may hold once the gates are open, best first: the inner gate's middle, the outer's, then inside the outer gate.</summary>
    public static IReadOnlyList<AnchorCandidate> CourtyardAnchors(SiegeGateReading outer, SiegeGateReading? inner, float insideGroundZ) =>
        Anchors(outer, inner, insideGroundZ, outerFirst: false);

    private static IReadOnlyList<AnchorCandidate> Anchors(SiegeGateReading outer, SiegeGateReading? inner, float insideGroundZ,
        bool outerFirst)
    {
        var anchors = new List<AnchorCandidate>(3);
        var outerMiddle = MiddleAnchor(outer, AnchorSource.OuterMiddle);
        var innerMiddle = inner == null ? null : MiddleAnchor(inner, AnchorSource.InnerMiddle);

        if (outerFirst)
        {
            if (outerMiddle != null) anchors.Add(outerMiddle.Value);
            if (innerMiddle != null) anchors.Add(innerMiddle.Value);
        }
        else
        {
            if (innerMiddle != null) anchors.Add(innerMiddle.Value);
            if (outerMiddle != null) anchors.Add(outerMiddle.Value);
        }

        var (insideX, insideY) = InsideOuterGatePoint(outer);
        anchors.Add(Candidate(AnchorSource.InsideOuterGate, insideX, insideY, insideGroundZ, outer.OriginZ));
        return anchors;
    }

    private static AnchorCandidate? MiddleAnchor(SiegeGateReading gate, AnchorSource source) =>
        gate.HasMiddle && IsNearItsGate(gate) ? Candidate(source, gate.MiddleX, gate.MiddleY, gate.MiddleZ, gate.OriginZ) : null;

    // A positive requirement, so a NaN or infinite coordinate fails it and the anchor is dropped.
    private static bool IsNearItsGate(SiegeGateReading gate)
    {
        var dx = gate.MiddleX - gate.OriginX;
        var dy = gate.MiddleY - gate.OriginY;
        return dx * dx + dy * dy <= MaxMiddleAnchorDistance * MaxMiddleAnchorDistance;
    }

    private static AnchorCandidate Candidate(AnchorSource source, float x, float y, float z, float gateBaseZ) =>
        new(source, new SiegePoint(x, y, z),
            FiniteFloatValidator.IsFinite(x) && FiniteFloatValidator.IsFinite(y) && IsAnchorHeightValid(z, gateBaseZ));

    // --- reapply ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether a creature that already holds <paramref name="role"/> needs its scripted state set again because the engine
    /// cleared a flag: vanilla does it on a formation transfer, an attack-entity disband, a ladder-queue drop and a task force
    /// disband. A striking creature needs its scripted position and its attack-entity target; a holding one its position; a
    /// released one nothing.
    /// </summary>
    public static bool NeedsReapply(SiegeRole role, bool hasScriptedPosition, bool isAttackingEntity) => role switch
    {
        SiegeRole.Release => false,
        SiegeRole.StrikeOuter or SiegeRole.StrikeInner => !(hasScriptedPosition && isAttackingEntity),
        _ => !hasScriptedPosition,
    };
}
