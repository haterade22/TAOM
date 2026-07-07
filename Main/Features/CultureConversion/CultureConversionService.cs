using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.CultureConversion;

/// <summary>
/// Pure decision logic for gradual culture conversion of conquered fiefs. Operates on string ids +
/// the persisted store; all engine reads/writes go through <see cref="ICultureConversionAdapter"/>.
///
/// Model (see <see cref="SettlementConversionRecord"/>):
///   - A cross-culture conquest starts a hold-timer toward the new owner's culture (gated on the
///     target being a recruitable culture and, optionally, on not being player-owned).
///   - After <c>RequiredHoldDays</c> (and an optional loyalty floor) the fief converts:
///     <c>Settlement.Culture</c> + bound villages flip to the new culture, foreign-culture notables are
///     replaced with new-culture ones (toggleable; property transfers, relations reset), and notable
///     volunteer slots are cleared so recruits repopulate from the new pool.
///   - Converting back to the original culture removes the override entirely (restores vanilla
///     same-culture loyalty state). Re-conquest mid-timer restarts/cancels as the owner changes.
/// </summary>
public class CultureConversionService : ICultureConversionService
{
    private readonly ICultureConversionStore _store;
    private readonly ICultureConversionAdapter _adapter;
    private readonly ICultureConversionSettingsProvider _settings;
    private readonly IVolunteerRecruitmentService _recruitment;
    private readonly IModLogger _logger;

    public CultureConversionService(
        ICultureConversionStore store,
        ICultureConversionAdapter adapter,
        ICultureConversionSettingsProvider settings,
        IVolunteerRecruitmentService recruitment,
        IModLogger logger)
    {
        _store = store;
        _adapter = adapter;
        _settings = settings;
        _recruitment = recruitment;
        _logger = logger;
    }

    public void OnSettlementConquered(string settlementId, double nowDays)
    {
        if (!_settings.IsEnabled)
            return;
        if (string.IsNullOrEmpty(settlementId) || !_adapter.IsFortification(settlementId))
            return;

        var ownerCulture = _adapter.GetOwnerCultureId(settlementId);
        if (string.IsNullOrEmpty(ownerCulture) || !IsStorableId(ownerCulture))
            return; // no owner culture, or an id that would corrupt the '|'-delimited save format

        var exists = _store.TryGet(settlementId, out var record);
        if (!exists)
        {
            // First time we've touched this fief: Settlement.Culture is still the authored original.
            var original = _adapter.GetCurrentCultureId(settlementId);
            if (string.IsNullOrEmpty(original) || !IsStorableId(original))
                return;
            record = new SettlementConversionRecord(settlementId, original);
        }

        // Already (effectively) this culture → no conversion needed; cancel a stale timer.
        if (ownerCulture == record.EffectiveCultureId)
        {
            CancelPendingIfTracked(exists, record);
            return;
        }

        // R4: never convert to a culture we have no recruitment pool for (minor / bandit / mercenary).
        if (!_recruitment.HasCulturePool(ownerCulture))
        {
            CancelPendingIfTracked(exists, record);
            return;
        }

        // R5: optionally leave the player's own conquests alone.
        if (!_settings.ConvertPlayerOwnedSettlements && _adapter.IsPlayerOwned(settlementId))
        {
            CancelPendingIfTracked(exists, record);
            return;
        }

        // Ownership moved within the same culture mid-hold (fief grant after capture, barter,
        // re-grant, same-culture recapture) — assimilation continues, the clock does NOT restart.
        // Without this, a contested frontier fief re-queues forever and never converts
        // (castle_E6 queued 16× with zero completions, play-test 2026-07-07).
        if (record.HasPending && record.PendingTargetCultureId == ownerCulture)
        {
            _logger.LogDebug($"CultureConversion: {settlementId} already pending toward {ownerCulture} — timer continues");
            return;
        }

        record.StartPending(nowDays, ownerCulture);
        _store.Put(record);
        _logger.LogDebug($"CultureConversion: {settlementId} queued for conversion to {ownerCulture} (hold {_settings.RequiredHoldDays}d)");
    }

    public void RunDailyChecks(double nowDays)
    {
        if (!_settings.IsEnabled)
            return;

        // Snapshot — ApplyConversion mutates the store.
        var pending = _store.GetAll().Where(r => r.HasPending).ToList();
        foreach (var record in pending)
        {
            // HasPending guarantees PendingStartDays has a value (filtered above).
            if (nowDays - record.PendingStartDays.GetValueOrDefault() < _settings.RequiredHoldDays)
                continue;

            // Defensive: the owner may have changed without us catching the event. Only convert toward
            // the CURRENT owner's culture; otherwise drop the stale timer.
            var ownerCulture = _adapter.GetOwnerCultureId(record.SettlementId);
            if (string.IsNullOrEmpty(ownerCulture) || ownerCulture != record.PendingTargetCultureId)
            {
                _logger.LogDebug($"CultureConversion: {record.SettlementId} stale timer toward {record.PendingTargetCultureId} dropped (owner culture now '{ownerCulture ?? "none"}')");
                record.ClearPending();
                PersistOrDrop(record);
                continue;
            }

            if (_settings.RequireStableLoyalty)
            {
                var loyalty = _adapter.GetLoyalty(record.SettlementId);
                if (!loyalty.HasValue || loyalty.Value < _settings.MinLoyaltyToConvert)
                    continue; // not pacified yet — re-check next day
            }

            ApplyConversion(record, ownerCulture);
        }
    }

    public void ReapplyConvertedCultures()
    {
        // Runs regardless of the master toggle: a fief converted in a prior session stays converted
        // (toggling off stops NEW conversions, it does not un-convert existing ones).
        foreach (var record in _store.GetAll().ToList())
        {
            if (!record.IsConverted)
                continue;
            if (!_adapter.SetSettlementCulture(record.SettlementId, record.AppliedCultureId))
            {
                // The applied culture (or the settlement) no longer resolves — e.g. a culture removed in a
                // later mod version. Purge the stale record: leaving it would keep IsConverted==true while
                // Settlement.Culture stayed at the XML original, so recruitment would take the converted
                // branch pointing at a culture that was never actually applied. Dropping it falls back to
                // the standard recruitment cascade — the correct degraded behavior.
                _logger.LogWarning($"CultureConversion: dropping stale conversion for {record.SettlementId} — applied culture '{record.AppliedCultureId}' no longer resolves");
                _store.Remove(record.SettlementId);
                continue;
            }
            foreach (var villageId in _adapter.GetBoundVillageSettlementIds(record.SettlementId))
                _adapter.SetSettlementCulture(villageId, record.AppliedCultureId);
        }
    }

    private void ApplyConversion(SettlementConversionRecord record, string targetCulture)
    {
        _adapter.SetSettlementCulture(record.SettlementId, targetCulture);
        ReplaceForeignNotables(record.SettlementId, targetCulture);
        _adapter.ResetVolunteers(record.SettlementId);
        foreach (var villageId in _adapter.GetBoundVillageSettlementIds(record.SettlementId))
        {
            _adapter.SetSettlementCulture(villageId, targetCulture);
            ReplaceForeignNotables(villageId, targetCulture);
            _adapter.ResetVolunteers(villageId);
        }

        record.ClearPending();

        if (targetCulture == record.OriginalCultureId)
        {
            // Restored to the authored culture — drop the override so recruitment uses the standard
            // cascade and vanilla's same-culture loyalty state returns.
            _store.Remove(record.SettlementId);
            _logger.LogInfo($"CultureConversion: {record.SettlementId} restored to original culture {targetCulture}");
        }
        else
        {
            record.AppliedCultureId = targetCulture;
            _store.Put(record);
            _logger.LogInfo($"CultureConversion: {record.SettlementId} converted to {targetCulture}");
        }
    }

    // Replacement must run AFTER SetSettlementCulture: the adapter draws the replacement's template
    // from the settlement's CURRENT culture. A skipped notable (no template, unresolvable hero) is
    // kept and logged — it never blocks the conversion itself.
    private void ReplaceForeignNotables(string settlementId, string targetCulture)
    {
        if (!_settings.ReplaceNotablesOnConversion)
            return;

        foreach (var notable in _adapter.GetNotables(settlementId))
        {
            if (!notable.IsAlive || notable.CultureId == targetCulture)
                continue;
            if (!_adapter.ReplaceNotable(notable.HeroId))
                _logger.LogWarning($"CultureConversion: kept notable {notable.HeroId} in {settlementId} — replacement skipped");
        }
    }

    // Cancels a running timer. For a brand-new (untracked) record we never stored, do nothing —
    // avoids creating clutter for same-culture or gated-out conquests.
    private void CancelPendingIfTracked(bool exists, SettlementConversionRecord record)
    {
        if (!exists || !record.HasPending)
            return;
        _logger.LogDebug($"CultureConversion: {record.SettlementId} pending conversion to {record.PendingTargetCultureId} cancelled");
        record.ClearPending();
        PersistOrDrop(record);
    }

    // A record with no applied override and no pending timer carries no information — drop it.
    private void PersistOrDrop(SettlementConversionRecord record)
    {
        if (record.IsConverted)
            _store.Put(record);
        else
            _store.Remove(record.SettlementId);
    }

    // Culture StringIds are simple author-controlled identifiers and never contain '|' in practice, but the
    // save format is '|'-delimited (SettlementConversionRecord.Serialize) and the engine does not enforce the
    // invariant, so reject any id that would corrupt a stored record (Codex review LOW, 2026-06-02).
    private static bool IsStorableId(string cultureId)
        => cultureId != null && cultureId.IndexOf('|') < 0;
}
