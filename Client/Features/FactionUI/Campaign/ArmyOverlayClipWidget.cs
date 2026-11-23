using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    /// <summary>
    /// Clips the army panel at the stats plate's gold frame (#1532); the bottom margin follows
    /// <see cref="CampaignStatsPanelWidget.ShownExpansion"/>. Set in the late update, after every layer's update, so
    /// it reads this frame's value from the map bar layer, which updates after the army panel's layer.
    /// </summary>
    public class ArmyOverlayClipWidget : Widget
    {
        public const float CollapsedClipBottom = 90f;

        public ArmyOverlayClipWidget(UIContext context) : base(context) =>
            MarginBottom = ClipBottom(CampaignStatsPanelWidget.ShownExpansion);

        public static float ClipBottom(float expansion) =>
            CollapsedClipBottom + CampaignStatsPanelWidget.SlideDistance * expansion;

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            MarginBottom = ClipBottom(CampaignStatsPanelWidget.ShownExpansion);
        }
    }
}
