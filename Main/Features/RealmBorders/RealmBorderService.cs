using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.RealmBorders.Domain;
using TAOM.Features.SettlementNameplateRelation;
using TAOM.SceneScripts.Roads;

namespace TAOM.Features.RealmBorders;

/// <summary>
/// Keeps the drawn realm borders in step with the campaign. When something may have changed (a
/// capture, a war, an alliance, a setting, the map mode) it snapshots the campaign on the game thread,
/// skips the work when nothing on screen would change, and otherwise paints on the worker: selection,
/// smoothing, painting, tiling and the realm names touch only that snapshot and the finished province
/// map. The game thread adopts the result, uploads only the tiles whose content changed, a few per
/// frame, and fades the borders with the camera distance. Hourly it tells the player when their party
/// has crossed into another realm. Nothing here runs without a map screen.
/// </summary>
public sealed class RealmBorderService
{
    public const float TileSize = 128f;
    public const float Lift = 0.3f;
    public const float ResampleSpacing = 1.2f;
    public const int ChaikinPasses = 4;
    public const double SamplingBudgetMs = 3.0;
    public const double UploadBudgetMs = 4.0;
    public const double CrossingCooldownHours = 12.0;
    public const int MinimumLabelCells = 400;
    public const uint FreePeoplesColour = 0xFFDCE6F2;
    public const uint ShadowColour = 0xFFB0231B;

    /// <summary>The province picture's own colours; RealmPaletteTests keeps every realm colour clear of them.</summary>
    internal const uint PictureUnownedFief = 0xFFFFFFFF, PictureWildLand = 0xFFE6DCC0, PictureWater = 0xFF2A6078, PictureEdge = 0xFF20180F;

    private const string FreeSide = "free";
    private const string ShadowSide = "shadow";

    /// <summary>The war mode's colours are the settlement nameplates' frame colours, so the two agree.</summary>
    internal static readonly IReadOnlyDictionary<string, uint> RelationColours = new Dictionary<string, uint>(StringComparer.Ordinal)
    {
        [RelationGroups.Own] = HexColorParser.ToPackedArgb(NameplateRelationPalette.SameFactionFrame),
        [RelationGroups.Ally] = HexColorParser.ToPackedArgb(NameplateRelationPalette.AllyFrame),
        [RelationGroups.Enemy] = HexColorParser.ToPackedArgb(NameplateRelationPalette.EnemyFrame),
        [RelationGroups.Neutral] = HexColorParser.ToPackedArgb(NameplateRelationPalette.NeutralFrame),
    };

    private readonly RealmTerritoryService _territory;
    private readonly IRealmMapAdapter _map;
    private readonly IBorderRenderAdapter _renderer;
    private readonly IRealmBordersSettings _settings;
    private readonly RealmPaletteProvider _palettes;
    private readonly IAlignmentService _alignment;
    private readonly IRealmNoticeAdapter _notices;
    private readonly ITerritoryWorker _worker;
    private readonly IModLogger _logger;
    private readonly BorderCrossingTracker _crossings = new BorderCrossingTracker(CrossingCooldownHours);
    private readonly List<BorderQuad> _quadBuffer = new List<BorderQuad>();

    private readonly Dictionary<(int X, int Y), ulong> _uploaded = new Dictionary<(int X, int Y), ulong>();
    private readonly Queue<(int X, int Y)> _pending = new Queue<(int X, int Y)>();
    private RealmPalette? _palette;
    private Painting? _painted;
    private Task<Painting>? _painting;
    private int _generation;
    private bool _dirty = true;
    private bool _checkTerritory = true;
    private (bool Heraldic, bool Gild, float Width, bool Names, bool Fill, float FillStrength, int ColourVersion) _look;
    private string? _renderMaterial;
    private string? _renderBlend;
    private int _appliedColourVersion = -1;
    private bool? _drawThroughTerrain;
    private float _lastAlpha = -1f;
    private double _slowestUploadMs;

    public RealmBorderService(
        RealmTerritoryService territory,
        IRealmMapAdapter map,
        IBorderRenderAdapter renderer,
        IRealmBordersSettings settings,
        RealmPaletteProvider palettes,
        IAlignmentService alignment,
        IRealmNoticeAdapter notices,
        ITerritoryWorker worker,
        IModLogger logger)
    {
        _territory = territory ?? throw new ArgumentNullException(nameof(territory));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _palettes = palettes ?? throw new ArgumentNullException(nameof(palettes));
        _alignment = alignment ?? throw new ArgumentNullException(nameof(alignment));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public MapMode Mode { get; private set; } = MapMode.Political;

    public bool Visible { get; private set; } = true;

    /// <summary>Where each realm's name goes, refreshed with the borders.</summary>
    public IReadOnlyList<RealmLabel> Labels { get; private set; } = Array.Empty<RealmLabel>();

    /// <summary>The borders' current opacity, 0 when hidden, zoomed in or switched off in MCM (the parchment map follows it).</summary>
    public float Alpha => _settings.Enabled ? Math.Max(0f, _lastAlpha) : 0f;

    public int DrawnTiles => _uploaded.Count;

    /// <summary>
    /// This campaign's colours: built fresh at every session start, so the reserve is whole again, with the
    /// player's MCM colours applied whenever they change (Your Realm follows the player's own realm), so a
    /// realm created in play avoids them too.
    /// </summary>
    private RealmPalette Palette
    {
        get
        {
            if (_palette == null)
            {
                _palette = _palettes.NewPalette();
                _appliedColourVersion = -1;
            }
            int version = _settings.ColourVersion;
            if (version != _appliedColourVersion)
            {
                _appliedColourVersion = version;
                foreach (string realm in _palette.CuratedRealms)
                    _palette.Override(realm, _settings.ColourOverride(realm));
            }
            _palette.ApplyYourRealm(_map.PlayerRealm, _settings.YourRealmColour);
            return _palette;
        }
    }

    /// <summary>A new or loaded campaign: every per-campaign state goes, the per-map provinces stay.</summary>
    public void OnSessionStart()
    {
        Mode = MapMode.Political;
        Visible = true;
        _crossings.Reset();
        _palette = null;
        ForgetDrawn();
        _checkTerritory = true;
    }

    /// <summary>The map screen was torn down (exit or load): let go of its scene; the next one is drawn afresh.</summary>
    public void OnMapScreenClosed()
    {
        ForgetDrawn();
        _renderer.Release();
    }

    /// <summary>Ownership, a kingdom, a war or an alliance may have changed: look again on the next map frame.</summary>
    public void MarkDirty() => _dirty = true;

    public void ToggleVisible()
    {
        if (_settings.Enabled)
            Visible = !Visible;
    }

    public void CycleMode()
    {
        if (!_settings.Enabled)
            return;
        Mode = MapModes.Next(Mode);
        _notices.ShowMapMode(Mode);
        _dirty = true;
    }

    /// <summary>
    /// The province map as ARGB pixels, row 0 along the map's southern (lowest Y) edge, the row order
    /// a bottom-up bitmap stores: each realm in its colour with its edges dark, a fief without an owner
    /// white, wild land parchment, water slate blue. Null until the provinces are ready. For the console.
    /// </summary>
    public (int Columns, int Rows, uint[] Pixels)? ProvinceImage()
    {
        var result = _territory.Result;
        if (result == null)
            return null;
        var map = result.Map;
        var realms = result.FiefIds.Select(id => _map.RealmOf(id)).ToList();
        string? RealmAt(int column, int row)
        {
            int province = map[column, row];
            return province >= 0 && province < realms.Count ? realms[province] : null;
        }

        var palette = Palette;
        var pixels = new uint[map.Columns * map.Rows];
        for (int row = 0; row < map.Rows; row++)
        {
            for (int column = 0; column < map.Columns; column++)
            {
                string? realm = RealmAt(column, row);
                uint colour = realm != null ? palette.ColourOf(realm)
                    : map[column, row] >= 0 ? PictureUnownedFief
                    : result.Terrain[column, row] == TerrainClass.Water ? PictureWater : PictureWildLand;
                bool edge = realm != null && (RealmAt(column + 1, row) != realm || RealmAt(column - 1, row) != realm
                    || RealmAt(column, row + 1) != realm || RealmAt(column, row - 1) != realm);
                if (edge)
                    colour = PictureEdge;
                pixels[row * map.Columns + column] = colour;
            }
        }
        return (map.Columns, map.Rows, pixels);
    }

    /// <summary>Redraws every tile from another material (the look session's console command).</summary>
    public bool UseMaterial(string name)
    {
        if (!_renderer.UseMaterial(name))
            return false;
        ForgetDrawn();
        return true;
    }

    /// <summary>Redraws every tile with another engine blend mode (the look session's console command).</summary>
    public bool UseBlendMode(string name)
    {
        if (!_renderer.UseBlendMode(name))
            return false;
        ForgetDrawn();
        return true;
    }

    /// <summary>Recomputes the provinces and redraws everything (the console rebuild).</summary>
    public void Rebuild()
    {
        _territory.Invalidate();
        ForgetDrawn();
    }

    public void OnMapFrame(float cameraDistance)
    {
        if (!_settings.Enabled)
        {
            if (_painted != null || _uploaded.Count > 0 || _pending.Count > 0)
                ForgetDrawn();
            return;
        }
        if (!_renderer.IsAvailable)
            return;

        // Each MCM render choice applies when it changes; a console command in between stands until its own
        // dropdown changes.
        string? material = _settings.MaterialName, blend = _settings.BlendMode;
        if (material != _renderMaterial)
        {
            _renderMaterial = material;
            if (!_renderer.UseMaterial(material))
                _logger.LogWarning($"[RealmBorders] MCM Border Material '{material}' does not exist; keeping the current one");
            ForgetDrawn();
        }
        if (blend != _renderBlend)
        {
            _renderBlend = blend;
            if (!_renderer.UseBlendMode(blend))
                _logger.LogWarning($"[RealmBorders] MCM Border Blend Mode '{blend}' is not an engine blend mode; keeping the current one");
            ForgetDrawn();
        }

        if (_checkTerritory || _territory.State == TerritoryState.Idle)
        {
            // Begin keeps a finished partition when the fief layout is unchanged, so a reload or a
            // second campaign on the same map costs nothing and a different map recomputes.
            _checkTerritory = false;
            _territory.Begin(new PartitionSettings());
        }
        _territory.Tick(SamplingBudgetMs);
        var result = _territory.Result;
        if (_territory.State != TerritoryState.Ready || result == null)
            return;

        bool drawThrough = _settings.DrawThroughTerrain;
        if (_drawThroughTerrain != drawThrough)
        {
            _drawThroughTerrain = drawThrough;
            ForgetDrawn();
        }
        var look = (_settings.HeraldicBands, _settings.GildPlayerRealm, _settings.WidthScale, _settings.RealmNames,
            _settings.FillLands, _settings.FillStrength, _settings.ColourVersion);
        if (!look.Equals(_look))
        {
            _look = look;
            _dirty = true;
        }

        Collect();
        if (_dirty && _painting == null)
            StartPainting(result);
        Collect(); // a synchronous worker has finished already
        Upload(drawThrough);
        ApplyFade(cameraDistance);
    }

    public void OnHourlyTick()
    {
        if (!_settings.Enabled || !_settings.CrossingNotices)
            return;
        var result = _territory.Result;
        if (_territory.State != TerritoryState.Ready || result == null)
            return;
        if (!_map.TryGetMainPartyPosition(out float x, out float y))
            return;
        int province = result.Map.ProvinceAt(x, y);
        string? realm = province >= 0 && province < result.FiefIds.Count ? _map.RealmOf(result.FiefIds[province]) : null;
        string? announce = _crossings.Observe(realm, _map.CampaignHours);
        if (announce != null)
            _notices.ShowEnteringRealm(_map.RealmName(announce));
    }

    public string Status()
    {
        var r = _territory.Result;
        return $"enabled={_settings.Enabled} visible={Visible} mode={Mode} territory={_territory.State} "
            + $"sampling={_territory.SamplingTime.TotalMilliseconds:F0}ms "
            + (r == null ? string.Empty : $"partition={r.PartitionTime.TotalMilliseconds:F0}ms grid={r.Map.Columns}x{r.Map.Rows} chains={r.Boundaries.Count} ")
            + (_painted == null ? string.Empty : $"repaint={_painted.Milliseconds:F0}ms lines={_painted.Lines} quads={_painted.Quads} ")
            + $"painting={_painting != null} tiles={_uploaded.Count} pending={_pending.Count} slowestUpload={_slowestUploadMs:F1}ms "
            + $"alpha={Alpha:F2} labels={Labels.Count} material={_renderer.ActiveMaterial ?? "(none yet)"} "
            + $"blend={_renderer.ActiveBlendMode ?? "(none yet)"}";
    }

    /// <summary>Snapshots the campaign on this thread and, unless nothing on screen would change, paints on the worker.</summary>
    private void StartPainting(TerritoryResult result)
    {
        _dirty = false;
        var scene = Snapshot(result);
        if (_painted != null && scene.SameAs(_painted.Scene))
            return;
        var keepLabels = _painted != null && scene.SameRealmsAs(_painted.Scene) ? _painted.Labels : null;
        int generation = _generation;
        var buffer = _quadBuffer;
        _painting = _worker.Run(() => Paint(scene, keepLabels, generation, buffer));
    }

    /// <summary>Adopts a finished repaint: its labels, and every tile whose content changed.</summary>
    private void Collect()
    {
        var task = _painting;
        if (task == null || !task.IsCompleted)
            return;
        _painting = null;
        if (task.IsFaulted || task.IsCanceled)
        {
            // A repaint is pure, so it would fail the same way again: wait for the next change rather
            // than retry every frame. The whole exception is logged, since off the game thread it no
            // longer reaches the crash report with its stack.
            _logger.LogError("[RealmBorders] repaint failed; the borders keep their current drawing, if any, until the next change: "
                + (task.Exception?.GetBaseException().ToString() ?? "cancelled"));
            return;
        }
        var painting = task.Result;
        if (painting.Generation != _generation)
        {
            _dirty = true; // the drawn state was forgotten while it painted: paint again
            return;
        }

        _painted = painting;
        Labels = painting.Labels;
        foreach (var tile in painting.Hashes)
        {
            if (!_uploaded.TryGetValue(tile.Key, out ulong hash) || hash != tile.Value)
                Enqueue(tile.Key);
        }
        foreach (var tile in _uploaded.Keys)
        {
            if (!painting.Hashes.ContainsKey(tile))
                Enqueue(tile);
        }
        _logger.LogDebug($"[RealmBorders] repaint ({painting.Scene.Mode}): {painting.Lines} lines, {painting.Quads} quads, "
            + $"{_pending.Count} tiles queued, {painting.Milliseconds:F0} ms on the worker");
    }

    /// <summary>The worker's part: everything here reads only the snapshot and the immutable province map.</summary>
    private static Painting Paint(PaintScene scene, IReadOnlyList<RealmLabel>? keepLabels, int generation, List<BorderQuad> quads)
    {
        var watch = Stopwatch.StartNew();
        var territory = scene.Territory;
        var lines = RealmBorderSelector.Select(territory.Boundaries, scene.Groups);
        var look = new BorderLook { WidthScale = scene.Width };
        float cell = territory.Map.CellSize;
        quads.Clear();
        // The tint goes in first: within a tile's mesh, later triangles draw over earlier ones.
        if (scene.FillStrength > 0f)
            RealmFill.Paint(territory.Map, scene.Groups, scene.FillColours, scene.FillStrength, quads);
        foreach (var line in lines)
        {
            var world = line.Points.Select(p => new MapPoint(territory.Map.MinX + p.Column * cell, territory.Map.MinY + p.Row * cell)).ToList();
            var smooth = PolylineMath.Smooth(world, line.IsClosed, tolerance: 0.9f * cell, iterations: ChaikinPasses);
            var even = PolylineMath.Resample(smooth, ResampleSpacing, line.IsClosed);
            var paint = BorderStyleSelector.PaintFor(line, scene.Mode, scene.Heraldic, scene.Player, scene.Gild, group => scene.Colours[group]);
            BorderPainter.Paint(even, line.IsClosed, paint, look, quads);
        }

        var tiles = TileBinner.Bin(quads, TileSize);
        var hashes = new Dictionary<(int X, int Y), ulong>(tiles.Count);
        foreach (var tile in tiles)
            hashes[tile.Key] = TileBinner.ContentHash(tile.Value);
        var labels = keepLabels
            ?? (scene.Names ? RealmLabelPlacer.Place(territory.Map, scene.Realms, MinimumLabelCells) : Array.Empty<RealmLabel>());
        return new Painting(scene, tiles, hashes, labels, lines.Count, quads.Count, watch.Elapsed.TotalMilliseconds, generation);
    }

    /// <summary>Everything a repaint reads from the campaign and the settings, taken on the game thread.</summary>
    private PaintScene Snapshot(TerritoryResult result)
    {
        int count = result.FiefIds.Count;
        var realms = new string?[count];
        var groups = new string?[count];
        var groupOf = new Dictionary<string, string?>(StringComparer.Ordinal);
        var colours = new Dictionary<string, uint>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string? realm = realms[i] = _map.RealmOf(result.FiefIds[i]);
            if (realm == null)
                continue;
            if (!groupOf.TryGetValue(realm, out string? group))
                groupOf[realm] = group = GroupOf(realm);
            groups[i] = group;
            if (group != null && !colours.ContainsKey(group))
                colours[group] = ColourOf(group);
        }
        string? player = Mode == MapMode.Political ? _map.PlayerRealm : null;
        // The allies and enemies mode leaves neutral land untinted: a white wash over half the map says nothing.
        var fillColours = Mode == MapMode.War
            ? colours.Where(pair => pair.Key != RelationGroups.Neutral).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : colours;
        float fill = _look.Fill ? _look.FillStrength : 0f;
        return new PaintScene(result, realms, groups, colours, fillColours, fill, player, Mode, _look.Heraldic, _look.Gild, _look.Width, _look.Names);
    }

    private string? GroupOf(string realm) => Mode switch
    {
        MapMode.Alignment => SideOf(realm),
        MapMode.War => RelationGroups.Of(_map.RelationToPlayer(realm)),
        _ => realm,
    };

    private uint ColourOf(string group) => Mode switch
    {
        MapMode.Alignment => group == FreeSide ? FreePeoplesColour : ShadowColour,
        MapMode.War => RelationColours.TryGetValue(group, out uint colour) ? colour : RelationColours[RelationGroups.Neutral],
        _ => Palette.ColourOf(group),
    };

    private string? SideOf(string realm) =>
        _alignment.ResolveSide(realm, _map.CultureOfRealm(realm) ?? string.Empty) switch
        {
            FactionSide.Free => FreeSide,
            FactionSide.Evil => ShadowSide,
            _ => null, // a neutral realm stands on neither side of the one front line
        };

    private void Upload(bool drawThrough)
    {
        if (_pending.Count == 0 || _painted == null)
            return;
        var slice = Stopwatch.StartNew();
        do
        {
            var tile = _pending.Dequeue();
            if (_painted.Tiles.TryGetValue(tile, out var quads))
            {
                double before = slice.Elapsed.TotalMilliseconds;
                _renderer.SetTile(tile, quads, Lift, drawThrough);
                _slowestUploadMs = Math.Max(_slowestUploadMs, slice.Elapsed.TotalMilliseconds - before);
                _uploaded[tile] = _painted.Hashes[tile];
            }
            else if (_uploaded.Remove(tile))
            {
                _renderer.RemoveTile(tile);
            }
        }
        while (_pending.Count > 0 && slice.Elapsed.TotalMilliseconds < UploadBudgetMs);
    }

    private void ApplyFade(float cameraDistance)
    {
        float alpha = 0f;
        if (Visible && FiniteFloat(cameraDistance))
        {
            float start = _settings.FadeStartDistance, full = _settings.FullOpacityDistance;
            alpha = full > start ? Math.Max(0f, Math.Min(1f, (cameraDistance - start) / (full - start))) : 1f;
        }
        alpha = (float)Math.Round(alpha * 50f) / 50f;
        if (alpha == _lastAlpha)
            return;
        _lastAlpha = alpha;
        _renderer.SetAlpha(alpha);
    }

    private void Enqueue((int X, int Y) tile)
    {
        if (!_pending.Contains(tile))
            _pending.Enqueue(tile);
    }

    /// <summary>
    /// Takes every border off the map and asks for a fresh drawing. A repaint still on the worker is
    /// left to finish and then dropped (its generation is stale), so two never share the quad buffer.
    /// </summary>
    private void ForgetDrawn()
    {
        _renderer.Clear();
        _uploaded.Clear();
        _pending.Clear();
        _painted = null;
        _generation++;
        Labels = Array.Empty<RealmLabel>();
        _lastAlpha = -1f;
        _dirty = true;
    }

    private static bool FiniteFloat(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>What a repaint was painted from; equal scenes paint equal borders.</summary>
    private sealed class PaintScene
    {
        public PaintScene(TerritoryResult territory, string?[] realms, string?[] groups, Dictionary<string, uint> colours,
            Dictionary<string, uint> fillColours, float fillStrength, string? player, MapMode mode, bool heraldic, bool gild, float width, bool names)
        {
            Territory = territory;
            Realms = realms;
            Groups = groups;
            Colours = colours;
            FillColours = fillColours;
            FillStrength = fillStrength;
            Player = player;
            Mode = mode;
            Heraldic = heraldic;
            Gild = gild;
            Width = width;
            Names = names;
        }

        public TerritoryResult Territory { get; }

        public string?[] Realms { get; }

        public string?[] Groups { get; }

        public Dictionary<string, uint> Colours { get; }

        /// <summary>The groups whose land is tinted, with their colours.</summary>
        public Dictionary<string, uint> FillColours { get; }

        /// <summary>The tint's strength; 0 when the land is not tinted.</summary>
        public float FillStrength { get; }

        public string? Player { get; }

        public MapMode Mode { get; }

        public bool Heraldic { get; }

        public bool Gild { get; }

        public float Width { get; }

        public bool Names { get; }

        /// <summary>The same province map and owners, with names on in both: the labels would not move.</summary>
        public bool SameRealmsAs(PaintScene other) =>
            ReferenceEquals(Territory, other.Territory) && Names == other.Names && Realms.SequenceEqual(other.Realms);

        public bool SameAs(PaintScene other) =>
            SameRealmsAs(other) && Groups.SequenceEqual(other.Groups) && Player == other.Player && Mode == other.Mode
            && Heraldic == other.Heraldic && Gild == other.Gild && Width.Equals(other.Width) && FillStrength.Equals(other.FillStrength)
            && Colours.Count == other.Colours.Count
            && Colours.All(pair => other.Colours.TryGetValue(pair.Key, out uint colour) && colour == pair.Value);
    }

    /// <summary>A finished repaint: the tiles and their content hashes, the names, and what it cost.</summary>
    private sealed class Painting
    {
        public Painting(PaintScene scene, Dictionary<(int X, int Y), List<BorderQuad>> tiles, Dictionary<(int X, int Y), ulong> hashes,
            IReadOnlyList<RealmLabel> labels, int lines, int quads, double milliseconds, int generation)
        {
            Scene = scene;
            Tiles = tiles;
            Hashes = hashes;
            Labels = labels;
            Lines = lines;
            Quads = quads;
            Milliseconds = milliseconds;
            Generation = generation;
        }

        public PaintScene Scene { get; }

        public Dictionary<(int X, int Y), List<BorderQuad>> Tiles { get; }

        public Dictionary<(int X, int Y), ulong> Hashes { get; }

        public IReadOnlyList<RealmLabel> Labels { get; }

        public int Lines { get; }

        public int Quads { get; }

        public double Milliseconds { get; }

        public int Generation { get; }
    }
}
