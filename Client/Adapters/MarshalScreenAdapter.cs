using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Adapters;

/// <summary>
/// <see cref="IMarshalScreenAdapter"/> over the live mission. Its flags come from an <see cref="OrderFlagPool"/>, built as
/// <c>OrderTroopPlacer.AddOrderPositionEntity</c> builds one flag.
/// </summary>
internal sealed class MarshalScreenAdapter : IMarshalScreenAdapter
{
    private readonly Mission _mission;
    private readonly OrderFlagPool _flags;

    internal MarshalScreenAdapter(Mission mission)
    {
        _mission = mission;
        _flags = new OrderFlagPool(mission);
    }

    public bool PlayerTeamExists => _mission.PlayerTeam != null;

    public void Say(MarshalMessage message) => InformationManager.DisplayMessage(new InformationMessage(TextFor(message).ToString()));

    public void ShowFlags(IReadOnlyList<MarshalFlagPoint> points)
    {
        var at = new List<Vec3>(points.Count);
        foreach (var point in points)
            at.Add(new Vec3(point.X, point.Y, point.Z));
        _flags.Show(at);
    }

    public void HideFlags() => _flags.Hide();

    public void DropFlags() => _flags.Drop();

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
