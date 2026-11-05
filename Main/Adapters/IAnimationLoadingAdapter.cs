namespace TAOM.Adapters;

/// <summary>The engine's on-demand animation clip loader, as far as the clip memory probe needs it (ADR-007).</summary>
public interface IAnimationLoadingAdapter
{
    /// <summary>True while any on-demand clip is being loaded from disk.</summary>
    bool IsAnyAnimationLoadingFromDisk();
}
