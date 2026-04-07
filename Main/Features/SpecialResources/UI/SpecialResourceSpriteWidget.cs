using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.TwoDimension;

namespace TAOM.Features.SpecialResources.UI;

public class SpecialResourceSpriteWidget : IconBrushWidget
{
    private ISpecialResourceConfigProvider _config;
    private string _cachedSpriteName;
    private Sprite _cachedSprite;

    public SpecialResourceSpriteWidget(UIContext context) : base(context)
    {
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        if (IconID != "special_resource")
            return;

        if (Game.Current?.GameType is not Campaign)
            return;

        var hero = Hero.MainHero;
        var kingdomId = hero?.Clan?.Kingdom?.StringId;
        if (kingdomId == null)
            return;

        _config ??= IoC.Resolve<ISpecialResourceConfigProvider>();
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null)
            return;

        var spriteName = resource.IconSpriteName;
        if (spriteName == _cachedSpriteName && _cachedSprite != null)
            return;

        var sprite = Context.SpriteData.GetSprite(spriteName);
        if (sprite == null)
            return;

        _cachedSprite = sprite;
        _cachedSpriteName = spriteName;

        // Set sprite on brush layers (not Widget.Sprite) to prevent
        // IconBrushWidget.UpdateIcon() from overwriting it each frame.
        if (Brush != null)
        {
            foreach (BrushLayer layer in Brush.Layers)
                layer.Sprite = sprite;
        }
    }
}
