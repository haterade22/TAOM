namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// The three lists the picker prefab binds. The order of this enum is the order the panel
/// renders, and <see cref="HeroPickList"/> keeps them separate so an empty group can render
/// its "none" text instead of collapsing into a neighbour.
/// </summary>
public enum HeroPickerGroup
{
    RulingHouse = 0,
    ClanLeaders = 1,
    Wanderers = 2,
}
