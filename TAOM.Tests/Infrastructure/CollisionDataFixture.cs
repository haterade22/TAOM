using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// An <see cref="AttackCollisionData"/> carrying the members a damage-model test needs. Outside a collision only the engine's
/// 37-argument debug factory (<c>GetAttackCollisionDataForDebugPurpose</c>) builds one; writing just the members a test sets
/// keeps each test's intent visible. <c>ChargeVelocity</c> and <c>AffectorWeaponSlotOrMissileIndex</c> are get-only auto-properties and
/// <c>IsColliderAgent</c> reads the private <c>_isColliderAgent</c> (v1.5.4 <c>AttackCollisionData.cs:20,82,108,138</c>),
/// so those fields are written through a boxed copy, and a renamed field fails here, loudly; <c>InflictedDamage</c> is a
/// public field. Reading the result runs engine getters, so a caller is <c>RequiresGame</c>.
/// </summary>
internal static class CollisionDataFixture
{
    public static AttackCollisionData With(float chargeVelocity = 0f, int inflictedDamage = 0, int weaponSlot = 0, bool isColliderAgent = false)
    {
        object boxed = default(AttackCollisionData);
        SetField(boxed, BackingField(nameof(AttackCollisionData.ChargeVelocity)), chargeVelocity);
        SetField(boxed, BackingField(nameof(AttackCollisionData.AffectorWeaponSlotOrMissileIndex)), weaponSlot);
        SetField(boxed, "_isColliderAgent", isColliderAgent);
        var data = (AttackCollisionData)boxed;
        data.InflictedDamage = inflictedDamage;
        return data;
    }

    private static string BackingField(string property) => $"<{property}>k__BackingField";

    private static void SetField(object boxed, string name, object value)
    {
        var field = typeof(AttackCollisionData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"AttackCollisionData has no field {name}; build this fixture another way");
        field!.SetValue(boxed, value);
    }
}
