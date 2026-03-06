using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public class BannerExclusionService : IBannerExclusionService
{
    private HashSet<string> _playerModifiedIds = new();

    public int ExclusionCount => _playerModifiedIds.Count;

    public void MarkAsPlayerModified(string id)
    {
        _playerModifiedIds.Add(id);
    }

    public bool IsPlayerModified(string id)
    {
        return _playerModifiedIds.Contains(id);
    }

    public void SyncData(IDataStore dataStore)
    {
        var list = new List<string>(_playerModifiedIds);
        dataStore.SyncData("_taom_playerModifiedBanners", ref list);
        if (list != null)
        {
            _playerModifiedIds = new HashSet<string>(list);
        }
    }
}
