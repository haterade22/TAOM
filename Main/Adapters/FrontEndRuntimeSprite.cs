using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace TAOM.Adapters;

/// <summary>
/// A Gauntlet sprite drawn from one whole runtime-loaded texture, under its own name and with optional
/// nine-patch borders. The named, nine-patch counterpart of FactionMap's <c>RuntimeSprite</c>, which is
/// always anonymous and unstretched; ported from Kysaro's TAOM_FactionUI (#704).
/// </summary>
internal sealed class FrontEndRuntimeSprite : Sprite
{
    private readonly Texture _texture;

    public FrontEndRuntimeSprite(string name, Texture texture, SpriteNinePatchParameters ninePatch)
        : base(name, texture.Width, texture.Height, ninePatch)
    {
        _texture = texture;
    }

    public override Texture Texture => _texture;

    public override Vec2 GetMinUvs() => Vec2.Zero;

    public override Vec2 GetMaxUvs() => Vec2.One;
}
