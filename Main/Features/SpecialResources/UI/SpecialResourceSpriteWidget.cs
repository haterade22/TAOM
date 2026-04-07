using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.TwoDimension;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources.UI;

public class SpecialResourceSpriteWidget : IconBrushWidget
{
    private ISpecialResourceConfigProvider _config;
    private IModLogger _logger;
    private string _cachedSpriteName;
    private Sprite _cachedSprite;
    private bool _loggedOnce;

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
        var cultureId = hero?.Culture?.StringId;
        if (kingdomId == null && cultureId == null)
            return;

        _config ??= IoC.Resolve<ISpecialResourceConfigProvider>();
        _logger ??= IoC.Resolve<IModLogger>();

        var resource = _config.GetByKingdomId(kingdomId) ?? _config.GetByCultureId(cultureId);
        if (resource == null)
        {
            if (!_loggedOnce)
            {
                _logger?.LogWarning($"[SpecRes] SpriteWidget: no resource for kingdom='{kingdomId}', culture='{cultureId}'");
                _loggedOnce = true;
            }
            return;
        }

        var spriteName = resource.IconSpriteName;
        if (spriteName == _cachedSpriteName && _cachedSprite != null)
            return;

        var sprite = Context.SpriteData.GetSprite(spriteName);
        if (sprite == null)
        {
            if (!_loggedOnce)
            {
                _logger?.LogWarning($"[SpecRes] SpriteWidget: sprite '{spriteName}' not found in SpriteData — icon will be blank until PNG is added to SpriteParts/ui_taom/MapBar/");
                _loggedOnce = true;
            }
            return;
        }

        _cachedSprite = sprite;
        _cachedSpriteName = spriteName;
        _logger?.LogInfo($"[SpecRes] SpriteWidget: loaded sprite '{spriteName}' for {resource.DisplayName}");
        _loggedOnce = true;

        if (Brush != null)
        {
            foreach (BrushLayer layer in Brush.Layers)
                layer.Sprite = sprite;
        }
    }
}
