using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.InputSystem;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The player-facing text of Marshal's Word's later stages. The C# literal (or MCM attribute) is the English source, and the English row in
/// <c>taom_module_strings.xml</c> must equal it. The key rows live in <c>global_strings.xml</c> with a seed row in each language file, and a
/// copied <c>{=KEY}</c> shows the old text, so a row left on an older wording fails. Each hint states its default, and the text is tied to the
/// number or the key it names.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalWordingTests
{
    private const string ScreenFile = "Client/Adapters/MarshalScreenAdapter.cs";
    private const string SettingsFile = "Main/Features/TaomSettings.cs";

    private static readonly (string File, string Key, string Wording)[] Lines =
    {
        (ScreenFile, "taom_marshal_fire_free", "The archers fire at will."),
        (ScreenFile, "taom_marshal_fire_on_word", "The archers draw and hold until you give the word, or for 14 seconds at most."),
        (ScreenFile, "taom_marshal_word_given", "The word is given."),
        (ScreenFile, "taom_marshal_fire_massed", "The archers draw and hold, and the word is given for you once most of them are ready. A bow is held for 14 seconds at most."),
        (ScreenFile, "taom_marshal_hold_on", "The selected companies hold the line: shielded foot soldiers in a shield wall, circle or square stop attacking until you press Hold the Line again or order a charge."),
        (ScreenFile, "taom_marshal_hold_off", "The selected companies fight again."),
        (SettingsFile, "taom_marshal_mcm_enabled_hint",
            "Richer orders for your companies in a battle. Once the battle has begun, hold Give the Order in Turn (default Left Shift, rebindable under Keybindings > Action) while you give an order with the order panel open, and the company carries it out after its current orders. Off, none of this acts from the next battle on, except Spacing Repair, which is decided when the game starts and needs this on at that start. Default: on."),
        (SettingsFile, "taom_marshal_mcm_hollow_square", "Hollow Square"),
        (SettingsFile, "taom_marshal_mcm_hollow_square_hint",
            "Once the battle has begun, a Square order on a company of yours that you command forms the plain square of the base game, which keeps the side you give it and grows only when its soldiers no longer fit, instead of the packed schiltron. A company of foot and ranged soldiers mixed keeps its mixed layout and ignores the side. Default: on."),
        (SettingsFile, "taom_marshal_mcm_spacing_repair", "Spacing Repair"),
        (SettingsFile, "taom_marshal_mcm_spacing_repair_hint",
            "A company of foot soldiers with riders under a tenth of its soldiers keeps the spacing limits of a foot company, as it already keeps their spacing, instead of the limits of cavalry. A change takes a restart of the game, and it applies only if Enable Marshal's Word is on when the game starts. It stays off in a co-op session and on a dedicated server, so that two machines never compute different widths. Default: on."),
        (SettingsFile, "taom_marshal_mcm_throwers_first", "Throwers Throw First"),
        (SettingsFile, "taom_marshal_mcm_throwers_first_hint",
            "On an Advance, a company of yours whose soldiers mostly carry javelins or other throwing weapons stops short of the enemy and throws, then goes in to melee when most have thrown, when the enemy comes close or after 40 seconds standing. A company holding the line, holding its fire, on a fire mode or in a shield wall, circle, square or column advances as before. Default: on."),
        (SettingsFile, "taom_marshal_mcm_outlines", "Outlines"),
        (SettingsFile, "taom_marshal_mcm_outlines_hint",
            "While the order panel is open, or while the show-indicators key (default Left Alt) is held, the soldiers of your selected companies wear a gold outline and an enemy company that one of them marks or charges wears a red one. Hold Mark, Do Not Move (default Left Ctrl) while you order a charge to mark an enemy company without moving: a mark steers archers and other ranged soldiers, and soldiers fighting hand to hand choose their own foe. Default: on."),
    };

    // The key rows of global_strings.xml, by the engine's id (category and game key id), with the English each language file seeds.
    private static readonly (string RowId, string Key, string Wording)[] KeyRows =
    {
        ("str_key_description.TaomMarshalHotKeyCategory_541", "taom_key_marshal_mark_desc",
            "Hold while you order a charge on an enemy company with the order panel open, and your company marks it as its target and keeps its own movement order. A mark steers archers and other ranged soldiers; soldiers fighting hand to hand choose their own foe. Defaults to Left Ctrl."),
        ("str_key_name.TaomMarshalHotKeyCategory_543", "taom_key_marshal_fire", "Change the Archers' Fire (TAOM)"),
        ("str_key_description.TaomMarshalHotKeyCategory_543", "taom_key_marshal_fire_desc",
            "Press with a company selected to switch its archers between firing at will, drawing their bows to hold until your word (or for 14 seconds at most), and drawing and holding until most of them are ready, when the word is given for you. Defaults to Comma."),
        ("str_key_name.TaomMarshalHotKeyCategory_544", "taom_key_marshal_word", "Give the Word (TAOM)"),
        ("str_key_description.TaomMarshalHotKeyCategory_544", "taom_key_marshal_word_desc",
            "Press with a company selected, and its archers who hold a drawn bow loose together. Defaults to M."),
        ("str_key_name.TaomMarshalHotKeyCategory_545", "taom_key_marshal_hold", "Hold the Line (TAOM)"),
        ("str_key_description.TaomMarshalHotKeyCategory_545", "taom_key_marshal_hold_desc",
            "Press with a company selected: its shielded foot soldiers in a shield wall, circle or square stop attacking and keep their places, and strike back for a few seconds when wounded. A charge, a retreat or a second press lets them fight. Defaults to Period."),
    };

    public static IEnumerable<object[]> LineData() => Lines.Select(l => new object[] { l.File, l.Key, l.Wording });

    public static IEnumerable<object[]> KeyRowData() => KeyRows.Select(r => new object[] { r.RowId, r.Key, r.Wording });

    private static string LiteralIn(string file, string key)
    {
        var src = RepoPaths.ReadSource(file, stripComments: true);
        var m = Regex.Match(src, @"""(\{=" + key + @"\}(?:\\.|[^""\\])*)""");
        Assert.IsTrue(m.Success, file + " must carry the {=" + key + "} literal");
        return m.Groups[1].Value;
    }

    private static List<string> Rows(string path, string key)
    {
        var text = File.ReadAllText(path);
        return Regex.Matches(text, @"<string\s+id=""" + key + @"""\s+text=""([^""]*)""")
            .Cast<Match>()
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .ToList();
    }

    private static string ModuleStrings() => RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml");

    private static string GlobalStrings() => RepoPaths.RepoPath("Main", "_Module", "ModuleData", "global_strings.xml");

    private static string[] LanguageFiles(string kind) =>
        Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_" + kind + "_strings_*.xml", SearchOption.AllDirectories);

    [DataTestMethod]
    [DynamicData(nameof(LineData), DynamicDataSourceType.Method)]
    public void TheLiteral_IsTheExpectedWording_WithItsKey(string file, string key, string wording)
    {
        Assert.AreEqual("{=" + key + "}" + wording, LiteralIn(file, key));
    }

    [DataTestMethod]
    [DynamicData(nameof(LineData), DynamicDataSourceType.Method)]
    public void TheEnglishRow_EqualsTheLiteral(string file, string key, string wording)
    {
        var rows = Rows(ModuleStrings(), key);

        Assert.AreEqual(1, rows.Count, "one English row for " + key);
        Assert.AreEqual("{=" + key + "}" + wording, rows[0]);
    }

    [DataTestMethod]
    [DynamicData(nameof(LineData), DynamicDataSourceType.Method)]
    public void EveryLanguageFile_SeedsTheEnglishWording(string file, string key, string wording)
    {
        // Nothing is translated yet, so each language's row is the English text; an English edit that missed a language shows here.
        var files = LanguageFiles("module");
        Assert.AreEqual(12, files.Length);
        foreach (var path in files)
        {
            var rows = Rows(path, key);
            Assert.AreEqual(1, rows.Count, Path.GetFileName(path) + " carries one row for " + key);
            Assert.AreEqual(wording, rows[0], Path.GetFileName(path));
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(KeyRowData), DynamicDataSourceType.Method)]
    public void TheKeyRow_IsTheExpectedWording_InGlobalStrings_AndSeededInEveryLanguage(string rowId, string key, string wording)
    {
        var rows = Rows(GlobalStrings(), rowId);

        Assert.AreEqual(1, rows.Count, rowId);
        Assert.AreEqual("{=" + key + "}" + wording, rows[0]);
        foreach (var path in LanguageFiles("keybind"))
        {
            var seeded = Rows(path, key);
            Assert.AreEqual(1, seeded.Count, Path.GetFileName(path) + " carries one row for " + key);
            Assert.AreEqual(wording, seeded[0], Path.GetFileName(path));
        }
    }

    [TestMethod]
    public void TheHoldText_NamesTheLimitTheRuleHas()
    {
        // MarshalFireRules releases a holder at HoldLimit even with no word, so a text that promised a hold without end would be false.
        var seconds = ((int)MarshalTuning.HoldLimit).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var message = Lines.Single(l => l.Key == "taom_marshal_fire_on_word").Wording;
        var description = KeyRows.Single(r => r.Key == "taom_key_marshal_fire_desc").Wording;

        StringAssert.Contains(message, seconds + " seconds");
        StringAssert.Contains(description, seconds + " seconds");
        Assert.AreEqual(14.0, MarshalTuning.HoldLimit);
    }

    [TestMethod]
    public void TheHoldTexts_NameTheRuleTheComponentHas_SoALineCompanyIsNotToldItStopsFighting()
    {
        // MarshalWordComponent qualifies only shielded foot soldiers in a shield wall, circle or square under an order that is not a charge or a retreat (build-spec 9.2).
        var message = Lines.Single(l => l.Key == "taom_marshal_hold_on").Wording;
        var description = KeyRows.Single(r => r.Key == "taom_key_marshal_hold_desc").Wording;

        foreach (var text in new[] { message, description })
        {
            StringAssert.Contains(text, "shielded foot soldiers");
            StringAssert.Contains(text, "shield wall, circle or square");
        }

        StringAssert.Contains(message, "or order a charge");
        StringAssert.Contains(description, "A charge, a retreat or a second press lets them fight");
        Assert.IsFalse(message.Contains("press the key again"), "a click on the panel also toggles it");
    }

    [TestMethod]
    public void TheEnableAndSpacingRepairHints_SayTheRepairNeedsEnableWhenTheGameStarts()
    {
        // MarshalSpacingPatch.Prepare reads Enabled once, at the first mission's patch pass, so a later change of the master setting leaves the repair as it was.
        var enable = Lines.Single(l => l.Key == "taom_marshal_mcm_enabled_hint").Wording;
        var repair = Lines.Single(l => l.Key == "taom_marshal_mcm_spacing_repair_hint").Wording;

        StringAssert.Contains(enable, "except Spacing Repair, which is decided when the game starts");
        StringAssert.Contains(repair, "applies only if Enable Marshal's Word is on when the game starts");
    }

    [TestMethod]
    public void TheSpacingAndSquareHints_StateTheRulesTheCodeHas()
    {
        // Formation.cs:536 tests the mounted ratio against 0.1, so a company with riders under a tenth of its soldiers keeps the foot limits; MarshalSquareRules grows a side to the arrangement's minimum.
        var repair = Lines.Single(l => l.Key == "taom_marshal_mcm_spacing_repair_hint").Wording;
        var square = Lines.Single(l => l.Key == "taom_marshal_mcm_hollow_square_hint").Wording;

        StringAssert.Contains(repair, "riders under a tenth of its soldiers");
        Assert.IsFalse(repair.Contains("one rider"), "one rider among ten or fewer is already a significant share");
        StringAssert.Contains(square, "grows only when its soldiers no longer fit");
        Assert.IsFalse(square.Contains("does not grow"));
    }

    [TestMethod]
    public void TheMarkTexts_SayAMarkSteersRangedSoldiers_AndSoldiersInHandToHandChooseTheirOwnFoe()
    {
        var hint = Lines.Single(l => l.Key == "taom_marshal_mcm_outlines_hint").Wording;
        var description = KeyRows.Single(r => r.Key == "taom_key_marshal_mark_desc").Wording;

        foreach (var text in new[] { hint, description })
        {
            StringAssert.Contains(text, "steers archers and other ranged soldiers");
            StringAssert.Contains(text, "soldiers fighting hand to hand choose their own foe");
        }
    }

    [TestMethod]
    public void EveryKeyDescription_NamesTheDefaultKeyTheCategoryRegisters()
    {
        var labels = new Dictionary<InputKey, string>
        {
            [InputKey.LeftShift] = "Left Shift", [InputKey.LeftControl] = "Left Ctrl", [InputKey.LeftAlt] = "Left Alt", [InputKey.Comma] = "Comma", [InputKey.M] = "M", [InputKey.Period] = "Period",
        };
        var sut = new TaomMarshalHotKeyCategory();
        var rows = System.Xml.Linq.XDocument.Load(GlobalStrings()).Root!.Elements("string").ToList();

        foreach (var key in sut.RegisteredGameKeys.Where(k => k != null))
        {
            var row = rows.Single(e => (string)e.Attribute("id")! == "str_key_description." + TaomMarshalHotKeyCategory.CategoryId + "_" + key.Id);
            StringAssert.EndsWith((string)row.Attribute("text")!, "Defaults to " + labels[key.KeyboardKey.InputKey] + ".", "key " + key.Id);
        }
    }

    [TestMethod]
    public void EveryMarshalSettingsHint_StatesTheDefaultTheProviderHolds()
    {
        var settings = RepoPaths.ReadSource(SettingsFile, stripComments: true);
        var defaults = new (string Key, bool Value)[]
        {
            ("taom_marshal_mcm_enabled_hint", MarshalSettingsProvider.DefaultEnabled),
            ("taom_marshal_mcm_keep_array_hint", MarshalSettingsProvider.DefaultKeepArray),
            ("taom_marshal_mcm_arrive_together_hint", MarshalSettingsProvider.DefaultArriveTogether),
            ("taom_marshal_mcm_outlines_hint", MarshalSettingsProvider.DefaultOutlines),
        };

        foreach (var (key, value) in defaults)
        {
            var hint = LiteralIn(SettingsFile, key);
            StringAssert.EndsWith(hint, value ? "Default: on." : "Default: off.", key);
        }

        Assert.IsTrue(settings.Contains("MarshalOutlines { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultOutlines;"));
    }

    [TestMethod]
    public void TheNewText_HasNoDashAndNoBrace_ExceptTheKeyPrefix()
    {
        foreach (var (_, _, wording) in Lines)
        {
            StringAssert.DoesNotMatch(wording, new Regex("[\u2013\u2014{}]"), wording);
        }

        foreach (var (_, _, wording) in KeyRows)
        {
            StringAssert.DoesNotMatch(wording, new Regex("[\u2013\u2014{}]"), wording);
        }
    }
}
