using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Arena.Hooks;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Behavior tests for <see cref="GauntletMovie_Release_AvGuard_Patch"/> (issue #339 — a
/// heap-corruption AccessViolationException inside the tournament movie's WidgetTemplate
/// release walk killed a player session; the guard converts that CTD into a logged leak).
/// The finalizer must suppress ONLY AccessViolationException; every other exception — and
/// the no-exception case — must pass through untouched.
/// <para>
/// The [HarmonyPatch] target binding (<c>GauntletMovie.Release</c>) is covered by
/// <c>HarmonyPatchBindingTests</c>; no bespoke drift-guard is needed here.
/// </para>
/// </summary>
[TestClass]
public class Patch62MovieReleaseAvGuardTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        GauntletMovie_Release_AvGuard_Patch.Initialize(_logger);
    }

    [TestMethod]
    public void Finalizer_AccessViolationException_SuppressesAndLogsWarning()
    {
        var av = new AccessViolationException("Attempted to read or write protected memory.");

        var result = GauntletMovie_Release_AvGuard_Patch.Finalizer(null, av);

        Assert.IsNull(result, "AccessViolationException must be suppressed (finalizer returns null).");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("AccessViolationException")));
    }

    [TestMethod]
    public void Finalizer_NonAvException_PropagatesUntouched()
    {
        var nre = new NullReferenceException("unrelated");

        var result = GauntletMovie_Release_AvGuard_Patch.Finalizer(null, nre);

        Assert.AreSame(nre, result, "Non-AV exceptions must propagate untouched.");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Finalizer_NoException_ReturnsNull()
    {
        var result = GauntletMovie_Release_AvGuard_Patch.Finalizer(null, null);

        Assert.IsNull(result, "The no-exception case must stay a no-op.");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Finalizer_AvWithNoLoggerInitialized_StillSuppresses()
    {
        GauntletMovie_Release_AvGuard_Patch.Initialize(null);

        var result = GauntletMovie_Release_AvGuard_Patch.Finalizer(null, new AccessViolationException());

        Assert.IsNull(result, "Suppression must not depend on logger availability.");
    }
}
