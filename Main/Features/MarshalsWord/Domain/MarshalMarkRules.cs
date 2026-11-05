namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>Whether the mark-effect check judges a marked company: only a ranged company on Stop is (build-spec 8.1); any other company is only logged.</summary>
internal enum MarkContext
{
    Other,
    RangedOnStop,
}

/// <summary>What one read of a marked company found: the company, its marked target, the sampled soldiers that aim at it, and how many sampled soldiers had a target at all.</summary>
internal readonly record struct MarkSample(int Company, int Target, int OnTarget, int Of, MarkContext Context);

/// <summary>A judged read of a marked company: the sample the verdict rests on, and whether at least half of the sampled soldiers aimed at the marked company.</summary>
internal readonly record struct MarkVerdict(MarkSample Sample, bool Pass);

/// <summary>The mark-effect rule of build-spec 8.1, pure so that a fixture can pin each side.</summary>
internal static class MarshalMarkRules
{
    /// <summary>A read judges only with at least <c>MarkMinSampled</c> soldiers that have a target.</summary>
    internal static bool Judgeable(MarkSample sample) => sample.Context != MarkContext.Other && sample.Of >= MarshalTuning.MarkMinSampled;

    /// <summary>At least half of the sampled soldiers with a target aim at the marked company.</summary>
    internal static bool Aims(MarkSample sample) => sample.OnTarget * 2 >= sample.Of;

    internal static MarkContext ContextOf(bool ranged, bool onStop) => ranged && onStop ? MarkContext.RangedOnStop : MarkContext.Other;
}
