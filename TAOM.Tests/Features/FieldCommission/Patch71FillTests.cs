using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.FieldCommission.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FieldCommission;

/// <summary>
/// Behavioural coverage for the one piece of Patch71 that can run without a campaign (#486).
/// <c>Prefix</c> needs a live <c>Hero</c>, but <c>Fill</c> takes four <c>Equipment</c> objects, and
/// <c>Equipment</c> is constructible standalone: its ctor allocates a 12-slot array, and
/// <c>FillFrom</c> only calls <c>IsItemFitsToSlot</c>, which returns true for a null item without
/// touching <c>Game.Current</c>. So the two things most worth pinning are pinned here rather than
/// left to the in-game smoke: the shared-singleton guard, and the equipment-type flag.
/// </summary>
[TestClass]
public class Patch71FillTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Equipment New(Equipment.EquipmentType type) => new Equipment(type);

    [TestMethod]
    public void Fill_TargetIsTheCampaignWideSingleton_IsLeftUnwritten()
    {
        RequireGame();
        // The whole reason the guard exists: Hero.CivilianEquipment returns
        // Campaign.Current.DeadCivilianEquipment when the backing field is null, and that object is
        // shared by every consumer. Filling it would re-equip the campaign, not the hero.
        var shared = New(Equipment.EquipmentType.Civilian);
        var source = New(Equipment.EquipmentType.Battle);

        Patch71_HeroResetEquipmentsGuard.Fill(shared, source, source, sharedDefault: shared);

        // The type field is the only mutation observable here: both equipments start with all 12
        // slots empty, so a slot-level write leaves no trace either way. It is a sound proxy because
        // KeepsSourceEquipmentType is tied to slot selection, and EquipmentResetPlanTests pins that
        // coupling exhaustively. Without the guard this flips Civilian to Battle.
        Assert.AreEqual(Equipment.EquipmentType.Civilian, shared.ItemEquipmentType,
            "The shared singleton's type flipped, so the guard let a write through. On the real object that silently re-equips every consumer reading it.");
    }

    [TestMethod]
    public void Fill_NullTarget_DoesNotThrow()
    {
        RequireGame();
        var source = New(Equipment.EquipmentType.Battle);

        // sharedDefault MUST be non-null here. With both null, ReferenceEquals(null, null) is true
        // and short-circuits the guard on its own, so the test would stay green even if the
        // `target == null` clause it exists to pin were deleted outright.
        Patch71_HeroResetEquipmentsGuard.Fill(null, source, source, sharedDefault: New(Equipment.EquipmentType.Battle));
    }

    [TestMethod]
    public void Fill_TemplateSuppliesTheSlot_CopiesTheSourceEquipmentType()
    {
        RequireGame();
        var target = New(Equipment.EquipmentType.Battle);
        var civilian = New(Equipment.EquipmentType.Civilian);

        Patch71_HeroResetEquipmentsGuard.Fill(target, civilian, New(Equipment.EquipmentType.Battle), sharedDefault: null);

        Assert.AreEqual(Equipment.EquipmentType.Civilian, target.ItemEquipmentType,
            "A slot filled from its own template equipment should carry that equipment's type.");
    }

    [TestMethod]
    public void Fill_FallsBackToBattleGear_LeavesTheTargetsOwnEquipmentTypeAlone()
    {
        RequireGame();
        // The retype defect, pinned. Copying the source type here turns a hero's civilian kit into
        // EquipmentType.Battle, which is what HeroCommissionAdapter did at creation time.
        var target = New(Equipment.EquipmentType.Civilian);
        var battle = New(Equipment.EquipmentType.Battle);

        Patch71_HeroResetEquipmentsGuard.Fill(target, slotSource: null, battleFallback: battle, sharedDefault: null);

        Assert.AreEqual(Equipment.EquipmentType.Civilian, target.ItemEquipmentType,
            "Falling back to battle gear must not retype the slot. Civilian equipment stays civilian.");
    }

    [TestMethod]
    public void Fill_NoSlotEquipmentAndNoBattleGear_LeavesTheTargetUntouched()
    {
        RequireGame();
        var target = New(Equipment.EquipmentType.Stealth);

        Patch71_HeroResetEquipmentsGuard.Fill(target, slotSource: null, battleFallback: null, sharedDefault: null);

        Assert.AreEqual(Equipment.EquipmentType.Stealth, target.ItemEquipmentType,
            "With no source at all the hero keeps the kit they are wearing.");
    }
}
