using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.Realms.Popups;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// The card heights the title tree reserves for wrapping text (#1255): short names keep each kind's height, a title,
/// holder or count that wraps grows its card, so the canvas places the next row or level below the last line.
/// </summary>
[TestClass]
public class TitleTreeTextFitTests
{
    [TestMethod]
    public void Lines_EmptyText_IsZero()
    {
        Assert.AreEqual(0, TitleTreeTextFit.Lines("", 18f, TitleTreeTextFit.RowTextWidth));
        Assert.AreEqual(0, TitleTreeTextFit.Lines(null, 18f, TitleTreeTextFit.RowTextWidth));
    }

    [TestMethod]
    public void Lines_ShortName_IsOne()
    {
        Assert.AreEqual(1, TitleTreeTextFit.Lines("Barony of Short", 18f, TitleTreeTextFit.RowTextWidth));
    }

    [TestMethod]
    public void Lines_AmonAngenedInARow_IsTwo()
    {
        // The game wrapped this row title onto a second line, and its holder drew over that line.
        Assert.AreEqual(2, TitleTreeTextFit.Lines("Barony of Amon Angened", 18f, TitleTreeTextFit.RowTextWidth));
    }

    [TestMethod]
    public void Lines_WordsWrapWhole()
    {
        Assert.AreEqual(3, TitleTreeTextFit.Lines("aaaa aaaa aaaa", 10f, 4 * 10f * TitleTreeTextFit.CharWidth + 1f), "one word to a line");
    }

    [DataTestMethod]
    [DataRow((int)TitleTreeCardKind.Root, "Stewardship of Gondor", "Denethor II", "+41 lordships")]
    [DataRow((int)TitleTreeCardKind.Node, "Principality of Dol Amroth", "Imrahil", "+3 lordships")]
    [DataRow((int)TitleTreeCardKind.Row, "Lordship of Henneth Annun", "Mablung", "")]
    [DataRow((int)TitleTreeCardKind.Row, "Barony of Amon Angened", "Narzugh", "+2 lordships")]
    public void CardHeight_UsualNames_KeepTheKindsHeight(int kindValue, string title, string holder, string folded)
    {
        // An int, because the card kind is internal and a public test cannot take it as a parameter.
        var kind = (TitleTreeCardKind)kindValue;
        Assert.AreEqual(Default(kind), TitleTreeTextFit.CardHeight(kind, title, holder, folded), 0.01f);
    }

    [TestMethod]
    public void CardHeight_ThreeLineNodeTitle_GrowsTheNodeBelowItsLastLine()
    {
        const string title = "A Very Long Great Title Name That Must Wrap Onto A Second Line";
        int lines = TitleTreeTextFit.Lines(title, TitleTreeTextFit.NodeTitleFont, TitleTreeTextFit.NodeTextWidth);

        float height = TitleTreeTextFit.CardHeight(TitleTreeCardKind.Node, title, "Faramir", "+27 lordships");

        Assert.IsTrue(lines >= 3, lines.ToString());
        Assert.IsTrue(height > TitleTreeCanvasLayout.NodeHeight, height.ToString());
        float text = (lines * TitleTreeTextFit.NodeTitleFont + 2f * TitleTreeTextFit.NodeSmallFont) * TitleTreeTextFit.LineHeight;
        Assert.IsTrue(height >= TitleTreeTextFit.NodeTextTop + text, height.ToString());
    }

    [TestMethod]
    public void CardHeight_LongRow_GrowsTheRow()
    {
        float height = TitleTreeTextFit.CardHeight(TitleTreeCardKind.Row, "Lordship of the Long Name Holding Beyond the River",
            "Lady Elenwe Morwen of the Long Holder Name", "+12 lordships");

        Assert.IsTrue(height > TitleTreeCanvasLayout.RowHeight, height.ToString());
    }

    [TestMethod]
    public void CardHeight_TwoLineRealm_GrowsTheRealm()
    {
        float height = TitleTreeTextFit.CardHeight(TitleTreeCardKind.Root,
            "The Reunited Kingdom of Gondor, Arnor and the Northern Dunedain", "Elessar Telcontar, King of the West", "+41 lordships");

        Assert.IsTrue(height > TitleTreeCanvasLayout.RootHeight, height.ToString());
    }

    private static float Default(TitleTreeCardKind kind) => kind switch
    {
        TitleTreeCardKind.Root => TitleTreeCanvasLayout.RootHeight,
        TitleTreeCardKind.Node => TitleTreeCanvasLayout.NodeHeight,
        _ => TitleTreeCanvasLayout.RowHeight,
    };
}
