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

        var original = (Settlement)_field.GetValue(party);
        _field.SetValue(party, target);
        return original;
    }

    public void Restore(Settlement original)
    {
        var party = MobileParty.MainParty;
        if (party == null || _field == null) return;
        _field.SetValue(party, original);
    }
}
