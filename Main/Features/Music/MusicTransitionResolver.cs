using System.Collections.Generic;

namespace TAOM.Features.Music;

public sealed class MusicTransitionResolver
{
    public MusicRouteDecision Resolve(
        MusicTrackIndex trackIndex,
        MusicRouteSettings settings,
        MusicRouteSnapshot missionSnapshot,
        MusicRouteSnapshot campaignSnapshot)
    {
        if (trackIndex == null)
            return MusicRouteDecision.None("missing_track_index");

        if (settings == null || !settings.MusicEnabled)
            return MusicRouteDecision.None("music_disabled");

        var missionDecision = TryResolveSnapshot(
            trackIndex,
            settings,
            missionSnapshot,
            MusicRouteSource.Mission,
            BuildMissionOrder);
        if (missionDecision.HasSelection)
            return missionDecision;

        var campaignDecision = TryResolveSnapshot(
            trackIndex,
            settings,
            campaignSnapshot,
            MusicRouteSource.Campaign,
            BuildCampaignOrder);
        if (campaignDecision.HasSelection)
            return campaignDecision;

        return MusicRouteDecision.None("no_candidate_pool");
    }

    private static MusicRouteDecision TryResolveSnapshot(
        MusicTrackIndex trackIndex,
        MusicRouteSettings settings,
        MusicRouteSnapshot snapshot,
        MusicRouteSource source,
        System.Func<MusicRouteSnapshot, IEnumerable<MusicBucket>> buildOrder)
    {
        if (snapshot == null || !snapshot.IsActive)
            return MusicRouteDecision.None("snapshot_inactive");

        foreach (var bucket in buildOrder(snapshot))
        {
            if (!settings.IsBucketEnabled(bucket))
                continue;

            var pool = trackIndex.ResolvePool(bucket, snapshot.CultureId);
            if (!pool.HasTracks)
                continue;

            return MusicRouteDecision.Selected(source, bucket, pool, snapshot.Reason);
        }

        return MusicRouteDecision.None("snapshot_no_candidate_pool");
    }

    private static IEnumerable<MusicBucket> BuildMissionOrder(MusicRouteSnapshot snapshot)
    {
        if (snapshot.CharacterCreation)
            yield return MusicBucket.CharacterCreation;
        if (snapshot.Siege)
            yield return MusicBucket.Siege;
        if (snapshot.Battle)
            yield return MusicBucket.Battle;
        if (snapshot.Tavern)
            yield return MusicBucket.Tavern;
        if (snapshot.Town)
            yield return MusicBucket.Town;
        if (snapshot.World)
            yield return MusicBucket.World;
    }

    private static IEnumerable<MusicBucket> BuildCampaignOrder(MusicRouteSnapshot snapshot)
    {
        if (snapshot.CharacterCreation)
            yield return MusicBucket.CharacterCreation;
        if (snapshot.Siege)
            yield return MusicBucket.Siege;
        if (snapshot.Battle)
            yield return MusicBucket.Battle;
        if (snapshot.Tavern)
            yield return MusicBucket.Tavern;
        if (snapshot.Town)
            yield return MusicBucket.Town;
        if (snapshot.World)
            yield return MusicBucket.World;
    }
}
