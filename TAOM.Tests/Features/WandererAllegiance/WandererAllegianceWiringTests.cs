using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>
/// Wiring regression guard. The feature has no Harmony patch and no GameModel; it hangs off three
/// lines that no behavioural test can see: the IoC registration, the <c>AddBehavior</c> call in
/// <c>SubModule.cs</c>, and the two dialogue lines being registered on vanilla's
/// <c>companion_hire</c> token ABOVE vanilla's priority 100. Drop any of them and every wanderer
/// hires as in vanilla with no error and no log line (the "registered in IoC, invoked by nothing"
/// class, <c>HeroRaceWiringTests</c>).
/// </summary>
[TestClass]
public class WandererAllegianceWiringTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"Expected source file not found: {path}");
        return File.ReadAllText(path);
    }

    private static string BehaviorSource() =>
        ReadSource("Main", "Features", "WandererAllegiance", "Hooks", "WandererAllegianceDialogBehavior.cs");

    [TestMethod]
    public void IoC_RegistersTheFeature()
    {
        var src = ReadSource("Main", "IoC.cs");

        StringAssert.Contains(src, "WandererAllegianceIoC.RegisterWandererAllegianceFeature(container)",
            "Main/IoC.cs no longer registers WandererAllegiance; SubModule's Resolve would throw at campaign start.");
    }

    [TestMethod]
    public void WandererAllegianceIoC_RegistersEveryConsumerOfTheBehavior()
    {
        var src = ReadSource("Main", "Features", "WandererAllegiance", "WandererAllegianceIoC.cs");

        StringAssert.Contains(src, "IWandererAllegianceConfigProvider, WandererAllegianceConfigProvider");
        StringAssert.Contains(src, "IWandererAllegianceSettingsProvider, WandererAllegianceSettingsProvider");
        StringAssert.Contains(src, "IWandererAllegianceService, WandererAllegianceService");
        StringAssert.Contains(src, "Hooks.WandererAllegianceDialogBehavior");
    }

    [TestMethod]
    public void SubModule_AddsTheDialogBehavior()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, "WandererAllegianceDialogBehavior>()",
            "SubModule.cs no longer adds WandererAllegianceDialogBehavior; the refusal lines are never registered.");
        StringAssert.Contains(src, "campaignStarter.AddBehavior(IoC.Resolve<Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior>())",
            "The behavior must be resolved from IoC (it needs the service) and added via AddBehavior.");
    }

    [TestMethod]
    public void DialogBehavior_RegistersBothRefusalsOnCompanionHire_ToLordPretalk()
    {
        var src = BehaviorSource();

        foreach (var id in new[] { "taom_wa_refuse_free", "taom_wa_refuse_evil" })
        {
            var line = Regex.Match(src,
                "AddDialogLine\\(\\s*\"" + id + "\"\\s*,\\s*\"(?<input>[^\"]+)\"\\s*,\\s*\"(?<output>[^\"]+)\"",
                RegexOptions.Singleline);

            Assert.IsTrue(line.Success, $"{id} is no longer registered with AddDialogLine.");
            Assert.AreEqual("companion_hire", line.Groups["input"].Value,
                $"{id} must sit on vanilla's companion_hire token, the only input the hire reply is selected on.");
            Assert.AreEqual("lord_pretalk", line.Groups["output"].Value,
                $"{id} must return to lord_pretalk, vanilla's own 'no deal' exit (hero_pretalk_2 loops back to hero_main_options).");
        }
    }

    [TestMethod]
    public void DialogBehavior_PriorityIsAboveVanillasHundred()
    {
        // ConversationManager.GetSentenceOptions returns the first NPC line whose condition is true,
        // walking the list sorted priority-descending. Vanilla's companion_hire reply sits at the
        // default 100; at 100 or below ours would never be reached.
        var src = BehaviorSource();
        var match = Regex.Match(src, "private const int Priority = (?<p>\\d+);");

        Assert.IsTrue(match.Success, "The behavior no longer declares a Priority constant.");
        Assert.IsTrue(int.Parse(match.Groups["p"].Value) > 100,
            $"Priority must exceed vanilla's 100, found {match.Groups["p"].Value}.");
        Assert.AreEqual(2, Regex.Matches(src, "null,\\s*Priority\\)").Count,
            "Both refusal lines must pass the Priority constant to AddDialogLine.");
    }

    [TestMethod]
    public void RefusalStrings_AreRegisteredForTranslation()
    {
        var strings = ReadSource("Main", "_Module", "ModuleData", "taom_module_strings.xml");

        StringAssert.Contains(strings, "id=\"taom_wa_refuse_free\"");
        StringAssert.Contains(strings, "id=\"taom_wa_refuse_evil\"");
    }
}
