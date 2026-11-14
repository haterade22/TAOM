using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NumberStyles = System.Globalization.NumberStyles;
using Invariant = System.Globalization.CultureInfo;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.LordFaceVariety;

/// <summary>What one Lord Face Variety pass did. <see cref="StillShared"/> lists the id groups that still share a face.</summary>
public sealed record FaceVarietyReport(
    int RolledInRange,
    int RolledAroundOwnFace,
    int RolledGenerated,
    int Failed,
    IReadOnlyList<IReadOnlyList<string>> StillShared);

/// <summary>
/// Lord Face Variety (#952, docs/features/lord-face-variety.md): one pass at campaign creation that stops
/// campaign heroes from sharing a face. Rule A: a hero still holding the minimum of a range that has spread
/// (a <c>face_key_template</c>) is rolled inside that range, the way the engine rolls a hero born from a
/// template. Rule B: heroes of one race and sex that still share an explicit face key, compared without its
/// colour fields (<see cref="FaceShapeKey"/>), keep it for one anchor and are rolled near their own face. Rule C: a hero generated at campaign creation (not an original character,
/// such as an initial child cloned from a lord) whose range is flat is rolled near its own face too. All decisions are here; the engine is reached through
/// <see cref="IHeroFaceAdapter"/>.
/// </summary>
public sealed class LordFaceVarietyService
{
    /// <summary>How far a rule B roll may move from the hero's own face (0 keeps it, 1 is a free roll). Tune after an in-game look.</summary>
    public const float OwnFaceVariation = 0.2f;

    /// <summary>The age a rule B or rule C roll is made at, so a young hero draws from the adult hair and beard tables.</summary>
    public const float RollAge = 30f;

    /// <summary>Anchors, in preference order: the canon heroes whose face should stay as authored.</summary>
    public static IReadOnlyList<string> CanonAnchorIds { get; } = new[]
    {
        "lord_R1_1",               // Elrond
        "lord_L1_1",               // Galadriel
        "lord_L1_2",               // Celeborn
        "lord_M1_1",               // Thranduil
        "lord_M1_11",              // Legolas
        "named_companion_gimli",   // Gimli
        "named_companion_aragorn", // Aragorn
        "lord_E1_5",               // Durin
    };

    /// <summary>Heroes whose shared face is the point: Elladan and Elrohir are identical twins.</summary>
    public static IReadOnlyCollection<string> SharedByDesignIds { get; } = new[] { "lord_R1_3", "lord_R1_4" };

    /// <summary>
    /// Bits of the key's first 64-bit part that rule B ignores: hair colour (30-35), two fields the recoloured
    /// heroes of #1221 also differ in (36-47, not decoded; eye colour is a guess) and skin colour (48-53). A
    /// hero whose key differs from another's only here shows the same face in other colours.
    /// </summary>
    public const ulong ColourBits = 0x003FFFFFC0000000UL;

    private static readonly Regex HexKey = new("[0-9A-Fa-f]{128}", RegexOptions.CultureInvariant);

    private const string LogPrefix = "[LordFaceVariety]";

    private readonly IHeroFaceAdapter _adapter;
    private readonly IModLogger _logger;

    public LordFaceVarietyService(IHeroFaceAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    /// <summary>
    /// FNV-1a 32-bit of the hero id, masked to 31 bits so it is never negative; the native roll treats -1 as
    /// "return the minimum". Stable across runs and machines.
    /// </summary>
    public static int SeedFor(string heroId)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(heroId ?? string.Empty))
        {
            hash ^= b;
            hash = unchecked(hash * 16777619u);
        }

        return (int)(hash & 0x7FFFFFFFu);
    }

    /// <summary>
    /// The face key with the <see cref="ColourBits"/> of its first part cleared, for grouping. The key is the
    /// first run of 128 hex digits in <paramref name="staticKey"/> (v1.5.7 <c>StaticBodyProperties.ToString()</c>
    /// writes <c>key="</c> and the eight parts as 16 upper-case digits each). Text without such a run comes back
    /// unchanged.
    /// </summary>
    public static string FaceShapeKey(string staticKey)
    {
        var match = HexKey.Match(staticKey ?? string.Empty);
        if (!match.Success) return staticKey;
        var part1 = ulong.Parse(match.Value.Substring(0, 16), NumberStyles.HexNumber, Invariant.InvariantCulture) & ~ColourBits;
        return staticKey.Substring(0, match.Index) + part1.ToString("X16", Invariant.InvariantCulture)
            + staticKey.Substring(match.Index + 16);
    }

    public FaceVarietyReport Run()
    {
        var candidates = _adapter.GetSnapshots()
            .Where(IsCandidate)
            .OrderBy(h => h.Id, StringComparer.Ordinal)
            .ToList();
        var originals = candidates.Where(h => h.IsOriginal).ToList();

        var failed = 0;
        var rolledInRange = 0;
        var rolledAroundOwnFace = 0;
        var rolledGenerated = 0;

        var rolledByA = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hero in originals.Where(h => h.RangeHasSpread && h.OnRangeMin))
        {
            if (TryRoll(hero.Id, () => _adapter.TryRollInRange(hero, SeedFor(hero.Id)), ref failed))
            {
                rolledByA.Add(hero.Id);
                rolledInRange++;
            }
        }

        foreach (var group in SharedGroups(originals.Where(h => !rolledByA.Contains(h.Id))))
        {
            var kept = SharedByDesignIds.Any(id => group.Any(h => h.Id == id))
                ? new HashSet<string>(SharedByDesignIds, StringComparer.Ordinal)
                : new HashSet<string>(new[] { PickAnchor(group).Id }, StringComparer.Ordinal);

            foreach (var hero in group.Where(h => !kept.Contains(h.Id)))
            {
                if (RollAroundOwnFace(hero, ref failed)) rolledAroundOwnFace++;
            }
        }

        // Rule C: a generated hero (a child cloned from a lord) on a flat range holds its parent's face.
        foreach (var hero in candidates.Where(h => !h.IsOriginal && !h.RangeHasSpread))
        {
            if (RollAroundOwnFace(hero, ref failed)) rolledGenerated++;
        }

        var stillShared = SharedGroups(_adapter.GetSnapshots().Where(IsCandidate))
            .Select(g => (IReadOnlyList<string>)g.Select(h => h.Id).OrderBy(x => x, StringComparer.Ordinal).ToList())
            .OrderBy(g => g[0], StringComparer.Ordinal)
            .ToList();

        var report = new FaceVarietyReport(rolledInRange, rolledAroundOwnFace, rolledGenerated, failed, stillShared);
        _logger.LogInfo($"{LogPrefix} varied {rolledInRange}+{rolledAroundOwnFace}+{rolledGenerated}, {stillShared.Count} still shared: "
            + (stillShared.Count == 0 ? "none" : string.Join("; ", stillShared.Select(g => string.Join("|", g)))));
        return report;
    }

    private static bool IsCandidate(HeroFaceSnapshot hero) => !hero.IsPlayer && !string.IsNullOrEmpty(hero.Id);

    private bool RollAroundOwnFace(HeroFaceSnapshot hero, ref int failed) =>
        TryRoll(hero.Id, () => _adapter.TryRollAroundOwnFace(hero, SeedFor(hero.Id), OwnFaceVariation, RollAge), ref failed);

    private bool TryRoll(string heroId, Func<bool> roll, ref int failed)
    {
        try
        {
            if (roll()) return true;
            _logger.LogWarning($"{LogPrefix} no roll for {heroId}: hero not found");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{LogPrefix} roll failed for {heroId}: {ex.Message}");
        }

        failed++;
        return false;
    }

    private static IEnumerable<List<HeroFaceSnapshot>> SharedGroups(IEnumerable<HeroFaceSnapshot> heroes) =>
        heroes
            .GroupBy(h => (h.RaceKey, h.IsFemale, FaceShapeKey(h.StaticKey)))
            .Where(g => g.Count() > 1)
            .Select(g => g.OrderBy(h => h.Id, StringComparer.Ordinal).ToList());

    private static HeroFaceSnapshot PickAnchor(List<HeroFaceSnapshot> group)
    {
        foreach (var id in CanonAnchorIds)
        {
            var canon = group.FirstOrDefault(h => h.Id == id);
            if (canon != null) return canon;
        }

        return group.FirstOrDefault(h => h.IsClanLeader) ?? group[0];
    }
}
