namespace TAOM.Adapters;

/// <summary>
/// The player's own vanilla Performance options that bear on battle load: Number of Ragdolls
/// (native, <c>engine_config.txt</c>), Number of Corpses and Battle Size (managed,
/// <c>BannerlordConfig.txt</c>). Every value is the option INDEX the options screen shows, 0-5 for
/// ragdolls and corpses, or -1 when the engine hands back something that is not one. Battle Size is
/// passed through raw. It also reports the texture, shadow-map and particle quality options for the
/// tick profiler's <c>[PerfContext]</c> line.
/// </summary>
public interface IGraphicsOptionsAdapter
{
    /// <summary>0-5: 0, 1, 3, 5, 10, Unlimited ragdolls; -1 when unreadable.</summary>
    int RagdollOption { get; }

    /// <summary>0-5: None, 25, 75, 125, 250, Unlimited corpses; -1 when unreadable.</summary>
    int CorpseOption { get; }

    /// <summary>The Battle Size option index, raw as stored (0-6), for the log line.</summary>
    int BattleSizeOption { get; }

    /// <summary>Texture Quality: the raw option value the engine stores, rounded; -1 when unreadable.</summary>
    int TextureQualityOption { get; }

    /// <summary>Shadow-map Resolution: the raw option value the engine stores, rounded; -1 when unreadable.</summary>
    int ShadowmapResolutionOption { get; }

    /// <summary>Particle Detail: the raw option value the engine stores, rounded; -1 when unreadable.</summary>
    int ParticleDetailOption { get; }

    /// <summary>
    /// Writes both options and saves both config files, as the vanilla options screen does. False when an
    /// index is out of range or a save reports failure; an engine exception propagates to the caller.
    /// </summary>
    bool SaveOptions(int ragdollOption, int corpseOption);
}
