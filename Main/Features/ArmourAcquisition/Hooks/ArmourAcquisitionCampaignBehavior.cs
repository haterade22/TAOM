using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Thin campaign boundary for Armour Acquisition (docs/features/armour-acquisition.md, ADR-002): saves
/// and restores <see cref="ArmourAcquisitionState"/>, resets it when a session starts without loading it,
/// sweeps each town's market daily of the gated pieces its armoury does not allow, and rolls the daily
/// visiting master armourer. World changes run on the campaign's authority only (a co-op guest defers to
/// the host). Built fresh for every campaign by the feature module, so the loaded-this-session flag never
/// leaks.
/// </summary>
public sealed class ArmourAcquisitionCampaignBehavior : CampaignBehaviorBase
{
    internal const string SaveKey = "_taom_armourAcquisition";

    private readonly ArmourAcquisitionState _state;
    private readonly IArmourGateService _gate;
    private readonly VisitingArmourerService _visits;
    private readonly ArmourStockSweepService _sweep;
    private readonly IArmouryTownAdapter _towns;
    private readonly IArmourAcquisitionSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;
    private readonly Random _rng = new();
    private bool _loadedThisSession;

    public ArmourAcquisitionCampaignBehavior(ArmourAcquisitionState state, IArmourGateService gate,
        VisitingArmourerService visits, ArmourStockSweepService sweep, IArmouryTownAdapter towns,
        IArmourAcquisitionSettingsProvider settings, ICoopSessionProvider coop, IDedicatedServerProvider server,
        IModLogger logger)
    {
        _state = state;
        _gate = gate;
        _visits = visits;
        _sweep = sweep;
        _towns = towns;
        _settings = settings;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var data = _state.Encode();
            dataStore.SyncData(SaveKey, ref data);
            return;
        }
        // A key miss leaves the ref unchanged, so load into a fresh dictionary: a save made before this
        // feature existed then decodes to an empty state instead of keeping the previous campaign's.
        var loaded = new Dictionary<string, string>();
        dataStore.SyncData(SaveKey, ref loaded);
        var skipped = _state.Decode(loaded);
        _loadedThisSession = true;
        if (skipped > 0)
            _logger.LogWarning($"[ArmourAcquisition] {skipped} damaged save entr(ies) were dropped while loading.");
    }

    internal void OnSessionLaunched(CampaignGameStarter? starter)
    {
        if (!_loadedThisSession)
            _state.Reset();
    }

    private void OnDailyTickSettlement(Settlement settlement)
    {
        // Raised for every settlement each day: the cheapest rejections first.
        if (settlement == null || !settlement.IsTown || _coop.ShouldDeferToHost)
            return;
        var removed = _sweep.SweepTown(settlement.StringId);
        if (removed > 0)
            _logger.LogInfo($"[ArmourAcquisition] {removed} gated piece(s) left {settlement.StringId}'s market: its armoury does not allow them today.");
    }

    private void OnDailyTick()
    {
        if (!_gate.IsActive || _coop.ShouldDeferToHost)
            return;
        var visited = _visits.OnDailyTick(_towns.Today, _rng, _settings.VisitingArmourerEnabled);
        if (visited == null)
            return;

        _logger.LogInfo($"[ArmourAcquisition] A master armourer visits {visited} until day {_state.VisitUntilDay[visited]}.");
        if (_server.IsDedicatedServer)
            return;
        var text = new TextObject("{=taom_armoury_visit_news}A master armourer has come to {TOWN}. For the next {DAYS} days its armoury stocks and forges as a finer one would.")
            .SetTextVariable("TOWN", _towns.GetName(visited))
            .SetTextVariable("DAYS", _state.VisitUntilDay[visited] - _towns.Today);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Yellow));
    }
}
