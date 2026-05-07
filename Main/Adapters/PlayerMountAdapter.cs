using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Concrete <see cref="IPlayerMountAdapter"/> backed by <c>Hero.MainHero.BattleEquipment</c>.
/// All TaleWorlds types stay inside this class — services see only <see cref="IMountSnapshot"/>.
/// Captures and restores the full <see cref="EquipmentElement"/> (including <c>ItemModifier</c>)
/// so durability and quality bonuses survive the dismount/remount round-trip.
/// </summary>
public class PlayerMountAdapter : IPlayerMountAdapter
{
    public bool HasMount()
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null) return false;
        return !equipment[EquipmentIndex.Horse].IsEmpty;
    }

    public IMountSnapshot Capture()
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null) return MountSnapshot.Empty;

        // Pass the full EquipmentElement (NOT just StringId) so ItemModifier survives the round trip.
        return new MountSnapshot(equipment[EquipmentIndex.Horse], equipment[EquipmentIndex.HorseHarness]);
    }

    public void Clear()
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null) return;

        equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
        equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
    }

    public void Restore(IMountSnapshot snapshot)
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null || snapshot == null) return;

        // The production code path: snapshot is a MountSnapshot with full EquipmentElement
        // captured by Capture(). Restore using the captured EquipmentElement directly so that
        // any ItemModifier (durability state, quality prefix) survives.
        if (snapshot is MountSnapshot concrete)
        {
            if (!concrete.Mount.IsEmpty)
                equipment[EquipmentIndex.Horse] = concrete.Mount;
            if (!concrete.Harness.IsEmpty)
                equipment[EquipmentIndex.HorseHarness] = concrete.Harness;
        }
        // Else: snapshot is a foreign IMountSnapshot impl (no production code path yet) — no-op.
        // Tests use mocked adapters and never reach this method on a real Hero.
    }
}
