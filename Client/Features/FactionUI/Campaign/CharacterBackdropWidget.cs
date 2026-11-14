using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    /// <summary>
    /// The character page's background image, contained in its parent and centred: the whole image shows,
    /// never cropped or stretched, and never enlarged past <see cref="MaxEnlargement"/> physical pixels per
    /// source pixel. A 2048 by 1152 image is 2560 by 1440 on 5120 by 1440 and on 3840 by 2160. The parent's
    /// Size and ScaledSuggestedWidth are both physical pixels, so the UI scale does not change the cap. The
    /// left and right fade children (dark ramps) are sized to <see cref="FadeWidth"/> of the image; the top and
    /// bottom ones, the same ramps turned 90 degrees, show only when the image is letterboxed.
    /// </summary>
    public class CharacterBackdropWidget : BrushWidget
    {
        internal const float MaxEnlargement = 1.25f;
        internal const float FadeFraction = 0.125f;
        private const float LayoutTolerance = 0.01f;

        // Set by Gauntlet from the prefab's widget references, before the first update.
        public Widget? LeftFadeWidget { get; set; }
        public Widget? RightFadeWidget { get; set; }
        public Widget? TopFadeWidget { get; set; }
        public Widget? BottomFadeWidget { get; set; }

        public CharacterBackdropWidget(UIContext context) : base(context) { }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (ParentWidget == null || Sprite == null)
                return;
            if (!TryFit(ParentWidget.Size.X, ParentWidget.Size.Y, Sprite.Width, Sprite.Height, MaxEnlargement,
                    out float width, out float height))
                return;
            ScaledSuggestedWidth = width;
            ScaledSuggestedHeight = height;
            float fade = FadeWidth(width);
            if (LeftFadeWidget != null) LeftFadeWidget.ScaledSuggestedWidth = fade;
            if (RightFadeWidget != null) RightFadeWidget.ScaledSuggestedWidth = fade;
            bool letterboxed = VerticalFade(ParentWidget.Size.Y, height, out float verticalFade, out float offset);
            PlaceVerticalFade(TopFadeWidget, letterboxed, verticalFade, width, -offset);
            PlaceVerticalFade(BottomFadeWidget, letterboxed, verticalFade, width, offset);
        }

        // The prefab turns these children 90 degrees about their centre, so the laid-out width is the fade's
        // height on screen and the laid-out height is the image width.
        private static void PlaceVerticalFade(Widget? fadeWidget, bool letterboxed, float fade, float imageWidth, float offset)
        {
            if (fadeWidget == null)
                return;
            fadeWidget.IsVisible = letterboxed;
            if (!letterboxed)
                return;
            fadeWidget.ScaledSuggestedWidth = fade;
            fadeWidget.ScaledSuggestedHeight = imageWidth;
            fadeWidget.ScaledPositionYOffset = offset;
        }

        /// <summary>
        /// True when dark shows above and below the image (a whole pixel or more). The fade is an eighth of the
        /// image height; offset moves a centred fade from the image centre to the band at the image's bottom edge.
        /// </summary>
        internal static bool VerticalFade(float containerHeight, float imageHeight, out float fade, out float offset)
        {
            fade = offset = 0f;
            if (!(imageHeight > 0f) || !(containerHeight - imageHeight >= 1f))
                return false;
            fade = FadeWidth(imageHeight);
            offset = (imageHeight - fade) / 2f;
            return true;
        }

        /// <summary>The largest whole-pixel size of the source inside the container, at most maxScale times the source.</summary>
        internal static bool TryFit(float containerWidth, float containerHeight, float sourceWidth, float sourceHeight,
            float maxScale, out float width, out float height)
        {
            width = height = 0f;
            if (!(containerWidth > 0f) || !(containerHeight > 0f) || !(sourceWidth > 0f) || !(sourceHeight > 0f)
                || !(maxScale > 0f) || float.IsInfinity(containerWidth) || float.IsInfinity(containerHeight))
                return false;
            float scale = Math.Min(maxScale, Math.Min(containerWidth / sourceWidth, containerHeight / sourceHeight));
            width = (float)Math.Floor(sourceWidth * scale + 0.5f);
            height = (float)Math.Floor(sourceHeight * scale + 0.5f);
            // Rounding up must not push past the container (a crop of one pixel). The tolerance keeps a column
            // that layout returns as 1919.99... for 1920 after the UI scale round trip.
            width = Math.Min(width, (float)Math.Floor(containerWidth + LayoutTolerance));
            height = Math.Min(height, (float)Math.Floor(containerHeight + LayoutTolerance));
            return width > 0f && height > 0f;
        }

        internal static float FadeWidth(float imageWidth) => (float)Math.Floor(imageWidth * FadeFraction + 0.5f);
    }
}
