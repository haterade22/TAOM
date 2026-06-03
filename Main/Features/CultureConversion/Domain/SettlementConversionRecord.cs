using System.Globalization;

namespace TAOM.Features.CultureConversion.Domain;

/// <summary>
/// Per-town/castle conversion state for the gradual culture-conversion feature.
///
/// A settlement enters our records only when it is conquered cross-culture (the owner's culture
/// differs from the settlement's effective culture). We capture the ORIGINAL (authored) culture
/// once, so a later reconquest BACK to the original culture is unambiguous and removes the override
/// (restoring the vanilla same-culture loyalty state) rather than recording a redundant one.
///
/// Lifecycle:
///   - <see cref="PendingStartDays"/> / <see cref="PendingTargetCultureId"/> = a conversion timer in progress.
///   - <see cref="AppliedCultureId"/> = the override currently applied (null = currently the original culture).
///
/// <see cref="Settlement.Culture"/> is NOT a saveable engine field, so completed overrides are
/// re-applied on load from <see cref="AppliedCultureId"/>. Bound villages are NOT stored here — they
/// follow the parent (re-applied from the parent's live BoundVillages, and covered in recruitment via
/// the volunteer context's BoundSettlementId).
/// </summary>
public sealed class SettlementConversionRecord
{
    public string SettlementId { get; }

    /// <summary>The authored/XML culture captured the first time this settlement was ever queued. Immutable.</summary>
    public string OriginalCultureId { get; }

    /// <summary>The override currently applied, or null when the settlement is currently its original culture.</summary>
    public string AppliedCultureId { get; set; }

    /// <summary>Conquest time (in <c>CampaignTime.ToDays</c>) of the in-progress conversion, or null.</summary>
    public double? PendingStartDays { get; set; }

    /// <summary>Culture the in-progress conversion is heading toward, or null.</summary>
    public string PendingTargetCultureId { get; set; }

    public SettlementConversionRecord(
        string settlementId,
        string originalCultureId,
        string appliedCultureId = null,
        double? pendingStartDays = null,
        string pendingTargetCultureId = null)
    {
        SettlementId = settlementId;
        OriginalCultureId = originalCultureId;
        AppliedCultureId = appliedCultureId;
        PendingStartDays = pendingStartDays;
        PendingTargetCultureId = pendingTargetCultureId;
    }

    /// <summary>True when an override is applied (settlement is not its original culture).</summary>
    public bool IsConverted => !string.IsNullOrEmpty(AppliedCultureId);

    /// <summary>True when a conversion timer is running.</summary>
    public bool HasPending => PendingStartDays.HasValue && !string.IsNullOrEmpty(PendingTargetCultureId);

    /// <summary>The culture the settlement currently is — the applied override, or the original.</summary>
    public string EffectiveCultureId => string.IsNullOrEmpty(AppliedCultureId) ? OriginalCultureId : AppliedCultureId;

    public void StartPending(double nowDays, string targetCultureId)
    {
        PendingStartDays = nowDays;
        PendingTargetCultureId = targetCultureId;
    }

    public void ClearPending()
    {
        PendingStartDays = null;
        PendingTargetCultureId = null;
    }

    // Composite storage form: original|applied|pendingStartDays|pendingTarget
    // Empty segment = absent (applied/pending are optional). Day value uses round-trip "R" format,
    // invariant culture (mirrors PendingMessenger). '|' never appears in culture StringIds.
    public string Serialize()
    {
        var inv = CultureInfo.InvariantCulture;
        var applied = AppliedCultureId ?? "";
        var pendingDays = PendingStartDays.HasValue ? PendingStartDays.Value.ToString("R", inv) : "";
        var pendingTarget = PendingTargetCultureId ?? "";
        return $"{OriginalCultureId}|{applied}|{pendingDays}|{pendingTarget}";
    }

    /// <summary>
    /// Parse a stored record. Returns false (caller drops + logs) only on STRUCTURAL failure
    /// (wrong segment count, empty original). A malformed/non-finite pending timer is dropped on
    /// its own while the applied override is preserved — a converted settlement staying converted is
    /// the safe outcome, mirroring PendingMessenger's NaN-reject without discarding a real conversion.
    /// </summary>
    public static bool TryParse(string settlementId, string serialized, out SettlementConversionRecord record)
    {
        record = null;
        if (string.IsNullOrEmpty(settlementId) || string.IsNullOrEmpty(serialized))
            return false;

        var parts = serialized.Split('|');
        if (parts.Length != 4)
            return false;

        var originalCultureId = parts[0];
        if (string.IsNullOrEmpty(originalCultureId))
            return false;

        var appliedCultureId = string.IsNullOrEmpty(parts[1]) ? null : parts[1];

        double? pendingStartDays = null;
        string pendingTargetCultureId = null;
        var hasPendingDays = !string.IsNullOrEmpty(parts[2]);
        var hasPendingTarget = !string.IsNullOrEmpty(parts[3]);
        if (hasPendingDays && hasPendingTarget)
        {
            var inv = CultureInfo.InvariantCulture;
            // Reject NaN/±Infinity (NumberStyles.Float accepts them) — a non-finite start makes
            // `now - start >= requiredHoldDays` never true, freezing the timer forever. Drop the
            // pending portion only; keep any applied override.
            if (double.TryParse(parts[2], NumberStyles.Float, inv, out var days)
                && !double.IsNaN(days) && !double.IsInfinity(days))
            {
                pendingStartDays = days;
                pendingTargetCultureId = parts[3];
            }
        }

        record = new SettlementConversionRecord(
            settlementId, originalCultureId, appliedCultureId, pendingStartDays, pendingTargetCultureId);
        return true;
    }
}
