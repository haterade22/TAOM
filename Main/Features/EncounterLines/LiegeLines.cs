using System;
using System.Collections.Generic;

namespace TAOM.Features.EncounterLines;

/// <summary>
/// The liege conversations Encounter Lines rewrites through its text seam (docs/features/encounter-lines.md "Liege
/// conversations"): vanilla's sentence ids in <c>LordConversationsCampaignBehavior</c> for leaving a realm's service, joining
/// it as a vassal and the mercenary contract, each with the TAOM game text whose rows hold the lines. A row's variation is
/// <c>realm_&lt;kingdom&gt;</c> for the realm's ruler, on the beats in <see cref="RealmTexts"/> only, or
/// <c>group_&lt;speaker group&gt;</c>.
/// </summary>
internal static class LiegeLines
{
    internal const string RealmPrefix = "realm_";
    internal const string GroupPrefix = "group_";

    internal const string LeaveAsk = "str_taom_liege_leave_ask";
    internal const string LeaveConfirm = "str_taom_liege_leave_confirm";
    internal const string LeaveGranted = "str_taom_liege_leave_granted";
    internal const string LeaveFarewell = "str_taom_liege_leave_farewell";
    internal const string MercenaryEnd = "str_taom_liege_merc_end";
    internal const string MercenaryEndFinal = "str_taom_liege_merc_end_final";
    internal const string JoinHow = "str_taom_liege_join_how";
    internal const string VassalLowRelation = "str_taom_liege_vassal_low";
    internal const string VassalReject = "str_taom_liege_vassal_reject";
    internal const string VassalAccept = "str_taom_liege_vassal_accept";
    internal const string VassalBond = "str_taom_liege_vassal_bond";
    internal const string VassalWelcome = "str_taom_liege_vassal_welcome";
    internal const string MercenaryPitch = "str_taom_liege_merc_pitch";
    internal const string MercenaryAccept = "str_taom_liege_merc_accept";
    internal const string MercenaryDecline = "str_taom_liege_merc_decline";

    /// <summary>Vanilla's sentence id to the TAOM game text. "1220" and "1221" are vanilla's two answers to a vassal who asks for release.</summary>
    internal static readonly IReadOnlyDictionary<string, string> TextOfSentence = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1220"] = LeaveAsk,
        ["1221"] = LeaveAsk,
        ["1225"] = LeaveConfirm,
        ["player_leave_faction_accepted"] = LeaveGranted,
        ["player_leaves_faction"] = LeaveFarewell,
        ["player_want_to_end_mercenary_service_response"] = MercenaryEnd,
        ["lord_ask_exit_service_confirm_final"] = MercenaryEndFinal,
        ["player_want_to_join_faction_as_mercenary_or_vassal_answer"] = JoinHow,
        ["vassalage_offer_player_has_low_relation"] = VassalLowRelation,
        ["vassalage_offer_rejected"] = VassalReject,
        ["vassalage_offer_accepted"] = VassalAccept,
        ["player_is_accepted_as_a_vassal"] = VassalBond,
        ["1210"] = VassalWelcome,
        ["player_want_to_join_faction_as_mercenary_lord_answer_3"] = MercenaryPitch,
        ["mercenary_player_accepts_lord_answer"] = MercenaryAccept,
        ["mercenary_player_rejects_lord_answer"] = MercenaryDecline,
    };

    /// <summary>The beats a realm's ruler speaks in the realm's own words; on every other beat the ruler speaks for the group.</summary>
    internal static readonly HashSet<string> RealmTexts = new(StringComparer.Ordinal)
    {
        LeaveAsk, LeaveGranted, VassalAccept, MercenaryAccept,
    };
}
