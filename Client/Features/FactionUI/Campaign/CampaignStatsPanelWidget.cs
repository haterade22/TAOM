using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    // Presentation only: both repeaters still bind the native MapInfoItemVM instances.
    public class CampaignStatsPanelWidget : Widget
    {
        public const float PanelHeight = 168f;
        public const float LowerPlateHeight = 94f;
        public const float SlideDistance = 64f;
        public const float TransitionSeconds = 0.32f;
        public bool IsInfoBarExtended { get; set; }
        // Set by Gauntlet from the prefab's widget references, before the first update.
        public Widget UpperPlate { get; set; } = null!;
        public Widget LowerPlate { get; set; } = null!;
        public Widget JoinBorderWidget { get; set; } = null!;
        public Widget RightJoinWidget { get; set; } = null!;
        public Widget PrimaryItemsWidget { get; set; } = null!;
        public Widget SecondaryItemsWidget { get; set; } = null!;
        public Widget ExpandButtonWidget { get; set; } = null!;
        public float Expansion { get; private set; }
        private float _progress;

        public CampaignStatsPanelWidget(UIContext context) : base(context) { }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            float direction = IsInfoBarExtended ? 1f : -1f;
            _progress = Math.Max(0f, Math.Min(1f, _progress + direction * dt / TransitionSeconds));
            Expansion = _progress * _progress * (3f - 2f * _progress);
            float upperY = SlideDistance * (1f - Expansion);
            float lowerY = PanelHeight - LowerPlateHeight * Expansion;
            UpperPlate.PositionYOffset = upperY;
            LowerPlate.PositionYOffset = lowerY;
            LowerPlate.IsVisible = Expansion > 0f;
            JoinBorderWidget.PositionYOffset = upperY + 84f;
            JoinBorderWidget.IsVisible = Expansion > 0f;
            RightJoinWidget.PositionYOffset = upperY + 64f;
            RightJoinWidget.IsVisible = Expansion > 0f;
            ExpandButtonWidget.PositionYOffset = upperY + 36f;
            // Native flips the entire brush; this clasp keeps its mounting pin at the top.
            var toggle = (ButtonWidget)ExpandButtonWidget;
            var toggleSprite = Context.SpriteData.GetSprite(IsInfoBarExtended ? "cb_stats_toggle_open" : "cb_stats_toggle_closed");
            foreach (var layer in toggle.Brush.Layers) { layer.Sprite = toggleSprite; layer.VerticalFlip = false; }
            foreach (Style style in toggle.Brush.Styles)
                for (int i = 0; i < style.LayerCount; i++)
                {
                    style.GetLayer(i).Sprite = toggleSprite;
                    style.GetLayer(i).VerticalFlip = false;
                }
            // The native lists split the lower stats across both repeaters, and the currency mixin adds
            // or removes one at runtime, so the shown set is gathered from both before placing any.
            int lowerMask = 0;
            foreach (Widget widget in PrimaryItemsWidget.Children)
                lowerMask = CampaignStatsLayout.Include(lowerMask, ((CampaignStatItemWidget)widget).StatId);
            foreach (Widget widget in SecondaryItemsWidget.Children)
                lowerMask = CampaignStatsLayout.Include(lowerMask, ((CampaignStatItemWidget)widget).StatId);
            LayoutItems(PrimaryItemsWidget, upperY, lowerY, lowerMask);
            LayoutItems(SecondaryItemsWidget, upperY, lowerY, lowerMask);
        }

        private void LayoutItems(Widget repeater, float upperY, float lowerY, int lowerMask)
        {
            foreach (Widget widget in repeater.Children)
            {
                var item = (CampaignStatItemWidget)widget;
                int slot = CampaignStatsLayout.Slot(item.StatId);
                bool lower = CampaignStatsLayout.IsLower(slot);
                item.IsVisible = slot >= 0 && (!lower || Expansion > 0f);
                item.IsDisabled = lower && _progress < 1f;
                if (slot < 0)
                    continue;
                var cell = lower ? CampaignStatsLayout.Lower(slot, lowerMask) : CampaignStatsLayout.Upper(slot);
                item.PositionXOffset = cell.X;
                item.PositionYOffset = (lower ? lowerY : upperY) + cell.Top;
                item.SuggestedWidth = cell.Width;
                item.SuggestedHeight = cell.Height;
                item.IconWidget.MarginTop = 0f;
                item.IconWidget.SuggestedWidth = cell.IconSize;
                item.IconWidget.SuggestedHeight = cell.IconSize;
                item.ValueWidget.MarginTop = cell.ValueTop;
                item.ValueWidget.SuggestedHeight = cell.ValueHeight;
            }
        }
    }

    public class CampaignStatItemWidget : Widget
    {
        public string? StatId { get; set; }
        // Set by Gauntlet from the prefab's widget references, before the first update.
        public Widget IconWidget { get; set; } = null!;
        public Widget ValueWidget { get; set; } = null!;
        public CampaignStatItemWidget(UIContext context) : base(context) { }
    }
}
