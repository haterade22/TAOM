using TAOM.Features.Execution;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.StoryMissions;

/// <summary>
/// An open story window with the sides this player may take (#1326), and his side of the war as it was resolved, for the check line.
/// </summary>
internal sealed record StoryOffer(ChronicleStoryOffer Window, StoryChoices Choices, FactionSide PlayerSide)
{
    public string EventId => Window.EventId;
}
