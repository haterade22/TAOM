using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TAOM.Features.AiPartySize.Cheats;

/// <summary>
/// One AI party's food picture as the report sees it. Pure data, filled by the console command from
/// engine state and consumed by <see cref="AiFoodReliefReport"/>, so the arithmetic that answers the
/// question is unit-testable without a campaign.
/// </summary>
public struct AiFoodReliefRow
{
    public string PartyName;
    public string ClanName;
    public string CultureName;
    public int Members;

    /// <summary>Whether this party passes <see cref="AiPartySizeService.IsScalableAiLordParty"/>.</summary>
    public bool Eligible;

    /// <summary>Actual daily consumption divided by the vanilla members/20 rate.</summary>
    public float Residual;
}

/// <summary>
/// Turns the rows into the answer to one question: is a party eating more than the relief setting
/// says because the relief COMPOSED badly, or because the party was never eligible for it?
///
/// That distinction is the one an external bug report on this feature could not make from
/// `MobileParty.FoodChange` alone. It measured a 9.2x spread in the residual and attributed all of
/// it to the additive relief, but a party failing `IsScalableAiLordParty` never reaches the relief
/// at all and its residual is pure vanilla perks plus culture feats. Splitting the population by
/// eligibility separates the two causes on sight.
/// </summary>
public static class AiFoodReliefReport
{
    /// <summary>
    /// Residual a party SHOULD land on: the relieved baseline times its clamped ability frame. The
    /// band, not a single number, because perks and feats stay meaningful inside it.
    /// </summary>
    public static void ExpectedResidualBand(float relief, out float low, out float high)
    {
        float residual = 1f - relief;
        low = residual * AiPartySizeService.MinAbilityScale;
        high = residual * AiPartySizeService.MaxAbilityScale;
    }

    public static string Summarize(IReadOnlyList<AiFoodReliefRow> rows, float relief)
    {
        if (rows == null || rows.Count == 0)
            return "No AI parties consuming food were found.";

        ExpectedResidualBand(relief, out var low, out var high);

        var eligible = rows.Where(r => r.Eligible).ToList();
        var ineligible = rows.Where(r => !r.Eligible).ToList();

        var text = new StringBuilder();
        text.AppendLine($"AI food relief: {Num(relief)}   ability clamp: "
            + $"{Num(AiPartySizeService.MinAbilityScale)}-{Num(AiPartySizeService.MaxAbilityScale)}");
        text.AppendLine($"Expected residual for an eligible party: {Num(low)}-{Num(high)} of vanilla.");
        text.AppendLine();
        text.AppendLine(Describe("Eligible (relief applies)", eligible));
        text.AppendLine(Describe("Not eligible (relief never runs)", ineligible));

        int outOfBand = eligible.Count(r => relief > 0f && (r.Residual < low - 0.02f || r.Residual > high + 0.02f));
        text.AppendLine();
        text.AppendLine(outOfBand == 0
            ? "Every eligible party is inside the band. One setting, one outcome."
            : $"{outOfBand} eligible parties are OUTSIDE the band — the composition is not holding.");

        return text.ToString();
    }

    public static string Table(IReadOnlyList<AiFoodReliefRow> rows, int limit)
    {
        var text = new StringBuilder();
        text.AppendLine("residual  elig  men   party / clan / culture");

        foreach (var row in rows.OrderByDescending(r => r.Residual).Take(limit))
        {
            text.AppendLine(
                $"{Num(row.Residual),8}  {(row.Eligible ? "yes " : "NO  ")}  {row.Members,4}  "
                + $"{row.PartyName} / {row.ClanName} / {row.CultureName}");
        }

        if (rows.Count > limit)
            text.AppendLine($"... {rows.Count - limit} more, highest residual shown first.");

        return text.ToString();
    }

    private static string Describe(string label, IReadOnlyList<AiFoodReliefRow> rows)
    {
        if (rows.Count == 0)
            return $"{label}: none.";

        var sorted = rows.Select(r => r.Residual).OrderBy(v => v).ToList();
        float median = sorted[sorted.Count / 2];
        float min = sorted[0];
        float max = sorted[sorted.Count - 1];

        // Spread is the number the whole exercise is about: one slider should not produce a 9x range.
        string spread = min > 0.0001f ? $"{Num(max / min)}x" : "unbounded";

        return $"{label}: {rows.Count} parties, median {Num(median)}, "
             + $"range {Num(min)}-{Num(max)}, spread {spread}.";
    }

    private static string Num(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);
}
