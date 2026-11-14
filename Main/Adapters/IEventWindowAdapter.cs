using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// What a story event's window shows (TaomEventWindow.xml): the layout and its sprites live in the prefab, every text
/// comes from here, already resolved. The banner travels as its code so no engine type crosses the seam.
/// </summary>
public sealed class EventWindow
{
    public EventWindow(string heading, string text, string? image, string button, string? bannerCode = null,
        IReadOnlyList<string>? lines = null, string? category = null, EventWindowEffects? effects = null)
    {
        Heading = heading;
        Text = text;
        Image = string.IsNullOrWhiteSpace(image) ? null : image;
        Button = button;
        BannerCode = string.IsNullOrWhiteSpace(bannerCode) ? null : bannerCode;
        Lines = lines ?? new string[0];
        Category = string.IsNullOrWhiteSpace(category) ? null : category;
        Effects = effects != null && effects.Rows.Count > 0 ? effects : null;
    }

    /// <summary>The kind of event over the heading ("War Chronicle", "Coronation"); null shows none.</summary>
    public string? Category { get; }

    /// <summary>What the event applied, as rows; null hides the section. Never built from the story text.</summary>
    public EventWindowEffects? Effects { get; }

    public string Heading { get; }
    public string Text { get; }

    /// <summary>The illustration's file name in the module's GUI/EventArt folder. Without one, or when the file is missing, the banner stands in its place, and without either the text alone fills the window.</summary>
    public string? Image { get; }

    public string Button { get; }
    public string? BannerCode { get; }

    /// <summary>What the event changed, one line each, under the story; empty hides the list and its rule.</summary>
    public IReadOnlyList<string> Lines { get; }
}

/// <summary>One applied effect: who it reached, what it changes, by how much, and for how long when the rows differ.</summary>
public sealed class EventWindowEffectRow
{
    public EventWindowEffectRow(string target, string label, string value, string? duration = null)
    {
        Target = target;
        Label = label;
        Value = value;
        Duration = string.IsNullOrWhiteSpace(duration) ? null : duration;
    }

    public string Target { get; }
    public string Label { get; }
    public string Value { get; }

    /// <summary>This row's own duration; null when <see cref="EventWindowEffects.Duration"/> covers every row, or none is known.</summary>
    public string? Duration { get; }
}

/// <summary>The effects list of an event window, already resolved to the player's language.</summary>
public sealed class EventWindowEffects
{
    public EventWindowEffects(IReadOnlyList<EventWindowEffectRow> rows, string? duration = null, string? note = null)
    {
        Rows = rows;
        Duration = string.IsNullOrWhiteSpace(duration) ? null : duration;
        Note = string.IsNullOrWhiteSpace(note) ? null : note;
    }

    public IReadOnlyList<EventWindowEffectRow> Rows { get; }

    /// <summary>The one duration every row shares ("For 15 days"); null when the rows carry their own.</summary>
    public string? Duration { get; }

    /// <summary>How to read the values (relative to the usual rate, capped, the player's clan exempt); null for none.</summary>
    public string? Note { get; }
}

/// <summary>
/// Opens story event windows, which live in the client (ADR-013): the War Chronicle's announcements and the Realms
/// coronation. Core registers <see cref="NullEventWindowAdapter"/> and TAOM.Client's composition replaces it.
/// </summary>
public interface IEventWindowAdapter
{
    /// <summary>An event window is showing or waiting to open. A caller that shows one at a time waits while it is true.</summary>
    bool IsOpen { get; }

    /// <summary>Queues the window; it opens on a coming frame, after any window already showing. False when there is no window to open.</summary>
    bool Show(EventWindow window);
}

/// <summary>There is no window to open (a dedicated server, or a client where TAOM.Client did not load).</summary>
public sealed class NullEventWindowAdapter : IEventWindowAdapter
{
    public bool IsOpen => false;

    public bool Show(EventWindow window) => false;
}
