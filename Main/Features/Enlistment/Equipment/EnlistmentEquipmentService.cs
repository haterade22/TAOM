using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Resolves the (culture, assignment, rank) roster via EnlistmentRosterResolver's
/// fallback chain, guards every item id through IItemPoolAdapter (skip-and-warn — one dead
/// data ref must not void the whole issue), adds surviving items to the party
/// inventory via IPartyItemRosterAdapter, and records the issue in the ledger.
/// Only Issued advances the ledger; every other outcome leaves it untouched so a
/// later attempt can succeed. Per-case logging mirrors CareerStartingEquipmentService.
/// </summary>
public sealed class EnlistmentEquipmentService : IEnlistmentEquipmentService
{
    private readonly IEquipmentRosterCatalogAdapter _catalog;
    private readonly IPartyItemRosterAdapter _partyItems;
    private readonly IItemPoolAdapter _itemPool;
    private readonly IEquipmentIssueLedger _ledger;
    private readonly IModLogger _logger;

    public EnlistmentEquipmentService(
        IEquipmentRosterCatalogAdapter catalog,
        IPartyItemRosterAdapter partyItems,
        IItemPoolAdapter itemPool,
        IEquipmentIssueLedger ledger,
        IModLogger logger)
    {
        _catalog = catalog;
        _partyItems = partyItems;
        _itemPool = itemPool;
        _ledger = ledger;
        _logger = logger;
    }

    public EquipmentIssueResult IssueForRank(
        string cultureId, ServiceAssignment assignment, EnlistmentRank rank)
    {
        if (_ledger.HasIssuedForRank(rank))
        {
            _logger.LogInfo($"EnlistmentEquipmentService: rank {rank} already covered "
                + $"(highest issued: {_ledger.HighestIssuedRank}) — skipping");
            return EquipmentIssueResult.AlreadyIssuedForRank;
        }

        var rosterId = EnlistmentRosterResolver.Resolve(
            cultureId, assignment, rank, _catalog.RosterExists);
        if (rosterId == null)
        {
            _logger.LogWarning("EnlistmentEquipmentService: no roster for "
                + $"({cultureId}/{assignment}/{rank}) anywhere on the fallback chain — "
                + "nothing issued");
            return EquipmentIssueResult.NoRosterFound;
        }

        var itemIds = _catalog.GetBattleSetItemIds(rosterId);
        var validItems = new List<string>();
        if (itemIds != null)
        {
            foreach (var itemId in itemIds)
            {
                if (string.IsNullOrEmpty(itemId))
                    continue;
                if (_itemPool.ItemExists(itemId))
                    validItems.Add(itemId);
                else
                    _logger.LogWarning($"EnlistmentEquipmentService: item '{itemId}' in roster "
                        + $"'{rosterId}' does not resolve — skipped");
            }
        }

        if (validItems.Count == 0)
        {
            _logger.LogWarning($"EnlistmentEquipmentService: roster '{rosterId}' yielded no "
                + "resolvable items — nothing issued");
            return EquipmentIssueResult.NoValidItems;
        }

        if (!_partyItems.IsMainPartyAvailable())
        {
            _logger.LogWarning($"EnlistmentEquipmentService: main party unavailable — cannot "
                + $"issue roster '{rosterId}' for rank {rank}");
            return EquipmentIssueResult.PartyUnavailable;
        }

        var issued = new List<string>();
        foreach (var itemId in validItems)
        {
            if (_partyItems.AddItem(itemId, 1))
                issued.Add(itemId);
            else
                _logger.LogWarning($"EnlistmentEquipmentService: failed to add '{itemId}' to the "
                    + "party inventory — skipped");
        }

        if (issued.Count == 0)
        {
            _logger.LogError($"EnlistmentEquipmentService: every inventory add failed for roster "
                + $"'{rosterId}' — treating the party as unavailable, ledger untouched");
            return EquipmentIssueResult.PartyUnavailable;
        }

        _ledger.RecordIssue(rank, issued);
        _logger.LogInfo($"EnlistmentEquipmentService: issued {issued.Count} item(s) from roster "
            + $"'{rosterId}' for ({cultureId}/{assignment}/{rank})");
        return EquipmentIssueResult.Issued;
    }
}
