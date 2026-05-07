namespace TAOM.Features.Messengers.Domain;

public enum MessengerValidationResult
{
    Ok,
    NullTarget,
    HumanPlayerCharacter,
    HeroDead,
    HeroPrisoner,
    HeroChild,
    HeroFugitive,
    TargetUnavailable,
    TargetInPlayerParty,
    InsufficientGold,
    AlreadyPending,
}
