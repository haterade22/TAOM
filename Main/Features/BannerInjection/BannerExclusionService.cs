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

    // Phase 9b #124 R1 — clear singleton state on new campaign; SyncData on a save predating
    // this feature, or an absent-key load, retains in-memory state from prior campaign.
    public void Reset()
    {
        _playerModifiedIds.Clear();
    }

    public void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var save = new List<string>(_playerModifiedIds);
            dataStore.SyncData("_taom_playerModifiedBanners", ref save);
            return;
        }

        // Loading: initialize to null so an absent-key load is a true no-op, not a re-assignment
        // to current in-memory state (which would carry stale data from a prior campaign).
        // Phase 9b #124 — was `new List<string>(_playerModifiedIds)` which masked absent-key loads.
        List<string> load = null;
        dataStore.SyncData("_taom_playerModifiedBanners", ref load);
        _playerModifiedIds = load != null
            ? new HashSet<string>(load)
            : new HashSet<string>();
    }
}
