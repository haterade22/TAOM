using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public sealed class AnimationLoadingAdapter : IAnimationLoadingAdapter
{
    public bool IsAnyAnimationLoadingFromDisk() => MBAnimation.IsAnyAnimationLoadingFromDisk();
}
