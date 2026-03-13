using TAOM.Features.InitialChildGeneration.Config;

namespace TAOM.Features.InitialChildGeneration;

public interface IInitialChildGenerationConfigProvider
{
    InitialChildGenerationConfig LoadConfig();
}
