namespace TAOM.Features.LoadTimeStamps.Domain;

/// <summary>The CampaignEvents events those dispatches invoke, each a list of campaign handlers.</summary>
public enum LifecycleEvent
{
    OnNewGameCreated,
    OnNewGameCreatedPartialFollowUp,
    OnNewGameCreatedPartialFollowUpEnd,
    OnGameEarlyLoaded,
    OnGameLoaded,
    OnSessionLaunched,
    OnAfterSessionLaunched,
}
