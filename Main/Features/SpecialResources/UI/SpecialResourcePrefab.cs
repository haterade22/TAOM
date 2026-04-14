using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.SpecialResources.UI;

// Inject a resource display widget into the MapBar, positioned next to existing info items.
// Uses bound properties from SpecialResourceMapBarMixin instead of modifying SecondaryInfoItems
// (which causes IndexOutOfRangeException in vanilla's HandlePanelSwitchingInput).
[PrefabExtension("MapBar",
    "descendant::ListPanel[@Id='BottomInfoBar']")]
internal class SpecialResourceMapBarPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<Widget " +
            "WidthSizePolicy=\"CoverChildren\" " +
            "HeightSizePolicy=\"Fixed\" " +
            "SuggestedHeight=\"40\" " +
            "VerticalAlignment=\"Center\" " +
            "MarginLeft=\"10\" " +
            "IsVisible=\"@IsResourceVisible\">" +
            "<Children>" +
            "<TextWidget " +
            "WidthSizePolicy=\"CoverChildren\" " +
            "HeightSizePolicy=\"CoverChildren\" " +
            "VerticalAlignment=\"Center\" " +
            "Text=\"@ResourceDisplayText\" " +
            "Brush=\"MapBar.Right.TupleValue.Text\" />" +
            "</Children>" +
            "</Widget>");
        return doc;
    }
}
