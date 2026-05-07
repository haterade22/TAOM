using TaleWorlds.Library;

namespace TAOM.Adapters;

/// <summary>
/// Write surface that the SmartCavalryAI service uses to drive the cavalry formation it
/// owns: issuing movement orders and applying line-charge positioning. All write paths
/// raise the <c>SmartCavalryRecursionGuard</c> internally so the Patch31 postfix knows
/// the order originated from us and bails out instead of re-entering the state machine.
///
/// <para>Target formations are referenced by opaque <c>object</c> tokens — concretely a
/// <c>TaleWorlds.MountAndBlade.Formation</c> reference, but the service must not depend
/// on that. The adapter is the only layer that casts the token back to a Formation.</para>
/// </summary>
public interface ICavalryCommandAdapter
{
    Vec2 CurrentPosition { get; }
    Vec2 Direction { get; }

    void IssueStop();
    void IssueMoveTo(Vec2 worldXY, float groundHeight);
    void IssueChargeToTarget(object targetToken);

    /// <summary>Invokes <c>Formation.SetPositioning</c> with the given world position,
    /// facing direction, and per-unit spacing — this is the v1.3.15 public method that
    /// produces a tight charge line.</summary>
    void ApplyChargeLine(Vec3 worldPosition, Vec2 direction, int unitSpacing);

    /// <summary>True iff the given target token still references a live formation
    /// (non-null Formation and CountOfUnits &gt; 0). Used by the cavalry state machine
    /// after rerouting to avoid charging a destroyed enemy formation.</summary>
    bool IsTargetAlive(object targetToken);
}
