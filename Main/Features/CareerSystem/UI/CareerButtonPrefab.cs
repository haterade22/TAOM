using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.CareerSystem.UI;

[PrefabExtension("CharacterDeveloper",
    "descendant::Widget[@Id='TopPanelParent']")]
internal class CareerButtonPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<ButtonWidget " +
            "WidthSizePolicy=\"Fixed\" " +
            "HeightSizePolicy=\"Fixed\" " +
            "SuggestedWidth=\"200\" " +
            "SuggestedHeight=\"40\" " +
            "HorizontalAlignment=\"Center\" " +
            "VerticalAlignment=\"Top\" " +
            "MarginTop=\"155\" " +
            "IsVisible=\"@HasCareer\" " +
            "Command.Click=\"ExecuteOpenCareerScreen\" " +
            "Brush=\"ButtonBrush1\">" +
            "<Children>" +
            "<TextWidget " +
            "WidthSizePolicy=\"StretchToParent\" " +
            "HeightSizePolicy=\"StretchToParent\" " +
            "Text=\"Career\" " +
            "Brush=\"ButtonBrush1.Text\" />" +
            "</Children>" +
            "</ButtonWidget>");
        return doc;
    }
}
