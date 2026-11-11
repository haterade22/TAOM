extern alias SNV;
using TAOM.Adapters;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.TwoDimension;
using Vector2 = SNV::System.Numerics.Vector2;

namespace TAOM.Features.FactionUI.UI.Widgets;

/// <summary>
/// The banner editor's live tableau, which can draw itself through its brush's mask the way the encyclopedia pennants
/// draw a banner (EncyclopediaMaskedImageWidget). Masked, the canvas is drawn into its parent's rect, the editor's
/// clipping viewport, centred like the tableau itself, so the mask covers exactly what the viewport shows. Unmasked it
/// is the vanilla BannerTableauWidget; it stays one, so the vanilla BannerBuilderEditableAreaWidget still drives it.
/// </summary>
public class MaskedBannerTableauWidget : BannerTableauWidget
{
    private Texture? _maskedTexture;
    private FrontEndRuntimeSprite? _maskedSprite;
    private float _frameDt;

    public MaskedBannerTableauWidget(UIContext context) : base(context) { }

    public bool IsMasked { get; set; }

    protected override void OnUpdate(float dt)
    {
        _frameDt = dt;
        base.OnUpdate(dt);
    }

    protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
    {
        var frame = ParentWidget;
        if (!IsMasked || frame == null || Brush?.DefaultLayer?.Sprite == null)
        {
            base.OnRender(twoDimensionContext, drawContext);
            return;
        }

        _isRenderRequestedPreviousFrame = true;
        if (TextureProvider == null)
            return;
        var texture = TextureProvider.GetTextureForRender(twoDimensionContext);
        Texture = texture;
        if (texture == null || !texture.IsValid || Size.X <= 0 || Size.Y <= 0 || frame.Size.X <= 0 || frame.Size.Y <= 0)
            return;

        if (texture != _maskedTexture)
        {
            _maskedTexture = texture;
            _maskedSprite = new FrontEndRuntimeSprite("BannerEditorMaskedCanvas", texture, SpriteNinePatchParameters.Empty);
        }

        var layer = Brush.DefaultLayer;
        layer.OverlayMethod = BrushOverlayMethod.CoverWithTexture;
        layer.UseOverlayAlphaAsMask = true;
        layer.OverlaySprite = _maskedSprite;
        UpdateBrushRendererInternal(_frameDt);

        float canvasWidth = Size.X * _inverseScaleToUse, canvasHeight = Size.Y * _inverseScaleToUse;
        float viewWidth = frame.Size.X * _inverseScaleToUse, viewHeight = frame.Size.Y * _inverseScaleToUse;
        var offset = new Vector2((canvasWidth - viewWidth) * 0.5f, (canvasHeight - viewHeight) * 0.5f);
        BrushRenderer.Render(drawContext, in frame.AreaRect, _scaleToUse, Context.ContextAlpha, offset,
            new Vector2(canvasWidth, canvasHeight));
    }
}
