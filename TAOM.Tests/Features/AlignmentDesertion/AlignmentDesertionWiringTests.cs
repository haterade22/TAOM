using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentDesertion.Hooks;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.AlignmentDesertion;

[TestClass]
public class AlignmentDesertionWiringTests
{
    // #1408: without the alignment handoff the recruit hint skips the block and shows "You need 0" again.
    [TestMethod]
    public void TheModule_InitializesTheRecruitHintStaticsFromTheContainer()
    {
        var guard = Substitute.For<IDesertionRecruitGuard>();
        var alignment = Substitute.For<IRecruitmentAlignmentService>();
        var logger = Substitute.For<IModLogger>();
        var fields = new[] { "_guard", "_alignment", "_logger" }
            .Select(n => typeof(Patch157_RecruitSlotHint_Prefix).GetField(n, BindingFlags.NonPublic | BindingFlags.Static))
            .ToArray();
        CollectionAssert.AllItemsAreNotNull(fields, "a Patch157 static was renamed");
        try
        {
            using var container = new Container();
            container.RegisterInstance(guard);
            container.RegisterInstance(alignment);
            container.RegisterInstance(logger);
            container.RegisterInstance(Substitute.For<ICoopSessionProvider>());

            new AlignmentDesertionModule().InitializeStatics(container);

            Assert.AreSame(guard, fields[0]!.GetValue(null));
            Assert.AreSame(alignment, fields[1]!.GetValue(null));
            Assert.AreSame(logger, fields[2]!.GetValue(null));
        }
        finally
        {
            foreach (var f in fields)
                f!.SetValue(null, null);
            Patch150_ConformityTick_Prefix.Initialize(null!, null!, null!);
            Patch158_AiMercenaryHire_Prefix.Initialize(null!, null!, null!);
            TavernMercenaries.Initialize(null!, null!);
        }
    }
}
