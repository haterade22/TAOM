using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// Gold mode (#1109): charges the gold price of elite prisoners recruited on the party screen.
/// That path never prices through <c>GetTroopRecruitmentCost</c>, and in resource mode its charge is
/// <c>recruit_cost</c> in the resource (SpecialResourcesBehavior.OnUnitRecruited, which stands down in
/// gold mode). It listens to the prisoner event only: OnUnitRecruited also fires for volunteers and
/// mercenaries, whose price already carries the gold surcharge. Like the resource charge it is not
/// gated (#563): GiveGoldAction takes at most the gold the hero holds, as the resource floors at zero.
/// </summary>
public sealed class SpecialResourceGoldPrisonerBehavior : CampaignBehaviorBase
{
    private readonly ISpecialResourceService _service;
    private readonly IModLogger _logger;

    public SpecialResourceGoldPrisonerBehavior(ISpecialResourceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnMainPartyPrisonerRecruitedEvent.AddNonSerializedListener(this, OnPrisonersRecruited);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnPrisonersRecruited(FlattenedTroopRoster roster)
    {
        var hero = Hero.MainHero;
        if (!_service.IsGoldMode || roster == null || hero == null) return;

        var troopIds = new List<string>();
        foreach (var troop in roster.Troops)
            troopIds.Add(troop?.StringId);

        var gold = _service.GetGoldPrisonerRecruitCharge(troopIds);
        if (gold <= 0) return;

        var before = hero.Gold;
        GiveGoldAction.ApplyBetweenCharacters(hero, null, gold);
        _logger.LogInfo($"[SpecRes] GOLD PRISONER RECRUIT: {troopIds.Count} unit(s) priced {gold} gold, paid {before - hero.Gold}");
    }
}
