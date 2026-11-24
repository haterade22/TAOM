using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Which group an image belongs to decides how long it stays in memory, so the split is
/// pinned name by name: the loading-screen frame stays resident, the skill icons replace vanilla's for
/// the session, the large faction art loads one image at a time, and everything else is shared UI
/// chrome held only while a themed screen is open.
/// </summary>
[TestClass]
public class FrontEndSpriteCatalogTests
{
    [DataTestMethod]
    [DataRow("ld_bar_stone", FrontEndSpriteKind.Resident)]
    [DataRow("gui_skills_icon_onehanded", FrontEndSpriteKind.SkillIcon)]
    [DataRow("gui_skills_icon_bow_tiny", FrontEndSpriteKind.SkillIcon)]
    [DataRow("fs_portrait_aragorn", FrontEndSpriteKind.Art)]
    [DataRow("fs_portrait_gondor", FrontEndSpriteKind.Art)]
    [DataRow("fs_reveal_sauron", FrontEndSpriteKind.Art)]
    [DataRow("fs_emblem_kingdom_of_rohan", FrontEndSpriteKind.Art)]
    [DataRow("fs_territory_havens_of_umbar", FrontEndSpriteKind.Art)]
    [DataRow("fs_bg_default", FrontEndSpriteKind.Art)]
    [DataRow("fs_minimap_big", FrontEndSpriteKind.Chrome)]
    [DataRow("fs_minimap", FrontEndSpriteKind.Chrome)]
    [DataRow("fs_confirm", FrontEndSpriteKind.Chrome)]
    [DataRow("cc_panel_bg", FrontEndSpriteKind.Chrome)]
    [DataRow("mm_logo", FrontEndSpriteKind.MenuChrome)]
    [DataRow("mm_btn_featured", FrontEndSpriteKind.MenuChrome)]
    public void Classify_ByName_ReturnsTheGroupThatControlsItsLifetime(string name, FrontEndSpriteKind expected)
    {
        Assert.AreEqual(expected, FrontEndSpriteCatalog.Classify(name));
    }

    [TestMethod]
    public void TryParseNinePatch_FourIntegers_ReadsThemInFileOrder()
    {
        Assert.IsTrue(FrontEndSpriteCatalog.TryParseNinePatch("12 34\r\n5,6", out var nine));

        Assert.AreEqual(new NinePatch(12, 34, 5, 6), nine);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("1 2 3")]
    [DataRow("1 2 3 4 5")]
    [DataRow("1 2 x 4")]
    [DataRow("1 -2 3 4")]
    public void TryParseNinePatch_AnythingButFourNonNegativeIntegers_IsRejected(string? text)
    {
        Assert.IsFalse(FrontEndSpriteCatalog.TryParseNinePatch(text, out _));
    }
}
