namespace TAOM.Features.WarChronicle.Events;

public sealed class ChronicleEventRecord
{
    public ChronicleEventRecord(string id, ChronicleEventState state)
    {
        Id = id;
        State = state;
    }

    public string Id { get; }

    public ChronicleEventState State { get; set; }

    public double? OpenDay { get; set; }

    public double? CloseDay { get; set; }

    public bool Assaulted { get; set; }

    public double? ResolvedDay { get; set; }

    /// <summary>The player chose "Let fate decide" on this window's story offer: it is not offered again (#1105). Saved as <c>"sd"</c>.</summary>
    public bool StoryDeclined { get; set; }
}
