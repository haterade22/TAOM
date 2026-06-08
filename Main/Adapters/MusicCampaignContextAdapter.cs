using TAOM.Features.Music;

namespace TAOM.Adapters;

public sealed class MusicCampaignContextAdapter : IMusicCampaignContextAdapter
{
    private readonly IMusicCampaignContextSource _source;

    public MusicCampaignContextAdapter(IMusicCampaignContextSource source)
    {
        _source = source;
    }

    public MusicRouteSnapshot CaptureSnapshot()
    {
        var state = _source.Capture();
        if (state == null || !state.IsActive)
            return MusicRouteSnapshot.Empty;

        var combat = state.Siege || state.Battle;
        var tavern = !combat && state.InSettlement && state.InTavern;
        var town = !combat && state.InSettlement && !tavern;
        var world = !combat && !tavern && !town;
        var cultureId = SelectCulture(state, tavern || town);
        var reason = FirstNonEmpty(state.Reason, BuildReason(state, tavern, town, world), "campaign");

        return new MusicRouteSnapshot(
            true,
            cultureId,
            state.Siege,
            state.Battle,
            tavern,
            town,
            world,
            reason);
    }

    private static string SelectCulture(MusicCampaignContextState state, bool settlementBucket)
    {
        return settlementBucket
            ? FirstNonEmpty(state.SettlementCultureId, state.StableCultureId, MusicTrackIndex.NeutralCulture)
            : FirstNonEmpty(state.StableCultureId, state.SettlementCultureId, MusicTrackIndex.NeutralCulture);
    }

    private static string BuildReason(MusicCampaignContextState state, bool tavern, bool town, bool world)
    {
        if (state.Siege)
            return "campaign_siege";
        if (state.Battle)
            return "campaign_battle";
        if (tavern)
            return BuildSettlementReason("campaign_tavern", state.SettlementId);
        if (town)
            return BuildSettlementReason("campaign_town", state.SettlementId);
        if (world)
            return "campaign_world";
        return "campaign";
    }

    private static string BuildSettlementReason(string prefix, string settlementId)
    {
        return string.IsNullOrWhiteSpace(settlementId) ? prefix : prefix + ":" + settlementId.Trim();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return MusicTrackIndex.NeutralCulture;
    }
}
