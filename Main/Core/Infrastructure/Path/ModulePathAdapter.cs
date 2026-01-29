using TaleWorlds.ModuleManager;

namespace TAOM.Core.Infrastructure;

public class ModulePathAdapter : IModulePathAdapter
{
    public string GetModuleFullPath(string moduleName)
    {
        return ModuleHelper.GetModuleFullPath(moduleName);
    }
}
