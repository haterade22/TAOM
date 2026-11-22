using System.Collections.Generic;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The chronicle seam that Story Missions reads and settles (#1105 slice 4): the open story offers, a declined offer, and a held fortress.
/// <see cref="ChronicleService"/> implements it; the Story Missions tests fake it.
/// </summary>
public interface IChronicleStoryPort
{
    IReadOnlyList<ChronicleStoryOffer> GetStoryOffers();

    bool DeclineStory(string? eventId);

    /// <summary>The walls held in the story mission: resolves the window Held. <paramref name="why"/> is the log's reason.</summary>
    bool ResolveStoryHeld(string? eventId, string why);

    /// <summary>
    /// A player-led attack in the story mission was thrown back (#1105 slice 5): resolves the window Held like <see cref="ResolveStoryHeld"/>,
    /// with the story's <c>repulsed</c> text, and ends the permanent alliance the story names (single-player authority only).
    /// </summary>
    bool ResolveStoryRepulsed(string? eventId, string why);

    /// <summary>
    /// Before the transfer of a fallen fortress: reconciles the target's live owner (which can change with no event) and is true only when the
    /// window is still Active and the war has not ended.
    /// </summary>
    bool ConfirmStoryWindow(string? eventId);

    /// <summary>
    /// The result of a hero story's mission (#1105 slice 3): the hero lives and the event is Averted, or he dies in battle and it Occurred.
    /// False when no Active hero window has this id.
    /// </summary>
    bool ResolveHeroStory(string? eventId, bool heroLives, string why);
}
