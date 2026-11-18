using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedStartOptions;
using TAOM.Features.CampaignStartOptions;
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

    private static readonly StartDefaults WarOn = new StartDefaults(true, 30, 44, true);
    private static readonly StartDefaults WarOff = new StartDefaults(false, 30, 44, true);

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void MergedOptions_VanillaThenTaom_CarryEveryTaomKingdom()
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(WarOn);

        foreach (var key in new[] { "KingdomId" })
        {
            var picker = GetOptionMethod(optionsType).Invoke(options, new object[] { key });
            Assert.IsNotNull(picker, key + " is no longer an option vanilla registers; the picker keys drifted.");
            var ids = ItemIds(picker!);
            var missing = TaomStartOptionsProvider.TaomKingdomIds.Where(id => !ids.Contains(id)).ToList();
            Assert.AreEqual(0, missing.Count, key + " lacks the TAOM kingdoms: " + string.Join(", ", missing));
        }
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Scenario_IsWarOfTheRingOrSandboxOnly_WarOfTheRingFirst(bool warOn)
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(warOn ? WarOn : WarOff);

        var scenario = Option(optionsType, options, "Scenario");

        CollectionAssert.AreEqual(
            new[] { CampaignStartOptionKeys.WarOfTheRingScenarioItem, CampaignStartOptionKeys.SandboxScenarioItem },
            ItemIds(scenario),
            "The scenario list is War of the Ring, then Sandbox: the vanilla war scenarios and United Empire stay hidden.");
    }

    [DataTestMethod]
    [DataRow(true, CampaignStartOptionKeys.WarOfTheRingScenarioItem)]
    [DataRow(false, CampaignStartOptionKeys.SandboxScenarioItem)]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Scenario_DefaultsToWhateverTheMcmOrJsonSwitchSays(bool warOn, string expected)
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(warOn ? WarOn : WarOff);

        var scenario = Option(optionsType, options, "Scenario");

        Assert.AreEqual(expected, GetValue<string>(scenario));
        Assert.IsFalse(HasValueChanged(scenario), "the default is not a change, so GetChangedOptions leaves it out");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void HiddenScenarioIds_AreAllVanillaScenarios()
    {
        RequireGame();
        var optionsType = AccessTools.TypeByName("SandBox.AdvancedStartOptions.AdvancedStartOptions")!;
        var vanillaProvider = AccessTools.TypeByName("SandBox.View.AdvancedStartOptions.SandBoxStartOptionsProvider");
        var attribute = AccessTools.TypeByName("SandBox.AdvancedStartOptions.StartOptionsProviderAttribute");
        Assert.IsNotNull(vanillaProvider, "SandBox.View.AdvancedStartOptions.SandBoxStartOptionsProvider did not resolve; the vanilla provider moved.");
        var options = Activator.CreateInstance(optionsType)!;
        InvokeProvider(vanillaProvider!, attribute!, options);

        var scenarios = ItemIds(Option(optionsType, options, "Scenario"));

        foreach (var id in CampaignStartOptionsScreen.HiddenScenarioIds)
        {
            // Added by NavalDLC's provider, which this test does not run; its id was read from that DLL.
            if (id == "nordinvasion") continue;
            CollectionAssert.Contains(scenarios, id, id + " is not a vanilla scenario id; the list drifted.");
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Scenario_KeepsAnotherModsItem_AndDropsOnlyTheHiddenOnes()
    {
        RequireGame();
        var optionsType = AccessTools.TypeByName("SandBox.AdvancedStartOptions.AdvancedStartOptions")!;
        var attribute = AccessTools.TypeByName("SandBox.AdvancedStartOptions.StartOptionsProviderAttribute")!;
        var options = Activator.CreateInstance(optionsType)!;
        InvokeProvider(AccessTools.TypeByName("SandBox.View.AdvancedStartOptions.SandBoxStartOptionsProvider")!, attribute, options);
        var scenario = Option(optionsType, options, "Scenario");
        // Another mod's provider adds a scenario of its own with AddItem.
        var itemType = scenario.GetType().GetMethod("GetItems")!.ReturnType.GetGenericArguments()[0];
        var item = Activator.CreateInstance(itemType, "othermod_scenario", null)!;
        scenario.GetType().GetMethod("AddItem")!.Invoke(scenario, new[] { item });

        WithDefaults(WarOn, () => InvokeProvider(typeof(TaomStartOptionsProvider), attribute, options));

        CollectionAssert.AreEqual(
            new[] { "taom_war_of_the_ring", "none", "othermod_scenario" },
            ItemIds(Option(optionsType, options, "Scenario")));
    }

    // The option conditions answer "is this option HIDDEN", so true hides: one test per polarity.
    [DataTestMethod]
    [DataRow(CampaignStartOptionKeys.WarOfTheRingScenarioItem, false)]
    [DataRow(CampaignStartOptionKeys.SandboxScenarioItem, true)]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void StartDate_IsShownOnlyForTheWarOfTheRing(string scenarioValue, bool hidden)
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(WarOn);
        SetValue(Option(optionsType, options, "Scenario"), scenarioValue);

        var startDate = Option(optionsType, options, CampaignStartOptionKeys.StartDateOption);

        Assert.AreEqual(hidden, IsHidden(startDate, options));
    }

    [DataTestMethod]
    [DataRow(CampaignStartOptionKeys.WarOfTheRingScenarioItem, CampaignStartOptionKeys.StartCustomItem, false)]
    [DataRow(CampaignStartOptionKeys.WarOfTheRingScenarioItem, CampaignStartOptionKeys.StartLateItem, true)]
    [DataRow(CampaignStartOptionKeys.WarOfTheRingScenarioItem, CampaignStartOptionKeys.StartStandardItem, true)]
    [DataRow(CampaignStartOptionKeys.SandboxScenarioItem, CampaignStartOptionKeys.StartCustomItem, true)]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void CustomSliders_AreShownOnlyForWarOfTheRingWithTheCustomStart(string scenarioValue, string startValue, bool hidden)
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(WarOn);
        SetValue(Option(optionsType, options, "Scenario"), scenarioValue);
        SetValue(Option(optionsType, options, CampaignStartOptionKeys.StartDateOption), startValue);

        foreach (var key in new[] { CampaignStartOptionKeys.CustomDay1Option, CampaignStartOptionKeys.CustomDay2Option })
            Assert.AreEqual(hidden, IsHidden(Option(optionsType, options, key), options), key);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void StartDateItems_AreAllSelectable_StandardFirstChoice()
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(WarOn);
        var startDate = Option(optionsType, options, CampaignStartOptionKeys.StartDateOption);

        CollectionAssert.AreEqual(
            new[] { CampaignStartOptionKeys.StartEarlyItem, CampaignStartOptionKeys.StartStandardItem, CampaignStartOptionKeys.StartLateItem, CampaignStartOptionKeys.StartCustomItem },
            ItemIds(startDate));
        Assert.AreEqual(CampaignStartOptionKeys.StartStandardItem, GetValue<string>(startDate));

        // Item conditions answer "is this item DISABLED": none of ours may be.
        var getCondition = startDate.GetType().GetMethod("GetItemCondition")!;
        foreach (var id in ItemIds(startDate))
        {
            var args = new object?[] { id, options, null };
            Assert.IsFalse((bool)getCondition.Invoke(startDate, args)!, id + " is disabled");
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void CustomSliders_StartAtTheDefaultDays_InsideTheirRange()
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(new StartDefaults(true, 25, 50, true));

        var day1 = Option(optionsType, options, CampaignStartOptionKeys.CustomDay1Option);
        var day2 = Option(optionsType, options, CampaignStartOptionKeys.CustomDay2Option);

        Assert.AreEqual(25, GetValue<int>(day1));
        Assert.AreEqual(50, GetValue<int>(day2));
        Assert.AreEqual(1, (int)day1.GetType().GetField("MinValue")!.GetValue(day1)!);
        Assert.AreEqual(365, (int)day2.GetType().GetField("MaxValue")!.GetValue(day2)!);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void RulesToggles_DefaultToTheirMcmValues_InTheirOwnCategory(bool weight)
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(new StartDefaults(true, 30, 44, weight));

        var troopWeight = Option(optionsType, options, CampaignStartOptionKeys.TroopWeightOption);

        Assert.AreEqual(weight, GetValue<bool>(troopWeight));
        Assert.AreEqual(CampaignStartOptionKeys.RulesCategory, (string)troopWeight.GetType().GetProperty("CategoryId")!.GetValue(troopWeight)!);
    }

    // GetChangedOptions keeps only options that differ from their default and are not hidden; the pin
    // depends on exactly that, so the end-to-end read goes through the engine's own method.
    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ChangedOptions_HoldTheChoices_AndNotTheHiddenOnes()
    {
        RequireGame();
        var (optionsType, options) = BuildMergedOptions(WarOn);
        SetValue(Option(optionsType, options, CampaignStartOptionKeys.StartDateOption), CampaignStartOptionKeys.StartCustomItem);
        SetValue(Option(optionsType, options, CampaignStartOptionKeys.CustomDay1Option), 100);
        SetValue(Option(optionsType, options, CampaignStartOptionKeys.TroopWeightOption), false);
        // The player then switches to Sandbox: the start date and the slider are hidden again and drop out.
        SetValue(Option(optionsType, options, "Scenario"), CampaignStartOptionKeys.SandboxScenarioItem);

        var changed = optionsType.GetMethod("GetChangedOptions")!.Invoke(options, null)!;
        var getOption = changed.GetType().GetMethod("GetOption")!;
        bool Has(string key) => getOption.Invoke(changed, new object[] { key }) != null;

        Assert.IsTrue(Has("Scenario"));
        Assert.IsTrue(Has(CampaignStartOptionKeys.TroopWeightOption));
        Assert.IsFalse(Has(CampaignStartOptionKeys.StartDateOption), "hidden with Sandbox");
        Assert.IsFalse(Has(CampaignStartOptionKeys.CustomDay1Option), "hidden with Sandbox");
        Assert.IsFalse(Has(CampaignStartOptionKeys.WarCanBeWonOption), "left at its default");
    }

    private static object Option(Type optionsType, object options, string key) =>
        GetOptionMethod(optionsType).Invoke(options, new object[] { key })
        ?? throw new AssertFailedException(key + " is not an option of the merged set.");

    private static T GetValue<T>(object option) =>
        (T)option.GetType().GetMethod("GetValue")!.MakeGenericMethod(typeof(T)).Invoke(option, null)!;

    private static void SetValue<T>(object option, T value) =>
        option.GetType().GetMethod("SetValue")!.MakeGenericMethod(typeof(T)).Invoke(option, new object?[] { value });

    private static bool HasValueChanged(object option) =>
        (bool)option.GetType().GetMethod("HasValueChanged")!.Invoke(option, null)!;

    private static bool IsHidden(object option, object options) =>
        (bool)option.GetType().GetMethod("GetIsHidden")!.Invoke(option, new[] { options })!;

    private static MethodInfo GetOptionMethod(Type optionsType) => optionsType.GetMethods()
        .Single(m => m.Name == "GetOption" && !m.IsGenericMethod && m.GetParameters().Length == 1);

    private static void WithDefaults(StartDefaults defaults, Action run)
    {
        var previous = TaomStartOptionsProvider.Defaults;
        TaomStartOptionsProvider.Defaults = () => defaults;
        try { run(); }
        finally { TaomStartOptionsProvider.Defaults = previous; }
    }

    // Runs vanilla's provider, then TAOM's, against one AdvancedStartOptions, with the defaults pinned
    // for the call (the engine runs every provider in assembly load order; SandBox loads before TAOM).
    private static (Type OptionsType, object Options) BuildMergedOptions(StartDefaults defaults)
    {
        var optionsType = AccessTools.TypeByName("SandBox.AdvancedStartOptions.AdvancedStartOptions");
        var vanillaProvider = AccessTools.TypeByName("SandBox.View.AdvancedStartOptions.SandBoxStartOptionsProvider");
        var attribute = AccessTools.TypeByName("SandBox.AdvancedStartOptions.StartOptionsProviderAttribute");
        Assert.IsNotNull(optionsType, "SandBox.AdvancedStartOptions.AdvancedStartOptions did not resolve.");
        Assert.IsNotNull(vanillaProvider, "SandBox.View.AdvancedStartOptions.SandBoxStartOptionsProvider did not resolve; the vanilla provider moved.");
        Assert.IsNotNull(attribute, "StartOptionsProviderAttribute did not resolve.");

        var options = Activator.CreateInstance(optionsType!)!;
        InvokeProvider(vanillaProvider!, attribute!, options);
        WithDefaults(defaults, () => InvokeProvider(typeof(TaomStartOptionsProvider), attribute!, options));
        return (optionsType!, options);
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

    private static readonly string[] CampaignOptionIds =
    {
        CampaignStartOptionKeys.StartDateOption, CampaignStartOptionKeys.CustomDay1Option,
        CampaignStartOptionKeys.CustomDay2Option, CampaignStartOptionKeys.TroopWeightOption,
        CampaignStartOptionKeys.WarCanBeWonOption, CampaignStartOptionKeys.EyeHuntersOption,
        CampaignStartOptionKeys.LargeAiArmiesOption, CampaignStartOptionKeys.AlignmentPresetOption,
    };

    private static readonly string[] CampaignListItemIds =
    {
        CampaignStartOptionKeys.WarOfTheRingScenarioItem, CampaignStartOptionKeys.SandboxScenarioItem,
        CampaignStartOptionKeys.StartEarlyItem, CampaignStartOptionKeys.StartStandardItem,
        CampaignStartOptionKeys.StartLateItem, CampaignStartOptionKeys.StartCustomItem,
        CampaignStartOptionKeys.AlignmentStandardItem, CampaignStartOptionKeys.AlignmentStrictItem,
        CampaignStartOptionKeys.AlignmentRelaxedItem,
    };

    [TestMethod]
    public void EveryCampaignStartOption_HasItsMenuRowsInGlobalStrings()
    {
        // The same #604 rule for the per-campaign options: the screen reads GlobalTextManager, so every
        // name, description, item name, item description and the category name sits in global_strings.xml.
        var ids = StringIds("global_strings.xml");
        var expected = CampaignOptionIds.SelectMany(o => new[]
            {
                "str_campaign_starting_options_name." + o,
                "str_campaign_starting_options_description." + o,
            })
            .Concat(CampaignListItemIds.SelectMany(i => new[]
            {
                "str_campaign_starting_options_item_name." + i,
                "str_campaign_starting_options_item_description." + i,
            }))
            .Append("str_campaign_starting_options_category_name." + CampaignStartOptionKeys.RulesCategory)
            .ToList();
        var missing = expected.Where(id => !ids.Contains(id)).ToList();
        Assert.AreEqual(0, missing.Count, "global_strings.xml lacks the campaign start option rows: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void TheScenarioValues_HaveTheirInGameNamesInModuleStrings()
    {
        // The Escape-menu summary reads str_advanced_start_value_name through GameTexts.FindText, and only for the
        // Scenario list and the six vanilla scenario parameters, so the start-date and alignment items need no
        // row. Vanilla's "none" row says "Default"; the same-id row here says Sandbox.
        var ids = StringIds("taom_module_strings.xml");
        var missing = new[] { CampaignStartOptionKeys.WarOfTheRingScenarioItem, CampaignStartOptionKeys.SandboxScenarioItem }
            .Select(i => "str_advanced_start_value_name." + i)
            .Where(id => !ids.Contains(id))
            .ToList();
        Assert.AreEqual(0, missing.Count, "taom_module_strings.xml lacks in-game value names for: " + string.Join(", ", missing));
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
