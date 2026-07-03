using System;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

using EngineTexture = TaleWorlds.Engine.Texture;
using TwoDimTexture = TaleWorlds.TwoDimension.Texture;

namespace TAOM.Features.FactionMap.Widgets;

public class BannerWidget : ImageWidget
{
    private float _targetX = -1f;
    private float _targetY = -1f;
    private float _currentX = -1f;
    private float _currentY = -1f;

    private float _alpha;
    private bool _wantVisible;
    private const float FadeSpeed = 5f;

    private float _bannerWidth = 48f;
    private float _bannerHeight = 64f;

    private Color _factionColor = new Color(1f, 1f, 1f, 1f);
    private string _bannerColorHexStr = "#FFFFFFFF";

    private bool _textureLoaded;
    private bool _loadFailed;
    private Sprite _loadedSprite;
    private string _bannerImage = "";

    private float _stampProgress;
    private const float StampSpeed = 6f;

    private string _bannerSide = "neutral";
    private Color _glowColor = new Color(1f, 1f, 1f, 0f);

    private const float DisplayScale = 0.76f;

    public BannerWidget(UIContext context) : base(context)
    {
        OverrideDefaultStateSwitchingEnabled = true;
    }

    [Editor(false)]
    public float BannerPosX
    {
        get => _targetX;
        set
        {
            if (Math.Abs(_targetX - value) > 0.0001f)
            {
                _targetX = value;
                _wantVisible = _targetX >= 0f && _targetY >= 0f;
                if (_wantVisible)
                {
                    _currentX = _targetX;
                    _stampProgress = 0f;
                }
                OnPropertyChanged(value, nameof(BannerPosX));
            }
        }
    }

    [Editor(false)]
    public float BannerPosY
    {
        get => _targetY;
        set
        {
            if (Math.Abs(_targetY - value) > 0.0001f)
            {
                _targetY = value;
                _wantVisible = _targetX >= 0f && _targetY >= 0f;
                if (_wantVisible)
                {
                    _currentY = _targetY;
                    _stampProgress = 0f;
                }
                OnPropertyChanged(value, nameof(BannerPosY));
            }
        }
    }

    [Editor(false)]
    public string BannerColorHex
    {
        get => _bannerColorHexStr;
        set
        {
            if (_bannerColorHexStr != value)
            {
                _bannerColorHexStr = value;
                _factionColor = ParseHexColor(value);
                OnPropertyChanged(value, nameof(BannerColorHex));
            }
        }
    }

    [Editor(false)]
    public string BannerImage
    {
        get => _bannerImage;
        set
        {
            if (_bannerImage != value)
            {
                _bannerImage = value;
                _textureLoaded = false;
                _loadFailed = false;
                _loadedSprite = null;
                Sprite = null;
                OnPropertyChanged(value, nameof(BannerImage));
            }
        }
    }

    [Editor(false)]
    public string BannerSide
    {
        get => _bannerSide;
        set
        {
            if (_bannerSide != value)
            {
                _bannerSide = value ?? "neutral";
                _glowColor = _bannerSide switch
                {
                    "free" => new Color(1.0f, 0.85f, 0.2f, 1f),
                    "evil" => new Color(1.0f, 0.15f, 0.1f, 1f),
                    _ => new Color(1f, 1f, 1f, 0f),
                };
                OnPropertyChanged(value, nameof(BannerSide));
            }
        }
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        float targetAlpha = _wantVisible ? 1f : 0f;
        if (Math.Abs(_alpha - targetAlpha) > 0.001f)
        {
            _alpha += Math.Sign(targetAlpha - _alpha) * FadeSpeed * dt;
            _alpha = Math.Max(0f, Math.Min(1f, _alpha));
        }
        else
        {
            _alpha = targetAlpha;
        }

        this.AlphaFactor = _alpha;

        if (!_wantVisible && _alpha <= 0.001f)
        {
            _currentX = -1f;
            _currentY = -1f;
        }

        if (_wantVisible && _stampProgress < 1f)
        {
            _stampProgress += StampSpeed * dt;
            if (_stampProgress > 1f) _stampProgress = 1f;
        }

        if (_wantVisible && _targetX >= 0f && _targetY >= 0f)
        {
            _currentX = _targetX;
            _currentY = _targetY;
        }

        if (ParentWidget != null && _currentX >= 0f && _currentY >= 0f)
        {
            float parentW = ParentWidget.Size.X;
            float parentH = ParentWidget.Size.Y;
            if (parentW > 0 && parentH > 0)
            {
                float displayW = _bannerWidth * DisplayScale;
                float displayH = _bannerHeight * DisplayScale;
                ScaledSuggestedWidth = displayW;
                ScaledSuggestedHeight = displayH;

                float baseX = _currentX * parentW - displayW * 0.5f;
                float baseY = _currentY * parentH - displayH;

                float t = _stampProgress;
                float eased = t * (2f - t);
                float stampOffset = (1f - eased) * -150f;

                ScaledPositionXOffset = baseX;
                ScaledPositionYOffset = baseY + stampOffset;
            }
        }
        else
        {
            ScaledSuggestedWidth = 0f;
            ScaledSuggestedHeight = 0f;
        }

        TryLoadTexture();
    }

    protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
    {
        if (_loadedSprite?.Texture != null && _glowColor.Alpha > 0.01f && _alpha > 0.01f)
        {
            float contextAlpha = _alpha * Context.ContextAlpha;
            const float glowAlpha = 0.55f;
            const float glowExpand = 4f;

            // AUDIT-NOTE: #169 audit recommendation to hoist SimpleMaterial allocation would BREAK
            // rendering (TwoDimensionDrawData holds material by REFERENCE; queued draws read CURRENT
            // values at end-of-frame; sharing one material across the 8 glow-offset iterations
            // would make every queued draw read the LAST iteration's offset color).  If perf
            // becomes profiler-measurable, use a SimpleMaterial pool indexed by (color, alpha)
            // tuple — not a hoist.  See feedback_audit_findings_not_always_correct.md.
            float[] offsets = { -glowExpand, 0f, glowExpand };
            foreach (float ox in offsets)
            {
                foreach (float oy in offsets)
                {
                    if (ox == 0f && oy == 0f) continue;

                    SimpleMaterial glowMat = drawContext.CreateSimpleMaterial();
                    glowMat.OverlayEnabled = false;
                    glowMat.CircularMaskingEnabled = false;
                    glowMat.Texture = _loadedSprite.Texture;
                    glowMat.NinePatchParameters = _loadedSprite.NinePatchParameters;
                    glowMat.Color = _glowColor;
                    glowMat.ColorFactor = 1f;
                    glowMat.AlphaFactor = contextAlpha * glowAlpha * (1f / 8f);
                    glowMat.HueFactor = 0f;
                    glowMat.SaturationFactor = 0f;
                    glowMat.ValueFactor = 20f;

                    Rectangle2D glowRect = AreaRect;
                    glowRect.SetVisualOffset(ox, oy);
                    glowRect.ValidateVisuals();
                    glowRect.CalculateVisualMatrixFrame();
                    drawContext.DrawSprite(_loadedSprite, glowMat, in glowRect, _scaleToUse);
                }
            }
        }

        base.OnRender(twoDimensionContext, drawContext);
    }

    private void TryLoadTexture()
    {
        if (_textureLoaded || _loadFailed || string.IsNullOrEmpty(_bannerImage))
            return;

        try
        {
            string modPath = FactionMapPaths.ModulePath;
            if (string.IsNullOrEmpty(modPath)) { _loadFailed = true; return; }

            string texturePath = System.IO.Path.Combine(modPath, "GUI", "SpriteData", "FactionMap");
            string file = System.IO.Path.Combine(texturePath, $"{_bannerImage}.png");

            if (!System.IO.File.Exists(file))
            {
                _loadFailed = true;
                // DEBUG (not ERROR): missing PNG is a recoverable race against data binding.
                // The setter resets _loadFailed when BannerImage changes, so a real bound name
                // gets a second chance to load. ERROR misled log readers into thinking the
                // map was broken when it wasn't.
                FactionMapPaths.LogDebug($"[Banner] File not found: {file}");
                return;
            }

            string fileName = System.IO.Path.GetFileName(file);
            string folder = System.IO.Path.GetDirectoryName(file);

            EngineTexture engineTex = EngineTexture.LoadTextureFromPath(fileName, folder);
            if (engineTex == null)
            {
                _loadFailed = true;
                FactionMapPaths.LogError($"[Banner] LoadTextureFromPath returned null");
                return;
            }

            _loadedSprite = RuntimeSpriteFactory.FromEngineTexture(engineTex);
            Sprite = _loadedSprite;
            _textureLoaded = true;

            _bannerWidth = _loadedSprite.Width;
            _bannerHeight = _loadedSprite.Height;
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            FactionMapPaths.LogError($"[Banner] Exception: {ex.Message}");
        }
    }

    protected override void OnHoverBegin() { }
    protected override void OnHoverEnd() { }

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
}
