using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters.Models;
using TAOM.Features.MixedFormations.Models;

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

    public IReadOnlyList<FormationUnit> Units =>
        _formation.UnitsWithoutLooseDetachedOnes
            .OfType<Agent>()
            .OrderBy(a => a.Index)
            .Select(a => new FormationUnit(a.Index, a.Character?.IsRanged ?? false))
            .ToList();

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

    // -- CompanionTactics extensions (Patch35) -----------------------------------

    public int FormationIndex => _formation == null ? -1 : (int)_formation.FormationIndex;

    public int RangedUnitCount =>
        _formation?.QuerySystem == null ? 0
            : (int)((_formation.QuerySystem.RangedUnitRatio) * _formation.CountOfUnits + 1e-5f);

    public int CavalryUnitCount =>
        _formation?.QuerySystem == null ? 0
            : (int)((_formation.QuerySystem.CavalryUnitRatio) * _formation.CountOfUnits + 1e-5f);

    // Polearm/shield require per-unit equipment scan. TTL-cached: action bar refreshes
    // ≤ twice/sec so 500ms is plenty.
    private float _lastCompositionScanMs = -1000f;
    private int _cachedPolearmCount;
    private int _cachedShieldCount;
    private const float CompositionTtlMs = 500f;

    public int PolearmUnitCount { get { EnsureCompositionFresh(); return _cachedPolearmCount; } }
    public int ShieldUnitCount  { get { EnsureCompositionFresh(); return _cachedShieldCount;  } }

    private void EnsureCompositionFresh()
    {
        if (_formation == null) { _cachedPolearmCount = 0; _cachedShieldCount = 0; return; }
        var nowMs = (float)System.Environment.TickCount;
        if (nowMs - _lastCompositionScanMs < CompositionTtlMs && _lastCompositionScanMs > 0f) return;
        _lastCompositionScanMs = nowMs;

        var polearm = 0;
        var shield = 0;
        foreach (var unit in _formation.UnitsWithoutLooseDetachedOnes)
        {
            if (unit is not Agent agent) continue;
            var equipment = agent.SpawnEquipment;
            if (equipment.IsEmpty()) continue;
            var sawPolearm = false;
            var sawShield = false;
            for (var slot = 0; slot < 4 && !(sawPolearm && sawShield); slot++)
            {
                var element = equipment[(TaleWorlds.Core.EquipmentIndex)slot];
                var item = element.Item;
                if (item == null) continue;
                if (!sawShield && item.ItemType == TaleWorlds.Core.ItemObject.ItemTypeEnum.Shield)
                    sawShield = true;
                var primary = item.PrimaryWeapon;
                if (!sawPolearm && primary != null)
                {
                    var wc = primary.WeaponClass;
                    if (wc == TaleWorlds.Core.WeaponClass.OneHandedPolearm
                        || wc == TaleWorlds.Core.WeaponClass.TwoHandedPolearm
                        || wc == TaleWorlds.Core.WeaponClass.LowGripPolearm)
                        sawPolearm = true;
                }
            }
            if (sawPolearm) polearm++;
            if (sawShield) shield++;
        }
        _cachedPolearmCount = polearm;
        _cachedShieldCount = shield;
    }

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
