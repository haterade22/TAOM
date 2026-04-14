using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.SpecialResources.UI;

// Inject resource display as a sibling of BottomInfoBar inside the vertical
// ListPanel, positioned to appear inline at the right end of the bottom row.
// Cannot add to SecondaryInfoItems (vanilla indexes by position → crash).
// Cannot append children to BottomInfoBar (data-bound template ListPanel).
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
            "SuggestedHeight=\"40\" " +
            "HorizontalAlignment=\"Right\" " +
            "VerticalAlignment=\"Bottom\" " +
            "MarginRight=\"40\" " +
            "StackLayout.LayoutMethod=\"HorizontalLeftToRight\" " +
            "IsVisible=\"@IsResourceVisible\">" +
            "<Children>" +
            "<Widget " +
            "WidthSizePolicy=\"Fixed\" " +
            "HeightSizePolicy=\"Fixed\" " +
            "SuggestedWidth=\"33\" " +
            "SuggestedHeight=\"33\" " +
            "VerticalAlignment=\"Center\" " +
            "MarginRight=\"3\" " +
            "Sprite=\"@ResourceIconSprite\" />" +
            "<TextWidget " +
            "WidthSizePolicy=\"CoverChildren\" " +
            "HeightSizePolicy=\"Fixed\" " +
            "SuggestedHeight=\"40\" " +
            "VerticalAlignment=\"Center\" " +
            "PositionYOffset=\"2\" " +
            "Text=\"@ResourceDisplayText\" " +
            "Brush=\"MapTextBrushWithAnim\" " +
            "Brush.FontSize=\"20\" />" +
            "</Children>" +
            "</ListPanel>");
        return doc;
    }
}
