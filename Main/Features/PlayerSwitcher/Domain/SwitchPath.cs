namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// Which handover a <see cref="SwitchPlan"/> performs.
/// </summary>
public enum SwitchPath
{
    /// <summary>
    /// The target already belongs to a clan, so the player takes over that identity wholesale:
    /// their clan, fiefs and kingdom. The throwaway character-creation clan is destroyed.
    /// </summary>
    AssumeIdentity = 0,

    /// <summary>
    /// The target is clanless (a wanderer), so they are adopted into the clan the player named
    /// during creation and made its leader. The player keeps their own clan name and banner.
    /// </summary>
    AdoptIntoPlayerClan = 1,
}
