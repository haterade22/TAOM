using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.PlayerSwitcher;

/// <inheritdoc cref="ITakeoverTreasuryService"/>
/// <remarks>
/// Mike, 2026-10-02: a taken-over lord's "starting gold should be his own treasury if they can. If they
/// wanted to start with 1K they would've made a character from scratch." The engine's
/// CharacterCreationState.FinalizeCharacterCreationState assigns <c>Hero.MainHero.Gold = 1000</c> after
/// every character-creation handler has run, when the main hero is already the lord, so the treasury
/// the lord held when the handover finished (recorded by the 1100 handler, including any starting gold
/// "Carry Over Starting Gold" moved across) is set back here, once creation is over. StartupResources,
/// which decides what replaces the engine's 1,000, calls this first and grants a taken-over lord no
/// culture starting gold.
/// </remarks>
public class TakeoverTreasuryService : ITakeoverTreasuryService
{
    private readonly IPlayerSwitchSession _session;
    private readonly IPlayerIdentityAdapter _identity;
    private readonly IModLogger _logger;

    public TakeoverTreasuryService(IPlayerSwitchSession session, IPlayerIdentityAdapter identity, IModLogger logger)
    {
        _session = session;
        _identity = identity;
        _logger = logger;
    }

    public bool RestoreIfTakenOver()
    {
        // An adopted wanderer brings no treasury and leads the clan the player made, which gets a new
        // character's starting gold.
        if (!_session.LordTakenOver)
            return false;

        var heroId = _session.LastSwitchedHeroId;
        var gold = _session.LastHeroGold;
        if (string.IsNullOrEmpty(heroId) || gold < 0)
            return true;

        if (!_identity.SetPlayerGold(heroId, gold))
        {
            _logger.LogWarning($"Player Switcher: '{heroId}' is not the player any more; their treasury of {gold} was not put back");
            return true;
        }

        _logger.LogInfo($"Player Switcher: '{heroId}' keeps their treasury of {gold} gold");
        return true;
    }
}
