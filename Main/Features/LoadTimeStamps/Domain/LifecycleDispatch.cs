namespace TAOM.Features.LoadTimeStamps.Domain;

/// <summary>The five CampaignEventDispatcher lifecycle methods the [Lifecycle] stamps time.</summary>
public enum LifecycleDispatch
{
    OnNewGameCreated,
    OnGameEarlyLoaded,
    OnGameLoaded,
    OnSessionStart,
    OnAfterSessionStart,
}
