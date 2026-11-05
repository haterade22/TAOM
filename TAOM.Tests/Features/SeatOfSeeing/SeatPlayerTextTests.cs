using System.Collections.Generic;
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

    // The key rows of the three time keys in global_strings.xml, by the engine's id, with the English each keybind language file seeds.
    private static readonly (string RowId, string Key, string Wording)[] TimeKeyRows =
    {
        ("str_key_name.TaomSeatHotKeyCategory_533", "taom_key_seat_quicken", "Quicken time (TAOM)"),
        ("str_key_description.TaomSeatHotKeyCategory_533", "taom_key_seat_quicken_desc",
            "In a battle, steps mission time up one level: 0.25, 0.5, 1, 2, 4 and 10. Level 10 needs the Seat up or nobody left for you to fight as. It works from the Seat or from your hero's eyes, in single player only. Defaults to Page Up."),
        ("str_key_name.TaomSeatHotKeyCategory_534", "taom_key_seat_slow", "Slow time (TAOM)"),
        ("str_key_description.TaomSeatHotKeyCategory_534", "taom_key_seat_slow_desc",
            "In a battle, steps mission time down one level, to a quarter of normal speed at the lowest. Defaults to Page Down."),
        ("str_key_name.TaomSeatHotKeyCategory_535", "taom_key_seat_stay", "Stay time (TAOM)"),
        ("str_key_description.TaomSeatHotKeyCategory_535", "taom_key_seat_stay_desc",
            "In a battle, cancels fast time and pauses the mission; press it again to go on. The Seat's view still flies while the battle is paused. Defaults to Home."),
    };

    public static IEnumerable<object[]> TimeKeyRowData() => TimeKeyRows.Select(r => new object[] { r.RowId, r.Key, r.Wording });

    private static List<string> RowsById(string path, string id) =>
        Regex.Matches(File.ReadAllText(path), @"<string\s+id=""" + Regex.Escape(id) + @"""\s+text=""([^""]*)""")
            .Cast<Match>().Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();

    private static string SettingLiteral(string key) =>
        Regex.Match(RepoPaths.ReadSource("Main/Features/TaomSettings.cs"), @"\{=" + key + @"\}([^""]+)""").Groups[1].Value;

    private static string[] LanguageFiles() =>
        Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories);

    [DataTestMethod]
    [DataRow("taom_seat_take_hero_alive", "You can take a soldier only after the one you fight as has fallen.")]
    [DataRow("taom_seat_first_entry", "Press the Seat key to leave the Seat.")]
    [DataRow("taom_seat_hero_fell", "Your hero has fallen. Press Take this soldier (Delete unless rebound) to take a soldier.")]
    [DataRow("taom_seat_time_level", "Mission time: {LEVEL}x")]
    [DataRow("taom_seat_time_stayed", "The battle is paused. Press Stay time (Home unless rebound) again to go on.")]
    [DataRow("taom_seat_time_resumed", "The battle goes on.")]
    [DataRow("taom_seat_time_other_clock", "This battle has its own speed control.")]
    public void ASeatMessage_ReadsTheSameInTheCode_TheEnglishRow_AndEveryLanguageRow(string key, string expected)
    {
        Assert.AreEqual(expected, Literal(key), "the C# literal");
        Assert.AreEqual(expected, Row(RepoPaths.ReadSource(Modules + "taom_module_strings.xml"), key), "the English row");

        var files = LanguageFiles();
        Assert.AreEqual(12, files.Length, "one module string file for each of the 12 languages");
        foreach (var file in files)
            Assert.AreEqual(expected, Row(File.ReadAllText(file), key), file);
    }

    [DataTestMethod]
    [DynamicData(nameof(TimeKeyRowData), DynamicDataSourceType.Method)]
    public void ATimeKeyRow_IsTheExpectedWording_InGlobalStrings_AndSeededInEveryKeybindLanguageFile(string rowId, string key, string wording)
    {
        var global = RowsById(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "global_strings.xml"), rowId);

        Assert.AreEqual(1, global.Count, rowId);
        Assert.AreEqual("{=" + key + "}" + wording, global[0], "global_strings.xml");
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_keybind_strings_*.xml", SearchOption.AllDirectories);
        Assert.AreEqual(12, files.Length, "one keybind string file for each of the 12 languages");
        foreach (var file in files)
        {
            var seeded = RowsById(file, key);
            Assert.AreEqual(1, seeded.Count, Path.GetFileName(file) + " carries one row for " + key);
            Assert.AreEqual(wording, seeded[0], Path.GetFileName(file));
        }
    }

    [TestMethod]
    public void TheEnabledHint_SaysTheCancelWaitsForTheNextTimeKey_BecauseTheSettingIsReadOnlyAtAPress()
    {
        var hint = SettingLiteral("taom_seat_mcm_enabled_hint");

        StringAssert.Contains(hint, "and the next press of one cancels any change they made");
    }

    [TestMethod]
    public void TheRiseHint_SaysTheLiftWorksWithTheSeatTurnedOff_BecauseItHasItsOwnSetting()
    {
        var hint = SettingLiteral("taom_seat_mcm_rise_hint");

        StringAssert.Contains(hint, "It works with the Seat turned off too.");
    }

    [TestMethod]
    public void TheTakeHeroAliveMessage_NeverSaysTheHeroStands_BecauseAfterATakeItIsTheTakenSoldierWhoStands()
    {
        var text = Literal("taom_seat_take_hero_alive");

        StringAssert.Contains(text, "only after the one you fight as has fallen");
        Assert.IsFalse(text.Contains("still stands"), "the answer also comes while a taken soldier lives, and the hero is dead by then");
    }

    [DataTestMethod]
    [DataRow("taom_seat_mcm_enabled_hint")]
    [DataRow("taom_seat_mcm_rise_hint")]
    [DataRow("taom_seat_mcm_rise")]
    public void ASettingText_ReadsTheSameInTheAttribute_TheEnglishRow_AndEveryLanguageRow(string key)
    {
        var literal = Regex.Match(RepoPaths.ReadSource("Main/Features/TaomSettings.cs"), "\\{=" + key + "\\}([^\"]+)\"").Groups[1].Value;
        Assert.IsTrue(literal.Length > 0, key + " has no literal in TaomSettings");
        Assert.AreEqual(literal, Row(RepoPaths.ReadSource(Modules + "taom_module_strings.xml"), key), "the English row");

        foreach (var file in LanguageFiles())
            Assert.AreEqual(literal, Row(File.ReadAllText(file), key), file);
    }
}
