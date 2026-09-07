using System;
using System.Collections.Generic;
using System.Text;
using TAOM.Features.DevConsole;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace TAOM.Features.MarriageAlignment.Cheats;

/// <summary>
/// <c>taom.print_marriages</c> — list every married couple whose cultures sit on opposite sides of
/// the Free/Evil line. READ-ONLY: it annuls nothing and mutates no hero.
/// </summary>
/// <remarks>
/// The #542 fix blocks FUTURE marriages only, deliberately: a save where the pairing already
/// happened keeps it, children and all. That makes the fix hard to confirm by eye, since the
/// evidence is an absence. This command is the measurement — take a count now, run the campaign
/// forward, and confirm it has not grown.
/// <para>
/// SHAPE IS LOAD-BEARING, NOT STYLE. A <c>[CommandLineArgumentFunction]</c> whose signature is not
/// exactly <c>public static string Name(List&lt;string&gt;)</c> throws inside the engine's unguarded
/// discovery loop at startup, past a native boundary with no managed backstop. Route through
/// <see cref="TaomConsole"/>. See docs/features/dev-console.md.
/// </para>
/// </remarks>
public static class MarriageAlignmentCheats
{
    private const string PrintUsage = "Format is \"taom.print_marriages\". No arguments.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_marriages", "taom")]
    public static string PrintMarriages(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, PrintUsage, args =>
        {
            var service = IoC.Resolve<IMarriageAlignmentService>();
            if (service == null) return "MarriageAlignment service is not registered.";

            var sb = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var crossAlignment = 0;
            var married = 0;

            foreach (var hero in Hero.AllAliveHeroes)
            {
                var spouse = hero?.Spouse;
                if (hero == null || spouse == null) continue;

                // One row per couple, keyed on the ordered id pair.
                var a = hero.StringId;
                var b = spouse.StringId;
                var key = string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
                if (!seen.Add(key)) continue;
                married++;

                var cultureA = hero.Culture?.StringId;
                var cultureB = spouse.Culture?.StringId;
                if (service.AreCulturesCompatible(cultureA, cultureB)) continue;

                crossAlignment++;
                sb.AppendLine(
                    $"  {hero.Name} ({cultureA ?? "<no culture>"}) + " +
                    $"{spouse.Name} ({cultureB ?? "<no culture>"}), {hero.Children.Count} child(ren)");
            }

            // State the toggles. The count below is ground truth about the couples' cultures and is
            // deliberately independent of them, but a reader watching the count grow with the
            // feature switched off would otherwise read that as "the fix stopped working" rather
            // than "the fix is off".
            var settings = IoC.Resolve<IMarriageAlignmentSettingsProvider>();
            var state = settings == null
                ? "settings unavailable"
                : $"enabled={settings.IsEnabled}, ai={settings.ApplyToAi}, player={settings.ApplyToPlayer}";

            var header = $"Marriage Alignment: {state}." + Environment.NewLine +
                         $"Married couples among living heroes: {married}. Cross-alignment: {crossAlignment}.";
            if (crossAlignment == 0)
                return header + " Nothing to report.";

            return header + Environment.NewLine + sb.ToString().TrimEnd();
        });
}
