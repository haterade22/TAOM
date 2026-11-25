using System.Collections.Generic;

namespace TAOM.CoopCompat.LoadOrder;

/// <summary>This game's active modules, in load order. The real one reads the engine's module list and each SubModule.xml.</summary>
public interface ILoadOrderAdapter
{
    IReadOnlyList<ModuleEntry> ActiveModules();
}
