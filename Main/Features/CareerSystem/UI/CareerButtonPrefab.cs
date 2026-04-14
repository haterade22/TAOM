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
            "SuggestedWidth=\"233\" " +
            "SuggestedHeight=\"75\" " +
            "HorizontalAlignment=\"Center\" " +
            "VerticalAlignment=\"Top\" " +
            "MarginTop=\"150\" " +
            "IsVisible=\"@HasCareer\" " +
            "Command.Click=\"ExecuteOpenCareerScreen\" " +
            "DoNotPassEventsToChildren=\"true\" " +
            "Sprite=\"CareerSystem\\career_button_placeholder\">" +
            "<Children>" +
            "<TextWidget " +
            "WidthSizePolicy=\"StretchToParent\" " +
            "HeightSizePolicy=\"StretchToParent\" " +
            "Text=\"Career\" " +
            "Brush=\"ButtonBrush1.Text\" " +
            "VerticalAlignment=\"Center\" " +
            "HorizontalAlignment=\"Center\" />" +
            "</Children>" +
            "</ButtonWidget>");
        return doc;
    }
}
