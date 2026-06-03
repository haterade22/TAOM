namespace TAOM.Features.TroopProgression;

public interface IVolunteerRecruitmentService
{
    string GetVolunteerTroopId(VolunteerContext context);

    /// <summary>
    /// True if a culture-level recruitment pool exists for the given culture id. CultureConversion
    /// gates conversion on this so a fief is never converted to a culture we can't recruit for.
    /// </summary>
    bool HasCulturePool(string cultureId);
}
