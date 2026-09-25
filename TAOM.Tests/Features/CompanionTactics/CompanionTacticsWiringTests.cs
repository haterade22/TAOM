using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CompanionTactics.FormationPresets;

namespace TAOM.Tests.Features.CompanionTactics;

/// <summary>
/// The OOB overlay is resolved inside Patch35_OOBUIHandler_Tick's empty catch, every frame, so a
/// dependency that stops resolving makes both overlay buttons vanish with no log line. Plan 022
/// added IOOBCaptainAutoAssigner to that graph; this pins that it closes (the
/// CampsContainerWiringTests shape: Validate walks the graph without constructing anything).
/// </summary>
[TestClass]
public class CompanionTacticsWiringTests
{
    [TestMethod]
    public void RegisterCompanionTacticsFeature_OverlayGraph_Resolves()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);

        var errors = container.Validate(typeof(IOOBOverlayService), typeof(IOOBCaptainAutoAssigner));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
