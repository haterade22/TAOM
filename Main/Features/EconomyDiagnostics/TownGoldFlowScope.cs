using System;

namespace TAOM.Features.EconomyDiagnostics;

/// <summary>
/// The ambient "who is moving gold right now" tag, set by thin prefixes on the known callers and
/// read by the single recorder on <c>SettlementComponent.ChangeGold</c>.
///
/// Why ambient rather than a parameter: <c>ChangeGold</c> is several frames below every caller, and
/// the intermediate hops (<c>GiveGoldAction</c>, <c>SellItemsAction</c>) are shared by flows we want
/// to tell apart. Threading a tag through would mean transpiling engine methods; an ambient set at
/// the entry point costs one field write.
///
/// **Outermost wins.** <see cref="TryEnter"/> only claims the tag when nothing else holds it, so a
/// specific outer caller (a caravan sale) keeps its label even though it passes through a generic
/// inner action (<c>SellItemsAction</c>) that would otherwise overwrite it with a vaguer one.
///
/// <c>[ThreadStatic]</c> is belt-and-braces. Every campaign ticker reaching this path is registered
/// <c>doParallel: false</c> (verified in <c>CampaignPeriodicEventManager.InitializeTickers</c>,
/// v1.4.7), so in practice this is all main-thread — but a per-thread tag cannot leak across threads
/// if that ever changes, whereas a plain static could silently mislabel every flow.
/// </summary>
public static class TownGoldFlowScope
{
    [ThreadStatic]
    private static TownGoldFlow _current;

    public static TownGoldFlow Current => _current;

    /// <summary>
    /// Claims the tag if it is free. Returns whether this caller took ownership — pass that straight
    /// back to <see cref="Exit"/> so a nested caller never clears a tag it did not set.
    /// </summary>
    public static bool TryEnter(TownGoldFlow flow)
    {
        if (_current != TownGoldFlow.Other) return false;

        _current = flow;
        return true;
    }

    public static void Exit(bool owned)
    {
        if (owned) _current = TownGoldFlow.Other;
    }

    /// <summary>
    /// Belt-and-braces reset. A Harmony postfix does not run when the patched method throws, so an
    /// exception escaping a tagged engine call would otherwise leave the tag stuck and mislabel
    /// every later flow on that thread. The recorder calls this at the start of each day roll.
    /// </summary>
    public static void Reset() => _current = TownGoldFlow.Other;
}
