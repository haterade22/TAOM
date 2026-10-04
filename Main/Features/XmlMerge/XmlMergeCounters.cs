namespace TAOM.Features.XmlMerge;

/// <summary>
/// What one fast merge spent and reused, for its [XmlMerge] line: <see cref="System.Diagnostics.Stopwatch"/> ticks
/// per phase, and the XSLT work done or saved.
/// </summary>
public sealed class XmlMergeCounters
{
    /// <summary>Loading and validating the files (the engine's CreateDocumentFromXmlFile).</summary>
    public long LoadTicks { get; internal set; }

    /// <summary>Applying XSLTs, compiles included.</summary>
    public long XsltTicks { get; internal set; }

    /// <summary>The engine's MergeElements, or the plain append when an entry has no XSD.</summary>
    public long MergeTicks { get; internal set; }

    public long XsltCompiles { get; internal set; }

    public long XsltCacheHits { get; internal set; }
}
