using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MainMenuCustomizer;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The themed main menu's two live touches (#704), from Kysaro's <c>MainMenuShimmer</c> and
/// <c>MainMenuFeature</c>: light sweeps across the title and tagline, and the new-game entry drawn in
/// the featured style. Kysaro found that entry by its English text; TAOM's
/// <c>MainMenuCustomizer</c> names it with <see cref="MainMenuCustomizerService.NewGameName"/>, so this
/// matches in every language. The sweeps stop, and let go of the menu's widgets, once the engine has
/// released the menu's movie.
/// </summary>
public sealed class MainMenuWidgets
{
    private const string FeaturedBrushName = "MM.ButtonText.Featured";
    private const string PlainBrushName = "MM.ButtonText";
    private const float FeaturedRowHeight = 66f;
    private const float StripTravel = 300f;

    private readonly IFrontEndStateAdapter _state;
    private readonly IModLogger _logger;
    private readonly Sweep _title = new("", new[] { 14f, 28f, 42f, 56f }, new ShimmerSweep(310f, 1.2f, 7f, 1.7f, 60f));
    private readonly Sweep _tagline = new("Tag", new[] { 10f, 20f, 30f, 40f }, new ShimmerSweep(170f, 3.6f, 9f, 2.2f, 40f));

    private object? _movie;
    private float _time;

    public MainMenuWidgets(IFrontEndStateAdapter state, IModLogger logger)
    {
        _state = state;
        _logger = logger;
    }

    /// <summary>Called once the themed main menu's movie (<paramref name="movie"/>) is built on
    /// <paramref name="layer"/>.</summary>
    public void Attach(GauntletLayer layer, object movie)
    {
        Stop();
        try
        {
            var root = layer.UIContext?.Root;
            if (root == null)
                return;

            _movie = movie;
            _time = 0f;
            _title.Attach(root);
            _tagline.Attach(root);
            ApplyFeaturedEntry(root);
        }
        catch (Exception ex)
        {
            Stop();
            _logger.LogWarning($"[FactionUI] main menu effects not attached: {ex.Message}");
        }
    }

    /// <summary>Once per application tick.</summary>
    public void Tick(float dt)
    {
        if (_movie == null)
            return;
        if (_state.IsMovieReleased(_movie))
        {
            Stop();
            return;
        }

        _time += dt;
        _title.Tick(_time);
        _tagline.Tick(_time);
    }

    private void Stop()
    {
        _movie = null;
        _title.Detach();
        _tagline.Detach();
    }

    private static void ApplyFeaturedEntry(Widget root)
    {
        var featured = UIResourceManager.BrushFactory?.GetBrush(FeaturedBrushName);
        var newGameText = new TextObject(MainMenuCustomizerService.NewGameName).ToString();
        if (featured == null || string.IsNullOrEmpty(newGameText))
            return;

        foreach (var widget in root.GetAllChildrenAndThisRecursive())
        {
            if (widget is not TextWidget text
                || text.Brush?.Name != PlainBrushName
                || text.Text == null
                || text.Text.IndexOf(newGameText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            text.Brush = featured;
            text.MarginLeft = 0f;
            text.MarginRight = 0f;
            var row = text.ParentWidget;
            if (row == null)
                continue;
            row.SuggestedHeight = FeaturedRowHeight;
            if (row.ParentWidget != null)
                row.ParentWidget.SuggestedHeight = FeaturedRowHeight;
        }
    }

    /// <summary>One sweep's strips: a clipping widget per strip width, each holding an image that
    /// counter-moves so the highlight stays fixed while the clip slides.</summary>
    private sealed class Sweep
    {
        private readonly string _idPrefix;
        private readonly float[] _widths;
        private readonly ShimmerSweep _timing;
        private Widget[]? _clips;
        private Widget[]? _images;

        public Sweep(string idPrefix, float[] widths, ShimmerSweep timing)
        {
            _idPrefix = idPrefix;
            _widths = widths;
            _timing = timing;
        }

        public void Detach()
        {
            _clips = null;
            _images = null;
        }

        public void Attach(Widget root)
        {
            var clips = new Widget[_widths.Length];
            var images = new Widget[_widths.Length];
            for (var i = 0; i < _widths.Length; i++)
            {
                var clipId = _idPrefix + "Shimmer" + i;
                var imageId = _idPrefix + "ShimmerImg" + i;
                var clip = root.GetFirstInChildrenAndThisRecursive(w => w.Id == clipId);
                var image = root.GetFirstInChildrenAndThisRecursive(w => w.Id == imageId);
                if (clip == null || image == null)
                    return;
                clips[i] = clip;
                images[i] = image;
            }
            _clips = clips;
            _images = images;
        }

        public void Tick(float time)
        {
            if (_clips == null || _images == null)
                return;

            var position = _timing.PositionAt(time);
            var parked = position < -StripTravel;
            for (var i = 0; i < _widths.Length; i++)
            {
                var offset = position - _widths[i] / 2f;
                _clips[i].PositionXOffset = parked ? -StripTravel : offset;
                _images[i].PositionXOffset = parked ? StripTravel : -offset;
            }
        }
    }
}
