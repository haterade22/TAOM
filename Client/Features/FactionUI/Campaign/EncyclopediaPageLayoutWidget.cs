using System;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    // Reflows the existing TAOM/native page widgets without replacing their bindings or commands.
    // Keeping the original hierarchy also preserves other mods' family, messenger and history additions.
    public class EncyclopediaPageLayoutWidget : Widget
    {
        // Set by Initialize, which Attach calls before the widget joins the tree.
        private Widget _canvas = null!, _body = null!;
        private Widget? _left, _history;
        private ImageWidget? _leftPlate, _bodyPlate, _historyPlate;
        public const float CanvasWidth = 1728f;
        public const float CanvasHeight = 780f;
        public const float ColumnGap = 24f;

        public EncyclopediaPageLayoutWidget(UIContext context) : base(context) { }

        public static bool IsDetailMovie(string name)
        {
            switch (name)
            {
                case "EncyclopediaHeroPage":
                case "EncyclopediaSettlementPage":
                case "EncyclopediaClanPage":
                case "EncyclopediaFactionPage":
                case "EncyclopediaUnitPage":
                case "EncyclopediaConceptPage":
                case "EncyclopediaShipPage": return true;
                default: return false;
            }
        }

        public static EncyclopediaPageLayoutWidget Attach(Widget root, bool portrait)
        {
            var columns = root.GetAllChildrenRecursive().OfType<ListPanel>().First(x =>
                x.ParentWidget.WidthSizePolicy == SizePolicy.Fixed && x.ParentWidget.SuggestedWidth > 800f);
            var layout = new EncyclopediaPageLayoutWidget(root.Context) { Id = "EncyclopediaPageLayout", DoNotAcceptEvents = true };
            layout.Initialize(columns, portrait);
            columns.ParentWidget.AddChild(layout);
            return layout;
        }

        private void Initialize(ListPanel columns, bool portrait)
        {
            _canvas = columns.ParentWidget;
            _canvas.Id = "EncyclopediaDetailCanvas";
            _canvas.WidthSizePolicy = SizePolicy.Fixed;
            _canvas.HeightSizePolicy = SizePolicy.Fixed;
            _canvas.SuggestedWidth = CanvasWidth;
            _canvas.SuggestedHeight = CanvasHeight;
            _canvas.HorizontalAlignment = HorizontalAlignment.Center;
            _canvas.VerticalAlignment = VerticalAlignment.Top;
            _canvas.MarginTop = 0;
            _canvas.PositionYOffset = 184f;

            _left = columns.Children.FirstOrDefault(x => !(x is ScrollbarWidget) && x.SuggestedWidth == 370f);
            if (_left != null)
            {
                _left.SuggestedWidth = 350f;
                _left.MarginRight = ColumnGap;
                _left.Sprite = null;
                // The native tableau viewport otherwise keeps its original fixed 370px width.
                if (portrait)
                {
                    var name = _left.Children.OfType<ListPanel>().FirstOrDefault(x => x.HeightSizePolicy == SizePolicy.CoverChildren && x.WidthSizePolicy == SizePolicy.StretchToParent);
                    if (name != null) name.MarginTop = 64f;
                    var bookmark = _left.FindChild("BookmarkButton", true);
                    if (bookmark != null)
                    {
                        bookmark.HorizontalAlignment = HorizontalAlignment.Center;
                        bookmark.MarginLeft = 0;
                        bookmark.MarginTop = 28f;
                        bookmark.SuggestedWidth = 32f;
                        bookmark.SuggestedHeight = 32f;
                    }
                    var tableau = _left.GetAllChildrenRecursive().FirstOrDefault(x => x.GetType().Name == "EncyclopediaCharacterTableauWidget");
                    if (tableau != null && tableau.ParentWidget.SuggestedWidth == 370f)
                    {
                        tableau.ParentWidget.WidthSizePolicy = SizePolicy.StretchToParent;
                        tableau.ParentWidget.MarginLeft = 10f;
                        tableau.ParentWidget.MarginRight = 10f;
                    }
                }
                _leftPlate = CreatePlate("EncyclopediaLeftPanel", portrait ? "ec_portrait_panel" : "ec_panel_9");
            }

            var right = columns.Children.Last(x => !(x is ScrollbarWidget) && x.WidthSizePolicy == SizePolicy.StretchToParent);
            var list = right.FindChild("RightSideList", true) as ListPanel;
            if (list != null)
            {
                list.MarginLeft = 0;
                list.MarginRight = 0;
                _body = list.Children.FirstOrDefault(x => x.WidthSizePolicy == SizePolicy.Fixed && x.SuggestedWidth >= 700f);
                if (_body != null)
                    _history = list.Children.FirstOrDefault(x => x != _body && x.WidthSizePolicy == SizePolicy.StretchToParent);
            }
            if (_body != null && _history != null)
            {
                _body.SuggestedWidth = 968f;
                _body.MarginRight = ColumnGap;
                _history.MarginLeft = 0;
                _history.MarginTop = 24f;
                _bodyPlate = CreatePlate("EncyclopediaReadingPanel", "ec_panel_9");
                _historyPlate = CreatePlate("EncyclopediaHistoryPanel", "ec_panel_9");
            }
            else
            {
                _body = right;
                _bodyPlate = CreatePlate("EncyclopediaReadingPanel", "ec_panel_9");
            }
            // Native vertical separators would sit in the newly open gaps between panels.
            foreach (var widget in columns.GetAllChildrenRecursive().Where(x => x.SuggestedWidth == 2f && x.Sprite != null && x.HeightSizePolicy == SizePolicy.StretchToParent))
                widget.IsVisible = false;
            // Native pages fade their old enclosing canvas across its full width, including our open panel gaps.
            foreach (var shadow in _canvas.GetAllChildrenRecursive().OfType<BrushWidget>())
            {
                var brush = shadow.ReadOnlyBrush;
                while (brush.ClonedFrom != null) brush = brush.ClonedFrom;
                if (brush.Name == "Encyclopedia.Scroll.Shadow") shadow.IsVisible = false;
            }
        }

        private ImageWidget CreatePlate(string id, string sprite)
        {
            var plate = new EncyclopediaStonePanelWidget(Context) { Id = id, Sprite = Context.SpriteData.GetSprite(sprite), DoNotAcceptEvents = true };
            _canvas.AddChildAtIndex(plate, 0);
            return plate;
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            // Same frame anchor as the TAOM detail pages, so the header stays clear (#1212).
            if (EncyclopediaFrameAnchorWidget.TryLogicalHeight(EventManager.PageSize.Y, _inverseScaleToUse, out float height))
                _canvas.PositionYOffset = EncyclopediaFrameAnchorWidget.Offset(height, EncyclopediaFrameAnchorWidget.PageTop, CanvasHeight, EncyclopediaFrameAnchorWidget.PageClearance);
            if (_body.Size.X <= 0) return;
            Fit(_leftPlate, _left, 0f, CanvasHeight);
            Fit(_bodyPlate, _body, 0f, CanvasHeight);
            Fit(_historyPlate, _history, 24f, CanvasHeight - 24f);
        }

        private void Fit(ImageWidget? plate, Widget? content, float top, float height)
        {
            if (plate == null || content == null) return;
            plate.PositionXOffset = (content.GlobalPosition.X - _canvas.GlobalPosition.X) / Context.Scale;
            plate.PositionYOffset = top;
            plate.SuggestedWidth = content.Size.X / Context.Scale;
            plate.SuggestedHeight = height;
        }
    }
}
