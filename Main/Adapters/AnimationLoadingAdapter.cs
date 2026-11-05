using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>Wraps <see cref="MBAnimation.IsAnyAnimationLoadingFromDisk"/> (v1.4.8: MBAnimation.cs:143, static).</summary>
public sealed class AnimationLoadingAdapter : IAnimationLoadingAdapter
{
    public bool IsAnyAnimationLoadingFromDisk() => MBAnimation.IsAnyAnimationLoadingFromDisk();
}
