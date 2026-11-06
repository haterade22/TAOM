using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

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
                case "character_developer": Text = "Character"; break;
                case "inventory": Text = "Inventory"; break;
                case "party": Text = "Party"; break;
                case "quest": Text = "Quests"; break;
                case "clan": Text = "Clan"; break;
                case "kingdom": Text = "Kingdom"; break;
                case "manage_fleet": Text = "Fleet"; break;
                default: Text = ""; break;
            }
        }
    }
}
