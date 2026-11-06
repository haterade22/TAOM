using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ImageIdentifiers;

namespace TAOM.Features.FactionUI.Campaign
{
    public class CampaignMusterWidget : Widget
    {
        public bool NativeVisible { get; set; }
        public bool CanGatherArmy { get; set; }
        public CampaignStatsPanelWidget StatsPanelWidget { get; set; }
        public ButtonWidget MusterButton { get; set; }
        public Widget BannerWidget { get; set; }
        public bool PreviewHasFaction { get; set; } = true;
        public string PreviewBannerSprite { get; set; } = "fs_previewonly_muster_gondor";
        private string _bannerCode;

        public CampaignMusterWidget(UIContext context) : base(context) { }

        public static bool ShouldShow(bool nativeVisible, bool hasFaction) => nativeVisible && hasFaction;

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            var kingdom = Clan.PlayerClan.Kingdom;
            bool hasFaction = kingdom != null;
            IsVisible = ShouldShow(NativeVisible, hasFaction);
            MusterButton.IsEnabled = CanGatherArmy;
            // Follow the same animated upper tier, not a separate fixed-duration transition.
            // Nest the rounded lower-left corner into the plate's curved upper-right shoulder.
            // Align the gold base itself; the clasp hangs slightly over the shared border.
            PositionYOffset = -(CampaignStatsPanelWidget.PanelHeight - StatsPanelWidget.UpperPlate.PositionYOffset - 26f);
            if (!IsVisible) return;
            ((CampaignMusterBannerWidget)BannerWidget).Brush.ColorFactor = CanGatherArmy ? 1f : 0.45f;
            var identifier = new BannerImageIdentifier(kingdom.Banner);
            if (_bannerCode == identifier.Id) return;
            _bannerCode = identifier.Id;
            var image = (CampaignMusterBannerWidget)BannerWidget;
            image.TextureProviderName = identifier.TextureProviderName;
            image.AdditionalArgs = identifier.AdditionalArgs;
            image.ImageId = identifier.Id;
        }
    }

    // Native masked tableau rendering preserves the cloth alpha and fold shading.
    public class CampaignMusterBannerWidget : MaskedTextureWidget
    {
        public CampaignMusterBannerWidget(UIContext context) : base(context) { }
    }
}
