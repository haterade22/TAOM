using TaleWorlds.CampaignSystem;

namespace TAOM.Features.StoryMissions.Hooks;

/// <summary>
/// Writes the story roster check (#1366) when a campaign session starts. The engine runs <c>OnSessionStart</c> for a new game and for a loaded save
/// (Campaign.cs:1685-1686 and :1709-1710, v1.5.7), and it invokes <c>OnSessionLaunchedEvent</c> (CampaignEvents.cs:2088-2091). Holds no saved data.
/// </summary>
public sealed class StoryRosterBehavior : CampaignBehaviorBase
{
    private readonly StoryRosterService _roster;

    internal StoryRosterBehavior(StoryRosterService roster) => _roster = roster;

    public override void RegisterEvents() => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _roster.Report());

    public override void SyncData(IDataStore dataStore) { }
}
