using System;
using System.Collections.Generic;
using TAOM.Features.FieldCamp;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// Refuge's hook into the FieldCamp overlay seam, replacing the source module's three mutable
/// static delegates on FieldCampButtonHook. Near a ready refuge the camp button reads
/// "Refuge"/"Stronghold", camp creation is blocked while any refuge stands in manage range, and a
/// raising refuge feeds the overlay a status line with progress.
///
/// <para>The three delegates run at 4 Hz from the overlay VM, and each would otherwise scan
/// MobileParty.All to find refuge parties by id. One cached snapshot, refreshed at most every
/// 250 ms, serves all three; the overlay never sees a stale answer for longer than a quarter
/// second, which is below what a player can perceive on the map.</para>
/// </summary>
public sealed class RefugeCampContributor : ICampOverlayContributor
{
    private const int CacheIntervalMs = 250;

    private readonly IRefugeService _refuges;
    private readonly IRefugeSettingsProvider _settings;

    private int _lastRefreshMs;
    private bool _cacheValid;

    private string? _caption;
    private string? _blockedReason;
    private CampOverlayStatus? _status;

    public RefugeCampContributor(IRefugeService refuges, IRefugeSettingsProvider settings)
    {
        _refuges = refuges;
        _settings = settings;
    }

    public string? CaptionOverride()
    {
        RefreshCacheThrottled();
        return _caption;
    }

    public string? CreationBlockedReason()
    {
        RefreshCacheThrottled();
        return _blockedReason;
    }

    public CampOverlayStatus? OverlayStatus()
    {
        RefreshCacheThrottled();
        return _status;
    }

    private void RefreshCacheThrottled()
    {
        // Environment.TickCount wraps after ~25 days; unchecked subtraction stays correct.
        int now = Environment.TickCount;
        if (_cacheValid && unchecked(now - _lastRefreshMs) < CacheIntervalMs)
            return;
        _lastRefreshMs = now;
        _cacheValid = true;

        _caption = null;
        _blockedReason = null;
        _status = null;

        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return;

            IReadOnlyCollection<RefugeData> refuges = _refuges.AllRefuges;
            if (refuges.Count == 0)
                return;

            float range = _settings.ManageRange;
            var mainPosition = main.GetPosition2D;

            foreach (var refuge in refuges)
            {
                var party = ResolveParty(refuge.PartyId);
                if (party == null || mainPosition.Distance(party.GetPosition2D) > range)
                    continue;

                // ANY refuge in range blocks pitching a fresh camp on top of it, ready or not.
                _blockedReason ??= new TextObject(
                    "{=taom_rf_blocks_camp}A refuge stands here - dismantle it to pitch a normal camp.").ToString();

                if (refuge.IsReady)
                {
                    _caption ??= (refuge.TierEnum == RefugeTier.Stronghold
                        ? new TextObject("{=taom_rf_cap_stronghold}Stronghold")
                        : new TextObject("{=taom_rf_cap_refuge}Refuge")).ToString();
                }

                if (refuge.Building && _status == null)
                {
                    var text = refuge.BuildingUpgrade
                        ? new TextObject("{=taom_rf_status_upgrading}Rebuilding into a stronghold.")
                        : new TextObject("{=taom_rf_status_raising}Raising refuge.");
                    _status = new CampOverlayStatus(text.ToString(), ProgressPercent(refuge));
                }
            }
        }
        catch
        {
            // Campaign mid-teardown: a blank overlay answer for one 250 ms window is invisible;
            // a throw here would ride the 4 Hz overlay refresh.
            _caption = null;
            _blockedReason = null;
            _status = null;
        }
    }

    private static int ProgressPercent(RefugeData refuge)
    {
        float progress = refuge.BuildProgress();
        if (!(progress > 0f))
            return 0;
        return progress >= 1f ? 100 : (int)(progress * 100f);
    }

    private static MobileParty? ResolveParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return null;
        foreach (var party in MobileParty.All)
        {
            if (party != null && string.Equals(party.StringId, partyId, StringComparison.Ordinal))
                return party;
        }
        return null;
    }
}
