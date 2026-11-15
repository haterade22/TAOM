using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.UI;

namespace TAOM.Client.Features.CheatMenu;

/// <summary>Builds the "Cheats" navigation button (ADR-013: the element type lives in TAOM.Client). Patch151 asks for one per handler.</summary>
internal sealed class TaomCheatsNavigationElementFactory : ICheatMenuNavigationElementFactory
{
    private readonly CheatMenuOpener _opener;
    private readonly IModLogger _logger;

    public TaomCheatsNavigationElementFactory(CheatMenuOpener opener, IModLogger logger)
    {
        _opener = opener;
        _logger = logger;
    }

    public INavigationElement? Create(object handler) =>
        handler is MapNavigationHandler map ? new TaomCheatsNavigationElement(map, _opener, _logger) : null;
}
