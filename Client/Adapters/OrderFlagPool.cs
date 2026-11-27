using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Client.Adapters;

/// <summary>
/// A pool of order flags, built as <c>OrderTroopPlacer.AddOrderPositionEntity</c> builds one (v1.5.7 OrderTroopPlacer.cs:507-511): an empty entity
/// in the scene, not affected by the season, holding a copy of the <c>order_flag_small</c> metamesh (a metamesh, not a prefab), hidden until it is
/// placed.
/// </summary>
internal sealed class OrderFlagPool
{
    private readonly Mission _mission;
    private readonly List<GameEntity> _pool = new();
    private int _shown;

    internal OrderFlagPool(Mission mission) => _mission = mission;

    /// <summary>How many flags are shown now.</summary>
    internal int Shown => _shown;

    /// <summary>Shows one flag at each point and hides the rest.</summary>
    internal void Show(IReadOnlyList<Vec3> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var entity = FlagAt(i);
            var frame = new MatrixFrame(Mat3.Identity, points[i]);
            entity.SetFrame(ref frame);
            entity.SetVisibilityExcludeParents(visible: true);
        }

        for (var i = points.Count; i < _shown; i++)
            _pool[i].SetVisibilityExcludeParents(visible: false);
        _shown = points.Count;
    }

    internal void Hide() => Show(System.Array.Empty<Vec3>());

    /// <summary>Forgets the entities with no native call: the scene goes with the mission.</summary>
    internal void Drop()
    {
        _pool.Clear();
        _shown = 0;
    }

    private GameEntity FlagAt(int index)
    {
        while (_pool.Count <= index)
        {
            var entity = GameEntity.CreateEmpty(_mission.Scene);
            entity.EntityFlags |= EntityFlags.NotAffectedBySeason;
            entity.AddComponent(MetaMesh.GetCopy("order_flag_small"));
            entity.SetVisibilityExcludeParents(visible: false);
            _pool.Add(entity);
        }

        return _pool[index];
    }
}
