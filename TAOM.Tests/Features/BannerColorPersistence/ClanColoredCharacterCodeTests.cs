using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.BannerColorPersistence;

/// <summary>
/// #993: <c>CharacterCode.Code</c> (private setter) is built when the code is created and is the only
/// field the portrait renderer and <c>CharacterImageIdentifier</c> read, so assigning Color1/Color2
/// afterwards never reached the screen. The helper rebuilds the code through the 8-argument
/// <c>CreateFrom</c>, which bakes the colours into <c>Code</c>.
/// </summary>
[TestClass]
public class ClanColoredCharacterCodeTests
{
    private const uint Red = 0xFFAA0000u;
    private const uint Blue = 0xFF0000AAu;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static CharacterCode WhiteCode() =>
        CharacterCode.CreateFrom("equip_code", default(BodyProperties), true, true,
            Color.White.ToUnsignedInteger(), Color.White.ToUnsignedInteger(), FormationClass.Cavalry, 3);

    [TestMethod]
    public void Recolor_WhiteCode_BakesTheNewColoursIntoTheCodeString()
    {
        RequireGame();
        var source = WhiteCode();

        var result = ClanColoredCharacterCode.Recolor(source, Red, Blue);

        StringAssert.Contains(result.Code, "@---@" + Red + "@---@" + Blue + "@---@");
        Assert.AreNotEqual(source.Code, result.Code);
        Assert.AreEqual(Red, result.Color1);
        Assert.AreEqual(Blue, result.Color2);
    }

    [TestMethod]
    public void Recolor_WhiteCode_KeepsEveryOtherField()
    {
        RequireGame();
        var source = WhiteCode();

        var result = ClanColoredCharacterCode.Recolor(source, Red, Blue);

        Assert.AreEqual("equip_code", result.EquipmentCode);
        Assert.AreEqual(source.BodyProperties, result.BodyProperties);
        Assert.IsTrue(result.IsFemale);
        Assert.IsTrue(result.IsHero);
        Assert.AreEqual(FormationClass.Cavalry, result.FormationClass);
        Assert.AreEqual(3, result.Race);
    }

    [TestMethod]
    public void Recolor_CodeAlreadyCarryingBothColours_ReturnsTheSameInstance()
    {
        RequireGame();
        var coloured = ClanColoredCharacterCode.Recolor(WhiteCode(), Red, Blue);

        var again = ClanColoredCharacterCode.Recolor(coloured, Red, Blue);

        Assert.AreSame(coloured, again);
    }

    [TestMethod]
    public void Recolor_NullCode_ReturnsNull()
    {
        RequireGame();
        Assert.IsNull(ClanColoredCharacterCode.Recolor(null, Red, Blue));
    }

    [TestMethod]
    public void Recolor_EmptyCode_ReturnsTheSameInstance()
    {
        RequireGame();
        // Hidden hero information returns CreateEmpty; an empty code must stay empty.
        var empty = CharacterCode.CreateEmpty();

        var result = ClanColoredCharacterCode.Recolor(empty, Red, Blue);

        Assert.AreSame(empty, result);
        Assert.IsTrue(result.IsEmpty);
    }

    [TestMethod]
    public void PartyPatch_TargetAndPostfix_ShareTheTypeAndSideParameterNames()
    {
        RequireGame();
        // The postfix reads the target's own arguments because PartyCharacterVM sets IsPrisoner only
        // after the Troop assignment has run GetCharacterCode (a Harmony parameter is matched by name).
        var target = PartyCharacterVM_GetCharacterCode_Patch.TargetMethod();
        var postfix = typeof(PartyCharacterVM_GetCharacterCode_Patch).GetMethod("Postfix")!;
        var targetNames = target.GetParameters().Select(p => p.Name).ToArray();

        CollectionAssert.Contains(targetNames, "type");
        CollectionAssert.Contains(targetNames, "side");
        foreach (var name in new[] { "type", "side" })
            Assert.IsTrue(postfix.GetParameters().Any(p => p.Name == name),
                "Postfix lost its '" + name + "' parameter; Harmony would bind nothing.");
        Assert.IsFalse(postfix.GetParameters().Any(p => p.Name == "__instance"),
            "`PartyCharacterVM` sets `IsPrisoner` (:1103) only after the `Troop` assignment (:1099) has run "
            + "`GetCharacterCode`, so the old prisoner check always read false; the postfix reads type and side from the arguments.");
    }

    [TestMethod]
    public void PartyPatch_AppliesTo_SkipsPrisonersAndLeftSide()
    {
        RequireGame();
        Assert.IsTrue(PartyCharacterVM_GetCharacterCode_Patch.AppliesTo(
            PartyScreenLogic.TroopType.Member, PartyScreenLogic.PartyRosterSide.Right));
        Assert.IsFalse(PartyCharacterVM_GetCharacterCode_Patch.AppliesTo(
            PartyScreenLogic.TroopType.Prisoner, PartyScreenLogic.PartyRosterSide.Right));
        Assert.IsFalse(PartyCharacterVM_GetCharacterCode_Patch.AppliesTo(
            PartyScreenLogic.TroopType.Member, PartyScreenLogic.PartyRosterSide.Left));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CharacterCode_EightArgumentCreateFrom_ResolvesAndMembersStayPublic()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var method = AccessTools.Method(typeof(CharacterCode), "CreateFrom", new[]
        {
            typeof(string), typeof(BodyProperties), typeof(bool), typeof(bool),
            typeof(uint), typeof(uint), typeof(FormationClass), typeof(int),
        });
        Assert.IsNotNull(method, "The 8-argument CharacterCode.CreateFrom is gone; the recolour helper cannot bake colours.");
        foreach (var name in new[] { "EquipmentCode", "IsFemale", "IsHero", "FormationClass", "Race", "Color1", "Color2", "Code" })
            Assert.IsNotNull(typeof(CharacterCode).GetProperty(name, BindingFlags.Public | BindingFlags.Instance),
                "CharacterCode." + name + " is no longer public.");
        Assert.IsNotNull(typeof(CharacterCode).GetField("BodyProperties", BindingFlags.Public | BindingFlags.Instance),
            "CharacterCode.BodyProperties is no longer a public field.");
        Assert.IsNotNull(typeof(CharacterCode).GetProperty("IsEmpty", BindingFlags.Public | BindingFlags.Instance),
            "CharacterCode.IsEmpty is no longer a public property.");
    }
}
