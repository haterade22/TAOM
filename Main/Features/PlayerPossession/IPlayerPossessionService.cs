namespace TAOM.Features.PlayerPossession;

/// <summary>
/// Detects the controlled-hero switch that every multiplayer base performs when a player joins,
/// and carries the character-creation choices across it.
///
/// The detection is pure engine state — "<c>Hero.MainHero</c> is not the hero it was when this
/// session started" — with no reference to any co-op assembly, so it behaves identically whether
/// the co-op mod is BannerlordCoop, Bannerlord Together, or something that does not exist yet.
///
/// Implementations must be process-lifetime singletons: the choices are recorded in the
/// character-creation campaign and consumed in a DIFFERENT one, after the first has been torn down.
/// </summary>
public interface IPlayerPossessionService
{
    /// <summary>
    /// Records which hero this session started with. Called on session launch and game load; the
    /// first non-empty value wins for the session, because a later call would move the goalposts
    /// and make the switch undetectable.
    /// </summary>
    void RecordBaselineHero(string heroId);

    /// <summary>Stores what the player picked in character creation, to re-apply after the switch.</summary>
    void CaptureCharacterCreationChoices(PlayerCharacterCreationChoices choices);

    /// <summary>
    /// True at most ONCE per captured set of choices, and only when all of these hold:
    /// a co-op module is active; choices were captured this process; and <paramref name="currentHeroId"/>
    /// differs from both the recorded baseline and the hero the choices were applied to.
    ///
    /// The co-op gate and the single-consumption rule together exist to keep this away from
    /// SINGLE-PLAYER HEIR SUCCESSION, which also changes <c>Hero.MainHero</c>. A solo player never
    /// passes the co-op gate; a co-op player whose heir inherits hours later has already consumed
    /// the choices at join time, so the heir cannot be re-granted a starting package.
    /// </summary>
    bool TryConsumePossession(string currentHeroId, out PlayerCharacterCreationChoices choices);

    /// <summary>The hero the local player actually controls once possession is detected, else null.</summary>
    string PossessedHeroId { get; }

    /// <summary>Clears per-campaign state so a second campaign in the same process starts clean.</summary>
    void ResetForNewCampaign();
}
