namespace TAOM.Features.HeroRace;

public interface IFaceGenIconService
{
    string GetBeardName(int index, int race, int gender);
    string GetHairName(int index, int race, int gender);
}
