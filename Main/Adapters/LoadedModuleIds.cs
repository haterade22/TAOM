using System.Collections.Generic;
using System.Linq;
using TaleWorlds.ModuleManager;

namespace TAOM.Adapters;

/// <summary>
/// v1.5.7 ModuleHelper.GetModules() walks _loadedModules, which InitializeModules fills from the
/// module list the game was started with (Utilities.GetModulesNames()), one entry per Id.
/// </summary>
public sealed class LoadedModuleIds : ILoadedModuleIds
{
    public IReadOnlyList<string> Get() => ModuleHelper.GetModules().Select(m => m.Id).ToList();
}
