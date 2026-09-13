namespace TAOM.Features.BattleLoadDiagnostics.Domain;

/// <summary>
/// One <c>Utilities.GetGPUMemoryStats</c> reading: the engine's GPU memory split into render targets,
/// depth targets, shader resource views (textures) and buffers (geometry). Values are passed through
/// in whatever unit the engine uses: no managed caller in the shipping or editor build formats them,
/// so the unit is calibrated from the first live reading against VMMap rather than assumed here.
/// </summary>
public readonly struct GpuMemorySplit
{
    public GpuMemorySplit(float total, float renderTarget, float depthTarget, float srv, float buffer)
    {
        Total = total;
        RenderTarget = renderTarget;
        DepthTarget = depthTarget;
        Srv = srv;
        Buffer = buffer;
    }

    public float Total { get; }
    public float RenderTarget { get; }
    public float DepthTarget { get; }
    public float Srv { get; }
    public float Buffer { get; }
}
