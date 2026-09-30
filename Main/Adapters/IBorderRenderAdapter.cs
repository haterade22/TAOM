using System.Collections.Generic;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Draws the painted borders on the campaign map scene, one mesh per map tile. Main-thread only. A
/// tile is replaced whole, so a capture rebuilds only the tiles whose quads changed.
/// </summary>
public interface IBorderRenderAdapter
{
    /// <summary>True while a campaign map scene exists to draw into.</summary>
    bool IsAvailable { get; }

    /// <summary>The material the meshes are built from, or null before the first tile.</summary>
    string? ActiveMaterial { get; }

    /// <summary>Builds later tiles from this material; false when it does not exist. The caller redraws.</summary>
    bool UseMaterial(string name);

    /// <summary>
    /// Replaces the tile's mesh with these quads, lifted onto the terrain by <paramref name="lift"/>.
    /// A new tile takes the opacity last given to <see cref="SetAlpha"/>.
    /// </summary>
    void SetTile((int X, int Y) tile, IReadOnlyList<BorderQuad> quads, float lift, bool drawThroughTerrain);

    void RemoveTile((int X, int Y) tile);

    /// <summary>The opacity of every border mesh, 0 to 1.</summary>
    void SetAlpha(float alpha);

    /// <summary>Removes every border mesh; the scene and its terrain heights stay known.</summary>
    void Clear();

    /// <summary>The map screen is closing: removes every mesh and lets go of the scene and its materials.</summary>
    void Release();
}
