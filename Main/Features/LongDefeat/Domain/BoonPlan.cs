using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>The pool a boon belongs to (phase2-plan.md section 2): a class row shows only for its class, a troop category's rows only while the garrison or shop holds a troop of that group.</summary>
internal enum BoonCategory
{
    Class,
    Infantry,
    Archers,
    Cavalry,
    Fortress,
    WarChest,
    Legend,
}

/// <summary>One number a boon changes, by the key of <see cref="BoonKeys"/>. A key that <see cref="BoonKeys.IsTargeted"/> says whom it changes in <paramref name="Target"/>; the others have none.</summary>
internal readonly record struct BoonEffect(string Key, float Value, BoonTarget? Target = null);

/// <summary>
/// One boon: TAOM's own name and text as <c>{=KEY}English</c>, what it gives and what it costs. A drawback uses the same keys as an effect and
/// shows on its own line (<see cref="DrawbackText"/>, required whenever <see cref="Drawback"/> is not empty, so a cost is never hidden).
/// </summary>
internal sealed record BoonRow(string Id, BoonCategory Category, string Name, string Text, IReadOnlyList<BoonEffect> Effects)
{
    /// <summary>The class a <see cref="BoonCategory.Class"/> row belongs to; empty for every other category.</summary>
    public string ClassId { get; init; } = "";

    public IReadOnlyList<BoonEffect> Drawback { get; init; } = Array.Empty<BoonEffect>();

    public string DrawbackText { get; init; } = "";

    public bool HasDrawback => Drawback.Count > 0;
}

/// <summary>A boon as the HUD and the shop bot see it: no numbers, only what is shown and whether it costs something.</summary>
internal sealed record BoonCard(string Id, string Name, string Text, string DrawbackText, BoonCategory Category)
{
    public bool HasDrawback => DrawbackText.Length > 0;

    public static BoonCard From(BoonRow row) => new(row.Id, row.Name, row.Text, row.HasDrawback ? row.DrawbackText : "", row.Category);
}

/// <summary>The boons of a run in file order, and the weight of a Legend row against 10 for every other row. <see cref="None"/>: nothing to offer, so every pick lapses.</summary>
internal sealed class BoonPlan
{
    public const int RowWeight = 10;

    public static BoonPlan None { get; } = new(Array.Empty<BoonRow>(), 1);

    private readonly Dictionary<string, BoonRow> _byId;

    public BoonPlan(IReadOnlyList<BoonRow> rows, int legendWeight)
    {
        Rows = rows;
        LegendWeight = legendWeight;
        _byId = new Dictionary<string, BoonRow>(StringComparer.Ordinal);
        foreach (var row in rows.Where(r => !_byId.ContainsKey(r.Id)))
            _byId[row.Id] = row;
    }

    public IReadOnlyList<BoonRow> Rows { get; }

    public int LegendWeight { get; }

    public int WeightOf(BoonRow row) => row.Category == BoonCategory.Legend ? LegendWeight : RowWeight;

    public BoonRow? Find(string id) => _byId.TryGetValue(id, out var row) ? row : null;
}

/// <summary>The range of one boon key: <c>Whole</c> keys are JSON integers. The vocabulary is closed, and the catalog refuses a key that is not here (phase2-plan.md section 2).</summary>
internal readonly record struct BoonKeyRule(float Min, float Max, bool Whole);

/// <summary>
/// The keys a boon effect may use: the war chest keys, the fortress keys and <c>affixCount</c> (Slice 4), and the troop and class keys that name a <see cref="BoonTarget"/> (Slice 6).
/// <c>affixCount</c> is negative only, and <c>gold</c> (paid once, at the pick) is an effect, never a drawback.
/// </summary>
internal static class BoonKeys
{
    public const string BountyPercent = "bountyPercent";
    public const string WaveBonusPercent = "waveBonusPercent";
    public const string EarlyGoldPercent = "earlyGoldPercent";
    public const string PricePercent = "pricePercent";
    public const string Gold = "gold";
    public const string GarrisonCap = "garrisonCap";
    public const string IntermissionSeconds = "intermissionSeconds";
    public const string RestShare = "restShare";
    public const string KeepHoldPercent = "keepHoldPercent";
    public const string PackBonus = "packBonus";
    public const string AffixCount = "affixCount";
    public const string SwingSpeedPercent = "swingSpeedPercent";
    public const string MoveSpeedPercent = "moveSpeedPercent";
    public const string DrawSpeedPercent = "drawSpeedPercent";
    public const string ReloadSpeedPercent = "reloadSpeedPercent";
    public const string BlockPercent = "blockPercent";
    public const string ParryPercent = "parryPercent";
    public const string AimErrorPercent = "aimErrorPercent";
    public const string DamagePercent = "damagePercent";
    public const string DamageTakenPercent = "damageTakenPercent";
    public const string HealthPercent = "healthPercent";

    public static IReadOnlyDictionary<string, BoonKeyRule> Rules { get; } = new Dictionary<string, BoonKeyRule>(StringComparer.Ordinal)
    {
        [BountyPercent] = new(-90f, 300f, false),
        [WaveBonusPercent] = new(-90f, 300f, false),
        [EarlyGoldPercent] = new(-90f, 300f, false),
        [PricePercent] = new(-90f, 300f, false),
        [Gold] = new(1f, 100000f, true),
        [GarrisonCap] = new(-100f, 100f, true),
        [IntermissionSeconds] = new(-50f, 300f, false),
        [RestShare] = new(-1f, 1f, false),
        [KeepHoldPercent] = new(-90f, 300f, false),
        [PackBonus] = new(-10f, 50f, true),
        [AffixCount] = new(-8f, -1f, true),
        [SwingSpeedPercent] = new(-90f, 300f, false),
        [MoveSpeedPercent] = new(-90f, 300f, false),
        [DrawSpeedPercent] = new(-90f, 300f, false),
        [ReloadSpeedPercent] = new(-90f, 300f, false),
        [BlockPercent] = new(-90f, 300f, false),
        [ParryPercent] = new(-90f, 300f, false),
        [AimErrorPercent] = new(-90f, 300f, false),
        [DamagePercent] = new(-90f, 300f, false),
        [DamageTakenPercent] = new(-90f, 300f, false),
        [HealthPercent] = new(-90f, 300f, false),
    };

    /// <summary>The keys whose stat percent is written onto a target's <c>RaceAbilityEffects</c> record and needs a refresh of the agents standing.</summary>
    public static IReadOnlyCollection<string> StatKeys { get; } = new[]
    {
        SwingSpeedPercent, MoveSpeedPercent, DrawSpeedPercent, ReloadSpeedPercent, BlockPercent, ParryPercent, AimErrorPercent,
    };

    /// <summary>The keys that change one <see cref="BoonTarget"/> and so need a <c>target</c>: the stat keys, the two damage factors and health.</summary>
    public static bool IsTargeted(string key) => StatKeys.Contains(key) || key == DamagePercent || key == DamageTakenPercent || key == HealthPercent;

    /// <summary>Whether a key may appear in a boon's drawback: a drawback costs, so it never pays gold at the pick or cuts an affix.</summary>
    public static bool AllowedAsDrawback(string key) => key != Gold && key != AffixCount;
}
