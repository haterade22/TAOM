using System;
using System.Collections.Generic;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// How much of a reinforcement batch the engine's Balanced spawn method releases (its own two percentages), and the trickle
/// interval the swapped-in settings must not undercut: with a custom timer the engine's rout check reads the settings' global
/// interval plus 3 s as its grace for a side whose active agents all run away.
/// </summary>
public readonly struct StagedSpawnPlan
{
    public StagedSpawnPlan(float batchPercentage, float desiredPercentage, float trickleSeconds)
    {
        BatchPercentage = batchPercentage;
        DesiredPercentage = desiredPercentage;
        TrickleSeconds = trickleSeconds;
    }

    public float BatchPercentage { get; }
    public float DesiredPercentage { get; }
    public float TrickleSeconds { get; }
}

/// <summary>
/// The share of each side's reserve that may be released by the end of each stage (A, B, C, D), per side and per which side
/// the player is on. Unspent shares carry forward, and the defenders keep a tail back for the lord's hall when the player attacks.
/// </summary>
public sealed class ReserveShares
{
    public ReserveShares(float[] attacker, float[] defenderPlayerAttacks, float[] defenderPlayerDefends)
    {
        Attacker = attacker;
        DefenderPlayerAttacks = defenderPlayerAttacks;
        DefenderPlayerDefends = defenderPlayerDefends;
    }

    public static ReserveShares Default => new(
        new[] { 0.20f, 0.30f, 0.25f, 0.25f },
        new[] { 0.15f, 0.25f, 0.20f, 0.20f },
        new[] { 0.15f, 0.30f, 0.25f, 0.30f });

    public float[] Attacker { get; }
    public float[] DefenderPlayerAttacks { get; }
    public float[] DefenderPlayerDefends { get; }

    /// <summary>The four shares for a side, given which side the player is on.</summary>
    public IReadOnlyList<float> For(StagedSide side, StagedSide playerSide)
    {
        if (side == StagedSide.Attacker)
            return Attacker;
        return playerSide == StagedSide.Attacker ? DefenderPlayerAttacks : DefenderPlayerDefends;
    }
}

/// <summary>One numeric tunable of <c>staged_siege.json</c>: its key, default and accepted range.</summary>
internal sealed class TunableSpec
{
    public TunableSpec(string key, double @default, double min, double max, bool isInteger)
    {
        Key = key;
        Default = @default;
        Min = min;
        Max = max;
        IsInteger = isInteger;
    }

    public string Key { get; }
    public double Default { get; }
    public double Min { get; }
    public double Max { get; }

    /// <summary>An integer tunable must be a JSON integer, never a fraction.</summary>
    public bool IsInteger { get; }
}

/// <summary>
/// The numbers that shape a staged siege, read once from <c>siege/staged_siege.json</c> (docs/features/staged-siege.md). The
/// catalog validates each against its <see cref="TunableSpec"/> range and falls back to the default for any that fails.
/// </summary>
public sealed class StagedSiegeTunables
{
    private const int CaptureRadiusAt = 0;
    private const int HeightBandAt = 1;
    private const int MinAttackersAt = 2;
    private const int DominanceRatioAt = 3;
    private const int CaptureSecondsAt = 4;
    private const int DecaySecondsAt = 5;
    private const int ReconcileSecondsAt = 6;
    private const int DetachmentShareAt = 7;
    private const int DetachmentCapAt = 8;
    private const int TrickleSecondsAt = 9;
    private const int RefillBurstSecondsAt = 10;
    private const int RefillBurstIntervalAt = 11;
    private const int BatchPercentageAt = 12;
    private const int DesiredPercentageAt = 13;

    /// <summary>The tunables in index order; the catalog walks this list.</summary>
    internal static readonly TunableSpec[] Specs =
    {
        new("captureRadius", 8, 2, 30, false),
        new("heightBand", 3, 0.5, 10, false),
        new("minAttackers", 3, 1, 50, true),
        new("dominanceRatio", 3, 1, 10, false),
        new("captureSeconds", 20, 2, 300, false),
        new("decaySeconds", 30, 2, 600, false),
        new("reconcileSeconds", 0.5, 0.1, 2, false),
        new("detachmentShare", 0.25, 0, 0.5, false),
        new("detachmentCap", 60, 0, 200, true),
        new("trickleSeconds", 10, 1, 120, false),
        new("refillBurstSeconds", 20, 0, 120, false),
        new("refillBurstIntervalSeconds", 1, 0.25, 10, false),
        new("reinforcementBatchPercentage", 0.05, 0.01, 0.5, false),
        new("desiredReinforcementPercentage", 0.166, 0.01, 1, false),
    };

    /// <summary>The range a scene point's own radius shares with the <c>captureRadius</c> tunable.</summary>
    internal static double RadiusMin => Specs[CaptureRadiusAt].Min;

    internal static double RadiusMax => Specs[CaptureRadiusAt].Max;

    private readonly double[] _values;

    internal StagedSiegeTunables(double[] values, ReserveShares shares)
    {
        if (values == null || values.Length != Specs.Length)
            throw new ArgumentException("one value per tunable is required", nameof(values));
        _values = (double[])values.Clone();
        ReserveShares = shares;
    }

    public static StagedSiegeTunables Default { get; } = Build();

    /// <summary>A fresh copy of the default values, in <see cref="Specs"/> order.</summary>
    internal static double[] DefaultValues()
    {
        var values = new double[Specs.Length];
        for (var i = 0; i < values.Length; i++)
            values[i] = Specs[i].Default;
        return values;
    }

    private static StagedSiegeTunables Build() => new(DefaultValues(), ReserveShares.Default);

    public float CaptureRadius => (float)_values[CaptureRadiusAt];
    public float HeightBand => (float)_values[HeightBandAt];
    public int MinAttackers => (int)_values[MinAttackersAt];
    public float DominanceRatio => (float)_values[DominanceRatioAt];
    public float CaptureSeconds => (float)_values[CaptureSecondsAt];
    public float DecaySeconds => (float)_values[DecaySecondsAt];
    public float ReconcileSeconds => (float)_values[ReconcileSecondsAt];
    public float DetachmentShare => (float)_values[DetachmentShareAt];
    public int DetachmentCap => (int)_values[DetachmentCapAt];
    public float TrickleSeconds => (float)_values[TrickleSecondsAt];
    public float RefillBurstSeconds => (float)_values[RefillBurstSecondsAt];
    public float RefillBurstIntervalSeconds => (float)_values[RefillBurstIntervalAt];
    public float ReinforcementBatchPercentage => (float)_values[BatchPercentageAt];
    public float DesiredReinforcementPercentage => (float)_values[DesiredPercentageAt];

    public ReserveShares ReserveShares { get; }

    /// <summary>The two Balanced-method percentages handed to the engine's spawn settings, and the trickle interval.</summary>
    public StagedSpawnPlan SpawnPlan => new(ReinforcementBatchPercentage, DesiredReinforcementPercentage, TrickleSeconds);
}
