using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Arena;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// #744: Patch69 resolves the alignment filter inside the alignment pass's own catch, so a dependency that
/// stops resolving turns the filter off with only a WARNING per roster read. Its IAlignmentService comes from
/// another feature's registration (ExecutionIoC); this pins that the graph closes across the two (the
/// CompanionTacticsWiringTests shape: Validate walks the graph without constructing anything).
/// </summary>
[TestClass]
public class ArenaWiringTests
{
    [TestMethod]
    public void RegisterArenaFeature_WithExecution_AlignmentFilterGraphResolves()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        ExecutionIoC.RegisterExecutionFeature(container);
        ArenaIoC.RegisterArenaFeature(container);

        var errors = container.Validate(typeof(TournamentAlignmentFilterService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
