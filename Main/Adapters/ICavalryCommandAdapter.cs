using TaleWorlds.Library;

namespace TAOM.Adapters;

/// <summary>
/// Write surface that the SmartCavalryAI service uses to drive the cavalry formation it
/// owns: issuing movement orders and applying line-charge positioning. All write paths
/// raise the <c>SmartCavalryRecursionGuard</c> internally so the Patch31 postfixes know
/// the order originated from us and bail out instead of re-entering the state machine.
///
/// <para>Target formations are referenced by opaque <c>object</c> tokens (concretely a
/// <c>TaleWorlds.MountAndBlade.Formation</c> reference), but the service must not depend
/// on that. The adapter is the only layer that casts the token back to a Formation.</para>
/// </summary>
public interface ICavalryCommandAdapter
{
    Vec2 CurrentPosition { get; }
    Vec2 Direction { get; }

    /// <summary>Stand your ground. Engine semantics: <c>MovementStateEnum.StandGround</c>, under
    /// which every rider holds its OWN current position and arrangement slots are ignored, so
    /// this never shapes a formation. Kept on the surface for callers that want a true halt.</summary>
    void IssueStop();

    /// <summary>Move to a point. Engine semantics: <c>MovementStateEnum.Hold</c>, riders take
    /// their arrangement slots around the point, which is what forms a line. Returns false when
    /// no scene is live or the world position is invalid, in which case no order was issued.</summary>
    bool IssueMoveTo(Vec2 worldXY, float groundHeight);

    /// <summary>Charge the named formation. Issues <c>MovementOrderChargeToTarget</c> AND then
    /// <c>Formation.SetTargetFormation(target)</c>: every <c>SetMovementOrder</c> ends by clearing
    /// the formation's native target (Formation.cs:714, v1.4.8), so without the second write the
    /// riders would charge with target index -1, which is a free charge at anyone.</summary>
    void IssueChargeToTarget(object targetToken);

    /// <summary>A plain vanilla Charge (<c>MovementOrder.MovementOrderCharge</c>): every rider
    /// picks its own target. The state machine hands a formation back to this whenever it gives
    /// up on a cycle, so no exit path ever leaves riders standing still.</summary>
    void IssueCharge();

    /// <summary>Lays out a line: <c>Formation.SetPositioning(position, direction, spacing)</c> plus
    /// <c>SetFacingOrder(FacingOrderLookAtDirection(direction))</c>. The facing order is required
    /// because <c>Formation.Tick</c> re-applies the retained <c>FacingOrder</c>'s direction every
    /// tick (Formation.cs:2311-2314); a direction written through positioning alone lasts one
    /// tick. Only takes effect under a Hold-type order.</summary>
    void ApplyChargeLine(Vec3 worldPosition, Vec2 direction, int unitSpacing);

    /// <summary>True iff the given target token still references a live formation
    /// (non-null Formation and CountOfUnits &gt; 0).</summary>
    bool IsTargetAlive(object targetToken);

    /// <summary>The target formation's CURRENT centroid. False (and <c>Vec2.Zero</c>) when the
    /// token is not a live formation. The machine reads this every tick instead of a snapshot
    /// taken at order time, because the enemy moves.</summary>
    bool TryGetTargetPosition(object targetToken, out Vec2 position);

    /// <summary>How far the target formation's riders extend PAST its centroid along
    /// <paramref name="direction"/>: the largest positive projection of any unit, 0 when the
    /// token is not a live formation. The reform point is placed beyond this, so a deep column
    /// or a line that turned sideways does not swallow it.</summary>
    float GetTargetDepthAlong(object targetToken, Vec2 direction);
}
