namespace TAOM.Features.Music;

public interface ICustomBattleMusicContextService
{
    string SelectedPlayerCultureId { get; }

    void SelectPlayerCulture(string cultureId);

    void ClearSelectedCulture();
}
