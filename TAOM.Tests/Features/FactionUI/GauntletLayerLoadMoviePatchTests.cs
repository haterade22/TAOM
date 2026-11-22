using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Hooks;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Under co-op the native map bar stays, so the movies that sit on or beside it stay native too: the Camp icon beside
/// the clock, and the army panel above the stats plate (#1532).
/// </summary>
[TestClass]
public class GauntletLayerLoadMoviePatchTests
{
    [DataTestMethod]
    [DataRow("MapBar")]
    [DataRow("TaomFieldCampOverlay")]
    [DataRow("ArmyOverlay")]
    public void StaysNativeUnderCoop_TheMapBarAndWhatSitsOnIt(string movieName)
    {
        Assert.IsTrue(GauntletLayerLoadMoviePatch.StaysNativeUnderCoop(movieName));
    }

    [DataTestMethod]
    [DataRow("EscapeMenu")]
    [DataRow("CharacterDeveloper")]
    public void StaysNativeUnderCoop_OtherThemedScreens_AreStillSwapped(string movieName)
    {
        Assert.IsFalse(GauntletLayerLoadMoviePatch.StaysNativeUnderCoop(movieName));
    }
}
