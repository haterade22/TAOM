using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.SpecialResources.UI;

// v1.5.0 restructured this template: the ListPanel is now the DIRECT child of ItemTemplate and
// HintWidget is an inner sibling leaf, where it used to be the outer wrapper. The old path matched
// nothing, and because this is an InsertType.Replace patch it failed as a SILENT no-op: vanilla's
// plain IconBrushWidget stayed and SpecialResourceSpriteWidget never installed. UIExtenderEx logs
// one red in-game message at movie load and nothing else, so this is invisible offline.
// Replace the IconBrushWidget inside the BottomInfoBar (SecondaryInfoItems) item template
// with our SpecialResourceSpriteWidget, which dynamically loads resource sprites.
// Normal icons fall through to IconBrushWidget base behavior; only "special_resource"
// triggers the custom sprite lookup.
[PrefabExtension("MapBar",
    "descendant::ListPanel[@Id='BottomInfoBar']/ItemTemplate/ListPanel/Children/IconBrushWidget")]
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
