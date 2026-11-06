using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Client.Adapters;

/// <summary>The themed campaign and encyclopedia screens behind Patch95's movie hook (ADR-013): forwards to <see cref="CampaignPresentation"/>.</summary>
internal sealed class CampaignPresentationAdapter : ICampaignPresentationAdapter
{
    public object? Bind(string movie, object? source) => CampaignPresentation.Bind(movie, source as ViewModel);

    public void OnLoaded(object movie, object? source)
    {
        if (movie is GauntletMovieIdentifier loaded)
            CampaignPresentation.OnLoaded(loaded, source as ViewModel);
    }
}
