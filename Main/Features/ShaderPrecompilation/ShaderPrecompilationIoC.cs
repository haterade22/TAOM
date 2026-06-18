using DryIoc;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation.Hooks;

namespace TAOM.Features.ShaderPrecompilation;

public static class ShaderPrecompilationIoC
{
    public static void RegisterShaderPrecompilationFeature(IContainer container)
    {
        container.Register<IShaderPrecompilationService, ShaderPrecompilationService>(Reuse.Singleton);
        container.Register<IPrecompileSceneProvider, PrecompileSceneProvider>(Reuse.Singleton);
        container.Register<IShaderPrecompileCrashGuard, ShaderPrecompileCrashGuard>(Reuse.Singleton);
        container.Register<ShaderPrecompileRunner>(Reuse.Singleton);
    }

    public static void InitializeHooks(IModLogger logger, ShaderPrecompileRunner runner)
    {
        LoadingScreen_ShaderProgress_Patch.Initialize(logger, runner);
    }
}
