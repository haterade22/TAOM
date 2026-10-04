using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>Wraps <see cref="MBAnimation.IsAnyAnimationLoadingFromDisk"/> (v1.5.3, static).</summary>
public sealed class AnimationLoadingAdapter : IAnimationLoadingAdapter
{
    public bool IsAnyAnimationLoadingFromDisk() => MBAnimation.IsAnyAnimationLoadingFromDisk();
}
