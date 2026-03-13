namespace TAOM.Features.CharacterCreation.Hooks;

public interface IOnGetRaceNames
{
    string[] FilterRaceNames(string[] allRaces);
}
