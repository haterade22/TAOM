using System;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>What a held soldier's attack flag should be: left alone, cleared, or given back for a while after a wound.</summary>
internal enum HoldWant
{
    Off,
    FlagOff,
    StrikeBack,
}

internal enum FiringAct
{
    None,
    Hold,
    Restore,
    Forget,
}

/// <summary>
/// The facts that decide whether one soldier of a held company stands in the line (build-spec 9.2), read by the component at most three times a second. A soldier
/// the engine says runs away, or walks to a game object such as a ladder, is out of the line, so the clamp never stops a rout or that walk.
/// </summary>
internal readonly record struct HoldFacts(bool ArrangementHolds, bool OrderAllowsHold, bool CompanyUnderPlayer, bool HasShield, bool Mounted,
    bool RangedWielded, bool Detached, bool UsingObject, bool IsMainAgent, bool RunningAway, bool GoingToObject);

/// <summary>
/// Whether a soldier the clamp steered ended a hold read closer to his slot, farther from it, or within the dead band of where the first replaced tick left him.
/// None means the read does not count: no steered tick, a distance that is not a number, or a soldier in his strike back window.
/// </summary>
internal enum SlotTrend
{
    None,
    Steady,
    Closer,
    Farther,
}

/// <summary>The place a held soldier is steered toward, and whether it is an anchor the soldier's own position set.</summary>
internal readonly record struct SlotRead(bool Valid, float X, float Y, bool Anchored);

/// <summary>The pure rules of hold the line beyond the attack flag itself: who qualifies, the strike back, the firing order, the slot and the steering clamp (build-spec 9.2).</summary>
internal static class MarshalHoldLineRules
{
    internal static bool Qualifies(in HoldFacts facts) => WhyNot(facts).Length == 0;

    /// <summary>The first clause that fails, in the order the diagnostics read, or an empty string when the soldier qualifies.</summary>
    internal static string WhyNot(in HoldFacts facts)
    {
        if (facts.IsMainAgent)
            return "main-agent";
        if (!facts.CompanyUnderPlayer)
            return "company-ai";
        if (!facts.ArrangementHolds)
            return "arrangement";
        if (!facts.OrderAllowsHold)
            return "order";
        if (!facts.HasShield)
            return "no-shield";
        if (facts.Mounted)
            return "mounted";
        if (facts.RangedWielded)
            return "ranged";
        if (facts.Detached)
            return "detached";
        if (facts.UsingObject)
            return "using-object";
        if (facts.RunningAway)
            return "running-away";
        return facts.GoingToObject ? "going-to-object" : "";
    }

    /// <summary>A hit counts as a wound when it did damage and was not a missile; a parried or blocked blow reaches the callback with none.</summary>
    internal static bool IsWound(int damage, bool isMissile) => damage > 0 && !isMissile;

    /// <summary>A blow by a soldier of the held line inside its strike back window is the line answering a wound, and is counted apart from the blows the flag should stop.</summary>
    internal static bool BlowIsStrikeBack(bool inTheLine, double woundedAt, double now) => inTheLine && StrikingBack(woundedAt, now);

    internal static bool StrikingBack(double woundedAt, double now) => now - woundedAt < MarshalTuning.StrikeBackSeconds;

    internal static HoldWant Want(bool holdOn, bool qualifies, double woundedAt, double now)
    {
        if (!holdOn || !qualifies)
            return HoldWant.Off;
        return StrikingBack(woundedAt, now) ? HoldWant.StrikeBack : HoldWant.FlagOff;
    }

    /// <summary>What to do to one soldier's firing order: hold fire while the line stands, and give back the company's own order when it stops.</summary>
    internal static FiringAct FiringAction(bool lineStands, int current, int holdValue, int formationValue, bool owed)
    {
        if (lineStands)
            return current == holdValue ? FiringAct.None : FiringAct.Hold;
        if (!owed)
            return FiringAct.None;
        return current == formationValue ? FiringAct.Forget : FiringAct.Restore;
    }

    internal static SlotRead ReadSlot(bool standGround, bool anchored, float anchorX, float anchorY, bool orderValid, float orderX, float orderY, float ownX, float ownY)
    {
        if (standGround)
        {
            if (anchored)
                return new SlotRead(true, anchorX, anchorY, true);
            return IsReal(ownX) && IsReal(ownY) ? new SlotRead(true, ownX, ownY, true) : default;
        }

        return orderValid && IsReal(orderX) && IsReal(orderY) ? new SlotRead(true, orderX, orderY, false) : default;
    }

    internal static bool Beyond(float distance) => distance > MarshalTuning.SlotSlack;

    /// <summary>The soldier's input is a walk: tested before the frame is read, because the frame is a native read and a zero or non-numeric input is never replaced.</summary>
    internal static bool Moves(float inX, float inY) => (double)inX * inX + (double)inY * inY > 1e-12;

    /// <summary>A read beyond the slack that the clamp did not touch since the previous read: a crowd, a press or an input the clamp leaves alone moved the soldier.</summary>
    internal static bool IsLoose(float distance, bool steered) => IsReal(distance) && Beyond(distance) && !steered;

    /// <summary>A blow counts against a held line when its dealer stands in the line, or when his company is not held at all, so a soldier outside the line never counts against it and a control always counts.</summary>
    internal static bool BlowCountsForLine(bool inTheLine, bool companyHeld) => inTheLine || !companyHeld;

    /// <summary>The order name under which the slot is an anchor, and the only state the slot proof judges.</summary>
    internal const string StopOrder = "Stop";

    /// <summary>The strike back answered the wounds when at least half of the soldiers with a settled wound read the flag on, and a line never struck proves nothing.</summary>
    internal static HoldProof StrikeProof(int settled, int flagOn)
    {
        if (settled < 1)
            return HoldProof.Pending;
        return flagOn * 2 >= settled ? HoldProof.Pass : HoldProof.Fail;
    }

    /// <summary>
    /// The clamp moved no more of the steered soldiers away from their slot than toward it, judged only once at least <see cref="MarshalTuning.SlotMinSteered"/> steered reads
    /// were counted. Soldiers far from the slot are a fact and not a test: a crowd or an input the clamp leaves alone moves them, and the clamp is judged on what it did.
    /// </summary>
    internal static HoldProof TrendProof(int steered, int closer, int farther)
    {
        if (steered < MarshalTuning.SlotMinSteered)
            return HoldProof.Pending;
        return farther > closer ? HoldProof.Fail : HoldProof.Pass;
    }

    /// <summary>The slot proof of a window under Stop: judged only for a line that was struck, with anchors, and by the clamp's trend.</summary>
    internal static HoldProof SlotProof(string order, int qualified, int anchored, int woundedEver, int steered, int closer, int farther)
    {
        if (order != StopOrder || qualified < MarshalTuning.HoldMinFlagOff || anchored * 2 < qualified || woundedEver < 1)
            return HoldProof.Pending;
        return TrendProof(steered, closer, farther);
    }

    /// <summary>Replaces an AI walk away from the slot with a step toward it, in the soldier's local frame, and returns every other input, including one that is not a number, unchanged.</summary>
    internal static (float X, float Y) ClampInput(float inX, float inY, float toSlotX, float toSlotY)
    {
        var distance = (float)Math.Sqrt((double)toSlotX * toSlotX + (double)toSlotY * toSlotY);
        var length = (float)Math.Sqrt((double)inX * inX + (double)inY * inY);
        if (!Beyond(distance) || !Moves(inX, inY))
            return (inX, inY);
        var cosine = (inX * toSlotX + inY * toSlotY) / (length * distance);
        if (!(cosine < -MarshalTuning.SlotAwayDot))
            return (inX, inY);
        var step = Math.Min(1f, distance) / distance;
        return (toSlotX * step, toSlotY * step);
    }

    /// <summary>Which way the distance to the slot went between the first replaced tick and the next hold read, counted only when the clamp steered the soldier in between and both distances are numbers.</summary>
    internal static SlotTrend Trend(float baseline, float current, bool steered, bool strikingBack)
    {
        if (!steered || strikingBack || !IsReal(baseline) || !IsReal(current))
            return SlotTrend.None;
        var change = current - baseline;
        if (change < -MarshalTuning.SlotTrendMetres)
            return SlotTrend.Closer;
        return change > MarshalTuning.SlotTrendMetres ? SlotTrend.Farther : SlotTrend.Steady;
    }

    private static bool IsReal(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
