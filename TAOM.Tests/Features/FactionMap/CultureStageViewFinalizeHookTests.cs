using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FactionMap.Hooks;

namespace TAOM.Tests.Features.FactionMap;

/// <summary>
/// Issue #704. When the culture stage closes, a screen that took it over (Kysaro's faction screen) is
/// told, so it lets go of its screen, widgets and view model.
/// </summary>
[TestClass]
public class CultureStageViewFinalizeHookTests
{
    [TestMethod]
    public void OnFinalize_TellsTheReplacementScreenTheStageIsClosing()
    {
        var movieOverride = Substitute.For<ICultureStageMovieOverride>();
        var sut = new CultureStageViewFinalizeHook(movieOverride);

        sut.OnFinalize();

        movieOverride.Received(1).OnCultureStageClosed();
    }
}
