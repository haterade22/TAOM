using TaleWorlds.Engine;

namespace TAOM.Adapters;

/// <summary><see cref="IShaderCompileQueueAdapter"/> over <c>Utilities.GetNumberOfShaderCompilationsInProgress</c>.</summary>
public sealed class ShaderCompileQueueAdapter : IShaderCompileQueueAdapter
{
    public int QueuedShaders()
    {
        try
        {
            return Utilities.GetNumberOfShaderCompilationsInProgress();
        }
        catch
        {
            return -1;
        }
    }
}
