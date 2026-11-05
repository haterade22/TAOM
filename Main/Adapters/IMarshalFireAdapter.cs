using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// One count of a company's soldiers by draw state: those who draw a bow (and, of those, the ones whose draw began longer ago than the draw limit), those who hold
/// one drawn, those in neither state who stand aside, the rest, and the soldiers whose attack flag reads off after hold the line cleared it.
/// </summary>
internal readonly record struct FireCensus(int Company, int Drawing, int Holding, int Idle = 0, int Aside = 0, int StaleDrawing = 0, int FlagOff = 0);

/// <summary>
/// One count of a held company's line: who stands in it, the flags read off, the slot distances and anchors, the wounds and the strike back, with the order and the first reason a soldier
/// did not qualify, and how many steered soldiers ended a hold read closer to their slot or farther from it since the previous count.
/// </summary>
internal readonly record struct HoldCensus(int Company, int Units, int Qualified, int FlagOff, int Anchored, int Far, int WoundedEver, int Struck, int StruckSettled,
    int StruckFlagOn, float SlotMax, float SlotMean, string Order, string WhyNot, int Closer = 0, int Farther = 0, int Steered = 0, int Loose = 0);

/// <summary>
/// The engine side of the archers' draw hold (build-spec 7.2): it walks the soldiers of the companies that have a rule, gives each the
/// component and turns its callback on, and turns the callback off for a soldier that no longer belongs to such a company. Every member runs
/// on the main thread, from <c>OnMissionTick</c>.
/// </summary>
internal interface IMarshalFireAdapter
{
    /// <summary>True while at least one soldier has the callback on, so a pass with no rule still has to turn them off.</summary>
    bool HasTouched { get; }

    /// <summary>The soldiers' callback threw and stays off for the process, so a held line must act as off and the mission tick pays what it took.</summary>
    bool CallbackFaulted { get; }

    /// <summary>
    /// Touches the soldiers of the ruled companies and releases every soldier touched earlier who is not among them. A company in <paramref name="held"/> has
    /// its soldiers' attack flag cleared, and any other touched soldier gets it back. One census per ruled company.
    /// </summary>
    IReadOnlyList<FireCensus> Sweep(IReadOnlyList<int> ruled, IReadOnlyList<int> held, double now);

    /// <summary>The 3 Hz pass of hold the line: applies each touched held soldier's flag and firing order to the line and counts it, one census per held company, adding no component.</summary>
    IReadOnlyList<HoldCensus> Hold(IReadOnlyList<int> held, double now);

    /// <summary>The cheap count of the Massed pass: the soldiers already touched, by company, with no component added and no flag written. A company with none is left out.</summary>
    IReadOnlyList<FireCensus> Count(IReadOnlyList<int> companies, double now);

    /// <summary>
    /// Turns the callback off for every touched soldier who is still the slot's occupant, resets its state and forgets it. This is the one teardown
    /// that makes native calls, for a fault with the mission still running: it runs on the main thread, from <c>OnMissionTick</c>.
    /// </summary>
    void ReleaseAll();

    /// <summary>Forgets every touched soldier with no native call. The finish of a mission calls it, because the agents go with the mission; after a fault it follows <see cref="ReleaseAll"/>.</summary>
    void Drop();
}
