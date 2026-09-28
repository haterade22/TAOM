using System.Collections.Generic;
using System.Globalization;

namespace TAOM.Features.DevConsole;

/// <summary>
/// The data <c>taom.print_face</c> reports, gathered at the entry point (ADR-007: sealed
/// <c>Hero</c>/<c>BodyProperties</c> stay out of this pure class). <see cref="BodyPropertiesText"/>
/// is <c>Hero.BodyProperties.ToString()</c> verbatim — never reformatted, since it is the exact
/// string a lord's <c>&lt;face&gt;</c> needs.
/// </summary>
internal sealed class FaceReportSnapshot
{
    internal bool Found;
    internal string ErrorMessage;
    internal string HeroId;
    internal string HeroName;
    internal string RaceName;
    internal bool IsFemale;
    internal float Age;
    internal string BodyPropertiesText;
}

/// <summary>
/// Renders the <c>taom.print_face</c> report. Pure — no engine types cross this boundary — so every
/// branch is testable without a live <c>Hero</c>.
///
/// Built because v1.5.3 has no way to get a face built in the editor back out: the face editor has
/// no export/clipboard action, vanilla ships no console command for it, and neither did TAOM's own
/// console until now. The <c>BodyPropertiesText</c> line is the payload — copy it verbatim into a
/// lord's <c>&lt;face&gt;</c> (docs/reference/race-face-and-hand-morphs.md, "Exporting a face for a
/// lord").
/// </summary>
internal static class FaceReportFormatter
{
    internal const string Prefix = "[PrintFace]";

    internal static List<string> Render(FaceReportSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.Found)
        {
            var message = snapshot?.ErrorMessage;
            if (string.IsNullOrWhiteSpace(message)) message = "No hero found.";
            return new List<string> { $"{Prefix} {message}" };
        }

        return new List<string>
        {
            $"{Prefix} {snapshot.HeroName} ({snapshot.HeroId})",
            $"{Prefix} race={snapshot.RaceName} isFemale={snapshot.IsFemale} "
                + $"age={snapshot.Age.ToString("0.##", CultureInfo.InvariantCulture)}",
            $"{Prefix} {snapshot.BodyPropertiesText}",
        };
    }
}
