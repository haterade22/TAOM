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
