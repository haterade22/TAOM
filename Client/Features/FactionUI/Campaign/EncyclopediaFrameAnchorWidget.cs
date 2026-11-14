using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    /// <summary>
    /// Places a top-aligned encyclopedia canvas against the shared 1728 by 972 frame, so the bar movie and the
    /// page movie under it agree at every viewport height (#1212). The frame is centred, as the bar always was, but
    /// raised when the viewport is too short to keep the page navigation 22 above the bottom, and never above the
    /// top. <see cref="FrameOffset"/> is the canvas top inside the frame (logical units); a positive
    /// <see cref="BottomClearance"/> also keeps the canvas that far above the viewport bottom.
    /// The prefab's PositionYOffset should be the 1080 result, which shows until the first late update.
    /// Below a 938 logical height the 780 page, the header and the navigation cannot all fit: the page bottom then
    /// runs under the navigation row (30 at 900).
    /// </summary>
    public class EncyclopediaFrameAnchorWidget : Widget
    {
        internal const float FrameHeight = 972f;
        internal const float PageTop = 130f;
        internal const float NavigationTop = 956f;
        // The page keeps 8 above the navigation row (48) and its 22 margin.
        internal const float PageClearance = 78f;
        internal const float HeaderClearance = 80f;
        private const float NavigationHeight = 48f;
        private const float NavigationMargin = 22f;
        private const float DesignHeight = 1080f;

        public float FrameOffset { get; set; }
        public float BottomClearance { get; set; }

        public EncyclopediaFrameAnchorWidget(UIContext context) : base(context) { }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            if (TryLogicalHeight(EventManager.PageSize.Y, _inverseScaleToUse, out float height))
                PositionYOffset = Offset(height, FrameOffset, SuggestedHeight, BottomClearance);
        }

        /// <summary>The frame top for a logical viewport height; the 1080 layout when the height is unusable.</summary>
        internal static float FrameTop(float viewportHeight)
        {
            if (!(viewportHeight > 0f) || float.IsInfinity(viewportHeight))
                viewportHeight = DesignHeight;
            float centred = (viewportHeight - FrameHeight) / 2f;
            float aboveNavigationMargin = viewportHeight - (NavigationTop + NavigationHeight + NavigationMargin);
            return Math.Max(0f, Math.Min(centred, aboveNavigationMargin));
        }

        /// <summary>
        /// The canvas top. A clearance only binds below a 1026 logical height (a UI scale above about 1.05 at 16:9);
        /// it raises the canvas, but never above <see cref="HeaderClearance"/>, just under the header.
        /// </summary>
        internal static float Offset(float viewportHeight, float frameOffset, float height, float bottomClearance)
        {
            float frameTop = FrameTop(viewportHeight);
            float top = frameTop + frameOffset;
            if (bottomClearance > 0f && viewportHeight > 0f && !float.IsInfinity(viewportHeight))
                top = Math.Max(frameTop + Math.Min(frameOffset, HeaderClearance), Math.Min(top, viewportHeight - bottomClearance - height));
            return top;
        }

        /// <summary>Physical pixels to this widget's logical units; false before the first layout.</summary>
        internal static bool TryLogicalHeight(float physicalHeight, float inverseScale, out float logicalHeight)
        {
            logicalHeight = physicalHeight * inverseScale;
            return physicalHeight > 0f && inverseScale > 0f && logicalHeight > 0f && !float.IsInfinity(logicalHeight);
        }
    }
}
