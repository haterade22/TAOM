using System.Collections.Generic;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The chronicle seam that Story Missions reads and settles (#1105 slice 4): the open story offers, a declined offer, and a won mission.
/// <see cref="ChronicleService"/> implements it; the Story Missions tests fake it.
/// </summary>
public interface IChronicleStoryPort
{
    IReadOnlyList<ChronicleStoryOffer> GetStoryOffers();

    bool DeclineStory(string? eventId);

    bool ResolveStoryWon(string? eventId);

    /// <summary>
    /// Before a lost mission's transfer: reconciles the target's live owner (which can change with no event) and is true only when the
    /// window is still Active and the war has not ended.
    /// </summary>
    bool ConfirmStoryWindow(string? eventId);
}
