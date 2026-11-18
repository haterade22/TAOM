using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8 wiring. The hunt's reset lives in <c>EyeOfSauronBehavior</c>'s constructor, so it holds only while the
/// module builds a fresh behavior inside the declaration's factory, which the runner calls once per campaign start. A
/// cached or shared instance (a <c>??=</c> field, a static, a container resolve) would run the constructor once per
/// process and carry one campaign's hunt into the next with every behavior test green, so the pin is the exact shape
/// (inline <c>new</c> as the factory's body, exactly once) and the cheapest regressions are rows it must reject
/// (lessons/testing-qa.md, "Mutation-check a source-text pin"). The two models' seams are pinned here too.
/// </summary>
[TestClass]
public class EyeOfSauronWiringTests
{
    private const string ModulePath = "Main/Features/EyeOfSauron/EyeOfSauronModule.cs";

    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+EyeOfSauronBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+EyeOfSauronBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<EyeOfSauronModule>().Count(),
            "EyeOfSauronModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void Module_OwnsSaveData_SoAFaultFailsClosed() => Assert.IsTrue(new EyeOfSauronModule().OwnsSaveData);

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("RegisterEyeOfSauronFeature"),
            "Main/IoC.cs registers the feature by hand AND through its module: every service gets a second default registration");
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "EyeOfSauronModule must build EyeOfSauronBehavior with new as the CampaignBehaviorDecl factory's body, exactly "
            + "once: its constructor is the per-campaign reset of the hunt service.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsOrRegistersTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !IsFile(path, "EyeOfSauronModule.cs") && !IsFile(path, "EyeOfSauronBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "EyeOfSauronBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only EyeOfSauronModule may construct EyeOfSauronBehavior; elsewhere could hand the campaign a shared instance: "
            + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new EyeOfSauronBehavior(a, b)),")]
    [DataRow("private static readonly EyeOfSauronBehavior Shared = new EyeOfSauronBehavior(a, b); CampaignBehaviorDecl.Of(r => Shared),")]
    [DataRow("CampaignBehaviorDecl.Of(r => Make()), static EyeOfSauronBehavior Make() => _c ??= new EyeOfSauronBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => new EyeOfSauronBehavior(a, b)), var x = new EyeOfSauronBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<EyeOfSauronBehavior>()),")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign("CampaignBehaviorDecl.Of(r =>\n    new EyeOfSauronBehavior(\n a, b)),"));
    }

    [TestMethod]
    public void SubModule_HandsOneResolvedHookToBothModels()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(src, "var eyeOfSauron = IoC.Resolve<TAOM.Features.EyeOfSauron.IEyeOfSauronHook>();");
        StringAssert.Contains(src, "new TaomPartySpeedModel(culturalFeats, careerPassives, eyeOfSauron)");
        Assert.IsTrue(Regex.IsMatch(src, @"new TaomPartySizeModel\([^;]*,\s*eyeOfSauron\)\);"),
            "TaomPartySizeModel must receive the hook as its last argument");
    }

    [TestMethod]
    public void SpeedModel_AppliesTheHunterFloorLast_AfterTheCareerLine()
    {
        var src = RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs", stripComments: true);
        int method = src.IndexOf("CalculateFinalSpeed(", StringComparison.Ordinal);
        int career = src.IndexOf("_careerPassives.ApplyFactor(", method, StringComparison.Ordinal);
        int floor = src.IndexOf("_eyeOfSauron.ApplyHunterSpeedFloor(mobileParty.StringId, ref result);", method, StringComparison.Ordinal);
        int ret = src.IndexOf("return result;", method, StringComparison.Ordinal);

        Assert.IsTrue(career > method, "the career speed line is gone");
        Assert.IsTrue(floor > career, "the hunter's floor must come after the career line, so it is compared against every other line");
        Assert.IsTrue(ret > floor, "the floor must be applied to the result the model returns");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<TAOM.Features.CampaignStartOptions.ICampaignStartOptions>());
        var module = new EyeOfSauronModule();

        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single();
        Assert.AreEqual(typeof(EyeOfSauronBehavior), decl.BehaviorType);
        Assert.IsInstanceOfType(decl.Create(container), typeof(EyeOfSauronBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
        Assert.AreSame(container.Resolve<EyeOfSauronService>(), container.Resolve<IEyeOfSauronHook>(),
            "the models must read the same service the behavior drives");
        Assert.IsInstanceOfType(container.Resolve<IHunterPartyAdapter>(), typeof(HunterPartyAdapter));
        Assert.IsInstanceOfType(container.Resolve<IEyeOfSauronSettings>(), typeof(EyeOfSauronSettingsProvider));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
