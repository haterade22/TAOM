using System.Collections.Generic;

namespace TAOM.Features.HeroRace.Hooks;

public interface IFaceGenItem
{
    int Index { get; }
    string ImagePath { get; set; }
}

public interface IOnFaceGenUpdateRaceResources
{
    void OnUpdateRaceAndGenderBasedResources(
        int race, int gender,
        IList<IFaceGenItem> beardItems, IList<IFaceGenItem> hairItems);
}
