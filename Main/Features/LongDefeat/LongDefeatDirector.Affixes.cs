using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>The affix proof (phase2-plan.md section 6): the <c>affix</c> check when a wave starts, and an <c>affix-effect</c> check per stat power once its first batch has closed.</summary>
internal sealed partial class LongDefeatDirector
{
    private bool _affixesToRead;

    private void BeginAffixes(int wave)
    {
        _affixesToRead = false;
        _terror = null;
        var rolled = _run.WaveAffixes;
        if (rolled.Count == 0 && _plan.Affixes.CountFor(wave) == 0)
            return;

        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.AffixId, "w" + wave, LongDefeatAffixChecks.Affix(_plan.Affixes, wave, rolled, _run.AffixCut));
        _logger.LogInfo($"{Tag}[Affix] w{wave} {string.Join(", ", rolled.Select(id => id + " (" + PlainName(_plan.Affixes.NameOf(id) ?? id) + ")"))}");
        if (_effects != null && rolled.Contains(AffixPlan.Hammer))
            RecordHammer(wave);
        if (_effects != null && rolled.Contains(AffixPlan.Terror))
            _terror = new TerrorWatch(wave, _plan.Affixes.Value(AffixPlan.Terror, "floor"));
        _affixesToRead = _effects != null && rolled.Any(id => LongDefeatAffixChecks.Expectations(_plan.Affixes, id).Count > 0);
    }

    /// <summary>Once per wave, with the first batch standing: each stat power and <c>hide</c> read back from a sampled attacker with no live race ability.</summary>
    private void ReadAffixEffects()
    {
        if (!_affixesToRead || _effects == null)
            return;

        _affixesToRead = false;
        var checks = _run.WaveAffixes
            .Select(power => (Power: power, Expected: LongDefeatAffixChecks.Expectations(_plan.Affixes, power)))
            .Where(c => c.Expected.Count > 0)
            .ToList();
        var sample = _effects.ReadStats(checks.SelectMany(c => c.Expected).Select(e => e.Stat).ToList());
        foreach (var (power, expected) in checks)
        {
            var verdict = LongDefeatAffixChecks.Effect(power, expected, sample);
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.AffixEffectId, power, verdict);
            _logger.LogInfo($"{Tag}[Affix] effect {verdict.Detail}");
        }
    }

    /// <summary>The English of a <c>{=KEY}English</c> name, for the log.</summary>
    private static string PlainName(string raw)
    {
        var close = raw.StartsWith("{=", System.StringComparison.Ordinal) ? raw.IndexOf('}') : -1;
        return close < 0 ? raw : raw.Substring(close + 1);
    }
}
