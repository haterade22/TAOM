namespace TAOM.Features.Music;

public interface ICharacterCreationMusicContextService
{
    void EnterCharacterCreation();

    void SelectCulture(string cultureId);

    void ConfirmCulture(string cultureId);

    MusicRouteSnapshot CaptureSnapshot();

    void ExitCharacterCreation(string reason = null);
}
