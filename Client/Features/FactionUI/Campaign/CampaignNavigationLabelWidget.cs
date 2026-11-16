using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.Campaign
{
    // Native navigation items expose ItemId; labels stay live text rather than baked sprite lettering.
    public class CampaignNavigationLabelWidget : TextWidget
    {
        public string? ItemId { get; set; }
        private string? _lastItemId;

        public CampaignNavigationLabelWidget(UIContext context) : base(context) { }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (ItemId == _lastItemId) return;
            _lastItemId = ItemId;
            switch (ItemId)
            {
                case "character_developer": Text = Shown("{=klZnvb15}Character"); break;
                case "inventory": Text = Shown("{=rbz1Bwbv}Inventory"); break;
                case "party": Text = Shown("{=qnxJYAs7}Party"); break;
                case "quest": Text = Shown("{=KUEHAD4U}Quests"); break;
                case "clan": Text = Shown("{=j4F7tTzy}Clan"); break;
                case "kingdom": Text = Shown("{=03mPaWum}Kingdom"); break;
                case "manage_fleet": Text = Shown("{=p6QNVu8p}Fleet"); break;
                default: Text = ""; break;
            }
        }

        // Vanilla's keys for these labels: TaleWorlds translates them in every language it ships.
        private static string Shown(string keyed) => new TextObject(keyed).ToString();
    }
}
