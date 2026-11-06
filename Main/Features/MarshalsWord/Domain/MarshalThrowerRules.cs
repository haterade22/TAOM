using System;

namespace TAOM.Features.MarshalsWord.Domain;

internal readonly record struct GroundPoint(float X, float Y);

/// <summary>The stand point published for one company.</summary>
internal readonly record struct ThrowerPoint(int Company, float X, float Y);

/// <summary>The three facts about a company that decide whether a run goes on: it has soldiers, its order is Advance, and the AI does not command it.</summary>
internal readonly record struct ThrowerCompany(bool HasUnits, bool OnAdvance, bool AiControlled);

/// <summary>Soldiers in a company, those armed with throwing weapons, and the throws they hold between them.</summary>
internal readonly record struct ThrowerCensus(int Units, int Armed, int Ammo);

/// <summary>Everything the stand rules read about one company on one pass. A null range has not been read yet.</summary>
internal readonly record struct ThrowerGates(
    bool SettingOn, bool FieldBattle, bool Deployment, bool Advance, bool AiControlled, bool Held, bool OnFireMode, bool HoldFire,
    bool OpenArrangement, bool RangedBranch, bool HasTarget, float? Range, float OwnDepth, GroundPoint Own, GroundPoint Target, float TargetDepth);

internal enum ApproachKind
{
    NoPoint,
    StandHere,
    Walk,
}

internal readonly record struct ApproachAnswer(ApproachKind Kind, GroundPoint Point, float DirX, float DirY);

internal enum ThrowerVerdictKind
{
    Pass,
    Fail,
    Skip,
}

internal readonly record struct ThrowerVerdict(ThrowerVerdictKind Kind, string Reason);

internal readonly record struct ThrowerCostVerdict(ThrowerVerdictKind Kind, string Reason, double MeanMicros, double MaxMicros);

/// <summary>The pure rules of build-spec 9.4: who may stand on an Advance, where, and how each check reads.</summary>
internal static class MarshalThrowerRules
{
    internal const string TooFew = "too-few";

    /// <summary>The enemy's front is already within <see cref="MarshalTuning.CloseMetres"/>, or its gap is not a number: a run begun there would release <c>enemy-close</c> on its first pass, having stood nowhere.</summary>
    internal const string Engaged = "engaged";

    private const float NoDirection = 1e-4f;

    /// <summary>An empty string when the company may stand, otherwise the first reason it may not.</summary>
    internal static string WhyNot(ThrowerGates gates, ThrowerCensus census)
    {
        var why = GateWhy(gates);
        if (why.Length > 0)
            return why;
        if (TooFewArmed(census))
            return TooFew;
        return gates.Range is { } range && ShortRange(range, gates.OwnDepth) ? "short-range" : "";
    }

    /// <summary>The first reason the gates alone refuse a stand, or an empty string; it reads no census, so a driver can test it before walking the company's soldiers.</summary>
    internal static string GateWhy(ThrowerGates gates)
    {
        if (!gates.SettingOn)
            return "setting-off";
        if (!gates.FieldBattle)
            return "not-field";
        if (gates.Deployment)
            return "deployment";
        if (!gates.Advance)
            return "not-advance";
        if (gates.AiControlled)
            return "ai-control";
        if (gates.Held)
            return "held";
        if (gates.OnFireMode)
            return "fire-mode";
        if (gates.HoldFire)
            return "hold-fire";
        if (!gates.OpenArrangement)
            return "arrangement";
        if (gates.RangedBranch)
            return "ranged";
        if (!gates.HasTarget)
            return "no-target";
        if (!(Gap(gates.Own, gates.Target, gates.TargetDepth) > MarshalTuning.CloseMetres))
            return Engaged;
        return "";
    }

    /// <summary>The front to front gap: the company's position is already its front (Formation.CurrentPosition), and the target's is its median soldier, the middle of its depth.</summary>
    internal static float Gap(GroundPoint own, GroundPoint target, float targetDepth)
    {
        var dx = target.X - own.X;
        var dy = target.Y - own.Y;
        var distance = (float)Math.Sqrt(dx * dx + dy * dy);
        return Math.Max(0f, distance - targetDepth / 2f);
    }

    internal static float StandGap(float range) => Math.Min(MarshalTuning.StandMaxGap, Math.Max(MarshalTuning.StandMinGap, MarshalTuning.StandShare * range));

    /// <summary>True when the range cannot be used, or the rear rank would not reach the enemy's front from the stand gap.</summary>
    internal static bool ShortRange(float range, float ownDepth)
    {
        if (!(range > 0f) || float.IsInfinity(range))
            return true;
        return !(MarshalTuning.ReachShare * range >= StandGap(range) + ownDepth);
    }

    internal static float StandDistance(float standGap, float targetDepth) => standGap + targetDepth / 2f;

    /// <summary>The point short of the target to stand at, or "stand here" when that point is not ahead of the company.</summary>
    internal static ApproachAnswer Approach(GroundPoint own, GroundPoint target, float standDistance)
    {
        var dx = target.X - own.X;
        var dy = target.Y - own.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (!(length > NoDirection) || float.IsInfinity(length) || float.IsNaN(standDistance) || float.IsInfinity(standDistance))
            return new ApproachAnswer(ApproachKind.NoPoint, default, 0f, 0f);

        var dirX = dx / length;
        var dirY = dy / length;
        var point = new GroundPoint(target.X - dirX * standDistance, target.Y - dirY * standDistance);
        var ahead = (point.X - own.X) * dirX + (point.Y - own.Y) * dirY;
        return ahead > 0f
            ? new ApproachAnswer(ApproachKind.Walk, point, dirX, dirY)
            : new ApproachAnswer(ApproachKind.StandHere, own, dirX, dirY);
    }

    /// <summary>The stand check, judged when a run ends.</summary>
    internal static ThrowerVerdict JudgeStand(bool everStood, bool closedBeforeStand, float maxOvershoot, bool faulted)
    {
        if (faulted)
            return new ThrowerVerdict(ThrowerVerdictKind.Fail, "fault");
        if (closedBeforeStand)
            return new ThrowerVerdict(ThrowerVerdictKind.Fail, "closed-before-stand");
        if (!(maxOvershoot <= MarshalTuning.OvershootMetres))
            return new ThrowerVerdict(ThrowerVerdictKind.Fail, "walked-in");
        return everStood ? new ThrowerVerdict(ThrowerVerdictKind.Pass, "") : new ThrowerVerdict(ThrowerVerdictKind.Skip, "released-before-stand");
    }

    /// <summary>The throw check, judged at release.</summary>
    internal static ThrowerVerdict JudgeThrow(bool everStood, double stoodSeconds, int shots)
    {
        if (!everStood)
            return new ThrowerVerdict(ThrowerVerdictKind.Skip, "never-stood");
        if (shots >= MarshalTuning.MinThrows)
            return new ThrowerVerdict(ThrowerVerdictKind.Pass, "");
        return stoodSeconds >= MarshalTuning.ThrowWindowSeconds
            ? new ThrowerVerdict(ThrowerVerdictKind.Fail, "few-throws")
            : new ThrowerVerdict(ThrowerVerdictKind.Skip, "short-stand");
    }

    /// <summary>The melee check after a release: null while it is still open.</summary>
    internal static ThrowerVerdict? JudgeMelee(bool orderStillAdvance, bool emptied, bool aiControlled, bool blowsRose, float advanced, double sinceRelease)
    {
        if (!orderStillAdvance)
            return new ThrowerVerdict(ThrowerVerdictKind.Skip, "order-changed");
        if (emptied)
            return new ThrowerVerdict(ThrowerVerdictKind.Skip, "emptied");
        if (aiControlled)
            return new ThrowerVerdict(ThrowerVerdictKind.Skip, "ai-control");
        if (blowsRose && advanced >= MarshalTuning.MeleeAdvanceMetres)
            return new ThrowerVerdict(ThrowerVerdictKind.Pass, "");
        if (sinceRelease < MarshalTuning.MeleeWaitSeconds)
            return null;
        return new ThrowerVerdict(ThrowerVerdictKind.Fail, blowsRose ? "no-advance" : "no-melee");
    }

    /// <summary>The cost check over one epoch's published calls: ticks are the clock's, so the frequency turns them into microseconds.</summary>
    internal static ThrowerCostVerdict CostVerdict(long calls, long totalTicks, long maxTicks, long frequency)
    {
        if (frequency <= 0)
            return new ThrowerCostVerdict(ThrowerVerdictKind.Skip, "no-clock", 0.0, 0.0);
        var mean = calls > 0 ? totalTicks * 1_000_000.0 / (calls * (double)frequency) : 0.0;
        var most = maxTicks * 1_000_000.0 / frequency;
        if (calls < MarshalTuning.CostMinCalls)
            return new ThrowerCostVerdict(ThrowerVerdictKind.Skip, "too-few-calls", mean, most);
        if (mean > MarshalTuning.CostMeanMicros)
            return new ThrowerCostVerdict(ThrowerVerdictKind.Fail, "mean", mean, most);
        return most > MarshalTuning.CostMaxMicros
            ? new ThrowerCostVerdict(ThrowerVerdictKind.Fail, "max", mean, most)
            : new ThrowerCostVerdict(ThrowerVerdictKind.Pass, "", mean, most);
    }

    private static bool TooFewArmed(ThrowerCensus census) =>
        census.Armed < MarshalTuning.ThrowerMinArmed || census.Armed < MarshalTuning.ThrowerQualifyShare * census.Units;
}
