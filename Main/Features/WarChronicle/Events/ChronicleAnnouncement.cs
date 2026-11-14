namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// One queued chronicle announcement: texts as authored (<c>{=taom_wc_*}English</c>) plus the values
/// their variables take. The presenter resolves them at show time, so nothing here is localized and
/// nothing is saved.
/// </summary>
public sealed class ChronicleAnnouncement
{
    public ChronicleAnnouncement(string eventId, string title, string? text, string? targetSettlementId, int days,
        string? image = null, ChronicleOutcomeReport? outcome = null)
    {
        EventId = eventId;
        Title = title;
        Text = text;
        TargetSettlementId = targetSettlementId;
        Days = days;
        Image = image;
        Outcome = outcome ?? ChronicleOutcomeReport.None;
    }

    public string EventId { get; }

    public string Title { get; }

    /// <summary>The body line; null shows the title alone.</summary>
    public string? Text { get; }

    /// <summary>Fills <c>{TARGET}</c> with that settlement's name; null leaves it empty.</summary>
    public string? TargetSettlementId { get; }

    /// <summary>Fills <c>{DAYS}</c>.</summary>
    public int Days { get; }

    /// <summary>The event's <c>image</c>: the illustration's file name in GUI/EventArt; null shows the text alone.</summary>
    public string? Image { get; }

    /// <summary>What the resolution applied, listed under the story; empty for an opening, a lapse or an outcome without effects.</summary>
    public ChronicleOutcomeReport Outcome { get; }
}
