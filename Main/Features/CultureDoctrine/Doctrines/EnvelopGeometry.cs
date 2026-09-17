using TAOM.Core.Validation;
using TaleWorlds.Library;

namespace TAOM.Features.CultureDoctrine.Doctrines;

public enum WingSide
{
    Left,
    Right,
}

/// <summary>
/// Where an enveloping wing goes: a point beside the enemy body, on the wing's side of the axis
/// from our centre to the enemy, far enough out that the wing's own width clears the enemy's
/// (half of each plus a margin), level with the enemy's median. The wing marches there in a
/// line and charges once it is within <see cref="ContactDistance"/> of the point or the enemy
/// is within arm's reach. Pure: positions in, a point and a verdict out.
/// </summary>
public static class EnvelopGeometry
{
    public const float DefaultMargin = 6f;
    public const float ContactDistance = 25f;
    public const float ArmsReach = 15f;

    /// <summary>The wing's destination. Falls back to the enemy position when the centre stands
    /// on the enemy (no axis), so a wing never receives an invalid point.</summary>
    public static Vec2 WingPoint(Vec2 ourCentre, Vec2 enemy, float enemyWidth, float wingWidth, WingSide side, float margin)
    {
        if (!Finite(ourCentre) || !Finite(enemy))
            return enemy;
        var axis = enemy - ourCentre;
        if (!(axis.LengthSquared > 1e-4f))
            return enemy;
        axis.Normalize();
        // Right of the axis of advance: rotate the axis a quarter turn clockwise.
        var lateral = side == WingSide.Right ? new Vec2(axis.y, -axis.x) : new Vec2(-axis.y, axis.x);
        var reach = Half(enemyWidth) + Half(wingWidth) + (FiniteFloatValidator.IsFinite(margin) ? margin : DefaultMargin);
        return enemy + lateral * reach;
    }

    /// <summary>True once the wing should stop marching and charge.</summary>
    public static bool ShouldCharge(float distanceToWingPoint, float distanceToEnemy) =>
        distanceToWingPoint <= ContactDistance || distanceToEnemy <= ArmsReach;

    private static float Half(float width) => FiniteFloatValidator.IsFinite(width) && width > 0f ? width * 0.5f : 0f;

    private static bool Finite(Vec2 v) => FiniteFloatValidator.IsFinite(v.x) && FiniteFloatValidator.IsFinite(v.y);
}
