using System.Collections.Generic;
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

    /// <summary>
    /// A join/attach was requested and has not been satisfied. Persisted so a save taken mid-battle
    /// resumes the retry instead of waiting for the next edge that may never come.
    /// </summary>
    public bool PendingCommanderAttachment { get; set; }

    /// <summary>
    /// Shore leave is active: the player has stepped out of the column's camp into the settlement it
    /// is resting in, so the settlement menu is his to use (field report 1). Suspends ONLY the
    /// town/castle/village redirects — every other redirect and every service rule still applies.
    ///
    /// Persisted because the pass has to survive a save taken while shopping; revoked by
    /// <see cref="TAOM.Features.Enlistment.TownLeavePolicy.ShouldRevokeLeave"/> the moment the column
    /// is no longer at rest in that settlement.
    /// </summary>
    public bool OnTownLeave { get; set; }

    /// <summary>
    /// Faction ids this service put the player at war with (field report 5), and the ids he was
    /// ALREADY at war with when he swore. Both are needed: the discharge unwinds the first minus the
    /// second, which is what stops enlistment becoming a free universal peace button the way it is
    /// in ServeAsSoldier.
    ///
    /// Serialised as comma-separated ids inside the existing semicolon-delimited record, so a
    /// faction id may not contain a comma or a semicolon — StringIds never do.
    /// </summary>
    public List<string> MirroredWars { get; set; } = new List<string>();

    public List<string> EnemiesAtOath { get; set; } = new List<string>();

    /// <summary>
    /// The player's own <c>MapFaction</c> id at the moment the mirror declared those wars — the
    /// identity that DID the declaring, pinned so the unwind can refuse to act on behalf of a
    /// different one.
    ///
    /// WHY IT IS NEEDED. Verified on installed 1.4.8, <c>Hero.MapFaction</c> is
    /// <c>Clan.Kingdom ?? Clan</c>, and the enlist gate deliberately admits a player whose clan is
    /// already a kingdom vassal. So the identity is not stable across a term of service: a player
    /// who is independent at oath declares as his CLAN, and if his clan joins a kingdom before
    /// discharge the unwind would resolve <c>MapFaction</c> live and call
    /// <c>MakePeaceAction.Apply</c> on the KINGDOM — ending a war for every vassal in it as a side
    /// effect of one soldier's discharge, invisibly. The reverse (vassal at oath, independent at
    /// discharge) silently strands the kingdom in wars the oath created.
    ///
    /// Empty means no pin was recorded — a save from before this field existed. The unwind then
    /// proceeds as it did before, which is the status quo rather than a new hazard, and says so in
    /// the log.
    /// </summary>
    public string OathFactionId { get; set; }

    /// <summary>
    /// Campaign-hours stamp before which no further attach/join attempt is made. ONE budget shared
    /// by the real-time pump and the hourly reconciler, so adding the pump cannot multiply the
    /// attempt rate. Null means 'retry immediately'.
    /// </summary>
    public double? NextAttachRetryAtHours { get; set; }

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
        PendingCommanderAttachment = false;
        OnTownLeave = false;
        MirroredWars = new List<string>();
        EnemiesAtOath = new List<string>();
        OathFactionId = null;
        NextAttachRetryAtHours = null;
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
        PendingCommanderAttachment = other.PendingCommanderAttachment;
        OnTownLeave = other.OnTownLeave;
        MirroredWars = new List<string>(other.MirroredWars ?? new List<string>());
        EnemiesAtOath = new List<string>(other.EnemiesAtOath ?? new List<string>());
        OathFactionId = other.OathFactionId;
        NextAttachRetryAtHours = other.NextAttachRetryAtHours;
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
        if (PendingCommanderAttachment)
            parts.Add("pendingAttach=1");
        if (OnTownLeave)
            parts.Add("onTownLeave=1");
        if (MirroredWars != null && MirroredWars.Count > 0)
            parts.Add("mirroredWars=" + string.Join(",", MirroredWars));
        if (EnemiesAtOath != null && EnemiesAtOath.Count > 0)
            parts.Add("enemiesAtOath=" + string.Join(",", EnemiesAtOath));
        if (!string.IsNullOrEmpty(OathFactionId))
            parts.Add("oathFactionId=" + OathFactionId);
        if (NextAttachRetryAtHours.HasValue)
            parts.Add("nextAttachHour=" + NextAttachRetryAtHours.Value.ToString("R", inv));
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
        string pendingAttachRaw = null, nextAttachHourRaw = null, onTownLeaveRaw = null;
        string mirroredWarsRaw = null, enemiesAtOathRaw = null, oathFactionIdRaw = null;

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
                case "pendingAttach": pendingAttachRaw = value; break;
                case "onTownLeave": onTownLeaveRaw = value; break;
                case "mirroredWars": mirroredWarsRaw = value; break;
                case "enemiesAtOath": enemiesAtOathRaw = value; break;
                case "oathFactionId": oathFactionIdRaw = value; break;
                case "nextAttachHour": nextAttachHourRaw = value; break;
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
            PendingCommanderAttachment = pendingAttachRaw == "1",
            OnTownLeave = onTownLeaveRaw == "1",
            MirroredWars = ParseIdList(mirroredWarsRaw),
            EnemiesAtOath = ParseIdList(enemiesAtOathRaw),
            // Absent in a save from before the pin existed. Empty means 'unknown', which
            // UnwindServiceWars treats as the pre-pin status quo rather than a new refusal.
            OathFactionId = NullIfEmpty(oathFactionIdRaw),
            // ParseFiniteDayOrNull, not a bare parse: a non-finite stamp would compare false
            // against every future hour and freeze EVERY retry for the rest of the campaign.
            NextAttachRetryAtHours = ParseFiniteDayOrNull(nextAttachHourRaw),
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

    /// <summary>
    /// EnlistedBattle and Discharging never persist — both coerce to EnlistedAttached.
    ///
    /// EnlistedDetachedOnDuty joins them as of 2026-08-08, for a different reason: field duties no
    /// longer detach the player, so nothing can PRODUCE that state any more. It is retired, not
    /// deleted — the enum member and its numeric value 4 must survive, because a save written
    /// before the change carries `state=4` and `TryParse` rejects any value that fails
    /// `Enum.IsDefined`, which would drop the WHOLE core record and silently un-enlist the player.
    /// Coercing here is the entire state migration: such a save loads as an ordinary attached
    /// soldier, which is where the reconciler would have put them anyway.
    /// </summary>
    private static EnlistmentState ToPersistedState(EnlistmentState state)
    {
        return state == EnlistmentState.EnlistedBattle
               || state == EnlistmentState.Discharging
               || state == EnlistmentState.EnlistedDetachedOnDuty
            ? EnlistmentState.EnlistedAttached
            : state;
    }

    /// <summary>
    /// Parse a comma-separated id list. An absent or malformed value yields an EMPTY list, never
    /// null: the war policy treats an empty mirror as "nothing to unwind", which is the safe
    /// reading, while a null would put a NullReferenceException on the discharge path.
    /// </summary>
    private static List<string> ParseIdList(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(raw))
            return list;

        foreach (var part in raw.Split(','))
        {
            var id = part?.Trim();
            if (!string.IsNullOrEmpty(id))
                list.Add(id);
        }
        return list;
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
