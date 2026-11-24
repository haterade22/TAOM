namespace TAOM.Adapters;

/// <summary>The engine's count of shaders queued for compilation (one native field read).</summary>
public interface IShaderCompileQueueAdapter
{
    /// <summary>The queued count, or -1 when the engine could not be read.</summary>
    int QueuedShaders();
}
