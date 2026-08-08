using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// The save/load half of Battlefield Promotions, kept out of <c>FieldCommissionBehavior</c> so the
/// behaviour stays a thin event router under the ADR-002 line budget.
///
/// Three collections persist per save: banked merit, promoted-hero ids, and the per-troop decline
/// marks. The pending-offer queue deliberately does NOT — it is transient state that must never
/// cross a session boundary (see <c>IFieldCommissionMeritService.ClearPendingOffers</c>).
/// </summary>
internal static class FieldCommissionSaveData
{
    internal const string MeritsKey = "_taom_fc_merits";
    internal const string PromotedKey = "_taom_fc_promotedHeroes";
    internal const string DeclinedKey = "_taom_fc_declinedAt";

    internal static void Save(IDataStore dataStore, IFieldCommissionMeritService merit)
    {
        var merits = merit.ExportMerits();
        var promoted = merit.ExportPromotedHeroIds();
        var declined = merit.ExportDeclinedMarks();
        dataStore.SyncData(MeritsKey, ref merits);
        dataStore.SyncData(PromotedKey, ref promoted);
        dataStore.SyncData(DeclinedKey, ref declined);
    }

    internal static void Load(IDataStore dataStore, IFieldCommissionMeritService merit)
    {
        Dictionary<string, int> merits = null;
        List<string> promoted = null;
        // DeclinedKey is absent from any save written before decline marks existed. SyncData leaves
        // the ref null in that case and ImportDeclinedMarks reads null as "nothing declined", so an
        // older save loads with every troop type eligible again — save-compatible in both directions.
        Dictionary<string, int> declined = null;

        dataStore.SyncData(MeritsKey, ref merits);
        dataStore.SyncData(PromotedKey, ref promoted);
        dataStore.SyncData(DeclinedKey, ref declined);

        merit.ImportMerits(merits);
        merit.ImportPromotedHeroIds(promoted);
        merit.ImportDeclinedMarks(declined);
    }
}
