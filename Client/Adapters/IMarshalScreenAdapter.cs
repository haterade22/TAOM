using System.Collections.Generic;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Adapters;

/// <summary>
/// What Marshal's Word asks of the mission screen and the scene (build-spec 5.5): a message, and a pool of vanilla order flags that show the
/// road ahead of the selected companies. A client type, because the flags are scene entities drawn for the player's own screen. Every member
/// runs on the main thread; <see cref="DropFlags"/> makes no native call, because the flags go with the scene.
/// </summary>
internal interface IMarshalScreenAdapter
{
    /// <summary>The mission has a player team (<c>Mission.PlayerTeam</c>), one managed read.</summary>
    bool PlayerTeamExists { get; }

    /// <summary>Shows one of Marshal's Word's messages to the player.</summary>
    void Say(MarshalMessage message);

    /// <summary>Places one flag at each point, growing the pool as needed, and hides the rest.</summary>
    void ShowFlags(IReadOnlyList<MarshalFlagPoint> points);

    /// <summary>Hides every flag.</summary>
    void HideFlags();

    /// <summary>Forgets the pool with no native call: the entities go with the scene.</summary>
    void DropFlags();
}
