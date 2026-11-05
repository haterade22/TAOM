using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The crash-loop marker belongs to game launches only. The Modding Kit loads TAOM.Dependencies from its
/// Win64_Shipping_wEditor bin (#1047) and never reaches the game's launch-finished hook, so a marker it
/// wrote made the next game log a false crash loop.
/// </summary>
[TestClass]
public class IncompatibleModDetectorTests
{
    [DataTestMethod]
    [DataRow(@"E:\Steam\Modules\TAOM.Dependencies\bin\Win64_Shipping_wEditor\TAOM.Dependencies.dll", true)]
    [DataRow(@"E:\Steam\Modules\TAOM.Dependencies\bin\win64_shipping_weditor\TAOM.Dependencies.dll", true)]
    [DataRow(@"E:\Steam\Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\TAOM.Dependencies.dll", false)]
    [DataRow(@"E:\Steam\Modules\TAOM.Dependencies\bin\Win64_Shipping_Server\TAOM.Dependencies.dll", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void IsEditorBin_AssemblyPath_TrueOnlyForTheModdingKitBin(string path, bool expected)
    {
        Assert.AreEqual(expected, IncompatibleModDetector.IsEditorBin(path));
    }
}
