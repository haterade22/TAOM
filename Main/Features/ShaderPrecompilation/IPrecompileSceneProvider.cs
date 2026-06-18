using System.Collections.Generic;

namespace TAOM.Features.ShaderPrecompilation;

// Supplies the battle scenes whose terrain + forced-atmosphere shaders the walk should compile.
public interface IPrecompileSceneProvider
{
    IReadOnlyList<string> GetScenes();
}
