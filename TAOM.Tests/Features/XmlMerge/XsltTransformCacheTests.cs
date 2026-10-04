using System;
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.XmlMerge;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// The XSLT half of the fast path (plan 042): the engine's ApplyXslt with the compiled stylesheet cached by path and
/// re-validated by the file's last-write time and length. System.Xml only, so these run on hosted CI.
/// </summary>
[TestClass]
public class XsltTransformCacheTests
{
    private const string DropB =
        "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">" +
        "<xsl:output omit-xml-declaration=\"yes\"/>" +
        "<xsl:template match=\"@*|node()\"><xsl:copy><xsl:apply-templates select=\"@*|node()\"/></xsl:copy></xsl:template>" +
        "<xsl:template match=\"Thing[@id='b']\"/>" +
        "</xsl:stylesheet>";

    private const string DropC =
        "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">" +
        "<xsl:output omit-xml-declaration=\"yes\"/>" +
        "<xsl:template match=\"@*|node()\"><xsl:copy><xsl:apply-templates select=\"@*|node()\"/></xsl:copy></xsl:template>" +
        "<xsl:template match=\"Thing[@id='c']\"/>" +
        "</xsl:stylesheet>";

    private string _folder = "";

    [TestInitialize]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "taom-xslt-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [TestMethod]
    public void Apply_SamePathTwice_CompilesOnce()
    {
        var path = Write("drop.xslt", DropB);
        var cache = new XsltTransformCache();

        cache.Apply(path, Things());
        cache.Apply(path, Things());

        Assert.AreEqual(1L, cache.Compiles);
        Assert.AreEqual(1L, cache.CacheHits);
    }

    [TestMethod]
    public void Apply_FileRewrittenWithNewTime_Recompiles()
    {
        var path = Write("drop.xslt", DropB);
        var cache = new XsltTransformCache();
        cache.Apply(path, Things());

        File.WriteAllText(path, DropC);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        var output = cache.Apply(path, Things());

        Assert.AreEqual(2L, cache.Compiles);
        Assert.AreEqual(0L, cache.CacheHits);
        Assert.IsNotNull(output.SelectSingleNode("/Things/Thing[@id='b']"), "the rewritten stylesheet drops c, not b");
        Assert.IsNull(output.SelectSingleNode("/Things/Thing[@id='c']"));
    }

    [TestMethod]
    public void Apply_DropTemplate_RemovesTheMatchedElement()
    {
        var path = Write("drop.xslt", DropB);

        var output = new XsltTransformCache().Apply(path, Things());

        Assert.AreEqual("<Things><Thing id=\"a\" /><Thing id=\"c\" /></Things>", output.OuterXml);
    }

    [TestMethod]
    public void Apply_Output_UsesTheInputDocumentsNameTable()
    {
        var path = Write("drop.xslt", DropB);
        var input = Things();

        var output = new XsltTransformCache().Apply(path, input);

        Assert.IsTrue(ReferenceEquals(output.NameTable, input.NameTable),
            "the engine builds the output as new XmlDocument(baseDocument.CreateNavigator().NameTable)");
    }

    [TestMethod]
    public void Apply_MissingStylesheet_Throws()
    {
        var cache = new XsltTransformCache();
        var missing = Path.Combine(_folder, "missing.xslt");

        Assert.ThrowsException<FileNotFoundException>(() => cache.Apply(missing, Things()));
        Assert.AreEqual(0L, cache.Compiles);
    }

    [TestMethod]
    public void Apply_FileRewrittenWithTheSameTimeButAnotherLength_Recompiles()
    {
        var path = Write("drop.xslt", DropB);
        var stamp = File.GetLastWriteTimeUtc(path);
        var cache = new XsltTransformCache();
        cache.Apply(path, Things());

        // DropB and DropC have the same length; the trailing space changes only the length, and the time is put back.
        File.WriteAllText(path, DropC + " ");
        File.SetLastWriteTimeUtc(path, stamp);
        var output = cache.Apply(path, Things());

        Assert.AreEqual(2L, cache.Compiles);
        Assert.AreEqual(0L, cache.CacheHits);
        Assert.IsNotNull(output.SelectSingleNode("/Things/Thing[@id='b']"), "the rewritten stylesheet drops c, not b");
        Assert.IsNull(output.SelectSingleNode("/Things/Thing[@id='c']"));
    }

    [TestMethod]
    public void Apply_MalformedStylesheet_ThrowsEveryTimeAndIsNeverCached()
    {
        var path = Write("broken.xslt",
            "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:template match=\"/\">");
        var cache = new XsltTransformCache();

        AssertThrows(() => cache.Apply(path, Things()));
        AssertThrows(() => cache.Apply(path, Things()));

        Assert.AreEqual(0L, cache.Compiles);
        Assert.AreEqual(0L, cache.CacheHits);
    }

    private static void AssertThrows(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }
        Assert.Fail("a malformed stylesheet must throw, as the engine's ApplyXslt does");
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static XmlDocument Things()
    {
        var document = new XmlDocument();
        document.LoadXml("<Things><Thing id=\"a\" /><Thing id=\"b\" /><Thing id=\"c\" /></Things>");
        return document;
    }
}
