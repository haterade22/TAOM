using System.Diagnostics;

namespace TAOM.Features.XmlMerge;

/// <summary>
/// One call of <c>MBObjectManager.CreateMergedXmlFile</c>, carried from the patch's prefix to its finalizer as Harmony's
/// <c>__state</c>. The prefix creates it (the start timestamp), the service fills it, and the finalizer reads it to log
/// an engine-path merge and to settle a fast-path failure.
/// </summary>
public sealed class XmlMergeCall
{
    public XmlMergeCall()
    {
        StartTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary><see cref="Stopwatch.GetTimestamp"/> when the prefix ran; every logged ms counts from here.</summary>
    public long StartTimestamp { get; }

    /// <summary>True when the fast path merged and the engine's own code was skipped.</summary>
    public bool Handled { get; internal set; }

    /// <summary>Why the engine's own code runs; "not-initialized" until the service decides.</summary>
    public string VanillaReason { get; internal set; } = XmlMergeService.ReasonNotInitialized;

    /// <summary>The exception type name the fast path threw on this call, or null.</summary>
    public string? FastErrorType { get; internal set; }

    /// <summary>The merged type id (the XSD file name), or "unknown".</summary>
    public string Type { get; internal set; } = XmlMergeService.UnknownType;

    /// <summary>Entries that name a file.</summary>
    public int Files { get; internal set; }

    /// <summary>Entries at index 1 or later that name an XSLT (the engine never applies index 0's).</summary>
    public int Xslts { get; internal set; }
}
