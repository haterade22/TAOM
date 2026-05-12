// Behavioral inspiration: Alliance mod's CS_Road mesh-attach pattern (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using System.Collections.Generic;
using TaleWorlds.Engine;

namespace TAOM.SceneScripts.Roads;

public static class RoadMeshAttacher
{
    public const string GeneratedMetaMeshName = "taom_cs_road_generated";

    public static MetaMesh BuildAndAttach(
        WeakGameEntity entity,
        Material material,
        IReadOnlyList<RoadTriangle> triangles,
        uint packedArgbColor,
        MetaMesh? previous)
    {
        var mesh = Mesh.CreateMeshWithMaterial(material);
        var lockHandle = mesh.LockEditDataWrite();
        try
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                mesh.AddTriangle(
                    t.P1, t.P2, t.P3,
                    t.UV1, t.UV2, t.UV3,
                    HexColorParser.OpaqueWhitePackedArgb,
                    lockHandle);
            }
        }
        finally
        {
            mesh.UnlockEditDataWrite(lockHandle);
        }

        mesh.Color = packedArgbColor;
        mesh.RecomputeBoundingBox();

        var metaMesh = MetaMesh.CreateMetaMesh(GeneratedMetaMeshName);
        metaMesh.AddMesh(mesh);

        if (previous != null) entity.RemoveMultiMesh(previous);
        entity.AddMultiMesh(metaMesh);

        return metaMesh;
    }
}
