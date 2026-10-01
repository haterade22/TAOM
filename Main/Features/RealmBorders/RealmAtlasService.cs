using System;
using System.Diagnostics;
using System.IO;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Features.RealmBorders;

/// <summary>
/// The parchment map (#698 follow-up): at full zoom-out a parchment map of TAOM_Map covers the campaign map,
/// with the realm tint, borders and names drawn over it. The picture is traced from the map's own terrain
/// (its generation record is the atlas entry in <c>tools/realm_border_art/provenance.json</c>), so the
/// borders run along its drawn coasts and rivers. The sheet is built once per map scene on first need and
/// fades in over the last stretch of the zoom; it shows only while MCM's switch is on and the borders show.
/// A build that fails is not tried again until the console's rebuild or the next map screen.
/// </summary>
public sealed class RealmAtlasService
{
    public const string TextureFileName = "atlas_parchment.png";
    public const float DefaultFadeStart = 0.8f;
    public const float DefaultFadeFull = 0.95f;

    /// <summary>
    /// The paper's colour. The banner material draws the picture in the mesh's factor colour where the picture
    /// is bright and see-through where it is dark (seen in game 2026-10-01: white paper came out a neutral
    /// 234, 234, 226, and the 3D map showed through the ink), so the paper takes this cream.
    /// </summary>
    public const uint DefaultPaper = 0xFFFFF0D8u;

    /// <summary>The ink underlay's colour, seen through the picture's dark strokes: the art's own sepia.</summary>
    public const uint DefaultInk = 0xFF3D281Bu;

    /// <summary>8 x 8 tiles of 16 x 16 cells: 12.5 world units a cell, 256 quads a mesh.</summary>
    private const int TilesPerSide = 8;
    private const int CellsPerTile = 16;

    /// <summary>Opacity steps: the sheet's alpha is pushed to the meshes only when it changes by a step.</summary>
    private const int AlphaSteps = 50;

    private const string NotBuiltYet = "not built yet";

    private static readonly IFormatProvider Invariant = System.Globalization.CultureInfo.InvariantCulture;

    private readonly IBorderRenderAdapter _renderer;
    private readonly IRealmBordersSettings _settings;
    private readonly IPathService _paths;
    private readonly IModLogger _logger;
    private float _lastAlpha = -1f;
    private bool _buildFailed;
    private string _lastBuild = NotBuiltYet;
    private float _cameraDistance = float.NaN;
    private float _maxDistance = float.NaN;

    public RealmAtlasService(IBorderRenderAdapter renderer, IRealmBordersSettings settings, IPathService paths, IModLogger logger)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The fraction of the furthest zoom at which the sheet starts to appear.</summary>
    public float FadeStart { get; private set; } = DefaultFadeStart;

    /// <summary>The fraction of the furthest zoom from which the sheet is fully drawn.</summary>
    public float FadeFull { get; private set; } = DefaultFadeFull;

    /// <summary>The paper's colour, opaque ARGB.</summary>
    public uint Paper { get; private set; } = DefaultPaper;

    /// <summary>The ink underlay's colour, opaque ARGB.</summary>
    public uint Ink { get; private set; } = DefaultInk;

    /// <summary>The sheet's current opacity, 0 when hidden, zoomed in or not built.</summary>
    public float Alpha => Math.Max(0f, _lastAlpha);

    private string TexturePath => Path.Combine(_paths.ModuleDataPath, "realm_borders", TextureFileName);

    /// <summary>One map frame: the camera's distance, its furthest zoom, and whether the borders are showing.</summary>
    public void OnMapFrame(float cameraDistance, float maxDistance, bool bordersShown)
    {
        _cameraDistance = cameraDistance;
        _maxDistance = maxDistance;
        float alpha = _settings.ParchmentMap && bordersShown ? Quantize(AtlasSheet.Alpha(cameraDistance, maxDistance, FadeStart, FadeFull)) : 0f;
        if (alpha > 0f && !_renderer.HasSheet && !TryBuild())
            alpha = 0f;
        if (alpha == _lastAlpha)
            return;
        _lastAlpha = alpha;
        _renderer.SetSheetAlpha(alpha);
    }

    /// <summary>The map screen closed: the sheet went with the renderer's release, and the next map may build one, a failed build included.</summary>
    public void OnMapScreenClosed()
    {
        _buildFailed = false;
        _lastAlpha = -1f;
        _lastBuild = NotBuiltYet;
    }

    /// <summary>Drops the sheet; the next frame that needs it builds it again, a failed build included.</summary>
    public void Rebuild()
    {
        _renderer.RemoveSheet();
        _buildFailed = false;
        _lastAlpha = -1f;
        _lastBuild = NotBuiltYet;
    }

    /// <summary>Rebuilds the sheet in these paper and ink colours (ink null keeps it); alpha bytes are ignored, the fade sets the opacity.</summary>
    public void UseTint(uint paper, uint? ink)
    {
        Paper = Opaque(paper);
        if (ink.HasValue)
            Ink = Opaque(ink.Value);
        Rebuild();
    }

    private static uint Opaque(uint argb) => 0xFF000000u | (argb & 0x00FFFFFFu);

    /// <summary>Moves the fade band, as fractions of the furthest zoom; false, and nothing changes, unless 0 &lt;= start &lt; full &lt;= 1.</summary>
    public bool SetFade(float start, float full)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(start, 0f, 1f) || !FiniteFloatValidator.IsFiniteInRange(full, 0f, 1f) || !(start < full))
            return false;
        FadeStart = start;
        FadeFull = full;
        return true;
    }

    public string Status()
    {
        string Number(float value) => FiniteFloatValidator.IsFinite(value) ? value.ToString("0", Invariant) : "unknown";
        string Fraction(float value) => value.ToString("0.###", Invariant);
        return $"Parchment map: {(_settings.ParchmentMap ? "on" : "off in MCM")}, sheet {(_renderer.HasSheet ? "built" : "not built")} ({_lastBuild}), "
             + $"material {_renderer.SheetMaterial ?? "none yet"}, alpha {Fraction(Alpha)}, "
             + $"camera {Number(_cameraDistance)} of {Number(_maxDistance)}, fades in from {Fraction(FadeStart)} to {Fraction(FadeFull)} of it, "
             + $"paper #{Paper & 0xFFFFFFu:X6}, ink #{Ink & 0xFFFFFFu:X6}, {_renderer.SheetTextureNote}";
    }

    /// <summary>
    /// Builds the sheet; false when it cannot. With no map scene yet it waits, since the next frame may have
    /// one; a refusal or an exception is latched, so a broken build costs one log line, not one per frame.
    /// </summary>
    private bool TryBuild()
    {
        if (_buildFailed || !_renderer.IsAvailable)
            return false;
        var watch = Stopwatch.StartNew();
        var tiles = AtlasSheet.Tiles(AtlasSheet.TerrainSize, TilesPerSide, CellsPerTile, Ink);
        bool built;
        try
        {
            built = _renderer.SetSheet(tiles, TexturePath, Paper);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RealmBorders] building the parchment map threw {ex.GetType().Name}: {ex.Message}");
            built = false;
        }
        if (!built)
        {
            // A build that threw partway may have added some tiles; none of a broken sheet is shown.
            _renderer.RemoveSheet();
            _buildFailed = true;
            _lastBuild = "the build failed, see the log";
            return false;
        }
        _lastAlpha = -1f;
        _lastBuild = $"built in {watch.Elapsed.TotalMilliseconds.ToString("0", Invariant)} ms";
        _logger.LogInfo($"[RealmBorders] parchment map {_lastBuild}: {tiles.Count} tiles, material '{_renderer.SheetMaterial}'");
        return true;
    }

    private static float Quantize(float alpha) => (float)Math.Round(alpha * AlphaSteps) / AlphaSteps;
}
