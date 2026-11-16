namespace TAOM.Features.HostilePeace;

/// <summary>
/// <c>ModuleData/diplomacy/hostile_peace.json</c>: one switch per block, all on by default. MCM wins when it is loaded
/// (<see cref="HostilePeaceSettingsProvider"/>); this file is the setting for an install without MCM.
/// </summary>
public sealed class HostilePeaceConfig
{
    /// <summary>No caravan trades in a town of a Hostile realm while the two are at peace.</summary>
    public bool BlockCaravanTrade { get; set; } = HostilePeaceSettingsProvider.DefaultBlockCaravanTrade;

    /// <summary>No trade agreement between two Hostile realms.</summary>
    public bool BlockTradeAgreements { get; set; } = HostilePeaceSettingsProvider.DefaultBlockTradeAgreements;

    /// <summary>The player meets a Hostile realm's towns and castles as at war: disguise to enter a town, no way into a castle,
    /// and no trading, recruiting or resting in its villages.</summary>
    public bool LimitTownAccess { get; set; } = HostilePeaceSettingsProvider.DefaultLimitTownAccess;

    /// <summary>Off (default): the player's own kingdom cannot ally with a realm of the other side of the Free/Evil line.
    /// On: the old full freedom, any alliance. AI realms are held to their own rule either way.</summary>
    public bool AllowPlayerAlliancesWithAnyone { get; set; } = HostilePeaceSettingsProvider.DefaultAllowPlayerAlliancesWithAnyone;
}
