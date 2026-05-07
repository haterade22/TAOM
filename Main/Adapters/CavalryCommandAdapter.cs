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

    public void IssueMoveTo(Vec2 worldXY, float groundHeight)
    {
        if (_formation == null) return;
        var scene = Mission.Current?.Scene;
        if (scene == null) return;
        var pos = new WorldPosition(scene, new Vec3(worldXY.x, worldXY.y, groundHeight, -1f));
        if (!pos.IsValid) return;
        using (SmartCavalryRecursionGuard.Enter())
        {
            _formation.SetMovementOrder(MovementOrder.MovementOrderMove(pos));
        }
    }

    public void IssueChargeToTarget(object targetToken)
    {
        if (_formation == null) return;
        if (targetToken is not Formation target) return;
        using (SmartCavalryRecursionGuard.Enter())
        {
            _formation.SetMovementOrder(MovementOrder.MovementOrderChargeToTarget(target));
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
        }
    }

    public bool IsTargetAlive(object targetToken)
    {
        if (targetToken is not Formation target) return false;
        return target.CountOfUnits > 0;
    }
}
