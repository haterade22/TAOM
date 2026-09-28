using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the `taom.print_face` report. Pure — hero resolution lives in the cheat's entry point
/// (ADR-007), so every rendering branch here is testable without a live <c>Hero</c>.
///
/// The face editor has no export, and the console text is hard to copy, so the one line that
/// matters most is the exact <c>&lt;BodyProperties .../&gt;</c> string: it must reach the reader
/// byte-for-byte, quotes and all, or a pasted lord face silently corrupts.
/// </summary>
[TestClass]
public class FaceReportFormatterTests
{
    private static string Hex128() => string.Concat(Enumerable.Repeat("0123456789abcdef", 8));

    [TestMethod]
    public void Render_PlayerDefault_ReportsHeroDetails()
    {
        var snapshot = new FaceReportSnapshot
        {
            Found = true,
            HeroId = "main_hero",
            HeroName = "Aragorn",
            RaceName = "human",
            IsFemale = false,
            Age = 34.5f,
            BodyPropertiesText = "<BodyProperties version=\"4\" age=\"34.50000\" weight=\"0.50000\" "
                + "build=\"0.50000\" key=\"" + Hex128() + "\" />",
        };

        var lines = FaceReportFormatter.Render(snapshot);

        StringAssert.Contains(lines[0], "Aragorn");
        StringAssert.Contains(lines[0], "main_hero");
        StringAssert.Contains(lines[1], "human");
        StringAssert.Contains(lines[1], "False");
        StringAssert.Contains(lines[2], "<BodyProperties");
        Assert.IsTrue(lines.All(l => l.StartsWith(FaceReportFormatter.Prefix)));
    }

    [TestMethod]
    public void Render_NamedHero_ReportsHeroDetails()
    {
        var snapshot = new FaceReportSnapshot
        {
            Found = true,
            HeroId = "lord_saruman",
            HeroName = "Saruman",
            RaceName = "human",
            IsFemale = false,
            Age = 200f,
            BodyPropertiesText = "<BodyProperties version=\"4\" age=\"200.00000\" weight=\"0.50000\" "
                + "build=\"0.50000\" key=\"" + Hex128() + "\" />",
        };

        var lines = FaceReportFormatter.Render(snapshot);

        StringAssert.Contains(lines[0], "Saruman");
        StringAssert.Contains(lines[0], "lord_saruman");
        StringAssert.Contains(lines[1], "200");
    }

    [TestMethod]
    public void Render_UnknownId_ReportsMessageWithoutThrowing()
    {
        var snapshot = new FaceReportSnapshot
        {
            Found = false,
            ErrorMessage = "No hero found for id 'nonexistent_hero'.",
        };

        var lines = FaceReportFormatter.Render(snapshot);

        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains(lines[0], "nonexistent_hero");
        StringAssert.Contains(lines[0], FaceReportFormatter.Prefix);
    }

    [TestMethod]
    public void Render_NullSnapshot_ReportsMessageWithoutThrowing()
    {
        var lines = FaceReportFormatter.Render(null);

        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains(lines[0], FaceReportFormatter.Prefix);
    }

    [TestMethod]
    public void Render_BodyPropertiesString_PassesThroughUnmodified()
    {
        var bodyPropertiesText = "<BodyProperties version=\"4\" age=\"34.50000\" weight=\"0.50000\" "
            + "build=\"0.50000\" key=\"" + Hex128() + "\" />";

        var snapshot = new FaceReportSnapshot
        {
            Found = true,
            HeroId = "h",
            HeroName = "H",
            RaceName = "human",
            IsFemale = false,
            Age = 34.5f,
            BodyPropertiesText = bodyPropertiesText,
        };

        var lines = FaceReportFormatter.Render(snapshot);

        Assert.AreEqual($"{FaceReportFormatter.Prefix} {bodyPropertiesText}", lines[2]);
    }
}
