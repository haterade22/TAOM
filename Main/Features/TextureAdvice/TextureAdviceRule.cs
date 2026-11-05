using System;

namespace TAOM.Features.TextureAdvice;

/// <summary>When the one-time memory advice shows (docs/features/texture-advice.md). Pure.</summary>
internal static class TextureAdviceRule
{
    /// <summary>17 GiB: a machine sold as 16 GB reports a little under 16,384 MB, so this takes every 16 GB machine and smaller and leaves out 24 GB and more.</summary>
    internal const long AdviseAtOrBelowMb = 17408;

    internal static bool ShouldAdvise(long totalPhysicalMb, int textureQuality, int topTextureQuality, bool shownBefore) =>
        !shownBefore
        && totalPhysicalMb > 0 && totalPhysicalMb <= AdviseAtOrBelowMb
        && topTextureQuality > 0 && textureQuality >= topTextureQuality;

    internal static int RoundedGigabytes(long totalPhysicalMb) =>
        (int)Math.Round(totalPhysicalMb / 1024d, MidpointRounding.AwayFromZero);
}
