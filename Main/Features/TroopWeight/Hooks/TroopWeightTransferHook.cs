using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

/// <summary>
/// Party screen transfers in the weighted frame (#1430, from Silas He's v1.5.6 fix package).
///
/// <see cref="OnInitialize"/> records, per side, whether the screen's limit is its owner's own limit. The header
/// (<see cref="TroopWeightDisplayHook"/>) and Transfer All read that record, because the live limit moves with the
/// first heavy troop moved.
///
/// <see cref="OnTransferAllBudget"/> replaces Transfer All's headcount budget for members moved into a side that pays
/// the size penalty and shows its own limit: the free weighted slots (the true base, less the target roster's weighted
/// count) filled row by row in vanilla's order (<see cref="TroopTransferPlanning.WeightedFillBudget"/>). Every other case,
/// and any failure, keeps vanilla's budget.
/// </summary>
public class TroopWeightTransferHook : IOnPartyScreenLogicTransfer
{
    private readonly ITroopWeightService _troopWeight;
    private readonly IModLogger _logger;
    // Per process: one warning is enough to show the weighted budget failed; the next press falls back again.
    private bool _warned;

    public TroopWeightTransferHook(ITroopWeightService troopWeight, IModLogger logger)
    {
        _troopWeight = troopWeight;
        _logger = logger;
    }

    public void OnInitialize(PartyScreenLogic logic)
    {
        try
        {
            if (logic == null)
                return;
            PartyScreenOwnLimits.Record(logic,
                leftIsOwn: IsOwn(logic.LeftOwnerParty, logic.LeftPartyMembersSizeLimit),
                rightIsOwn: IsOwn(logic.RightOwnerParty, logic.RightPartyMembersSizeLimit));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Party screen own-limit record failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsOwn(PartyBase? party, int screenLimit)
        => party?.MemberRoster != null && screenLimit == party.PartySizeLimit;

    public int OnTransferAllBudget(int vanillaBudget, PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
    {
        try
        {
            // The campaign's own troop-weight choice first, then the MCM default (TroopWeightSettingsProvider, #1441).
            if (!_troopWeight.IsEnabled || logic == null || command == null || command.Type != PartyScreenLogic.TroopType.Member)
                return vanillaBudget;

            var from = command.RosterSide;
            if (from != PartyScreenLogic.PartyRosterSide.Left && from != PartyScreenLogic.PartyRosterSide.Right)
                return vanillaBudget;

            bool intoRight = from == PartyScreenLogic.PartyRosterSide.Left;
            var owner = intoRight ? logic.RightOwnerParty : logic.LeftOwnerParty;
            if (owner?.MemberRoster == null || !TroopWeightPenaltyScope.PaysSizePenalty(owner)
                || PartyScreenOwnLimits.IsOwnLimit(logic, intoRight) != true)
                return vanillaBudget;

            var target = logic.MemberRosters[intoRight ? 1 : 0];
            var source = logic.MemberRosters[(int)from];
            if (target == null || source == null)
                return vanillaBudget;

            float used = _troopWeight.CalculateWeightedRosterCount(target);
            if (float.IsNaN(used) || (used <= 0f && target.TotalManCount > 0))
                return vanillaBudget; // the roster walk failed

            int screenLimit = intoRight ? logic.RightPartyMembersSizeLimit : logic.LeftPartyMembersSizeLimit;
            int cap = TroopWeightDisplay.DisplayLimit(screenLimit, _troopWeight.GetTrueBaseSizeLimit(owner));

            // Vanilla's own row filter (v1.5.7 :1019 reads the locks, :1027 filters): troop locks apply only to rows
            // moved out of the right side.
            var locks = from == PartyScreenLogic.PartyRosterSide.Right
                ? new HashSet<string>(Campaign.Current?.GetCampaignBehavior<IViewDataTracker>()?.GetPartyTroopLocks() ?? Enumerable.Empty<string>())
                : new HashSet<string>();

            var rows = new List<TroopTransferRow>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var element = source.GetElementCopyAtIndex(i);
                var character = element.Character;
                bool eligible = character != null
                                && !locks.Contains(character.StringId)
                                && logic.IsTroopTransferable(command.Type, character, (int)from);
                rows.Add(new TroopTransferRow(element.Number, character == null ? 1f : _troopWeight.GetTroopWeight(character), eligible));
            }

            return TroopTransferPlanning.WeightedFillBudget(rows, cap - used);
        }
        catch (Exception ex)
        {
            if (!_warned)
            {
                _logger.LogWarning($"[TroopWeight] Transfer All weighted budget failed, vanilla's used: {ex.GetType().Name}: {ex.Message}");
                _warned = true;
            }
            return vanillaBudget;
        }
    }
}
