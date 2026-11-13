using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.PlayerPossession;

/// <inheritdoc />
public sealed class PlayerPossessionService : IPlayerPossessionService
{
    private readonly ICoopPresenceProvider _coopPresence;
    private readonly IModLogger _logger;

    private string _baselineHeroId;
    private PlayerCharacterCreationChoices _choices;
    private bool _consumed;

    public string PossessedHeroId { get; private set; }

    public PlayerPossessionService(ICoopPresenceProvider coopPresence, IModLogger logger)
    {
        _coopPresence = coopPresence;
        _logger = logger;
    }

    public void RecordBaselineHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        // First value wins. OnGameLoaded and OnSessionLaunched both call this, and on a client the
        // switch can land between them — overwriting here would adopt the POST-switch hero as the
        // baseline and make the switch permanently undetectable.
        if (!string.IsNullOrEmpty(_baselineHeroId)) return;

        _baselineHeroId = heroId;
    }

    public void BeginCharacterCreation()
    {
        if (_choices != null)
            _logger.LogInfo($"[Possession] A new character creation began; dropped the choices captured for '{_choices.HeroId}'.");
        _choices = null;
    }

    public void CaptureCharacterCreationChoices(PlayerCharacterCreationChoices choices)
    {
        if (choices == null || string.IsNullOrEmpty(choices.HeroId)) return;

        _choices = choices;
        _consumed = false;
        _logger.LogInfo(
            $"[Possession] Captured character-creation choices for '{choices.HeroId}' " +
            $"(culture={choices.CultureId}, race={choices.RaceId}, career={choices.CareerId ?? "none"}). " +
            "These are re-applied if a multiplayer join hands us a different hero.");
    }

    public bool TryConsumePossession(string currentHeroId, out PlayerCharacterCreationChoices choices)
    {
        choices = null;

        // With no co-op module enabled, play never reaches the re-grant. This is the guard that keeps
        // single-player HEIR SUCCESSION, which also changes Hero.MainHero, from being mistaken for a
        // join hand-off. With a co-op module enabled but no session it does not hold (#1183).
        if (!_coopPresence.IsCoopActive) return false;

        if (_consumed || _choices == null) return false;
        if (string.IsNullOrEmpty(currentHeroId) || string.IsNullOrEmpty(_baselineHeroId)) return false;

        // Must differ from BOTH the session baseline and the hero the choices were applied to.
        // Checking only the baseline would fire on a client whose baseline was recorded late.
        if (currentHeroId == _baselineHeroId) return false;
        if (currentHeroId == _choices.HeroId) return false;

        _consumed = true;
        PossessedHeroId = currentHeroId;
        choices = _choices;

        _logger.LogInfo(
            $"[Possession] Controlled hero changed '{_baselineHeroId}' -> '{currentHeroId}'. " +
            $"Re-applying the character-creation package recorded for '{_choices.HeroId}'.");
        return true;
    }

    public void ResetForNewCampaign()
    {
        _baselineHeroId = null;
        PossessedHeroId = null;
        // Deliberately NOT clearing _choices/_consumed: character creation finishes and raises its
        // event BEFORE the joining client's campaign is replaced, so clearing on new-campaign would
        // throw away the very data this feature exists to carry across that boundary.
    }
}
