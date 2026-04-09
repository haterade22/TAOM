using TaleWorlds.Library;

namespace TAOM.Features.SpecialResources;

internal sealed class SpecialResourceNotifier
{
    private readonly ISpecialResourceService _service;

    public SpecialResourceNotifier(ISpecialResourceService service)
    {
        _service = service;
    }

    public void NotifyEarned(string heroId, string kingdomId, string cultureId, string source)
    {
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var amount = _service.GetCurrentAmount(heroId, kingdomId, cultureId);
        InformationManager.DisplayMessage(new InformationMessage(
            $"{resource.DisplayName} earned from {source} (total: {amount:F0})",
            Colors.Green));
    }

    public void NotifyEarnedDelta(string heroId, string kingdomId, string cultureId, float before, string source)
    {
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var after = _service.GetCurrentAmount(heroId, kingdomId, cultureId);
        var earned = after - before;
        if (earned > 0f)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"+{earned:F0} {resource.DisplayName} from {source}",
                Colors.Green));
        }
    }
}
