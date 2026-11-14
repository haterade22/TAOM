using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    /// <summary>
    /// The encyclopedia bar's root (#1212). The native EncyclopediaData loads the bar movie first and each home, list
    /// or detail movie after it on the same layer, and Gauntlet renders and hit-tests the layer root's children in
    /// list order, so every page covered the header and the search results. This widget keeps its own movie node
    /// the last child of the layer root and moves <see cref="BackdropWidget"/> (the dimmer) to the first, under every
    /// page: the header, its buttons and the search results then sit above the page, and clicks outside every page
    /// still reach the dimmer, which closes the search results as the old dimmer root did. The checks run in the late
    /// update, before the frame's last layout and its render, and cost two index reads when nothing changed.
    /// </summary>
    public class EncyclopediaOverlayWidget : Widget
    {
        private Widget? _movieNode;

        // Set by Gauntlet from the prefab's widget reference, before the first update.
        public Widget? BackdropWidget { get; set; }

        public EncyclopediaOverlayWidget(UIContext context) : base(context) { }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            Widget layerRoot = EventManager.Root;
            if (_movieNode == null || _movieNode.ParentWidget != layerRoot)
                _movieNode = TopLevelNode(this, layerRoot);
            if (_movieNode == null)
                return;

            if (BackdropWidget != null)
                KeepUnderEveryMovie(BackdropWidget, layerRoot, EventManager.PageSize.X, EventManager.PageSize.Y);
            int last = layerRoot.ChildCount - 1;
            if (_movieNode.GetSiblingIndex() != last)
                _movieNode.SetSiblingIndex(last);
        }

        protected override void OnDisconnectedFromRoot()
        {
            base.OnDisconnectedFromRoot();
            // The dimmer lives outside this movie once moved; it leaves with the bar, and no reference outlives it.
            if (BackdropWidget != null && BackdropWidget.ParentWidget == EventManager.Root)
                BackdropWidget.ParentWidget = null;
            _movieNode = null;
        }

        private static Widget? TopLevelNode(Widget widget, Widget layerRoot)
        {
            Widget? node = widget;
            while (node != null && node.ParentWidget != layerRoot)
                node = node.ParentWidget;
            return node;
        }

        // The layer root keeps a fixed design size, so the dimmer is sized to the viewport in pixels, as GauntletMovie
        // sizes each movie node.
        private static void KeepUnderEveryMovie(Widget backdrop, Widget layerRoot, float width, float height)
        {
            if (backdrop.ParentWidget != layerRoot)
            {
                backdrop.WidthSizePolicy = SizePolicy.Fixed;
                backdrop.HeightSizePolicy = SizePolicy.Fixed;
                layerRoot.AddChildAtIndex(backdrop, 0);
            }
            else if (backdrop.GetSiblingIndex() != 0)
                backdrop.SetSiblingIndex(0);
            if (width > 0f && height > 0f)
            {
                backdrop.ScaledSuggestedWidth = width;
                backdrop.ScaledSuggestedHeight = height;
            }
        }
    }
}
