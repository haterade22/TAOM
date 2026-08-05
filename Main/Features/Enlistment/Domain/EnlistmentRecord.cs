using System;
using System.Globalization;

namespace TAOM.Features.Enlistment.Domain;

/// <summary>
/// The core persisted service record: who is enlisted, under whom, and the contract/grace
/// timers. Content counters (rank progress, trust, duty state) live in sibling store
/// sections added by later phases — this record stays small and load-bearing.
///
/// Storage form is <c>key=value</c> pairs joined with <c>';'</c> (Bannerlord StringIds and
/// invariant "R" floats never contain <c>';'</c>/<c>'='</c>). Unknown keys are ignored on
/// parse so older TAOM builds tolerate additive fields. Day values use
/// <c>CampaignTime.ToDays</c> semantics.
/// </summary>
public sealed class EnlistmentRecord
{
    public EnlistmentState State { get; set; }

    /// <summary>StringId of the hero who swore the oath — the identity guard against co-op join / heir succession.</summary>
    public string EnlistedHeroId { get; set; }

    public string CommanderHeroId { get; set; }

    /// <summary>Commander a petition is pending with (pre-oath), or null.</summary>
    public string PetitionCommanderId { get; set; }

    public double? EnlistedAtDay { get; set; }

    public double? ContractEndDay { get; set; }

    /// <summary>End of the CommanderUnavailable grace window, or null when not in grace.</summary>
    public double? GraceEndsAtDay { get; set; }

    public bool IsEnlisted =>
        State == EnlistmentState.EnlistedAttached
        || State == EnlistmentState.EnlistedBattle
        || State == EnlistmentState.EnlistedDetachedOnDuty
        || State == EnlistmentState.EnlistedPlayerCaptive
        || State == EnlistmentState.CommanderUnavailable;

    public void Reset()
    {
        State = EnlistmentState.NotEnlisted;
        EnlistedHeroId = null;
        CommanderHeroId = null;
        PetitionCommanderId = null;
        EnlistedAtDay = null;
        ContractEndDay = null;
        GraceEndsAtDay = null;
    }

    public void CopyFrom(EnlistmentRecord other)
    {
        if (other == null)
            return;
        State = other.State;
        EnlistedHeroId = other.EnlistedHeroId;
        CommanderHeroId = other.CommanderHeroId;
        PetitionCommanderId = other.PetitionCommanderId;
        EnlistedAtDay = other.EnlistedAtDay;
        ContractEndDay = other.ContractEndDay;
        GraceEndsAtDay = other.GraceEndsAtDay;
    }

    public string Serialize()
    {
        var inv = CultureInfo.InvariantCulture;
        var parts = new System.Collections.Generic.List<string>(7)
        {
            "state=" + ((int)ToPersistedState(State)).ToString(inv),
        };
        if (!string.IsNullOrEmpty(EnlistedHeroId))
            parts.Add("heroId=" + EnlistedHeroId);
        if (!string.IsNullOrEmpty(CommanderHeroId))
            parts.Add("commanderId=" + CommanderHeroId);
        if (!string.IsNullOrEmpty(PetitionCommanderId))
            parts.Add("petitionId=" + PetitionCommanderId);
        if (EnlistedAtDay.HasValue)
            parts.Add("enlistedDay=" + EnlistedAtDay.Value.ToString("R", inv));
        if (ContractEndDay.HasValue)
            parts.Add("contractEndDay=" + ContractEndDay.Value.ToString("R", inv));
        if (GraceEndsAtDay.HasValue)
            parts.Add("graceEndDay=" + GraceEndsAtDay.Value.ToString("R", inv));
        return string.Join(";", parts);
    }

    /// <summary>
    /// Parse a stored record. Returns false when the record cannot be FAITHFULLY restored
    /// (missing/invalid state, or an enlisted-family/petition state missing its identity
    /// ids) — the caller falls back to a fresh NotEnlisted record and warns; the load
    /// normalizer's ownerless-hidden-party guard rescues the world side. A malformed or
    /// non-finite day value is dropped field-level (null) while the record survives.
    /// </summary>
    public static bool TryParse(string serialized, out EnlistmentRecord record)
    {
        record = null;
        if (string.IsNullOrEmpty(serialized))
            return false;

        string stateRaw = null, heroId = null, commanderId = null, petitionId = null;
        string enlistedDayRaw = null, contractEndRaw = null, graceEndRaw = null;

        foreach (var part in serialized.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = part.Substring(0, eq);
            var value = part.Substring(eq + 1);
            switch (key)
            {
                case "state": stateRaw = value; break;
                case "heroId": heroId = value; break;
                case "commanderId": commanderId = value; break;
                case "petitionId": petitionId = value; break;
                case "enlistedDay": enlistedDayRaw = value; break;
                case "contractEndDay": contractEndRaw = value; break;
                case "graceEndDay": graceEndRaw = value; break;
                // Unknown keys: additive fields from a newer same-major build — ignore.
            }
        }

        if (stateRaw == null
            || !int.TryParse(stateRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stateInt)
            || !Enum.IsDefined(typeof(EnlistmentState), stateInt))
        {
            return false;
        }

        var state = ToPersistedState((EnlistmentState)stateInt);

        var parsed = new EnlistmentRecord
        {
            State = state,
            EnlistedHeroId = NullIfEmpty(heroId),
            CommanderHeroId = NullIfEmpty(commanderId),
            PetitionCommanderId = NullIfEmpty(petitionId),
            EnlistedAtDay = ParseFiniteDayOrNull(enlistedDayRaw),
            ContractEndDay = ParseFiniteDayOrNull(contractEndRaw),
            GraceEndsAtDay = ParseFiniteDayOrNull(graceEndRaw),
        };

        if (parsed.IsEnlisted
            && (string.IsNullOrEmpty(parsed.EnlistedHeroId) || string.IsNullOrEmpty(parsed.CommanderHeroId)))
        {
            return false;
        }

        if (parsed.State == EnlistmentState.PetitionPending && string.IsNullOrEmpty(parsed.PetitionCommanderId))
            return false;

        record = parsed;
        return true;
    }

    /// <summary>EnlistedBattle and Discharging never persist — both coerce to EnlistedAttached.</summary>
    private static EnlistmentState ToPersistedState(EnlistmentState state)
    {
        return state == EnlistmentState.EnlistedBattle || state == EnlistmentState.Discharging
            ? EnlistmentState.EnlistedAttached
            : state;
    }

    private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static double? ParseFiniteDayOrNull(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return null;
        // NumberStyles.Float accepts "NaN"/"Infinity" — a non-finite timer would freeze
        // contract/grace comparisons forever, so reject to null (field-level tolerance).
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var days)
            && !double.IsNaN(days) && !double.IsInfinity(days))
        {
            return days;
        }
        return null;
    }
}
