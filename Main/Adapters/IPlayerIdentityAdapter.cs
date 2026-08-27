using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The only seam in the repo that changes which hero the player controls.
///
/// Every step is its own method with primitive arguments, so HeroSwitchService's ordering can be
/// asserted with Received.InOrder in a unit test. That ordering is not cosmetic: vanilla
/// KillCharacterAction only destroys the abandoned character-creation clan when it is no longer
/// Clan.PlayerClan, so <see cref="ReassignPlayerClan"/> has to happen before
/// <see cref="RemoveOriginalHero"/> or the campaign keeps an orphan clan forever.
/// </summary>
public interface IPlayerIdentityAdapter
{
    /// <summary>
    /// Whether the player-clan pointer can actually be moved. Campaign.PlayerDefaultFaction is
    /// internal, so this is a reflection probe taken once at construction, before any UI exists.
    /// False means the feature disables itself rather than risking a half-swapped campaign.
    /// </summary>
    bool CanReassignPlayerClan { get; }

    /// <summary>True when the id resolves to a living hero who is not already the player.</summary>
    bool IsSwitchable(string heroId);

    /// <summary>
    /// Snapshots the pre-swap world. Must run before any mutation: once the swap happens,
    /// Hero.MainHero and Clan.PlayerClan no longer describe the character the player built.
    /// </summary>
    SwitchTicket Capture(string targetHeroId, string careerId);

    /// <summary>
    /// Moves a clanless hero into the player's clan and makes them its leader, for the adoption
    /// path. The player keeps the clan they named and the banner they designed.
    /// </summary>
    void AdoptIntoPlayerClan(string heroId);

    /// <summary>Hands control to the hero. Wraps ChangePlayerCharacterAction.Apply.</summary>
    void ApplyPlayerCharacter(string heroId);

    /// <summary>
    /// Points Campaign.PlayerDefaultFaction at the taken-over clan. The reflection site.
    /// ChangePlayerCharacterAction never does this itself, and without it CharacterDeveloperVM
    /// throws enumerating Clan.PlayerClan.Heroes on a clan the player no longer belongs to.
    /// </summary>
    void ReassignPlayerClan(string clanId);

    /// <summary>Moves the created character's gold to the hero, when the policy asks for it.</summary>
    void TransferGold(string fromHeroId, string toHeroId);

    /// <summary>
    /// Absorbs the character-creation party's rosters into the player's current party and
    /// disposes of it. Adoption path only, and always by the explicit captured id.
    /// </summary>
    void AbsorbOriginalParty(string partyId);

    /// <summary>
    /// Removes the created character. Strictly after <see cref="ApplyPlayerCharacter"/>, because
    /// KillCharacterAction takes its main-hero branches while the victim is still the player.
    /// </summary>
    void RemoveOriginalHero(string heroId);

    /// <summary>
    /// Marks the hero's clan and their kingdom's other clan leaders as met. Vanilla only marks
    /// Mother and Father on a character change, so without this the clan and kingdom screens open
    /// full of unknown entries for a lord the player is supposed to have led for years.
    /// </summary>
    void MarkClanAndKingdomKnown(string heroId);

    /// <summary>Drops queued notifications addressed to a hero who no longer exists.</summary>
    void ClearPendingNotifications();
}
