using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Features.Enlistment.Content;

public class DischargeConsequenceService : IDischargeConsequenceService
{
    /// <summary>Toast variable + key for the reclaim report. One key, one English string.</summary>
    internal const string KitReclaimedKey = "taom_enlist_kit_reclaimed";
    internal const string KitReclaimedFallback = "The quartermaster takes back your service kit: {COUNT} item(s).";

    private readonly IEnlistmentContentStore _contentStore;
    private readonly IServiceRewardService _rewards;
    private readonly IEquipmentIssueLedger _ledger;
    private readonly IPartyItemRosterAdapter _partyItems;
    private readonly IEnlistmentFeatureSettingsProvider _feature;
    private readonly IInquiryAdapter _inquiry;
    private readonly IModLogger _logger;

    public DischargeConsequenceService(
        IEnlistmentContentStore contentStore,
        IServiceRewardService rewards,
        IEquipmentIssueLedger ledger,
        IPartyItemRosterAdapter partyItems,
        IEnlistmentFeatureSettingsProvider feature,
        IInquiryAdapter inquiry,
        IModLogger logger)
    {
        _contentStore = contentStore;
        _rewards = rewards;
        _ledger = ledger;
        _partyItems = partyItems;
        _feature = feature;
        _inquiry = inquiry;
        _logger = logger;
    }

    public void ApplyConsequences(DischargeReason reason)
    {
        var record = _contentStore.Record;

        switch (reason)
        {
            case DischargeReason.Desertion:
                if (record.DeferredWages > 0)
                    _logger?.LogInfo($"[Enlistment] desertion — {record.DeferredWages} in deferred wages forfeited");
                record.DeferredWages = 0;
                // The kit walks out with the deserter, deliberately: it is the one thing they
                // gain by breaking the oath, against forfeited pay and a commander who
                // remembers. Nobody is left to collect it either.
                break;

            case DischargeReason.PlayerRequest:
            case DischargeReason.Commission:
            case DischargeReason.ContractNotRenewed:
            case DischargeReason.CommanderDead:
            case DischargeReason.CommanderUnavailableGraceExpired:
                // Honorable exit: the paymaster settles what the column still owes.
                if (record.DeferredWages > 0)
                {
                    _rewards.Grant(new RewardSpec { Gold = record.DeferredWages }, "final-settlement");
                    record.DeferredWages = 0;
                }
                ReclaimIssuedKit(reason);
                break;

            // HeirSuccessionOrPossessionMismatch / SaveNormalization are quiet, penalty-free
            // bookkeeping exits — no settlement, no forfeit, and no reclaim: neither is an act
            // of the player's, and both can fire on a load path.
        }

        _contentStore.Clear();
    }

    /// <summary>
    /// Hand the service kit back at an honourable exit — the reclaim
    /// <see cref="IPartyItemRosterAdapter"/> has promised in its own doc since it was written.
    /// Issued gear goes into the player's INVENTORY and is never equipped, so without this the
    /// quartermaster is a free-gold loop: draw a kit at each rank, sell it, ask for release.
    ///
    /// <para><b>The ledger is not the authority on what is taken — the return value is.</b>
    /// <see cref="IPartyItemRosterAdapter.RemoveItem"/> drains only the UNMODIFIED stack and
    /// clamps to what is held, so anything the player improved into a modified variant, equipped
    /// onto a character, sold or lost is out of reach BY DESIGN and stays theirs. Report the sum
    /// the adapter actually returned; reporting the ledger count would claim takings that never
    /// happened.</para>
    ///
    /// <para><b>Confiscation only, no gold payoff.</b> Paying the kit out in coin needs a
    /// per-item value surface that does not exist — <c>IItemPoolAdapter</c>'s item snapshot
    /// exposes no Value — so <c>EquipmentPayoffCalculator</c> stays unused until one does.
    /// Deferred deliberately, not forgotten.</para>
    /// </summary>
    private void ReclaimIssuedKit(DischargeReason reason)
    {
        // The shipped MCM hint for TaomSettings.EnableEnlistment promises that switching the
        // feature OFF releases a serving player "honourably (you keep your pay and your gear)".
        // EnlistmentReconciler runs that release as PlayerRequest, which is indistinguishable
        // from an ordinary one here — so the toggle is what has to be read, not the reason.
        // Confiscating on a settings change would make shipped player-facing text a lie.
        if (_feature?.IsEnabled == false)
        {
            _logger?.LogInfo($"[Enlistment] discharge({reason}) with the feature switched off — the kit stays with the player");
            return;
        }

        var issued = _ledger?.IssuedItemIds;
        if (issued == null || issued.Count == 0)
            return;

        if (!_partyItems.IsMainPartyAvailable())
        {
            _logger?.LogWarning($"[Enlistment] discharge({reason}): main party unavailable — {issued.Count} issued item(s) left with the player");
            return;
        }

        var reclaimed = 0;
        foreach (var itemId in issued)
        {
            if (string.IsNullOrEmpty(itemId))
                continue;
            // One call per ledger ENTRY: the ledger stores one entry per physical instance,
            // and RemoveItem clamps each call to what is actually held.
            reclaimed += _partyItems.RemoveItem(itemId, 1);
        }

        _logger?.LogInfo($"[Enlistment] discharge({reason}): quartermaster reclaimed {reclaimed} of {issued.Count} issued item(s)");

        // Silent when nothing came back: saying "0 returned" tells a player who kept their kit
        // that the game tried to take it. The number that matters is in the log either way.
        if (reclaimed > 0)
            _inquiry?.ShowMessage(KitReclaimedKey, KitReclaimedFallback, "COUNT", reclaimed.ToString());
    }
}
