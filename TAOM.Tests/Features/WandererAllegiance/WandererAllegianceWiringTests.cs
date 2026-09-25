using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.NamedCompanions;
using TAOM.Features.WandererAllegiance;
using TAOM.Features.WandererAllegiance.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>
/// Wiring regression guard. The feature has no Harmony patch and no GameModel; it hangs off its
/// feature module (listed once in <c>FeatureModules.All</c>; its service graph resolves the dialog
/// behavior the runner adds at campaign start) and the two dialogue lines registered on vanilla's
/// <c>companion_hire</c> token ABOVE vanilla's priority 100. Drop the list entry or either line and
/// every wanderer hires as in vanilla with no error and no log line (the "registered in IoC, invoked
/// by nothing" class, <c>HeroRaceWiringTests</c>).
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
    public void FeatureModules_ListTheWandererAllegianceModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<WandererAllegianceModule>().Count(),
            "WandererAllegianceModule must be listed exactly once in Main/Composition/FeatureModules.cs, or the "
            + "refusal lines are never registered (or registered twice).");
    }

    [TestMethod]
    public void IoC_NoLongerRegistersTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("RegisterWandererAllegianceFeature"),
            "Main/IoC.cs registers WandererAllegiance by hand AND through its module: every service gets a second "
            + "default registration and Resolve throws at campaign start.");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndItsBehaviorDeclResolvesTheSingleton()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-plan018-" + Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<INamedCompanionConfigProvider>());
        var module = new WandererAllegianceModule();

        module.RegisterServices(container);

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        var decl = module.CampaignBehaviors[0];
        Assert.AreEqual(typeof(WandererAllegianceDialogBehavior), decl.BehaviorType);
        var behavior = decl.Create(container);
        Assert.IsInstanceOfType(behavior, typeof(WandererAllegianceDialogBehavior));
        Assert.AreSame(behavior, decl.Create(container),
            "Parity: the behavior stays a container singleton, as it was when SubModule resolved it.");
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
