using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    /// <summary>
    /// Holds the navigation, time and stats bars of TAOMMapBar and hides them, with their hit targets, while a native
    /// screen or a full-screen map overlay is open. GauntletMapBarGlobalLayer keeps the map bar enabled over Inventory,
    /// Party and the other screens, and writes the flags read here into MapTimeControlVM every frame (v1.5.7), so this
    /// widget recomputes from them each update and holds no latch. It writes only its own IsVisible.
    /// </summary>
    public class CampaignBarsWidget : Widget
    {
        // Set by CampaignPresentation.OnLoaded after the movie is built; null until then. OnLoaded finds this widget with
        // FindChildrenWithType, which skips a widget without an Id, so the prefab must give it one.
        public MapBarVM? Model { get; set; }

        public CampaignBarsWidget(UIContext context) : base(context) { }

        public static bool ShouldShow(bool nativeEnabled, MapTimeControlVM? time)
        {
            if (!nativeEnabled)
                return false;
            if (time == null)
                return true;
            return time.IsInMap && !(time.IsEncyclopediaOpen || time.IsInArmyManagement || time.IsInTownManagement ||
                time.IsInRecruitment || time.IsInHideoutTroopManage || time.IsInCampaignOptions || time.IsInBattleSimulation);
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            var model = Model;
            if (model != null)
                IsVisible = ShouldShow(model.IsEnabled, model.MapTimeControl);
        }
    }
}
