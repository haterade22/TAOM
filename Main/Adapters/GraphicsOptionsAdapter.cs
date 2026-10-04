using System;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Validation;

namespace TAOM.Adapters;

/// <summary>
/// Wraps the static vanilla option stores (ADR-007). The write mirrors the vanilla options screen
/// (<c>OptionsVM.ApplyChangedOptions</c>, v1.5.3): set the value, then <c>NativeOptions.SaveConfig</c>
/// and <c>ManagedOptions.SaveConfig</c>. <c>NumberOfRagDolls</c> is in none of the
/// <c>NativeOptions.Apply</c> flags, so no apply call is needed.
/// </summary>
public sealed class GraphicsOptionsAdapter : IGraphicsOptionsAdapter
{
    private const int MaxOptionIndex = 5;

    // A sanity bound that rejects garbage from the engine store, not the length of any options list.
    private const int MaxRawOptionIndex = 16;

    public int RagdollOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.NumberOfRagDolls), MaxOptionIndex);

    public int CorpseOption => InRange(BannerlordConfig.NumberOfCorpses, MaxOptionIndex);

    public int BattleSizeOption => BannerlordConfig.BattleSize;

    public int TextureQualityOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.TextureQuality), MaxRawOptionIndex);

    public int ShadowmapResolutionOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.ShadowmapResolution), MaxRawOptionIndex);

    public int ParticleDetailOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.ParticleDetail), MaxRawOptionIndex);

    public bool SaveOptions(int ragdollOption, int corpseOption)
    {
        if (InRange(ragdollOption, MaxOptionIndex) < 0 || InRange(corpseOption, MaxOptionIndex) < 0)
            return false;

        NativeOptions.SetConfig(NativeOptions.NativeOptionsType.NumberOfRagDolls, ragdollOption);
        ManagedOptions.SetConfig(ManagedOptions.ManagedOptionsType.NumberOfCorpses, corpseOption);
        var native = NativeOptions.SaveConfig();
        var managed = ManagedOptions.SaveConfig();
        return native == SaveResult.Success && managed == SaveResult.Success;
    }

    // GetRGLConfig hands back a float; (int)NaN is int.MinValue, so finiteness is checked at the cast.
    internal static int ToOptionIndex(float raw, int max) =>
        FiniteFloatValidator.IsFiniteInRange(raw, 0f, max) ? (int)Math.Round(raw) : -1;

    private static int InRange(int value, int max) => value >= 0 && value <= max ? value : -1;
}
