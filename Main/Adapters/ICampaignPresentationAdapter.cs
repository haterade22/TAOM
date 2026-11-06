namespace TAOM.Adapters;

/// <summary>
/// The themed campaign and encyclopedia screens' hand-off from Patch95's movie hook (ADR-013). The screens' widgets,
/// view models and mixins name client-only assemblies, so the implementation lives in TAOM.Client; core
/// NullCampaignPresentationAdapter leaves every movie as vanilla built it. View models cross as opaque objects (the engine's
/// ViewModel), so the signature names no TaleWorlds class (ADR-007).
/// </summary>
public interface ICampaignPresentationAdapter
{
    /// <summary>The data source the themed <paramref name="movie"/> is built with: <paramref name="source"/>, or a TAOM view model wrapping it.</summary>
    object? Bind(string movie, object? source);

    /// <summary>
    /// Runs after a movie loaded. <paramref name="movie"/> is the engine's GauntletMovieIdentifier, typed object here because its
    /// assembly is client-only.
    /// </summary>
    void OnLoaded(object movie, object? source);
}
