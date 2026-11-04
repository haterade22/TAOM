using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// What the command panel does while the commander fights as a soldier (<see cref="FordsControlView"/>). A visible pointer stops a soldier's
/// attack and block (MissionMainAgentController reads MouseVisible), so the panel hides and gives back the mouse. The match runs at 1x for that
/// time, and the chosen speed comes back with the panel.
/// </summary>
internal sealed class FordsPanelStandAside
{
    private readonly FordsGameSpeed _fightSpeed = new();
    private bool _controlling;

    /// <summary>Whether a soldier is controlled this frame.</summary>
    internal bool Update(Mission mission, FordsCommandVM vm, GauntletLayer layer)
    {
        var controlling = FordsAgentControl.IsControlling(mission);
        if (controlling == _controlling)
            return controlling;

        _controlling = controlling;
        vm.SetControlling(controlling);
        if (controlling)
            layer.InputRestrictions.ResetInputRestrictions();
        else
            layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);
        return controlling;
    }

    internal FordsGameSpeed Speed(FordsCommandVM vm) => _controlling ? _fightSpeed : vm.Speed;
}
