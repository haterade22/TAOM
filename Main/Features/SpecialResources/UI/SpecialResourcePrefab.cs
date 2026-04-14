using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.SpecialResources.UI;

// Replace the IconBrushWidget inside the BottomInfoBar (SecondaryInfoItems) item template
// with our SpecialResourceSpriteWidget, which dynamically loads resource sprites.
// Normal icons fall through to IconBrushWidget base behavior; only "special_resource"
// triggers the custom sprite lookup.
[PrefabExtension("MapBar",
    "descendant::ListPanel[@Id='BottomInfoBar']/ItemTemplate/HintWidget/Children/ListPanel/Children/IconBrushWidget")]
internal class SpecialResourceIconPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Replace;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<SpecialResourceSpriteWidget" +
            " WidthSizePolicy=\"Fixed\"" +
            " HeightSizePolicy=\"Fixed\"" +
            " SuggestedWidth=\"33\"" +
            " SuggestedHeight=\"33\"" +
            " VerticalAlignment=\"Center\"" +
            " IconBrush=\"MapBar.Right.Icons\"" +
            " IconID=\"@VisualId\"" +
            " UseStylesFromSourceIcon=\"true\" />");
        return doc;
    }
}
