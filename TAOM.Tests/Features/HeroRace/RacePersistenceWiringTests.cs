using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// #1035: RacePersistenceBehavior's constructor is the per-campaign reset of the race persistence singleton, so
/// SubModule.OnGameStart must build it inline with <c>new</c> as AddBehavior's argument, exactly once, and nothing else may
/// construct or register it (csharp-architecture.md, "Singleton Services Holding Per-Campaign State"). A container-resolved
/// behavior is built once per process and would reset once. Source text only, so no RequiresGame tag.
/// </summary>
[TestClass]
public class RacePersistenceWiringTests
{
    private static readonly Regex InlinePerCampaign =
        new Regex(@"campaignStarter\.AddBehavior\(\s*new\s+RacePersistenceBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+RacePersistenceBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void SubModule_BuildsTheBehaviorInlineAsTheAddBehaviorArgument()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "SubModule.cs must build RacePersistenceBehavior inline as campaignStarter.AddBehavior's argument, exactly "
            + "once: its constructor is the #1035 per-campaign reset of the race persistence service.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsOrRegistersTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !IsFile(path, "SubModule.cs") && !IsFile(path, "RacePersistenceBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "RacePersistenceBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only SubModule.cs may construct RacePersistenceBehavior (#1035): " + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("campaignStarter.AddBehavior(_cached ??= new RacePersistenceBehavior(s));")]
    [DataRow("private static readonly RacePersistenceBehavior B = new RacePersistenceBehavior(s); campaignStarter.AddBehavior(B);")]
    [DataRow("campaignStarter.AddBehavior(IoC.Resolve<RacePersistenceBehavior>());")]
    [DataRow("campaignStarter.AddBehavior(new RacePersistenceBehavior(s)); var late = new RacePersistenceBehavior(s);")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstruction()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign("campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));"));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
