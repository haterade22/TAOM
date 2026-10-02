using TAOM.Features.FactionUI.FactionScreen;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Features.FactionUI.UI.FactionScreen;

/// <summary>What <see cref="FactionScreenVM"/> and its cards need, bundled for the launcher (#704).</summary>
public sealed class FactionScreenServices
{
    public FactionScreenServices(
        FrontEndSpriteService sprites,
        FactionPresetService presets,
        FactionRoster roster,
        FactionScreenWidgets widgets)
    {
        Sprites = sprites;
        Presets = presets;
        Roster = roster;
        Widgets = widgets;
    }

    public FrontEndSpriteService Sprites { get; }

    public FactionPresetService Presets { get; }

    public FactionRoster Roster { get; }

    public FactionScreenWidgets Widgets { get; }
}
