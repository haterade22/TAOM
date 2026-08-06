using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
using TaleWorlds.Engine;

namespace TAOM.Features.HeroRace.Diagnostics;

/// <summary>
/// Reads what a character tableau entity is ACTUALLY about to draw — mesh names, material names,
/// shaders, per-mesh vertex colours and the bound diffuse texture — for issue #389 (Isengard
/// uruk_hai/berserker troops rendering as black silhouettes).
///
/// WHY THIS AND NOT A RESOURCE CHECK. An adversarial review refuted the residency framing: vanilla
/// only makes a visual visible once both loading counters clear, so a character with correct
/// geometry on screen has by definition finished loading. Every remaining hypothesis — black albedo
/// payload, degenerate normals, a wrong shader, a black vertex colour, a lighting failure — produces
/// an identical "resources are resident" answer. Only the CONTENT of the bound material can tell
/// them apart, and nothing in the engine logs it.
///
/// Read this by diffing an affected troop against a working one. `sk_uruk_hai_*` meshes vs
/// `sk_md_orc_*`: if the material name, shader, or diffuse texture differs in kind — or if
/// <c>color</c>/<c>color2</c> read as black on one and not the other — that names the cause.
///
/// Costs nothing in the steady state: it is called once per character, on the frame the visual
/// becomes ready, never per frame.
/// </summary>
public static class TableauRenderCensus
{
    /// <summary>A troop carries roughly 8–14 meta meshes (body, head, 5 armour slots, weapons).</summary>
    private const int MaxMetaMeshes = 14;

    /// <summary>Sub-meshes within one meta mesh: LODs and material splits. The first few carry the signal.</summary>
    private const int MaxSubMeshesPerMetaMesh = 2;

    /// <summary>
    /// Groups the entity's drawn meshes by (material, shader, flags, diffuse texture) and reports each
    /// group with its mesh count and vertex-colour range. Never throws, never returns null.
    ///
    /// Reads the SKELETON, not the entity's MetaMesh components. `AgentVisuals` attaches skin and
    /// armour through <c>GetSkeleton()</c>, so <c>entity.MultiMeshComponentCount</c> is 0 for a
    /// character tableau — verified in-game 2026-08-06, where the first version of this census
    /// reported <c>metaMeshCount=0</c> for every troop including ones that render correctly. The
    /// component walk is retained as a secondary source because weapons/props can live there.
    /// </summary>
    [HandleProcessCorruptedStateExceptions]
    public static IReadOnlyList<string> Describe(GameEntity entity)
    {
        var lines = new List<string>();
        try
        {
            if (entity == null)
            {
                lines.Add("<null-entity>");
                return lines;
            }

            DescribeSkeletonMeshes(entity, lines);

            int metaMeshCount = Count(() => entity.MultiMeshComponentCount);
            lines.Add($"entityMetaMeshComponents={metaMeshCount}");
            int limit = Math.Min(metaMeshCount, MaxMetaMeshes);
            for (int i = 0; i < limit; i++) lines.Add(DescribeMetaMesh(entity, i));
        }
        catch (Exception e)
        {
            lines.Add("<census-failed:" + e.GetType().Name + ">");
        }

        return lines;
    }

    [HandleProcessCorruptedStateExceptions]
    private static void DescribeSkeletonMeshes(GameEntity entity, List<string> lines)
    {
        try
        {
            var skeleton = entity.Skeleton;
            if (skeleton == null)
            {
                lines.Add("skeleton=<null> (no skin/armour meshes to census)");
                return;
            }

            // Key on everything that could make a draw black, so two troops diff cleanly.
            var groups = new Dictionary<string, MeshGroup>(StringComparer.Ordinal);
            int total = 0;

            foreach (var mesh in skeleton.GetAllMeshes())
            {
                total++;
                if (mesh == null) continue;

                string material = "<no-material>", shader = "-", flags = "-", diffuse = "-";
                var mat = mesh.GetMaterial();
                if (mat != null)
                {
                    material = Safe(() => mat.Name);
                    shader = Safe(() => mat.GetShader()?.Name);
                    flags = Safe(() => "0x" + mat.GetShaderFlags().ToString("X"));
                    diffuse = Safe(() => mat.GetTextureWithSlot(0)?.Name);
                }

                string key = material + "|" + shader + "|" + flags + "|" + diffuse;
                if (!groups.TryGetValue(key, out var g))
                {
                    g = new MeshGroup { Material = material, Shader = shader, Flags = flags, Diffuse = diffuse };
                    groups[key] = g;
                }

                g.Count++;
                g.Note(Safe(() => mesh.Name), Safe(() => "0x" + mesh.Color.ToString("X8")), Safe(() => "0x" + mesh.Color2.ToString("X8")));
            }

            lines.Add($"skeletonMeshes={total} distinctMaterials={groups.Count}");
            foreach (var g in groups.Values)
            {
                lines.Add(
                    $"  x{g.Count,-3} mat='{g.Material}' shader='{g.Shader}' flags={g.Flags} diffuse='{g.Diffuse}'");
                lines.Add(
                    $"        colors={g.Colors} example='{g.ExampleMesh}'");
            }
        }
        catch (Exception e)
        {
            lines.Add("skeleton census <threw:" + e.GetType().Name + ">");
        }
    }

    private sealed class MeshGroup
    {
        private readonly HashSet<string> _colors = new(StringComparer.Ordinal);
        public string Material = "-";
        public string Shader = "-";
        public string Flags = "-";
        public string Diffuse = "-";
        public int Count;
        public string ExampleMesh = "-";

        public void Note(string meshName, string color, string color2)
        {
            if (ExampleMesh == "-") ExampleMesh = meshName;
            if (_colors.Count < 4) _colors.Add(color + "/" + color2);
        }

        public string Colors => string.Join(" ", _colors);
    }

    private static int Count(Func<int> f)
    {
        try { return f(); }
        catch { return -1; }
    }

    [HandleProcessCorruptedStateExceptions]
    private static string DescribeMetaMesh(GameEntity entity, int index)
    {
        try
        {
            var metaMesh = entity.GetMetaMesh(index);
            if (metaMesh == null) return $"  [{index}] <null-metamesh>";

            int meshCount = metaMesh.MeshCount;
            var sb = new StringBuilder();
            sb.Append("  [").Append(index).Append("] subMeshes=").Append(meshCount);

            int limit = Math.Min(meshCount, MaxSubMeshesPerMetaMesh);
            for (int j = 0; j < limit; j++)
            {
                sb.Append(Environment.NewLine).Append("      ").Append(DescribeMesh(metaMesh, j));
            }
            return sb.ToString();
        }
        catch (Exception e)
        {
            return $"  [{index}] <threw:{e.GetType().Name}>";
        }
    }

    [HandleProcessCorruptedStateExceptions]
    private static string DescribeMesh(MetaMesh metaMesh, int meshIndex)
    {
        try
        {
            var mesh = metaMesh.GetMeshAtIndex(meshIndex);
            if (mesh == null) return $"mesh[{meshIndex}] <null>";

            string name = Safe(() => mesh.Name);
            // Vertex colours are what AgentVisuals.AddTeamColorToMesh writes; a black value here on a
            // team-coloured mesh is a direct explanation for a black draw.
            string color = Safe(() => "0x" + mesh.Color.ToString("X8"));
            string color2 = Safe(() => "0x" + mesh.Color2.ToString("X8"));

            string material = "<no-material>";
            string shader = "-";
            string flags = "-";
            string diffuse = "-";

            var mat = mesh.GetMaterial();
            if (mat != null)
            {
                material = Safe(() => mat.Name);
                shader = Safe(() => mat.GetShader()?.Name);
                flags = Safe(() => "0x" + mat.GetShaderFlags().ToString("X"));
                // Slot 0 is DiffuseMap: `Material.GetTexture(MBTextureType)` is itself just
                // `GetTexture(pointer, (int)textureType)` over the enum whose first member is
                // DiffuseMap. The slot overload avoids taking a compile-time dependency on that enum,
                // which is not visible from this project's reference set.
                diffuse = Safe(() => mat.GetTextureWithSlot(0)?.Name);
            }

            return $"mesh[{meshIndex}] '{name}' mat='{material}' shader='{shader}' flags={flags} " +
                   $"color={color} color2={color2} diffuse='{diffuse}'";
        }
        catch (Exception e)
        {
            return $"mesh[{meshIndex}] <threw:{e.GetType().Name}>";
        }
    }

    /// <summary>
    /// These accessors are thin P/Invoke wrappers over native pointers with no managed validation, on
    /// entities that may already be finalised. TAOM's convention for that hazard is an explicit
    /// corrupted-state handler (see <c>CharacterSpawnerService</c>); the launcher config enables
    /// <c>legacyCorruptedStateExceptionsPolicy</c>, so an access violation here is catchable.
    /// </summary>
    [HandleProcessCorruptedStateExceptions]
    private static string Safe(Func<string> f)
    {
        try { return f() ?? "<null>"; }
        catch (Exception e) { return "<threw:" + e.GetType().Name + ">"; }
    }
}
