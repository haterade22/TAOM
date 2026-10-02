using System;

namespace TAOM.Features.FactionUI.Resources;

/// <summary>
/// The image groups a themed screen holds while it is open (#704). A group is released
/// <see cref="FrontEndSpriteService.ReleaseDelayTicks"/> ticks after the last screen holding it is gone.
/// </summary>
[Flags]
public enum FrontEndImageGroups
{
    None = 0,

    /// <summary>The main menu's images (<see cref="FrontEndSpriteKind.MenuChrome"/>).</summary>
    MainMenu = 1,

    /// <summary>The character-creation frame (<see cref="FrontEndSpriteKind.Chrome"/>), held by every
    /// themed character-creation screen and the faction screen.</summary>
    CharacterCreation = 2,

    /// <summary>The faction art (<see cref="FrontEndSpriteKind.Art"/>), held only by the faction screen,
    /// so it goes once the player has moved on from it.</summary>
    FactionArt = 4,
}
