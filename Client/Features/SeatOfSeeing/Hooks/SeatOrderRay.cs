using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TAOM.Core.Validation;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// The order ray from the Seat (build-spec 5.8), the body of <c>Patch140_OrderFlagGetCollidedEntity</c>. It repeats vanilla's
/// <c>OrderFlag.GetCollidedEntity</c> (v1.5.6 OrderFlag.cs:335-350) with one change, the start of the ray: the cursor or the screen
/// centre through <c>ScreenPointToWorldRay</c>, not the main agent's eye. Vanilla's other responsibilities are kept: the 10000 m
/// extension, the focus ray flags (<c>CommonFocusRayCastExcludeFlags</c> plus flora), the 0.3 m ray thickness, and the walk up the
/// parents to an entity with an <c>IOrderable</c> script that gives the player's side an order.
/// </summary>
internal static class SeatOrderRay
{
    private const float RayLength = 10000f;
    internal const float RayThickness = 0.3f;
    internal const BodyFlags RayFlags = BodyFlags.CommonFocusRayCastExcludeFlags | BodyFlags.BodyOwnerFlora;
    private const float SeatedRayLength = 1000f;

    /// <summary>
    /// The ray end lengthened to <see cref="SeatedRayLength"/> along the same line (the postfix on <c>ScreenPointToWorldRay</c>); a ray
    /// already that long, a degenerate one and one that is not a real number are returned as they are.
    /// </summary>
    internal static Vec3 Lengthen(Vec3 rayBegin, Vec3 rayEnd)
    {
        var line = rayEnd - rayBegin;
        var length = line.Length;
        if (!FiniteFloatValidator.IsFinite(length) || length < 1e-4f || length >= SeatedRayLength)
            return rayEnd;

        return rayBegin + line * (SeatedRayLength / length);
    }

    /// <summary>The ray through the pointer: the cursor when the screen shows one, else the screen centre.</summary>
    internal static void PointerRay(MissionScreen screen, out Vec3 begin, out Vec3 end)
    {
        var point = screen.MouseVisible ? Input.MousePositionRanged : new Vec2(0.5f, 0.5f);
        screen.ScreenPointToWorldRay(point, out begin, out end);
    }

    internal static WeakGameEntity Collide(MissionScreen screen, Mission mission, out Vec3 closestPoint)
    {
        PointerRay(screen, out var rayBegin, out var rayEnd);
        var rayEndFar = rayBegin + (rayEnd - rayBegin).NormalizedCopy() * RayLength;

        mission.Scene.RayCastForClosestEntityOrTerrain(rayBegin, rayEndFar, out _, out closestPoint, out var entity, RayThickness, RayFlags);

        while (entity.IsValid && !entity.GetScriptComponents().Any(sc => sc is IOrderable orderable && orderable.GetOrder(Mission.Current.PlayerTeam.Side) != OrderType.None))
            entity = entity.Parent;
        return entity;
    }
}
