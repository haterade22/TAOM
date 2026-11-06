extern alias SNV;
using System;
using TAOM.Adapters;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;
using Vector2 = SNV::System.Numerics.Vector2;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>Centers a live image in an alpha mask using its actual texture proportions.</summary>
public sealed class EncyclopediaMaskedImageWidget : MaskedTextureWidget
{
    private Texture? _imageTexture;
    private FrontEndRuntimeSprite? _imageSprite;
    private float _frameDt;

    public float Zoom { get; set; } = 1f;

    public EncyclopediaMaskedImageWidget(UIContext context) : base(context) { }

    internal static (float Width, float Height) CalculateOverlaySize(
        float sourceWidth, float sourceHeight, float width, float height, float zoom)
    {
        float scale = Math.Max(width / sourceWidth, height / sourceHeight) * zoom;
        return (sourceWidth * scale, sourceHeight * scale);
    }

    public override void OnClearTextureProvider()
    {
        _imageTexture = null;
        _imageSprite = null;
        base.OnClearTextureProvider();
    }

    protected override void OnUpdate(float dt)
    {
        _frameDt = dt;
        base.OnUpdate(dt);
    }

    protected override void OnRender(TwoDimensionContext context, TwoDimensionDrawContext drawContext)
    {
        _isRenderRequestedPreviousFrame = true;
        var texture = TextureProvider?.GetTextureForRender(context);
        if (texture == null || !texture.IsValid || Size.X <= 0 || Size.Y <= 0)
            return;

        if (texture != _imageTexture)
        {
            _imageTexture = texture;
            _imageSprite = new FrontEndRuntimeSprite("EncyclopediaLiveImage", texture,
                SpriteNinePatchParameters.Empty);
        }

        var layer = Brush.DefaultLayer;
        layer.OverlayMethod = BrushOverlayMethod.CoverWithTexture;
        layer.UseOverlayAlphaAsMask = true;
        layer.OverlaySprite = _imageSprite;
        UpdateBrushRendererInternal(_frameDt);

        float width = Size.X * _inverseScaleToUse;
        float height = Size.Y * _inverseScaleToUse;
        var imageSize = CalculateOverlaySize(texture.Width, texture.Height, width, height,
            Math.Max(1f, Zoom));
        var offset = new Vector2((imageSize.Width - width) * 0.5f,
            (imageSize.Height - height) * 0.5f);
        var overlaySize = new Vector2(imageSize.Width, imageSize.Height);
        BrushRenderer.Render(drawContext, in AreaRect, _scaleToUse, Context.ContextAlpha,
            offset, overlaySize);
    }
}
