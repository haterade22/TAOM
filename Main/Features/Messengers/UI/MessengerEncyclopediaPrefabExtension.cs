using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.Messengers.UI;

[PrefabExtension("EncyclopediaHeroPage",
    "descendant::RichTextWidget[@Text='@InformationText']")]
internal class MessengerEncyclopediaPrefabExtension : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

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
            "<ButtonWidget" +
            " WidthSizePolicy=\"Fixed\"" +
            " HeightSizePolicy=\"Fixed\"" +
            " SuggestedWidth=\"227\"" +
            " SuggestedHeight=\"40\"" +
            " HorizontalAlignment=\"Center\"" +
            " VerticalAlignment=\"Center\"" +
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
            "<HintWidget" +
            " WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\"" +
            " DataSource=\"{SendMessengerHint}\"" +
            " Command.HoverBegin=\"ExecuteBeginHint\"" +
            " Command.HoverEnd=\"ExecuteEndHint\"" +
            " IsDisabled=\"true\" />" +
            "</Children>" +
            "</ButtonWidget>" +
            "</Children>" +
            "</ListPanel>");
        return doc;
    }
}
