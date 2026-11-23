using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// #773: these behaviors reset a process-lifetime store in their constructors, so the reset holds
/// only while SubModule builds a fresh behavior for every campaign. The pinned shape is the behavior
/// built inline as the <c>campaignStarter.AddBehavior</c> argument, exactly once, and no other file
/// constructing, registering or resolving it. A cached field, a static or a container singleton
/// would run the constructor once per process with every behavior test still green. Same pin as
/// <c>WarOfTheRingWiringTests</c> (#764), which covers WarOfTheRingBehavior. Source text only, so
/// these carry no RequiresGame tag.
/// </summary>
[TestClass]
public class PerCampaignBehaviorWiringTests
{
    private static Regex InlinePerCampaign(string type) =>
        new(@"campaignStarter\.AddBehavior\(\s*new\s+(?:\w+\.)*" + type + @"\(");

    private static Regex AnyConstruction(string type) => new(@"new\s+(?:\w+\.)*" + type + @"\(");

    private static Regex RegisteredOrResolved(string type) =>
        new(@"(?:Register|Resolve)\s*<\s*(?:\w+\.)*" + type + @"\s*>");

    private static bool IsBuiltInlinePerCampaign(string source, string type) =>
        InlinePerCampaign(type).Matches(source).Count == 1
        && AnyConstruction(type).Matches(source).Count == 1
        && !RegisteredOrResolved(type).IsMatch(source);

    [DataTestMethod]
    [DataRow("WarOfTheRingMomentumBehavior")]
    [DataRow("FiefGrantingCampaignBehavior")]
    [DataRow("SupplyLinesCampaignBehavior")]
    [DataRow("FieldCampCampaignBehavior")]
    [DataRow("RefugeCampaignBehavior")]
    public void SubModule_BuildsTheBehaviorInlineAsTheAddBehaviorArgument(string type)
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src, type),
            $"SubModule.cs must build {type} inline as campaignStarter.AddBehavior's argument, exactly once: "
            + "its constructor is the per-campaign reset of its store (#773).");
    }

    [DataTestMethod]
    [DataRow("WarOfTheRingMomentumBehavior")]
    [DataRow("FiefGrantingCampaignBehavior")]
    [DataRow("SupplyLinesCampaignBehavior")]
    [DataRow("FieldCampCampaignBehavior")]
    [DataRow("RefugeCampaignBehavior")]
    public void ProductionSource_NoOtherFileConstructsRegistersOrResolvesTheBehavior(string type)
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !IsFile(path, "SubModule.cs") && !IsFile(path, type + ".cs"))
            .Where(path =>
            {
                var src = RepoPaths.StripComments(RepoPaths.Text(path));
                return AnyConstruction(type).IsMatch(src) || RegisteredOrResolved(type).IsMatch(src);
            })
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            $"Only SubModule.cs may construct {type} (#773); a registration or construction elsewhere could "
            + "hand the campaign a shared instance: " + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("campaignStarter.AddBehavior(_cached ??= new WarOfTheRingMomentumBehavior(a, b));")]
    [DataRow("private static readonly WarOfTheRingMomentumBehavior M = new WarOfTheRingMomentumBehavior(a); campaignStarter.AddBehavior(M);")]
    [DataRow("campaignStarter.AddBehavior(new WarOfTheRingMomentumBehavior(a)); var x = new WarOfTheRingMomentumBehavior(a);")]
    [DataRow("campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());")]
    [DataRow("container.Register<WarOfTheRingMomentumBehavior>(Reuse.Singleton); campaignStarter.AddBehavior(new WarOfTheRingMomentumBehavior(a));")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant, "WarOfTheRingMomentumBehavior"), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsAQualifiedInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign(
            "campaignStarter.AddBehavior(new Features.Refuge.Hooks.RefugeCampaignBehavior(a,\n    b));",
            "RefugeCampaignBehavior"));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
