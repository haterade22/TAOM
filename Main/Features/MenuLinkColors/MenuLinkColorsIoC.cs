using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.MenuLinkColors;

public static class MenuLinkColorsIoC
{
    public static void RegisterMenuLinkColorsFeature(IContainer container)
    {
        container.Register<IEncyclopediaCultureLookup, EncyclopediaCultureLookup>(Reuse.Singleton);
        container.Register<IMenuBrushStyleProbe, MenuBrushStyleProbe>(Reuse.Singleton);
        container.Register<IMenuLinkStyleRewriter, MenuLinkStyleRewriter>(Reuse.Singleton);
    }
}
