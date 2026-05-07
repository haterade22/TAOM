namespace TAOM.Features.Messengers.Domain;

public sealed class HeroSnapshot
{
    public string HeroId { get; }
    public bool IsAlive { get; }
    public bool IsPrisoner { get; }
    public bool IsChild { get; }
    public bool IsFugitive { get; }
    public bool IsActive { get; }
    public bool IsWanderer { get; }
    public bool IsHumanPlayerCharacter { get; }
    public bool IsInPlayerParty { get; }
    public bool IsInMapEvent { get; }

    public HeroSnapshot(
        string heroId,
        bool isAlive,
        bool isPrisoner,
        bool isChild,
        bool isFugitive,
        bool isActive,
        bool isWanderer,
        bool isHumanPlayerCharacter,
        bool isInPlayerParty,
        bool isInMapEvent)
    {
        HeroId = heroId;
        IsAlive = isAlive;
        IsPrisoner = isPrisoner;
        IsChild = isChild;
        IsFugitive = isFugitive;
        IsActive = isActive;
        IsWanderer = isWanderer;
        IsHumanPlayerCharacter = isHumanPlayerCharacter;
        IsInPlayerParty = isInPlayerParty;
        IsInMapEvent = isInMapEvent;
    }
}
