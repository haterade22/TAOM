using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>One line of what the coronation changed ("Minas Tirith passes to him, with the Duchy of Anórien").</summary>
internal sealed class CoronationLineVM : ViewModel
{
    public CoronationLineVM(string text) => Text = text;

    [DataSourceProperty] public string Text { get; }
}

/// <summary>
/// The data source of RealmsCoronationPopup.xml: the coronation's event window. The heading, story, illustration and
/// button come from the &lt;Coronation&gt; entry (placeholders until the event art is made); without an illustration the
/// new house's banner stands in its place. The lines are what actually changed.
/// </summary>
internal sealed class CoronationPopupVM : ViewModel
{
    private readonly Action _close;

    public CoronationPopupVM(string heading, string text, string? image, string button, Banner banner,
        System.Collections.Generic.IEnumerable<string> lines, Action close)
    {
        _close = close;
        Heading = heading;
        Text = text;
        ImageSprite = image ?? string.Empty;
        HasImage = image != null;
        ButtonText = button;
        Banner = new BannerImageIdentifierVM(banner, nineGrid: true);
        Lines = new MBBindingList<CoronationLineVM>();
        foreach (var line in lines)
            Lines.Add(new CoronationLineVM(line));
    }

    [DataSourceProperty] public string Heading { get; }
    [DataSourceProperty] public string Text { get; }
    [DataSourceProperty] public string ImageSprite { get; }
    [DataSourceProperty] public bool HasImage { get; }
    [DataSourceProperty] public bool HasNoImage => !HasImage;
    [DataSourceProperty] public string ButtonText { get; }
    [DataSourceProperty] public BannerImageIdentifierVM Banner { get; }
    [DataSourceProperty] public MBBindingList<CoronationLineVM> Lines { get; }

    public void ExecuteClose() => _close();

    public override void OnFinalize()
    {
        base.OnFinalize();
        Banner.OnFinalize();
    }
}
