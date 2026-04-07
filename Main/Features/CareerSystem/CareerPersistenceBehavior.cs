using TaleWorlds.CampaignSystem;
using TAOM.Features.CareerSystem.Domain;
using System.Collections.Generic;

namespace TAOM.Features.CareerSystem;

public class CareerPersistenceBehavior : CampaignBehaviorBase
{
    private readonly ICareerDataService _dataService;

    public CareerPersistenceBehavior(ICareerDataService dataService)
    {
        _dataService = dataService;
    }

    public override void RegisterEvents()
    {
    }

    public override void SyncData(IDataStore dataStore)
    {
        var data = _dataService.GetAllData();
        dataStore.SyncData("_taom_careerData", ref data);
        _dataService.RestoreData(data);
    }
}
