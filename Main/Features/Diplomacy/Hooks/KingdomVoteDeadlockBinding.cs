using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;

namespace TAOM.Features.Diplomacy.Hooks;

/// <summary>
/// Shared state and cached reflection for the three <c>Patch80_KingdomVoteDeadlock</c> seams.
///
/// All reflection is resolved once in <see cref="Initialize"/> and never inside a patch body
/// (harmony-patches.md: no reflection in hot paths — <c>HandleDecision</c> is reachable from
/// <c>KingdomDecisionsVM.OnFrameTick</c>, which runs every frame the kingdom screen is open).
///
/// The queue repair lives here rather than in either patch because two seams gating one decision
/// with independently written guards is how they end up contradicting each other
/// (lessons/harmony-il.md, "Two seams that gate the same decision must carry the SAME guards").
/// </summary>
internal static class KingdomVoteDeadlockBinding
{
    private static IKingdomVoteDeadlockService _service;
    private static IModLogger _logger;

    private static FieldInfo _examinedDecisionsField;
    private static MethodInfo _shouldCheckForDecisionSetter;
    private static FieldInfo _itemDecisionField;
    private static FieldInfo _onDecisionOverField;

    /// <summary>
    /// False when any member failed to resolve. Every patch body checks it and defers to vanilla,
    /// so an engine rename degrades to "the guard is not installed" rather than a throw.
    /// </summary>
    internal static bool IsReady { get; private set; }

    internal static IKingdomVoteDeadlockService Service => _service;

    internal static void Initialize(IKingdomVoteDeadlockService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;

        _examinedDecisionsField =
            AccessTools.Field(typeof(KingdomDecisionsVM), "_examinedDecisionsSinceInit");
        _shouldCheckForDecisionSetter =
            AccessTools.PropertySetter(typeof(KingdomDecisionsVM), "_shouldCheckForDecision");
        _itemDecisionField = AccessTools.Field(typeof(DecisionItemBaseVM), "_decision");
        _onDecisionOverField = AccessTools.Field(typeof(DecisionItemBaseVM), "_onDecisionOver");

        IsReady = _examinedDecisionsField != null
                  && _shouldCheckForDecisionSetter != null
                  && _itemDecisionField != null
                  && _onDecisionOverField != null;

        if (!IsReady)
        {
            _logger?.LogWarning(
                "[KingdomVote] Patch80 disabled: a kingdom-decision view model member did not resolve " +
                $"(examined={_examinedDecisionsField != null}, shouldCheck={_shouldCheckForDecisionSetter != null}, " +
                $"decision={_itemDecisionField != null}, onDecisionOver={_onDecisionOverField != null}).");
        }
    }

    /// <summary>
    /// Takes a withdrawn ballot out of the queue's way: records it as examined so
    /// <c>OnFrameTick</c> stops offering it, then re-arms the check vanilla left switched off.
    ///
    /// Vanilla's own bail-out (<c>HandleDecision</c>'s else branch) sets
    /// <c>_shouldCheckForDecision = false</c> without recording the ballot, so every later decision
    /// in that session is silently never offered. Re-arming without recording would loop instead,
    /// which is why both halves happen together.
    /// </summary>
    internal static void WithdrawBallotFromQueue(KingdomDecisionsVM vm, KingdomDecision decision)
    {
        if (!IsReady || vm == null) return;

        try
        {
            if (decision != null &&
                _examinedDecisionsField.GetValue(vm) is List<KingdomDecision> examined &&
                !examined.Contains(decision))
            {
                examined.Add(decision);
            }

            _shouldCheckForDecisionSetter.Invoke(vm, new object[] { true });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[KingdomVote] Could not withdraw a ballot from the queue: {ex.Message}");
        }
    }

    /// <summary>The engine decision an open decision window was built for, or null.</summary>
    internal static KingdomDecision GetDecisionOf(DecisionItemBaseVM item)
    {
        if (!IsReady || item == null) return null;

        try
        {
            return _itemDecisionField.GetValue(item) as KingdomDecision;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[KingdomVote] Could not read a decision window's ballot: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Vanilla's own close callback (<c>KingdomDecisionsVM.OnDecisionOver</c>), which finalizes the
    /// item view model, clears <c>CurrentDecision</c> and re-arms the queue.
    ///
    /// Invoked directly instead of calling <c>ExecuteDone</c>, because <c>ExecuteDone</c> opens with
    /// <c>KingdomDecisionMaker.GetChosenOutcomeText()</c>, which dereferences the election's
    /// <c>_chosenOutcome</c> — null on a cancelled election. Calling it would trade the hang for an NRE.
    /// </summary>
    internal static Action GetOnDecisionOver(DecisionItemBaseVM item)
    {
        if (!IsReady || item == null) return null;

        try
        {
            return _onDecisionOverField.GetValue(item) as Action;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[KingdomVote] Could not read a decision window's close callback: {ex.Message}");
            return null;
        }
    }
}
