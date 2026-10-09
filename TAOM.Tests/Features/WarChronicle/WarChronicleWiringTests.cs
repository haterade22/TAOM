using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarChronicle;

/// <summary>
/// The War Chronicle reset lives in <c>WarChronicleBehavior</c>'s constructor (the session-reset rule in
/// csharp-architecture.md), so it holds only while a fresh behavior is built for every campaign. The
/// module's <c>CampaignBehaviorDecl.Of</c> factory runs once per campaign: <c>FeatureModuleHooks.AddGameStartContent</c>
/// calls <c>decl.Create(resolver)</c> inside <c>RunCampaignStart</c>, from <c>SubModule.OnGameStart</c>,
/// which precedes <c>LoadBehaviorData</c>. The pinned shape is the behavior built INLINE as the lambda
/// body, exactly once; a cached field, a <c>??=</c> or a container-resolved singleton would run the
/// constructor once per process and bring back the stale-campaign bug with every behavior test green.
/// Same method as <c>WarOfTheRingWiringTests</c>. These read source text only.
/// </summary>
[TestClass]
public class WarChronicleWiringTests
{
    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*\w+\s*=>\s*new\s+WarChronicleBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+WarChronicleBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheDeclarationFactory()
    {
        var src = RepoPaths.ReadSource("Main/Features/WarChronicle/WarChronicleModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "WarChronicleModule.cs must build WarChronicleBehavior inline as CampaignBehaviorDecl.Of's lambda body, "
            + "exactly once: its constructor is the per-campaign reset of the War Chronicle singletons.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileNamesTheBehavior()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsFile(path, "WarChronicleModule.cs") && !IsFile(path, "WarChronicleBehavior.cs"))
            .Where(path => Regex.IsMatch(RepoPaths.StripComments(File.ReadAllText(path)), @"\bWarChronicleBehavior\b"))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only the module may construct WarChronicleBehavior; a registration or construction elsewhere could hand "
            + "the campaign a shared instance: " + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new WarChronicleBehavior(a, b));")]
    [DataRow("private static readonly WarChronicleBehavior Shared = new WarChronicleBehavior(a, b); CampaignBehaviorDecl.Of(r => Shared);")]
    [DataRow("CampaignBehaviorDecl.Of(r => new WarChronicleBehavior(a, b)); var extra = new WarChronicleBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<WarChronicleBehavior>());")]
    [DataRow("CampaignBehaviorDecl.Of(r => Cached); var x = new WarChronicleBehavior(a, b);")]
    [DataRow("container.Register<WarChronicleBehavior>(Reuse.Singleton);")]
    public void Predicate_RejectsACachedSharedOrContainerResolvedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign(
            "CampaignBehaviorDecl.Of(r => new WarChronicleBehavior(\n    r.Resolve<A>(),\n    r.Resolve<B>()))"));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModuleIsRegisteredAndOwnsSaveData()
    {
        var module = FeatureModules.All.SingleOrDefault(m => m.Id == "WarChronicle");

        Assert.IsNotNull(module, "WarChronicleModule must be listed in FeatureModules.All.");
        Assert.IsTrue(module!.OwnsSaveData, "the behavior persists data in SyncData, so the module fails closed");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.IsNull(module.ParkedReason);
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
