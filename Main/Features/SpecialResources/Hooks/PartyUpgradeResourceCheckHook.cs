using System.Collections.Generic;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SpecialResources.Hooks;

public class PartyUpgradeResourceCheckHook : IOnPartyUpgradeResourceCheck
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceConfigProvider _config;
    private readonly ICoopSessionProvider _coop;

    public PartyUpgradeResourceCheckHook(ISpecialResourceService service, ISpecialResourceConfigProvider config,
        ICoopSessionProvider coop)
    {
        _service = service;
        _config = config;
        _coop = coop;
    }

    public bool CanAffordUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count)
        => _service.CanAffordUpgrade(heroId, kingdomId, cultureId, troopId, count);

    public void QueueUpgradeSpend(string heroId, string troopId, int count)
        => _service.QueueUpgradeSpend(heroId, troopId, count);

    public int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount, IReadOnlyList<RecruitCartEntry> recruitedThisSession)
        => _service.ClampUpgradeCount(heroId, kingdomId, cultureId, troopId, requestedCount,
            _coop.IsSessionActive ? null : recruitedThisSession);

    public int GetUpgradeCost(string troopId)
    {
        var cost = _config.GetTroopCost(troopId);
        return cost?.UpgradeCost ?? 0;
    }

    // In a live co-op session Coop's prisoner path charges no recruit_cost and a guest reads a stale
    // client balance (#967), so the prisoner gate lets every recruit through (#563, the maintainer's
    // decision A, 2026-11-19). Read live: a session can start and end inside one process.
    public int ClampRecruitCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount, IReadOnlyList<RecruitCartEntry> recruitedThisSession)
        => _coop.IsSessionActive
            ? requestedCount
            : _service.ClampRecruitCount(heroId, kingdomId, cultureId, troopId, requestedCount, recruitedThisSession);

    public int GetRecruitCost(string troopId) => _config.GetTroopCost(troopId)?.RecruitCost ?? 0;

    public float GetAvailableAmount(string heroId, string kingdomId, string cultureId)
        => _service.GetAvailableAfterPending(heroId, kingdomId, cultureId);

    public string GetResourceDisplayName(string kingdomId, string cultureId)
    {
        var resource = _service.ResolveResource(kingdomId, cultureId);
        return resource?.DisplayName;
    }

    public void BeginSession() => _service.BeginPartyScreenSession();
    public void CommitSession(string heroId, string kingdomId, string cultureId) => _service.CommitSession(heroId, kingdomId, cultureId);
    public void CancelSession() => _service.CancelSession();

    public float ChargeAppliedUpgrades(string heroId, string kingdomId, string cultureId)
        => _coop.IsSessionActive ? 0f : _service.CommitAppliedUpgrades(heroId, kingdomId, cultureId);
}
