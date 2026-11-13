namespace TAOM.Features.Messengers.Domain;

/// <summary>
/// The engine's Hero.CharacterStates, one to one, in the same order and with the same values (v1.5.7 Hero.cs:28-38).
/// MessengerTargetReader casts by value; MessengerArrivalClassificationTests checks the two enums still match.
/// </summary>
public enum MessengerHeroState
{
    NotSpawned,
    Active,
    Fugitive,
    Prisoner,
    Released,
    Dead,
    Disabled,
    Traveling,
}
