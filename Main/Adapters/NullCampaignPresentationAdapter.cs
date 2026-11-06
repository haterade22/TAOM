namespace TAOM.Adapters;

/// <summary>No themed campaign screens (a dedicated server, or a client where TAOM.Client did not load): every movie keeps its own data source.</summary>
public sealed class NullCampaignPresentationAdapter : ICampaignPresentationAdapter
{
    public object? Bind(string movie, object? source) => source;

    public void OnLoaded(object movie, object? source)
    {
    }
}
