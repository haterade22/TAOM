using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// #764: the War of the Ring reset lives in <c>WarOfTheRingBehavior</c>'s constructor, so it holds
/// only while SubModule builds a fresh behavior for every campaign. <c>AddBehavior</c> on the
/// <c>CampaignGameStarter</c> is reached once per campaign from OnGameStart, so the pinned shape is
/// the behavior built INLINE as that call's argument, exactly once. A cached or shared instance (a
/// <c>??=</c> field, a static, a container singleton like the momentum behavior beside it) would run
/// the constructor once per process and bring #764 back with every behavior test still green. The
/// Codex review of 2026-10-08 showed the first, contains-only version of this pin passing a cached
/// mutant, which the negative rows below now reject. These read source text only, so they carry no
/// RequiresGame tag and also run in the reference-assembly build.
/// </summary>
[TestClass]
public class WarOfTheRingWiringTests
{
    private static readonly Regex InlinePerCampaign =
        new Regex(@"campaignStarter\.AddBehavior\(\s*new\s+WarOfTheRingBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+WarOfTheRingBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void SubModule_BuildsTheBehaviorInlineAsTheAddBehaviorArgument()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "SubModule.cs must build WarOfTheRingBehavior inline as campaignStarter.AddBehavior's argument, "
            + "exactly once: its constructor is the #764 per-campaign reset of the War of the Ring service.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsOrRegistersTheBehavior()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsFile(path, "SubModule.cs") && !IsFile(path, "WarOfTheRingBehavior.cs"))
            .Where(path => Regex.IsMatch(RepoPaths.StripComments(File.ReadAllText(path)), @"\bWarOfTheRingBehavior\b"))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only SubModule.cs may construct WarOfTheRingBehavior (#764); a registration or construction "
            + "elsewhere could hand the campaign a shared instance: " + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("campaignStarter.AddBehavior(_cachedWotr ??= new WarOfTheRingBehavior(a, b, c));")]
    [DataRow("private static readonly WarOfTheRingBehavior Wotr = new WarOfTheRingBehavior(a, b, c); campaignStarter.AddBehavior(Wotr);")]
    [DataRow("campaignStarter.AddBehavior(Shared); campaignStarter.AddBehavior(new WarOfTheRingBehavior(a, b, c)); var x = new WarOfTheRingBehavior(a, b, c);")]
    [DataRow("campaignStarter.AddBehavior(IoC.Resolve<Features.Diplomacy.WarOfTheRingBehavior>());")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign("campaignStarter.AddBehavior(new WarOfTheRingBehavior(a, b,\n    c));"));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
