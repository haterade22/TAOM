namespace TAOM.Features.Music;

public sealed class CharacterCreationMusicContextService : ICharacterCreationMusicContextService
{
    private bool _active;
    private string _cultureId;
    private string _reason = "empty";

    public void EnterCharacterCreation()
    {
        if (string.IsNullOrWhiteSpace(_cultureId))
        {
            _active = false;
            _reason = "character_creation_waiting_for_culture";
            return;
        }

        _active = true;
    }

    public void SelectCulture(string cultureId)
    {
        SetCulture(cultureId, "character_creation_culture_selected");
    }

    public void ConfirmCulture(string cultureId)
    {
        SetCulture(cultureId, "character_creation_culture_confirmed");
    }

    public MusicRouteSnapshot CaptureSnapshot()
    {
        if (!_active)
            return new MusicRouteSnapshot(
                false,
                MusicTrackIndex.NeutralCulture,
                false,
                false,
                false,
                false,
                false,
                false,
                _reason);

        return new MusicRouteSnapshot(
            true,
            _cultureId,
            false,
            false,
            false,
            false,
            false,
            true,
            _reason);
    }

    public void ExitCharacterCreation(string reason = null)
    {
        _active = false;
        _cultureId = null;
        _reason = string.IsNullOrWhiteSpace(reason) ? "character_creation_exit" : reason.Trim();
    }

    private void SetCulture(string cultureId, string reason)
    {
        if (string.IsNullOrWhiteSpace(cultureId))
            return;

        _cultureId = cultureId.Trim();
        _active = true;
        _reason = reason;
    }
}
