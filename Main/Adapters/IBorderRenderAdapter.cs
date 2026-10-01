using System.Collections.Generic;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Draws the painted borders on the campaign map scene, one mesh per map tile, and the parchment map
/// under them. Main-thread only. A tile is replaced whole, so a capture rebuilds only the tiles whose
/// quads changed.
/// </summary>
public interface IBorderRenderAdapter
{
    /// <summary>True while a campaign map scene exists to draw into.</summary>
    bool IsAvailable { get; }

    /// <summary>The material the meshes are built from, or null before the first tile.</summary>
    string? ActiveMaterial { get; }

    /// <summary>
    /// Builds later tiles from this material, or for null the first of
    /// <see cref="BorderRenderAdapter.AutomaticMaterials"/> that exists; false when it does not exist.
    /// The caller redraws.
    /// </summary>
    bool UseMaterial(string? name);

    /// <summary>The blend mode the meshes are drawn with, or null before the first tile.</summary>
    string? ActiveBlendMode { get; }

    /// <summary>Builds later tiles with this engine blend mode, by name, or the material's own for null; false for an unknown name. The caller redraws.</summary>
    bool UseBlendMode(string? name);

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

    /// <summary>The map screen is closing: removes every mesh, the sheet included, and lets go of the scene and its materials.</summary>
    void Release();

    /// <summary>True while the parchment map is built in the current map scene.</summary>
    bool HasSheet { get; }

    /// <summary>The material the parchment map is drawn from, or null before it is built.</summary>
    string? SheetMaterial { get; }

    /// <summary>The parchment map's picture: its file and size once loaded, or that it is not.</summary>
    string SheetTextureNote { get; }

    /// <summary>
    /// Builds the parchment map from these tiles of quads over the whole map, under the borders, in two
    /// layers: an underlay in each corner's colour (the ink), and over it the picture at
    /// <paramref name="texturePath"/>, which the banner material draws in <paramref name="paperColour"/>
    /// where the picture is bright and see-through where it is dark, so the ink shows there. It starts at
    /// the opacity last given to <see cref="SetSheetAlpha"/>. False, logged, when there is no map scene or
    /// the picture or its material is missing.
    /// </summary>
    bool SetSheet(IReadOnlyList<IReadOnlyList<BorderQuad>> tiles, string texturePath, uint paperColour);

    /// <summary>The parchment map's opacity, 0 to 1; 0 hides it.</summary>
    void SetSheetAlpha(float alpha);

    /// <summary>Removes the parchment map; the next <see cref="SetSheet"/> builds it again.</summary>
    void RemoveSheet();
}
