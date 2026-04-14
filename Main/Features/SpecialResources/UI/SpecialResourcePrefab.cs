using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.SpecialResources.UI;

// Inject resource display as a sibling of TopInfoBar/BottomInfoBar inside the
// vertical ListPanel that wraps them. This avoids touching SecondaryInfoItems
// (vanilla HandlePanelSwitchingInput indexes by position → IndexOutOfRangeException)
// and avoids appending into a data-bound ListPanel (BottomInfoBar is template-driven).
//
// Target: the unnamed ListPanel that is the first child of InfoBarWidget.
// XPath: InfoBarWidget > Children > ListPanel (first one, vertical stack).
[PrefabExtension("MapBar",
    "descendant::MapInfoBarWidget[@Id='InfoBarWidget']/Children/ListPanel")]
internal class SpecialResourceMapBarPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<ListPanel " +
            "WidthSizePolicy=\"CoverChildren\" " +
            "HeightSizePolicy=\"Fixed\" " +
            "SuggestedHeight=\"30\" " +
            "HorizontalAlignment=\"Center\" " +
            "VerticalAlignment=\"Bottom\" " +
            "MarginLeft=\"40\" " +
            "MarginRight=\"35\" " +
            "MarginBottom=\"-5\" " +
            "IsVisible=\"@IsResourceVisible\">" +
            "<Children>" +
            "<TextWidget " +
            "WidthSizePolicy=\"CoverChildren\" " +
            "HeightSizePolicy=\"CoverChildren\" " +
            "VerticalAlignment=\"Center\" " +
            "Text=\"@ResourceDisplayText\" " +
            "Brush=\"MapTextBrushWithAnim\" " +
            "Brush.FontSize=\"18\" />" +
            "</Children>" +
            "</ListPanel>");
        return doc;
    }
}
