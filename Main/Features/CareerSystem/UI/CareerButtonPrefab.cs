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
        // Issue #378 — Id gives external tooling a stable anchor (pre-fix this was "the
        // id-less ButtonWidget among its siblings"); the Brush (not a bare Sprite) is what
        // gives Gauntlet its Default/Hovered/Pressed visual states.
        // Issue #379 — the unspent-points badge is a child of the button; the button's
        // DoNotPassEventsToChildren keeps the whole area one click target. A plain Widget
        // (not ImageWidget) multiplies Sprite × Color, which is what tints the disc red.
        var doc = new XmlDocument();
        doc.LoadXml(
            "<ButtonWidget " +
            "Id=\"TaomCareerButton\" " +
            "WidthSizePolicy=\"Fixed\" " +
            "HeightSizePolicy=\"Fixed\" " +
            "SuggestedWidth=\"220\" " +
            "SuggestedHeight=\"93\" " +
            "HorizontalAlignment=\"Center\" " +
            "VerticalAlignment=\"Top\" " +
            "MarginTop=\"30\" " +
            "PositionXOffset=\"500\" " +
            "IsVisible=\"@HasCareer\" " +
            "Command.Click=\"ExecuteOpenCareerScreen\" " +
            "DoNotPassEventsToChildren=\"true\" " +
            "Brush=\"CareerSystem.CareerButton\">" +
            "<Children>" +
            "<Widget " +
            "WidthSizePolicy=\"Fixed\" " +
            "HeightSizePolicy=\"Fixed\" " +
            "SuggestedWidth=\"36\" " +
            "SuggestedHeight=\"36\" " +
            "HorizontalAlignment=\"Right\" " +
            "VerticalAlignment=\"Top\" " +
            "PositionXOffset=\"10\" " +
            "PositionYOffset=\"-8\" " +
            "Sprite=\"BlankWhiteCircle\" " +
            "Color=\"#C43B2FFF\" " +
            "IsVisible=\"@HasUnspentPoints\">" +
            "<Children>" +
            "<TextWidget " +
            "WidthSizePolicy=\"StretchToParent\" " +
            "HeightSizePolicy=\"StretchToParent\" " +
            "HorizontalAlignment=\"Center\" " +
            "VerticalAlignment=\"Center\" " +
            "Text=\"@UnspentPointsText\" " +
            "Brush=\"CareerSystem.Badge.Text\" />" +
            "</Children>" +
            "</Widget>" +
            "</Children>" +
            "</ButtonWidget>");
        return doc;
    }
}
