using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;
using TAOM.Features.SupplyLines.UI;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The goods and troops lists share one row VM; only the label differs. Each form keeps its own
/// registered key and count variable ({=taom_sl_good_row} with STOCK, {=taom_sl_troop_row} with COUNT),
/// or the translated rows would show an unfilled count. A ViewModel and TextObject.ToString run engine
/// code, so the class needs the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SupplyRowVMTests
{
    private static SupplyLineItem Line(string id, string name, int available, int unitPrice)
        => new SupplyLineItem { Id = id, Name = name, Available = available, UnitPrice = unitPrice };

    [TestMethod]
    public void ForGood_Label_FillsNameStockAndPrice()
        => Assert.AreEqual("Grain - 5 in stock, 10 denars", SupplyRowVM.ForGood(Line("grain", "Grain", 5, 10), () => { }).Label);

    [TestMethod]
    public void ForTroop_Label_FillsNameCountAndPrice()
        => Assert.AreEqual("Militia - 4 available, 50 denars", SupplyRowVM.ForTroop(Line("militia", "Militia", 4, 50), () => { }).Label);

    [TestMethod]
    public void ForGood_NegativeStockAndNoName_ClampsAndFallsBackToTheId()
    {
        var row = SupplyRowVM.ForGood(Line("grain", null!, -3, 10), () => { });
        Assert.AreEqual(0, row.Available);
        Assert.AreEqual("grain - 0 in stock, 10 denars", row.Label);
    }

    [TestMethod]
    public void ExecutePlus_RaisesQtyTextBeforeTheCallback()
    {
        var events = new List<string>();
        var row = SupplyRowVM.ForTroop(Line("militia", "Militia", 4, 50), () => events.Add("callback"));
        row.PropertyChanged += (_, e) => events.Add(e.PropertyName!);
        row.ExecutePlus();
        CollectionAssert.AreEqual(new[] { "QtyText", "callback" }, events);
        Assert.AreEqual("1", row.QtyText);
    }
}
