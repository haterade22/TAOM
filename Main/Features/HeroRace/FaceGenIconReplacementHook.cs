using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace.Hooks;

namespace TAOM.Features.HeroRace;

public class FaceGenIconReplacementHook : IOnFaceGenUpdateRaceResources
{
    private readonly IFaceGenIconService _faceGenIconService;
    private readonly IModLogger _logger;

    public FaceGenIconReplacementHook(IFaceGenIconService faceGenIconService, IModLogger logger)
    {
        _faceGenIconService = faceGenIconService;
        _logger = logger;
    }

    public void OnUpdateRaceAndGenderBasedResources(
        int race, int gender,
        IList<IFaceGenItem> beardItems, IList<IFaceGenItem> hairItems)
    {
        if (race == 0)
            return;

        foreach (var item in beardItems)
        {
            var name = _faceGenIconService.GetBeardName(item.Index, race, gender);
            if (!string.IsNullOrEmpty(name))
            {
                item.ImagePath = @"FaceGen\Beard\" + name;
            }
        }

        var genderFolder = gender == 1 ? "Female" : "Male";
        foreach (var item in hairItems)
        {
            var name = _faceGenIconService.GetHairName(item.Index, race, gender);
            if (!string.IsNullOrEmpty(name))
            {
                item.ImagePath = @"FaceGen\Hair\" + genderFolder + @"\" + name;
            }
        }
    }
}
