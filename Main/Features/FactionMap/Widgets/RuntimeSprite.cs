using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace TAOM.Features.FactionMap.Widgets;

public class RuntimeSprite : Sprite
{
    private Texture _texture;

    public override Texture Texture => _texture;

    public RuntimeSprite(Texture texture, int width, int height)
        : base("RuntimeSprite", width, height, SpriteNinePatchParameters.Empty)
    {
        _texture = texture;
    }

    public override Vec2 GetMinUvs() => Vec2.Zero;

    public override Vec2 GetMaxUvs() => Vec2.One;
}
