using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.RaceAge.Models;

public class TaomAgeModel : DefaultAgeModel
{
    private readonly IRaceAgeService _raceAgeService;

    public TaomAgeModel(IRaceAgeService raceAgeService)
    {
        _raceAgeService = raceAgeService;
    }

    public override int MaxAge => 10000;

    public override int BecomeOldAge => 5000;

    public override void GetAgeLimitForLocation(
        CharacterObject character,
        out int minimumAge,
        out int maximumAge,
        string additionalTags = "")
    {
        base.GetAgeLimitForLocation(character, out minimumAge, out maximumAge, additionalTags);

        var race = character.Race;
        ApplyRaceAgeLimits(
            ref minimumAge,
            ref maximumAge,
            _raceAgeService.GetComesOfAge(race),
            _raceAgeService.GetMaxAge(race),
            additionalTags);
    }

    internal static void ApplyRaceAgeLimits(
        ref int minimumAge,
        ref int maximumAge,
        int raceComesOfAge,
        int raceMax,
        string additionalTags)
    {
        if (maximumAge > raceMax)
            maximumAge = raceMax;

        if (minimumAge < raceComesOfAge && additionalTags != "Child")
            minimumAge = raceComesOfAge;

        if (minimumAge > maximumAge)
            minimumAge = maximumAge;
    }
}
