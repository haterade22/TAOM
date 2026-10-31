namespace TAOM.Features.CastleRecruitment;

/// <summary>Who took a castle notable's volunteer, as the #918 check sees one recruit event.</summary>
public enum CastleRecruitKind
{
    // Not a castle notable's volunteer: a town or village recruit, a mercenary or a prisoner.
    None,
    // An AI lord's party, a companion-led party of the player's clan included, as the castle rule treats it.
    Ai,
    // The main hero. Vanilla v1.5.6 raises OnUnitRecruited for the player's recruits, so this is kept only so
    // a raiser that passes the main hero is never counted as an AI lord.
    Player,
    // No recruiter hero. Vanilla v1.5.6 garrisons recruit without the event, so this is kept only so a
    // raiser with no leader is never counted as an AI lord.
    Garrison,
}
