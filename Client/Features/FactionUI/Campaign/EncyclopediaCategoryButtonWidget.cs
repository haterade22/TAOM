using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    // Presentation only: the button still invokes the native ListTypeVM.Execute command.
    public class EncyclopediaCategoryButtonWidget : ButtonWidget
    {
        private string? _categoryId;

        public string? CategoryId
        {
            get => _categoryId;
            set
            {
                _categoryId = value;
                string id = (value ?? "").ToLowerInvariant();
                string category = id.Contains("settlement") ? "settlements"
                    : id.Contains("hero") ? "heroes"
                    : id.Contains("unit") || id.Contains("troop") ? "troops"
                    : id.Contains("faction") || id.Contains("kingdom") ? "kingdoms"
                    : id.Contains("clan") ? "clans" : "concepts";
                // Concepts stays reachable by search and links; only its home card is hidden for now.
                IsVisible = !id.Contains("concept");
                var sprite = Context.SpriteData.GetSprite("ec_window_" + category);
                Brush.Sprite = sprite;
                foreach (var layer in Brush.Layers) layer.Sprite = sprite;
            }
        }

        public EncyclopediaCategoryButtonWidget(UIContext context) : base(context) { }
    }
}
