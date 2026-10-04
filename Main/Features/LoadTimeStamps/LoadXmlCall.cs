namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// One MBObjectManager.LoadXML call in flight on its thread: started by the LoadXML prefix, given
/// its merge end and counts by the CreateMergedXmlFile finalizer, closed by the LoadXML finalizer.
/// </summary>
public sealed class LoadXmlCall
{
    public LoadXmlCall(string? id, string? gameType, long start, LoadXmlCall? parent)
    {
        Id = id;
        GameType = gameType;
        Start = start;
        Parent = parent;
    }

    public string? Id { get; }

    public string? GameType { get; }

    /// <summary>Clock ticks at LoadXML entry.</summary>
    public long Start { get; }

    /// <summary>The call that was in flight on this thread when this one began, restored at its end.</summary>
    public LoadXmlCall? Parent { get; }

    /// <summary>Clock ticks when the call's first merge finished; null when no merge was seen.</summary>
    public long? MergeEnd { get; set; }

    /// <summary>Merged files (entries with a path).</summary>
    public int Files { get; set; }

    /// <summary>XSLTs the merge applied (entries after the first that name one).</summary>
    public int Xslt { get; set; }
}
