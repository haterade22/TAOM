namespace TAOM.Features.Music;

public sealed class CustomBattleMusicContextService : ICustomBattleMusicContextService
{
    private readonly object _sync = new object();
    private string _selectedPlayerCultureId;

    public string SelectedPlayerCultureId
    {
        get
        {
            lock (_sync)
            {
                return _selectedPlayerCultureId;
            }
        }
    }

    public void SelectPlayerCulture(string cultureId)
    {
        lock (_sync)
        {
            _selectedPlayerCultureId = string.IsNullOrWhiteSpace(cultureId)
                ? null
                : cultureId.Trim();
        }
    }

    public void ClearSelectedCulture()
    {
        lock (_sync)
        {
            _selectedPlayerCultureId = null;
        }
    }
}
