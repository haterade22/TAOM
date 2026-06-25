using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>Adds purchased emissary troops to the main party roster (ADR-007). Returns false on any
/// reason a charge must NOT follow — no main party, unresolvable troop id, or an add exception.</summary>
public sealed class PlayerPartyAdapter : IPlayerPartyAdapter
{
    private readonly IModLogger _logger;

    public PlayerPartyAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string GetMainHeroId() => Hero.MainHero?.StringId;

    public bool GrantTroop(string troopId, int count)
    {
        if (string.IsNullOrEmpty(troopId) || count <= 0)
            return false;

        try
        {
            var roster = Hero.MainHero?.PartyBelongedTo?.MemberRoster;
            if (roster == null)
            {
                _logger.LogWarning("[EliteEmissary] GrantTroop: no main-party roster (dead hero / no party)");
                return false;
            }

            var character = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
            if (character == null)
            {
                _logger.LogError($"[EliteEmissary] GrantTroop: troop id '{troopId}' did not resolve to a CharacterObject");
                return false;
            }

            roster.AddToCounts(character, count);
            _logger.LogInfo($"[EliteEmissary] GrantTroop: added {count}x {troopId} to the main party");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EliteEmissary] GrantTroop('{troopId}' x{count}) failed: {ex.Message}");
            return false;
        }
    }
}
