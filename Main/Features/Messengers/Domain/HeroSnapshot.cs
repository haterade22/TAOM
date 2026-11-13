namespace TAOM.Features.Messengers.Domain;

public sealed class HeroSnapshot
{
    public string HeroId { get; }
    public bool IsAlive { get; }
    public bool IsPrisoner { get; }
    public bool IsChild { get; }
    public bool IsFugitive { get; }
    public MessengerHeroState HeroState { get; }
    public bool IsWanderer { get; }
    public bool IsHumanPlayerCharacter { get; }
    public bool IsInPlayerParty { get; }
    public bool SameSettlement { get; }
    public bool SameArmy { get; }

    public HeroSnapshot(
        string heroId,
        bool isAlive,
        bool isPrisoner,
        bool isChild,
        bool isFugitive,
        MessengerHeroState heroState,
        bool isWanderer,
        bool isHumanPlayerCharacter,
        bool isInPlayerParty,
        bool sameSettlement = false,
        bool sameArmy = false)
    {
        HeroId = heroId;
        IsAlive = isAlive;
        IsPrisoner = isPrisoner;
        IsChild = isChild;
        IsFugitive = isFugitive;
        HeroState = heroState;
        IsWanderer = isWanderer;
        IsHumanPlayerCharacter = isHumanPlayerCharacter;
        IsInPlayerParty = isInPlayerParty;
        SameSettlement = sameSettlement;
        SameArmy = sameArmy;
    }
}
