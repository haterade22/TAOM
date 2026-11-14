using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;

namespace TAOM.Client.Features.EventWindow;

/// <summary>One line of what the event changed ("Minas Tirith passes to him, with the Duchy of Anórien").</summary>
internal sealed class EventWindowLineVM : ViewModel
{
    public EventWindowLineVM(string text) => Text = text;

    [DataSourceProperty] public string Text { get; }
}

/// <summary>One applied effect: who, what, by how much, and its own duration when the rows differ.</summary>
internal sealed class EventWindowEffectRowVM : ViewModel
{
    public EventWindowEffectRowVM(EventWindowEffectRow row)
    {
        Target = row.Target;
        Label = row.Label;
        Value = row.Value;
        Duration = row.Duration ?? string.Empty;
    }

    [DataSourceProperty] public string Target { get; }
    [DataSourceProperty] public string Label { get; }
    [DataSourceProperty] public string Value { get; }
    [DataSourceProperty] public string Duration { get; }
    [DataSourceProperty] public bool HasDuration => Duration.Length > 0;
}

/// <summary>
/// The data source of TaomEventWindow.xml, the story event window of the Realms coronation and the War Chronicle. The
/// prefab holds the layout and its sprites; every text comes from the <see cref="TAOM.Adapters.EventWindow"/>. The
/// illustration is the event's picture from the art folder (<see cref="EventArt"/>), or the banner without
/// one, or with neither the story alone, centred.
/// </summary>
internal sealed class EventWindowVM : ViewModel
{
    private readonly Action _close;

    internal const string EffectsHeadingText = "{=taom_event_effects}Effects";

    public EventWindowVM(TAOM.Adapters.EventWindow window, Action close, string artDirectory)
    {
        _close = close;
        Category = window.Category ?? string.Empty;
        Effects = new MBBindingList<EventWindowEffectRowVM>();
        if (window.Effects != null)
        {
            foreach (var row in window.Effects.Rows)
                Effects.Add(new EventWindowEffectRowVM(row));
        }

        EffectsDuration = window.Effects?.Duration ?? string.Empty;
        EffectsNote = window.Effects?.Note ?? string.Empty;
        EffectsHeading = Effects.Count > 0 ? new TextObject(EffectsHeadingText).ToString() : string.Empty;
        Heading = window.Heading;
        Text = window.Text;
        ImageFile = EventArt.Resolve(window.Image, artDirectory) ?? string.Empty;
        HasImage = ImageFile.Length > 0;
        HasBanner = !HasImage && window.BannerCode != null;
        ButtonText = window.Button;
        // Never null: the prefab binds the pennant to it even while hidden.
        Banner = new BannerImageIdentifierVM(HasBanner ? new Banner(window.BannerCode) : new Banner(), nineGrid: true);
        Lines = new MBBindingList<EventWindowLineVM>();
        foreach (var line in window.Lines)
            if (!string.IsNullOrWhiteSpace(line))
                Lines.Add(new EventWindowLineVM(line));
    }

    /// <summary>The kind of event over the heading; empty hides it.</summary>
    [DataSourceProperty] public string Category { get; }
    [DataSourceProperty] public bool HasCategory => Category.Length > 0;
    [DataSourceProperty] public string Heading { get; }
    /// <summary>What the event applied; empty hides the section, its heading and its rule.</summary>
    [DataSourceProperty] public MBBindingList<EventWindowEffectRowVM> Effects { get; }
    [DataSourceProperty] public bool HasEffects => Effects.Count > 0;
    [DataSourceProperty] public string EffectsHeading { get; }
    [DataSourceProperty] public string EffectsDuration { get; }
    [DataSourceProperty] public bool HasEffectsDuration => EffectsDuration.Length > 0;
    [DataSourceProperty] public string EffectsNote { get; }
    [DataSourceProperty] public bool HasEffectsNote => EffectsNote.Length > 0;
    [DataSourceProperty] public string Text { get; }
    /// <summary>The picture's full path for the prefab's EncyclopediaPictureWidget; empty without one.</summary>
    [DataSourceProperty] public string ImageFile { get; }
    [DataSourceProperty] public bool HasImage { get; }
    [DataSourceProperty] public bool HasBanner { get; }
    [DataSourceProperty] public bool HasIllustration => HasImage || HasBanner;
    [DataSourceProperty] public bool HasLines => Lines.Count > 0;
    [DataSourceProperty] public string ButtonText { get; }
    [DataSourceProperty] public BannerImageIdentifierVM Banner { get; }
    [DataSourceProperty] public MBBindingList<EventWindowLineVM> Lines { get; }

    public void ExecuteClose() => _close();

    public override void OnFinalize()
    {
        base.OnFinalize();
        Banner.OnFinalize();
    }
}
