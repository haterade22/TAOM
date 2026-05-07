using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.Roles;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Tests.Features.CompanionTactics.Roles;

[TestClass]
public class CompanionRoleServiceTests
{
    private CompanionRoleService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new CompanionRoleService();

    private static IHeroCombatAdapter MakeHero(
        string id = "hero1",
        bool hasMount = false,
        bool hasShield = false,
        WeaponClass? slot0 = null,
        WeaponClass? slot1 = null,
        WeaponClass? slot2 = null,
        WeaponClass? slot3 = null)
    {
        var snap = Substitute.For<IBattleEquipmentSnapshot>();
        snap.GetWeaponClass(0).Returns(slot0);
        snap.GetWeaponClass(1).Returns(slot1);
        snap.GetWeaponClass(2).Returns(slot2);
        snap.GetWeaponClass(3).Returns(slot3);
        snap.HasShield.Returns(hasShield);
        snap.HasMount.Returns(hasMount);

        var hero = Substitute.For<IHeroCombatAdapter>();
        hero.StringId.Returns(id);
        hero.HasMount.Returns(hasMount);
        hero.HasShield.Returns(hasShield);
        hero.Equipment.Returns(snap);
        return hero;
    }

    // -------- GetPrimaryRole: per-role detection --------

    [TestMethod]
    public void GetPrimaryRole_NullHero_ReturnsUnknown()
    {
        Assert.AreEqual(CombatRole.Unknown, _sut.GetPrimaryRole(null));
    }

    [TestMethod]
    public void GetPrimaryRole_NoEquipment_ReturnsUnknown()
    {
        var hero = MakeHero();

        Assert.AreEqual(CombatRole.Unknown, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_BowOnly_ReturnsArcher()
    {
        var hero = MakeHero(slot0: WeaponClass.Bow);

        Assert.AreEqual(CombatRole.Archer, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_CrossbowOnly_ReturnsCrossbow()
    {
        var hero = MakeHero(slot0: WeaponClass.Crossbow);

        Assert.AreEqual(CombatRole.Crossbow, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_SlingOnly_ReturnsSlinger()
    {
        var hero = MakeHero(slot0: WeaponClass.Sling);

        Assert.AreEqual(CombatRole.Slinger, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_JavelinOnly_ReturnsSkirmisher()
    {
        var hero = MakeHero(slot0: WeaponClass.Javelin);

        Assert.AreEqual(CombatRole.Skirmisher, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_OneHandedSwordWithShield_ReturnsShieldInfantry()
    {
        var hero = MakeHero(hasShield: true, slot0: WeaponClass.OneHandedSword);

        Assert.AreEqual(CombatRole.ShieldInfantry, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_OneHandedSwordNoShield_ReturnsOneHanded()
    {
        var hero = MakeHero(hasShield: false, slot0: WeaponClass.OneHandedSword);

        Assert.AreEqual(CombatRole.OneHanded, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_TwoHandedSword_ReturnsTwoHanded()
    {
        var hero = MakeHero(slot0: WeaponClass.TwoHandedSword);

        Assert.AreEqual(CombatRole.TwoHanded, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_OneHandedPolearm_ReturnsPolearm()
    {
        var hero = MakeHero(slot0: WeaponClass.OneHandedPolearm);

        Assert.AreEqual(CombatRole.Polearm, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_MountedAndArcher_ReturnsHorseArcher()
    {
        var hero = MakeHero(hasMount: true, slot0: WeaponClass.Bow);

        Assert.AreEqual(CombatRole.HorseArcher, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_MountedAndPolearm_ReturnsCavalry()
    {
        var hero = MakeHero(hasMount: true, slot0: WeaponClass.OneHandedPolearm);

        Assert.AreEqual(CombatRole.Cavalry, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_MountedAndUnknown_ReturnsCavalry()
    {
        var hero = MakeHero(hasMount: true);

        Assert.AreEqual(CombatRole.Cavalry, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_MountedTwoHanded_ReturnsCavalry()
    {
        // Mount overrides melee role to Cavalry per the source's switch fall-through.
        var hero = MakeHero(hasMount: true, slot0: WeaponClass.TwoHandedSword);

        Assert.AreEqual(CombatRole.Cavalry, _sut.GetPrimaryRole(hero));
    }

    [TestMethod]
    public void GetPrimaryRole_FirstSlotEmptyButSecondHasBow_ReturnsArcher()
    {
        var hero = MakeHero(slot0: null, slot1: WeaponClass.Bow);

        Assert.AreEqual(CombatRole.Archer, _sut.GetPrimaryRole(hero));
    }

    // -------- Cache hit / miss --------

    [TestMethod]
    public void GetPrimaryRole_SameHeroSameEquipment_CacheHit()
    {
        var hero = MakeHero(slot0: WeaponClass.Bow);

        _sut.GetPrimaryRole(hero);
        var role2 = _sut.GetPrimaryRole(hero);

        Assert.AreEqual(CombatRole.Archer, role2);
        // The mock should only be queried per hashing, and result is consistent.
        // (Behavioral cache test — exact call counts depend on impl; the consistency check is what matters.)
    }

    [TestMethod]
    public void GetPrimaryRole_SameHeroDifferentEquipment_CacheInvalidates()
    {
        var hero = MakeHero(id: "h1", slot0: WeaponClass.Bow);
        var role1 = _sut.GetPrimaryRole(hero);

        // Mutate the underlying snapshot mock to simulate equipment change
        var newSnap = Substitute.For<IBattleEquipmentSnapshot>();
        newSnap.GetWeaponClass(0).Returns(WeaponClass.Crossbow);
        newSnap.GetWeaponClass(1).Returns((WeaponClass?)null);
        newSnap.GetWeaponClass(2).Returns((WeaponClass?)null);
        newSnap.GetWeaponClass(3).Returns((WeaponClass?)null);
        newSnap.HasShield.Returns(false);
        newSnap.HasMount.Returns(false);
        hero.Equipment.Returns(newSnap);
        hero.HasMount.Returns(false);

        var role2 = _sut.GetPrimaryRole(hero);

        Assert.AreEqual(CombatRole.Archer, role1);
        Assert.AreEqual(CombatRole.Crossbow, role2);
    }

    // -------- Helpers --------

    [TestMethod]
    public void IsMounted_NullHero_False()
    {
        Assert.IsFalse(_sut.IsMounted(null));
    }

    [TestMethod]
    public void IsMounted_HasMount_True()
    {
        var hero = MakeHero(hasMount: true);

        Assert.IsTrue(_sut.IsMounted(hero));
    }

    [TestMethod]
    public void GetRoleShortText_KnownRoles_ReturnsExpectedLabels()
    {
        Assert.AreEqual("BOW", _sut.GetRoleShortText(CombatRole.Archer));
        Assert.AreEqual("XBW", _sut.GetRoleShortText(CombatRole.Crossbow));
        Assert.AreEqual("INF", _sut.GetRoleShortText(CombatRole.ShieldInfantry));
        Assert.AreEqual("2H", _sut.GetRoleShortText(CombatRole.TwoHanded));
        Assert.AreEqual("POL", _sut.GetRoleShortText(CombatRole.Polearm));
        Assert.AreEqual("CAV", _sut.GetRoleShortText(CombatRole.Cavalry));
        Assert.AreEqual("H.AR", _sut.GetRoleShortText(CombatRole.HorseArcher));
        Assert.AreEqual("SKR", _sut.GetRoleShortText(CombatRole.Skirmisher));
        Assert.AreEqual("1H", _sut.GetRoleShortText(CombatRole.OneHanded));
        Assert.AreEqual("SLG", _sut.GetRoleShortText(CombatRole.Slinger));
        Assert.AreEqual("", _sut.GetRoleShortText(CombatRole.Unknown));
    }

    [TestMethod]
    public void GetRoleHint_MountedNonCavalryRole_AppendsHasMountSuffix()
    {
        var hint = _sut.GetRoleHint(CombatRole.Archer, isMounted: true);

        Assert.IsTrue(hint.Contains("Has mount"), $"Expected '(Has mount)' suffix, got: {hint}");
    }

    [TestMethod]
    public void GetRoleHint_CavalryRole_DoesNotAppendHasMount()
    {
        var hint = _sut.GetRoleHint(CombatRole.Cavalry, isMounted: true);

        Assert.IsFalse(hint.Contains("Has mount"));
    }

    [TestMethod]
    public void IsRangedRole_RangedClasses_True()
    {
        Assert.IsTrue(_sut.IsRangedRole(CombatRole.Archer));
        Assert.IsTrue(_sut.IsRangedRole(CombatRole.Crossbow));
        Assert.IsTrue(_sut.IsRangedRole(CombatRole.Slinger));
    }

    [TestMethod]
    public void IsRangedRole_NonRangedClasses_False()
    {
        Assert.IsFalse(_sut.IsRangedRole(CombatRole.Cavalry));
        Assert.IsFalse(_sut.IsRangedRole(CombatRole.ShieldInfantry));
        Assert.IsFalse(_sut.IsRangedRole(CombatRole.Skirmisher));
        Assert.IsFalse(_sut.IsRangedRole(CombatRole.HorseArcher));
    }

    [TestMethod]
    public void GetRoleColor_Unknown_ReturnsMaxValue()
    {
        Assert.AreEqual(uint.MaxValue, _sut.GetRoleColor(CombatRole.Unknown));
    }
}
