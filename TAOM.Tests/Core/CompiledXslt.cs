using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace TAOM.Tests.Core;

/// <summary>How a test compiles a stylesheet. Each value is one loader the tests used before the cache, copied as it was.</summary>
internal enum XsltLoadStyle
{
    /// <summary><c>XslCompiledTransform.Load(string)</c>: the framework's default reader, settings and resolver.</summary>
    FrameworkDefaults,

    /// <summary>An <c>XmlReader</c> that ignores a DTD and resolves nothing, <c>XsltSettings(false, false)</c>, no resolver.</summary>
    IgnoreDtdNoResolver,
}

/// <summary>
/// One compiled stylesheet per (full path, load style) for the whole test run, so a test that runs a shipped
/// XSLT does not compile it again. A compiled XslCompiledTransform is safe to share for Transform. A
/// stylesheet that fails to compile throws on every call and is never cached. Not re-validated: a test
/// that edits a stylesheet during the run must compile it itself.
/// </summary>
internal static class CompiledXslt
{
    // The path half compares ordinally on purpose: the same file spelled in another case only compiles
    // one more copy, never a wrong one.
    private static readonly ConcurrentDictionary<(string Path, XsltLoadStyle Style), XslCompiledTransform> Cache = new();

    public static XslCompiledTransform Get(string stylesheetPath, XsltLoadStyle style) =>
        Cache.GetOrAdd((Path.GetFullPath(stylesheetPath), style), key => Compile(key.Path, key.Style));

    private static XslCompiledTransform Compile(string path, XsltLoadStyle style)
    {
        var xslt = new XslCompiledTransform();
        if (style == XsltLoadStyle.FrameworkDefaults)
        {
            xslt.Load(path);
            return xslt;
        }

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using (var sheet = XmlReader.Create(path, settings))
            xslt.Load(sheet, new XsltSettings(false, false), null);
        return xslt;
    }
}
