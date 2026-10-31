using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Turns one tick's unit samples into the <see cref="MatchSnapshot"/> the match consumes: per (side, lane) count and center,
/// the nearest enemy to each center, the enemies in fire range of every standing tower, and the unit counts inside every
/// capture radius. Pure: the adapters measure, this only counts, so the rules never see an engine type.
/// </summary>
internal static class FordsSnapshotBuilder
{
    public static MatchSnapshot Build(float dt, IReadOnlyList<FordsUnitSample> samples, Func<AgentKey, bool> isHero,
        IReadOnlyList<TowerSlot> standingTowers, IReadOnlyList<CapturePointSlot> points,
        FordsTowerConfig towers, FordsCaptureConfig capture)
    {
        return new MatchSnapshot
        {
            Dt = dt,
            Lanes = BuildLanes(samples),
            TowerTargets = BuildTowerTargets(samples, isHero, standingTowers, towers.FireRange),
            Capture = BuildCapture(samples, points, capture.Radius),
            LiveBySide = new Dictionary<MatchSide, int>
            {
                [MatchSide.West] = samples.Count(s => s.Side == MatchSide.West),
                [MatchSide.East] = samples.Count(s => s.Side == MatchSide.East),
            },
        };
    }

    private static List<LaneSnapshot> BuildLanes(IReadOnlyList<FordsUnitSample> samples)
    {
        var lanes = new List<LaneSnapshot>();
        foreach (var group in samples.GroupBy(s => (s.Side, s.Lane)))
        {
            var members = group.ToList();
            var center = new Vec3f(members.Average(m => m.Position.X), members.Average(m => m.Position.Y),
                members.Average(m => m.Position.Z));
            lanes.Add(new LaneSnapshot(group.Key.Side, group.Key.Lane, members.Count, center,
                NearestEnemy(samples, group.Key.Side, center)));
        }

        return lanes;
    }

    private static float NearestEnemy(IReadOnlyList<FordsUnitSample> samples, MatchSide side, Vec3f center)
    {
        var best = float.MaxValue;
        foreach (var sample in samples)
        {
            if (sample.Side == side)
                continue;

            var distance = sample.Position.Distance2D(center);
            if (distance < best)
                best = distance;
        }

        return best;
    }

    private static List<TowerTargets> BuildTowerTargets(IReadOnlyList<FordsUnitSample> samples, Func<AgentKey, bool> isHero,
        IReadOnlyList<TowerSlot> standingTowers, float fireRange)
    {
        var result = new List<TowerTargets>();
        foreach (var tower in standingTowers)
        {
            var candidates = new List<TargetCandidate>();
            foreach (var sample in samples)
            {
                if (sample.Side == tower.Owner)
                    continue;

                var distance = sample.Position.Distance2D(tower.Position);
                if (distance <= fireRange)
                    candidates.Add(new TargetCandidate(sample.Key, distance, isHero(sample.Key)));
            }

            if (candidates.Count > 0)
                result.Add(new TowerTargets(tower.Id, candidates));
        }

        return result;
    }

    private static List<CaptureCounts> BuildCapture(IReadOnlyList<FordsUnitSample> samples, IReadOnlyList<CapturePointSlot> points,
        float radius)
    {
        var result = new List<CaptureCounts>();
        foreach (var point in points)
        {
            var west = 0;
            var east = 0;
            foreach (var sample in samples)
            {
                // The include form, so an unreadable (NaN) position counts at no point.
                if (!(sample.Position.Distance2D(point.Position) <= radius))
                    continue;
                if (sample.Side == MatchSide.West)
                    west++;
                else
                    east++;
            }

            result.Add(new CaptureCounts(point.Id, west, east));
        }

        return result;
    }
}
