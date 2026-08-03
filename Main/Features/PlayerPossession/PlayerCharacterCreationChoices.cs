namespace TAOM.Features.PlayerPossession;

/// <summary>
/// What the player picked in character creation, kept alive past the campaign that recorded it.
///
/// Every multiplayer base discards the character-creation hero at the join hand-off and gives the
/// joining player a host-authored one instead. TAOM's CC grants — race, starting gold, career pick,
/// special-resource seed — all ran against the discarded hero, so a joiner arrived with the host's
/// race and none of their culture's bonuses (field report 2026-08-03 §1 and §7, both log-confirmed:
/// a Mirkwood player received the native 1000 gold instead of 1000+4000, and the +4000 grant is
/// visible in the client log immediately before the hero it applied to ceased to exist).
///
/// Immutable and campaign-free on purpose: it is held by a process-lifetime singleton and must not
/// reference anything the campaign teardown disposes.
/// </summary>
public sealed class PlayerCharacterCreationChoices
{
    /// <summary>The hero these choices were applied to — the one the hand-off throws away.</summary>
    public string HeroId { get; }

    public string CultureId { get; }

    /// <summary>FaceGen race index chosen in CC, or -1 when the race stage did not resolve one.</summary>
    public int RaceId { get; }

    /// <summary>The career pick, or null when the player made none.</summary>
    public string CareerId { get; }

    public PlayerCharacterCreationChoices(string heroId, string cultureId, int raceId, string careerId)
    {
        HeroId = heroId;
        CultureId = cultureId;
        RaceId = raceId;
        CareerId = careerId;
    }
}
