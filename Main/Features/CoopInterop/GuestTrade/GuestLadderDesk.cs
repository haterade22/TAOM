using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// The lord's gear ladder at a co-op guest's armoury, on the server: TAOM's own <see cref="LordsLadderService"/>, built
/// for the guest's hero, with the checks the armoury window makes for a local player. <c>HandIn</c> does not check the
/// town and <c>Claim</c> not the armoury level, and a guest's request can name any slot, so both are checked here.
/// </summary>
internal static class GuestLadderDesk
{
    /// <summary>The hero's rung as the server holds it; null once every rung is claimed.</summary>
    public static GuestLadderView? View(LordsLadderService ladder, string heroId, string? cultureId, int armouryLevel, int lordLevel)
    {
        if (ladder.CurrentStep(heroId) is not { } step)
            return null;
        var ready = ladder.IsReady(heroId);
        var handIn = ready ? null : ladder.HandInStatus();
        var sendsHandIn = handIn != null && Fits(handIn.MaterialId);
        var choices = ready ? Cap(ladder.RewardChoices(step, cultureId)) : Array.Empty<string>();
        return new GuestLadderView(step.Slot, ready, ladder.CanClaimAt(armouryLevel), lordLevel,
            sendsHandIn ? handIn!.MaterialId : null, sendsHandIn ? handIn!.Needed : 0, sendsHandIn ? handIn!.Carried : 0, choices);
    }

    /// <summary>Hands in the materials for the rung the guest saw.</summary>
    public static GuestLadderOutcome HandIn(LordsLadderService ladder, string heroId, LadderSlot slot)
    {
        if (ladder.CurrentStep(heroId) is not { } step || step.Slot != slot || ladder.IsReady(heroId)
            || ladder.HandInStatus() is not { } status)
            return GuestLadderOutcome.RungChanged;
        if (!status.IsEnough)
            return GuestLadderOutcome.NotEnough;
        return ladder.HandIn() ? GuestLadderOutcome.Done : GuestLadderOutcome.NotDone;
    }

    /// <summary>Claims a piece of the rung the guest saw, at an armoury of <paramref name="armouryLevel"/>.</summary>
    public static GuestLadderOutcome Claim(LordsLadderService ladder, string heroId, string? cultureId, LadderSlot slot,
        string itemId, int armouryLevel)
    {
        if (ladder.CurrentStep(heroId) is not { } step || step.Slot != slot || !ladder.IsReady(heroId))
            return GuestLadderOutcome.RungChanged;
        if (!ladder.CanClaimAt(armouryLevel))
            return GuestLadderOutcome.ArmouryLevel;
        if (!ladder.RewardChoices(step, cultureId).Contains(itemId, StringComparer.Ordinal))
            return GuestLadderOutcome.NotAChoice;
        return ladder.Claim(itemId) ? GuestLadderOutcome.Done : GuestLadderOutcome.NotDone;
    }

    private static IReadOnlyList<string> Cap(IEnumerable<string> ids) =>
        ids.Where(Fits).Take(GuestLadderView.MaxChoices).ToList();

    private static bool Fits(string? id) => !string.IsNullOrEmpty(id) && id!.Length <= GuestLadderView.MaxIdLength;
}
