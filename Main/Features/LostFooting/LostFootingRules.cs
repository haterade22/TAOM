using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Validation;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Features.LostFooting;

/// <summary>
/// The Lost Footing judgements, as pure functions over <see cref="AgentProbe"/> facts and the mission state: which
/// kind an agent is, whether it is stranded and for how long, the episodes and hotspots that follow from it, and
/// where the next slice of the agent list starts. Every engine-supplied float passes a finite check before a
/// comparison, so a NaN fails its gate instead of passing it. No engine types, no clock of its own.
/// </summary>
public static class LostFootingRules
{
    /// <summary>Hotspot squares listed in the summary.</summary>
    public const int TopHotspots = 5;

    /// <summary>An entry not read for more than this many complete passes over the agent list is dropped.</summary>
    public const int PruneAfterSweeps = 2;

    /// <summary>The most specific kind that fits: an object user, then a router, a creature, a riderless mount, a soldier.</summary>
    public static AgentKind Classify(AgentProbe p)
    {
        if (p.IsUsingObject) return AgentKind.UsingObject;
        if (p.IsRouter) return AgentKind.Router;
        if (p.IsCreature) return AgentKind.Creature;
        if (p.IsMount) return AgentKind.RiderlessMount;
        return p.IsHuman && p.InFormation ? AgentKind.Soldier : AgentKind.Other;
    }

    /// <summary>
    /// Folds one sighting into the state at mission clock <paramref name="now"/> and returns the agent's entry, or null
    /// for an agent the census never counts (one a person controls).
    /// </summary>
    public static StrandEntry? Observe(LostFootingMissionState state, AgentProbe p, float now, LostFootingTunables t)
    {
        if (p.IsPlayerControlled) return null;

        if (!state.Entries.TryGetValue(p.Handle, out var e))
        {
            e = new StrandEntry();
            state.Entries[p.Handle] = e;
        }

        var previous = e.LastSeen;
        e.LastSeen = now;
        e.Sweep = state.Sweep;
        e.Kind = Classify(p);
        e.X = p.X;
        e.Y = p.Y;
        e.Z = p.Z;
        e.Depth = FiniteFloatValidator.IsFinite(p.WaterLevelHere) && FiniteFloatValidator.IsFinite(p.Z) ? p.WaterLevelHere - p.Z : float.NaN;
        e.DistToFormation = p.DistToFormation;
        e.DistToDestination = p.DistToDestination;
        e.Unreachable = p.Unreachable;

        if (p.FaceMiss)
        {
            if (float.IsNaN(e.MissSince)) e.MissSince = now;
        }
        else
        {
            e.MissSince = float.NaN;
            e.ConfirmTried = false;
            e.Detail = null;
        }

        e.OffMesh = Elapsed(e.MissSince, now, t.OffMeshConfirmSeconds);

        var slow = FiniteFloatValidator.IsFinite(p.Speed) && p.Speed < t.StillSpeed;
        // The formation spot is the goal while the engine gives one; otherwise the AI's move destination.
        var toGoal = FiniteFloatValidator.IsFinite(p.DistToFormation) ? p.DistToFormation : p.DistToDestination;
        var hasWhereToGo = FiniteFloatValidator.IsFinite(toGoal) && toGoal >= t.MinDestinationMeters;
        e.WaterSince = Hold(e.WaterSince, p.InWaterMode && slow && hasWhereToGo, now);
        e.WaterStill = Elapsed(e.WaterSince, now, t.WaterStillSeconds);

        var belowTheWater = !p.InWaterMode && slow && FiniteFloatValidator.IsFinite(state.SceneWaterLevel)
                            && FiniteFloatValidator.IsFinite(p.Z) && p.Z < state.SceneWaterLevel;
        e.LandSince = Hold(e.LandSince, belowTheWater, now);
        e.LandBelow = Elapsed(e.LandSince, now, t.WaterStillSeconds);

        if (e.IsStranded)
        {
            if (float.IsNaN(e.StrandedSince)) e.StrandedSince = EarliestEvidence(e);
            AddHotspot(state, e, previous, now, t);
        }
        else if (!float.IsNaN(e.StrandedSince))
        {
            CloseEpisode(state, e, now, EpisodeEnd.Recovered);
        }

        return e;
    }

    private static float Hold(float since, bool holds, float now) => !holds ? float.NaN : float.IsNaN(since) ? now : since;

    private static bool Elapsed(float since, float now, float seconds) => !float.IsNaN(since) && now - since >= seconds;

    private static float EarliestEvidence(StrandEntry e)
    {
        var earliest = float.NaN;
        if (e.OffMesh) earliest = Earlier(earliest, e.MissSince);
        if (e.WaterStill) earliest = Earlier(earliest, e.WaterSince);
        if (e.LandBelow) earliest = Earlier(earliest, e.LandSince);
        return earliest;
    }

    private static float Earlier(float a, float b) => float.IsNaN(a) || b < a ? b : a;

    private static void AddHotspot(LostFootingMissionState state, StrandEntry e, float previous, float now, LostFootingTunables t)
    {
        if (!FiniteFloatValidator.IsFinite(previous) || !FiniteFloatValidator.IsFinite(e.X) || !FiniteFloatValidator.IsFinite(e.Y)) return;
        var seconds = Math.Max(0f, now - previous);
        var key = (Cell(e.X, t.HotspotCellMeters), Cell(e.Y, t.HotspotCellMeters));
        state.Hotspots.TryGetValue(key, out var total);
        state.Hotspots[key] = total + seconds;
    }

    private static int Cell(float metres, float cell) => (int)Math.Floor(metres / cell);

    private enum EpisodeEnd
    {
        Recovered,
        Removed,
        AtEnd,
    }

    private static void CloseEpisode(LostFootingMissionState state, StrandEntry e, float end, EpisodeEnd reason)
    {
        var length = end - e.StrandedSince;
        e.StrandedSince = float.NaN;
        if (!FiniteFloatValidator.IsFinite(length) || length < 0f) return;
        if (reason == EpisodeEnd.Recovered) state.Recovered++;
        else if (reason == EpisodeEnd.Removed) state.Removed++;
        else state.AtEnd++;
        state.EpisodeKinds[(int)e.Kind]++;
        if (length > state.LongestEpisode) state.LongestEpisode = length;
    }

    /// <summary>Closes every episode still open, at each agent's last sighting. Safe to call twice.</summary>
    public static void CloseEpisodes(LostFootingMissionState state)
    {
        foreach (var e in state.Entries.Values)
        {
            if (!float.IsNaN(e.StrandedSince)) CloseEpisode(state, e, e.LastSeen, EpisodeEnd.AtEnd);
        }
    }

    /// <summary>
    /// The census stops looking (switched off, or the gate closed): open episodes end where they were last seen and every
    /// agent is forgotten, so the paused time is never bridged into a stranded stretch or a hotspot.
    /// </summary>
    public static void Pause(LostFootingMissionState state)
    {
        CloseEpisodes(state);
        state.Entries.Clear();
        state.Cursor = 0;
    }

    /// <summary>
    /// Plans the next slice of an agent list of <paramref name="count"/> from <paramref name="cursor"/>. A cursor past
    /// the end (the list shrank) restarts at zero; the slice that reaches the end wraps, and so does an empty list, so a
    /// sweep always finishes and stale entries age out.
    /// </summary>
    public static SlicePlan PlanSlice(int cursor, int max, int count)
    {
        if (count <= 0) return new SlicePlan { Start = 0, Take = 0, Next = 0, Wrapped = true };
        var start = cursor < 0 || cursor >= count ? 0 : cursor;
        var take = Math.Min(Math.Max(max, 0), count - start);
        var next = start + take;
        var wrapped = next >= count;
        return new SlicePlan { Start = start, Take = take, Next = wrapped ? 0 : next, Wrapped = wrapped };
    }

    /// <summary>Ends one complete pass over the agent list: counts it, and drops entries not read for too many passes.</summary>
    public static void EndSweep(LostFootingMissionState state)
    {
        state.Sweep++;
        List<object>? gone = null;
        foreach (var pair in state.Entries)
        {
            if (state.Sweep - pair.Value.Sweep <= PruneAfterSweeps) continue;
            (gone ??= new List<object>()).Add(pair.Key);
        }

        if (gone == null) return;
        foreach (var handle in gone)
        {
            var e = state.Entries[handle];
            if (!float.IsNaN(e.StrandedSince)) CloseEpisode(state, e, e.LastSeen, EpisodeEnd.Removed);
            state.Entries.Remove(handle);
        }
    }

    /// <summary>Counts the stranded agents by class and kind, and raises the peaks.</summary>
    public static LostFootingTally Tally(LostFootingMissionState state)
    {
        var tally = new LostFootingTally { Tracked = state.Entries.Count };
        foreach (var e in state.Entries.Values)
        {
            if (!e.IsStranded) continue;
            if (e.OffMesh)
            {
                tally.Add(StrandClass.OffMesh, e.Kind);
                if (e.Detail != null && e.Detail.Confirmed) tally.Confirmed++;
            }

            if (e.WaterStill) tally.Add(StrandClass.WaterStill, e.Kind);
            if (e.LandBelow) tally.Add(StrandClass.LandBelowWater, e.Kind);
        }

        state.PeakOffMesh = Math.Max(state.PeakOffMesh, tally.Count(StrandClass.OffMesh));
        state.PeakConfirmed = Math.Max(state.PeakConfirmed, tally.Confirmed);
        state.PeakWaterStill = Math.Max(state.PeakWaterStill, tally.Count(StrandClass.WaterStill));
        state.PeakLandBelow = Math.Max(state.PeakLandBelow, tally.Count(StrandClass.LandBelowWater));
        return tally;
    }

    /// <summary>Up to <paramref name="max"/> agents of a class, the longest stranded first, then by position so the order is stable.</summary>
    public static List<StrandEntry> Samples(LostFootingMissionState state, StrandClass c, int max)
    {
        if (max <= 0) return new List<StrandEntry>();
        return state.Entries.Values
            .Where(e => e.InClass(c))
            .OrderBy(e => e.StrandedSince)
            .ThenBy(e => e.X)
            .ThenBy(e => e.Y)
            .Take(max)
            .ToList();
    }

    /// <summary>The squares with the most agent-seconds, most first, ties in cell order.</summary>
    public static List<HotCell> Hotspots(LostFootingMissionState state, int count)
    {
        return state.Hotspots
            .Select(pair => new HotCell { CellX = pair.Key.X, CellY = pair.Key.Y, AgentSeconds = pair.Value })
            .OrderByDescending(c => c.AgentSeconds)
            .ThenBy(c => c.CellX)
            .ThenBy(c => c.CellY)
            .Take(count)
            .ToList();
    }
}
