using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using TAOM.Features.CoopInterop;
using Attr = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace TAOM.Features.TimeAcceleration.UI;

// Widen the CenterPanel to make room for the extra fast-forward button.
[PrefabExtension("MapBar", "descendant::MapCurrentTimeVisualWidget[@Id='CenterPanel']")]
[CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")]
internal class PrefabCenterPanel : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("SuggestedWidth", "500")
    };
}

// Shift the vanilla FastForward button left to create space for the new button, and route its
// click through the mixin (#574). Vanilla's handler, ExecuteTimeControlChange(2), sets the time
// MODE only; after one extra fast-forward the engine kept the extra SpeedUpMultiplier and this
// button silently ran at it. ExecuteFastForward writes the normal multiplier back first, then
// makes the same vanilla call. CommandParameter.Click stays vanilla's "2" and is forwarded.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='FastForwardButton']")]
[CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")]
internal class PrefabFastForwardButton : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("PositionXOffset", "-105"),
        new Attr("Command.Click", "ExecuteFastForward")
    };
}

// Shift the vanilla Play button left.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='PlayButton']")]
[CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")]
internal class PrefabPlayButton : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("PositionXOffset", "-145")
    };
}

// Shift the vanilla Pause button left.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='PauseButton']")]
[CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")]
internal class PrefabPauseButton : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("PositionXOffset", "-185")
    };
}

// Insert the Extra Fast-Forward button after the PauseButton in the CenterPanel children.
// Command.Click is the mixin's ExecuteExtraFastForward (#574), which writes the extra
// SpeedUpMultiplier before the vanilla mode change; bound straight to ExecuteTimeControlChange, as
// it was until then, the button was vanilla fast-forward under a different tooltip.
[PrefabExtension("MapBar", "descendant::ButtonWidget[@Id='PauseButton']")]
[CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")]
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
            " Command.Click=\"ExecuteExtraFastForward\"" +
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
