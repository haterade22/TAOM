extern alias SNV;
using System;
using TAOM.Features.FactionMap;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using Vector2 = SNV::System.Numerics.Vector2;

using EngineTexture = TaleWorlds.Engine.Texture;
using TwoDimTexture = TaleWorlds.TwoDimension.Texture;

namespace TAOM.Features.FactionMap.Widgets;

/// <summary>
/// Region puzzle piece widget - inherits ImageWidget (BrushWidget).
/// OverrideDefaultStateSwitchingEnabled = true -> stays in "Default" state always.
/// NEVER touches the Brush -- any Brush modification causes permanent darkening.
/// Hover/click feedback via a second DrawSprite pass with white tint overlay.
/// </summary>
public class PolygonWidget : ImageWidget
{
    private string _regionName = "";
    private bool _isSelected;
    private bool _isHovered;
    private bool _isPlayable;   // Tier 3: full hover/click/selection
    private bool _hasFaction;   // Tier 2: minimal hover + border, clickable for info
    // Tier 1 (neither): completely inert, no interaction
    private Color _factionColor = new Color(1f, 1f, 1f, 1f);
    private string _factionSide = "neutral"; // "free" | "evil" | "neutral"
    private string _factionId = "";          // faction key for banner_<factionId>.png

    // Faction banner overlay during pulse
    private Sprite _bannerSprite;             // per-instance: banner_<factionId>.png
    private bool _bannerLoaded;
    private bool _bannerLoadFailed;
    private const float BannerDisplaySize = 80f; // max display dimension in pixels

    // BBox positioning
    private float _bboxX, _bboxY, _bboxW, _bboxH;

    // Texture loading
    private bool _textureLoaded;
    private bool _loadFailed;
    private Sprite _loadedSprite;

    // Alpha hit-test data (1 byte per pixel, stored row-major)
    private byte[] _alphaMap;
    private int _texWidth;
    private int _texHeight;
    private const byte AlphaThreshold = 20; // pixel alpha > this = opaque for hit-testing

    // Hover animation
    private float _hoverOffset;
    private const float HoverTargetOffset = -8f;  // pixels up
    private const float HoverAnimSpeed = 80f;      // pixels per second

    // Faction display name for tooltip (read via HoveredFactionName static property)
    private string _factionDisplayName = "";

    // Rotating pulse: one playable region at a time glows in its faction color
    private static float _globalPulseTimer;       // shared timer across all instances
    private static int _globalPulseIndex;          // index into playable list of who is pulsing
    private static float _globalPulseAlpha;        // current pulse alpha (fade in/out)
    private const float PulseCycleDuration = 5.0f; // seconds per region (fade in + hold + fade out)
    private int _playableIndex = -1;               // this instance's index in the playable rotation

    // Faction pin/flag icon at capital
    private static Sprite _emblemSprite;    // shared across all instances
    private static bool _emblemLoaded;
    private static bool _emblemLoadFailed;
    private float _capitalNormX = -1f;  // capital position normalized within bbox (0-1)
    private float _capitalNormY = -1f;
    private const float EmblemSize = 32f;  // display size in pixels

    // Deferred pin rendering: all pins collected here, drawn by the last instance
    private struct PinRenderData
    {
        public float ScreenX, ScreenY; // absolute screen position of pin center
        public Color PinColor;
        public float Alpha;
    }
    private static readonly System.Collections.Generic.List<PinRenderData> _pendingPins
        = new System.Collections.Generic.List<PinRenderData>();

    /// <summary>
    /// Set by the last clicked PolygonWidget so the VM knows which region was selected.
    /// </summary>
    public static string LastClickedRegionName { get; private set; } = "";

    /// <summary>
    /// Name of the currently hovered faction, or empty. Read by VM for tooltip display.
    /// </summary>
    public static string HoveredFactionName { get; private set; } = "";


    // Global registry of all LIVE PolygonWidget instances for custom hover resolution
    private static readonly System.Collections.Generic.List<PolygonWidget> _allInstances
        = new System.Collections.Generic.List<PolygonWidget>();
    // The currently hovered widget (globally unique -- only one at a time)
    private static PolygonWidget _globalHovered;
    private bool _registered;

    // Polygon hit-test data (normalized 0-1 relative to bbox)
    private float[] _pointsX = Array.Empty<float>();
    private float[] _pointsY = Array.Empty<float>();
    private int _pointCount;

    private static int _totalPlayable; // total count of playable regions for pulse rotation

    // Counter for assigning playable indices
    private static int _nextPlayableIndex;

    /// <summary>
    /// Called from the Harmony Postfix before new widgets are created.
    /// Clears all stale state from the previous session so pulse indices start fresh.
    /// </summary>
    internal static void ResetSession()
    {
        _allInstances.Clear();
        _globalHovered = null;
        _nextPlayableIndex = 0;
        _totalPlayable = 0;
        _globalPulseTimer = 0f;
        _globalPulseIndex = 0;
        _globalPulseAlpha = 0f;
        HoveredFactionName = "";
    }

    public PolygonWidget(UIContext context) : base(context)
    {
        OverrideDefaultStateSwitchingEnabled = true;
    }

    [Editor(false)]
    public string RegionName
    {
        get => _regionName;
        set
        {
            if (_regionName != value)
            {
                _regionName = value;
                _textureLoaded = false;
                _loadFailed = false;
                _loadedSprite = null;

                // Load BBox from region registry (geometry from regions.json)
                var region = FactionMapStaticBridge.GetRegion(value);
                if (region != null)
                {
                    _bboxX = region.BBoxX;
                    _bboxY = region.BBoxY;
                    _bboxW = region.BBoxW;
                    _bboxH = region.BBoxH;
                }

                // Capital position -- convert from map-normalized (0-1 on whole map)
                // to bbox-relative (0-1 within this region's bbox) for rendering
                if (region != null && region.HasCapitalPos && _bboxW > 0 && _bboxH > 0)
                {
                    _capitalNormX = (region.CapitalX - _bboxX) / _bboxW;
                    _capitalNormY = (region.CapitalY - _bboxY) / _bboxH;
                }

                // Load faction data via region -> faction link
                // 3 tiers: no faction (inert) -> has faction (info only) -> playable (full)
                var faction = FactionMapStaticBridge.GetFactionForRegion(value);
                if (faction != null)
                {
                    _hasFaction = true;
                    _isPlayable = faction.Playable;
                    _factionDisplayName = faction.Name ?? "";
                    _factionSide = faction.Side ?? "neutral";
                    _factionId = region?.FactionId ?? "";
                    if (!string.IsNullOrEmpty(faction.Color))
                        FactionColor = ParseHexColor(faction.Color);

                    // Playable index is assigned lazily in OnLateUpdate after registration
                    // to ensure static counters are reset properly across sessions
                }
                else
                {
                    _hasFaction = false;
                    _isPlayable = false;
                    _factionDisplayName = "";
                    _factionId = "";
                }

                OnPropertyChanged(value, nameof(RegionName));
            }
        }
    }

    [Editor(false)]
    public float BBoxX
    {
        get => _bboxX;
        set { _bboxX = value; OnPropertyChanged(value, nameof(BBoxX)); }
    }

    [Editor(false)]
    public float BBoxY
    {
        get => _bboxY;
        set { _bboxY = value; OnPropertyChanged(value, nameof(BBoxY)); }
    }

    [Editor(false)]
    public float BBoxW
    {
        get => _bboxW;
        set { _bboxW = value; OnPropertyChanged(value, nameof(BBoxW)); }
    }

    [Editor(false)]
    public float BBoxH
    {
        get => _bboxH;
        set { _bboxH = value; OnPropertyChanged(value, nameof(BBoxH)); }
    }

    [Editor(false)]
    public bool IsPolygonSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(value, nameof(IsPolygonSelected));
            }
        }
    }

    [Editor(false)]
    public bool IsPolygonHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                OnPropertyChanged(value, nameof(IsPolygonHovered));
            }
        }
    }

    [Editor(false)]
    public string Points
    {
        get => PointsToString();
        set
        {
            ParsePoints(value);
            OnPropertyChanged(value, nameof(Points));
        }
    }

    [Editor(false)]
    public Color FactionColor
    {
        get => _factionColor;
        set
        {
            if (_factionColor != value)
            {
                _factionColor = value;
                OnPropertyChanged(value, nameof(FactionColor));
            }
        }
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        // Register ourselves in the global list (ensures only live widgets participate in hover)
        if (!_registered)
        {
            _registered = true;
            if (!_allInstances.Contains(this))
                _allInstances.Add(this);

            // Assign playable pulse index (counter is reset by ResetSession before new widgets are created)
            if (_isPlayable && _playableIndex < 0)
            {
                _playableIndex = _nextPlayableIndex++;
                _totalPlayable = _nextPlayableIndex;
            }
        }

        // Hover animation -- smooth lerp toward target offset (Tier 3 only: playable)
        // Selected regions also stay lifted (like hover)
        float target = ((_isHovered || _isSelected) && _isPlayable) ? HoverTargetOffset : 0f;
        if (Math.Abs(_hoverOffset - target) > 0.1f)
            _hoverOffset += Math.Sign(target - _hoverOffset) * HoverAnimSpeed * dt;
        else
            _hoverOffset = target;

        // BBox positioning + hover offset
        // ParentWidget.Size is in scaled coordinates (physical pixels).
        // ScaledSuggestedWidth/Height and ScaledPositionXOffset/YOffset accept scaled values
        // and internally divide by _scaleToUse. Using unscaled PositionXOffset with scaled
        // parentW would cause double-scaling on 4K displays.
        if (ParentWidget != null && _bboxW > 0 && _bboxH > 0)
        {
            float parentW = ParentWidget.Size.X;
            float parentH = ParentWidget.Size.Y;
            if (parentW > 0 && parentH > 0)
            {
                ScaledSuggestedWidth = _bboxW * parentW;
                ScaledSuggestedHeight = _bboxH * parentH;
                ScaledPositionXOffset = _bboxX * parentW;
                ScaledPositionYOffset = _bboxY * parentH + _hoverOffset;
            }
        }

        // Rotating pulse timer (shared across all instances, advances once per frame)
        // Only the first registered instance advances the timer to avoid double-counting
        if (_allInstances.Count > 0 && _allInstances[0] == this)
        {
            _globalPulseTimer += dt;
            if (_totalPlayable > 0)
            {
                float totalCycleTime = _totalPlayable * PulseCycleDuration;
                if (_globalPulseTimer >= totalCycleTime)
                    _globalPulseTimer -= totalCycleTime;

                _globalPulseIndex = (int)(_globalPulseTimer / PulseCycleDuration);
                // Phase within the current region's cycle: 0->1
                float phase = (_globalPulseTimer - _globalPulseIndex * PulseCycleDuration) / PulseCycleDuration;
                // Smooth fade in/out: peak at center (0.5)
                _globalPulseAlpha = (float)Math.Sin(phase * Math.PI); // 0->1->0
            }
        }

        // Global hover resolution -- every instance runs it each frame.
        // ResolveGlobalHover is idempotent (same result regardless of who calls it)
        // and cheap (~36 iterations per call). This ensures it always runs even if
        // the list order changes or instances are added/removed.
        ResolveGlobalHover();

        // Texture loading
        TryLoadTexture();
        if (_isPlayable) TryLoadBanner();
    }

    protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
    {
        // Skip BrushWidget.OnRender entirely -- render ourselves using Widget.OnRender pattern
        // This avoids BrushRenderer which causes darkening issues
        if (_loadedSprite?.Texture == null)
            return;

        // Clear pending pins at the start of each render cycle (first instance)
        if (_allInstances.Count > 0 && _allInstances[0] == this)
            _pendingPins.Clear();

        float contextAlpha = AlphaFactor * Context.ContextAlpha;

        // -- 3-TIER RENDERING --
        // Tier 1 (no faction): desaturated/dark, no hover effects
        // Tier 2 (faction, not playable): normal color, minimal hover (outline + subtle brighten), no lift
        // Tier 3 (playable): full effects (lift, shadow, edge, outline, selection)

        // How far are we hovering/selected? 0 = resting, 1 = fully lifted
        // Tier 3 (playable) gets the lift animation for both hover and selected
        float liftT = _isPlayable ? Math.Abs(_hoverOffset / HoverTargetOffset) : 0f;

        // Pass 1: Drop shadow -- only for playable regions (Tier 3)
        if (liftT > 0.01f)
        {
            SimpleMaterial shadowMat = drawContext.CreateSimpleMaterial();
            shadowMat.OverlayEnabled = false;
            shadowMat.CircularMaskingEnabled = false;
            shadowMat.Texture = _loadedSprite.Texture;
            shadowMat.NinePatchParameters = _loadedSprite.NinePatchParameters;
            shadowMat.Color = new Color(0f, 0f, 0f, 1f);
            shadowMat.ColorFactor = 1f;  // multiply by black
            shadowMat.AlphaFactor = contextAlpha * 0.35f * liftT;
            shadowMat.HueFactor = 0f;
            shadowMat.SaturationFactor = 0f;
            shadowMat.ValueFactor = -100f; // force dark

            // Shadow offset grows with lift: max ~8px right, ~12px down
            float shadowX = 8f * liftT;
            float shadowY = 12f * liftT;
            Rectangle2D shadowRect = AreaRect;
            shadowRect.SetVisualOffset(shadowX, shadowY);
            shadowRect.ValidateVisuals();
            shadowRect.CalculateVisualMatrixFrame();
            drawContext.DrawSprite(_loadedSprite, shadowMat, in shadowRect, _scaleToUse);
        }

        // Pass 2: Edge/thickness -- only for playable regions (Tier 3)
        if (liftT > 0.01f)
        {
            const float edgeThickness = 7f; // max pixels of visible edge
            float edgePx = edgeThickness * liftT;

            // Draw multiple thin slices for a solid edge look
            for (float y = edgePx; y >= 1f; y -= 1f)
            {
                SimpleMaterial edgeMat = drawContext.CreateSimpleMaterial();
                edgeMat.OverlayEnabled = false;
                edgeMat.CircularMaskingEnabled = false;
                edgeMat.Texture = _loadedSprite.Texture;
                edgeMat.NinePatchParameters = _loadedSprite.NinePatchParameters;
                // Gradient: darker at bottom, lighter near top
                float shade = y / edgePx; // 1 = bottom (darkest), 0 = top (lightest)
                edgeMat.Color = Color;
                edgeMat.ColorFactor = ColorFactor;
                edgeMat.AlphaFactor = contextAlpha;
                edgeMat.HueFactor = 0f;
                edgeMat.SaturationFactor = SaturationFactor;
                edgeMat.ValueFactor = -40f - 30f * shade; // -40 to -70 = darkened

                Rectangle2D edgeRect = AreaRect;
                edgeRect.SetVisualOffset(0f, y);
                edgeRect.ValidateVisuals();
                edgeRect.CalculateVisualMatrixFrame();
                drawContext.DrawSprite(_loadedSprite, edgeMat, in edgeRect, _scaleToUse);
            }
        }

        // Pass 3: Main sprite
        SimpleMaterial material = drawContext.CreateSimpleMaterial();
        material.OverlayEnabled = false;
        material.CircularMaskingEnabled = false;
        material.Texture = _loadedSprite.Texture;
        material.NinePatchParameters = _loadedSprite.NinePatchParameters;

        material.Color = Color;
        material.ColorFactor = ColorFactor;
        material.AlphaFactor = contextAlpha;
        material.HueFactor = 0f;
        material.SaturationFactor = SaturationFactor;
        material.ValueFactor = ValueFactor;

        if (!_hasFaction)
        {
            // Tier 1: no faction -- desaturate and darken, looks like background terrain
            material.SaturationFactor = -50f;
            material.ValueFactor = -30f;
        }
        else if (!_isPlayable)
        {
            // Tier 2: has faction, not playable -- slightly muted
            material.SaturationFactor = -15f;
            material.ValueFactor = -10f;
            if (_isHovered)
            {
                // Minimal hover: subtle brightening only
                material.ValueFactor = 10f;
                material.SaturationFactor = 0f;
            }
            if (_isSelected)
            {
                // Muted selection tint for info display
                material.Color = new Color(0.8f, 0.7f, 0.5f, 1f);
                material.ColorFactor = 0.3f;
                material.ValueFactor = 10f;
                material.SaturationFactor = 0f;
            }
        }
        else if (_isSelected)
        {
            // Tier 3: playable + selected -- golden highlight + hover brightness
            material.Color = new Color(1f, 0.85f, 0.3f, 1f);
            material.ColorFactor = 0.7f;
            material.ValueFactor = 40f;
        }
        else if (_isHovered)
        {
            // Tier 3: playable + hovered -- bright
            material.ValueFactor = 40f;
        }

        drawContext.DrawSprite(_loadedSprite, material, in AreaRect, _scaleToUse);

        // Pass 5: Rotating faction color pulse -- ONE playable region at a time glows
        // The spotlight rotates across all playable factions, highlighting each in turn
        if (_isPlayable && !_isSelected && !_isHovered &&
            _playableIndex >= 0 && _playableIndex == _globalPulseIndex &&
            _globalPulseAlpha > 0.01f && _factionColor.Alpha > 0f)
        {
            SimpleMaterial pulseMat = drawContext.CreateSimpleMaterial();
            pulseMat.OverlayEnabled = false;
            pulseMat.CircularMaskingEnabled = false;
            pulseMat.Texture = _loadedSprite.Texture;
            pulseMat.NinePatchParameters = _loadedSprite.NinePatchParameters;
            // Tint with faction color. Same brightening strategy as outline.
            pulseMat.Color = BrightenColor(_factionColor, 0.6f);
            pulseMat.ColorFactor = 1f;
            pulseMat.AlphaFactor = contextAlpha * _globalPulseAlpha * 0.5f;
            pulseMat.HueFactor = 0f;
            pulseMat.SaturationFactor = 0f;
            pulseMat.ValueFactor = 120f;

            drawContext.DrawSprite(_loadedSprite, pulseMat, in AreaRect, _scaleToUse);
        }

        // Pass 5b: Faction banner watermark overlay during pulse
        // Draws the faction's banner image centered on the region, synchronized with the pulse
        if (_isPlayable && !_isSelected && !_isHovered &&
            _playableIndex >= 0 && _playableIndex == _globalPulseIndex &&
            _globalPulseAlpha > 0.01f &&
            _bannerSprite?.Texture != null)
        {
            float widgetW = AreaRect.LocalScale.X;
            float widgetH = AreaRect.LocalScale.Y;
            if (widgetW > 0 && widgetH > 0)
            {
                // Banner display size preserving aspect ratio (84x128 source)
                float bannerAspect = (float)_bannerSprite.Height / _bannerSprite.Width;
                float displayW = Math.Min(BannerDisplaySize, widgetW * 0.9f);
                float displayH = displayW * bannerAspect;
                if (displayH > widgetH * 0.9f)
                {
                    displayH = widgetH * 0.9f;
                    displayW = displayH / bannerAspect;
                }

                float sX = displayW / widgetW;
                float sY = displayH / widgetH;
                float ofsX = (widgetW - displayW) * 0.5f;
                float ofsY = (widgetH - displayH) * 0.5f;

                SimpleMaterial bannerMat = drawContext.CreateSimpleMaterial();
                bannerMat.OverlayEnabled = false;
                bannerMat.CircularMaskingEnabled = false;
                bannerMat.Texture = _bannerSprite.Texture;
                bannerMat.NinePatchParameters = _bannerSprite.NinePatchParameters;
                bannerMat.Color = BrightenColor(_factionColor, 0.8f);
                bannerMat.ColorFactor = 1f;  // tint with brightened faction color
                bannerMat.AlphaFactor = contextAlpha * _globalPulseAlpha * 0.7f;
                bannerMat.HueFactor = 0f;
                bannerMat.SaturationFactor = 0f;
                bannerMat.ValueFactor = 80f;  // bright overlay

                Rectangle2D bannerRect = AreaRect;
                bannerRect.SetVisualScale(sX, sY);
                bannerRect.SetVisualOffset(ofsX, ofsY);
                bannerRect.ValidateVisuals();
                bannerRect.CalculateVisualMatrixFrame();
                drawContext.DrawSprite(_bannerSprite, bannerMat, in bannerRect, _scaleToUse);
            }
        }

        // Pass 6: Collect pin data for deferred rendering (drawn after all regions)
        // Pins always visible for playable regions (including when hovered/selected)
        if (_isPlayable &&
            _capitalNormX >= 0f && _capitalNormX <= 1f &&
            _capitalNormY >= 0f && _capitalNormY <= 1f)
        {
            float widgetW = AreaRect.LocalScale.X;
            float widgetH = AreaRect.LocalScale.Y;
            if (widgetW > 0 && widgetH > 0)
            {
                Color pinColor;
                switch (_factionSide)
                {
                    case "free":
                        pinColor = new Color(0.85f, 0.75f, 0.2f, 1f);
                        break;
                    case "evil":
                        pinColor = new Color(0.9f, 0.15f, 0.1f, 1f);
                        break;
                    default:
                        pinColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                        break;
                }
                // Store absolute screen coords for the pin
                float absX = GlobalPosition.X + _capitalNormX * widgetW;
                float absY = GlobalPosition.Y + _capitalNormY * widgetH + _hoverOffset;
                _pendingPins.Add(new PinRenderData
                {
                    ScreenX = absX,
                    ScreenY = absY,
                    PinColor = pinColor,
                    Alpha = contextAlpha * 0.9f
                });
            }
        }

        // Deferred pin rendering: the last instance in the list draws ALL collected pins
        // This ensures pins are always rendered on top of all region pieces
        if (_allInstances.Count > 0 && _allInstances[_allInstances.Count - 1] == this && _pendingPins.Count > 0)
        {
            TryLoadEmblem();
            if (_emblemSprite?.Texture != null)
            {
                foreach (var pin in _pendingPins)
                {
                    SimpleMaterial emblemMat = drawContext.CreateSimpleMaterial();
                    emblemMat.OverlayEnabled = false;
                    emblemMat.CircularMaskingEnabled = false;
                    emblemMat.Texture = _emblemSprite.Texture;
                    emblemMat.NinePatchParameters = _emblemSprite.NinePatchParameters;
                    emblemMat.Color = pin.PinColor;
                    emblemMat.ColorFactor = 1f;
                    emblemMat.AlphaFactor = pin.Alpha;
                    emblemMat.HueFactor = 0f;
                    emblemMat.SaturationFactor = 0f;
                    emblemMat.ValueFactor = 0f;

                    // Position the pin sprite at absolute screen coords
                    // We draw from this widget's AreaRect but offset to the pin's position
                    float ofsX = pin.ScreenX - GlobalPosition.X - EmblemSize * 0.5f;
                    float ofsY = pin.ScreenY - GlobalPosition.Y - _hoverOffset - EmblemSize * 0.5f;
                    float widgetW2 = AreaRect.LocalScale.X;
                    float widgetH2 = AreaRect.LocalScale.Y;
                    if (widgetW2 > 0 && widgetH2 > 0)
                    {
                        float sX = EmblemSize / widgetW2;
                        float sY = EmblemSize / widgetH2;

                        Rectangle2D emblemRect = AreaRect;
                        emblemRect.SetVisualScale(sX, sY);
                        emblemRect.SetVisualOffset(ofsX, ofsY);
                        emblemRect.ValidateVisuals();
                        emblemRect.CalculateVisualMatrixFrame();
                        drawContext.DrawSprite(_emblemSprite, emblemMat, in emblemRect, _scaleToUse);
                    }
                }
            }
            _pendingPins.Clear();
        }

        // Tooltip: update static property so the VM can show the colored tooltip.
        // Hover takes priority; pulsing region also shows its name.
        if (_isHovered && _hasFaction && !string.IsNullOrEmpty(_factionDisplayName))
        {
            HoveredFactionName = _factionDisplayName;
        }
        else if (_isPlayable && !_isHovered &&
                 _playableIndex >= 0 && _playableIndex == _globalPulseIndex &&
                 _globalPulseAlpha > 0.3f &&
                 _globalHovered == null &&
                 !string.IsNullOrEmpty(_factionDisplayName))
        {
            HoveredFactionName = _factionDisplayName;
        }
    }

    protected override void OnHoverBegin()
    {
        // Gauntlet hover is unreliable with overlapping BBoxes.
        // We handle hover entirely ourselves in ResolveGlobalHover().
        // Do NOT call base.OnHoverBegin().
    }

    protected override void OnHoverEnd()
    {
        // Hover is managed by ResolveGlobalHover() -- Gauntlet's HoverEnd
        // can fire when the cursor moves to an overlapping sibling's BBox,
        // even though the pixel under the cursor still belongs to us.
        // We ignore Gauntlet hover and manage _isHovered ourselves.
    }

    /// <summary>
    /// Resolve which PolygonWidget should be hovered based on alpha-tested hit.
    /// Called from every instance's OnLateUpdate (idempotent -- safe to call multiple times).
    /// Iterates all instances, checks mouse position against alpha maps,
    /// and assigns hover to the correct widget (the last one in the list
    /// that passes alpha check -- later widgets are rendered on top).
    /// </summary>
    private static void ResolveGlobalHover()
    {
        PolygonWidget bestCandidate = null;

        // Cleanup: remove widgets that were explicitly removed from the visual tree.
        // Only remove if the widget was registered (had a parent before) but now lost it.
        // Don't remove during initial frames where ParentWidget might not be set yet.
        for (int i = _allInstances.Count - 1; i >= 0; i--)
        {
            var inst = _allInstances[i];
            if (inst.ParentWidget == null && inst._textureLoaded)
            {
                _allInstances.RemoveAt(i);
                inst._registered = false; // allow re-registration if re-parented
            }
        }

        // Second pass: find hover candidate (forward -- last match = highest z-order wins)
        for (int i = 0; i < _allInstances.Count; i++)
        {
            var pw = _allInstances[i];
            if (!pw._hasFaction) continue;
            if (pw.Size.X <= 0 || pw.Size.Y <= 0) continue;

            // Check if mouse is within widget bounds
            Vector2 mousePos;
            try
            {
                mousePos = pw.EventManager.MousePosition;
            }
            catch
            {
                continue; // EventManager not ready
            }

            float localX = mousePos.X - pw.GlobalPosition.X;
            float localY = mousePos.Y - pw.GlobalPosition.Y;
            if (localX < 0 || localX >= pw.Size.X || localY < 0 || localY >= pw.Size.Y)
                continue;

            // Check alpha
            if (!pw.IsMouseOnOpaquePixel())
                continue;

            // This widget is a valid hover candidate.
            // Later widgets in the list (higher z-order) take priority.
            bestCandidate = pw;
        }

        // Apply hover state changes
        if (_globalHovered != bestCandidate)
        {
            // Un-hover the old widget
            if (_globalHovered != null && _globalHovered._isHovered)
            {
                _globalHovered._isHovered = false;
                _globalHovered.OnPropertyChanged(false, nameof(IsPolygonHovered));
                _globalHovered.EventFired("HoverEnd");
            }

            // Hover the new widget
            _globalHovered = bestCandidate;
            if (_globalHovered != null)
            {
                _globalHovered._isHovered = true;
                _globalHovered.OnPropertyChanged(true, nameof(IsPolygonHovered));
                _globalHovered.EventFired("HoverBegin");
                HoveredFactionName = _globalHovered._factionDisplayName;
            }
            else
            {
                HoveredFactionName = "";
            }
        }
    }

    private void TryLoadTexture()
    {
        if (_textureLoaded || _loadFailed || string.IsNullOrEmpty(_regionName))
            return;

        try
        {
            string modPath = FactionMapPaths.ModulePath;
            if (string.IsNullOrEmpty(modPath))
            {
                _loadFailed = true;
                return;
            }

            string texturePath = System.IO.Path.Combine(modPath, "GUI", "SpriteData", "FactionMap");
            string file = System.IO.Path.Combine(texturePath, $"region_{_regionName}.png");

            if (!System.IO.File.Exists(file))
            {
                _loadFailed = true;
                FactionMapPaths.LogError($"[{_regionName}] File not found: {file}");
                return;
            }

            string fileName = System.IO.Path.GetFileName(file);
            string folder = System.IO.Path.GetDirectoryName(file);

            EngineTexture engineTex = EngineTexture.LoadTextureFromPath(fileName, folder);
            if (engineTex == null)
            {
                _loadFailed = true;
                FactionMapPaths.LogError($"[{_regionName}] LoadTextureFromPath returned null");
                return;
            }

            var twoDimTex = new TwoDimTexture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engineTex));
            _loadedSprite = new RuntimeSprite(twoDimTex, twoDimTex.Width, twoDimTex.Height);
            Sprite = _loadedSprite;
            _textureLoaded = true;
            _texWidth = twoDimTex.Width;
            _texHeight = twoDimTex.Height;

            // Extract alpha map from PNG file for pixel-accurate hit testing
            try
            {
                _alphaMap = LoadAlphaMapFromPng(file, _texWidth, _texHeight);
            }
            catch (Exception alphaEx)
            {
                FactionMapPaths.LogError($"[{_regionName}] Alpha map failed: {alphaEx.Message}");
                _alphaMap = null; // fallback to bbox-only hit testing
            }

        }
        catch (Exception ex)
        {
            _loadFailed = true;
            FactionMapPaths.LogError($"[{_regionName}] Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Load the shared faction emblem sprite (static, loaded once for all instances).
    /// </summary>
    private static void TryLoadEmblem()
    {
        if (_emblemLoaded || _emblemLoadFailed) return;

        try
        {
            string modPath = FactionMapPaths.ModulePath;
            if (string.IsNullOrEmpty(modPath))
            {
                _emblemLoadFailed = true;
                return;
            }

            string file = System.IO.Path.Combine(modPath, "GUI", "SpriteData", "FactionMap", "faction_emblem.png");
            if (!System.IO.File.Exists(file))
            {
                _emblemLoadFailed = true;
                FactionMapPaths.LogError($"[Emblem] File not found: {file}");
                return;
            }

            string fileName = System.IO.Path.GetFileName(file);
            string folder = System.IO.Path.GetDirectoryName(file);

            EngineTexture engineTex = EngineTexture.LoadTextureFromPath(fileName, folder);
            if (engineTex == null)
            {
                _emblemLoadFailed = true;
                FactionMapPaths.LogError("[Emblem] LoadTextureFromPath returned null");
                return;
            }

            var twoDimTex = new TwoDimTexture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engineTex));
            _emblemSprite = new RuntimeSprite(twoDimTex, twoDimTex.Width, twoDimTex.Height);
            _emblemLoaded = true;
        }
        catch (Exception ex)
        {
            _emblemLoadFailed = true;
            FactionMapPaths.LogError($"[Emblem] Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Load the per-faction banner sprite for the pulse overlay.
    /// Each instance loads its own banner_&lt;factionId&gt;.png.
    /// </summary>
    private void TryLoadBanner()
    {
        if (_bannerLoaded || _bannerLoadFailed || string.IsNullOrEmpty(_factionId))
            return;

        try
        {
            string modPath = FactionMapPaths.ModulePath;
            if (string.IsNullOrEmpty(modPath))
            {
                _bannerLoadFailed = true;
                return;
            }

            string file = System.IO.Path.Combine(modPath, "GUI", "SpriteData", "FactionMap",
                $"banner_{_factionId}.png");
            if (!System.IO.File.Exists(file))
            {
                _bannerLoadFailed = true;
                return;
            }

            string fileName = System.IO.Path.GetFileName(file);
            string folder = System.IO.Path.GetDirectoryName(file);

            EngineTexture engineTex = EngineTexture.LoadTextureFromPath(fileName, folder);
            if (engineTex == null)
            {
                _bannerLoadFailed = true;
                return;
            }

            var twoDimTex = new TwoDimTexture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engineTex));
            _bannerSprite = new RuntimeSprite(twoDimTex, twoDimTex.Width, twoDimTex.Height);
            _bannerLoaded = true;
        }
        catch (Exception ex)
        {
            _bannerLoadFailed = true;
            FactionMapPaths.LogError($"[{_regionName}] Banner exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Load the alpha channel from a PNG file using System.Drawing.
    /// Returns a byte array (1 byte per pixel, row-major) or null on failure.
    /// The alpha map is downsampled by 4x in each dimension to save memory.
    /// Uses MAX alpha in each 4x4 block to avoid missing edge pixels.
    /// </summary>
    private static byte[] LoadAlphaMapFromPng(string pngPath, int texWidth, int texHeight)
    {
        const int DS = 4;
        int mapW = (texWidth + DS - 1) / DS;
        int mapH = (texHeight + DS - 1) / DS;

        using (var bmp = new System.Drawing.Bitmap(pngPath))
        {
            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                byte[] alphaMap = new byte[mapW * mapH];
                int stride = bmpData.Stride;
                IntPtr scan0 = bmpData.Scan0;

                unsafe
                {
                    byte* ptr = (byte*)scan0.ToPointer();
                    for (int my = 0; my < mapH; my++)
                    {
                        for (int mx = 0; mx < mapW; mx++)
                        {
                            byte maxAlpha = 0;
                            int startY = my * DS;
                            int startX = mx * DS;
                            int endY = Math.Min(startY + DS, bmp.Height);
                            int endX = Math.Min(startX + DS, bmp.Width);
                            for (int sy = startY; sy < endY; sy++)
                            {
                                for (int sx = startX; sx < endX; sx++)
                                {
                                    byte a = ptr[sy * stride + sx * 4 + 3];
                                    if (a > maxAlpha) maxAlpha = a;
                                }
                            }
                            alphaMap[my * mapW + mx] = maxAlpha;
                        }
                    }
                }

                return alphaMap;
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }
    }

    private static Color BrightenColor(Color c, float amount)
    {
        return new Color(
            c.Red + (1f - c.Red) * amount,
            c.Green + (1f - c.Green) * amount,
            c.Blue + (1f - c.Blue) * amount,
            c.Alpha);
    }

    private static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7)
            return new Color(1f, 1f, 1f, 1f);

        hex = hex.TrimStart('#');
        try
        {
            float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float a = hex.Length >= 8
                ? int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f
                : 1f;
            return new Color(r, g, b, a);
        }
        catch
        {
            return new Color(1f, 1f, 1f, 1f);
        }
    }

    private string PointsToString()
    {
        if (_pointCount == 0) return "";
        var parts = new string[_pointCount];
        for (int i = 0; i < _pointCount; i++)
            parts[i] = $"{_pointsX[i]:F3},{_pointsY[i]:F3}";
        return string.Join(";", parts);
    }

    private void ParsePoints(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _pointsX = Array.Empty<float>();
            _pointsY = Array.Empty<float>();
            _pointCount = 0;
            return;
        }

        var pointStrings = value.Split(';');
        var xList = new System.Collections.Generic.List<float>();
        var yList = new System.Collections.Generic.List<float>();

        foreach (var ps in pointStrings)
        {
            var coords = ps.Trim().Split(',');
            if (coords.Length >= 2 &&
                float.TryParse(coords[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(coords[1].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                xList.Add(x);
                yList.Add(y);
            }
        }

        _pointsX = xList.ToArray();
        _pointsY = yList.ToArray();
        _pointCount = _pointsX.Length;
    }

    private bool IsMouseOnOpaquePixel()
    {
        if (_alphaMap == null || _texWidth <= 0 || _texHeight <= 0)
            return true; // no alpha data -- fallback to bbox

        var mousePos = EventManager.MousePosition;
        float localX = mousePos.X - GlobalPosition.X;
        float localY = mousePos.Y - GlobalPosition.Y;

        float widgetW = Size.X;
        float widgetH = Size.Y;
        if (widgetW <= 0 || widgetH <= 0) return false;

        float normX = localX / widgetW;
        float normY = localY / widgetH;

        if (normX < 0f || normX >= 1f || normY < 0f || normY >= 1f)
            return false;

        const int DS = 4;
        int mapW = (_texWidth + DS - 1) / DS;
        int mapH = (_texHeight + DS - 1) / DS;

        int mapX = (int)(normX * mapW);
        int mapY = (int)(normY * mapH);
        mapX = Math.Max(0, Math.Min(mapX, mapW - 1));
        mapY = Math.Max(0, Math.Min(mapY, mapH - 1));

        int idx = mapY * mapW + mapX;
        if (idx < 0 || idx >= _alphaMap.Length)
            return false;

        return _alphaMap[idx] > AlphaThreshold;
    }

    protected override bool OnPreviewMousePressed()
    {
        // Tier 1 (no faction): completely inert, not clickable
        if (!_hasFaction) return false;

        // Alpha hit-test: only click if mouse is on an opaque pixel
        if (!IsMouseOnOpaquePixel())
            return false;

        var mousePos = EventManager.MousePosition;
        float localX = mousePos.X - GlobalPosition.X;
        float localY = mousePos.Y - GlobalPosition.Y;

        if (localX >= 0 && localX <= Size.X && localY >= 0 && localY <= Size.Y)
        {
            // Radio-button: deselect all sibling PolygonWidgets
            if (ParentWidget != null)
            {
                foreach (var child in ParentWidget.Children)
                {
                    if (child is PolygonWidget pw && pw != this && pw._isSelected)
                    {
                        pw._isSelected = false;
                        pw.OnPropertyChanged(false, nameof(IsPolygonSelected));
                    }
                }
            }

            // Select this one
            _isSelected = true;
            LastClickedRegionName = _regionName;
            OnPropertyChanged(true, nameof(IsPolygonSelected));
            EventFired("Click", Array.Empty<object>());
            return true;
        }

        return false;
    }
}
