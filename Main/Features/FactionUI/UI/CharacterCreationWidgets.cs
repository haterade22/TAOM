using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The themed face generator's live touches (#704), from Kysaro's <c>ViewportReveal</c>,
/// <c>TabContentReveal</c> and <c>QuoteRotator</c>: the screen fades in once the 3D body is ready to draw
/// (at most four seconds), each tab's content slides in when picked, and the quote under the tabs changes
/// with the tab. The model is also shown dressed, as Kysaro's screen expects. Everything lets go once
/// the engine has released the face generator's movie, so a closed face generator's view is never
/// asked whether it is ready to render.
/// </summary>
public sealed class CharacterCreationWidgets
{
    private const string TabControlId = "PropertyTab";
    private const string QuoteBlockId = "QuoteBlock";
    private const string QuoteTextId = "QuoteText";
    private const string QuoteAttributionId = "QuoteAttribution";
    private const float ViewportFade = 0.35f;
    private const float ViewportMaxWait = 4f;
    private const float TabSlide = -40f;
    private const float TabDuration = 0.28f;
    private const float QuoteSlide = -60f;
    private const float QuoteDuration = 0.4f;

    private static readonly (string Line, string Author)[] Quotes =
    {
        ("{=taom_fui_quote_gold}All that is gold does not glitter, not all those who wander are lost.", "{=taom_fui_quote_gold_by}- Bilbo Baggins"),
        ("{=taom_fui_quote_light}A light from the shadows shall spring.", "{=taom_fui_quote_light_by}- Bilbo Baggins"),
        ("{=taom_fui_quote_smallest}Even the smallest person can change the course of the future.", "{=taom_fui_quote_smallest_by}- Galadriel"),
        ("{=taom_fui_quote_foul}I look foul and feel fair.", "{=taom_fui_quote_foul_by}- Aragorn"),
    };

    // FaceGenVM keeps its screen in a private readonly field; resolved once (v1.5.3 FaceGenVM).
    private static readonly FieldInfo? FaceGeneratorScreenField = AccessTools.Field(typeof(FaceGenVM), "_faceGeneratorScreen");

    private readonly IFrontEndStateAdapter _state;
    private readonly IModLogger _logger;

    private object? _movie;
    private Widget? _fadeRoot;
    private BodyGeneratorView? _bodyView;
    private float _fadeWait;
    private float _fadeTime;
    private bool _bodyReady;

    private TabControl? _tabs;
    private Widget? _slidingTab;
    private Widget? _previousTab;
    private float _tabTime = -1f;

    private Widget? _quoteBlock;
    private RichTextWidget? _quoteText;
    private TextWidget? _quoteAttribution;
    private int _quoteIndex = -1;
    private float _quoteTime = -1f;

    public CharacterCreationWidgets(IFrontEndStateAdapter state, IModLogger logger)
    {
        _state = state;
        _logger = logger;
    }

    /// <summary>The themed face generator's movie (<paramref name="movie"/>) is built on
    /// <paramref name="layer"/>.</summary>
    public void OnFaceGenLoaded(GauntletLayer layer, object movie, object? dataSource)
    {
        Detach();
        try
        {
            if (dataSource is FaceGenVM faceGen && !faceGen.IsDressed)
                faceGen.ExecuteChangeClothing();

            var root = layer.UIContext?.Root;
            if (root == null)
                return;

            _movie = movie;
            if (dataSource is FaceGenVM vm && FaceGeneratorScreenField?.GetValue(vm) is BodyGeneratorView bodyView)
            {
                _bodyView = bodyView;
                _fadeRoot = root;
                root.AlphaFactor = 0f;
            }

            if (root.GetFirstInChildrenAndThisRecursive(w => w.Id == TabControlId) is TabControl tabs)
            {
                _tabs = tabs;
                tabs.OnActiveTabChange += OnActiveTabChange;
                _quoteBlock = root.GetFirstInChildrenAndThisRecursive(w => w.Id == QuoteBlockId);
                _quoteText = root.GetFirstInChildrenAndThisRecursive(w => w.Id == QuoteTextId) as RichTextWidget;
                _quoteAttribution = root.GetFirstInChildrenAndThisRecursive(w => w.Id == QuoteAttributionId) as TextWidget;
            }
        }
        catch (Exception ex)
        {
            if (_fadeRoot != null)
                _fadeRoot.AlphaFactor = 1f;
            Detach();
            _logger.LogWarning($"[FactionUI] face generator effects not attached: {ex.Message}");
        }
    }

    public void Tick(float dt)
    {
        if (_movie == null)
            return;
        try
        {
            if (_state.IsMovieReleased(_movie))
            {
                Detach();
                return;
            }

            TickViewportFade(dt);
            _tabTime = Slide(_slidingTab, _tabTime, dt, TabDuration, vertical: true, TabSlide);
            _quoteTime = Slide(_quoteBlock, _quoteTime, dt, QuoteDuration, vertical: false, QuoteSlide);
        }
        catch (Exception)
        {
            if (_fadeRoot != null)
                _fadeRoot.AlphaFactor = 1f;
            Detach();
        }
    }

    private void TickViewportFade(float dt)
    {
        if (_fadeRoot == null)
            return;

        if (!_bodyReady)
        {
            _fadeWait += dt;
            _bodyReady = _fadeWait >= ViewportMaxWait || _bodyView?.ReadyToRender() != false;
            if (!_bodyReady)
                return;
        }

        _fadeTime += dt;
        var progress = Math.Min(1f, _fadeTime / ViewportFade);
        _fadeRoot.AlphaFactor = progress * progress * (3f - 2f * progress);
        if (progress >= 1f)
        {
            _fadeRoot = null;
            _bodyView = null;
        }
    }

    private void OnActiveTabChange()
    {
        try
        {
            if (_previousTab != null)
            {
                _previousTab.AlphaFactor = 1f;
                _previousTab.PositionYOffset = 0f;
            }

            _previousTab = _tabs?.ActiveTab;
            if (_previousTab != null)
            {
                _previousTab.AlphaFactor = 0f;
                _previousTab.PositionYOffset = TabSlide;
                _slidingTab = _previousTab;
                _tabTime = 0f;
            }

            var index = _tabs?.SelectedIndex ?? -1;
            if (index < 0 || index >= Quotes.Length || index == _quoteIndex
                || _quoteBlock == null || _quoteText == null || _quoteAttribution == null)
            {
                return;
            }

            _quoteIndex = index;
            _quoteText.Text = new TextObject(Quotes[index].Line).ToString();
            _quoteAttribution.Text = new TextObject(Quotes[index].Author).ToString();
            _quoteBlock.AlphaFactor = 0f;
            _quoteBlock.PositionXOffset = QuoteSlide;
            _quoteTime = 0f;
        }
        catch (Exception)
        {
            _tabTime = -1f;
            _quoteTime = -1f;
        }
    }

    /// <summary>Eases <paramref name="widget"/> in from <paramref name="offset"/>; returns the next time,
    /// or -1 once done.</summary>
    private static float Slide(Widget? widget, float time, float dt, float duration, bool vertical, float offset)
    {
        if (time < 0f || widget == null)
            return -1f;

        time += dt;
        var progress = Math.Min(1f, time / duration);
        var eased = 1f - (1f - progress) * (1f - progress);
        if (vertical)
            widget.PositionYOffset = offset * (1f - eased);
        else
            widget.PositionXOffset = offset * (1f - eased);
        widget.AlphaFactor = eased;
        return progress >= 1f ? -1f : time;
    }

    private void Detach()
    {
        if (_tabs != null)
            _tabs.OnActiveTabChange -= OnActiveTabChange;
        _movie = null;
        _tabs = null;
        _fadeRoot = null;
        _bodyView = null;
        _fadeWait = 0f;
        _fadeTime = 0f;
        _bodyReady = false;
        _slidingTab = null;
        _previousTab = null;
        _tabTime = -1f;
        _quoteBlock = null;
        _quoteText = null;
        _quoteAttribution = null;
        _quoteIndex = -1;
        _quoteTime = -1f;
    }
}
