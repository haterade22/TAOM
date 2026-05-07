using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters.Models;

namespace TAOM.Adapters;

public sealed class FormationAdapter : IFormationAdapter
{
    private readonly Formation _formation;

    public FormationAdapter(Formation formation)
    {
        _formation = formation;
    }

    public int CountOfUnits => _formation.CountOfUnits;
    public bool OrderPositionIsValid => _formation.OrderPositionIsValid;
    public Vec2 OrderPosition => _formation.OrderPosition;
    public Vec2 Direction => _formation.Direction;
    public float Width => _formation.Width;
    public float Interval => _formation.Interval;

    public bool IsHolding =>
        _formation.GetMovementState() == MovementOrder.MovementStateEnum.Hold;

    public object FormationKey => _formation;

    public bool RepresentativeIsCavalry =>
        _formation?.QuerySystem != null && _formation.QuerySystem.IsCavalryFormation;

    public bool IsMoving =>
        _formation != null && _formation.GetMovementState() != MovementOrder.MovementStateEnum.Hold;

    public MovementOrderType CurrentMovementOrderType
    {
        get
        {
            if (_formation == null) return MovementOrderType.Other;
            ref readonly var order = ref _formation.GetReadonlyMovementOrderReference();
            return order.OrderEnum switch
            {
                MovementOrder.MovementOrderEnum.Charge => MovementOrderType.Charge,
                MovementOrder.MovementOrderEnum.ChargeToTarget => MovementOrderType.ChargeToTarget,
                _ => MovementOrderType.Other,
            };
        }
    }

    public Vec2 CurrentPosition => _formation?.CurrentPosition ?? Vec2.Zero;

    public bool IsAligned(float strictness)
    {
        if (_formation == null || _formation.CountOfUnits < 2) return true;

        // Single pass — sum + count for the average, AND collect projections so the
        // second pass for max-deviation doesn't re-iterate the engine collection.
        var right = _formation.Direction.RightVec();
        var sumProj = 0f;
        var count = 0;
        _alignmentScratch.Clear();
        foreach (var unit in _formation.UnitsWithoutLooseDetachedOnes)
        {
            if (unit is not Agent agent) continue;
            var proj = Vec2.DotProduct(agent.Position.AsVec2, right);
            _alignmentScratch.Add(proj);
            sumProj += proj;
            count++;
        }
        if (count < 2) return true;

        var avgProj = sumProj / count;
        var maxDeviation = 0f;
        for (var i = 0; i < _alignmentScratch.Count; i++)
        {
            var dev = System.Math.Abs(_alignmentScratch[i] - avgProj);
            if (dev > maxDeviation) maxDeviation = dev;
        }

        var tolerance = 5f * (1f - strictness);
        return maxDeviation < tolerance;
    }

    private static readonly System.Collections.Generic.List<float> _alignmentScratch = new();
}
