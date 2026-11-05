using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The Seat's player text in the C# literal, the English row and the 12 language rows. Each language row holds the seeded English, so a row
/// left behind would keep the old sentence for good: translator discovery treats a row equal to its English as untranslated, and no other
/// test compares a row with the English.
/// </summary>
[TestClass]
public class SeatPlayerTextTests
{
    private const string Modules = "Main/_Module/ModuleData/";

    private static string Literal(string key) =>
        Regex.Match(RepoPaths.ReadSource("Client/Adapters/SeatScreenAdapter.cs"), "\\{=" + key + "\\}([^\"]+)\"").Groups[1].Value;

    private static string Row(string text, string key)
    {
        var match = Regex.Match(text, "<string id=\"" + key + "\" text=\"(?:\\{=" + key + "\\})?([^\"]*)\"");
        Assert.IsTrue(match.Success, key + " has no row");
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string[] LanguageFiles() =>
        Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories);

    [DataTestMethod]
    [DataRow("taom_seat_take_hero_alive", "You can take a soldier only after the one you fight as has fallen.")]
    [DataRow("taom_seat_first_entry", "Press the Seat key to leave the Seat.")]
    [DataRow("taom_seat_hero_fell", "Your hero has fallen. Press Take this soldier (Delete unless rebound) to take a soldier.")]
    public void ASeatMessage_ReadsTheSameInTheCode_TheEnglishRow_AndEveryLanguageRow(string key, string expected)
    {
        Assert.AreEqual(expected, Literal(key), "the C# literal");
        Assert.AreEqual(expected, Row(RepoPaths.ReadSource(Modules + "taom_module_strings.xml"), key), "the English row");

        var files = LanguageFiles();
        Assert.AreEqual(12, files.Length, "one module string file for each of the 12 languages");
        foreach (var file in files)
            Assert.AreEqual(expected, Row(File.ReadAllText(file), key), file);
    }

    [TestMethod]
    public void TheTakeHeroAliveMessage_NeverSaysTheHeroStands_BecauseAfterATakeItIsTheTakenSoldierWhoStands()
    {
        var text = Literal("taom_seat_take_hero_alive");

        StringAssert.Contains(text, "only after the one you fight as has fallen");
        Assert.IsFalse(text.Contains("still stands"), "the answer also comes while a taken soldier lives, and the hero is dead by then");
    }
}
