using System;
using System.Collections.Generic;
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
/// plus <c>NoDepthTest</c> when borders draw through hills, each triangle added in both windings, and
/// the fade applied with <c>GameEntity.SetAlpha</c>. That mod drew one colour per triangle, so whether
/// the material blends TAOM's per-vertex alpha (the watercolour fade) is for the in-game look session.
/// Unlike that mod it builds tens of tile meshes, not one entity per strip, and touches alpha only when
/// it changes. Vertex heights come from the terrain, one query per distinct vertex, kept across
/// repaints on the same scene. When the map scene changes (a save load) the old entities die with
/// their scene and are forgotten, never removed.
/// </summary>
public sealed class BorderRenderAdapter : IBorderRenderAdapter
{
    /// <summary>
    /// Tried in order: the Kingdom Borders mod's material, then the vertex-colour material v1.5.3's
    /// MapScreen itself names. Both names are in Native's core material packages; which one resolves
    /// is decided at run time and logged, and <c>taom.realm_borders_material</c> tries another.
    /// </summary>
    public static readonly string[] CandidateMaterials = { "vertex_color_mat", "vertex_color_lighting" };

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
    private Scene? _scene;
    private Material? _overlay;
    private Material? _grounded;
    private bool _materialMissingLogged;
    private bool _removeFailureLogged;
    private string? _materialOverride;
    private Material.MBAlphaBlendMode? _blendOverride;
    private float _alpha = 1f;

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
                AddBothWindings(mesh, a, b, c, quad.NearStart, quad.FarStart, quad.FarEnd, handle);
                AddBothWindings(mesh, a, c, d, quad.NearStart, quad.FarEnd, quad.NearEnd, handle);
            }
        }
        finally
        {
            mesh.UnlockEditDataWrite(handle);
        }
        mesh.ComputeNormals();
        mesh.RecomputeBoundingBox();

        var entity = GameEntity.CreateEmpty(_scene, false, true, true);
        if (entity == null)
            return;
        var frame = MatrixFrame.Identity;
        entity.SetGlobalFrame(in frame);
        entity.AddMesh(mesh);
        entity.SetAlpha(_alpha);
        entity.SetVisibilityExcludeParents(_alpha > 0f);
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
        _heights.Clear();
        _overlay = _grounded = null;
        _scene = null;
    }

    private static Scene? CurrentScene() => (Campaign.Current?.MapSceneWrapper as MapScene)?.Scene;

    /// <summary>A new map scene means a new session: the old entities went with the old scene.</summary>
    private void TrackScene()
    {
        var scene = CurrentScene();
        if (ReferenceEquals(scene, _scene))
            return;
        _tiles.Clear();
        _heights.Clear();
        _overlay = _grounded = null;
        _scene = scene;
    }

    private Material? MaterialFor(bool drawThroughTerrain)
    {
        if (drawThroughTerrain)
            return _overlay ??= BuildMaterial(MaterialFlags.NoModifyDepthBuffer | MaterialFlags.NoDepthTest);
        return _grounded ??= BuildMaterial(MaterialFlags.NoModifyDepthBuffer);
    }

    private Material? BuildMaterial(MaterialFlags extra)
    {
        var names = _materialOverride != null ? new[] { _materialOverride } : CandidateMaterials;
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

    private static void AddBothWindings(Mesh mesh, Vec3 p1, Vec3 p2, Vec3 p3, BorderVertex v1, BorderVertex v2, BorderVertex v3, UIntPtr handle)
    {
        var uv1 = new Vec2(v1.U, v1.V);
        var uv2 = new Vec2(v2.U, v2.V);
        var uv3 = new Vec2(v3.U, v3.V);
        mesh.AddTriangleWithVertexColors(p1, p2, p3, uv1, uv2, uv3, v1.Colour, v2.Colour, v3.Colour, handle);
        mesh.AddTriangleWithVertexColors(p3, p2, p1, uv3, uv2, uv1, v3.Colour, v2.Colour, v1.Colour, handle);
    }

    /// <summary>Removes one tile's entity; a failure is logged once, since the next removal would say the same.</summary>
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
            _logger.LogWarning($"[RealmBorders] a border tile could not be removed ({ex.GetType().Name}: {ex.Message}); it goes with its scene");
        }
    }
}
