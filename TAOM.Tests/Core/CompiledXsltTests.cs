using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

[TestClass]
public class CompiledXsltTests
{
    private const string Sheet =
        "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
        + "<xsl:template match=\"/\"><out/></xsl:template></xsl:stylesheet>";

    private static string WriteSheet()
    {
        var dir = Path.Combine(Path.GetTempPath(), "taom-compiledxslt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "probe.xslt");
        File.WriteAllText(path, Sheet);
        return path;
    }

    [TestMethod]
    public void Get_SamePathAndStyle_ReturnsOneCompiledInstance()
    {
        var path = WriteSheet();
        try
        {
            Assert.AreSame(CompiledXslt.Get(path, XsltLoadStyle.FrameworkDefaults), CompiledXslt.Get(path, XsltLoadStyle.FrameworkDefaults));
            Assert.AreSame(CompiledXslt.Get(path, XsltLoadStyle.IgnoreDtdNoResolver), CompiledXslt.Get(path, XsltLoadStyle.IgnoreDtdNoResolver));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [TestMethod]
    public void Get_SamePathOtherStyle_CompilesItsOwnInstance()
    {
        var path = WriteSheet();
        try
        {
            Assert.AreNotSame(CompiledXslt.Get(path, XsltLoadStyle.FrameworkDefaults), CompiledXslt.Get(path, XsltLoadStyle.IgnoreDtdNoResolver));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }
}
