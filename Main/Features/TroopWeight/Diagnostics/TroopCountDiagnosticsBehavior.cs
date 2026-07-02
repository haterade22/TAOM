using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.TroopWeight.Diagnostics;

/// <summary>
/// TEMPORARY diagnostic for the "special-currency troops undercounted on map/battle" report. When the
/// player opens the party screen, dumps the main party's raw + weighted count state (per-slot bodies,
/// wounded, resolved weight, special-currency flag) plus a scan of where the player's special-currency
/// troops actually live (other war parties + owned garrisons). One reproduction pins whether the "20"
/// is a wounded/fighting-strength display, troops living outside the main party, or a stale cached
/// count — see the plan's Step-3 table. Remove once the root cause is identified.
///
/// Runs regardless of the <c>EnableTroopWeight</c> setting (standalone behavior, not gated behind the
/// weight patches) so the log also reveals whether Troop Weight is on. Whole path is try/catch'd so a
/// diagnostic failure can never break the party screen.
/// </summary>
public class TroopCountDiagnosticsBehavior : CampaignBehaviorBase
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly ISpecialResourceConfigProvider _resourceConfig;
    private readonly IModLogger _logger;

    private bool _subscribed;

    public TroopCountDiagnosticsBehavior(
        ITroopWeightService troopWeightService,
        ISpecialResourceConfigProvider resourceConfig,
        IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _resourceConfig = resourceConfig;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        ScreenManager.OnPushScreen += OnScreenPushed;
        _subscribed = true;
        CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, Unsubscribe);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        ScreenManager.OnPushScreen -= OnScreenPushed;
        _subscribed = false;
    }

    private void OnScreenPushed(ScreenBase screen)
    {
        try
        {
            if (screen?.GetType().Name != "GauntletPartyScreen")
                return;

            DumpMainParty();
            DumpSpecialTroopLocations();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{TroopCountDiagnosticsFormatter.Prefix} dump failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void DumpMainParty()
    {
        var party = MobileParty.MainParty?.Party;
        var roster = party?.MemberRoster;
        if (party == null || roster == null)
            return;

        int count = roster.Count;
        var slots = new List<DiagnosticSlot>(count);
        for (int i = 0; i < count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            var character = element.Character;
            string id = character?.StringId ?? "<null>";
            float weight = _troopWeightService.GetTroopWeight(character);
            bool special = character != null && _resourceConfig.GetTroopCost(id) != null;
            slots.Add(new DiagnosticSlot(id, element.Number, element.WoundedNumber, weight, special));
        }

        var (weightedHealthy, weightedWounded) = _troopWeightService.GetWeightedHealthAndWounded(party);
        bool enableWeight = TaomSettings.Instance?.EnableTroopWeight ?? true;

        var snapshot = new DiagnosticSnapshot(
            enableWeight,
            count,
            roster.TotalManCount,
            roster.TotalWounded,
            party.NumberOfAllMembers,
            party.NumberOfHealthyMembers,
            weightedHealthy,
            weightedWounded,
            party.PartySizeLimit,
            slots);

        foreach (var line in TroopCountDiagnosticsFormatter.Format(snapshot))
            _logger.LogInfo(line);
    }

    // Where do the player's special-currency troops actually live? Scans the clan's other war parties
    // and owned-settlement garrisons so we can tell whether an auto-add placed them OUTSIDE the main
    // party (hypothesis B). The main party re-appears here too, which cross-checks the block above.
    private void DumpSpecialTroopLocations()
    {
        var clan = Hero.MainHero?.Clan;
        if (clan == null)
            return;

        foreach (var component in clan.WarPartyComponents)
        {
            var mobileParty = component?.MobileParty;
            if (mobileParty == null)
                continue;
            LogSpecialInRoster($"party:{mobileParty.Name}", mobileParty.MemberRoster);
        }

        foreach (var settlement in clan.Settlements)
        {
            var garrison = settlement?.Town?.GarrisonParty;
            if (garrison == null)
                continue;
            LogSpecialInRoster($"garrison:{settlement.Name}", garrison.MemberRoster);
        }
    }

    private void LogSpecialInRoster(string where, TroopRoster roster)
    {
        if (roster == null)
            return;

        int count = roster.Count;
        for (int i = 0; i < count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            var character = element.Character;
            if (character == null || element.Number <= 0)
                continue;
            if (_resourceConfig.GetTroopCost(character.StringId) == null)
                continue;

            _logger.LogInfo(
                $"{TroopCountDiagnosticsFormatter.Prefix} special-troop location: {where} " +
                $"id={character.StringId} number={element.Number} wounded={element.WoundedNumber}");
        }
    }
}
