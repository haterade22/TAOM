using System;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.LifeDeathOption;

public sealed class LifeDeathOptionService
{
    public const string OptionId = "IsLifeDeathCycleEnabled";
    public const string OfficialModuleId = "BirthAndDeath";
    public const int PriorityIndex = 890;

    private readonly ILoadedModuleIds _loadedModules;

    public LifeDeathOptionService(ILoadedModuleIds loadedModules)
    {
        _loadedModules = loadedModules;
    }

    public bool ShouldOffer() =>
        !_loadedModules.Get().Any(id => string.Equals(id, OfficialModuleId, StringComparison.OrdinalIgnoreCase));
}
