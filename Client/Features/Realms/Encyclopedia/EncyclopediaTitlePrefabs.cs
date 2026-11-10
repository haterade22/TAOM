using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Client.Features.Realms.Encyclopedia;

// Title sections on TAOM's themed encyclopedia pages (TAOM swaps the native movies for "TAOM" + name, with the native view
// models, so the mixins in EncyclopediaTitleMixins.cs bind here). TAOM's own files are not edited: these patches insert
// one wrapper node each, by Id anchors in TAOM's FactionUI prefabs. They reuse the brushes and sub-prefabs those pages
// already use (TAOM.Encyclopedia.* text brushes, the settlement divider, the native EncyclopediaDivider). Every section is
// a list of rows: a muted label and a value, gold when it is a link (the tree or claimants popup, or another page).
// If TAOM's FactionUI is off the native movies show and these do not apply.

internal static class EncyclopediaTitleXml
{
    public static IEnumerable<XmlNode> Nodes(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Root>" + xml + "</Root>");
        return doc.DocumentElement!.ChildNodes.Cast<XmlNode>().ToList();
    }

    /// <summary>The title's name as a gold link to its encyclopedia page.</summary>
    public static string NameLink(string margins) =>
        $"<ButtonWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" {margins} Brush=\"TAOM.Encyclopedia.TextButton\""
        + " Command.Click=\"ExecuteRealmsOpenTitle\" DoNotPassEventsToChildren=\"true\" UpdateChildrenStates=\"true\">"
        + "<Children>"
        + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" Text=\"@RealmsTitleName\""
        + " Brush=\"TAOM.Encyclopedia.ListActionText\" Brush.FontSize=\"25\" Brush.TextHorizontalAlignment=\"Left\" />"
        + "</Children>"
        + "</ButtonWidget>";

    /// <summary>The rows of <c>RealmsTitleRows</c>: label column, then the value, a link when <c>IsLink</c>.</summary>
    public static string Rows(int labelWidth, string margins) =>
        $"<ListPanel DataSource=\"{{RealmsTitleRows}}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" {margins}"
        + " StackLayout.LayoutMethod=\"VerticalTopToBottom\">"
        + "<ItemTemplate>"
        + "<ListPanel WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" MarginTop=\"3\" StackLayout.LayoutMethod=\"HorizontalLeftToRight\">"
        + "<Children>"
        + $"<TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"CoverChildren\" SuggestedWidth=\"{labelWidth}\" VerticalAlignment=\"Top\""
        + " Text=\"@LabelText\" Brush=\"TAOM.Encyclopedia.ListValue\" Brush.FontSize=\"19\" Brush.TextHorizontalAlignment=\"Left\" DoNotAcceptEvents=\"true\" />"
        + "<ButtonWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" VerticalAlignment=\"Top\" Brush=\"TAOM.Encyclopedia.TextButton\""
        + " Command.Click=\"ExecuteClick\" IsEnabled=\"@IsLink\" DoNotPassEventsToChildren=\"true\" UpdateChildrenStates=\"true\">"
        + "<Children>"
        + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" Text=\"@ValueText\" IsVisible=\"@IsLink\""
        + " Brush=\"TAOM.Encyclopedia.ListActionText\" Brush.FontSize=\"20\" Brush.TextHorizontalAlignment=\"Left\" />"
        + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" Text=\"@ValueText\" IsHidden=\"@IsLink\""
        + " Brush=\"TAOM.Encyclopedia.ListText\" Brush.FontSize=\"20\" Brush.TextHorizontalAlignment=\"Left\" />"
        + "</Children>"
        + "</ButtonWidget>"
        + "</Children>"
        + "</ListPanel>"
        + "</ItemTemplate>"
        + "</ListPanel>";
}

/// <summary>
/// Settlement page: a "Title" section appended to the ownership column, under the owner, with the same divider as Owner.
/// The column is a vertical ListPanel of fixed width (480) inside the page's scrolling body.
/// </summary>
[PrefabExtension("TAOMEncyclopediaSettlementPage", "descendant::ListPanel[@Id='OwnershipColumn']/Children")]
internal sealed class SettlementTitlePrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;
    public override int Index => int.MaxValue;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes() => EncyclopediaTitleXml.Nodes(
        "<ListPanel Id=\"RealmsTitleSection\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" MarginTop=\"18\""
        + " StackLayout.LayoutMethod=\"VerticalTopToBottom\" IsVisible=\"@RealmsHasTitle\">"
        + "<Children>"
        + "<TAOMEncyclopediaSettlementDivider Id=\"RealmsTitleDivider\" Parameter.Title=\"@RealmsTitleHeaderText\""
        + " Parameter.ItemList=\"..\\RealmsTitleList\" GamepadNavigationIndex=\"0\" />"
        + "<ListPanel Id=\"RealmsTitleList\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" MarginLeft=\"4\" MarginTop=\"6\""
        + " StackLayout.LayoutMethod=\"VerticalTopToBottom\">"
        + "<Children>"
        + EncyclopediaTitleXml.NameLink(string.Empty)
        + EncyclopediaTitleXml.Rows(160, "MarginTop=\"4\"")
        + "</Children>"
        + "</ListPanel>"
        + "</Children>"
        + "</ListPanel>");
}

/// <summary>
/// Hero page: a "Titles" section right after the clan section (the InfoContainer widget) in the right-hand scrolling list,
/// with the same divider as Clan and Family.
/// </summary>
[PrefabExtension("TAOMEncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
internal sealed class HeroTitlesPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes() => EncyclopediaTitleXml.Nodes(
        "<ListPanel Id=\"RealmsHeroTitles\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\""
        + " StackLayout.LayoutMethod=\"VerticalTopToBottom\" IsVisible=\"@RealmsHasTitles\">"
        + "<Children>"
        + "<EncyclopediaDivider Id=\"RealmsTitlesDivider\" MarginTop=\"20\" Parameter.Title=\"@RealmsTitlesHeaderText\""
        + " Parameter.ItemList=\"..\\RealmsTitlesContent\" GamepadNavigationIndex=\"0\" />"
        + "<ListPanel Id=\"RealmsTitlesContent\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" MarginLeft=\"15\" MarginTop=\"10\""
        + " StackLayout.LayoutMethod=\"VerticalTopToBottom\">"
        + "<Children>"
        + EncyclopediaTitleXml.Rows(120, string.Empty)
        + "</Children>"
        + "</ListPanel>"
        + "</Children>"
        + "</ListPanel>");
}

/// <summary>
/// Kingdom page: the realm title (a label, the name as a link to its page, then its rows) as the first entry of the
/// overview's scrolling description, above the native description text.
/// </summary>
[PrefabExtension("TAOMEncyclopediaFactionPage", "descendant::ListPanel[@Id='DescriptionList']/Children")]
internal sealed class KingdomTitlePrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;
    public override int Index => 0;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes() => EncyclopediaTitleXml.Nodes(
        "<ListPanel Id=\"RealmsRealmTitle\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" MarginRight=\"16\" MarginBottom=\"16\""
        + " StackLayout.LayoutMethod=\"VerticalTopToBottom\" IsVisible=\"@RealmsHasTitle\">"
        + "<Children>"
        + "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" Text=\"@RealmsRealmTitleLabel\""
        + " Brush=\"TAOM.Encyclopedia.ListValue\" Brush.FontSize=\"19\" Brush.TextHorizontalAlignment=\"Left\" DoNotAcceptEvents=\"true\" />"
        + EncyclopediaTitleXml.NameLink("MarginTop=\"2\"")
        + EncyclopediaTitleXml.Rows(160, "MarginTop=\"4\"")
        + "</Children>"
        + "</ListPanel>");
}
