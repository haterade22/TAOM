using System.Collections.Generic;

namespace TAOM.Features.ShaderPrecompilation;

public interface IShaderPrecompilationService
{
    IReadOnlyList<string> GetCharacterIdsForShaderBattle();
    IReadOnlyList<string> GetCultureIdsForShaderBattle();
}
