using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Adapters;

/// <summary><see cref="IMarshalOutlineAdapter{TKey}"/> over the player's order controller, painting as <c>RaceAbilityVisuals.Paint</c> does.</summary>
internal sealed class MarshalOutlineAdapter : IMarshalOutlineAdapter<Agent>
{
    private readonly IModLogger _logger;
    private Dictionary<Agent, uint>? _into;
    private uint _colour;

    public MarshalOutlineAdapter(IModLogger logger) => _logger = logger;

    public void ReadWanted(Dictionary<Agent, uint> into)
    {
        var selected = Mission.Current?.PlayerTeam?.PlayerOrderController?.SelectedFormations;
        if (selected == null)
            return;
        _into = into;
        foreach (var formation in selected)
        {
            Collect(formation, MarshalTuning.SelectedOutline);
            if (formation.TargetFormation != null)
                Collect(formation.TargetFormation, MarshalTuning.MarkedOutline);
        }

        _into = null;
    }

    // Paints or clears the one unit it is given. A mount is a wanted unit of its own (Add), so the service's dictionary remembers it and clears it as itself:
    // a clear that reached the mount through its rider would miss it once the rider dismounted or died.
    public void Paint(Agent unit, uint? colour)
    {
        MissionThreadGuard.NoteCall("MarshalOutlineAdapter.Paint", _logger.LogError);
        PaintOne(unit, colour);
    }

    private static void PaintOne(Agent agent, uint? colour)
    {
        if (!AgentSlotIdentity.IsCurrentOccupant(agent) || agent.State == AgentState.Deleted)
            return;
        var visuals = agent.AgentVisuals;
        if (visuals != null && visuals.IsValid())
            visuals.SetContourColor(colour, true);
    }

    private void Collect(Formation formation, uint colour)
    {
        _colour = colour;
        formation.ApplyActionOnEachUnit(Add);
    }

    private void Add(Agent agent)
    {
        if (!agent.IsActive())
            return;
        _into![agent] = _colour;
        var mount = agent.MountAgent;
        if (mount != null && mount.IsActive())
            _into[mount] = _colour;
    }
}
