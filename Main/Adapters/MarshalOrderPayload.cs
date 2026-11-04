using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Adapters;

/// <summary>
/// One captured player order (build-spec 5.2): which of the six <c>OrderController</c> methods it came through, with its arguments, so a
/// step can issue it again on the master controller.
/// </summary>
internal sealed class MarshalOrderPayload
{
    private readonly MarshalCall _call;
    private readonly WorldPosition _first;
    private readonly WorldPosition _second;
    private Vec3? _point;

    private MarshalOrderPayload(MarshalCall call, OrderType type, StepEnd? end, WorldPosition first = default, WorldPosition second = default,
        Formation? target = null, IOrderable? orderable = null, bool keepWidths = false)
    {
        _call = call;
        KeepWidths = keepWidths;
        Type = type;
        End = end;
        _first = first;
        _second = second;
        Target = target;
        Orderable = orderable;
    }

    internal OrderType Type { get; }

    /// <summary>Null when the order is not on the allow list.</summary>
    internal StepEnd? End { get; }

    internal Formation? Target { get; }

    internal IOrderable? Orderable { get; }

    /// <summary>
    /// The widths key (542) as it stood when a drag was given, for a line order only. A queued drag is issued later, when the key may be in
    /// another state, and it must lay out as the player saw it in the preview (build-spec 6.2).
    /// </summary>
    internal bool KeepWidths { get; }

    internal static MarshalOrderPayload ForOrder(OrderType type) => new(MarshalCall.Order, type, EndFor(MarshalCall.Order, type));

    /// <summary>An order that carries an agent (FollowMe): never queued, so the payload only lets the capture count a decline.</summary>
    internal static MarshalOrderPayload ForAgent(OrderType type) => new(MarshalCall.Agent, type, null);

    internal static MarshalOrderPayload ForPosition(OrderType type, WorldPosition position) =>
        new(MarshalCall.Position, type, EndFor(MarshalCall.Position, type), position);

    internal static MarshalOrderPayload ForFormation(OrderType type, Formation target) =>
        new(MarshalCall.Formation, type, EndFor(MarshalCall.Formation, type), target: target);

    internal static MarshalOrderPayload ForTwoPositions(OrderType type, WorldPosition first, WorldPosition second) =>
        new(MarshalCall.TwoPositions, type, EndFor(MarshalCall.TwoPositions, type), first, second, keepWidths: MarshalHooks.KeepWidthsHeld);

    /// <summary>The order type is what the orderable gives the player's side now, as <c>SetOrderWithOrderableObject</c> asks it (OrderController.cs:1010).</summary>
    internal static MarshalOrderPayload ForOrderable(IOrderable orderable, BattleSideEnum side)
    {
        var type = orderable.GetOrder(side);
        return new MarshalOrderPayload(MarshalCall.Orderable, type, EndFor(MarshalCall.Orderable, type), orderable: orderable);
    }

    /// <summary>The allow list of build-spec 5.2: how an order ends, or null for an order that is not queued.</summary>
    internal static StepEnd? EndFor(MarshalCall call, OrderType type)
    {
        switch (call)
        {
            case MarshalCall.Order:
                return IsInstantOrder(type) ? StepEnd.AtOnce : null;
            case MarshalCall.Position:
                return type == OrderType.Move ? StepEnd.Arrival : type == OrderType.LookAtDirection ? StepEnd.AtOnce : null;
            case MarshalCall.Formation:
                return type == OrderType.Charge || type == OrderType.Advance ? StepEnd.TargetGone : null;
            case MarshalCall.TwoPositions:
                return type == OrderType.MoveToLineSegment || type == OrderType.MoveToLineSegmentWithHorizontalLayout ? StepEnd.Arrival : null;
            case MarshalCall.Orderable:
                if (type == OrderType.AttackEntity)
                    return StepEnd.EntityOrderEnded;
                return type == OrderType.Move || type == OrderType.MoveToLineSegment || type == OrderType.MoveToLineSegmentWithHorizontalLayout
                    || type == OrderType.PointDefence ? StepEnd.Arrival : null;
            default:
                return null;
        }
    }

    private static bool IsInstantOrder(OrderType type)
    {
        switch (type)
        {
            case OrderType.Charge:
            case OrderType.StandYourGround:
            case OrderType.Advance:
            case OrderType.FallBack:
            case OrderType.Retreat:
            case OrderType.AdvanceTenPaces:
            case OrderType.FallBackTenPaces:
            case OrderType.LookAtEnemy:
            case OrderType.HoldFire:
            case OrderType.FireAtWill:
            case OrderType.Mount:
            case OrderType.Dismount:
            case OrderType.FormDeep:
            case OrderType.FormWide:
            case OrderType.FormWider:
                return true;
            default:
                return IsArrangement(type);
        }
    }

    private static bool IsArrangement(OrderType type) => type >= OrderType.ArrangementLine && type <= OrderType.ArrangementScatter;

    /// <summary>
    /// Whether a direct player order contradicts a walk and so clears the ordered companies' queues (5.3, rule 6). Firing, riding,
    /// arrangement and form orders do not.
    /// </summary>
    internal static bool ClearsQueue(OrderType type)
    {
        switch (type)
        {
            case OrderType.HoldFire:
            case OrderType.FireAtWill:
            case OrderType.RideFree:
            case OrderType.Mount:
            case OrderType.Dismount:
            case OrderType.FormCustom:
            case OrderType.FormDeep:
            case OrderType.FormWide:
            case OrderType.FormWider:
                return false;
            default:
                return !IsArrangement(type);
        }
    }

    /// <summary>Issues the order on a controller whose selection is already set. Every call is one of the six methods the capture patches.</summary>
    internal void Apply(OrderController controller)
    {
        switch (_call)
        {
            case MarshalCall.Order:
                controller.SetOrder(Type);
                break;
            case MarshalCall.Position:
                controller.SetOrderWithPosition(Type, _first);
                break;
            case MarshalCall.Formation:
                controller.SetOrderWithFormation(Type, Target!);
                break;
            case MarshalCall.TwoPositions:
                controller.SetOrderWithTwoPositions(Type, _first, _second);
                break;
            case MarshalCall.Orderable:
                controller.SetOrderWithOrderableObject(Orderable!);
                break;
            default:
                throw new InvalidOperationException("a " + _call + " order is never queued");
        }
    }

    /// <summary>
    /// The point of the order for its flag, read once and kept for a Move or a line, and read each time for an orderable, because an entity can move.
    /// </summary>
    internal bool TryPoint(out float x, out float y, out float z)
    {
        x = y = z = 0f;
        Vec3 point;
        switch (_call)
        {
            case MarshalCall.Position when Type == OrderType.Move:
            case MarshalCall.TwoPositions:
                point = _point ??= ReadPoint();
                break;
            case MarshalCall.Orderable when Orderable is MissionObject missionObject:
                point = missionObject.GameEntity.GlobalPosition;
                break;
            default:
                return false;
        }

        x = point.x;
        y = point.y;
        z = point.z;
        return true;
    }

    private Vec3 ReadPoint()
    {
        var a = _first.GetGroundVec3();
        if (_call == MarshalCall.Position)
            return a;
        var b = _second.GetGroundVec3();
        var x = (a.x + b.x) / 2f;
        var y = (a.y + b.y) / 2f;
        var z = FlagHeight((a.z + b.z) / 2f, Mission.Current?.Scene?.GetGroundHeightAtPosition(new Vec3(x, y, Math.Max(a.z, b.z), -1f), BodyFlags.CommonCollisionExcludeFlags));
        return new Vec3(x, y, z, -1f);
    }

    /// <summary>
    /// The height of the flag of a line (build-spec 5.5): the ground at its middle point, because the ground between two ends can lie above
    /// or below the straight line through them. The native probe starts 1.5 m above the asked height and casts down, with a terrain fallback,
    /// and answers 0 when nothing is there (as the Seat reads it, <c>SeatScreenAdapter</c>); with no answer, or no scene to ask, the flag
    /// stands at the average of the ends' ground heights.
    /// </summary>
    internal static float FlagHeight(float average, float? probed) => probed == null || probed.Value == 0f ? average : probed.Value;

    internal string Describe() => _call + ":" + Type;
}
