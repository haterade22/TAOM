namespace TAOM.Features.MountReins;

/// <summary>
/// The one decision of the mount reins guard (Patch153, docs/features/mount-reins-guard.md): the engine may build a
/// harness's cloth reins only on a mount whose Monster names a rein skeleton. Native's horse, mule and camel declare one,
/// and the Armory's chariot and horse-skeleton mounts (elk, ram, WildMesh) inherit one; the warg, fell warg, dire wolf and spider
/// Monsters have none, yet every family 1 harness fits them, 45 of them with reins.
/// </summary>
public static class MountReinsRule
{
    /// <summary>True when the mount's Monster declares a rein skeleton for the cloth reins to hang on.</summary>
    public static bool CanBuildReinsCloth(string? monsterReinSkeleton) => !string.IsNullOrEmpty(monsterReinSkeleton);
}
