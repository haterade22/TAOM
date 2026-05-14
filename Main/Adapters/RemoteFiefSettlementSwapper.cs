using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public class RemoteFiefSettlementSwapper : IRemoteFiefSettlementSwapper
{
    private readonly IModLogger _logger;
    private readonly FieldInfo _field;
    private bool _missingFieldLogged;

    // Phase 9b #143 P1 — hold the party ref captured at Swap time. Pre-fix Restore re-queried
    // MobileParty.MainParty and silently bailed if null at restore time (campaign teardown,
    // VM exception mid-flow). The swap was never restored, leaving the global
    // MobileParty._currentSettlement pointing at a remote fief — corrupting party movement, AI,
    // and every subsequent F6 invocation in the same session.
    private MobileParty _swappedParty;

    public RemoteFiefSettlementSwapper(IModLogger logger)
    {
        _logger = logger;
        _field = AccessTools.Field(typeof(MobileParty), "_currentSettlement");
    }

    public bool ReflectionTargetAvailable => _field != null;

    public Settlement Swap(Settlement target)
    {
        var party = MobileParty.MainParty;
        if (party == null) return null;
        if (_field == null)
        {
            if (!_missingFieldLogged)
            {
                _logger.LogError("[FiefManagement] MobileParty._currentSettlement field not found — remote fief swap disabled");
                _missingFieldLogged = true;
            }
            return null;
        }

        _swappedParty = party;
        var original = (Settlement)_field.GetValue(party);
        _field.SetValue(party, target);
        return original;
    }

    public void Restore(Settlement original)
    {
        if (_field == null) return;

        // Phase 9b #143 P1 — use captured ref from Swap, not a fresh MobileParty.MainParty query.
        // The captured ref is non-null whenever a Swap actually succeeded; if MainParty has
        // since become null (campaign tearing down), we still need to restore that exact ref
        // to keep the engine's _currentSettlement consistent with whatever party we swapped.
        var party = _swappedParty;
        if (party == null)
        {
            // No prior Swap (or already cleared) — fall back to current MainParty to handle the
            // legacy call shape, but log loudly so we know if this fallback ever fires.
            party = MobileParty.MainParty;
            if (party == null)
            {
                _logger.LogError("[FiefManagement] Restore called with no captured party AND MainParty null — _currentSettlement may be corrupted");
                return;
            }
            _logger.LogWarning("[FiefManagement] Restore called without prior Swap; falling back to MainParty");
        }

        _field.SetValue(party, original);
        _swappedParty = null;
    }
}
