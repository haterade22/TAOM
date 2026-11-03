using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>The neutral creatures of a Fords of War match, and the ring that shows each camp's border.</summary>
internal interface IFordsCampAdapter
{
    /// <summary>Makes the neutral team, hostile to West and East. False when the mission has no such teams to set it against.</summary>
    bool Prepare();

    /// <summary>Spawns one creature of the camp. False when it could not be made.</summary>
    bool Spawn(string campId, string troopId, Vec3f position);

    /// <summary>Draws the camp's ring on the ground. A camp without a ring still plays.</summary>
    void AddRing(string campId, Vec3f position, float radius);

    /// <summary>Sends the camp's creatures out to fight, or back to <paramref name="home"/>.</summary>
    void SetAlerted(string campId, Vec3f home, bool alerted);

    /// <summary>
    /// Forgets a removed agent when it is a camp creature, and names its camp and, when it was killed by a registered unit, its killer.
    /// False for any other agent. May run off the main thread.
    /// </summary>
    bool TryRemove(object affected, object? affector, bool killed, out string campId, out AgentKey? killer);

    void Clear();
}
