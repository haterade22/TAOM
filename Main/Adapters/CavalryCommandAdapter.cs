using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.SmartCavalryAI;

namespace TAOM.Adapters;

public sealed class CavalryCommandAdapter : ICavalryCommandAdapter
{
    private readonly Formation _formation;

    public CavalryCommandAdapter(Formation formation)
    {
        _formation = formation;
    }

    public Vec2 CurrentPosition => _formation?.CurrentPosition ?? Vec2.Zero;
    public Vec2 Direction => _formation?.Direction ?? Vec2.Zero;

    public void IssueStop()
    {
        if (_formation == null) return;
        using (SmartCavalryRecursionGuard.Enter())
        {
            _formation.SetMovementOrder(MovementOrder.MovementOrderStop);
        }
    }

    public bool IssueMoveTo(Vec2 worldXY, float groundHeight)
    {
        if (_formation == null) return false;
        var scene = Mission.Current?.Scene;
        if (scene == null) return false;
        var pos = new WorldPosition(scene, new Vec3(worldXY.x, worldXY.y, groundHeight, -1f));
        if (!pos.IsValid) return false;
        using (SmartCavalryRecursionGuard.Enter())
        {
            _formation.SetMovementOrder(MovementOrder.MovementOrderMove(pos));
        }
        return true;
    }

    public void IssueChargeToTarget(object targetToken)
    {
        if (_formation == null) return;
        if (targetToken is not Formation target) return;
        using (SmartCavalryRecursionGuard.Enter())
        {
            _formation.SetMovementOrder(MovementOrder.MovementOrderChargeToTarget(target));
            // SetMovementOrder ends with SetTargetFormation(null) (Formation.cs:714), which pushes
            // target index -1 to every rider. Vanilla's own targeted charge sets the target AFTER the
            // movement order for the same reason (OrderController.cs:812-817).
            _formation.SetTargetFormation(target);
        }
    }

    public void IssueCharge()
    {
        if (_formation == null) return;
        using (SmartCavalryRecursionGuard.Enter())
        {
            _formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }
    }

    public void ApplyChargeLine(Vec3 worldPosition, Vec2 direction, int unitSpacing)
    {
        if (_formation == null) return;
        var scene = Mission.Current?.Scene;
        if (scene == null) return;
        var worldPos = new WorldPosition(scene, worldPosition);
        if (!worldPos.IsValid) return;
        using (SmartCavalryRecursionGuard.Enter())
        {
            _formation.SetPositioning(worldPos, direction, unitSpacing);
            // Formation.Tick re-applies FacingOrder.GetDirection through SetPositioning every tick
            // (Formation.cs:2311-2314); without this the line faces whatever the last facing order
            // said (LookAtDirection from deployment, or LookAtEnemy) one tick later.
            _formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(direction));
        }
    }

    public bool IsTargetAlive(object targetToken)
    {
        if (targetToken is not Formation target) return false;
        return target.CountOfUnits > 0;
    }

    public bool TryGetTargetPosition(object targetToken, out Vec2 position)
    {
        if (targetToken is Formation target && target.CountOfUnits > 0)
        {
            position = target.CurrentPosition;
            return true;
        }
        position = Vec2.Zero;
        return false;
    }

    public float GetTargetDepthAlong(object targetToken, Vec2 direction)
    {
        if (targetToken is not Formation target || target.CountOfUnits == 0) return 0f;
        var centre = target.CurrentPosition;
        var depth = 0f;
        foreach (var unit in target.UnitsWithoutLooseDetachedOnes)
        {
            if (unit is not Agent agent) continue;
            var along = Vec2.DotProduct(agent.Position.AsVec2 - centre, direction);
            if (along > depth) depth = along;
        }
        return depth;
    }
}
