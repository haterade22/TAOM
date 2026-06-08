using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.Music;

namespace TAOM.Adapters;

public sealed class TaleWorldsMusicCampaignContextSource : IMusicCampaignContextSource
{
    private readonly IMusicTavernContextSource _tavernContextSource;

    public TaleWorldsMusicCampaignContextSource(IMusicTavernContextSource tavernContextSource)
    {
        _tavernContextSource = tavernContextSource;
    }

    public MusicCampaignContextState Capture()
    {
        try
        {
            var campaign = Campaign.Current;
            if (campaign == null)
                return MusicCampaignContextState.Inactive;

            var mainParty = SafeRead(() => campaign.MainParty);
            if (mainParty == null)
                return MusicCampaignContextState.Inactive;

            var settlement = SafeRead(() => Settlement.CurrentSettlement) ?? SafeRead(() => mainParty.CurrentSettlement);
            var mapEvent = SafeRead(() => mainParty.MapEvent);
            var mapEventActive = mapEvent != null && !SafeRead(() => mapEvent.IsFinalized);
            var siege = mapEventActive && IsSiegeEvent(mapEvent);
            var battle = mapEventActive && !siege && IsBattleEvent(mapEvent);

            if (!siege)
                siege = SafeRead(() => mainParty.BesiegedSettlement != null);

            var inTavern = !siege
                && !battle
                && settlement != null
                && SafeRead(() => _tavernContextSource != null && _tavernContextSource.IsInTavernContext);

            return new MusicCampaignContextState(
                true,
                siege,
                battle,
                settlement != null,
                inTavern,
                SafeRead(() => mainParty.MapFaction?.Culture?.StringId),
                SafeRead(() => settlement?.Culture?.StringId),
                SafeRead(() => settlement?.StringId),
                BuildReason(siege, battle, settlement, inTavern));
        }
        catch
        {
            return MusicCampaignContextState.Inactive;
        }
    }

    private static bool IsSiegeEvent(MapEvent mapEvent)
    {
        return SafeRead(() =>
            mapEvent.IsSiegeAssault ||
            mapEvent.IsSallyOut ||
            mapEvent.IsSiegeOutside ||
            mapEvent.IsBlockade ||
            mapEvent.IsBlockadeSallyOut ||
            mapEvent.IsSiegeAmbush);
    }

    private static bool IsBattleEvent(MapEvent mapEvent)
    {
        return SafeRead(() =>
            mapEvent.IsFieldBattle ||
            mapEvent.IsRaid ||
            mapEvent.IsHideoutBattle);
    }

    private static string BuildReason(bool siege, bool battle, Settlement settlement, bool inTavern)
    {
        if (siege)
            return "campaign_siege";
        if (battle)
            return "campaign_battle";
        if (settlement != null && inTavern)
            return string.IsNullOrWhiteSpace(settlement.StringId) ? "campaign_tavern" : "campaign_tavern:" + settlement.StringId;
        if (settlement != null)
            return string.IsNullOrWhiteSpace(settlement.StringId) ? "campaign_town" : "campaign_town:" + settlement.StringId;
        return "campaign_world";
    }

    private static T SafeRead<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }
}
