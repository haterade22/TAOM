using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The chronicle's router (<c>WarChronicleEventsBehavior</c>) is built inline per campaign like
/// <c>WarChronicleBehavior</c>, because its constructor clears the announcement queue and the army focus;
/// it saves nothing (one SyncData owner); every chronicle call is gated on the co-op authority. Source-text
/// checks, plus a container resolve of the whole chain.
/// </summary>
[TestClass]
public class WarChronicleEventsWiringTests
{
    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*\w+\s*=>\s*new\s+(Events\.)?WarChronicleEventsBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+(Events\.)?WarChronicleEventsBehavior\(");

    [TestMethod]
    public void Module_BuildsTheEventsBehaviorInlineAsADeclarationFactory_ExactlyOnce()
    {
        var src = RepoPaths.ReadSource("Main/Features/WarChronicle/WarChronicleModule.cs", stripComments: true);

        Assert.AreEqual(1, InlinePerCampaign.Matches(src).Count);
        Assert.AreEqual(1, AnyConstruction.Matches(src).Count);
    }

    [TestMethod]
    public void ProductionSource_OnlyTheModuleConstructsTheEventsBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(p => !p.EndsWith("WarChronicleModule.cs", StringComparison.OrdinalIgnoreCase)
                        && !p.EndsWith("WarChronicleEventsBehavior.cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => RepoPaths.MentionsOutsideComments(p, "WarChronicleEventsBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [TestMethod]
    public void EventsBehavior_ResetsTheQueueAndTheFocusInItsConstructor_AndSavesNothing()
    {
        var src = RepoPaths.ReadSource("Main/Features/WarChronicle/Events/WarChronicleEventsBehavior.cs", stripComments: true);
        var ctor = Regex.Match(src, @"public WarChronicleEventsBehavior\([^)]*\)\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        StringAssert.Contains(ctor, "_announcements.ResetForNewSession()");
        StringAssert.Contains(ctor, "_chronicle.ResetTransientState()");
        Assert.IsFalse(src.Contains("dataStore.SyncData"), "WarChronicleBehavior is the one SyncData owner");
    }

    [TestMethod]
    public void EventsBehavior_GatesEveryChronicleCallOnTheCoopAuthority()
    {
        var src = RepoPaths.ReadSource("Main/Features/WarChronicle/Events/WarChronicleEventsBehavior.cs", stripComments: true);

        foreach (var call in new[] { "LaunchChronicle", "RunChronicleDay", "OnTargetOwnerChanged", "OnTargetBesieged" })
        {
            var line = src.Split('\n').Single(l => l.Contains("_chronicle." + call + "("));
            var index = Array.IndexOf(src.Split('\n'), line);
            StringAssert.Contains(src.Split('\n')[index - 1], "MayRunWorldWork", call + " runs only where world work may run");
        }

        StringAssert.Contains(src, "_coop.IsAuthority && CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient)");
    }

    [TestMethod]
    public void TheChronicleChain_ResolvesFromTheContainerAsSingletons()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IWarOfTheRingService>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IAllianceAdapter>());
        container.RegisterInstance(Substitute.For<IMomentumAwardService>());
        container.RegisterInstance(Substitute.For<ITextLocalizerAdapter>());
        // The real ArmyTargeting registration: the chronicle (writer) and the scoring path (reader) must share
        // one focus registry, which only its Reuse.Singleton guarantees.
        ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
        WarChronicleIoC.RegisterWarChronicleFeature(container);

        Assert.AreSame(container.Resolve<ChronicleService>(), container.Resolve<ChronicleService>());
        Assert.AreSame(container.Resolve<ChronicleAnnouncementQueue>(), container.Resolve<ChronicleAnnouncementQueue>());
        Assert.AreSame(container.Resolve<ChronicleStateStore>(), container.Resolve<ChronicleStateStore>());
        Assert.AreSame(container.Resolve<ArmyTargetFocusRegistry>(), container.Resolve<ArmyTargetFocusRegistry>());
        Assert.IsInstanceOfType(container.Resolve<IChronicleConfigProvider>(), typeof(ChronicleConfigProvider));
        Assert.IsInstanceOfType(container.Resolve<IChronicleWorldAdapter>(), typeof(ChronicleWorldAdapter));

        // The save owner and the chronicle share one store.
        container.Resolve<ChronicleStateStore>().Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Held));
        StringAssert.Contains(string.Concat(container.Resolve<WarChronicleStateService>().CaptureChunks()), "hornburg");
    }
}
