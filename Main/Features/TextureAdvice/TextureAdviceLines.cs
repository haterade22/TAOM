using System;
using System.Globalization;

namespace TAOM.Features.TextureAdvice;

/// <summary>The advice's log lines, built in one place.</summary>
internal static class TextureAdviceLines
{
    internal const string Tag = "[TextureAdvice]";

    internal static string BuildDecisionLine(long totalPhysMb, int quality, int top, bool shownBefore, bool advise) =>
        string.Format(CultureInfo.InvariantCulture, "{0} totalPhysMB={1} textureQuality={2}/{3} shownBefore={4}: {5}",
            Tag, totalPhysMb, quality, top, shownBefore ? "true" : "false",
            advise ? "advice shown (lower Texture Quality one step)" : "no advice");

    internal static string BuildFailedLine(Exception ex) =>
        $"{Tag} no advice: {ex.GetType().Name}: {ex.Message}";

    internal static string BuildNotRememberedLine(Exception ex) =>
        $"{Tag} the advice was shown but could not be recorded ({ex.GetType().Name}: {ex.Message}); it shows again at the next launch";
}
