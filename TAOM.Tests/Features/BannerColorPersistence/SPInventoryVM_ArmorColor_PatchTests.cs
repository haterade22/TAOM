using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;

namespace TAOM.Tests.Features.BannerColorPersistence;

/// <summary>
/// #973: the inventory doll keeps the clan colours on every refresh, not only a character switch. The postfix sits
/// on the private <c>SPInventoryVM.UpdateCharacterArmorColor</c> (v1.4.8 :4421), the vanilla repaint of the doll's
/// colours that <c>UpdateRightCharacter</c> reaches (:4381), and leaves vanilla's stealth colour alone (:4423-4427).
/// Engine objects are built bare.
/// </summary>
[TestClass]
public class SPInventoryVM_ArmorColor_PatchTests
{
    private const uint ClanColor1 = 0xFF112233;
    private const uint ClanColor2 = 0xFF445566;
    private const uint VanillaColor = 0xFF999999;

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{name} is gone; the test cannot stage the screen.");
        field!.SetValue(target, value);
    }

    private static SPInventoryVM Screen(CharacterObject character)
    {
        var vm = Bare<SPInventoryVM>();
        SetField(vm, "_currentCharacter", character);
        vm.MainCharacter = Bare<HeroViewModel>();
        vm.MainCharacter.ArmorColor1 = VanillaColor;
        vm.MainCharacter.ArmorColor2 = VanillaColor;
        return vm;
    }

    private static void InitializeWithClanColours(CharacterObject character)
    {
        var service = Substitute.For<IBannerColorService>();
        var heroes = Substitute.For<IBannerHeroAdapter>();
        var info = new ClanColorInfo("clan_test", ClanColor1, ClanColor2);
        heroes.GetClanColorInfo(character).Returns(info);
        service.ShouldUseClanColor(info).Returns(true);
        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(service, heroes);
    }

    [TestMethod]
    public void TargetMethod_IsUpdateCharacterArmorColor_SoEveryDollRefreshIsCovered()
    {
        var target = SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.TargetMethod();

        Assert.IsNotNull(target, "SPInventoryVM.UpdateCharacterArmorColor() no longer binds.");
        Assert.AreEqual("UpdateCharacterArmorColor", target.Name);
    }

    [TestMethod]
    public void Postfix_OutsideStealth_PaintsTheClanColours()
    {
        var character = Bare<CharacterObject>();
        InitializeWithClanColours(character);
        var vm = Screen(character); // a bare screen is in civilian mode

        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Postfix(vm);

        Assert.AreEqual(ClanColor1, vm.MainCharacter.ArmorColor1);
        Assert.AreEqual(ClanColor2, vm.MainCharacter.ArmorColor2);
    }

    [TestMethod]
    public void Postfix_InStealthMode_KeepsVanillasStealthColour()
    {
        var hero = Bare<Hero>();
        var stealth = typeof(Hero).GetProperty("_stealthEquipment", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(stealth, "Hero._stealthEquipment is gone; the test cannot stage the stealth outfit.");
        stealth!.SetValue(hero, new Equipment(Equipment.EquipmentType.Stealth));
        var character = Bare<CharacterObject>();
        SetField(character, "_heroObject", hero);
        InitializeWithClanColours(character);
        var vm = Screen(character);
        SetField(vm, "_equipmentMode", SPInventoryVM.EquipmentModes.Stealth);

        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Postfix(vm);

        Assert.AreEqual(VanillaColor, vm.MainCharacter.ArmorColor1, "The stealth outfit must keep vanilla's colour.");
        Assert.AreEqual(VanillaColor, vm.MainCharacter.ArmorColor2);
    }

    [TestMethod]
    public void Postfix_InStealthModeWithASetNotTypedStealth_PaintsTheClanColours()
    {
        // Vanilla's guard is ActiveEquipment.IsStealth, not the mode: a non-stealth set gets the faction branch (:4428-4432).
        var hero = Bare<Hero>();
        var stealth = typeof(Hero).GetProperty("_stealthEquipment", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(stealth, "Hero._stealthEquipment is gone; the test cannot stage the outfit.");
        stealth!.SetValue(hero, new Equipment(Equipment.EquipmentType.Battle));
        var character = Bare<CharacterObject>();
        SetField(character, "_heroObject", hero);
        InitializeWithClanColours(character);
        var vm = Screen(character);
        SetField(vm, "_equipmentMode", SPInventoryVM.EquipmentModes.Stealth);

        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Postfix(vm);

        Assert.AreEqual(ClanColor1, vm.MainCharacter.ArmorColor1);
        Assert.AreEqual(ClanColor2, vm.MainCharacter.ArmorColor2);
    }

    [TestMethod]
    public void Postfix_ClanColourNotWanted_KeepsVanillasColours()
    {
        // ShouldUseClanColor is false when the feature is off or the clan has no custom colours.
        var character = Bare<CharacterObject>();
        var service = Substitute.For<IBannerColorService>();
        var heroes = Substitute.For<IBannerHeroAdapter>();
        heroes.GetClanColorInfo(character).Returns(new ClanColorInfo("clan_test", ClanColor1, ClanColor2));
        service.ShouldUseClanColor(Arg.Any<ClanColorInfo>()).Returns(false);
        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(service, heroes);
        var vm = Screen(character);

        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Postfix(vm);

        Assert.AreEqual(VanillaColor, vm.MainCharacter.ArmorColor1);
        Assert.AreEqual(VanillaColor, vm.MainCharacter.ArmorColor2);
    }
}
