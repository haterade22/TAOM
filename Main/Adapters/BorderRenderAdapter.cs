using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IBorderRenderAdapter"/> on the campaign map scene (v1.5.3): one runtime mesh and one
/// empty entity per map tile. The drawing recipe was learned from the Kingdom Borders mod (see the
/// provenance register): a copy of an engine vertex-colour material with <c>NoModifyDepthBuffer</c>,
/// plus <c>NoDepthTest</c> when borders draw through hills, and the fade applied with
/// <c>GameEntity.SetAlpha</c>. A material that culls back faces gets every triangle in both windings; a
/// two-sided one shows one winding from both sides, and a second would blend every pixel twice. Every
/// border material draws in the late pass after post effects, so the borders stay over the parchment map
/// whatever MCM's Border Material says. Unlike that mod it builds tens of tile meshes, not one entity per
/// strip, and touches alpha only when it changes. Vertex heights come from the terrain, one query per
/// distinct vertex, kept across repaints on the same scene. When the map scene changes (a save load) the
/// old entities die with their scene and are forgotten, never removed.
/// <para>
/// The parchment map is built the same way, from quads whose corners carry the picture's texture
/// coordinates and the ink colour, in two layers per tile: an ink underlay in its corners' colour, and over
/// it the picture on a copy of Native's banner-icon material, which draws the picture's brightness as
/// opacity in the mesh's factor colour (<c>Mesh.Color</c>) and ignores vertex colours, so the ink shows
/// through the dark of the picture. Both layers draw with no depth test in the late pass, over land, water,
/// smoke and models, at render orders just under the borders' engine default. Their meshes are two-sided
/// through their culling mode, as vanilla's boundary wall is. What the look session found in game is in
/// docs/features/realm-borders.md, "Parchment map".
/// </para>
/// </summary>
public sealed class BorderRenderAdapter : IBorderRenderAdapter
{
    /// <summary>The engine's default mesh render order (set by v1.5.3's native mesh constructor); the borders keep it.</summary>
    internal const int EngineDefaultRenderOrder = 128;

    /// <summary>The parchment map's render orders, just under the borders': the ink underlay, then the picture.</summary>
    internal const int SheetInkRenderOrder = 126, SheetPaperRenderOrder = 127;

    /// <summary>
    /// The picture's material. Of six tried in game (2026-10-01), the only one that draws a texture set on a
    /// copy, as the picture's brightness for opacity in the mesh's factor colour: show_texture_2d,
    /// editmode_icons and editor_map_border drew nothing on the map, and default_alpha and
    /// vertex_color_blend_mat drew the sheet plain, ignoring the picture.
    /// </summary>
    public const string SheetMaterialName = "custom_banner_icons_09";

    /// <summary>The ink underlay's material: Native's normally blended late-pass vertex-colour material.</summary>
    public const string SheetUnderlayMaterial = "vertex_color_blend_after_postfx_mat";

    /// <summary>
    /// Bit 0x20000000: v1.5.3 names it <c>AlwaysDepthTest</c> in C#, but its native struct member is
    /// <c>render_after_postfx</c> (the enum's <c>CustomEngineStructMemberData</c>), the late pass that
    /// <c>vertex_color_blend_after_postfx_mat</c> draws in.
    /// </summary>
    private const MaterialFlags RenderAfterPostFx = MaterialFlags.AlwaysDepthTest;

    /// <summary>
    /// The picture's extra flags: streaming off, since a file-loaded picture is not in the texture streamer
    /// (with streaming on, a material drew it plain), and the late pass, over the map's water and smoke.
    /// </summary>
    internal const MaterialFlags SheetFlags = MaterialFlags.DisableStreaming | RenderAfterPostFx;

    /// <summary>
    /// What MCM's "Automatic" tries, in order: the normally blended late-pass material first (Mike's pick,
    /// 2026-10-01), so the dark ink shows, then the Kingdom Borders mod's material and the vertex-colour
    /// material v1.5.3's MapScreen names. MCM's Border Material lists the same names in its own order, which
    /// never changes, since MCM stores the selected index.
    /// </summary>
    public static readonly string[] AutomaticMaterials = { "vertex_color_blend_after_postfx_mat", "vertex_color_mat", "vertex_color_lighting" };

    private const int RemoveReason = 0;

    /// <summary>
    /// Terrain heights kept between tiles and repaints, so a vertex shared by neighbouring quads costs
    /// one native query. Each repaint can add new positions, so the cache starts over past this many
    /// (about 10 MB) rather than growing for the whole session.
    /// </summary>
    private const int MaxCachedHeights = 300000;

    private readonly IMapTerrainAdapter _terrain;
    private readonly IModLogger _logger;
    private readonly Dictionary<(int X, int Y), GameEntity> _tiles = new Dictionary<(int X, int Y), GameEntity>();
    private readonly Dictionary<(int, int), float> _heights = new Dictionary<(int, int), float>();
    private readonly List<GameEntity> _sheet = new List<GameEntity>();
    private Scene? _scene;
    private Material? _overlay;
    private Material? _grounded;
    private Material? _sheetUnderlay;
    private bool _materialMissingLogged;
    private bool _removeFailureLogged;
    private string? _materialOverride;
    private Material.MBAlphaBlendMode? _blendOverride;
    private float _alpha = 1f;
    private float _sheetAlpha;
    private Texture? _sheetTexture;
    private string? _sheetTexturePath;

    public BorderRenderAdapter(IMapTerrainAdapter terrain, IModLogger logger)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsAvailable => CurrentScene() != null;

    /// <summary>The material the meshes were built from, or null before the first tile.</summary>
    public string? ActiveMaterial { get; private set; }

    /// <summary>The blend mode the meshes are drawn with, or null before the first tile.</summary>
    public string? ActiveBlendMode { get; private set; }

    /// <summary>Builds every later tile with this blend mode; the caller redraws. False for an unknown name.</summary>
    public bool UseBlendMode(string? name)
    {
        if (name == null)
        {
            _blendOverride = null;
            _overlay = _grounded = null;
            return true;
        }
        if (!TryParseBlendMode(name, out var mode))
            return false;
        _blendOverride = mode;
        _overlay = _grounded = null;
        return true;
    }

    /// <summary>
    /// A blend mode by its exact enum name, any case. Matched against the names, never Enum.TryParse
    /// alone, which would also take a number or a comma list (lessons/testing-qa.md).
    /// </summary>
    internal static bool TryParseBlendMode(string? name, out Material.MBAlphaBlendMode mode)
    {
        mode = default;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        foreach (string known in Enum.GetNames(typeof(Material.MBAlphaBlendMode)))
        {
            if (known == nameof(Material.MBAlphaBlendMode.Total) || !string.Equals(known, name!.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;
            mode = (Material.MBAlphaBlendMode)Enum.Parse(typeof(Material.MBAlphaBlendMode), known);
            return true;
        }
        return false;
    }

    /// <summary>The flags every border material copy adds: no depth writes, the late pass, and no depth test when the borders draw through hills.</summary>
    internal static MaterialFlags BorderFlags(bool drawThroughTerrain) =>
        MaterialFlags.NoModifyDepthBuffer | RenderAfterPostFx | (drawThroughTerrain ? MaterialFlags.NoDepthTest : default(MaterialFlags));

    /// <summary>True when the material culls back faces, so a quad needs its second winding to show from both sides.</summary>
    internal static bool NeedsSecondWinding(MaterialFlags materialFlags) => (materialFlags & MaterialFlags.TwoSided) == 0;

    /// <summary>Builds every later tile from this material; the caller redraws. False when it does not exist.</summary>
    public bool UseMaterial(string? name)
    {
        if (name == null)
        {
            _materialOverride = null;
            _overlay = _grounded = null;
            _materialMissingLogged = false;
            return true;
        }
        if (string.IsNullOrWhiteSpace(name) || Material.GetFromResource(name) == null)
            return false;
        _materialOverride = name;
        _overlay = _grounded = null;
        _materialMissingLogged = false;
        return true;
    }

    public void SetTile((int X, int Y) tile, IReadOnlyList<BorderQuad> quads, float lift, bool drawThroughTerrain)
    {
        TrackScene();
        if (_scene == null)
            return;
        RemoveTile(tile);
        if (quads == null || quads.Count == 0)
            return;
        var material = MaterialFor(drawThroughTerrain);
        if (material == null)
            return;
        var entity = AddEntity(BuildMesh(quads, lift, material, NeedsSecondWinding(material.Flags)), _alpha);
        if (entity != null)
            _tiles[tile] = entity;
    }

    public void RemoveTile((int X, int Y) tile)
    {
        if (!_tiles.TryGetValue(tile, out var entity))
            return;
        _tiles.Remove(tile);
        if (ReferenceEquals(CurrentScene(), _scene))
            SafeRemove(entity);
    }

    public void SetAlpha(float alpha)
    {
        _alpha = Math.Max(0f, Math.Min(1f, alpha));
        TrackScene();
        foreach (var entity in _tiles.Values)
        {
            entity.SetAlpha(_alpha);
            entity.SetVisibilityExcludeParents(_alpha > 0f);
        }
    }

    public void Clear()
    {
        if (ReferenceEquals(CurrentScene(), _scene))
        {
            foreach (var entity in _tiles.Values)
                SafeRemove(entity);
        }
        _tiles.Clear();
    }

    public void Release()
    {
        Clear();
        RemoveSheet();
        _heights.Clear();
        _overlay = _grounded = null;
        _sheetUnderlay = null;
        _scene = null;
    }

    public bool HasSheet => _sheet.Count > 0 && ReferenceEquals(CurrentScene(), _scene);

    public string? SheetMaterial { get; private set; }

    public string SheetTextureNote => _sheetTexture != null
        ? $"picture {System.IO.Path.GetFileName(_sheetTexturePath)} {_sheetTexture.Width}x{_sheetTexture.Height} (ready {_sheetTexture.IsLoaded()})"
        : "picture not loaded";

    public bool SetSheet(IReadOnlyList<IReadOnlyList<BorderQuad>> tiles, string texturePath, uint paperColour)
    {
        TrackScene();
        RemoveSheet();
        if (_scene == null)
        {
            _logger.LogWarning("[RealmBorders] there is no map scene to draw the parchment map into");
            return false;
        }
        if (tiles == null || tiles.Count == 0)
            return false;
        var texture = SheetTexture(texturePath);
        if (texture == null)
            return false;
        var material = BuildSheetMaterial(texture);
        if (material == null)
            return false;
        var underlay = _sheetUnderlay ??= BuildUnderlayMaterial();
        foreach (var quads in tiles)
        {
            if (quads == null || quads.Count == 0)
                continue;
            var paper = BuildMesh(quads, 0f, material, bothWindings: false);
            paper.SetMeshRenderOrder(SheetPaperRenderOrder);
            paper.CullingMode = MBMeshCullingMode.None;
            Mesh? ink = null;
            if (underlay != null)
            {
                // The same draped geometry, copied before the paper takes its factor colour.
                ink = paper.CreateCopy();
                ink.SetMaterial(underlay);
                ink.SetMeshRenderOrder(SheetInkRenderOrder);
                ink.CullingMode = MBMeshCullingMode.None;
            }
            // The banner material draws in the factor colour. Set it before the entity's SetAlpha, which
            // writes the alpha of the same native colour.
            paper.Color = paperColour;
            var entity = AddEntity(paper, _sheetAlpha, ink);
            if (entity == null)
            {
                _logger.LogError("[RealmBorders] the engine created no entity for a parchment map tile; the map is not drawn");
                RemoveSheet();
                return false;
            }
            _sheet.Add(entity);
        }
        return _sheet.Count > 0;
    }

    public void SetSheetAlpha(float alpha)
    {
        _sheetAlpha = Math.Max(0f, Math.Min(1f, alpha));
        TrackScene();
        foreach (var entity in _sheet)
        {
            entity.SetAlpha(_sheetAlpha);
            entity.SetVisibilityExcludeParents(_sheetAlpha > 0f);
        }
    }

    public void RemoveSheet()
    {
        if (ReferenceEquals(CurrentScene(), _scene))
        {
            foreach (var entity in _sheet)
                SafeRemove(entity);
        }
        _sheet.Clear();
    }

    /// <summary>The parchment picture, loaded once per process the way FactionMap loads its module images; null, logged, when it cannot be.</summary>
    private Texture? SheetTexture(string path)
    {
        if (_sheetTexture != null && string.Equals(_sheetTexturePath, path, StringComparison.OrdinalIgnoreCase))
            return _sheetTexture;
        try
        {
            if (!File.Exists(path))
            {
                _logger.LogError($"[RealmBorders] the parchment picture is not at {path}");
                return null;
            }
            var watch = Stopwatch.StartNew();
            var texture = Texture.LoadTextureFromPath(System.IO.Path.GetFileName(path), System.IO.Path.GetDirectoryName(path));
            if (texture == null)
            {
                _logger.LogError($"[RealmBorders] the engine could not load the parchment picture {path}");
                return null;
            }
            // A file-loaded picture is not in the texture streamer: mark it always valid, then load it now, in
            // vanilla's order (TwoDimensionEngineResourceContext), or a 3D material binds a default instead.
            texture.SetTextureAsAlwaysValid();
            texture.PreloadTexture(true);
            _sheetTexture = texture;
            _sheetTexturePath = path;
            _logger.LogInfo($"[RealmBorders] parchment picture loaded in {watch.Elapsed.TotalMilliseconds:0} ms: "
                + $"{System.IO.Path.GetFileName(path)} {texture.Width}x{texture.Height}, memory {texture.MemorySize}, ready {texture.IsLoaded()}");
            return texture;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RealmBorders] loading the parchment picture {path} failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The ink underlay's material, or null, logged, when it is missing: the sheet then shows the map through its ink.</summary>
    private Material? BuildUnderlayMaterial()
    {
        var source = Material.GetFromResource(SheetUnderlayMaterial);
        if (source == null)
        {
            _logger.LogWarning($"[RealmBorders] the parchment map's ink material '{SheetUnderlayMaterial}' is missing; the map shows through the ink");
            return null;
        }
        var copy = source.CreateCopy();
        copy.Flags |= MaterialFlags.NoModifyDepthBuffer | MaterialFlags.NoDepthTest | RenderAfterPostFx;
        return copy;
    }

    private Material? BuildSheetMaterial(Texture texture)
    {
        var source = Material.GetFromResource(SheetMaterialName);
        if (source == null)
        {
            _logger.LogError($"[RealmBorders] the material '{SheetMaterialName}' is missing; the parchment map cannot be drawn");
            return null;
        }
        var copy = source.CreateCopy();
        copy.Flags |= MaterialFlags.NoModifyDepthBuffer | MaterialFlags.NoDepthTest | SheetFlags;
        copy.SetAlphaBlendMode(Material.MBAlphaBlendMode.Modulate);
        copy.SetTexture(Material.MBTextureType.DiffuseMap, texture);
        SheetMaterial = SheetMaterialName;
        _logger.LogInfo($"[RealmBorders] parchment map drawn from '{SheetMaterialName}' (its own blend {source.GetAlphaBlendMode()}, drawn Modulate, flags {copy.Flags})");
        return copy;
    }

    private static Scene? CurrentScene() => (Campaign.Current?.MapSceneWrapper as MapScene)?.Scene;

    /// <summary>A new map scene means a new session: the old entities went with the old scene.</summary>
    private void TrackScene()
    {
        var scene = CurrentScene();
        if (ReferenceEquals(scene, _scene))
            return;
        _tiles.Clear();
        _sheet.Clear();
        _heights.Clear();
        _overlay = _grounded = null;
        _sheetUnderlay = null;
        _scene = scene;
    }

    private Material? MaterialFor(bool drawThroughTerrain)
    {
        if (drawThroughTerrain)
            return _overlay ??= BuildMaterial(BorderFlags(true));
        return _grounded ??= BuildMaterial(BorderFlags(false));
    }

    private Material? BuildMaterial(MaterialFlags extra)
    {
        var names = _materialOverride != null ? new[] { _materialOverride } : AutomaticMaterials;
        foreach (var name in names)
        {
            var source = Material.GetFromResource(name);
            if (source == null)
                continue;
            var copy = source.CreateCopy();
            copy.Flags |= extra;
            if (_blendOverride.HasValue)
                copy.SetAlphaBlendMode(_blendOverride.Value);
            string blend = copy.GetAlphaBlendMode().ToString();
            if (ActiveMaterial != name || ActiveBlendMode != blend)
                _logger.LogInfo($"[RealmBorders] drawing with material '{name}' (its own blend {source.GetAlphaBlendMode()}, drawn {blend}, flags {copy.Flags})");
            ActiveMaterial = name;
            ActiveBlendMode = blend;
            return copy;
        }
        if (!_materialMissingLogged)
        {
            _materialMissingLogged = true;
            _logger.LogError($"[RealmBorders] none of the materials {string.Join(", ", names)} exist; borders cannot be drawn. "
                + "Try another with taom.realm_borders_material <name>.");
        }
        return null;
    }

    private Vec3 Lifted(BorderVertex vertex, float lift)
    {
        var key = ((int)Math.Round(vertex.Position.X * 20f), (int)Math.Round(vertex.Position.Y * 20f));
        if (!_heights.TryGetValue(key, out float height))
        {
            height = _terrain.HeightAt(vertex.Position.X, vertex.Position.Y, 0f);
            if (float.IsNaN(height) || float.IsInfinity(height))
                height = 0f;
            _heights[key] = height;
        }
        return new Vec3(vertex.Position.X, vertex.Position.Y, height + lift);
    }

    /// <summary>One mesh from quads draped on the terrain, each quad two triangles, in both windings when asked.</summary>
    private Mesh BuildMesh(IReadOnlyList<BorderQuad> quads, float lift, Material material, bool bothWindings)
    {
        if (_heights.Count > MaxCachedHeights)
            _heights.Clear();
        var mesh = Mesh.CreateMesh(true);
        mesh.SetMaterial(material);
        UIntPtr handle = mesh.LockEditDataWrite();
        try
        {
            foreach (var quad in quads)
            {
                var a = Lifted(quad.NearStart, lift);
                var b = Lifted(quad.FarStart, lift);
                var c = Lifted(quad.FarEnd, lift);
                var d = Lifted(quad.NearEnd, lift);
                AddTriangle(mesh, a, b, c, quad.NearStart, quad.FarStart, quad.FarEnd, bothWindings, handle);
                AddTriangle(mesh, a, c, d, quad.NearStart, quad.FarEnd, quad.NearEnd, bothWindings, handle);
            }
        }
        finally
        {
            mesh.UnlockEditDataWrite(handle);
        }
        mesh.ComputeNormals();
        mesh.RecomputeBoundingBox();
        return mesh;
    }

    private GameEntity? AddEntity(Mesh mesh, float alpha, Mesh? under = null)
    {
        var entity = GameEntity.CreateEmpty(_scene, false, true, true);
        if (entity == null)
            return null;
        var frame = MatrixFrame.Identity;
        entity.SetGlobalFrame(in frame);
        if (under != null)
            entity.AddMesh(under);
        entity.AddMesh(mesh);
        entity.SetAlpha(alpha);
        entity.SetVisibilityExcludeParents(alpha > 0f);
        return entity;
    }

    private static void AddTriangle(Mesh mesh, Vec3 p1, Vec3 p2, Vec3 p3, BorderVertex v1, BorderVertex v2, BorderVertex v3, bool bothWindings, UIntPtr handle)
    {
        var uv1 = new Vec2(v1.U, v1.V);
        var uv2 = new Vec2(v2.U, v2.V);
        var uv3 = new Vec2(v3.U, v3.V);
        mesh.AddTriangleWithVertexColors(p1, p2, p3, uv1, uv2, uv3, v1.Colour, v2.Colour, v3.Colour, handle);
        if (bothWindings)
            mesh.AddTriangleWithVertexColors(p3, p2, p1, uv3, uv2, uv1, v3.Colour, v2.Colour, v1.Colour, handle);
    }

    /// <summary>Removes one entity; a failure is logged once, since the next removal would say the same.</summary>
    private void SafeRemove(GameEntity entity)
    {
        try
        {
            entity?.Remove(RemoveReason);
        }
        catch (Exception ex)
        {
            if (_removeFailureLogged)
                return;
            _removeFailureLogged = true;
            _logger.LogWarning($"[RealmBorders] a map mesh could not be removed ({ex.GetType().Name}: {ex.Message}); it goes with its scene");
        }
    }
}
