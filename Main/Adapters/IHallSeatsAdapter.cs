using System.Collections.Generic;
using TAOM.Features.HallSeats.Domain;

namespace TAOM.Adapters;

/// <summary>What the lord's hall looks like when its characters are about to spawn (Hall Seats, docs/features/hall-seats.md).</summary>
public sealed class HallSeatSnapshot
{
    public HallSeatSnapshot(string sceneName, IReadOnlyList<HallSeatCandidate> lords, int freeLordSpots, int spotsTakenByOthers)
    {
        SceneName = sceneName ?? string.Empty;
        Lords = lords;
        FreeLordSpots = freeLordSpots;
        SpotsTakenByOthers = spotsTakenByOthers;
    }

    public string SceneName { get; }

    /// <summary>Lords in the hall's character list that are not spawned yet and are not pinned to a tag (the ruler's throne).</summary>
    public IReadOnlyList<HallSeatCandidate> Lords { get; }

    /// <summary>Free <c>sp_notable</c>, <c>npc_common_limited</c> and <c>npc_common</c> spots: the ladder vanilla walks for a lord.</summary>
    public int FreeLordSpots { get; }

    /// <summary>Other characters that will land on the same spots (no tag, one of those tags, or a tag the scene lacks).</summary>
    public int SpotsTakenByOthers { get; }
}

/// <summary>Reads the lord's hall mission for <see cref="Features.HallSeats.HallSeatsService"/>. Main thread, inside the spawn pass.</summary>
public interface IHallSeatsAdapter
{
    /// <summary>The hall about to spawn its characters, or null when the mission is not a campaign lord's hall.</summary>
    HallSeatSnapshot? ReadLordsHall(object missionAgentHandler);
}
