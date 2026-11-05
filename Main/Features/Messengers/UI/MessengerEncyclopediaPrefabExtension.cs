using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.Messengers.UI;

[PrefabExtension("EncyclopediaHeroPage",
    "descendant::RichTextWidget[@Text='@InformationText']")]
internal class MessengerEncyclopediaPrefabExtension : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    // Vanilla's governor-button shape (SandBox TownManagement.xml:105-111, #944): Gauntlet never hovers a disabled
    // widget, so the disabled-state hint is a sibling of the ButtonWidget inside a wrapper that stays enabled.
    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<ListPanel" +
            " WidthSizePolicy=\"CoverChildren\"" +
            " HeightSizePolicy=\"CoverChildren\"" +
            " HorizontalAlignment=\"Center\"" +
            " VerticalAlignment=\"Center\"" +
            " MarginTop=\"10\"" +
            " LayoutImp.LayoutMethod=\"VerticalBottomToTop\">" +
            "<Children>" +
            "<Widget" +
            " WidthSizePolicy=\"Fixed\"" +
            " HeightSizePolicy=\"Fixed\"" +
            " SuggestedWidth=\"227\"" +
            " SuggestedHeight=\"40\"" +
            " HorizontalAlignment=\"Center\"" +
            " VerticalAlignment=\"Center\">" +
            "<Children>" +
            "<ButtonWidget" +
            " WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\"" +
            " IsEnabled=\"@IsMessengerAvailable\"" +
            " Command.Click=\"ExecuteSendMessenger\"" +
            " DoNotPassEventsToChildren=\"true\"" +
            " Brush=\"Popup.PartySelection.Confirm\">" +
            "<Children>" +
            "<TextWidget" +
            " WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\"" +
            " Text=\"@SendMessengerActionName\"" +
            " Brush=\"Popup.PartySelection.Button.Text\"" +
            " VerticalAlignment=\"Center\"" +
            " HorizontalAlignment=\"Center\" />" +
            "</Children>" +
            "</ButtonWidget>" +
            "<HintWidget" +
            " WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\"" +
            " DataSource=\"{SendMessengerHint}\"" +
            " Command.HoverBegin=\"ExecuteBeginHint\"" +
            " Command.HoverEnd=\"ExecuteEndHint\"" +
            " IsDisabled=\"true\" />" +
            "</Children>" +
            "</Widget>" +
            "</Children>" +
            "</ListPanel>");
        return doc;
    }
}
