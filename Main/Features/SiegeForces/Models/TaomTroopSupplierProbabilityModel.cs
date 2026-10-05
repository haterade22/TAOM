using System.Collections.Generic;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Adapters;

namespace TAOM.Features.SiegeForces.Models;

/// <summary>
/// The siege troop picker's exclusion seam (docs/features/siege-forces.md). The engine builds each side's ready list in
/// <c>MapEventSide.MakeReady</c> by calling this model once per party with the side's shared list, which the base model
/// appends that party's healthy troops to. This override notes the list's length before the base call, calls the base
/// first, and hands the service a window over what the base appended. While a plan is armed the service removes the
/// troops the player left out, so they are never allocated: no agent, no casualty, no capture.
///
/// Vanilla precedent for the count-then-base shape (v1.5.3 StoryModeTroopSupplierProbabilityModel.cs): it notes
/// <c>priorityList.Count</c> (:19), calls its BaseModel (:20), and its tutorial branch then scans only the entries
/// appended after that count (:28). Its other branch scans from index 0 (:40-49), and both only set one entry's
/// priority to 0.01f, where this override's service removes entries. Nothing here branches (gamemodels.md rule 4): the
/// window is built by the service, inside its try, from the factory below. With no plan armed the call is the base
/// model's, unchanged, so a campaign without a wall battle in progress behaves as vanilla. Campaign only: a wall
/// battle's picker exists only in a campaign, and CustomBattle builds its own troop supplier.
/// </summary>
public sealed class TaomTroopSupplierProbabilityModel : DefaultTroopSupplierProbabilityModel
{
    private readonly SiegeForcesService _service;

    public TaomTroopSupplierProbabilityModel(SiegeForcesService service)
    {
        _service = service;
    }

    public override void EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization(
        MapEventParty battleParty,
        FlattenedTroopRoster priorityTroops,
        bool includePlayer,
        int sizeOfSide,
        bool forcePriorityTroops,
        List<(FlattenedTroopRosterElement, MapEventParty, float)> priorityList)
    {
        var from = priorityList.Count;
        base.EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization(
            battleParty, priorityTroops, includePlayer, sizeOfSide, forcePriorityTroops, priorityList);
        _service.FilterAppended(includePlayer, () => new ReadyListWindow(battleParty, priorityList, from));
    }
}
