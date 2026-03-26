using TAOM.Features.StartupResources.Config;

namespace TAOM.Features.StartupResources;

public interface IStartupResourcesConfigProvider
{
    StartupResourcesConfig LoadConfig();
}
