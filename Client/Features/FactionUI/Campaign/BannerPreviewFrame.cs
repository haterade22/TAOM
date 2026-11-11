using TaleWorlds.Core;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>
/// The banner editor's canvas preview: a viewport onto the tableau, which draws the whole 1528 canvas (nine grid). Each
/// frame shows the part of the canvas one of the game's banner renders shows, at the size that render crops it to.
/// </summary>
public readonly struct BannerPreviewFrame
{
    /// <summary>The viewport's height in the editor; both frames keep it so the toggle never moves the layout.</summary>
    public const float ViewHeight = 330f;

    /// <summary>The encyclopedia pennant's proportions: the ec_hero_clan_* art and every pennant frame that uses it.</summary>
    public const float PennantArtWidth = 408f, PennantArtHeight = 664f;

    /// <summary>The Zoom every encyclopedia pennant gives its EncyclopediaMaskedImageWidget.</summary>
    public const float PennantZoom = 1.35f;

    private BannerPreviewFrame(float viewWidth, float canvasSize)
    {
        ViewWidth = viewWidth;
        CanvasSize = canvasSize;
    }

    /// <summary>
    /// Shields, flags and the map: banner textures are drawn by the default banner camera, the middle 512 of the canvas
    /// (BannerTextureCreator), so a square viewport shows that third.
    /// </summary>
    public static BannerPreviewFrame Shield { get; } =
        new(ViewHeight, ViewHeight * Banner.BannerFullSize / Banner.BannerEditableAreaSize);

    /// <summary>
    /// The encyclopedia pennant: a nine grid banner image (the whole canvas, square) cover-fitted to the 408:664 frame and
    /// zoomed 1.35 (EncyclopediaMaskedImageWidget), so the frame's height spans 1/1.35 of the canvas and its width
    /// (408/664)/1.35. That is about 696 x 1132 of 1528: wider and much taller than the shield's 512.
    /// </summary>
    public static BannerPreviewFrame Pennant { get; } =
        new(ViewHeight * PennantArtWidth / PennantArtHeight, ViewHeight * PennantZoom);

    public float ViewWidth { get; }

    /// <summary>The tableau's width and height: the whole canvas at the scale the viewport shows it.</summary>
    public float CanvasSize { get; }

    /// <summary>How many canvas units across and down the viewport shows.</summary>
    public (float Width, float Height) VisibleCanvas =>
        (ViewWidth / CanvasSize * Banner.BannerFullSize, ViewHeight / CanvasSize * Banner.BannerFullSize);
}
