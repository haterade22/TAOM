using System;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// Owns the one subscription to <c>Mission.GetAgentTroopClass_Override</c> for a mission: the
/// engine asks it for every spawned troop, from <c>MissionAgentSpawnLogic.OnMissionTick</c> on
/// the main thread, after every behavior's <c>EarlyStart</c>, so subscribing there is in time
/// for the first spawn. The event is a <c>Func</c>; with several subscribers the last one's
/// answer wins, and neither vanilla nor any other TAOM feature subscribes. Nothing is
/// subscribed when no doctrine routes a troop, and the handler is removed at mission end so a
/// catalog reference never outlives the mission object it was attached to.
/// </summary>
public sealed class FormationRoutingSubscriber
{
    private Mission? _mission;
    private DoctrineCatalog? _catalog;
    private Func<BattleSideEnum, BasicCharacterObject, FormationClass>? _handler;

    public bool IsSubscribed => _mission != null;

    public void Subscribe(Mission mission, DoctrineCatalog catalog)
    {
        Unsubscribe();
        if (!catalog.HasFormationRouting)
            return;
        _mission = mission;
        _catalog = catalog;
        _handler = Route;
        mission.GetAgentTroopClass_Override += _handler;
    }

    public void Unsubscribe()
    {
        if (_mission != null && _handler != null)
            _mission.GetAgentTroopClass_Override -= _handler;
        _mission = null;
        _catalog = null;
        _handler = null;
    }

    private FormationClass Route(BattleSideEnum side, BasicCharacterObject character)
    {
        var mission = _mission;
        var catalog = _catalog;
        var engineClass = character.GetFormationClass();
        if (mission == null || catalog == null)
            return engineClass;
        var dismount = FormationRoutingRule.Dismounts(mission.IsSiegeBattle, mission.IsNavalBattle, mission.IsNavalRaidBattle, mission.IsSallyOutBattle, side);
        var routing = catalog.Resolve(character.Culture?.StringId).Formations;
        return FormationRoutingRule.Apply(engineClass, dismount, routing, character.StringId);
    }
}
