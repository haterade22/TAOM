using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Adapters;

/// <summary>
/// <see cref="IMarshalScreenAdapter"/> over the live mission. A flag is built as <c>OrderTroopPlacer.AddOrderPositionEntity</c> builds
/// one (v1.5.6 OrderTroopPlacer.cs:507-511): an empty entity in the scene, not affected by the season, holding a copy of the
/// <c>order_flag_small</c> metamesh (a metamesh, not a prefab), hidden until it is placed.
/// </summary>
internal sealed class MarshalScreenAdapter : IMarshalScreenAdapter
{
    private readonly Mission _mission;
    private readonly List<GameEntity> _pool = new();
    private int _shown;

    internal MarshalScreenAdapter(Mission mission) => _mission = mission;

    public bool PlayerTeamExists => _mission.PlayerTeam != null;

    public void Say(MarshalMessage message) => InformationManager.DisplayMessage(new InformationMessage(TextFor(message).ToString()));

    public void ShowFlags(IReadOnlyList<MarshalFlagPoint> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var entity = FlagAt(i);
            var frame = new MatrixFrame(Mat3.Identity, new Vec3(points[i].X, points[i].Y, points[i].Z));
            entity.SetFrame(ref frame);
            entity.SetVisibilityExcludeParents(visible: true);
        }

        for (var i = points.Count; i < _shown; i++)
            _pool[i].SetVisibilityExcludeParents(visible: false);
        _shown = points.Count;
    }

    public void HideFlags() => ShowFlags(System.Array.Empty<MarshalFlagPoint>());

    public void DropFlags()
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

    private static TextObject TextFor(MarshalMessage message) => message switch
    {
        MarshalMessage.QueueFull => new TextObject("{=taom_marshal_queue_full}A selected company already has twelve orders waiting. The order was not given."),
        MarshalMessage.FireFree => new TextObject("{=taom_marshal_fire_free}The archers fire at will."),
        MarshalMessage.FireOnWord => new TextObject("{=taom_marshal_fire_on_word}The archers draw and hold until you give the word, or for 14 seconds at most."),
        MarshalMessage.FireMassed => new TextObject("{=taom_marshal_fire_massed}The archers draw and hold, and the word is given for you once most of them are ready. A bow is held for 14 seconds at most."),
        MarshalMessage.HoldOn => new TextObject("{=taom_marshal_hold_on}The selected companies hold the line: shielded foot soldiers in a shield wall, circle or square stop attacking until you press Hold the Line again or order a charge."),
        MarshalMessage.HoldOff => new TextObject("{=taom_marshal_hold_off}The selected companies fight again."),
        MarshalMessage.WordGiven => new TextObject("{=taom_marshal_word_given}The word is given."),
        _ => new TextObject("{=taom_marshal_fault}Marshal's Word failed and is off until this scene ends. Your orders work as before."),
    };
}
