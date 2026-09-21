using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

public interface IVolunteerRecruitmentService
{
    string GetVolunteerTroopId(VolunteerContext context);

    /// <summary>
    /// True if a culture-level recruitment pool exists for the given culture id. CultureConversion
    /// gates conversion on this so a fief is never converted to a culture we can't recruit for.
    /// </summary>
    bool HasCulturePool(string cultureId);

    /// <summary>
    /// Every culture id that has a culture-level pool, i.e. exactly the set
    /// <see cref="HasCulturePool"/> answers true for. This is the set of cultures a fief can be
    /// converted TO, so anything that has to be complete across conversion targets enumerates it
    /// rather than guessing from the troop XML.
    /// </summary>
    IReadOnlyCollection<string> GetPooledCultureIds();

    /// <summary>
    /// The culture pool's troop ids (the roots the settlement actually recruits), or empty when the
    /// culture has no pool.
    ///
    /// These are roots, not the culture's whole roster: walk each one's upgrade targets to reach the
    /// full line. Crucially the ids need NOT carry this culture. Several TAOM cultures deliberately
    /// field another's troops (Lothlorien recruits the Rivendell line, Khand the Rhun line,
    /// Shaghana and Abanissa the Harad line), so this is the only honest answer to "which troops does
    /// this culture put in the field", and grouping <c>CharacterObject.Culture</c> is not.
    /// </summary>
    IReadOnlyList<string> GetCulturePoolTroopIds(string cultureId);
}
