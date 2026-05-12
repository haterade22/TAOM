using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class CampaignSessionAdapter : ICampaignSessionAdapter
{
    public bool IsReadyForRebuild(out string reason)
    {
        if (Campaign.Current == null)
        {
            reason = "Campaign.Current is null (no campaign active — load a save first)";
            return false;
        }
        if (Campaign.Current.MapSceneWrapper == null)
        {
            reason = "Campaign.Current.MapSceneWrapper is null (campaign still loading — wait until fully initialized)";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    public INavigationCacheAdapter CreateDefaultRuntimeCacheAdapter(IModLogger? logger)
    {
        // SandBoxNavigationCache's ctor reads Campaign.Current.Models.PartyNavigationModel and
        // .MapDistanceModel. Caller is responsible for verifying IsReadyForRebuild beforehand.
        var cache = new SandBoxNavigationCache(MobileParty.NavigationType.Default);
        return new NavigationCacheAdapter(cache, logger);
    }

    public CampaignSnapshot GetSnapshot()
    {
        var snapshot = new CampaignSnapshot();
        try
        {
            var campaign = Campaign.Current;
            if (campaign == null) return snapshot;

            snapshot.GameId = campaign.UniqueGameId ?? string.Empty;
            snapshot.MapSceneWrapperType = campaign.MapSceneWrapper?.GetType().FullName ?? string.Empty;

            try { snapshot.StartTime = (campaign.Models?.CampaignTimeModel?.CampaignStartTime ?? CampaignTime.Zero).ToString(); }
            catch { /* CampaignTimeModel may not be loaded */ }
            try { snapshot.CurrentTime = CampaignTime.Now.ToString(); }
            catch { /* MapTimeTracker may not be initialized */ }

            var all = Settlement.All;
            if (all != null)
            {
                snapshot.SettlementCount = all.Count;
                snapshot.FortificationCount = all.Count(s => s.IsFortification);
                snapshot.TownCount = all.Count(s => s.IsTown);
                snapshot.CastleCount = all.Count(s => s.IsCastle);
                snapshot.VillageCount = all.Count(s => s.IsVillage);
            }
        }
        catch
        {
            // GetSnapshot is best-effort diagnostic; partial data is fine.
        }
        return snapshot;
    }
}
