using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>The equivalence gate is only as good as its comparison: these prove it can fail, and how it reports.</summary>
[TestClass]
public class XmlMergeAssertTests
{
    [TestMethod]
    public void SameDocument_IdenticalStrings_Passes()
    {
        XmlMergeAssert.SameDocument("<a x=\"1\"/>", "<a x=\"1\"/>", "same");
    }

    [TestMethod]
    public void SameDocument_OneCharacterDiffers_FailsNamingTheIndex()
    {
        var message = FailureOf("<a x=\"1\"/>", "<a x=\"2\"/>");

        StringAssert.Contains(message, "first difference at 6");
        StringAssert.Contains(message, "label: documents differ");
    }

    [TestMethod]
    public void SameDocument_DifferentLengths_FailsNamingBothLengths()
    {
        var message = FailureOf("<a/>", "<a/>x");

        StringAssert.Contains(message, "engine length 4, fast length 5, first difference at 4");
    }

    private static string FailureOf(string engine, string fast)
    {
        try
        {
            XmlMergeAssert.SameDocument(engine, fast, "label");
        }
        catch (AssertFailedException ex)
        {
            return ex.Message;
        }
        Assert.Fail("SameDocument passed two different strings.");
        return "";
    }
}
