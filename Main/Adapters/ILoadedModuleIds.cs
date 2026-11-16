using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>The ids of the modules this game process loaded (the launcher's ticked list, as the engine took it).</summary>
public interface ILoadedModuleIds
{
    IReadOnlyList<string> Get();
}
