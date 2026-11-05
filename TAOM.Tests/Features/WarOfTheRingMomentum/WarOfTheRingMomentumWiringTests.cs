using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// #773: the momentum reset lives in <c>WarOfTheRingMomentumBehavior</c>'s constructor, so it holds only
/// while SubModule builds a fresh behavior for every campaign, inline as <c>AddBehavior</c>'s argument. A
/// cached or container-resolved instance would run the constructor once per process and bring #773 back
/// with every behavior test still green. These read source text only, so they carry no RequiresGame tag.
/// </summary>
[TestClass]
public class WarOfTheRingMomentumWiringTests
{
    private static readonly Regex InlinePerCampaign = new Regex(
        @"campaignStarter\.AddBehavior\(\s*new\s+(?:Features\.WarOfTheRingMomentum\.)?WarOfTheRingMomentumBehavior\(");
    private static readonly Regex AnyConstruction = new Regex(@"new\s+(?:[\w.]+\.)?WarOfTheRingMomentumBehavior\(");
    private static readonly Regex ContainerUse = new Regex(@"(?:Register|Resolve)\s*<\s*(?:[\w.]+\.)?WarOfTheRingMomentumBehavior\s*>");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1
        && !ContainerUse.IsMatch(source);

    [TestMethod]
    public void SubModule_BuildsTheMomentumBehaviorInlineAsTheAddBehaviorArgument()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "SubModule.cs must build WarOfTheRingMomentumBehavior inline as campaignStarter.AddBehavior's argument, "
            + "exactly once: its constructor is the #773 per-campaign reset of the momentum store.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsRegistersOrResolvesTheBehavior()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsFile(path, "SubModule.cs") && !IsFile(path, "WarOfTheRingMomentumBehavior.cs"))
            .Where(path =>
            {
                var source = RepoPaths.StripComments(File.ReadAllText(path));
                return AnyConstruction.IsMatch(source) || ContainerUse.IsMatch(source);
            })
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only SubModule.cs may construct WarOfTheRingMomentumBehavior (#773); a registration or construction "
            + "elsewhere could hand the campaign a shared instance: " + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());")]
    [DataRow("campaignStarter.AddBehavior(_cached ??= new WarOfTheRingMomentumBehavior(a));")]
    [DataRow("container.Register<WarOfTheRingMomentumBehavior>(Reuse.Singleton); campaignStarter.AddBehavior(new WarOfTheRingMomentumBehavior(a));")]
    public void Predicate_RejectsACachedOrContainerShape(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign(
            "campaignStarter.AddBehavior(new Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior(\n    a,\n    b));"));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
