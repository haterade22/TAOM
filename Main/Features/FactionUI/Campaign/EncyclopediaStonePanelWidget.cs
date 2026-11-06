extern alias SNV;
using System;
using Vector2 = SNV::System.Numerics.Vector2;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>Draws continuous stone inside a panel mask, with the gold rim above it.</summary>
public sealed class EncyclopediaStonePanelWidget : ImageWidget
{
    private readonly Brush _material = new Brush();
    private readonly BrushRenderer _renderer = new BrushRenderer();
    private ulong _renderFrame;

    public EncyclopediaStonePanelWidget(UIContext context) : base(context)
    {
        _renderer.Brush = _material;
        _renderer.CurrentState = "Default";
        _material.DefaultLayer.OverlayMethod = BrushOverlayMethod.CoverWithTexture;
        _material.DefaultLayer.UseOverlayAlphaAsMask = true;
    }

    protected override void OnRender(TwoDimensionContext context, TwoDimensionDrawContext drawContext)
    {
        if (Sprite == null || Size.X <= 0 || Size.Y <= 0)
            return;

        string mask = Sprite.Name == "ec_portrait_panel" ? "ec_portrait_mask"
            : Sprite.Name == "ec_header" ? "ec_header_mask" : "ec_panel_mask_9";
        float width = Size.X * _inverseScaleToUse;
        float height = Size.Y * _inverseScaleToUse;
        var narrow = Context.SpriteData.GetSprite("ec_stone");
        var wide = Context.SpriteData.GetSprite("ec_stone_landscape");
        var stone = narrow == null ? wide : wide == null ? narrow
            : UseWideStone(width, height, narrow.Width, narrow.Height, wide.Width, wide.Height) ? wide : narrow;
        _material.DefaultLayer.Sprite = Context.SpriteData.GetSprite(mask);
        _material.DefaultLayer.OverlaySprite = stone;
        if (stone != null && _material.DefaultLayer.Sprite != null)
        {
            float cover = CoverScale(width, height, stone.Width, stone.Height);
            var imageSize = new Vector2(stone.Width * cover, stone.Height * cover);
            var offset = (imageSize - new Vector2(width, height)) * 0.5f;
            _renderer.Update(++_renderFrame, 0f, 0f);
            _renderer.Render(drawContext, in AreaRect, _scaleToUse, Context.ContextAlpha,
                offset, imageSize);
        }

        base.OnRender(context, drawContext);
    }

    /// <summary>Uniform scale that makes a source cover the panel without changing its proportions.</summary>
    internal static float CoverScale(float width, float height, float sourceWidth, float sourceHeight)
        => Math.Max(width / sourceWidth, height / sourceHeight);

    /// <summary>
    /// Picks the source that covers the panel with the least magnification. A near-square panel
    /// (the 772 by 798 list results) is narrower than tall, yet the narrow crop must be enlarged
    /// about 1.2 times to cover it while the wide continuation is reduced, so aspect alone is wrong.
    /// </summary>
    internal static bool UseWideStone(float width, float height,
        float narrowWidth, float narrowHeight, float wideWidth, float wideHeight)
        => CoverScale(width, height, wideWidth, wideHeight)
            < CoverScale(width, height, narrowWidth, narrowHeight);
}
