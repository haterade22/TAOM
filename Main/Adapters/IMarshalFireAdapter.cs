using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>One walk's count of a company's soldiers by draw state: those who draw a bow, those who hold one drawn, those in neither state who stand aside, and the rest.</summary>
internal readonly record struct FireCensus(int Company, int Drawing, int Holding, int Idle = 0, int Aside = 0);

/// <summary>
/// The engine side of the archers' draw hold (build-spec 7.2): it walks the soldiers of the companies that have a rule, gives each the
/// component and turns its callback on, and turns the callback off for a soldier that no longer belongs to such a company. Every member runs
/// on the main thread, from <c>OnMissionTick</c>.
/// </summary>
internal interface IMarshalFireAdapter
{
    /// <summary>True while at least one soldier has the callback on, so a pass with no rule still has to turn them off.</summary>
    bool HasTouched { get; }

    /// <summary>Touches the soldiers of the ruled companies and releases every soldier touched earlier who is not among them. One census per ruled company.</summary>
    IReadOnlyList<FireCensus> Sweep(IReadOnlyList<int> ruled);

    /// <summary>
    /// Turns the callback off for every touched soldier who is still the slot's occupant, resets its state and forgets it. This is the one teardown
    /// that makes native calls, for a fault with the mission still running: it runs on the main thread, from <c>OnMissionTick</c>.
    /// </summary>
    void ReleaseAll();

    /// <summary>Forgets every touched soldier with no native call. The finish of a mission calls it, because the agents go with the mission; after a fault it follows <see cref="ReleaseAll"/>.</summary>
    void Drop();
}
