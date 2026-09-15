using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.LocalizationOverride;
using TAOM.Tests.Core;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LocalizationOverride;

/// <summary>
/// The main-menu <c>Module.CurrentModule.GlobalTextManager</c> is filled by <c>LoadDefaultTexts</c>
/// from each module's <c>ModuleData/global_strings.xml</c>, raw file after raw file, through
/// <c>GameText.AddVariationWithId</c>. That method APPENDS a same-id variation whose text differs and
/// <c>GetVariation</c> returns the FIRST match, so a TAOM row that reuses a vanilla variation id
/// (<c>str_campaign_starting_options_item_name.sturgia</c>) trails Native's and is never returned.
/// <see cref="GlobalStringsOverrides"/> re-applies TAOM's rows with the engine's replace primitive,
/// <c>SetVariationWithId</c>. The engine-backed tests here pin both halves of that claim on the
/// installed DLLs (#604).
/// </summary>
[TestClass]
public class GlobalStringsOverridesTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private const string ItemName = "str_campaign_starting_options_item_name";

    // ---- the parser mirrors GameTextManager.LoadFromXML: id split once on '.' ----

    [TestMethod]
    public void Parse_SplitsIdAndVariationOnTheFirstDot()
    {
        var rows = GlobalStringsOverrides.Parse(@"<?xml version=""1.0"" encoding=""utf-8""?>
<strings>
    <!-- a comment the engine skips too -->
    <string id=""str_campaign_starting_options_item_name.sturgia"" text=""{=taom_aso_kingdom.sturgia}Dale"" />
    <string id=""str_plain"" text=""{=taom_plain}No variation"" />
</strings>");

        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual(ItemName, rows[0].Id);
        Assert.AreEqual("sturgia", rows[0].Variation);
        Assert.AreEqual("{=taom_aso_kingdom.sturgia}Dale", rows[0].Text);
        Assert.AreEqual("str_plain", rows[1].Id);
        Assert.AreEqual("", rows[1].Variation, "An undotted id is the default variation, which the engine stores as \"\".");
    }

    [TestMethod]
    public void Parse_SkipsRowsMissingIdOrText()
    {
        // LoadFromXML throws per row and swallows it; skipping is the same observable outcome.
        var rows = GlobalStringsOverrides.Parse(@"<strings>
    <string text=""{=x}no id"" />
    <string id=""str_no_text"" />
    <string id=""str_ok.v"" text=""kept"" />
</strings>");

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("str_ok", rows[0].Id);
    }

    // ---- the engine facts this class exists for, pinned on the installed DLLs ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Engine_AddVariationWithId_AppendsAndTheFirstRowWins()
    {
        // If TaleWorlds ever makes AddVariationWithId replace, this fails and GlobalStringsOverrides
        // becomes dead weight to delete, rather than an unexplained belt-and-braces call.
        RequireGame();
        var manager = new GameTextManager();
        var text = manager.AddGameText(ItemName);
        text.AddVariationWithId("sturgia", new TextObject("{=PjO7oY16}Sturgia"), new List<GameTextManager.ChoiceTag>());
        text.AddVariationWithId("sturgia", new TextObject("{=taom_aso_kingdom.sturgia}Dale"), new List<GameTextManager.ChoiceTag>());

        Assert.AreEqual(2, text.Variations.Count(), "Expected the differing same-id row to be appended, not merged.");
        Assert.IsTrue(manager.TryGetText(ItemName, "sturgia", out var found));
        Assert.AreEqual("{=PjO7oY16}Sturgia", found.Value, "GetVariation is a first-match scan; the later row must lose.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Apply_ReplacesVanillasRowInPlace_AndAddsTaomOnlyRows()
    {
        RequireGame();
        var manager = new GameTextManager();
        manager.AddGameText(ItemName)
            .AddVariationWithId("sturgia", new TextObject("{=PjO7oY16}Sturgia"), new List<GameTextManager.ChoiceTag>());

        var applied = GlobalStringsOverrides.Apply(manager, new[]
        {
            (Id: ItemName, Variation: "sturgia", Text: "{=taom_aso_kingdom.sturgia}Dale"),
            (Id: ItemName, Variation: "rivendell", Text: "{=taom_aso_kingdom.rivendell}Imladris"),
        });

        Assert.AreEqual(2, applied);
        Assert.IsTrue(manager.TryGetText(ItemName, "sturgia", out var sturgia));
        Assert.AreEqual("{=taom_aso_kingdom.sturgia}Dale", sturgia.Value, "The vanilla-id row must be replaced, not appended behind vanilla's.");
        Assert.IsTrue(manager.TryGetText(ItemName, "rivendell", out var rivendell), "A TAOM-only id must resolve.");
        Assert.AreEqual("{=taom_aso_kingdom.rivendell}Imladris", rivendell.Value);
        Assert.AreEqual(2, manager.GetGameText(ItemName).Variations.Count(), "Replace in place: no duplicate sturgia row.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Apply_TheRealGlobalStrings_MakesEveryAsoKingdomResolveOverVanilla()
    {
        // Native first, exactly as LoadDefaultTexts orders modules, then TAOM's real file re-applied.
        RequireGame();
        var manager = new GameTextManager();
        var vanilla = manager.AddGameText(ItemName);
        foreach (var (id, english) in new[]
                 {
                     ("sturgia", "Sturgia"), ("vlandia", "Vlandia"), ("battania", "Battania"),
                     ("empire", "Northern Empire"), ("empire_w", "Western Empire"),
                     ("empire_s", "Southern Empire"), ("khuzait", "Khuzait"), ("aserai", "Aserai"),
                 })
            vanilla.AddVariationWithId(id, new TextObject("{=vanilla_" + id + "}" + english), new List<GameTextManager.ChoiceTag>());

        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "global_strings.xml");
        GlobalStringsOverrides.Apply(manager, GlobalStringsOverrides.ParseFromFile(path));

        var wrong = new List<string>();
        foreach (var kingdom in new[] { "sturgia", "vlandia", "battania", "empire", "empire_w", "empire_s", "khuzait", "aserai",
                                        "erebor", "rivendell", "mirkwood", "lothlorien", "isengard", "gundabad", "umbar",
                                        "dolguldur", "shaghana", "abanissa", "goblin", "mistymountainorcs", "lindon", "bluecraig" })
        {
            if (!manager.TryGetText(ItemName, kingdom, out var text)
                || !text.Value.StartsWith("{=taom_aso_kingdom." + kingdom + "}"))
                wrong.Add(kingdom + " -> " + (text?.Value ?? "<missing>"));
        }
        Assert.AreEqual(0, wrong.Count, "ASO picker rows that do not resolve to TAOM's text: " + string.Join("; ", wrong));
    }
}
