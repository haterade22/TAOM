using TaleWorlds.Core;

namespace TAOM.Features.SpecialResources.Models;

public class TaomSpecialResourceModel : GameModel
{
    private readonly ISpecialResourceService _service;

    public TaomSpecialResourceModel(ISpecialResourceService service)
    {
        _service = service;
    }

    public float GetCurrentResource(string heroId) => _service.GetCurrentAmount(heroId);

    public bool CanAffordUpgrade(string heroId, string troopId, int count)
        => _service.CanAffordUpgrade(heroId, troopId, count);
}
