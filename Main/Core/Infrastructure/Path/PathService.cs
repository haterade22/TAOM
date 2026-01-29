namespace TAOM.Core.Infrastructure;

public class PathService : IPathService
{
    private readonly IModulePathAdapter _modulePathAdapter;

    public PathService(IModulePathAdapter modulePathAdapter)
    {
        _modulePathAdapter = modulePathAdapter;
    }

    public string ModuleRootPath => _modulePathAdapter.GetModuleFullPath("TAOM");

    public string ModuleDataPath => ModuleRootPath + "ModuleData/";

    public string ConfigPath => ModuleDataPath + "configs/";
}
