using System;
using System.Collections.Concurrent;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.MountReins.Hooks;

/// <summary>
/// Patch153's replacement for the one <c>MBAgentVisuals.AddHorseReinsClothMesh</c> call in
/// <c>MountVisualCreator.AddMountMesh</c>. Vanilla calls it for any harness with reins at character detail above 2,
/// whatever the mount; on a Monster without a rein skeleton the cloth has nothing to hang on. The hook makes the call
/// only when <see cref="MountReinsRule"/> allows it. Vanilla still draws the plain rein mesh and the harness, as it does
/// at character detail 2 and below, and invalidates the rope mesh after the call either way.
/// </summary>
public static class MountReinsHook
{
    private static IModLogger? _logger;
    private static readonly ConcurrentDictionary<string, byte> Reported = new(StringComparer.Ordinal);

    internal static void Initialize(IModLogger? logger) => _logger = logger;

    public static void AddReinsCloth(MBAgentVisuals agentVisual, MetaMesh reinMesh, MetaMesh ropeMesh, ItemObject mountItem)
    {
        string? reinSkeleton;
        string monsterId;
        try
        {
            var monster = mountItem?.HorseComponent?.Monster;
            reinSkeleton = monster?.ReinSkeleton;
            monsterId = monster?.StringId ?? "<none>";
        }
        catch (Exception ex)
        {
            // The cloth reins are looks only; skipping them is safe, building them on an unknown mount is not.
            _logger?.LogWarning($"[MountReins] could not read the mount's Monster ({ex.GetType().Name}): cloth reins skipped");
            return;
        }

        if (MountReinsRule.CanBuildReinsCloth(reinSkeleton))
        {
            agentVisual.AddHorseReinsClothMesh(reinMesh, ropeMesh);
            return;
        }

        if (Reported.TryAdd(monsterId, 0))
            _logger?.LogInfo($"[MountReins] mount '{mountItem?.StringId}' (Monster '{monsterId}') has no rein skeleton: cloth reins of its harness skipped (logged once per Monster)");
    }
}
