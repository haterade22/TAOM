using TaleWorlds.Core;

namespace TAOM.Features.SiegeDismount.Models;

public sealed class MountSnapshot : IMountSnapshot
{
    public static readonly MountSnapshot Empty = new(null, null);

    /// <summary>
    /// Test-only constructor: tracks just StringIds, no full EquipmentElement.
    /// Production code (PlayerMountAdapter) must use the EquipmentElement constructor below
    /// so that ItemModifier (durability, quality bonuses) is preserved through deposit/restore.
    /// </summary>
    public MountSnapshot(string? mountItemId, string? harnessItemId)
    {
        MountItemId = mountItemId;
        HarnessItemId = harnessItemId;
        // EquipmentElements stay default (IsEmpty == true) — the production adapter path is the only
        // path that preserves modifier; tests use mock adapters and don't exercise the full restore.
    }

    /// <summary>
    /// Production constructor: stores full EquipmentElement instances so that
    /// ItemModifier and any cosmetic/quest-item flags survive the round-trip into
    /// inventory and back into the equipment slot.
    /// </summary>
    public MountSnapshot(EquipmentElement mount, EquipmentElement harness)
    {
        Mount = mount;
        Harness = harness;
        MountItemId = mount.Item?.StringId;
        HarnessItemId = harness.Item?.StringId;
    }

    public bool HasMount => !string.IsNullOrEmpty(MountItemId);
    public bool HasHarness => !string.IsNullOrEmpty(HarnessItemId);
    public string? MountItemId { get; }
    public string? HarnessItemId { get; }

    /// <summary>
    /// Full equipment element (incl. modifier). Adapters access this directly to preserve
    /// quality/durability across the dismount/remount cycle. Default-empty when constructed
    /// from string IDs only (test code path).
    /// </summary>
    internal EquipmentElement Mount { get; }
    internal EquipmentElement Harness { get; }
}
