using TAOM.Features.Spider;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal partial interface ICommandTrialHost
{
    /// <summary>
    /// The player's riders on a spider (the spider Monster id): the living ones among those seen so far, how many of them still sit on a living spider,
    /// and the highest planar speed in metres per second of a living spider that carries one. It reads agents: call it after the in-mission-tick marker.
    /// </summary>
    SpiderReading ReadSpiders();

    /// <summary>Chooses a living rider who sits on a living spider as the target of the next keep; false when there is none. It reads agents: call it after the in-mission-tick marker.</summary>
    bool ChooseSpiderRider();

    /// <summary>Gives every company of the player's team the Stop order, so the army holds where it stands. A formation write: call it after the in-mission-tick marker.</summary>
    void HoldAll();
}
