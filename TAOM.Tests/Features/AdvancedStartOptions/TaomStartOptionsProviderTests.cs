using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedStartOptions;
using TAOM.Tests.Core;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.AdvancedStartOptions;

/// <summary>
/// Two things the compiler cannot certify about the Advanced Starting Options provider.
///
/// <para>
/// The engine discovers providers by reflection: <c>AdvancedStartOptionsManager.Initialize</c> walks
/// every static method carrying <c>[StartOptionsProvider]</c> and binds it with
/// <c>Delegate.CreateDelegate(typeof(StartOptionsProviderDelegate), method)</c>. A signature drift
/// is a <c>Debug.FailedAssert</c> in a release build, which is to say silence: the menu simply keeps
/// its eight vanilla factions. The binding test performs the same <c>CreateDelegate</c> against the
/// installed engine's private delegate type.
/// </para>
///
/// <para>
/// The kingdom list is a literal because the menu runs before any campaign exists. It must match
/// <c>taom_spkingdoms.xml</c> exactly: a missing kingdom is invisible to the pickers, a stale id is a
/// picker entry that resolves to nothing.
/// </para>
/// </summary>
[TestClass]
public class TaomStartOptionsProviderTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void TaomKingdomIds_MatchTaomSpkingdoms()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_spkingdoms.xml");
        var declared = XDocument.Load(path).Descendants("Kingdom")
            .Select(k => (string)k.Attribute("id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var provided = TaomStartOptionsProvider.TaomKingdomIds.OrderBy(id => id, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(declared, provided,
            "TaomStartOptionsProvider.TaomKingdomIds must list exactly the kingdoms taom_spkingdoms.xml declares. "
            + "Declared: " + string.Join(", ", declared) + ". Provided: " + string.Join(", ", provided));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Provider_BindsTheWayTheEngineDiscoversIt()
    {
        RequireGame();
        var manager = AccessTools.TypeByName("SandBox.AdvancedStartOptions.AdvancedStartOptionsManager");
        Assert.IsNotNull(manager, "SandBox.AdvancedStartOptions.AdvancedStartOptionsManager did not resolve.");
        var attribute = AccessTools.TypeByName("SandBox.AdvancedStartOptions.StartOptionsProviderAttribute");
        Assert.IsNotNull(attribute, "StartOptionsProviderAttribute did not resolve; the extension point moved.");
        var delegateType = manager!.GetNestedType("StartOptionsProviderDelegate", BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(delegateType, "AdvancedStartOptionsManager.StartOptionsProviderDelegate did not resolve; the binding contract moved.");

        var providers = typeof(TaomStartOptionsProvider)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttributes(attribute!, false).Length > 0)
            .ToList();
        Assert.AreEqual(1, providers.Count, "Expected exactly one [StartOptionsProvider] method on TaomStartOptionsProvider.");

        var bound = Delegate.CreateDelegate(delegateType!, providers[0], throwOnBindFailure: false);
        Assert.IsNotNull(bound,
            "The engine binds providers with Delegate.CreateDelegate(StartOptionsProviderDelegate, method); "
            + "TaomStartOptionsProvider." + providers[0].Name + " no longer matches 'static void (AdvancedStartOptions)'.");
    }

    // ---- the merged option set, vanilla's provider first as load order guarantees ----

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void MergedOptions_VanillaThenTaom_CarryEveryTaomKingdomAndNoUnitedEmpire()
    {
        RequireGame();
        var optionsType = AccessTools.TypeByName("SandBox.AdvancedStartOptions.AdvancedStartOptions");
        var vanillaProvider = AccessTools.TypeByName("SandBox.View.AdvancedStartOptions.SandBoxStartOptionsProvider");
        var attribute = AccessTools.TypeByName("SandBox.AdvancedStartOptions.StartOptionsProviderAttribute");
        Assert.IsNotNull(optionsType, "SandBox.AdvancedStartOptions.AdvancedStartOptions did not resolve.");
        Assert.IsNotNull(vanillaProvider, "SandBox.View.AdvancedStartOptions.SandBoxStartOptionsProvider did not resolve; the vanilla provider moved.");
        Assert.IsNotNull(attribute, "StartOptionsProviderAttribute did not resolve.");

        // The engine runs every provider in assembly load order against one AdvancedStartOptions;
        // SandBox loads before TAOM, so vanilla's pickers exist when TAOM's provider asks for them.
        var options = Activator.CreateInstance(optionsType!);
        InvokeProvider(vanillaProvider!, attribute!, options);
        InvokeProvider(typeof(TaomStartOptionsProvider), attribute!, options);

        var getOption = optionsType.GetMethods()
            .Single(m => m.Name == "GetOption" && !m.IsGenericMethod && m.GetParameters().Length == 1);
        foreach (var key in new[] { "KingdomId", "LastStandKingdomId", "InvasionScenarioFactionId", "TwoFactionWarFaction1Id", "TwoFactionWarFaction2Id" })
        {
            var picker = getOption.Invoke(options, new object[] { key });
            Assert.IsNotNull(picker, key + " is no longer an option vanilla registers; the picker keys drifted.");
            var ids = ItemIds(picker!);
            var missing = TaomStartOptionsProvider.TaomKingdomIds.Where(id => !ids.Contains(id)).ToList();
            Assert.AreEqual(0, missing.Count, key + " lacks the TAOM kingdoms: " + string.Join(", ", missing));
        }

        var scenario = getOption.Invoke(options, new object[] { "Scenario" });
        Assert.IsNotNull(scenario, "The Scenario option is gone; the scenario key drifted.");
        var scenarios = ItemIds(scenario!);
        CollectionAssert.DoesNotContain(scenarios, "unitedempire", "United Empire is still selectable.");
        CollectionAssert.Contains(scenarios, "none", "The default scenario should survive the removal.");
    }

    // ---- where the string rows live, which is the whole bug of #604 ----

    // The eight playable kingdoms that keep vanilla StringIds (renamed in place by spkingdoms.xslt).
    private static readonly string[] RenamedVanillaKingdomIds =
        { "empire", "empire_w", "empire_s", "sturgia", "aserai", "vlandia", "battania", "khuzait" };

    // Scenario text vanilla words as "Calradia"; TAOM re-words each of these for Middle-earth.
    private static readonly string[] CalradiaRewriteIds =
    {
        "str_campaign_starting_options_description.InvasionScenarioFactionId",
        "str_campaign_starting_options_item_description.InvasionId",
        "str_campaign_starting_options_item_name.alternativecalradia",
        "str_campaign_starting_options_item_description.alternativecalradia",
    };

    private static List<string> StringIds(string fileName)
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), fileName);
        Assert.IsTrue(File.Exists(path), fileName + " not found at " + path);
        return XDocument.Load(path).Descendants("string")
            .Select(s => (string)s.Attribute("id") ?? "")
            .ToList();
    }

    [TestMethod]
    public void EveryPickerKingdom_HasItsMenuNameInGlobalStrings()
    {
        // The ASO screen runs from the main menu and reads Module.CurrentModule.GlobalTextManager,
        // which LoadDefaultTexts fills from the LITERAL path ModuleData/global_strings.xml of each
        // module and nothing else. A row anywhere else renders as
        // "ERROR: Text with id str_campaign_starting_options_item_name doesn't exist! Variation: <id>"
        // (#604: fourteen of those, one per TAOM kingdom).
        var ids = StringIds("global_strings.xml");
        var missing = TaomStartOptionsProvider.TaomKingdomIds.Concat(RenamedVanillaKingdomIds)
            .Select(k => "str_campaign_starting_options_item_name." + k)
            .Concat(CalradiaRewriteIds)
            .Where(id => !ids.Contains(id))
            .ToList();
        Assert.AreEqual(0, missing.Count,
            "global_strings.xml lacks ASO menu rows; the picker shows the ERROR text or vanilla's name for: "
            + string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryPickerKingdom_HasItsInGameValueNameInModuleStrings()
    {
        // The in-game Escape-menu summary reads str_advanced_start_value_name through
        // GameTexts.FindText, the per-Game manager, which merges every GameText XML by id. That
        // family belongs in the GameText XML, not in global_strings.xml.
        var ids = StringIds("taom_module_strings.xml");
        var missing = TaomStartOptionsProvider.TaomKingdomIds.Concat(RenamedVanillaKingdomIds)
            .Select(k => "str_advanced_start_value_name." + k)
            .Where(id => !ids.Contains(id))
            .ToList();
        Assert.AreEqual(0, missing.Count,
            "taom_module_strings.xml lacks in-game value-name rows for: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void NoMenuFacingAsoRow_LivesInTheGameTextXml()
    {
        // A str_campaign_starting_options_* row in taom_module_strings.xml is dead: the only readers
        // of that family are the main-menu classes, and they never see a GameText XML. This is the
        // placement mistake #604 shipped; the row looks right, tests that scan the file find it, and
        // the menu never does.
        var stray = StringIds("taom_module_strings.xml")
            .Where(id => id.StartsWith("str_campaign_starting_options", StringComparison.Ordinal))
            .ToList();
        Assert.AreEqual(0, stray.Count,
            "Move these to global_strings.xml, the only file the main-menu GlobalTextManager reads: "
            + string.Join(", ", stray));
    }

    private static void InvokeProvider(Type providerType, Type attribute, object options)
    {
        var provider = providerType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.GetCustomAttributes(attribute, false).Length > 0);
        provider.Invoke(null, new[] { options });
    }

    // GetItems() yields (string Identifier, ListItemCondition Condition) value tuples.
    private static List<string> ItemIds(object listOption)
    {
        var getItems = listOption.GetType().GetMethod("GetItems", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(getItems, listOption.GetType().Name + ".GetItems() did not resolve.");
        var ids = new List<string>();
        foreach (var item in (System.Collections.IEnumerable)getItems!.Invoke(listOption, null))
            ids.Add((string)item.GetType().GetField("Item1")!.GetValue(item));
        return ids;
    }
}
