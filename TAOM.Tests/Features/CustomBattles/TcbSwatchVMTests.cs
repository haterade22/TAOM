using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.CustomBattles.Muster;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>The banner palette marks the colour the army already uses, so the open palette shows the current choice.</summary>
[TestClass]
public class TcbSwatchVMTests
{
    [TestMethod]
    public void MarkCurrent_ColourInPalette_SelectsOnlyThatSwatch()
    {
        var red = new TcbSwatchVM(0xFFAA0000, null!);
        var blue = new TcbSwatchVM(0xFF0000AA, null!);

        TcbSwatchVM.MarkCurrent(new[] { red, blue }, 0xFF0000AA);

        Assert.IsFalse(red.IsSelected);
        Assert.IsTrue(blue.IsSelected);
    }

    [TestMethod]
    public void MarkCurrent_ColourChanged_MovesTheSelection()
    {
        var red = new TcbSwatchVM(0xFFAA0000, null!);
        var blue = new TcbSwatchVM(0xFF0000AA, null!);
        var palette = new[] { red, blue };

        TcbSwatchVM.MarkCurrent(palette, 0xFF0000AA);
        TcbSwatchVM.MarkCurrent(palette, 0xFFAA0000);

        Assert.IsTrue(red.IsSelected);
        Assert.IsFalse(blue.IsSelected);
    }
}
