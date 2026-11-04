using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Build-spec 5.6: a view whose camera override is declared outside the TaleWorlds and SandBox assemblies owns the camera.</summary>
[TestClass]
public class SeatForeignCameraRuleTests
{
    [DataTestMethod]
    [DataRow("TAOM.Client", false, true)]
    [DataRow("SomeOtherMod", false, true)]
    [DataRow("TaleWorlds.MountAndBlade.View", false, false)]
    [DataRow("TaleWorlds.MountAndBlade.GauntletUI", false, false)]
    [DataRow("SandBox.View", false, false)]
    [DataRow("SandBox", false, false)]
    [DataRow("SandBox.GauntletUI", false, false)]
    public void IsForeign_ByDeclaringAssembly(string assembly, bool isSeatView, bool expected) =>
        Assert.AreEqual(expected, SeatForeignCameraRule.IsForeign(assembly, isSeatView));

    [TestMethod]
    public void IsForeign_TheSeatViewItself_IsNeverForeign() =>
        Assert.IsFalse(SeatForeignCameraRule.IsForeign("TAOM.Client", isSeatView: true));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IsForeign_UnnamedAssembly_IsForeign_SoTheSeatStaysOut(string? assembly) =>
        Assert.IsTrue(SeatForeignCameraRule.IsForeign(assembly, isSeatView: false));

    [TestMethod]
    public void IsForeign_AssemblyThatMerelyStartsLikeTaleWorlds_IsForeign() =>
        Assert.IsTrue(SeatForeignCameraRule.IsForeign("TaleWorldsMod", isSeatView: false));
}
