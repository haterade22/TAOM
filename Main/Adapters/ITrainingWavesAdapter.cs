namespace TAOM.Adapters;

/// <summary>
/// The engine facts and the one write behind the wave fight at the training master (docs/features/training-waves.md): the day, the
/// town, the player's state, the conversation partner and the vanilla practice flags, and the town's coffers. Ids and plain values only
/// (ADR-007). The reads never write. The player's purse comes from <see cref="ICareerPurseAdapter"/>.
/// </summary>
public interface ITrainingWavesAdapter
{
    /// <summary>The campaign day, whole (<c>CampaignTime.Now.ToDays</c>); 0 without a campaign.</summary>
    int Today { get; }

    /// <summary>The id of the town the player stands in; null outside a town.</summary>
    string? TownId { get; }

    /// <summary>The town holds a tournament now: vanilla keeps the yard for it and refuses a practice fight.</summary>
    bool TownHasTournament { get; }

    /// <summary>Vanilla's check for joining a fight: the player hero is wounded.</summary>
    bool PlayerWounded { get; }

    /// <summary>Vanilla's check for joining a fight: the player is in disguise.</summary>
    bool PlayerDisguised { get; }

    /// <summary>
    /// What the Deep Pockets perk multiplies a bet by (<c>TournamentBehavior.GetMaximumBet</c>): its primary bonus when the player has
    /// the perk, 1 otherwise or when the bonus is not a finite number of at least 1.
    /// </summary>
    int DeepPocketsFactor { get; }

    /// <summary>
    /// The conversation partner is the practice master of a town, the mission runs vanilla's practice controller, and the player is not
    /// practising. A practice master stands only in the town's yard scene, which also carries the controller.
    /// </summary>
    bool TalkingToPracticeMaster { get; }

    /// <summary>Vanilla's post-practice flag (<c>ArenaPracticeFightMissionController.AfterPractice</c>): its prize debrief is still due.</summary>
    bool VanillaAfterPractice { get; }

    /// <summary>
    /// Gives <paramref name="amount"/> gold from no one to the town's coffers, as vanilla does with a lost tournament bet. False, with
    /// nothing given, for an amount of 0 or less, no current settlement, or a fault.
    /// </summary>
    bool GiveToTown(int amount);
}
