using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Attr = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace TAOM.Features.TimeAcceleration.UI;

// Widen the CenterPanel to make room for the extra fast-forward button.
[PrefabExtension("MapBar", "descendant::MapCurrentTimeVisualWidget[@Id='CenterPanel']")]
internal class PrefabCenterPanel : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("SuggestedWidth", "500")
    };
}

// Shift the vanilla FastForward button left to create space for the new button.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='FastForwardButton']")]
internal class PrefabFastForwardButton : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("PositionXOffset", "-105")
    };
}

// Shift the vanilla Play button left.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='PlayButton']")]
internal class PrefabPlayButton : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("PositionXOffset", "-145")
    };
}

// Shift the vanilla Pause button left.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='PauseButton']")]
internal class PrefabPauseButton : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("PositionXOffset", "-185")
    };
}

// Insert the Extra Fast-Forward button after the PauseButton in the CenterPanel children.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='PauseButton']")]
internal class PrefabInsertExtraFastForward : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<ButtonWidget" +
            " Id=\"FastFastForwardButton\"" +
            " WidthSizePolicy=\"Fixed\"" +
            " HeightSizePolicy=\"Fixed\"" +
            " SuggestedWidth=\"35\"" +
            " SuggestedHeight=\"24\"" +
            " HorizontalAlignment=\"Right\"" +
            " VerticalAlignment=\"Bottom\"" +
            " PositionXOffset=\"-62\"" +
            " PositionYOffset=\"-13\"" +
            " Brush=\"MapBarFastForwardButton\"" +
            " IsSelected=\"@IsExtraFastForwardActive\"" +
            " Command.Click=\"ExecuteTimeControlChange\"" +
            " CommandParameter.Click=\"2\"" +
            " GamepadNavigationIndex=\"4\">" +
            "<Children>" +
            "<HintWidget DataSource=\"{ExtraFastForwardHint}\"" +
            " WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\"" +
            " Command.HoverBegin=\"ExecuteBeginHint\"" +
            " Command.HoverEnd=\"ExecuteEndHint\"" +
            " IsDisabled=\"true\" />" +
            "</Children>" +
            "</ButtonWidget>");
        return doc;
    }
}
