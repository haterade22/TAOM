namespace TAOM.Adapters;

/// <summary>
/// A dedicated server, or a client where TAOM.Client did not load: every movie keeps its own data source. It does not stop
/// FrontEndMovieService swapping in a themed prefab; without TAOM.Client that prefab's campaign widgets load as plain
/// widgets, a degraded mode ClientComposition already logs as an error.
/// </summary>
public sealed class NullCampaignPresentationAdapter : ICampaignPresentationAdapter
{
    public object? Bind(string movie, object? source) => source;

    public void OnLoaded(object movie, object? source)
    {
    }
}
