using EngineTexture = TaleWorlds.Engine.Texture;
using TwoDimTexture = TaleWorlds.TwoDimension.Texture;

namespace TAOM.Features.FactionMap.Widgets;

/// <summary>
/// The one way a runtime-loaded engine texture becomes a Gauntlet <see cref="RuntimeSprite"/> — the
/// exact 2-line wrap previously copy-pasted across five FactionMap loaders (PolygonWidget region/
/// emblem/banner, BannerWidget, FactionImageWidget; round-4 O2). Path resolution, existence checks,
/// logging, and error handling deliberately stay at the call sites — they differ legitimately per loader.
/// </summary>
internal static class RuntimeSpriteFactory
{
    internal static RuntimeSprite FromEngineTexture(EngineTexture engineTexture)
    {
        var twoDimTexture = new TwoDimTexture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engineTexture));
        return new RuntimeSprite(twoDimTexture, twoDimTexture.Width, twoDimTexture.Height);
    }
}
